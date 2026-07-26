using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;

namespace MeowSci.HumbleArteestLib;

/// <summary>
/// ImGui panel for the vehicle paint feature: arm the patched part shaders, pick a blend mode,
/// and assign colors per part instance, per part type, or to everything at once.
/// </summary>
public sealed partial class VehiclePaintSubmod : ISubmod
{
    private const double RefreshIntervalSeconds = 0.5;

    private static readonly string[] BlendModeNames = { "Multiply", "Tint", "Replace" };

    private float3 _brush = new(1f, 0.25f, 0.2f);
    private int _tab;
    private int _groupIndex;

    private List<PaintTargets.Group> _groups = new();
    private double _timeSinceRefresh = double.MaxValue;

    private ImGuiTextFilter _partFilter = new();
    private ImGuiTextFilter _typeFilter = new();

    private string? _status;
    private bool _statusIsError;

    public string Name => "Vehicle Paint";

    public string Tooltip => "Paints individual vehicle parts by injecting a tint into the part shaders.";

    public void Initialize() { }

    public void Update(double dt)
    {
        if (_timeSinceRefresh < double.MaxValue)
            _timeSinceRefresh += dt;
    }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##vp_content");

        bool headerOpen = ImGui.CollapsingHeader("Vehicle Paint (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip(HeaderTooltip);
        if (headerOpen)
            RenderBody();

        SubmodUI.EndContentArea();
    }

    /// <summary>Renders the panel contents without the collapsing header, for the combined submod.</summary>
    internal void RenderBody()
    {
        RefreshIfStale();

        RenderShaderRow();
        ImGui.Spacing();
        RenderBrushRow();
        ImGui.Spacing();
        RenderTargetTabs();
        RenderStatus();
    }

    public void Dispose() => VehiclePaint.Cleanup();

    internal const string HeaderTooltip =
        "Packs a per-part color into the unused high bits of the part instance state flags and\n" +
        "applies it to the albedo in a runtime-patched copy of the part fragment shaders.\n\n" +
        "Enabling or changing the blend mode triggers a renderer rebuild (a brief hitch),\n" +
        "the same one the game performs when you change a graphics setting.";

    // ---- Header rows ----

    private void RenderShaderRow()
    {
        bool enabled = VehiclePaint.Active;
        if (ImGui.Checkbox("Enable painting##vp", ref enabled))
        {
            if (enabled)
            {
                if (VehiclePaint.Enable())
                    SetStatus("Paint shaders armed — rebuilding renderer.", false);
                else
                    SetStatus(VehiclePaint.LastError ?? "Could not arm the paint shaders.", true);
            }
            else
            {
                VehiclePaint.Disable();
                SetStatus("Paint shaders removed — rebuilding renderer.", false);
            }
        }
        ImGui.SetItemTooltip(
            "Installs a patched copy of the part fragment shaders.\n" +
            "Paint has no visible effect while this is off.");

        ImGui.SameLine(0, 12);
        if (VehiclePaint.Active)
            ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f), $"Active ({VehiclePaintShaders.CompileCount} compiles)");
        else
            ImGui.TextColored(new float4(1f, 1f, 0.4f, 1f), "Inactive");

        ImGui.SameLine(0, 12);
        if (ImGui.Button(" Clear all paint ##vp"))
        {
            VehiclePaint.ClearAllPaint();
            SetStatus("All paint cleared.", false);
        }

        if (VehiclePaint.Active && VehiclePaintShaders.LastError != null)
            ImGui.TextColored(new float4(1f, 0.55f, 0.2f, 1f), VehiclePaintShaders.LastError);

        int applied = VehiclePaintPatches.AppliedPatchCount;
        if (applied < VehiclePaintPatches.RequiredPatchCount)
        {
            ImGui.TextColored(new float4(1f, 0.55f, 0.2f, 1f),
                $"Only {applied}/{VehiclePaintPatches.RequiredPatchCount} game hooks attached — " +
                "this KSA build moved something. See the log for which one.");
        }
    }

    private void RenderBrushRow()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##vp_brush", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##vp_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##vp_widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Brush color");
            ImGui.TableNextColumn();
            ImGui.ColorEdit3("##vp_brush_color", ref _brush, ImGuiColorEditFlags.NoInputs);
            ImGui.SameLine(0, 8);
            ImGui.TextDisabled("(?)");
            ImGui.SetItemTooltip(
                "Color applied when you tick a part or part type below.\n" +
                "Colors are stored at 7 bits per channel.");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Blend");
            ImGui.TableNextColumn();
            int blend = (int)VehiclePaint.BlendMode;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.Combo("##vp_blend", ref blend, BlendModeNames, BlendModeNames.Length))
            {
                VehiclePaint.BlendMode = (PaintBlendMode)blend;
                SetStatus($"Blend mode set to {BlendModeNames[blend]} — rebuilding renderer.", false);
            }
            ImGui.SetItemTooltip(
                "Multiply — albedo x color. Keeps every texture detail, can only darken.\n" +
                "Tint — recolors by luminance. Keeps shading, can brighten.\n" +
                "Replace — flat color; shape still comes from the normal and PBR maps.");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Paint everything");
            ImGui.TableNextColumn();
            bool paintAll = VehiclePaint.GlobalEnabled;
            if (ImGui.Checkbox("##vp_all", ref paintAll))
                VehiclePaint.GlobalEnabled = paintAll;
            ImGui.SameLine(0, 8);
            var globalColor = VehiclePaint.GlobalColor;
            if (ImGui.ColorEdit3("##vp_all_color", ref globalColor, ImGuiColorEditFlags.NoInputs))
                VehiclePaint.GlobalColor = globalColor;
            ImGui.SameLine(0, 8);
            ImGui.TextDisabled("fallback for parts with no other paint");

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    // ---- Target selection ----

    private void RenderTargetTabs()
    {
        if (ImGui.RadioButton("Parts##vp_tab", _tab == 0)) _tab = 0;
        ImGui.SameLine(0, 12);
        if (ImGui.RadioButton("Part types##vp_tab", _tab == 1)) _tab = 1;
        ImGui.SameLine(0, 16);
        if (ImGui.Button(" Refresh ##vp"))
            Refresh();
        ImGui.SameLine(0, 8);
        ImGui.TextDisabled($"{VehiclePaint.PaintedPartCount} parts / {VehiclePaint.PaintedTemplates.Count} types painted");

        ImGui.Spacing();

        if (_tab == 0)
            RenderPartsTab();
        else
            RenderTypesTab();
    }

    private void RenderStatus()
    {
        if (string.IsNullOrEmpty(_status)) return;
        ImGui.Spacing();
        ImGui.TextWrapped(_status);
        if (_statusIsError)
            ImGui.TextColored(new float4(1f, 0.35f, 0.35f, 1f), "^ paint could not be applied");
    }

    private void SetStatus(string message, bool isError)
    {
        _status = message;
        _statusIsError = isError;
    }

    // ---- Target cache ----

    private void RefreshIfStale()
    {
        if (_timeSinceRefresh >= RefreshIntervalSeconds)
            Refresh();
    }

    private void Refresh()
    {
        _groups = PaintTargets.Enumerate();
        _typeCounts = PaintTargets.CountTemplates(_groups);
        _timeSinceRefresh = 0;

        if (_groupIndex >= _groups.Count)
            _groupIndex = _groups.Count - 1;
        if (_groupIndex < 0 && _groups.Count > 0)
            _groupIndex = 0;

        VehiclePaint.PruneParts(PaintTargets.FlattenParts(_groups));
    }

    private PaintTargets.Group? CurrentGroup =>
        _groupIndex >= 0 && _groupIndex < _groups.Count ? _groups[_groupIndex] : null;

    private void RenderGroupSelector()
    {
        if (_groups.Count == 0)
        {
            ImGui.TextDisabled("No vehicles or editor parts to paint.");
            return;
        }

        var labels = new string[_groups.Count];
        for (int i = 0; i < _groups.Count; i++)
            labels[i] = _groups[i].Label;

        ImGui.AlignTextToFramePadding();
        ImGui.Text("Target");
        ImGui.SameLine(0, 8);
        ImGui.SetNextItemWidth(-1f);
        ImGui.Combo("##vp_group", ref _groupIndex, labels, labels.Length);
    }
}
