using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace MeowSci.FlexoLib.Editor;

public sealed class FlexoEditorScene : IDisposable
{
    private VehicleEditingSpace? _editingSpace;
    private GenericGizmo? _originGizmo;
    private IFollowable? _savedFollowing;
    private readonly List<Part> _editorParts = new();

    public static FlexoEditorScene? Current { get; private set; }

    public bool IsActive => _editingSpace != null;
    public VehicleEditingSpace? EditingSpace => _editingSpace;
    public IReadOnlyList<Part> EditorParts => _editorParts;

    public bool OriginVisible { get; set; } = true;
    public float OriginAlpha { get; set; } = 0.8f;
    public float OriginSize { get; set; } = 1.0f;

    public void Enter()
    {
        if (IsActive)
        {
            Console.WriteLine("flexo: Editor scene already active");
            return;
        }

        if (Universe.CurrentSystem == null)
        {
            Console.WriteLine("flexo: No celestial system loaded");
            return;
        }

        try
        {
            double sunRadius = Universe.CurrentSystem.GetWorldSun()?.MeanRadius ?? 696_000_000.0;
            double3 positionEcl = new double3(0, 0, 10.0 * sunRadius);
            _editingSpace = new VehicleEditingSpace(positionEcl, doubleQuat.Identity, 10.0, null);

            _originGizmo = new GenericGizmo(
                ModLibrary.Get<MeshReference>("Box"),
                GenericGizmo.Static.GenericGizmoRenderData,
                3);

            _savedFollowing = Program.GetCamera().Following;

            Program.SetCameraMode(CameraMode.Orbit);
            Program.MainViewport.MapCamera.SetFollow(_editingSpace, tidalLocking: false, changeControl: true, alert: false);
            Program.MainViewport.BaseCamera.SetFollow(_editingSpace, tidalLocking: false, changeControl: true, alert: false);
            Program.GetHoveredCamera().SetFollow(_editingSpace, tidalLocking: false, changeControl: true, alert: false);

            Current = this;
            Console.WriteLine("flexo: Editor scene entered");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Editor scene enter failed: {ex}");
            _editingSpace = null;
            _savedFollowing = null;
        }
    }

    public void Exit()
    {
        if (!IsActive) return;

        try
        {
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
            Console.WriteLine($"flexo: Camera restore failed: {ex}");
        }
        finally
        {
            ClearEditorParts();
            Current = null;
            _originGizmo?.Dispose();
            _originGizmo = null;
            _editingSpace = null;
            _savedFollowing = null;
        }

        Console.WriteLine("flexo: Editor scene exited");
    }

    public void LoadVehicleParts(Vehicle vehicle)
    {
        ClearEditorParts();

        foreach (var part in vehicle.Parts.Parts)
        {
            try
            {
                var editorPart = ClonePart(part);
                _editorParts.Add(editorPart);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"flexo: Failed to clone part {part.Template.Id}: {ex.Message}");
            }
        }

        Console.WriteLine($"flexo: Loaded {_editorParts.Count} parts into editor scene");
    }

    private static Part ClonePart(Part original)
    {
        var part = new Part(original.Id, original.Template);
        part.PositionParentAsmb = original.PositionParentAsmb;
        part.Asmb2ParentAsmb = original.Asmb2ParentAsmb;
        part.Scale = original.Scale;
        PartTree.CreateFromNewPartTree(part);
        EnsureMeshViewModule(part);
        return part;
    }

    private static void EnsureMeshViewModule(Part part)
    {
        if (!part.Modules.Get<MeshViewModule>().IsEmpty)
            return;

        Span<PartModelModule> partModels = part.Modules.Get<PartModelModule>();
        if (partModels.IsEmpty)
            return;

        MeshReference? renderMesh = partModels[0].PartModel?.Template?.Mesh;
        if (renderMesh == null || renderMesh.PositionCompare is not { Length: > 0 } || renderMesh.BoundingSphereRadius <= 0)
            return;

        var module = new MeshViewModule(part.Template.Id, renderMesh) { Parent = part };
        part.Modules.Add(module);
    }

    public double4x4 GetMatrixAsmb2Ego(Viewport viewport)
        => _editingSpace?.GetMatrixAsmb2Ego(viewport.GetCamera()) ?? double4x4.Identity;

    public void UpdateGizmo(Viewport viewport)
    {
        if (!IsActive || _originGizmo == null || _editingSpace == null)
        {
            if (_originGizmo != null) DeactivateGizmoSegments(viewport);
            return;
        }

        if (!OriginVisible)
        {
            DeactivateGizmoSegments(viewport);
            return;
        }

        try
        {
            Camera camera = viewport.GetCamera();
            double4x4 matrix = _editingSpace.GetMatrixAsmb2Ego(camera);
            double3 originEgo = double3.Zero.Transform(matrix);
            double a = OriginAlpha;
            double s = OriginSize;

            GenericGizmo.PerSegmentData[] seg = _originGizmo.GetSegmentDataByViewport(viewport);

            seg[0].Active = true;
            seg[0].PositionEgo = originEgo;
            seg[0].Body2Cce = doubleQuat.Identity;
            seg[0].Scale = new double3(0.5 * s, 0.02 * s, 0.02 * s);
            seg[0].Color = new double4(1.0, 0.0, 0.0, a);

            seg[1].Active = true;
            seg[1].PositionEgo = originEgo;
            seg[1].Body2Cce = doubleQuat.Identity;
            seg[1].Scale = new double3(0.02 * s, 0.5 * s, 0.02 * s);
            seg[1].Color = new double4(0.0, 1.0, 0.0, a);

            seg[2].Active = true;
            seg[2].PositionEgo = originEgo;
            seg[2].Body2Cce = doubleQuat.Identity;
            seg[2].Scale = new double3(0.02 * s, 0.02 * s, 0.5 * s);
            seg[2].Color = new double4(0.0, 0.0, 1.0, a);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: UpdateGizmo error: {ex.Message}");
        }
    }

    private void DeactivateGizmoSegments(Viewport viewport)
    {
        if (_originGizmo == null) return;
        GenericGizmo.PerSegmentData[] seg = _originGizmo.GetSegmentDataByViewport(viewport);
        for (int i = 0; i < 3; i++)
            seg[i].Active = false;
    }

    private void ClearEditorParts()
    {
        foreach (var part in _editorParts)
        {
            part.Highlighted = false;
            part.Selected = false;
        }
        _editorParts.Clear();
    }

    public void Dispose()
    {
        if (IsActive) Exit();
        _originGizmo?.Dispose();
        _originGizmo = null;
    }
}
