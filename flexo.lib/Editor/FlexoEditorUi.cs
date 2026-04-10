using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.FlexoLib.Data;
using MeowSci.KsaAbstractions;

namespace MeowSci.FlexoLib.Editor;

public sealed class FlexoEditorUi
{
    private readonly FlexoEditorScene _scene;
    private readonly FlexoEditorInteraction _interaction;
    private readonly FlexoEditorState _state;
    private readonly FlexoCameraSnap _cameraSnap;
    private readonly FlexoEditorLighting _lighting;
    private readonly FlexoDataManager _dataManager;

    private int _selectedVehicleIndex = -1;
    private List<Vehicle>? _cachedVehicles;
    private int _axisPreset = 1; // 0=X, 1=Y, 2=Z, 3=Custom
    private readonly ImInputString _displayNameInput = new ImInputString(256);

    public FlexoEditorUi(
        FlexoEditorScene scene,
        FlexoEditorInteraction interaction,
        FlexoEditorState state,
        FlexoCameraSnap cameraSnap,
        FlexoEditorLighting lighting,
        FlexoDataManager dataManager)
    {
        _scene = scene;
        _interaction = interaction;
        _state = state;
        _cameraSnap = cameraSnap;
        _lighting = lighting;
        _dataManager = dataManager;
    }

    public void Render(ref bool open)
    {
        ImGui.SetNextWindowSize(new float2(450, 600), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Flexo Editor##flexo_editor", ref open))
        {
            ImGui.End();
            return;
        }

        try
        {
            RenderToolbar();
            ImGui.Spacing();
            RenderVehicleLoader();
            ImGui.Spacing();

            if (_scene.IsActive && _state.LoadedVehicle != null)
            {
                RenderPartList();
                ImGui.Spacing();
                RenderHingeCreator();
            }

            RenderStatus();
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"UI Error: {ex.Message}");
        }

        ImGui.End();
    }

    private void RenderToolbar()
    {
        if (!ImGui.CollapsingHeader("Toolbar##flexo_toolbar"))
            return;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##flexo_cam_snaps", 3, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); RenderSnapButton("Front", CameraSnapMode.Front);
            ImGui.TableNextColumn(); RenderSnapButton("Back", CameraSnapMode.Back);
            ImGui.TableNextColumn(); RenderSnapButton("Left", CameraSnapMode.Left);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); RenderSnapButton("Right", CameraSnapMode.Right);
            ImGui.TableNextColumn(); RenderSnapButton("Top", CameraSnapMode.Top);
            ImGui.TableNextColumn(); RenderSnapButton("Bottom", CameraSnapMode.Bottom);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();

        bool gridVisible = _cameraSnap.GridVisible;
        if (ImGui.Checkbox("Grid##flexo_grid", ref gridVisible))
            _cameraSnap.GridVisible = gridVisible;

        ImGui.SameLine(0, 12);

        string[] lightModes = { "Off", "Box Corners", "Sphere" };
        int lightIdx = (int)_lighting.Arrangement;
        ImGui.SetNextItemWidth(120);
        if (ImGui.Combo("Lighting##flexo_light", ref lightIdx, lightModes, lightModes.Length))
            _lighting.Arrangement = (LightArrangement)lightIdx;
    }

    private void RenderSnapButton(string label, CameraSnapMode mode)
    {
        bool active = _cameraSnap.ActiveMode == mode;
        if (active)
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(new float4(0.3f, 0.5f, 0.8f, 1f)));

        if (ImGui.Button($" {label} ##snap_{mode}", new float2(-1, 0)))
        {
            _cameraSnap.SnapTo(active ? CameraSnapMode.None : mode, _scene);
        }

        if (active)
            ImGui.PopStyleColor();
    }

    private void RenderVehicleLoader()
    {
        if (!ImGui.CollapsingHeader("Vehicle##flexo_vehicle", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        _cachedVehicles ??= VehicleProvider.GetAllVehicles();

        string preview = _selectedVehicleIndex >= 0 && _selectedVehicleIndex < _cachedVehicles.Count
            ? _cachedVehicles[_selectedVehicleIndex].Id
            : "(none)";

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##flexo_veh_loader", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Vehicle");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##flexo_veh_combo", preview))
            {
                for (int i = 0; i < _cachedVehicles.Count; i++)
                {
                    bool sel = _selectedVehicleIndex == i;
                    if (ImGui.Selectable(_cachedVehicles[i].Id, sel))
                        _selectedVehicleIndex = i;
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();

        if (ImGui.Button(" Refresh ##flexo_veh_refresh"))
        {
            _cachedVehicles = VehicleProvider.GetAllVehicles();
            _selectedVehicleIndex = -1;
        }

        ImGui.SameLine(0, 8);

        bool canLoad = _selectedVehicleIndex >= 0 && _selectedVehicleIndex < (_cachedVehicles?.Count ?? 0);
        if (!canLoad) ImGui.BeginDisabled();
        if (ImGui.Button(" Load ##flexo_veh_load"))
        {
            LoadSelectedVehicle();
        }
        if (!canLoad) ImGui.EndDisabled();

        ImGui.SameLine(0, 8);

        if (_scene.IsActive)
        {
            if (ImGui.Button(" Close Editor ##flexo_close"))
            {
                CloseEditor();
            }
        }
    }

    private void LoadSelectedVehicle()
    {
        if (_cachedVehicles == null || _selectedVehicleIndex < 0) return;

        var vehicle = _cachedVehicles[_selectedVehicleIndex];
        if (!_scene.IsActive)
            _scene.Enter();

        _state.Reset();
        _state.LoadedVehicle = vehicle;
        _scene.LoadVehicleParts(vehicle);
        _state.StatusMessage = $"Loaded {vehicle.Id} ({_scene.EditorParts.Count} parts)";
        _state.StatusIsError = false;
    }

    private void CloseEditor()
    {
        _state.Reset();
        _interaction.ClearVisualState();
        _scene.Exit();
    }

    private void RenderPartList()
    {
        if (!ImGui.CollapsingHeader("Parts##flexo_parts", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ImGui.BeginChild("##flexo_part_list", new float2(0, 200), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);

        foreach (var part in _scene.EditorParts)
        {
            bool isFixed = part == _state.FixedPart;
            bool isMoving = part == _state.MovingPart;
            bool isSelected = part == _interaction.SelectedPart;

            string label = part.Template.Id;
            if (isFixed) label = "[FIXED] " + label;
            else if (isMoving) label = "[MOVING] " + label;

            if (isFixed)
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(new float4(0.4f, 0.8f, 1f, 1f)));
            else if (isMoving)
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(new float4(1f, 0.8f, 0.3f, 1f)));

            if (ImGui.Selectable($"{label}##{part.Id}", isSelected))
            {
                _interaction.SelectPart(part);
                _state.OnPartSelected(part);
            }

            if (isFixed || isMoving)
                ImGui.PopStyleColor();
        }

        ImGui.EndChild();
    }

    private void RenderHingeCreator()
    {
        if (!ImGui.CollapsingHeader("Hinge##flexo_hinge", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        switch (_state.Mode)
        {
            case FlexoEditorMode.Idle:
                RenderHingeIdle();
                break;
            case FlexoEditorMode.SelectFixed:
                RenderHingeSelectFixed();
                break;
            case FlexoEditorMode.SelectMoving:
                RenderHingeSelectMoving();
                break;
            case FlexoEditorMode.ConfigureHinge:
            case FlexoEditorMode.ReadyToSave:
                RenderHingeConfigure();
                break;
        }
    }

    private void RenderHingeIdle()
    {
        if (ImGui.Button(" New Hinge ##flexo_new_hinge"))
        {
            _state.StartNewHinge();
        }

        ImGui.SameLine(0, 12);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Click to start creating a hinge definition.");
    }

    private void RenderHingeSelectFixed()
    {
        ImGui.TextColored(new float4(0.4f, 0.8f, 1f, 1f), "Select the FIXED part (click a part in the list or 3D view)");
        ImGui.Spacing();
        if (ImGui.Button(" Cancel ##flexo_cancel_hinge"))
            _state.Reset();
    }

    private void RenderHingeSelectMoving()
    {
        ImGui.TextDisabled($"Fixed: {_state.WorkingHinge.FixedPartTemplateId}");
        ImGui.Spacing();
        ImGui.TextColored(new float4(1f, 0.8f, 0.3f, 1f), "Select the MOVING part (click a part in the list or 3D view)");
        ImGui.Spacing();
        if (ImGui.Button(" Cancel ##flexo_cancel_hinge"))
            _state.Reset();
    }

    private void RenderHingeConfigure()
    {
        var hinge = _state.WorkingHinge;

        ImGui.TextDisabled($"Fixed: {hinge.FixedPartTemplateId}");
        ImGui.TextDisabled($"Moving: {hinge.MovingPartTemplateId}");
        ImGui.Spacing();

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##flexo_hinge_params", 4, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX))
        {
            // Axis preset
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Axis");
            ImGui.TableNextColumn();
            string[] axisPresets = { "X", "Y", "Z", "Custom" };
            ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("##flexo_axis_preset", ref _axisPreset, axisPresets, axisPresets.Length))
            {
                switch (_axisPreset)
                {
                    case 0: hinge.AxisX = 1; hinge.AxisY = 0; hinge.AxisZ = 0; break;
                    case 1: hinge.AxisX = 0; hinge.AxisY = 1; hinge.AxisZ = 0; break;
                    case 2: hinge.AxisX = 0; hinge.AxisY = 0; hinge.AxisZ = 1; break;
                }
            }
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();

            // Custom axis (only if Custom selected)
            if (_axisPreset == 3)
            {
                float ax = (float)hinge.AxisX, ay = (float)hinge.AxisY, az = (float)hinge.AxisZ;
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Axis X");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); ImGui.DragFloat("##flexo_ax", ref ax, 0.01f, -1f, 1f); hinge.AxisX = ax;
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Axis Y");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); ImGui.DragFloat("##flexo_ay", ref ay, 0.01f, -1f, 1f); hinge.AxisY = ay;

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Axis Z");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); ImGui.DragFloat("##flexo_az", ref az, 0.01f, -1f, 1f); hinge.AxisZ = az;
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
            }

            // Min / Max degrees
            float minDeg = (float)hinge.MinDegrees;
            float maxDeg = (float)hinge.MaxDegrees;
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Min °");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); ImGui.DragFloat("##flexo_min", ref minDeg, 1f, -360f, 360f); hinge.MinDegrees = minDeg;
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Max °");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); ImGui.DragFloat("##flexo_max", ref maxDeg, 1f, -360f, 360f); hinge.MaxDegrees = maxDeg;

            // Resting / Speed
            float restDeg = (float)hinge.RestingDegrees;
            float speed = (float)hinge.SpeedDegreesPerSecond;
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Rest °");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); ImGui.DragFloat("##flexo_rest", ref restDeg, 1f, minDeg, maxDeg); hinge.RestingDegrees = restDeg;
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Speed");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); ImGui.DragFloat("##flexo_speed", ref speed, 1f, 1f, 360f); hinge.SpeedDegreesPerSecond = speed;

            // Preview angle
            float preview = _state.PreviewAngle;
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Preview");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat("##flexo_preview", ref preview, 1f, minDeg, maxDeg))
            {
                _state.PreviewAngle = preview;
                ApplyPreviewRotation();
            }
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();

        // Display name
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##flexo_name_tbl", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Name");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##flexo_display_name", _displayNameInput))
                _state.DisplayName = _displayNameInput.ToString();

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();

        // Validate and update mode
        if (_state.IsValid() && _state.Mode == FlexoEditorMode.ConfigureHinge)
            _state.Mode = FlexoEditorMode.ReadyToSave;
        else if (!_state.IsValid() && _state.Mode == FlexoEditorMode.ReadyToSave)
            _state.Mode = FlexoEditorMode.ConfigureHinge;

        // Save section
        bool canSave = _state.Mode == FlexoEditorMode.ReadyToSave;
        if (!canSave) ImGui.BeginDisabled();
        if (ImGui.Button(" Save Flexo Part ##flexo_save"))
        {
            SaveDefinition();
        }
        if (!canSave) ImGui.EndDisabled();

        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Cancel ##flexo_cancel_config"))
            _state.Reset();
    }

    private void SaveDefinition()
    {
        try
        {
            var def = new FlexoPartDefinition
            {
                PartType = FlexoPartType.Hinge,
                DisplayName = _state.DisplayName,
                CreatedFromVehicle = _state.LoadedVehicle?.Id ?? "",
                Hinge = new HingeDefinition
                {
                    FixedPartTemplateId = _state.WorkingHinge.FixedPartTemplateId,
                    MovingPartTemplateId = _state.WorkingHinge.MovingPartTemplateId,
                    AxisX = _state.WorkingHinge.AxisX,
                    AxisY = _state.WorkingHinge.AxisY,
                    AxisZ = _state.WorkingHinge.AxisZ,
                    MinDegrees = _state.WorkingHinge.MinDegrees,
                    MaxDegrees = _state.WorkingHinge.MaxDegrees,
                    RestingDegrees = _state.WorkingHinge.RestingDegrees,
                    SpeedDegreesPerSecond = _state.WorkingHinge.SpeedDegreesPerSecond,
                }
            };

            _dataManager.SaveDefinition(def);
            _state.StatusMessage = $"Saved: {def.FileName}";
            _state.StatusIsError = false;
            _state.Reset();
        }
        catch (Exception ex)
        {
            _state.StatusMessage = $"Save failed: {ex.Message}";
            _state.StatusIsError = true;
        }
    }

    private void ApplyPreviewRotation()
    {
        if (_state.MovingPart == null) return;

        var hinge = _state.WorkingHinge;
        double3 axis = new double3(hinge.AxisX, hinge.AxisY, hinge.AxisZ);
        double lenSq = axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z;
        if (lenSq < 0.001) return;

        double len = Math.Sqrt(lenSq);
        axis = new double3(axis.X / len, axis.Y / len, axis.Z / len);

        double radians = _state.PreviewAngle * Math.PI / 180.0;
        doubleQuat rotation = doubleQuat.CreateFromAxisAngle(axis, radians);

        // Find the matching editor part for the moving part template
        foreach (var part in _scene.EditorParts)
        {
            if (part.Template.Id == _state.MovingPart.Template.Id)
            {
                part.Asmb2ParentAsmb = rotation;
                break;
            }
        }
    }

    private void RenderStatus()
    {
        if (string.IsNullOrEmpty(_state.StatusMessage)) return;

        ImGui.Spacing();
        if (_state.StatusIsError)
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _state.StatusMessage);
        else
            ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f), _state.StatusMessage);
    }
}
