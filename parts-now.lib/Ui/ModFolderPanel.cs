// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.PartsNowLib;

/// <summary>
/// T13.3 — the "Mod folder" panel: what is on disk under the game's mods directory, and the
/// Load / Reload / Unload actions parts-now is allowed to perform on each entry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing is scanned or safety-checked per frame.</b> <c>ModFolderScanner.Scan()</c> touches the
/// file system and <c>RuntimeModUnloader.CheckCanUnload</c> walks every live vehicle, so both run
/// only on demand: on the first render, when <b>Rescan</b> is pressed, when the selection changes,
/// and once when a load job finishes.
/// </para>
/// <para>
/// Reload and Unload are destructive — they dispose GPU objects and unregister templates — so both
/// go through a confirmation modal naming the mod id and its part count.
/// </para>
/// </remarks>
public sealed partial class ModFolderPanel
{
    private const string ConfirmPopupId = "Confirm##pn_confirm";
    private const float TableHeight = 200f;

    private readonly ImInputString _filter = new ImInputString(128);
    private readonly List<ScannedMod> _mods = new List<ScannedMod>();

    private bool _scanned;
    private string _selectedModId = string.Empty;
    private string? _selectedGateReason;
    private LoadJobState _lastLoaderState = LoadJobState.Idle;

    private string _actionMessage = string.Empty;
    private bool _actionIsError;

    private bool _openConfirm;
    private bool _confirmIsUnload;
    private string _confirmModId = string.Empty;
    private int _confirmPartCount;

    /// <summary>Draws the mod-folder panel.</summary>
    /// <param name="canLoad">
    /// <c>StatusPanel.LoadingEnabled</c> — false when the reflection self-test failed or no mesh
    /// headroom was reserved, in which case every action stays disabled.
    /// </param>
    public void Render(bool canLoad)
    {
        RescanWhenJobFinished();

        if (!_scanned)
        {
            Rescan();
        }

        bool open = ImGui.CollapsingHeader("Mod folders (?)##pn_folders", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip(
            "Every folder under the game's mods directory that contains a mod.toml. Only content "
            + "mods that KSA did not already load at startup can be loaded from here.");

        if (!open)
        {
            return;
        }

        PanelStyle.CopyableText("pn_modsdir", ModIdValidator.ModsDirectory);

        if (ImGui.Button(" Rescan ##pn_rescan"))
        {
            Rescan();
        }

        ImGui.SameLine(0, 8);
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##pn_modfilter", "filter..."u8, _filter);

        RenderTable();
        RenderSelection(canLoad);
        RenderMessage();

        if (_openConfirm)
        {
            ImGui.OpenPopup(ConfirmPopupId);
            _openConfirm = false;
        }

        RenderConfirmModal();
    }

    /// <summary>
    /// Re-reads the mods directory and re-evaluates the selected mod's safety gate. Called on the
    /// first render, from the Rescan button, and after every completed job.
    /// </summary>
    public void Rescan()
    {
        _scanned = true;
        _mods.Clear();
        _mods.AddRange(ModFolderScanner.Scan());
        RefreshSelectionGate();
    }

    private void RescanWhenJobFinished()
    {
        LoadJobState state = RuntimeModLoader.State;
        if (state == _lastLoaderState)
        {
            return;
        }

        _lastLoaderState = state;

        // Done and Failed both change what is loaded: a failed reload has already purged the
        // previous load, so the table would otherwise still show it as loaded.
        if (state is LoadJobState.Done or LoadJobState.Failed)
        {
            Rescan();
        }
    }

    private void RenderTable()
    {
        string filter = _filter.ToString().Trim();

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));

        ImGuiTableFlags flags = ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;

        if (ImGui.BeginTable("##pn_modtable", 5, flags, new float2(0f, TableHeight)))
        {
            ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthStretch, 3f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 3f);
            ImGui.TableSetupColumn("Kind", ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableSetupColumn("Assets", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            int shown = 0;
            for (int i = 0; i < _mods.Count; i++)
            {
                ScannedMod mod = _mods[i];
                if (filter.Length > 0
                    && !mod.ModId.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    && !mod.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                shown++;
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                bool selected = string.Equals(mod.ModId, _selectedModId, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable($"{mod.ModId}##pn_row_{i}", selected, ImGuiSelectableFlags.SpanAllColumns))
                {
                    _selectedModId = mod.ModId;
                    RefreshSelectionGate();
                }

                ImGui.TableNextColumn();
                ImGui.Text(mod.DisplayName);
                ImGui.TableNextColumn();
                ImGui.Text(mod.Kind.ToString());
                ImGui.TableNextColumn();
                RenderState(mod.State);
                ImGui.TableNextColumn();
                ImGui.Text(mod.AssetFiles.Count.ToString());
            }

            ImGui.EndTable();

            if (shown == 0)
            {
                ImGui.TextDisabled(_mods.Count == 0
                    ? "No mod folders were found."
                    : "No mod folder matches the filter.");
            }
        }

        ImGui.PopStyleVar();
    }

    private static void RenderState(ModFolderState state)
    {
        switch (state)
        {
            case ModFolderState.LoadedByPartsNow:
                ImGui.TextColored(PanelStyle.Success, "loaded (parts-now)");
                break;
            case ModFolderState.LoadedAtBoot:
                ImGui.TextDisabled("loaded at boot");
                break;
            default:
                ImGui.TextDisabled("not loaded");
                break;
        }
    }

    private ScannedMod? Selected()
    {
        for (int i = 0; i < _mods.Count; i++)
        {
            if (string.Equals(_mods[i].ModId, _selectedModId, StringComparison.OrdinalIgnoreCase))
            {
                return _mods[i];
            }
        }

        return null;
    }

    private void RefreshSelectionGate()
    {
        _selectedGateReason = null;

        LoadedModRecord? record = RuntimeModRegistry.Find(_selectedModId);
        if (record is null)
        {
            return;
        }

        // Walks live vehicles and the editor — far too expensive for a render loop, which is why it
        // is cached here and only recomputed on selection change / rescan.
        _selectedGateReason = RuntimeModUnloader.CheckCanUnload(record, RuntimeModLoader.IsBusy);
    }

    private void RenderSelection(bool canLoad)
    {
        ScannedMod? selected = Selected();
        if (selected is null)
        {
            ImGui.TextDisabled("Select a mod folder to see its asset files and load it.");
            return;
        }

        ImGui.Spacing();
        PanelStyle.BeginBorderedChild($"##pn_seldetail_{selected.ModId}", 0f);

        ImGui.Text($"{selected.DisplayName}  ({selected.ModId})");
        ImGui.TextDisabled(selected.Directory);
        if (selected.Version.Length > 0 || selected.Author.Length > 0)
        {
            ImGui.TextDisabled($"version {selected.Version}   by {selected.Author}");
        }

        ImGui.Spacing();
        RenderAssetFiles(selected);
        ImGui.Spacing();
        RenderActions(selected, canLoad);

        ImGui.Spacing();
        ImGui.EndChild();
    }

    private static void RenderAssetFiles(ScannedMod mod)
    {
        if (mod.AssetFiles.Count == 0)
        {
            ImGui.TextDisabled("This mod declares no asset files.");
            return;
        }

        ImGui.Text("Asset files:");
        for (int i = 0; i < mod.AssetFiles.Count; i++)
        {
            ModAssetFile file = mod.AssetFiles[i];
            if (file.Exists)
            {
                ImGui.TextColored(PanelStyle.Success, $"  [ok]      {file.RelativePath}");
            }
            else
            {
                ImGui.TextColored(PanelStyle.Error, $"  [missing] {file.RelativePath}");
            }
        }
    }
}
