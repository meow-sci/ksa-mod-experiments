// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do not introduce background access to KSA state; parts-now must remain safe standalone.

using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// T13.2 — the "Paste XML" panel: a mod-id form, three tabbed XML documents, and the Validate /
/// Install &amp; Load pair that turns them into a real mod folder KSA will also load at next launch.
/// </summary>
/// <remarks>
/// <para>
/// Nothing expensive happens per frame. Mod-id validation runs only when the text actually changes
/// (the <c>InputText</c> return value says so), and bundle validation runs only when
/// <b>Validate</b> is pressed.
/// </para>
/// <para>
/// Every text buffer is a <c>readonly</c> field: <c>ImInputString</c> is the native buffer ImGui
/// edits in place, so a local would discard the user's typing on the next frame.
/// </para>
/// </remarks>
public sealed partial class PastePanel
{
    private readonly ImInputString _modId = new ImInputString(64);
    private readonly ImInputString _displayName = new ImInputString(128);
    private readonly ImInputString _author = new ImInputString(128, "parts-now"u8);
    private readonly ImInputString _version = new ImInputString(32, "1.0.0"u8);

    private readonly XmlTabEditor _xml = new XmlTabEditor();

    private readonly List<string> _modIdProblems = new List<string>();
    private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();
    private readonly List<string> _installedParts = new List<string>();

    private bool _modIdChecked;
    private bool _modIdValid;
    private string _targetPath = string.Empty;
    private bool _displayNameEdited;

    private bool _validated;
    private bool _validationClean;
    private string _validationSummary = string.Empty;

    private string _refusal = string.Empty;
    private bool _awaitingInstall;
    private string _installedPath = string.Empty;

    /// <summary>Draws the paste panel.</summary>
    /// <param name="canLoad">
    /// <c>StatusPanel.LoadingEnabled</c> — false when the reflection self-test failed or no mesh
    /// headroom was reserved, in which case Install &amp; Load stays disabled.
    /// </param>
    public void Render(bool canLoad)
    {
        CaptureInstallResult();

        bool open = ImGui.CollapsingHeader("Paste XML (?)##pn_paste", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip(
            "Writes the pasted documents into a brand new mod folder and loads it immediately. The "
            + "folder is a normal KSA mod, so the parts also load at the next launch.");

        if (!open)
        {
            return;
        }

        RenderForm();
        ImGui.Spacing();
        _xml.Render();
        ImGui.Spacing();
        RenderActions(canLoad);
        RenderIssues();
        RenderInstallResult();
    }

    private void RenderForm()
    {
        if (!PanelStyle.BeginLabelTable("##pn_form"))
        {
            return;
        }

        PanelStyle.LabelRow("Mod Id");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputTextWithHint("##pn_modid", "my-new-parts"u8, _modId))
        {
            RevalidateModId();
        }

        PanelStyle.LabelRow("Display Name");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText("##pn_displayname", _displayName))
        {
            // Once the user touches this field it stops mirroring the mod id.
            _displayNameEdited = true;
        }

        PanelStyle.LabelRow("Author");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputText("##pn_author", _author);

        PanelStyle.LabelRow("Version");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputText("##pn_version", _version);

        PanelStyle.EndLabelTable();

        for (int i = 0; i < _modIdProblems.Count; i++)
        {
            ImGui.TextColored(PanelStyle.Error, _modIdProblems[i]);
        }

        if (_modIdChecked && _targetPath.Length > 0)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Will be written to:");
            ImGui.SameLine(0, 8);
            ImGui.TextColored(_modIdValid ? PanelStyle.Success : PanelStyle.Error, _targetPath);
        }
    }

    private void RevalidateModId()
    {
        string id = _modId.ToString().Trim();

        _modIdChecked = true;
        _modIdProblems.Clear();
        _modIdProblems.AddRange(ModIdValidator.Validate(id));
        _modIdValid = _modIdProblems.Count == 0;
        _targetPath = ModIdValidator.ResolveTargetPath(id);

        if (!_displayNameEdited)
        {
            _displayName.SetValue(id.AsSpan());
        }

        // A changed id changes what V3/V14 collide against, so the previous verdict is stale.
        _validated = false;
        _validationClean = false;
        _validationSummary = string.Empty;
        _issues.Clear();
    }

    private void RenderActions(bool canLoad)
    {
        bool busy = RuntimeModLoader.IsBusy;
        bool hasXml = !_xml.IsEmpty;

        bool canValidate = hasXml && !busy;
        if (!canValidate)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button(" Validate ##pn_validate") && canValidate)
        {
            Validate();
        }

        if (!canValidate)
        {
            ImGui.EndDisabled();
        }

        PanelStyle.HoverTooltip(hasXml
            ? "Parses the documents and runs all fifteen rules. Nothing is written or registered."
            : "Paste at least one XML document first.");

        bool canInstall = canLoad && !busy && _modIdValid && _validated && _validationClean && hasXml;
        ImGui.SameLine(0, 8);
        if (!canInstall)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button(" Install & Load ##pn_install") && canInstall)
        {
            Install();
        }

        if (!canInstall)
        {
            ImGui.EndDisabled();
        }

        PanelStyle.HoverTooltip(InstallTooltip(canLoad, busy, hasXml));

        if (_validationSummary.Length > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(_validationClean ? PanelStyle.Success : PanelStyle.Error, _validationSummary);
        }

        if (_refusal.Length > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(PanelStyle.Error, _refusal);
        }
    }

    private string InstallTooltip(bool canLoad, bool busy, bool hasXml)
    {
        if (!canLoad)
        {
            return "Loading is disabled — see the banner at the top of this window.";
        }

        if (busy)
        {
            return "A parts-now job is already running.";
        }

        if (!hasXml)
        {
            return "Paste at least one XML document first.";
        }

        if (!_modIdValid)
        {
            return "Enter a valid, unused mod id first.";
        }

        if (!_validated)
        {
            return "Press Validate first.";
        }

        return _validationClean
            ? "Writes the mod folder, adds it to the game's mod manifest, then loads it."
            : "Validation found errors — fix them and validate again.";
    }
}
