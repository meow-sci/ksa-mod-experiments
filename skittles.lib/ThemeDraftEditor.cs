using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;
namespace MeowSci.SkittlesLib;
internal static class ThemeDraftEditor
{
    public static void Render(ThemeDefinition theme)
    {
        if (WorkspaceUi.Header("Style settings"))
        {
        using var grid = new FormGrid("theme-fields");
        { float v = theme.Alpha; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Alpha"), ref v, .1f)) theme.Alpha = v; }
        { float v = theme.DisabledAlpha; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("DisabledAlpha"), ref v, .1f)) theme.DisabledAlpha = v; }
        { float v = theme.WindowRounding; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("WindowRounding"), ref v, .1f)) theme.WindowRounding = v; }
        { float v = theme.WindowBorderSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("WindowBorderSize"), ref v, .1f)) theme.WindowBorderSize = v; }
        { float v = theme.WindowBorderHoverPadding; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("WindowBorderHoverPadding"), ref v, .1f)) theme.WindowBorderHoverPadding = v; }
        { float v = theme.ChildRounding; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("ChildRounding"), ref v, .1f)) theme.ChildRounding = v; }
        { float v = theme.ChildBorderSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("ChildBorderSize"), ref v, .1f)) theme.ChildBorderSize = v; }
        { float v = theme.PopupRounding; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("PopupRounding"), ref v, .1f)) theme.PopupRounding = v; }
        { float v = theme.PopupBorderSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("PopupBorderSize"), ref v, .1f)) theme.PopupBorderSize = v; }
        { float v = theme.FrameRounding; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("FrameRounding"), ref v, .1f)) theme.FrameRounding = v; }
        { float v = theme.FrameBorderSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("FrameBorderSize"), ref v, .1f)) theme.FrameBorderSize = v; }
        { float v = theme.IndentSpacing; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("IndentSpacing"), ref v, .1f)) theme.IndentSpacing = v; }
        { float v = theme.ColumnsMinSpacing; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("ColumnsMinSpacing"), ref v, .1f)) theme.ColumnsMinSpacing = v; }
        { float v = theme.ScrollbarSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("ScrollbarSize"), ref v, .1f)) theme.ScrollbarSize = v; }
        { float v = theme.ScrollbarRounding; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("ScrollbarRounding"), ref v, .1f)) theme.ScrollbarRounding = v; }
        { float v = theme.GrabMinSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("GrabMinSize"), ref v, .1f)) theme.GrabMinSize = v; }
        { float v = theme.GrabRounding; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("GrabRounding"), ref v, .1f)) theme.GrabRounding = v; }
        { float v = theme.LogSliderDeadzone; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("LogSliderDeadzone"), ref v, .1f)) theme.LogSliderDeadzone = v; }
        { float v = theme.ImageBorderSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("ImageBorderSize"), ref v, .1f)) theme.ImageBorderSize = v; }
        { float v = theme.TabRounding; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("TabRounding"), ref v, .1f)) theme.TabRounding = v; }
        { float v = theme.TabBorderSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("TabBorderSize"), ref v, .1f)) theme.TabBorderSize = v; }
        { float v = theme.TabMinWidthBase; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("TabMinWidthBase"), ref v, .1f)) theme.TabMinWidthBase = v; }
        { float v = theme.TabMinWidthShrink; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("TabMinWidthShrink"), ref v, .1f)) theme.TabMinWidthShrink = v; }
        { float v = theme.TabCloseButtonMinWidthSelected; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("TabCloseButtonMinWidthSelected"), ref v, .1f)) theme.TabCloseButtonMinWidthSelected = v; }
        { float v = theme.TabCloseButtonMinWidthUnselected; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("TabCloseButtonMinWidthUnselected"), ref v, .1f)) theme.TabCloseButtonMinWidthUnselected = v; }
        { float v = theme.TabBarBorderSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("TabBarBorderSize"), ref v, .1f)) theme.TabBarBorderSize = v; }
        { float v = theme.TabBarOverlineSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("TabBarOverlineSize"), ref v, .1f)) theme.TabBarOverlineSize = v; }
        { float v = theme.TableAngledHeadersAngle; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("TableAngledHeadersAngle"), ref v, .1f)) theme.TableAngledHeadersAngle = v; }
        { float v = theme.TreeLinesSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("TreeLinesSize"), ref v, .1f)) theme.TreeLinesSize = v; }
        { float v = theme.TreeLinesRounding; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("TreeLinesRounding"), ref v, .1f)) theme.TreeLinesRounding = v; }
        { float v = theme.SeparatorTextBorderSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("SeparatorTextBorderSize"), ref v, .1f)) theme.SeparatorTextBorderSize = v; }
        { float v = theme.DockingSeparatorSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("DockingSeparatorSize"), ref v, .1f)) theme.DockingSeparatorSize = v; }
        { float v = theme.MouseCursorScale; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("MouseCursorScale"), ref v, .1f)) theme.MouseCursorScale = v; }
        { float v = theme.CurveTessellationTol; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("CurveTessellationTol"), ref v, .1f)) theme.CurveTessellationTol = v; }
        { float v = theme.CircleTessellationMaxError; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("CircleTessellationMaxError"), ref v, .1f)) theme.CircleTessellationMaxError = v; }
        { var a = theme.WindowPadding; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("WindowPadding"), ref v, .1f)) theme.WindowPadding = new[] { v.X, v.Y }; } }
        { var a = theme.WindowMinSize; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("WindowMinSize"), ref v, .1f)) theme.WindowMinSize = new[] { v.X, v.Y }; } }
        { var a = theme.WindowTitleAlign; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("WindowTitleAlign"), ref v, .1f)) theme.WindowTitleAlign = new[] { v.X, v.Y }; } }
        { var a = theme.FramePadding; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("FramePadding"), ref v, .1f)) theme.FramePadding = new[] { v.X, v.Y }; } }
        { var a = theme.ItemSpacing; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("ItemSpacing"), ref v, .1f)) theme.ItemSpacing = new[] { v.X, v.Y }; } }
        { var a = theme.ItemInnerSpacing; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("ItemInnerSpacing"), ref v, .1f)) theme.ItemInnerSpacing = new[] { v.X, v.Y }; } }
        { var a = theme.CellPadding; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("CellPadding"), ref v, .1f)) theme.CellPadding = new[] { v.X, v.Y }; } }
        { var a = theme.TouchExtraPadding; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("TouchExtraPadding"), ref v, .1f)) theme.TouchExtraPadding = new[] { v.X, v.Y }; } }
        { var a = theme.ButtonTextAlign; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("ButtonTextAlign"), ref v, .1f)) theme.ButtonTextAlign = new[] { v.X, v.Y }; } }
        { var a = theme.SelectableTextAlign; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("SelectableTextAlign"), ref v, .1f)) theme.SelectableTextAlign = new[] { v.X, v.Y }; } }
        { var a = theme.SeparatorTextAlign; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("SeparatorTextAlign"), ref v, .1f)) theme.SeparatorTextAlign = new[] { v.X, v.Y }; } }
        { var a = theme.SeparatorTextPadding; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("SeparatorTextPadding"), ref v, .1f)) theme.SeparatorTextPadding = new[] { v.X, v.Y }; } }
        { var a = theme.DisplayWindowPadding; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("DisplayWindowPadding"), ref v, .1f)) theme.DisplayWindowPadding = new[] { v.X, v.Y }; } }
        { var a = theme.DisplaySafeAreaPadding; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("DisplaySafeAreaPadding"), ref v, .1f)) theme.DisplaySafeAreaPadding = new[] { v.X, v.Y }; } }
        { var a = theme.TableAngledHeadersTextAlign; if (a?.Length == 2) { var v = new float2(a[0], a[1]); if (ImGui.DragFloat2(MeowSci.KsaAbstractions.FormField.Label("TableAngledHeadersTextAlign"), ref v, .1f)) theme.TableAngledHeadersTextAlign = new[] { v.X, v.Y }; } }
        { bool v = theme.AntiAliasedLines; if (ImGui.Checkbox(FormField.Label("AntiAliasedLines"), ref v)) theme.AntiAliasedLines = v; }
        { bool v = theme.AntiAliasedLinesUseTex; if (ImGui.Checkbox(FormField.Label("AntiAliasedLinesUseTex"), ref v)) theme.AntiAliasedLinesUseTex = v; }
        { bool v = theme.AntiAliasedFill; if (ImGui.Checkbox(FormField.Label("AntiAliasedFill"), ref v)) theme.AntiAliasedFill = v; }
        }
        if (WorkspaceUi.Header("Style colors"))
        {
            using var grid = new FormGrid("theme-colors");
            for (int i = 0; i < theme.Colors.Length; i++)
            { var a = theme.Colors[i]; if (a?.Length != 4) continue; var color = new float4(a[0], a[1], a[2], a[3]);
              if (ImGui.ColorEdit4(MeowSci.KsaAbstractions.FormField.Label(((ImGuiCol)i).ToString()), ref color)) theme.Colors[i] = new[] { color.X, color.Y, color.Z, color.W }; }
        }
    }
}
