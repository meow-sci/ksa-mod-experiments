// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// GPU load/purge operations use RuntimeModLoader.Step at the host BeforeGui boundary,
// before this frame emits any ImGui texture draw commands.

using Brutal.ImGuiApi;

namespace MeowSci.PartsNowLib;

/// <summary>
/// The two collapsed reference sections of the status strip: the persisted mesh-headroom settings,
/// and the standing limitations every parts-now user has to know about (plan §16).
/// </summary>
public sealed partial class StatusPanel
{
    /// <summary>
    /// The limitations from the plan's §16, shown verbatim so nobody has to read the README to find
    /// out why a reload was refused or where their mesh memory went.
    /// </summary>
    private static readonly string[] Limitations =
    {
        "Mesh memory is never reclaimed. Every reload permanently spends part of the reserved "
            + "headroom in KSA's shared interleaved buffer until the game restarts.",
        "Headroom is fixed at launch. Changing it below requires a restart to take effect.",
        "New EditorTags, Substances, Reactions and GrainGeometry are rejected by validation. Parts "
            + "must reference ids that already exist in the game.",
        "Mods KSA loaded at boot cannot be reloaded or unloaded — only mods parts-now itself loaded "
            + "this session.",
        "Reload and unload require the mod's parts to be unused: no live vehicle may use one and the "
            + "vehicle editor must not hold one. An open editor containing none of them is fine.",
        "Raytracing (IVA) is untested. With IVARayTracing enabled the shared buffer is allocated "
            + "through the raytrace allocator; verify before relying on it.",
        "Saved vehicles depend on the mod folder staying put. Deleting it breaks every vehicle that "
            + "used its parts.",
    };

    private bool _settingsLoaded;
    private int _vertexHeadroomMiB = PartsNowSettings.DefaultVertexHeadroomMiB;
    private int _indexHeadroomMiB = PartsNowSettings.DefaultIndexHeadroomMiB;
    private string _settingsMessage = string.Empty;

    internal void BindDraft(MeowSci.KsaAbstractions.DraftBindings state)
    {
        _vertexHeadroomMiB = PartsNowSettings.VertexHeadroomMiB; _indexHeadroomMiB = PartsNowSettings.IndexHeadroomMiB; _settingsLoaded = true;
        state.Value("VertexHeadroomMiB", () => _vertexHeadroomMiB, v => _vertexHeadroomMiB = v);
        state.Value("IndexHeadroomMiB", () => _indexHeadroomMiB, v => _indexHeadroomMiB = v);
    }
    private void RenderSettings()
    {
        bool open = MeowSci.KsaAbstractions.WorkspaceUi.Header("Settings (?)##pn_settings");
        ImGui.SetItemTooltip(
            "How much room parts-now reserves inside KSA's single shared vertex / index buffer pair "
            + "during startup. Everything it ever loads has to fit in there.");

        if (!open)
        {
            return;
        }

        if (!_settingsLoaded)
        {
            // Read once: every getter calls PartsNowSettings.Load(), which touches the file system
            // on its first call.
            _vertexHeadroomMiB = PartsNowSettings.VertexHeadroomMiB;
            _indexHeadroomMiB = PartsNowSettings.IndexHeadroomMiB;
            _settingsLoaded = true;
        }

        if (PanelStyle.BeginLabelTable("##pn_settings_tbl"))
        {
            PanelStyle.LabelRow("Vertex headroom (MiB)");
            ImGui.SetNextItemWidth(-1f);
            ImGui.DragInt(
                "##pn_vtx_headroom", ref _vertexHeadroomMiB, 1f,
                PartsNowSettings.MinHeadroomMiB, PartsNowSettings.MaxHeadroomMiB,
                default, ImGuiSliderFlags.AlwaysClamp);

            PanelStyle.LabelRow("Index headroom (MiB)");
            ImGui.SetNextItemWidth(-1f);
            ImGui.DragInt(
                "##pn_idx_headroom", ref _indexHeadroomMiB, 1f,
                PartsNowSettings.MinHeadroomMiB, PartsNowSettings.MaxHeadroomMiB,
                default, ImGuiSliderFlags.AlwaysClamp);

            PanelStyle.EndLabelTable();
        }

        ImGui.TextColored(
            PanelStyle.Warning,
            "Headroom changes take effect on the NEXT LAUNCH of the game. The buffers are allocated "
            + "once during startup and can never be resized afterwards.");

        if (ImGui.Button(" Save ##pn_settings_save"))
        {
            PartsNowSettings.VertexHeadroomMiB = _vertexHeadroomMiB;
            PartsNowSettings.IndexHeadroomMiB = _indexHeadroomMiB;
            PartsNowSettings.Save();

            // Read back: the setters clamp, so this shows the user what was actually stored.
            _vertexHeadroomMiB = PartsNowSettings.VertexHeadroomMiB;
            _indexHeadroomMiB = PartsNowSettings.IndexHeadroomMiB;
            _settingsMessage = "Saved. Restart the game for the new headroom to take effect.";
        }

        ImGui.SameLine(0, 12);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(PartsNowSettings.FilePath);

        if (_settingsMessage.Length > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(PanelStyle.Success, _settingsMessage);
        }

        ImGui.Spacing();
    }

    private static void RenderLimitations()
    {
        bool open = MeowSci.KsaAbstractions.WorkspaceUi.Header("Limitations (?)##pn_limits");
        ImGui.SetItemTooltip("What parts-now deliberately cannot do, and why.");

        if (!open)
        {
            return;
        }

        for (int i = 0; i < Limitations.Length; i++)
        {
            ImGui.Bullet();
            ImGui.SameLine(0, 4);
            ImGui.TextWrapped(Limitations[i]);
        }

        ImGui.Spacing();
    }
}
