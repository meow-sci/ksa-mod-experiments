// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// GPU load/purge operations use RuntimeModLoader.Step at the host BeforeGui boundary,
// before this frame emits any ImGui texture draw commands.

using System.Collections.Generic;
using Brutal.ImGuiApi;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// The paste panel's actions: running the validation rules, starting the install, picking up
/// its result, and rendering both.
/// </summary>
/// <remarks>
/// None of this runs per frame. <see cref="Validate" /> and <see cref="Install" /> are button
/// handlers, and <c>CaptureInstallResult</c> does its work exactly once, on the first frame
/// after the job it started stops being busy.
/// </remarks>
public sealed partial class PastePanel
{
    private void Validate()
    {
        _issues.Clear();
        _validated = true;

        List<ParsedBundle> bundles = new List<ParsedBundle>(3);
        Parse("Assets", _xml.AssetsXml, bundles);
        Parse("Part", _xml.PartXml, bundles);
        Parse("GameData", _xml.GameDataXml, bundles);

        // reloadingModId is null: this is always a fresh install, so no id may collide with anything
        // already registered. The target directory does not exist yet, which BundleValidator handles
        // by degrading the file-existence half of V6 and V11 to warnings.
        string modId = _modId.ToString().Trim();
        _issues.AddRange(BundleValidator.Validate(bundles, reloadingModId: null,
            ModIdValidator.ResolveTargetPath(modId)));

        int errors = 0;
        int warnings = 0;
        for (int i = 0; i < _issues.Count; i++)
        {
            if (_issues[i].Severity == IssueSeverity.Error)
            {
                errors++;
            }
            else
            {
                warnings++;
            }
        }

        _validationClean = errors == 0;
        _validationSummary = _validationClean
            ? $"Validation passed: {bundles.Count} document(s), {warnings} warning(s)."
            : $"Validation FAILED: {errors} error(s), {warnings} warning(s). Nothing was written.";
    }

    private void Parse(string sourceName, string xml, List<ParsedBundle> bundles)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return;
        }

        if (BundleParser.TryParse(sourceName, xml, out ParsedBundle? parsed, out string? error)
            && parsed is not null)
        {
            bundles.Add(parsed);
            return;
        }

        _issues.Add(BundleValidator.ParseFailure(sourceName, error ?? "unknown parse failure."));
    }

    private void Install()
    {
        _refusal = string.Empty;
        _installedPath = string.Empty;
        _installedParts.Clear();

        ModFolderRequest request = new ModFolderRequest(
            _modId.ToString().Trim(),
            _displayName.ToString().Trim(),
            _author.ToString().Trim(),
            _version.ToString().Trim(),
            _xml.AssetsXml,
            _xml.PartXml,
            _xml.GameDataXml);

        if (!RuntimeModLoader.StartInstall(request, out string? refusal))
        {
            _refusal = refusal ?? "the install was refused for an unknown reason.";
            return;
        }

        _awaitingInstall = true;
    }

    /// <summary>
    /// Picks up the result of an install this panel started, exactly once, on the first frame after
    /// the job stops being busy.
    /// </summary>
    private void CaptureInstallResult()
    {
        if (!_awaitingInstall || RuntimeModLoader.IsBusy)
        {
            return;
        }

        _awaitingInstall = false;

        if (RuntimeModLoader.State != LoadJobState.Done
            || RuntimeModLoader.CurrentRecord is not { } record)
        {
            return;
        }

        _installedPath = record.ModDir;
        _installedParts.Clear();
        foreach (PartTemplate part in record.NewParts)
        {
            if (!part.IsSubPart)
            {
                _installedParts.Add(part.Id);
            }
        }

        // The id is now taken, so the form's verdict must not keep claiming it is free.
        RevalidateModId();
    }

    private void RenderIssues()
    {
        if (_issues.Count == 0)
        {
            return;
        }

        ImGui.Spacing();
        ImGui.SeparatorText($"Validation issues ( {_issues.Count} )");
        ValidationIssueView.Render("pn_paste_issues", _issues);
    }

    private void RenderInstallResult()
    {
        if (_installedPath.Length == 0)
        {
            return;
        }

        ImGui.Spacing();
        ImGui.SeparatorText("Installed");
        ImGui.TextColored(PanelStyle.Success, "The mod folder was written and loaded.");
        PanelStyle.CopyableText("pn_installed", _installedPath);

        if (_installedParts.Count == 0)
        {
            ImGui.TextDisabled("No top-level Parts were registered — only SubParts or assets.");
            return;
        }

        ImGui.Text($"Parts now available in the vehicle editor ( {_installedParts.Count} ):");
        for (int i = 0; i < _installedParts.Count; i++)
        {
            ImGui.BulletText(_installedParts[i]);
        }
    }
}
