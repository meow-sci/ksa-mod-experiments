using System;
using System.Reflection;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Floating ImGui editor window for the Space Tape part editor.
/// Renders toolbar, part ID, subpart hierarchy, transform properties, game data, and save controls.
/// </summary>
public sealed class PartEditorUi
{
    public bool WindowOpen { get; set; }

    private readonly ImInputString _partIdInput = new ImInputString(128);
    private readonly ImInputString _instanceIdInput = new ImInputString(128);
    private readonly ImInputString _displayNameInput = new ImInputString(256);

    // Change-detection for input buffer sync (avoids overwriting in-progress edits)
    private string _lastKnownPartId = "";
    private int _lastKnownPlacementIndex = -2;
    private string _lastKnownInstanceId = "";
    private string _lastKnownDisplayName = "";

    private string? _saveStatusMessage;
    private float4 _saveStatusColor;

    private static readonly string[] KnownEditorTags =
        { "Command", "Structural", "Cargo", "Propulsion", "Aero",
          "Electrical", "Thermal", "Science", "Coupling", "Ground", "Payload" };

    private int _selectedNewTagIndex;

    // Reflection: invalidate Part's cached transform matrix after manual edits
    private static readonly FieldInfo? MatrixAsmbField =
        typeof(Part).GetField("_matrixAsmb", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? MatrixAsmb2ParentField =
        typeof(Part).GetField("_matrixAsmb2Parent", BindingFlags.NonPublic | BindingFlags.Instance);

    public void RenderEditorWindow(
        PartEditorController controller,
        PartEditorScene scene,
        PartEditorGizmos gizmos,
        SubPartCatalog catalog,
        PartModWriter writer)
    {
        if (!WindowOpen) return;

        ImGui.SetNextWindowSize(new float2(440, 700), ImGuiCond.FirstUseEver);
        bool open = WindowOpen;
        if (ImGui.Begin("Space Tape — Part Editor##st_editor", ref open))
        {
            RenderToolbar(controller, gizmos);
            ImGui.Spacing();
            RenderPartIdSection(controller);
            ImGui.Spacing();
            RenderHierarchySection(controller, scene);
            ImGui.Spacing();
            RenderPropertiesSection(controller, scene);
            ImGui.Spacing();
            RenderGameDataSection(controller);
            ImGui.Spacing();
            RenderSaveSection(controller, writer);
        }
        ImGui.End();
        WindowOpen = open;
    }

    // -------------------------------------------------------------------------
    // Toolbar
    // -------------------------------------------------------------------------

    private void RenderToolbar(PartEditorController controller, PartEditorGizmos gizmos)
    {
        ImGui.SeparatorText("Transform Mode");

        if (ImGui.RadioButton(" Translate ", gizmos.ActiveMode == PartEditorGizmos.GizmoMode.Translate))
            gizmos.ActiveMode = PartEditorGizmos.GizmoMode.Translate;
        ImGui.SameLine();
        if (ImGui.RadioButton(" Rotate ", gizmos.ActiveMode == PartEditorGizmos.GizmoMode.Rotate))
            gizmos.ActiveMode = PartEditorGizmos.GizmoMode.Rotate;
        ImGui.SameLine();
        if (ImGui.RadioButton(" Scale ", gizmos.ActiveMode == PartEditorGizmos.GizmoMode.Scale))
            gizmos.ActiveMode = PartEditorGizmos.GizmoMode.Scale;

        ImGui.Spacing();

        if (!controller.CanUndo) ImGui.BeginDisabled();
        if (ImGui.Button(" Undo ")) controller.Undo();
        if (!controller.CanUndo) ImGui.EndDisabled();

        ImGui.SameLine();

        if (!controller.CanRedo) ImGui.BeginDisabled();
        if (ImGui.Button(" Redo ")) controller.Redo();
        if (!controller.CanRedo) ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(" New Part "))
        {
            controller.NewPart();
            _saveStatusMessage = null;
            _lastKnownPartId = "";
            _lastKnownPlacementIndex = -2;
        }
    }

    // -------------------------------------------------------------------------
    // Part ID
    // -------------------------------------------------------------------------

    private void RenderPartIdSection(PartEditorController controller)
    {
        ImGui.SeparatorText("Part Identity");

        // Sync buffer when Part ID changes externally (e.g. after NewPart())
        if (controller.CurrentPart.PartId != _lastKnownPartId)
        {
            _partIdInput.SetValue(controller.CurrentPart.PartId.AsSpan());
            _lastKnownPartId = controller.CurrentPart.PartId;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##st_partid_tbl", 2, flags))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##val", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Part ID:");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##st_partid", _partIdInput))
            {
                controller.CurrentPart.PartId = _partIdInput.ToString();
                _lastKnownPartId = controller.CurrentPart.PartId;
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    // -------------------------------------------------------------------------
    // SubPart Hierarchy
    // -------------------------------------------------------------------------

    private void RenderHierarchySection(PartEditorController controller, PartEditorScene scene)
    {
        bool open = ImGui.CollapsingHeader(
            $"SubParts ({controller.CurrentPart.Placements.Count})##st_hier",
            ImGuiTreeNodeFlags.DefaultOpen);
        if (!open) return;

        ImGui.BeginChild("##st_hier_list", new float2(0, 120),
            ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);

        for (int i = 0; i < controller.CurrentPart.Placements.Count; i++)
        {
            var p = controller.CurrentPart.Placements[i];
            bool isSelected = controller.SelectedPlacementIndex == i;
            if (ImGui.Selectable($"{p.InstanceId}  ({p.SubPartTemplateId})##st_h{i}", isSelected))
                controller.SelectedPlacementIndex = i;
        }

        if (controller.CurrentPart.Placements.Count == 0)
            ImGui.TextDisabled("No SubParts placed yet. Pick one from the catalog.");

        ImGui.EndChild();
    }

    // -------------------------------------------------------------------------
    // Properties (transform)
    // -------------------------------------------------------------------------

    private void RenderPropertiesSection(PartEditorController controller, PartEditorScene scene)
    {
        bool open = ImGui.CollapsingHeader("Properties##st_props", ImGuiTreeNodeFlags.DefaultOpen);
        if (!open) return;

        var placement = controller.SelectedPlacement;
        if (placement == null)
        {
            ImGui.TextDisabled("No SubPart selected.");
            return;
        }

        // Sync instance ID buffer when selection changes
        if (controller.SelectedPlacementIndex != _lastKnownPlacementIndex
            || placement.InstanceId != _lastKnownInstanceId)
        {
            _instanceIdInput.SetValue(placement.InstanceId.AsSpan());
            _lastKnownPlacementIndex = controller.SelectedPlacementIndex;
            _lastKnownInstanceId = placement.InstanceId;
        }

        // Instance ID row
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var tableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##st_instid_tbl", 2, tableFlags))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##val", ImGuiTableColumnFlags.WidthStretch, 3f);
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Instance ID:");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##st_instid", _instanceIdInput))
            {
                placement.InstanceId = _instanceIdInput.ToString();
                _lastKnownInstanceId = placement.InstanceId;
            }
            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();

        // Position
        if (ImGui.TreeNodeEx("Position (m)##st_pos", ImGuiTreeNodeFlags.DefaultOpen))
        {
            bool posChanged = false;
            double px = placement.Position.X, py = placement.Position.Y, pz = placement.Position.Z;

            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(4f, 4f));
            if (ImGui.BeginTable("##st_pos_tbl", 4,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("X:");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                posChanged |= ImGui.InputDouble("##px", ref px);
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Y:");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                posChanged |= ImGui.InputDouble("##py", ref py);

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Z:");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                posChanged |= ImGui.InputDouble("##pz", ref pz);

                ImGui.EndTable();
            }
            ImGui.PopStyleVar();

            if (posChanged)
            {
                placement.Position = new double3(px, py, pz);
                SyncPlacementToRuntimePart(controller, scene);
            }
            ImGui.TreePop();
        }

        // Rotation (Euler XYZ degrees)
        if (ImGui.TreeNodeEx("Rotation (deg)##st_rot", ImGuiTreeNodeFlags.DefaultOpen))
        {
            double3 eulerRad = placement.Rotation.NormalizedOrIdentity().ToXyzRadians();
            double rx = eulerRad.X * (180.0 / Math.PI);
            double ry = eulerRad.Y * (180.0 / Math.PI);
            double rz = eulerRad.Z * (180.0 / Math.PI);
            bool rotChanged = false;

            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(4f, 4f));
            if (ImGui.BeginTable("##st_rot_tbl", 4,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("X:");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                rotChanged |= ImGui.InputDouble("##rx", ref rx);
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Y:");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                rotChanged |= ImGui.InputDouble("##ry", ref ry);

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Z:");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                rotChanged |= ImGui.InputDouble("##rz", ref rz);

                ImGui.EndTable();
            }
            ImGui.PopStyleVar();

            if (rotChanged)
            {
                placement.Rotation = QuaternionEx.CreateFromXyzRadians(
                    new double3(rx * (Math.PI / 180.0), ry * (Math.PI / 180.0), rz * (Math.PI / 180.0)));
                SyncPlacementToRuntimePart(controller, scene);
            }
            ImGui.TreePop();
        }

        // Scale
        if (ImGui.TreeNodeEx("Scale##st_scale", ImGuiTreeNodeFlags.DefaultOpen))
        {
            bool scaleChanged = false;
            double sx = placement.Scale.X, sy = placement.Scale.Y, sz = placement.Scale.Z;

            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(4f, 4f));
            if (ImGui.BeginTable("##st_scale_tbl", 4,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("X:");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                scaleChanged |= ImGui.InputDouble("##sx", ref sx);
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Y:");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                scaleChanged |= ImGui.InputDouble("##sy", ref sy);

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Z:");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                scaleChanged |= ImGui.InputDouble("##sz", ref sz);

                ImGui.EndTable();
            }
            ImGui.PopStyleVar();

            if (scaleChanged)
            {
                placement.Scale = new double3(
                    Math.Max(sx, 0.001), Math.Max(sy, 0.001), Math.Max(sz, 0.001));
                SyncPlacementToRuntimePart(controller, scene);
            }
            ImGui.TreePop();
        }

        // Delete / Duplicate
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(new float4(0.7f, 0.1f, 0.1f, 1f)));
        if (ImGui.Button(" Delete ##st_del"))
        {
            controller.RemoveSelected();
            scene.SyncParts(controller.CurrentPart);
            _lastKnownPlacementIndex = -2;
        }
        ImGui.PopStyleColor();

        ImGui.SameLine();
        if (ImGui.Button(" Duplicate ##st_dup"))
        {
            controller.DuplicateSelected();
            scene.SyncParts(controller.CurrentPart);
            _lastKnownPlacementIndex = -2;
        }
    }

    // -------------------------------------------------------------------------
    // Runtime sync helper (updates the live Part without recreating all parts)
    // -------------------------------------------------------------------------

    private void SyncPlacementToRuntimePart(PartEditorController controller, PartEditorScene scene)
    {
        int idx = controller.SelectedPlacementIndex;
        if (idx < 0 || idx >= scene.EditorParts.Count) return;

        var placement = controller.CurrentPart.Placements[idx];
        var part = scene.EditorParts[idx];
        part.PositionParentAsmb = placement.Position;
        part.Asmb2ParentAsmb = placement.Rotation;
        part.Scale = placement.Scale;

        // Invalidate cached matrices so the renderer picks up the updated transform
        MatrixAsmbField?.SetValue(part, double4x4.Identity);
        MatrixAsmb2ParentField?.SetValue(part, double4x4.Identity);
    }

    // -------------------------------------------------------------------------
    // Game Data
    // -------------------------------------------------------------------------

    private void RenderGameDataSection(PartEditorController controller)
    {
        if (!ImGui.CollapsingHeader("Game Data##st_gd")) return;

        var gd = controller.CurrentPart.GameData;

        // Sync display name buffer when it changes externally
        if (gd.DisplayName != _lastKnownDisplayName)
        {
            _displayNameInput.SetValue(gd.DisplayName.AsSpan());
            _lastKnownDisplayName = gd.DisplayName;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##st_gd_tbl", 2,
            ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##val", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Display Name:");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##st_dn", _displayNameInput))
            {
                gd.DisplayName = _displayNameInput.ToString();
                _lastKnownDisplayName = gd.DisplayName;
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Mass (kg):");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            double mass = gd.CustomMass ?? 0.0;
            if (ImGui.InputDouble("##st_mass", ref mass, 0.5)) gd.CustomMass = mass > 0 ? mass : (double?)null;

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Battery (kWh):");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            double battery = gd.BatteryCapacity ?? 0.0;
            if (ImGui.InputDouble("##st_bat", ref battery, 0.1)) gd.BatteryCapacity = battery > 0 ? battery : (double?)null;

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Generator (W):");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            double gen = gd.GeneratorOutput ?? 0.0;
            if (ImGui.InputDouble("##st_gen", ref gen, 1.0)) gd.GeneratorOutput = gen > 0 ? gen : (double?)null;

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        // Editor Tags
        ImGui.Spacing();
        ImGui.Text("Editor Tags:");
        for (int i = 0; i < gd.EditorTags.Count; i++)
        {
            ImGui.BulletText(gd.EditorTags[i]);
            ImGui.SameLine();
            if (ImGui.SmallButton($" x ##st_tag{i}"))
            {
                gd.EditorTags.RemoveAt(i);
                i--;
            }
        }

        if (gd.EditorTags.Count == 0)
            ImGui.TextDisabled("No tags.");

        ImGui.Spacing();
        ImGui.SetNextItemWidth(160f);
        ImGui.Combo("##st_newtag", ref _selectedNewTagIndex, KnownEditorTags, KnownEditorTags.Length);
        ImGui.SameLine();
        if (ImGui.Button(" Add Tag ##st_addtag"))
        {
            string tag = KnownEditorTags[_selectedNewTagIndex];
            if (!gd.EditorTags.Contains(tag))
                gd.EditorTags.Add(tag);
        }
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    private void RenderSaveSection(PartEditorController controller, PartModWriter writer)
    {
        ImGui.SeparatorText("Save");

        bool canSave = controller.CurrentPart.Placements.Count > 0
                       && !string.IsNullOrWhiteSpace(controller.CurrentPart.PartId);

        if (!canSave)
            ImGui.TextDisabled("Add at least one SubPart and set a Part ID to save.");

        writer.RenderFilePicker();

        ImGui.Spacing();

        if (!canSave) ImGui.BeginDisabled();
        if (ImGui.Button(" Save to Disk ##st_save"))
        {
            bool ok = writer.SavePart(controller.CurrentPart);
            if (ok)
            {
                controller.MarkSaved();
                _saveStatusMessage = "Saved!";
                _saveStatusColor = new float4(0.3f, 1f, 0.3f, 1f);
                Console.WriteLine($"space-tape: Part '{controller.CurrentPart.PartId}' saved.");
            }
            else
            {
                _saveStatusMessage = $"Save failed: {writer.LastError}";
                _saveStatusColor = new float4(1f, 0.3f, 0.3f, 1f);
                Console.WriteLine($"space-tape: Save failed: {writer.LastError}");
            }
        }
        if (!canSave) ImGui.EndDisabled();

        if (_saveStatusMessage != null)
        {
            ImGui.SameLine();
            ImGui.TextColored(_saveStatusColor, _saveStatusMessage);
        }
    }
}
