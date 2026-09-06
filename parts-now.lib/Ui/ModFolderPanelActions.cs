// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do not introduce background access to KSA state; parts-now must remain safe standalone.

using System;
using Brutal.ImGuiApi;

namespace MeowSci.PartsNowLib;

/// <summary>
/// The mod-folder panel's Load / Reload / Unload buttons, the reason tooltips shown when they
/// are disabled, and the destructive-action confirmation modal.
/// </summary>
/// <remarks>
/// The safety gate itself is not evaluated here — it is read from the cached
/// <c>_selectedGateReason</c>, which the panel refreshes only when the selection changes, on a
/// rescan, and once per finished job. Walking every live vehicle every frame would be absurd.
/// </remarks>
public sealed partial class ModFolderPanel
{
    private void RenderActions(ScannedMod mod, bool canLoad)
    {
        bool busy = RuntimeModLoader.IsBusy;
        bool isOurs = mod.State == ModFolderState.LoadedByPartsNow;
        LoadedModRecord? record = RuntimeModRegistry.Find(mod.ModId);

        bool canLoadThis = canLoad && !busy && mod.Loadable && mod.State == ModFolderState.NotLoaded;
        Action(" Load ##pn_load", canLoadThis, LoadTooltip(mod, canLoad, busy), () =>
        {
            if (RuntimeModLoader.StartLoad(mod.Directory, mod.ModId, out string? refusal))
            {
                SetMessage($"Loading '{mod.ModId}'...", isError: false);
            }
            else
            {
                SetMessage(refusal ?? "the load was refused.", isError: true);
            }
        });

        ImGui.SameLine(0, 8);

        bool gateOpen = _selectedGateReason is null;
        bool canReload = canLoad && !busy && isOurs && record is not null && gateOpen;
        PanelStyle.PushDanger();
        Action(" Reload ##pn_reload", canReload, MutateTooltip(mod, canLoad, busy, isOurs, "reloaded"), () =>
        {
            _confirmIsUnload = false;
            _confirmModId = mod.ModId;
            _confirmPartCount = record?.NewParts.Count ?? 0;
            _openConfirm = true;
        });

        ImGui.SameLine(0, 8);

        bool canUnload = !busy && isOurs && record is not null && gateOpen;
        Action(" Unload ##pn_unload", canUnload, MutateTooltip(mod, canLoad: true, busy, isOurs, "unloaded"), () =>
        {
            _confirmIsUnload = true;
            _confirmModId = mod.ModId;
            _confirmPartCount = record?.NewParts.Count ?? 0;
            _openConfirm = true;
        });
        PanelStyle.PopDanger();

        if (mod.NotLoadableReason is { } reason)
        {
            ImGui.Spacing();
            ImGui.TextDisabled($"Not loadable: {reason}");
        }

        if (_selectedGateReason is { } gate)
        {
            ImGui.Spacing();
            ImGui.TextColored(PanelStyle.Warning, $"Reload / unload blocked: {gate}");
        }
    }

    private static void Action(string label, bool enabled, string tooltip, Action onClick)
    {
        if (!enabled)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button(label) && enabled)
        {
            onClick();
        }

        if (!enabled)
        {
            ImGui.EndDisabled();
        }

        PanelStyle.HoverTooltip(tooltip);
    }

    private static string LoadTooltip(ScannedMod mod, bool canLoad, bool busy)
    {
        if (!canLoad)
        {
            return "Loading is disabled — see the banner at the top of this window.";
        }

        if (busy)
        {
            return "A parts-now job is already running.";
        }

        if (mod.State != ModFolderState.NotLoaded)
        {
            return mod.State == ModFolderState.LoadedAtBoot
                ? "Loaded at startup — restart the game to reload it. parts-now cannot account for "
                    + "what KSA registered on its behalf."
                : "Already loaded by parts-now — use Reload.";
        }

        return mod.NotLoadableReason ?? "Validates, registers, binds and thumbnails this mod folder.";
    }

    private string MutateTooltip(ScannedMod mod, bool canLoad, bool busy, bool isOurs, string verb)
    {
        if (!canLoad)
        {
            return "Loading is disabled — see the banner at the top of this window.";
        }

        if (busy)
        {
            return "A parts-now job is already running.";
        }

        if (!isOurs)
        {
            return mod.State == ModFolderState.LoadedAtBoot
                ? $"Only mods parts-now loaded this session can be {verb}."
                : "This mod is not loaded.";
        }

        return _selectedGateReason ?? $"Purges every template, mesh and texture this mod registered, "
            + (verb == "reloaded" ? "then loads it again from disk." : "leaving it unloaded.");
    }

    private void RenderConfirmModal()
    {
        bool open = true;
        if (!ImGui.BeginPopupModal(ConfirmPopupId, ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        string verb = _confirmIsUnload ? "Unload" : "Reload";
        ImGui.Text($"{verb} '{_confirmModId}'?");
        ImGui.Spacing();
        ImGui.TextWrapped(
            $"This purges the {_confirmPartCount} part template(s) parts-now registered for this mod, "
            + "along with their meshes, textures and materials. Their slice of KSA's shared vertex "
            + "buffer is NOT reclaimed — it stays spent until the game restarts.");

        if (!_confirmIsUnload)
        {
            ImGui.Spacing();
            ImGui.TextWrapped("The mod is then loaded again from disk, re-reading every file.");
        }

        ImGui.Spacing();
        PanelStyle.PushDanger();
        if (ImGui.Button($" {verb} ##pn_confirm_yes"))
        {
            Confirm();
            ImGui.CloseCurrentPopup();
        }

        PanelStyle.PopDanger();

        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Cancel ##pn_confirm_no"))
        {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void Confirm()
    {
        if (_confirmIsUnload)
        {
            // Deferred to the next Update(dt), NEVER run here. Purging disposes ThumbnailReferences,
            // which calls ImGuiBackend.Vulkan.RemoveTexture (vkFreeDescriptorSets) and destroys the
            // image view immediately. This method runs from [StarMapAfterGui], a postfix on
            // Program.OnDrawUiViewports — but the vehicle editor's part browser is an IStaticWindow
            // drawn by ImGuiWindow.DrawAllStaticWindows inside OnDrawUiFrame (Program.cs:2898),
            // EARLIER in this same frame. Its ImageButton draw commands already hold those descriptor
            // sets, and ImGui.Render() has not run yet, so freeing them here is a use-after-free.
            // Device.WaitIdle() in the purge does not help: it drains submitted frames, and this one
            // has not been submitted.
            //
            // Update(dt) runs from [StarMapBeforeGui], a prefix on OnDrawUiFrame — before anything
            // has drawn this frame. That is where every other purge in parts-now already runs.
            _pendingUnloadModId = _confirmModId;
            SetMessage($"Unloading '{_confirmModId}'...", isError: false);
            return;
        }

        if (RuntimeModLoader.StartReload(_confirmModId, out string? reloadRefusal))
        {
            SetMessage($"Reloading '{_confirmModId}'...", isError: false);
        }
        else
        {
            SetMessage(reloadRefusal ?? "the reload was refused.", isError: true);
        }
    }

    /// <summary>
    /// Runs an unload the confirm modal requested, from the pre-GUI phase where freeing ImGui
    /// textures is safe. Call once per frame from <c>PartsNowSubmod.Update(dt)</c>, before anything
    /// has drawn. A no-op when nothing is pending.
    /// </summary>
    public void ProcessPendingActions()
    {
        if (_pendingUnloadModId.Length == 0)
        {
            return;
        }

        string modId = _pendingUnloadModId;
        _pendingUnloadModId = string.Empty;

        if (RuntimeModLoader.Unload(modId, out string? refusal))
        {
            SetMessage($"Unloaded '{modId}'.", isError: false);
            Rescan();
        }
        else
        {
            SetMessage(refusal ?? "the unload was refused.", isError: true);
        }
    }

    private void SetMessage(string message, bool isError)
    {
        _actionMessage = message;
        _actionIsError = isError;
    }

    private void RenderMessage()
    {
        if (_actionMessage.Length == 0)
        {
            return;
        }

        ImGui.Spacing();
        if (_actionIsError)
        {
            ImGui.TextColored(PanelStyle.Error, _actionMessage);
        }
        else
        {
            ImGui.TextDisabled(_actionMessage);
        }
    }
}
