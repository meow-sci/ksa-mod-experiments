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

    private readonly ImInputString _instanceIdInput = new ImInputString(128);

    // Change-detection for input buffer sync (avoids overwriting in-progress edits)
    private int _lastKnownPlacementIndex = -2;
    private string _lastKnownInstanceId = "";

    private readonly SavePartModal _savePartModal = new();
    private readonly ImportModal _importModal = new();

    // Hot-reload spike state
    private string? _hotReloadMessage;
    private bool _hotReloadSuccess;

    private static readonly string[] KnownEditorTags =
        { "Command", "Structural", "Cargo", "Propulsion", "Aero",
          "Electrical", "Thermal", "Science", "Coupling", "Ground", "Payload" };

    private int _selectedNewTagIndex;

    // Transform options
    private bool _gridModeEnabled;
    private float _gridStep = 0.05f;
    private bool _rotSnapEnabled;
    private float _rotSnapDeg = 15f;
    private PartEditorGizmos.GizmoMode _lastNonNoneGizmoMode = PartEditorGizmos.GizmoMode.Translate;

    // SubParts hierarchy filter
    private readonly ImInputString _hierarchyFilter = new(128);

    // Reflection: invalidate Part's cached transform matrix after manual edits
    private static readonly FieldInfo? MatrixAsmbField =
        typeof(Part).GetField("_matrixAsmb", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? MatrixAsmb2ParentField =
        typeof(Part).GetField("_matrixAsmb2Parent", BindingFlags.NonPublic | BindingFlags.Instance);

    public void RenderEditorWindow(
        PartEditorController controller,
        PartEditorScene scene,
        PartEditorGizmos gizmos,
        PartEditorInteraction interaction,
        SubPartCatalog catalog,
        PartModWriter writer,
        CameraSnapController cameraSnap,
        EditorLighting lighting)
    {
        if (!WindowOpen) return;

        ImGui.SetNextWindowSize(new float2(440, 700), ImGuiCond.FirstUseEver);
        bool open = WindowOpen;
        if (ImGui.Begin("Part Editor##st_editor", ref open))
        {
            RenderToolbar(controller, gizmos, interaction, scene, writer, cameraSnap, lighting);
            _importModal.Render(controller, scene, writer);
            if (_importModal.ShouldResetTracking)
            {
                _lastKnownPlacementIndex = -2;
            }
            _savePartModal.Render(controller, writer, onSaveSuccess: () =>
            {
                controller.MarkSaved();
            });
            ImGui.Spacing();
            RenderHierarchySection(controller, scene);
            ImGui.Spacing();
            RenderPropertiesSection(controller, scene);
            ImGui.Spacing();
            RenderGameDataSection(controller);
            ImGui.Spacing();
            RenderExperimentalSection(controller);
        }
        ImGui.End();
        WindowOpen = open;
    }

    // -------------------------------------------------------------------------
    // Toolbar
    // -------------------------------------------------------------------------

    private void RenderToolbar(PartEditorController controller, PartEditorGizmos gizmos, PartEditorInteraction interaction, PartEditorScene scene, PartModWriter writer, CameraSnapController cameraSnap, EditorLighting lighting)
    {
        bool canSave = controller.CurrentPart.Placements.Count > 0;

        if (!canSave) ImGui.BeginDisabled();
        if (ImGui.Button(" Save "))
        {
            writer.RefreshFileList();
            _savePartModal.OnOpen(writer, controller);
            ImGui.OpenPopup(SavePartModal.PopupId);
        }
        if (!canSave) ImGui.EndDisabled();
        if (!canSave && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetItemTooltip("Add at least one SubPart to save.");
        ImGui.SameLine(0, 8);

        if (ImGui.Button(" Import ##st_imp_open_btn"))
        {
            _importModal.OnOpen(writer);
            ImGui.OpenPopup(ImportModal.PopupId);
        }
        ImGui.SameLine(0, 8);

        if (ImGui.Button(" New Part "))
        {
            controller.NewPart();
            scene.SyncParts(controller.CurrentPart);
            _lastKnownPlacementIndex = -2;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetItemTooltip("Danger Will Robinson!\n\nThis will clear out all SubParts in the workspace!");

        ImGui.Spacing();

        // --- Camera Snap (always visible) ---
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        float checkW = ImGui.GetFrameHeight();
        var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##st_camsnap_tbl", 3, tableFlags))
        {
            ImGui.TableSetupColumn("##cb", ImGuiTableColumnFlags.WidthFixed, checkW);
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthFixed, 270f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch);

            // Row: Camera Snap — 6 directional snap buttons
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); // empty checkbox column
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Camera Snap");
            ImGui.TableNextColumn();
            float snapBtnW = (ImGui.GetContentRegionAvail().X - 8f) / 3f;
            RenderSnapButton("Left", CameraSnapMode.Left, cameraSnap, scene, snapBtnW);
            ImGui.SameLine(0, 4);
            RenderSnapButton("Front", CameraSnapMode.Front, cameraSnap, scene, snapBtnW);
            ImGui.SameLine(0, 4);
            RenderSnapButton("Right", CameraSnapMode.Right, cameraSnap, scene, snapBtnW);
            ImGui.Spacing();
            RenderSnapButton("Top", CameraSnapMode.Top, cameraSnap, scene, snapBtnW);
            ImGui.SameLine(0, 4);
            RenderSnapButton("Back", CameraSnapMode.Back, cameraSnap, scene, snapBtnW);
            ImGui.SameLine(0, 4);
            RenderSnapButton("Bottom", CameraSnapMode.Bottom, cameraSnap, scene, snapBtnW);

            if (cameraSnap.DebugReadout)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                OrbitView? ov = Program.GetCamera()?.Following?.OrbitView;
                if (ov != null)
                    ImGui.Text($"Az: {ov.Azimuth:F3}  El: {ov.Elevation:F3}");
                else
                    ImGui.TextDisabled("OrbitView: null");
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();
        bool gridVis = cameraSnap.GridVisible;
        if (ImGui.Checkbox(" Show Visual Grid ##st_show_grid", ref gridVis))
            cameraSnap.GridVisible = gridVis;

        // --- Editor Stuff (collapsible) ---
        if (ImGui.CollapsingHeader("Editor Stuff##st_editor_stuff"))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
            if (ImGui.BeginTable("##st_editor_tbl", 3, tableFlags))
            {
                ImGui.TableSetupColumn("##cb", ImGuiTableColumnFlags.WidthFixed, checkW);
                ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthFixed, 270f);
                ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch);

                // Gizmo
                bool gizmoEnabled = gizmos.ActiveMode != PartEditorGizmos.GizmoMode.None;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Checkbox("##st_gizmo_en", ref gizmoEnabled))
                    gizmos.ActiveMode = gizmoEnabled ? _lastNonNoneGizmoMode : PartEditorGizmos.GizmoMode.None;
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Gizmo");
                ImGui.TableNextColumn();
                if (!gizmoEnabled) ImGui.BeginDisabled();
                if (ImGui.RadioButton("Translate##st_gizmo_t", gizmos.ActiveMode == PartEditorGizmos.GizmoMode.Translate))
                { gizmos.ActiveMode = PartEditorGizmos.GizmoMode.Translate; _lastNonNoneGizmoMode = PartEditorGizmos.GizmoMode.Translate; }
                ImGui.SameLine();
                if (ImGui.RadioButton("Rotate##st_gizmo_r", gizmos.ActiveMode == PartEditorGizmos.GizmoMode.Rotate))
                { gizmos.ActiveMode = PartEditorGizmos.GizmoMode.Rotate; _lastNonNoneGizmoMode = PartEditorGizmos.GizmoMode.Rotate; }
                ImGui.SameLine();
                if (ImGui.RadioButton("Scale##st_gizmo_s", gizmos.ActiveMode == PartEditorGizmos.GizmoMode.Scale))
                { gizmos.ActiveMode = PartEditorGizmos.GizmoMode.Scale; _lastNonNoneGizmoMode = PartEditorGizmos.GizmoMode.Scale; }
                if (!gizmoEnabled) ImGui.EndDisabled();

                // Gizmo Size
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Gizmo Size");
                ImGui.TableNextColumn();
                float gizmoScale = gizmos.GizmoScale;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat("##st_gizmo_scale", ref gizmoScale, 0.05f, 0.1f, 10f, "%.2fx"))
                    gizmos.GizmoScale = gizmoScale;

                // Origin Alpha
                bool originVisible = scene.OriginVisible;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Checkbox("##st_origin_cb1", ref originVisible)) scene.OriginVisible = originVisible;
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Origin Alpha");
                ImGui.TableNextColumn();
                if (!scene.OriginVisible) ImGui.BeginDisabled();
                float originAlpha = scene.OriginAlpha;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat("##st_origin_alpha", ref originAlpha, 0.01f, 0f, 1f, "%.2f"))
                    scene.OriginAlpha = originAlpha;
                if (!scene.OriginVisible) ImGui.EndDisabled();

                // Origin Size
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                bool originVisible2 = scene.OriginVisible;
                if (ImGui.Checkbox("##st_origin_cb2", ref originVisible2)) scene.OriginVisible = originVisible2;
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Origin Size");
                ImGui.TableNextColumn();
                if (!scene.OriginVisible) ImGui.BeginDisabled();
                float originSize = scene.OriginSize;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat("##st_origin_size", ref originSize, 0.05f, 0.1f, 10f, "%.2fx"))
                    scene.OriginSize = originSize;
                if (!scene.OriginVisible) ImGui.EndDisabled();

                // Grid Snap
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Checkbox("##st_grid_en", ref _gridModeEnabled);
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Grid Snap");
                ImGui.TableNextColumn();
                if (!_gridModeEnabled) ImGui.BeginDisabled();
                ImGui.SetNextItemWidth(-1);
                ImGui.DragFloat("##st_gridstep", ref _gridStep, 0.001f, 0.001f, 10f, "%.4f");
                if (!_gridModeEnabled) ImGui.EndDisabled();

                // Rotation Snap
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Checkbox("##st_rot_en", ref _rotSnapEnabled);
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Rotation Snap");
                ImGui.TableNextColumn();
                if (!_rotSnapEnabled) ImGui.BeginDisabled();
                ImGui.SetNextItemWidth(-1);
                ImGui.DragFloat("##st_rotsnapdeg", ref _rotSnapDeg, 0.5f, 0.5f, 90f, "%.1f°");
                if (!_rotSnapEnabled) ImGui.EndDisabled();

                // Grid Size
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Grid Size");
                ImGui.TableNextColumn();
                float gridW = cameraSnap.GridWidth;
                float gridH = cameraSnap.GridHeight;
                float halfWidth = (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(" x ").X) / 2f;
                ImGui.SetNextItemWidth(halfWidth);
                if (ImGui.DragFloat("##st_gridw", ref gridW, 0.1f, 0.5f, 50f, "%.1f"))
                    cameraSnap.GridWidth = gridW;
                ImGui.SameLine(0, 2);
                ImGui.AlignTextToFramePadding(); ImGui.Text(" x ");
                ImGui.SameLine(0, 2);
                ImGui.SetNextItemWidth(halfWidth);
                if (ImGui.DragFloat("##st_gridh", ref gridH, 0.1f, 0.5f, 50f, "%.1f"))
                    cameraSnap.GridHeight = gridH;

                // Grid Spacing
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Grid Spacing");
                ImGui.TableNextColumn();
                float spacing = cameraSnap.GridSpacing;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat("##st_gridspacing", ref spacing, 0.01f, 0.01f, 5f, "%.3f"))
                    cameraSnap.GridSpacing = spacing;

                // Grid Color
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Grid Color");
                ImGui.TableNextColumn();
                float4 gridCol = cameraSnap.GridColor;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.ColorEdit4("##st_gridcolor", ref gridCol, ImGuiColorEditFlags.NoLabel))
                    cameraSnap.GridColor = gridCol;

                // Lighting
                bool lightingEnabled = lighting.Arrangement != LightArrangement.Off;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Checkbox("##st_light_en", ref lightingEnabled))
                    lighting.Arrangement = lightingEnabled ? LightArrangement.BoxCorners : LightArrangement.Off;
                if (ImGui.IsItemHovered())
                    ImGui.SetItemTooltip("Uses invisible Light parts to illuminate the workspace on all sides");
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Lighting");
                if (ImGui.IsItemHovered())
                    ImGui.SetItemTooltip("Uses invisible Light parts to illuminate the workspace on all sides");
                ImGui.TableNextColumn();
                if (!lightingEnabled) ImGui.BeginDisabled();
                if (ImGui.RadioButton("Box##st_light_box", lighting.Arrangement == LightArrangement.BoxCorners))
                    lighting.Arrangement = LightArrangement.BoxCorners;
                ImGui.SameLine();
                if (ImGui.RadioButton("Sphere##st_light_sph", lighting.Arrangement == LightArrangement.Sphere))
                    lighting.Arrangement = LightArrangement.Sphere;
                if (!lightingEnabled) ImGui.EndDisabled();

                if (lightingEnabled)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding(); ImGui.Text("Light Radius");
                    ImGui.TableNextColumn();
                    float lightRadius = lighting.Radius;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.DragFloat("##st_light_radius", ref lightRadius, 0.1f, 0.5f, 50f, "%.1f"))
                        lighting.Radius = lightRadius;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding(); ImGui.Text("Light Intensity");
                    ImGui.TableNextColumn();
                    float lightIntensity = lighting.Intensity;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.DragFloat("##st_light_intensity", ref lightIntensity, 0.1f, 0.1f, 100f, "%.1f"))
                        lighting.Intensity = lightIntensity;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding(); ImGui.Text("Light Range");
                    ImGui.TableNextColumn();
                    float lightRange = lighting.Range;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.DragFloat("##st_light_range", ref lightRange, 0.5f, 1f, 100f, "%.1f"))
                        lighting.Range = lightRange;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding(); ImGui.Text("Light Color");
                    ImGui.TableNextColumn();
                    float3 lightColor = lighting.Color;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.ColorEdit3("##st_light_color", ref lightColor, ImGuiColorEditFlags.NoLabel))
                        lighting.Color = lightColor;

                    if (lighting.Arrangement == LightArrangement.Sphere)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.AlignTextToFramePadding(); ImGui.Text("Lights / Ring");
                        ImGui.TableNextColumn();
                        int lightsPerRing = lighting.LightsPerRing;
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.DragInt("##st_light_perring", ref lightsPerRing, 0.1f, 2, 16))
                            lighting.LightsPerRing = lightsPerRing;

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.AlignTextToFramePadding(); ImGui.Text("Rings");
                        ImGui.TableNextColumn();
                        int rings = lighting.Rings;
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.DragInt("##st_light_rings", ref rings, 0.1f, 1, 8))
                            lighting.Rings = rings;
                    }
                }

                ImGui.EndTable();
            }
            ImGui.PopStyleVar();
        }

        // Always sync interaction settings regardless of collapsible state
        interaction.GridSnapEnabled = _gridModeEnabled;
        interaction.GridSnapStep = _gridStep;
        interaction.RotSnapEnabled = _rotSnapEnabled;
        interaction.RotSnapDeg = _rotSnapDeg;

        // Pan mode indicator
        ImGui.Spacing();
        PanMode panMode = interaction.CurrentPanMode;
        float4 panColor = panMode switch
        {
            PanMode.PlaneX => new float4(1f, 0.3f, 0.3f, 1f),
            PanMode.PlaneY => new float4(0.3f, 1f, 0.3f, 1f),
            PanMode.PlaneZ => new float4(0.3f, 0.3f, 1f, 1f),
            _ => new float4(0.5f, 0.5f, 0.5f, 1f)
        };
        string panLabel = panMode switch
        {
            PanMode.PlaneX => "Pan: YZ Plane (lock X / front)",
            PanMode.PlaneY => "Pan: XZ Plane (lock Y) / side",
            PanMode.PlaneZ => "Pan: XY Plane (lock Z) / top",
            _ => "Pan: Normal"
        };
        ImGui.TextColored(panColor, panLabel);
        ImGui.SameLine();
        ImGui.TextDisabled("(P to cycle)");
    }

    private static void RenderSnapButton(string label, CameraSnapMode mode, CameraSnapController snap, PartEditorScene scene, float width)
    {
        bool isActive = snap.ActiveMode == mode;
        if (isActive)
            ImGui.PushStyleColor(ImGuiCol.Button, (float4)KSAColor.Xkcd.BrightLightBlue);

        if (ImGui.Button($"{label}##st_snap_{mode}", new float2(width, 0)))
        {
            if (isActive)
                snap.SnapTo(CameraSnapMode.None, scene);
            else
                snap.SnapTo(mode, scene);
        }

        if (isActive)
            ImGui.PopStyleColor();
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

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##st_hier_filter", _hierarchyFilter);
        if (ImGui.IsItemHovered()) ImGui.SetItemTooltip("Filter SubParts by instance ID or template ID");

        string filterText = _hierarchyFilter.ToString();

        ImGui.BeginChild("##st_hier_list", new float2(0, 120),
            ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);

        bool anyVisible = false;
        for (int i = 0; i < controller.CurrentPart.Placements.Count; i++)
        {
            var p = controller.CurrentPart.Placements[i];
            if (filterText.Length > 0
                && !p.InstanceId.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                && !p.SubPartTemplateId.Contains(filterText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            anyVisible = true;
            bool isSelected = controller.SelectedPlacementIndex == i;
            if (ImGui.Selectable($"{p.InstanceId}  ({p.SubPartTemplateId})##st_h{i}", isSelected))
                controller.SelectedPlacementIndex = i;
        }

        if (controller.CurrentPart.Placements.Count == 0)
            ImGui.TextDisabled("No SubParts placed yet. Pick one from the catalog.");
        else if (!anyVisible)
            ImGui.TextDisabled("No matches.");

        ImGui.EndChild();
    }

    // -------------------------------------------------------------------------
    // Properties (transform)
    // -------------------------------------------------------------------------

    private void RenderPropertiesSection(PartEditorController controller, PartEditorScene scene)
    {
        bool open = ImGui.CollapsingHeader("SubPart Properties##st_props", ImGuiTreeNodeFlags.DefaultOpen);
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
            float rotSpeed = _rotSnapEnabled ? _rotSnapDeg : 0.05f;
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

        // --- Basic Info ---
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##st_gd_tbl", 2,
            ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##val", ImGuiTableColumnFlags.WidthStretch, 3f);

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

    private void RenderExperimentalSection(PartEditorController controller)
    {
        if (!ImGui.CollapsingHeader("Experimental##st_experimental")) return;

        if (ImGui.Button(" Test Hot-Reload ##st_hotreload"))
        {
            var (success, message) = HotReloadSpike.TryRegisterPart(controller.CurrentPart);
            _hotReloadSuccess = success;
            _hotReloadMessage = success
                ? message + " (Verified in ModLibrary)"
                : message;
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
