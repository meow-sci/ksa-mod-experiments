using System;
using System.Collections.Generic;
using System.Reflection;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.GarrysTorchLib;

/// <summary>Stateless core weld computation logic.</summary>
public static class WeldEngine
{
    /// <summary>
    /// Teleports the source vehicle to maintain the weld relative to the target.
    /// Returns false if the weld should be removed (e.g. parent body mismatch).
    /// </summary>
    public static bool UpdateWeld(WeldEntry entry)
    {
        // KSA 2026.9.7.5402 added structural part failure (KSA/PartFailure.cs), which can destroy a
        // vehicle mid-flight with no setting to turn it off — and welded craft are held overlapping
        // the target every frame, which is exactly the contact case that trips it. A destroyed
        // vehicle is disposed but our WeldEntry still references it, so read the flag before
        // touching anything on it and drop the weld instead of throwing out of OnAfterUi.
        if (entry.Source.IsDisposed || entry.Target.IsDisposed)
        {
            Console.WriteLine("garrys-torch: welded vehicle no longer exists (destroyed or recovered), unwelding");
            return false;
        }

        if (entry.Source.Parent != entry.Target.Parent)
        {
            Console.WriteLine("garrys-torch: Parent body mismatch, unwelding");
            return false;
        }

        if (!entry.WeldEnabled)
            return true;

        double3 tgtPosCci = entry.Target.GetPositionCci();
        double3 tgtVelCci = entry.Target.GetVelocityCci();
        doubleQuat tgtBody2Cci = entry.Target.GetBody2Cci();

        // Guard against NaN target state — target vehicle may be mid-physics-blowup
        if (double.IsNaN(tgtPosCci.X) || double.IsNaN(tgtPosCci.Y) || double.IsNaN(tgtPosCci.Z) ||
            double.IsNaN(tgtVelCci.X) || double.IsNaN(tgtVelCci.Y) || double.IsNaN(tgtVelCci.Z))
        {
            Console.WriteLine("garrys-torch: NaN detected in target vehicle state, skipping weld update");
            return true;
        }

        // Normalize target orientation — denormalized quaternion would corrupt offset transform
        tgtBody2Cci = tgtBody2Cci.NormalizedOrZero();
        if (tgtBody2Cci == default)
        {
            Console.WriteLine("garrys-torch: zero/NaN quaternion in target orientation, skipping weld update");
            return true;
        }

        // Determine the anchor position and orientation.
        // When a target part is specified, anchor to that part's live CCI position and orientation.
        // This avoids the CoM drift issue (vehicle CoM shifts as fuel burns) and allows
        // the weld to track robotics-moved parts naturally.
        double3 anchorPosCci;
        doubleQuat anchorBody2Cci;

        if (entry.TargetPart != null)
        {
            // Offset from vehicle CoM to the target part, in vehicle assembly space
            double3 partOffset = entry.TargetPart.PositionVehicleAsmb - entry.Target.CenterOfMassAsmb;
            anchorPosCci = tgtPosCci + partOffset.Transform(tgtBody2Cci);
            // Compose part-in-vehicle rotation with vehicle-to-CCI to get part world orientation
            anchorBody2Cci = doubleQuat.Concatenate(entry.TargetPart.Asmb2VehicleAsmb, tgtBody2Cci).NormalizedOrZero();
            if (anchorBody2Cci == default) anchorBody2Cci = tgtBody2Cci;
        }
        else
        {
            // Legacy path: anchor to vehicle CoM
            anchorPosCci = tgtPosCci;
            anchorBody2Cci = tgtBody2Cci;
        }

        double3 offsetCci = new double3(entry.Position.X, entry.Position.Y, entry.Position.Z).Transform(anchorBody2Cci);
        double3 newSrcPosCci = anchorPosCci + offsetCci;
        double3 newSrcVelCci = tgtVelCci;

        doubleQuat cci2Cce = entry.Source.Parent.GetCci2Cce();
        doubleQuat newSrcBody2Cce;
        double3 newBodyRates;

        if (entry.LockRotation)
        {
            // Apply Euler rotation relative to the anchor's orientation
            doubleQuat deltaRot = EulerDegreesToQuat(entry.Rotation.X, entry.Rotation.Y, entry.Rotation.Z);
            doubleQuat newSrcBody2Cci = doubleQuat.Concatenate(deltaRot, anchorBody2Cci);
            newSrcBody2Cce = doubleQuat.Concatenate(newSrcBody2Cci, cci2Cce).NormalizedOrZero();
            newBodyRates = entry.Target.BodyRates;
        }
        else
        {
            // Rotation unlocked — preserve source's current orientation and body rates
            doubleQuat srcBody2Cci = entry.Source.GetBody2Cci().NormalizedOrZero();
            newSrcBody2Cce = doubleQuat.Concatenate(srcBody2Cci, cci2Cce).NormalizedOrZero();
            newBodyRates = entry.Source.BodyRates;

            // Guard against NaN body rates that can feed back into physics
            if (double.IsNaN(newBodyRates.X) || double.IsNaN(newBodyRates.Y) || double.IsNaN(newBodyRates.Z))
            {
                Console.WriteLine("garrys-torch: NaN detected in body rates, resetting to zero");
                newBodyRates = new double3(0, 0, 0);
            }
        }

        // Stamp the orbit with the time that the just-completed vehicle worker
        // tick advanced to, NOT Universe.GetElapsedTime() (which is the
        // PREVIOUS tick's end time — _lastSimStep doesn't roll forward until
        // the next PrepareFrame's ApplyVehicleSolvers).
        //
        // Vehicle.Teleport calls _kinematicStates.OverwriteFromLeaderAnalytic
        // which sets body.Time = orbit.StateVectors.StateTime. The target
        // vehicle's task.Origin.Time has been propagated forward by the workers
        // we just Wait()-ed on, so if we used the stale GetElapsedSimTime() the
        // source's body.Time would land one frame behind the task's Origin.Time
        // and the worker would log:
        //   "Called SnapToLeader with body time X but origin time Y"
        // (Y - X ≈ dtPlayer * achievedSpeed * simSpeed)
        //
        // GetJobSimStep is the same pure helper the game itself calls in
        // PrepareFrame to build the SimStep that's about to be queued, so this
        // value matches what the next-tick workers will be aligned to.
        UniverseTime tickEndTime = Universe.GetJobSimStep(Program.GetPlayerDeltaTime()).NextTime;

        Orbit newOrbit = Orbit.CreateFromStateCci(
            entry.Source.Parent,
            tickEndTime,
            newSrcPosCci,
            newSrcVelCci,
            entry.Source.Orbit.OrbitLineColor
        );

        entry.Source.Teleport(newOrbit, newSrcBody2Cce, newBodyRates);
        entry.Source.UpdatePerFrameData();
        return true;
    }

    /// <summary>Converts Euler angles (degrees) to a quaternion using ZYX intrinsic convention.</summary>
    public static doubleQuat EulerDegreesToQuat(float pitchDeg, float yawDeg, float rollDeg)
    {
        double pitchRad = pitchDeg * (Math.PI / 180.0);
        double yawRad   = yawDeg   * (Math.PI / 180.0);
        double rollRad  = rollDeg  * (Math.PI / 180.0);

        double cp = Math.Cos(pitchRad / 2), sp = Math.Sin(pitchRad / 2);
        double cy = Math.Cos(yawRad   / 2), sy = Math.Sin(yawRad   / 2);
        double cr = Math.Cos(rollRad  / 2), sr = Math.Sin(rollRad  / 2);

        // Individual axis quaternions: new doubleQuat(x, y, z, w)
        var qPitch = new doubleQuat(sp,  0,  0, cp);
        var qYaw   = new doubleQuat( 0, sy,  0, cy);
        var qRoll  = new doubleQuat( 0,  0, sr, cr);

        // Compose: Yaw * Pitch * Roll (ZYX intrinsic Euler)
        return doubleQuat.Concatenate(doubleQuat.Concatenate(qYaw, qPitch), qRoll);
    }

    /// <summary>Applies a uniform scale to all parts of a vehicle, with special handling for KittenEva.</summary>
    public static void ApplyVehicleScale(Vehicle vehicle, float factor)
    {
        foreach (var part in vehicle.Parts.Parts)
            SetPartScaleRecursive(part, factor);

        // KittenEva renders via CharacterAvatar.Core.Scale (Core.Scale 0.01 = 1:1)
        if (vehicle.GetType().Name == "KittenEva")
        {
            try
            {
                var renderable = ReflectionHelpers.GetFieldValue(vehicle, "_renderable");
                if (renderable == null) return;

                var avatar = ReflectionHelpers.GetFieldValue(renderable, "_characterAvatar");
                if (avatar == null) return;

                var allFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var coreField = avatar.GetType().GetField("Core", allFlags);
                var core = coreField?.GetValue(avatar);
                if (core == null) return;

                var scaleField = core.GetType().GetField("Scale", allFlags);
                var scaleProp  = core.GetType().GetProperty("Scale", allFlags);

                if (scaleField != null && scaleField.FieldType == typeof(float))
                {
                    scaleField.SetValue(core, factor * 0.01f);
                    coreField!.SetValue(avatar, core);
                }
                else if (scaleProp != null && scaleProp.PropertyType == typeof(float))
                {
                    scaleProp.SetValue(core, factor * 0.01f);
                    coreField!.SetValue(avatar, core);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"garrys-torch: KittenEva scale error: {ex.Message}");
            }
        }
    }

    /// <summary>Recursively sets uniform scale on a part and all its sub-parts.</summary>
    public static void SetPartScaleRecursive(Part part, float factor)
    {
        part.Scale = new double3(factor, factor, factor);
        foreach (var sub in part.SubParts)
            SetPartScaleRecursive(sub, factor);
    }

    /// <summary>
    /// Returns welds sorted so that a target is always processed before its source.
    /// If a cycle is detected, the original order is returned unchanged.
    /// </summary>
    public static List<WeldEntry> TopologicalSort(List<WeldEntry> welds)
    {
        var inDegree = new Dictionary<WeldEntry, int>();
        var adj = new Dictionary<WeldEntry, List<WeldEntry>>();

        foreach (var w in welds)
        {
            inDegree[w] = 0;
            adj[w] = new List<WeldEntry>();
        }

        foreach (var x in welds)
        {
            foreach (var y in welds)
            {
                if (x.Source == y.Target)
                {
                    adj[x].Add(y);
                    inDegree[y]++;
                }
            }
        }

        var queue = new Queue<WeldEntry>();
        foreach (var w in welds)
            if (inDegree[w] == 0)
                queue.Enqueue(w);

        var sorted = new List<WeldEntry>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);
            foreach (var neighbor in adj[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        if (sorted.Count == welds.Count)
            return sorted;

        Console.WriteLine("garrys-torch: TopologicalSort: cycle detected, leaving order as-is.");
        return new List<WeldEntry>(welds);
    }
}
