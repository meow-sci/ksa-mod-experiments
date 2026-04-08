using System;
using System.Collections.Generic;
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

    // Hot-reload spike state
    private string? _hotReloadMessage;
    private bool _hotReloadSuccess;

    private static readonly string[] KnownEditorTags =
        { "Command", "Structural", "Cargo", "Propulsion", "Aero",
          "Electrical", "Thermal", "Science", "Coupling", "Ground", "Payload" };

    private int _selectedNewTagIndex;

    // Import From Game state
    private readonly PartCatalog _gameParts = new();
    private int _selectedGamePartIndex = -1;
    private readonly ImInputString _gamePartFilter = new ImInputString(128);
    private List<int> _filteredGamePartIndices = new();
    private string? _importStatusMessage;
    private float4 _importStatusColor;

    // Transform options
    private bool _gridModeEnabled;
    private float _gridStep = 0.05f;
    private bool _rotSnapEnabled;
    private float _rotSnapDeg = 15f;

    // Load section state
    private List<(string partId, string fileName)> _savedParts = new();
    private int _selectedSavedPartIndex = -1;
    private string? _loadStatusMessage;
    private float4 _loadStatusColor;

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
            RenderToolbar(controller, gizmos, scene);
            ImGui.Spacing();
            RenderLoadSection(controller, scene, writer);
            ImGui.Spacing();
            RenderImportSection(controller, scene);
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

    private void RenderToolbar(PartEditorController controller, PartEditorGizmos gizmos, PartEditorScene scene)
    {
        ImGui.SeparatorText("Active Gizmo");

        if (ImGui.RadioButton(" None ", gizmos.ActiveMode == PartEditorGizmos.GizmoMode.None))
            gizmos.ActiveMode = PartEditorGizmos.GizmoMode.None;
        ImGui.SameLine();
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

        ImGui.SeparatorText("Transform Options");

        ImGui.Checkbox("Grid##st_grid", ref _gridModeEnabled);
        ImGui.SameLine(0, 8);
        if (!_gridModeEnabled) ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(120f);
        ImGui.DragFloat("##st_gridstep", ref _gridStep, 0.001f, 0.001f, 10f, "%.4f");
        if (!_gridModeEnabled) ImGui.EndDisabled();

        ImGui.Checkbox("Snap##st_rotsnap", ref _rotSnapEnabled);
        ImGui.SameLine(0, 8);
        if (!_rotSnapEnabled) ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(120f);
        ImGui.DragFloat("##st_rotsnapdeg", ref _rotSnapDeg, 0.5f, 0.5f, 90f, "%.1f°");
        if (!_rotSnapEnabled) ImGui.EndDisabled();
        ImGui.SeparatorText("Origin Marker");

        bool originVisible = scene.OriginVisible;
        ImGui.Checkbox("Visible##st_origin", ref originVisible);
        scene.OriginVisible = originVisible;
        if (originVisible)
        {
            ImGui.SameLine(0, 8);
            float originAlpha = scene.OriginAlpha;
            ImGui.SetNextItemWidth(120f);
            ImGui.DragFloat("Alpha##st_origin_alpha", ref originAlpha, 0.01f, 0f, 1f, "%.2f");
            scene.OriginAlpha = originAlpha;
        }    }

    // -------------------------------------------------------------------------
    // Load existing part
    // -------------------------------------------------------------------------

    private void RenderLoadSection(PartEditorController controller, PartEditorScene scene, PartModWriter writer)
    {
        if (!ImGui.CollapsingHeader("Load Existing Part##st_load")) return;

        if (ImGui.Button(" Refresh ##st_load_refresh"))
        {
            writer.RefreshFileList();
            _savedParts = writer.ListSavedParts();
            _selectedSavedPartIndex = -1;
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"({_savedParts.Count} part(s) found)");

        if (_savedParts.Count == 0)
        {
            ImGui.TextDisabled("No saved parts. Save a part first.");
        }
        else
        {
            ImGui.SetNextItemWidth(-1);
            string preview = _selectedSavedPartIndex >= 0 && _selectedSavedPartIndex < _savedParts.Count
                ? $"{_savedParts[_selectedSavedPartIndex].partId}  [{_savedParts[_selectedSavedPartIndex].fileName}]"
                : "(select a part)";

            if (ImGui.BeginCombo("##st_load_combo", preview))
            {
                for (int i = 0; i < _savedParts.Count; i++)
                {
                    bool sel = i == _selectedSavedPartIndex;
                    var (partId, fileName) = _savedParts[i];
                    if (ImGui.Selectable($"{partId}  [{fileName}]##st_lp{i}", sel))
                        _selectedSavedPartIndex = i;
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();

            bool canLoad = _selectedSavedPartIndex >= 0;
            if (!canLoad) ImGui.BeginDisabled();
            if (ImGui.Button(" Load Part ##st_load_btn") && canLoad)
            {
                var (partId, fileName) = _savedParts[_selectedSavedPartIndex];
                var loaded = writer.LoadPart(partId, fileName);
                if (loaded != null)
                {
                    controller.LoadPart(loaded);
                    if (scene.IsActive)
                        scene.SyncParts(controller.CurrentPart);
                    writer.CurrentFileName = fileName;
                    _loadStatusMessage = $"Loaded '{partId}' from {fileName}.xml";
                    _loadStatusColor = new float4(0.3f, 1f, 0.3f, 1f);
                    _lastKnownPartId = "";      // force Part ID buffer sync
                    _lastKnownPlacementIndex = -2;
                    _saveStatusMessage = null;
                    Console.WriteLine($"space-tape: Loaded part '{partId}' from '{fileName}'");
                }
                else
                {
                    _loadStatusMessage = $"Failed to load '{partId}' from {fileName}.xml";
                    _loadStatusColor = new float4(1f, 0.3f, 0.3f, 1f);
                    Console.WriteLine($"space-tape: LoadPart failed for '{partId}' in '{fileName}'");
                }
            }
            if (!canLoad) ImGui.EndDisabled();

            if (_loadStatusMessage != null)
            {
                ImGui.SameLine();
                ImGui.TextColored(_loadStatusColor, _loadStatusMessage);
            }
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
        {
            float px = (float)placement.Position.X;
            float py = (float)placement.Position.Y;
            float pz = (float)placement.Position.Z;

            bool posX = false, posY = false, posZ = false;
            float posSpeed = _gridModeEnabled ? _gridStep : 0.001f;
            ImGui.TextDisabled("Position (m)");
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(4f, 4f));
            if (ImGui.BeginTable("##st_pos_tbl", 3,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                posX = ImGui.DragFloat("##px", ref px, posSpeed, 0f, 0f, "%.4f");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                posY = ImGui.DragFloat("##py", ref py, posSpeed, 0f, 0f, "%.4f");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                posZ = ImGui.DragFloat("##pz", ref pz, posSpeed, 0f, 0f, "%.4f");
                ImGui.EndTable();
            }
            ImGui.PopStyleVar();

            if (posX || posY || posZ)
            {
                placement.Position = new double3(px, py, pz);
                SyncPlacementToRuntimePart(controller, scene);
            }
        }

        ImGui.Spacing();

        // Rotation (Euler XYZ degrees)
        {
            double3 eulerRad = placement.Rotation.NormalizedOrIdentity().ToXyzRadians();
            float rx = (float)(eulerRad.X * (180.0 / Math.PI));
            float ry = (float)(eulerRad.Y * (180.0 / Math.PI));
            float rz = (float)(eulerRad.Z * (180.0 / Math.PI));

            bool rotX = false, rotY = false, rotZ = false;
            float rotSpeed = _rotSnapEnabled ? _rotSnapDeg : 0.1f;
            ImGui.TextDisabled("Rotation (\u00b0)");
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(4f, 4f));
            if (ImGui.BeginTable("##st_rot_tbl", 3,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                rotX = ImGui.DragFloat("##rx", ref rx, rotSpeed, -360f, 360f, "%.2f");
                if (rotX && _rotSnapEnabled) rx = MathF.Round(rx / _rotSnapDeg) * _rotSnapDeg;
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                rotY = ImGui.DragFloat("##ry", ref ry, rotSpeed, -360f, 360f, "%.2f");
                if (rotY && _rotSnapEnabled) ry = MathF.Round(ry / _rotSnapDeg) * _rotSnapDeg;
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                rotZ = ImGui.DragFloat("##rz", ref rz, rotSpeed, -360f, 360f, "%.2f");
                if (rotZ && _rotSnapEnabled) rz = MathF.Round(rz / _rotSnapDeg) * _rotSnapDeg;
                ImGui.EndTable();
            }
            ImGui.PopStyleVar();

            if (rotX || rotY || rotZ)
            {
                placement.Rotation = QuaternionEx.CreateFromXyzRadians(
                    new double3(rx * (Math.PI / 180.0), ry * (Math.PI / 180.0), rz * (Math.PI / 180.0)));
                SyncPlacementToRuntimePart(controller, scene);
            }
        }

        ImGui.Spacing();

        // Scale
        {
            float sx = (float)placement.Scale.X;
            float sy = (float)placement.Scale.Y;
            float sz = (float)placement.Scale.Z;

            bool scaleX = false, scaleY = false, scaleZ = false;
            ImGui.TextDisabled("Scale");
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(4f, 4f));
            if (ImGui.BeginTable("##st_scale_tbl", 3,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                scaleX = ImGui.DragFloat("##sx", ref sx, 0.001f, 0f, 0f, "%.4f");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                scaleY = ImGui.DragFloat("##sy", ref sy, 0.001f, 0f, 0f, "%.4f");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                scaleZ = ImGui.DragFloat("##sz", ref sz, 0.001f, 0f, 0f, "%.4f");
                ImGui.EndTable();
            }
            ImGui.PopStyleVar();

            if (scaleX || scaleY || scaleZ)
            {
                placement.Scale = new double3(
                    Math.Max(sx, 0.001), Math.Max(sy, 0.001), Math.Max(sz, 0.001));
                SyncPlacementToRuntimePart(controller, scene);
            }
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

        // --- Basic Info ---
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

        // --- Tank ---
        ImGui.Spacing();
        ImGui.SeparatorText("Tank");
        GameDataEditorUi.RenderTankSection(gd);

        // --- Power ---
        ImGui.Spacing();
        ImGui.SeparatorText("Power");
        GameDataEditorUi.RenderPowerSection(gd);

        // --- Connectors ---
        ImGui.Spacing();
        ImGui.SeparatorText("Connectors");
        GameDataEditorUi.RenderConnectorsSection(gd);

        // --- Coupling ---
        ImGui.Spacing();
        ImGui.SeparatorText("Coupling");
        GameDataEditorUi.RenderCouplingSection(gd);
    }

    // -------------------------------------------------------------------------
    // Import From Game Part
    // -------------------------------------------------------------------------

    private void RenderImportSection(PartEditorController controller, PartEditorScene scene)
    {
        if (!ImGui.CollapsingHeader("Import From Game Part##st_import")) return;

        if (ImGui.Button(" Load Part List ##st_import_load"))
            _gameParts.Load();

        if (_gameParts.IsLoaded)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"({_gameParts.Parts.Count} parts)");
        }

        if (!_gameParts.IsLoaded || _gameParts.Parts.Count == 0)
        {
            ImGui.TextDisabled("Click 'Load Part List' to discover game parts.");
            return;
        }

        // Filter
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Filter:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##st_import_filter", _gamePartFilter);

        // Compute filtered indices
        string filterText = _gamePartFilter.ToString().Trim();
        _filteredGamePartIndices.Clear();
        for (int i = 0; i < _gameParts.Parts.Count; i++)
        {
            var (id, displayName) = _gameParts.Parts[i];
            if (string.IsNullOrEmpty(filterText)
                || id.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                || displayName.Contains(filterText, StringComparison.OrdinalIgnoreCase))
            {
                _filteredGamePartIndices.Add(i);
            }
        }

        ImGui.TextDisabled($"{_filteredGamePartIndices.Count} matching");

        // Combo
        string preview = _selectedGamePartIndex >= 0 && _selectedGamePartIndex < _gameParts.Parts.Count
            ? $"{_gameParts.Parts[_selectedGamePartIndex].displayName}  ({_gameParts.Parts[_selectedGamePartIndex].id})"
            : "(select a part)";

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##st_import_combo", preview))
        {
            for (int fi = 0; fi < _filteredGamePartIndices.Count; fi++)
            {
                int idx = _filteredGamePartIndices[fi];
                var (id, displayName) = _gameParts.Parts[idx];
                bool sel = idx == _selectedGamePartIndex;
                if (ImGui.Selectable($"{displayName}  ({id})##st_ip{idx}", sel))
                    _selectedGamePartIndex = idx;
            }
            ImGui.EndCombo();
        }

        ImGui.Spacing();

        bool canImport = _selectedGamePartIndex >= 0 && _selectedGamePartIndex < _gameParts.Parts.Count;
        if (!canImport) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(new float4(0.1f, 0.5f, 0.7f, 1f)));
        if (ImGui.Button(" Import ##st_import_btn") && canImport)
        {
            var partId = _gameParts.Parts[_selectedGamePartIndex].id;
            var imported = PartImporter.ImportFromTemplate(partId);
            if (imported != null)
            {
                controller.LoadPart(imported);
                if (scene.IsActive)
                    scene.SyncParts(controller.CurrentPart);
                _importStatusMessage = $"Imported '{partId}' ({imported.Placements.Count} SubParts, {imported.GameData.Connectors.Count} Connectors)";
                _importStatusColor = new float4(0.3f, 1f, 0.3f, 1f);
                _lastKnownPartId = "";
                _lastKnownPlacementIndex = -2;
                _saveStatusMessage = null;
            }
            else
            {
                _importStatusMessage = $"Failed to import '{partId}'";
                _importStatusColor = new float4(1f, 0.3f, 0.3f, 1f);
            }
        }
        ImGui.PopStyleColor();
        if (!canImport) ImGui.EndDisabled();

        if (_importStatusMessage != null)
        {
            ImGui.TextColored(_importStatusColor, _importStatusMessage);
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

        // Hot-reload spike (experimental)
        ImGui.Spacing();
        ImGui.SeparatorText("Experimental");
        if (ImGui.Button(" Test Hot-Reload ##st_hotreload"))
        {
            var (success, message) = HotReloadSpike.TryRegisterPart(controller.CurrentPart);
            _hotReloadSuccess = success;
            if (success)
            {
                bool verified = HotReloadSpike.VerifyRegistration(controller.CurrentPart.PartId);
                _hotReloadMessage = message + (verified ? "  (Verified in ModLibrary)" : "  (NOT found in ModLibrary after registration!)");
            }
            else
            {
                _hotReloadMessage = message;
            }
        }
        if (_hotReloadMessage != null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(
                _hotReloadSuccess ? new float4(0.3f, 1f, 0.3f, 1f) : new float4(1f, 0.5f, 0.3f, 1f)));
            ImGui.TextWrapped(_hotReloadMessage);
            ImGui.PopStyleColor();
        }
    }
}
