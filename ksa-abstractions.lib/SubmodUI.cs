using Brutal.Numerics;
using Brutal.ImGuiApi;

namespace MeowSci.KsaAbstractions;

/// <summary>
/// Shared ImGui layout helpers for submod content rendering.
/// </summary>
public static class SubmodUI
{
    /// <summary>Horizontal padding (px) inside the submod content area.</summary>
    public const float ContentPaddingX = 12f;

    /// <summary>Vertical padding (px) applied to top and bottom of the submod content area.</summary>
    public const float ContentPaddingY = 20f;

    /// <summary>Extra padding (px) added only to the bottom, on top of <see cref="ContentPaddingY"/>.</summary>
    public const float ContentExtraBottomPaddingY = 20f;

    /// <summary>
    /// Begins a padded child window for submod content — no border, no scrollbar, auto-sizes
    /// vertically to fit content. Always pair with <see cref="EndContentArea"/>.
    /// Sets WindowPadding so all children (tables, collapsing headers, text) are
    /// consistently inset by <see cref="ContentPaddingX"/> on left and right.
    /// </summary>
    public static void BeginContentArea(string id)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(ContentPaddingX, ContentPaddingY));
        ImGui.BeginChild(id, new float2(0, 0),
            ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysUseWindowPadding,
            ImGuiWindowFlags.NoScrollbar);
        ImGui.PopStyleVar();
    }

    /// <summary>
    /// Closes the content area opened by <see cref="BeginContentArea"/>.
    /// </summary>
    public static void EndContentArea()
    {
        ImGui.Dummy(new float2(0, ContentExtraBottomPaddingY));
        ImGui.EndChild();
    }
}
