using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Manages the isolated 3D editing space for the Part editor.
/// Creates a VehicleEditingSpace far from celestial bodies, manages camera transitions,
/// and renders the origin axis indicator gizmo.
/// </summary>
public sealed class PartEditorScene : IDisposable
{
    private VehicleEditingSpace? _editingSpace;
    private GenericGizmo? _originGizmo;
    private IFollowable? _savedFollowing;
    private readonly List<Part> _editorParts = new();

    /// <summary>The currently active PartEditorScene instance, or null when no editor is open. Read by the render patch.</summary>
    public static PartEditorScene? Current { get; private set; }

    public bool IsActive => _editingSpace != null;

    public VehicleEditingSpace? EditingSpace => _editingSpace;

    public IReadOnlyList<Part> EditorParts => _editorParts;

    /// <summary>
    /// Opens the editing scene: creates the VehicleEditingSpace, saves camera state,
    /// and moves the camera to the editing space.
    /// </summary>
    public void Enter()
    {
        if (IsActive)
        {
            Console.WriteLine("space-tape: PartEditorScene.Enter() called while already active");
            return;
        }

        if (Universe.CurrentSystem == null)
        {
            Console.WriteLine("space-tape: PartEditorScene.Enter() - no celestial system loaded");
            return;
        }

        try
        {
            // Position far from everything (10x sun radius along Z, same as vehicle editor)
            double sunRadius = Universe.CurrentSystem.GetWorldSun()?.MeanRadius ?? 696_000_000.0;
            double3 positionEcl = new double3(0, 0, 10.0 * sunRadius);
            _editingSpace = new VehicleEditingSpace(positionEcl, doubleQuat.Identity, 10.0, null);

            // Create origin gizmo
            _originGizmo = new GenericGizmo(
                ModLibrary.Get<MeshReference>("Box"),
                GenericGizmo.Static.GenericGizmoRenderData,
                3);

            // Save current following target for restoration later
            _savedFollowing = Program.GetCamera().Following;

            // Set camera to orbit the editing space
            Program.SetCameraMode(CameraMode.Orbit);
            Program.MainViewport.MapCamera.SetFollow(_editingSpace, tidalLocking: false, changeControl: true, alert: false);
            Program.MainViewport.BaseCamera.SetFollow(_editingSpace, tidalLocking: false, changeControl: true, alert: false);
            Program.GetHoveredCamera().SetFollow(_editingSpace, tidalLocking: false, changeControl: true, alert: false);

            Current = this;
            Console.WriteLine("space-tape: Part editor scene entered.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"space-tape: PartEditorScene.Enter() failed: {ex}");
            _editingSpace = null;
            _savedFollowing = null;
        }
    }

    /// <summary>
    /// Closes the editing scene and restores the camera to its previous following target.
    /// </summary>
    public void Exit()
    {
        if (!IsActive) return;

        try
        {
            // Restore camera to what it was following before we entered
            IFollowable? fallback = _savedFollowing
                ?? Program.ControlledVehicle as IFollowable
                ?? Universe.CurrentSystem?.GetWorldSun() as IFollowable;

            if (fallback != null)
            {
                Program.MainViewport.MapCamera.SetFollow(fallback, tidalLocking: false, changeControl: true, alert: false);
                Program.MainViewport.BaseCamera.SetFollow(fallback, tidalLocking: false, changeControl: true, alert: false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"space-tape: PartEditorScene.Exit() camera restore failed: {ex}");
        }
        finally
        {
            _editorParts.Clear();
            Current = null;
            _originGizmo?.Dispose();
            _originGizmo = null;
            _editingSpace = null;
            _savedFollowing = null;
        }

        Console.WriteLine("space-tape: Part editor scene exited.");
    }

    /// <summary>
    /// Updates the origin axis gizmo for the current viewport.
    /// Must be called once per frame when the editor is active.
    /// </summary>
    public void UpdateGizmo(Viewport viewport)
    {
        if (!IsActive || _originGizmo == null || _editingSpace == null)
        {
            if (_originGizmo != null)
            {
                // Gizmo exists but editor not active — hide all segments
                DeactivateGizmoSegments(viewport);
            }
            return;
        }

        try
        {
            Camera camera = viewport.GetCamera();
            double4x4 matrix = _editingSpace.GetMatrixAsmb2Ego(camera);
            double3 originEgo = double3.Zero.Transform(matrix);

            GenericGizmo.PerSegmentData[] seg = _originGizmo.GetSegmentDataByViewport(viewport);

            // X axis — red, elongated in X
            seg[0].Active = true;
            seg[0].PositionEgo = originEgo;
            seg[0].Body2Cce = doubleQuat.Identity;
            seg[0].Scale = new double3(0.5, 0.02, 0.02);
            seg[0].Color = new double4(1.0, 0.0, 0.0, 0.8);

            // Y axis — green, elongated in Y
            seg[1].Active = true;
            seg[1].PositionEgo = originEgo;
            seg[1].Body2Cce = doubleQuat.Identity;
            seg[1].Scale = new double3(0.02, 0.5, 0.02);
            seg[1].Color = new double4(0.0, 1.0, 0.0, 0.8);

            // Z axis — blue, elongated in Z
            seg[2].Active = true;
            seg[2].PositionEgo = originEgo;
            seg[2].Body2Cce = doubleQuat.Identity;
            seg[2].Scale = new double3(0.02, 0.02, 0.5);
            seg[2].Color = new double4(0.0, 0.0, 1.0, 0.8);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"space-tape: UpdateGizmo error: {ex.Message}");
        }
    }

    private void DeactivateGizmoSegments(Viewport viewport)
    {
        if (_originGizmo == null) return;
        GenericGizmo.PerSegmentData[] seg = _originGizmo.GetSegmentDataByViewport(viewport);
        for (int i = 0; i < 3; i++)
            seg[i].Active = false;
    }

    /// <summary>Returns the assembly-space to eye-space matrix for the current editing space and camera.</summary>
    public double4x4 GetMatrixAsmb2Ego(Viewport viewport)
        => _editingSpace?.GetMatrixAsmb2Ego(viewport.GetCamera()) ?? double4x4.Identity;

    /// <summary>
    /// Synchronises the internal list of runtime <see cref="Part"/> instances to match the
    /// placements in <paramref name="editingPart"/>. Call whenever the placement list changes.
    /// </summary>
    public void SyncParts(EditingPart editingPart)
    {
        _editorParts.Clear();
        foreach (var placement in editingPart.Placements)
        {
            _editorParts.Add(CreatePartFromPlacement(placement));
        }
    }

    private static Part CreatePartFromPlacement(SubPartPlacement placement)
    {
        PartTemplate template = ModLibrary.Get<PartTemplate>(placement.SubPartTemplateId);
        var part = new Part(placement.InstanceId, template);
        part.PositionParentAsmb = placement.Position;
        part.Asmb2ParentAsmb = placement.Rotation;
        part.Scale = placement.Scale;
        // Populate PartTree.Modules so UpdateRenderData can find PartModelModule
        PartTree.CreateFromNewPartTree(part);
        return part;
    }

    public void Dispose()
    {
        if (IsActive) Exit();
        _originGizmo?.Dispose();
        _originGizmo = null;
    }
}
