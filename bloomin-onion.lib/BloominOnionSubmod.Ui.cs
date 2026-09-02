using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;
using MeowSci.RockyMcRockFaceLib;

namespace MeowSci.BloominOnionLib;

public sealed partial class BloominOnionSubmod
{
    private static readonly float4 ErrorColor = new(1f, 0.3f, 0.3f, 1f);
    private static readonly float4 SuccessColor = new(0.4f, 1f, 0.4f, 1f);
    private static readonly float4 WarningColor = new(1f, 0.8f, 0.3f, 1f);

    private readonly ImInputString _assetFilter = new(128);
    private readonly ImInputString _presetName = new(64);
    private int _selectedBodyIndex;
    private string _selectedPreset = "";
    private string _statusMessage = "";
    private bool _statusIsError;

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##bloominonion_content");

        if (_bodies.Count == 0)
        {
            ImGui.TextDisabled("No celestial bodies found - load a save and this panel will populate.");
            SubmodUI.EndContentArea();
            return;
        }

        RenderStatusHints();
        _selectedBodyIndex = Math.Clamp(_selectedBodyIndex, 0, _bodies.Count - 1);
        var body = _bodies[_selectedBodyIndex];

        RenderBodyAndPresetRows(body);
        ImGui.Spacing();
        RenderGeometrySection(body);
        ImGui.Spacing();
        RenderBandSection();
        ImGui.Spacing();
        RenderVolumetricsSection();
        ImGui.Spacing();
        RenderRockFieldSection();
        ImGui.Spacing();
        RenderActions(body);
        RenderAppliedRings();

        SubmodUI.EndContentArea();
    }

    private void RenderStatusHints()
    {
        if (!GameSettings.ShowRings())
            ImGui.TextColored(ErrorColor, "Planetary rings are disabled in graphics settings - nothing will render.");
        else if (!GameSettings.ShowRingMeshes())
            ImGui.TextColored(WarningColor, "Ring meshes are disabled in graphics settings - only the flat band will show.");
        if (!_catalogReady)
            ImGui.TextColored(WarningColor, "Asset catalog still loading...");
        else if (!_controller.Stock.IsComplete)
            ImGui.TextColored(WarningColor, "Stock ring assets not found - fill every texture/mesh slot explicitly.");
        ImGui.TextDisabled($"{_controller.Catalog.MeshIds.Length} meshes, {_controller.Catalog.TextureIds.Length} textures, " +
                           $"{_presets.Names.Length} presets, {_controller.Applied.Count} ring(s) applied");
        ImGui.Spacing();
    }

    private void RenderBodyAndPresetRows(Celestial body)
    {
        if (!RockyUi.BeginFormTable("##bloominonion_top")) return;

        RockyUi.FormLabel("Body");
        if (ImGui.BeginCombo("##bloominonion_body", BodyLabel(body)))
        {
            if (ImGui.IsWindowAppearing())
            {
                ImGui.SetKeyboardFocusHere();
                _assetFilter.Clear();
            }
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("##bloominonion_body_filter", "filter...", _assetFilter);
            string filter = _assetFilter.ToString().Trim();
            for (int i = 0; i < _bodies.Count; i++)
            {
                if (filter.Length > 0 && !_bodies[i].Id.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                bool selected = i == _selectedBodyIndex;
                ImGui.PushID(i);
                if (ImGui.Selectable(BodyLabel(_bodies[i]), selected)) _selectedBodyIndex = i;
                ImGui.PopID();
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        RockyUi.FormLabel("Preset");
        string preview = _selectedPreset.Length > 0 ? _selectedPreset : "(select a saved preset)";
        if (ImGui.BeginCombo("##bloominonion_preset", preview))
        {
            foreach (var name in _presets.Names)
            {
                bool selected = string.Equals(name, _selectedPreset, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(name, selected))
                {
                    _selectedPreset = name;
                    _presetName.Value16 = name;
                }
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        RockyUi.FormLabel("Ring name");
        ImGui.InputTextWithHint("##bloominonion_preset_name", "name to save as...", _presetName);
        RockyUi.EndFormTable();

        RenderPresetButtons(body);
    }

    private void RenderPresetButtons(Celestial body)
    {
        string name = _presetName.ToString().Trim();
        if (name.Length == 0) ImGui.BeginDisabled();
        if (ImGui.Button(" Save Preset ##bloominonion_save"))
        {
            _editor.Name = name;
            _presets.Save(name, _editor);
            _selectedPreset = name;
            SetStatus($"saved preset '{name}' to {_presets.FilePath}", false);
        }
        if (name.Length == 0) ImGui.EndDisabled();

        ImGui.SameLine(0, 8);
        bool hasPreset = _selectedPreset.Length > 0 && _presets.Exists(_selectedPreset);
        if (!hasPreset) ImGui.BeginDisabled();
        if (ImGui.Button(" Load Preset ##bloominonion_load"))
        {
            var loaded = _presets.Get(_selectedPreset);
            if (loaded != null)
            {
                _editor = loaded;
                _presetName.Value16 = loaded.Name;
                SetStatus($"loaded preset '{loaded.Name}' into the editor", false);
            }
        }
        ImGui.SameLine(0, 8);
        RockyUi.DangerButtonBegin();
        if (ImGui.Button(" Delete Preset ##bloominonion_delete"))
        {
            _presets.Delete(_selectedPreset);
            SetStatus($"deleted preset '{_selectedPreset}'", false);
            _selectedPreset = "";
        }
        RockyUi.DangerButtonEnd();
        if (!hasPreset) ImGui.EndDisabled();

        ImGui.SameLine(0, 16);
        if (ImGui.Button(" New Ring ##bloominonion_new"))
        {
            _editor = RingDefinition.CreateDefault();
            SetStatus("editor reset to a Saturn-like painted ring", false);
        }
        ImGui.SetItemTooltip("Reset the editor to the default painted ring (stock LOD ladder, Saturn-like stripes).");

        var stock = body.BodyTemplate?.RingsReference;
        bool canCopy = stock != null && !_controller.TryGetApplied(body, out _);
        if (canCopy)
        {
            ImGui.SameLine(0, 8);
            if (ImGui.Button($" Copy {body.Id}'s Ring ##bloominonion_copy"))
            {
                _editor = RingDefinitionSerializer.FromReference($"{body.Id} ring", stock!);
                _presetName.Value16 = _editor.Name;
                SetStatus($"editor now mirrors {body.Id}'s stock ring (texture mode)", false);
            }
            ImGui.SetItemTooltip("Load this body's built-in ring definition into the editor as a starting point.");
        }
        if (_controller.TryGetApplied(body, out var applied))
        {
            ImGui.SameLine(0, 8);
            if (ImGui.Button(" Edit Applied ##bloominonion_edit_applied"))
            {
                _editor = applied.Definition.Clone();
                _presetName.Value16 = _editor.Name;
                SetStatus($"editor now holds the ring applied to {body.Id}", false);
            }
        }
    }

    private void RenderActions(Celestial body)
    {
        bool applied = _controller.TryGetApplied(body, out _);
        if (!_catalogReady) ImGui.BeginDisabled();
        if (ImGui.Button($" Apply to {body.Id} ##bloominonion_apply"))
        {
            _editor.Name = _presetName.ToString().Trim().Length > 0 ? _presetName.ToString().Trim() : _editor.Name;
            bool ok = _controller.Apply(body, _editor, out var message);
            SetStatus(message, !ok);
        }
        if (!_catalogReady) ImGui.EndDisabled();
        ImGui.SetItemTooltip(_controller.HasStockRings(body)
            ? "This body has a built-in ring; applying replaces it until you Remove."
            : "Builds the ring reference and rebuilds the renderer.");

        ImGui.SameLine(0, 8);
        if (!applied) ImGui.BeginDisabled();
        RockyUi.DangerButtonBegin();
        if (ImGui.Button($" Remove from {body.Id} ##bloominonion_remove"))
        {
            bool ok = _controller.Remove(body, out var message);
            SetStatus(message, !ok);
        }
        RockyUi.DangerButtonEnd();
        if (!applied) ImGui.EndDisabled();

        ImGui.SameLine(0, 8);
        if (_controller.Applied.Count == 0) ImGui.BeginDisabled();
        RockyUi.DangerButtonBegin();
        if (ImGui.Button(" Remove All ##bloominonion_remove_all"))
        {
            bool ok = _controller.RemoveAll(out var message);
            SetStatus(message, !ok);
        }
        RockyUi.DangerButtonEnd();
        if (_controller.Applied.Count == 0) ImGui.EndDisabled();

        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Rescan Assets ##bloominonion_rescan"))
        {
            RescanAssets();
            SetStatus("asset catalog rescanned", false);
        }

        ImGui.TextDisabled("Apply and Remove rebuild the renderer - expect a brief hitch. Rings are session-only.");
        if (_statusMessage.Length > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(_statusIsError ? ErrorColor : SuccessColor, _statusMessage);
        }
    }

    private void RenderAppliedRings()
    {
        if (_controller.Applied.Count == 0) return;
        ImGui.Spacing();
        ImGui.SeparatorText("Applied rings");
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        if (ImGui.BeginTable("##bloominonion_applied", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("##body", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##ring", ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableSetupColumn("##btn", ImGuiTableColumnFlags.WidthStretch, 1f);
            int row = 0;
            foreach (var applied in _controller.Applied)
            {
                ImGui.PushID(row++);
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text(applied.BodyId);
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding();
                ImGui.Text($"{applied.Definition.Name}  ({applied.Definition.InnerRadiusKm:N0}-{applied.Definition.OuterRadiusKm:N0} km)");
                ImGui.TableNextColumn();
                if (ImGui.SmallButton("Select"))
                    _selectedBodyIndex = Math.Max(0, _bodies.FindIndex(b => ReferenceEquals(b, applied.Celestial)));
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    private string BodyLabel(Celestial body)
    {
        if (_controller.TryGetApplied(body, out var applied)) return $"{body.Id}  [custom: {applied.Definition.Name}]";
        if (_controller.HasStockRings(body)) return $"{body.Id}  [stock rings]";
        return body.Id;
    }

    private void SetStatus(string message, bool isError)
    {
        _statusMessage = message;
        _statusIsError = isError;
    }
}
