using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.HumbleArteestLib;

/// <summary>
/// ISubmod implementation for the Kitten Coloring feature.
/// Provides an ImGui panel to tint kitten character models by modifying
/// MaterialData.AlbedoColor in the GPU material buffer.
/// </summary>
public sealed class KittenColorSubmod : ISubmod
{
    public string Name => "Kitten Color";

    private float4 _color = new float4(1f, 1f, 1f, 1f);
    private bool _tintActive;
    private int _selectedMaterialIndex = -1;
    private string? _statusMessage;
    private bool _statusIsError;

    public void Initialize() { }
    public void Update(double dt) { }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##kc_content");

        bool headerOpen = ImGui.CollapsingHeader("Kitten Color (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip(
            "Tints kitten character models by writing AlbedoColor into the\n" +
            "GPU material buffer. Only affects models using ModelPbr.frag\n" +
            "(fur, glass, eyes) — vehicle parts use a different shader path.\n\n" +
            "Alpha < 0.1 triggers discard (makes parts invisible).\n" +
            "The material list is for reference only — color applies to all.");
        if (!headerOpen)
        {
            SubmodUI.EndContentArea();
            return;
        }

        RenderBody();

        SubmodUI.EndContentArea();
    }

    internal void RenderBody()
    {
        RenderInitOrControls();
        RenderStatusMessage();
    }

    public void Dispose()
    {
        if (_tintActive)
        {
            KittenColor.ResetAll();
            _tintActive = false;
        }
        KittenColor.Cleanup();
    }

    // ---- Main rendering ----

    private void RenderInitOrControls()
    {
        if (!KittenColor.IsInitialized)
        {
            if (ImGui.Button(" Initialize "))
            {
                if (KittenColor.Initialize())
                    SetStatus($"Ready — {KittenColor.GetMaterials().Length} materials found.", false);
                else
                    SetStatus(KittenColor.LastError ?? "Initialization failed.", true);
            }
            ImGui.SameLine(0, 12);
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("Discovers GPU material system via reflection.");
            return;
        }

        RenderColorControls();
        ImGui.Spacing();
        RenderMaterialList();
    }

    // ---- Color controls ----

    private void RenderColorControls()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##kc_controls", 2, flags))
        {
            ImGui.TableSetupColumn("##kc_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##kc_widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            // Color picker with alpha
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Color");
            ImGui.TableNextColumn();
            if (ImGui.ColorEdit4("##kc_color", ref _color,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar)
                && KittenColor.IsInitialized)
            {
                if (KittenColor.ApplyToAll(_color))
                    _tintActive = true;
                else
                    SetStatus(KittenColor.LastError ?? "Apply failed.", true);
            }
            ImGui.SameLine(0, 8);

            if (!_tintActive) ImGui.BeginDisabled();
            if (ImGui.Button(" Reset "))
            {
                if (KittenColor.ResetAll())
                {
                    _tintActive = false;
                    _color = new float4(1f, 1f, 1f, 1f);
                    SetStatus("Colors reset to default.", false);
                }
                else
                {
                    SetStatus(KittenColor.LastError ?? "Reset failed.", true);
                }
            }
            if (!_tintActive) ImGui.EndDisabled();

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }

    // ---- Material list (read-only reference) ----

    private void RenderMaterialList()
    {
        var materials = KittenColor.GetMaterials();
        if (materials.Length == 0)
        {
            ImGui.TextDisabled("No materials found.");
            return;
        }

        ImGui.SeparatorText("GPU Materials (reference)");

        // Combo showing all materials
        string preview = _selectedMaterialIndex >= 0 && _selectedMaterialIndex < materials.Length
            ? $"[{materials[_selectedMaterialIndex].Handle}] {materials[_selectedMaterialIndex].Name}"
            : $"{materials.Length} materials";

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##kc_matsel", 2, flags))
        {
            ImGui.TableSetupColumn("##kc_mlbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##kc_mwidget", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Materials");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);

            if (ImGui.BeginCombo("##kc_materials", preview))
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    var (name, handle) = materials[i];
                    bool sel = _selectedMaterialIndex == i;
                    if (ImGui.Selectable($"[{handle}] {name}", sel))
                        _selectedMaterialIndex = i;
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Refresh "))
        {
            KittenColor.RefreshMaterialCache();
            _selectedMaterialIndex = -1;
            SetStatus($"Refreshed — {KittenColor.GetMaterials().Length} materials.", false);
        }
    }

    // ---- Status ----

    private void RenderStatusMessage()
    {
        if (string.IsNullOrEmpty(_statusMessage)) return;
        ImGui.Spacing();
        if (_statusIsError)
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _statusMessage);
        else
            ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f), _statusMessage);
    }

    private void SetStatus(string msg, bool isError)
    {
        _statusMessage = msg;
        _statusIsError = isError;
    }
}
