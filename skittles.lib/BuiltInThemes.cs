using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.SkittlesLib;

public static class BuiltInThemes
{
    public static ThemeDefinition InanimateCarbonRod()
    {
        var theme = new ThemeDefinition
        {
            Name = "Inanimate Carbon Rod",
            Description = "Radioactive terminal — dark backgrounds with radioactive green accents",
            Alpha = 1.0f,
            DisabledAlpha = 0.6f,
            WindowRounding = 4.0f,
            WindowBorderSize = 1.0f,
            WindowBorderHoverPadding = 4.0f,
            ChildRounding = 0.0f,
            ChildBorderSize = 1.0f,
            PopupRounding = 2.0f,
            PopupBorderSize = 1.0f,
            FrameRounding = 2.0f,
            FrameBorderSize = 1.0f,
            IndentSpacing = 21.0f,
            ColumnsMinSpacing = 6.0f,
            ScrollbarSize = 14.0f,
            ScrollbarRounding = 9.0f,
            GrabMinSize = 12.0f,
            GrabRounding = 2.0f,
            LogSliderDeadzone = 4.0f,
            ImageBorderSize = 0.0f,
            TabRounding = 4.0f,
            TabBorderSize = 0.0f,
            TabBarBorderSize = 1.0f,
            TabBarOverlineSize = 2.0f,
            TableAngledHeadersAngle = 0.6108652f,
            TreeLinesSize = 1.0f,
            TreeLinesRounding = 0.0f,
            SeparatorTextBorderSize = 3.0f,
            DockingSeparatorSize = 2.0f,
            MouseCursorScale = 1.0f,
            CurveTessellationTol = 1.25f,
            CircleTessellationMaxError = 0.3f,
            WindowPadding = new float[] { 8f, 8f },
            WindowMinSize = new float[] { 32f, 32f },
            WindowTitleAlign = new float[] { 0f, 0.5f },
            FramePadding = new float[] { 4f, 3f },
            ItemSpacing = new float[] { 8f, 4f },
            ItemInnerSpacing = new float[] { 4f, 4f },
            CellPadding = new float[] { 4f, 2f },
            TouchExtraPadding = new float[] { 0f, 0f },
            ButtonTextAlign = new float[] { 0.5f, 0.5f },
            SelectableTextAlign = new float[] { 0f, 0f },
            SeparatorTextAlign = new float[] { 0f, 0.5f },
            SeparatorTextPadding = new float[] { 20f, 3f },
            DisplayWindowPadding = new float[] { 19f, 19f },
            DisplaySafeAreaPadding = new float[] { 3f, 3f },
            TableAngledHeadersTextAlign = new float[] { 0.5f, 0f },
            AntiAliasedLines = true,
            AntiAliasedLinesUseTex = true,
            AntiAliasedFill = true,
        };

        // RadioactiveGreen = rgba(0.17, 0.98, 0.12, 1.0) — XKCD #2CFA1F
        // Colors — all 60 in order (index matches ImGuiCol enum):
        // 0=Text, 1=TextDisabled, 2=WindowBg, 3=ChildBg, 4=PopupBg,
        // 5=Border, 6=BorderShadow, 7=FrameBg, 8=FrameBgHovered, 9=FrameBgActive,
        // 10=TitleBg, 11=TitleBgActive, 12=TitleBgCollapsed, 13=MenuBarBg,
        // 14=ScrollbarBg, 15=ScrollbarGrab, 16=ScrollbarGrabHovered, 17=ScrollbarGrabActive,
        // 18=CheckMark, 19=SliderGrab, 20=SliderGrabActive,
        // 21=Button, 22=ButtonHovered, 23=ButtonActive,
        // 24=Header, 25=HeaderHovered, 26=HeaderActive,
        // 27=Separator, 28=SeparatorHovered, 29=SeparatorActive,
        // 30=ResizeGrip, 31=ResizeGripHovered, 32=ResizeGripActive,
        // 33=InputTextCursor, 34=TabHovered, 35=Tab, 36=TabSelected, 37=TabSelectedOverline,
        // 38=TabDimmed, 39=TabDimmedSelected, 40=TabDimmedSelectedOverline,
        // 41=DockingPreview, 42=DockingEmptyBg,
        // 43=PlotLines, 44=PlotLinesHovered, 45=PlotHistogram, 46=PlotHistogramHovered,
        // 47=TableHeaderBg, 48=TableBorderStrong, 49=TableBorderLight,
        // 50=TableRowBg, 51=TableRowBgAlt,
        // 52=TextLink, 53=TextSelectedBg, 54=TreeLines,
        // 55=DragDropTarget, 56=NavCursor, 57=NavWindowingHighlight, 58=NavWindowingDimBg,
        // 59=ModalWindowDimBg

        theme.Colors[0]  = new float[] { 0.17f, 0.98f, 0.12f, 1.00f }; // Text - RadioactiveGreen
        theme.Colors[1]  = new float[] { 0.07f, 0.39f, 0.05f, 1.00f }; // TextDisabled - dimmed green
        theme.Colors[2]  = new float[] { 0.02f, 0.05f, 0.02f, 0.96f }; // WindowBg - near-black/green tint
        theme.Colors[3]  = new float[] { 0.00f, 0.02f, 0.00f, 0.00f }; // ChildBg - transparent
        theme.Colors[4]  = new float[] { 0.02f, 0.06f, 0.02f, 0.94f }; // PopupBg - very dark green
        theme.Colors[5]  = new float[] { 0.17f, 0.98f, 0.12f, 0.50f }; // Border - green at half alpha
        theme.Colors[6]  = new float[] { 0.00f, 0.00f, 0.00f, 0.00f }; // BorderShadow - transparent
        theme.Colors[7]  = new float[] { 0.05f, 0.14f, 0.05f, 0.54f }; // FrameBg - dark green
        theme.Colors[8]  = new float[] { 0.10f, 0.60f, 0.08f, 0.40f }; // FrameBgHovered
        theme.Colors[9]  = new float[] { 0.12f, 0.80f, 0.10f, 0.67f }; // FrameBgActive
        theme.Colors[10] = new float[] { 0.02f, 0.07f, 0.02f, 1.00f }; // TitleBg - very dark green
        theme.Colors[11] = new float[] { 0.06f, 0.30f, 0.05f, 1.00f }; // TitleBgActive - dark green
        theme.Colors[12] = new float[] { 0.00f, 0.05f, 0.00f, 0.51f }; // TitleBgCollapsed
        theme.Colors[13] = new float[] { 0.02f, 0.07f, 0.02f, 1.00f }; // MenuBarBg
        theme.Colors[14] = new float[] { 0.01f, 0.02f, 0.01f, 0.53f }; // ScrollbarBg
        theme.Colors[15] = new float[] { 0.10f, 0.55f, 0.08f, 1.00f }; // ScrollbarGrab
        theme.Colors[16] = new float[] { 0.13f, 0.70f, 0.10f, 1.00f }; // ScrollbarGrabHovered
        theme.Colors[17] = new float[] { 0.17f, 0.98f, 0.12f, 1.00f }; // ScrollbarGrabActive - full green
        theme.Colors[18] = new float[] { 0.17f, 0.98f, 0.12f, 1.00f }; // CheckMark - RadioactiveGreen
        theme.Colors[19] = new float[] { 0.14f, 0.80f, 0.10f, 1.00f }; // SliderGrab
        theme.Colors[20] = new float[] { 0.17f, 0.98f, 0.12f, 1.00f }; // SliderGrabActive - full green
        theme.Colors[21] = new float[] { 0.10f, 0.55f, 0.08f, 0.40f }; // Button
        theme.Colors[22] = new float[] { 0.17f, 0.98f, 0.12f, 0.80f }; // ButtonHovered
        theme.Colors[23] = new float[] { 0.14f, 0.85f, 0.10f, 1.00f }; // ButtonActive
        theme.Colors[24] = new float[] { 0.17f, 0.98f, 0.12f, 0.31f }; // Header
        theme.Colors[25] = new float[] { 0.17f, 0.98f, 0.12f, 0.60f }; // HeaderHovered
        theme.Colors[26] = new float[] { 0.17f, 0.98f, 0.12f, 1.00f }; // HeaderActive
        theme.Colors[27] = new float[] { 0.17f, 0.98f, 0.12f, 0.50f }; // Separator
        theme.Colors[28] = new float[] { 0.14f, 0.85f, 0.10f, 0.78f }; // SeparatorHovered
        theme.Colors[29] = new float[] { 0.17f, 0.98f, 0.12f, 1.00f }; // SeparatorActive
        theme.Colors[30] = new float[] { 0.17f, 0.98f, 0.12f, 0.20f }; // ResizeGrip
        theme.Colors[31] = new float[] { 0.17f, 0.98f, 0.12f, 0.67f }; // ResizeGripHovered
        theme.Colors[32] = new float[] { 0.17f, 0.98f, 0.12f, 0.95f }; // ResizeGripActive
        theme.Colors[33] = new float[] { 0.17f, 0.98f, 0.12f, 1.00f }; // InputTextCursor
        theme.Colors[34] = new float[] { 0.17f, 0.98f, 0.12f, 0.80f }; // TabHovered
        theme.Colors[35] = new float[] { 0.04f, 0.20f, 0.03f, 0.86f }; // Tab - dark with green tint
        theme.Colors[36] = new float[] { 0.08f, 0.40f, 0.06f, 1.00f }; // TabSelected - darker green
        theme.Colors[37] = new float[] { 0.17f, 0.98f, 0.12f, 1.00f }; // TabSelectedOverline
        theme.Colors[38] = new float[] { 0.02f, 0.06f, 0.02f, 0.97f }; // TabDimmed
        theme.Colors[39] = new float[] { 0.05f, 0.18f, 0.04f, 1.00f }; // TabDimmedSelected
        theme.Colors[40] = new float[] { 0.10f, 0.55f, 0.08f, 1.00f }; // TabDimmedSelectedOverline
        theme.Colors[41] = new float[] { 0.17f, 0.98f, 0.12f, 0.70f }; // DockingPreview
        theme.Colors[42] = new float[] { 0.05f, 0.10f, 0.05f, 1.00f }; // DockingEmptyBg
        theme.Colors[43] = new float[] { 0.17f, 0.98f, 0.12f, 1.00f }; // PlotLines
        theme.Colors[44] = new float[] { 0.30f, 1.00f, 0.10f, 1.00f }; // PlotLinesHovered - bright green
        theme.Colors[45] = new float[] { 0.17f, 0.98f, 0.12f, 1.00f }; // PlotHistogram
        theme.Colors[46] = new float[] { 0.40f, 1.00f, 0.10f, 1.00f }; // PlotHistogramHovered
        theme.Colors[47] = new float[] { 0.04f, 0.15f, 0.04f, 1.00f }; // TableHeaderBg
        theme.Colors[48] = new float[] { 0.17f, 0.98f, 0.12f, 0.50f }; // TableBorderStrong
        theme.Colors[49] = new float[] { 0.10f, 0.55f, 0.08f, 0.30f }; // TableBorderLight
        theme.Colors[50] = new float[] { 0.00f, 0.00f, 0.00f, 0.00f }; // TableRowBg
        theme.Colors[51] = new float[] { 0.17f, 0.98f, 0.12f, 0.06f }; // TableRowBgAlt
        theme.Colors[52] = new float[] { 0.17f, 0.98f, 0.12f, 1.00f }; // TextLink
        theme.Colors[53] = new float[] { 0.17f, 0.98f, 0.12f, 0.35f }; // TextSelectedBg
        theme.Colors[54] = new float[] { 0.17f, 0.98f, 0.12f, 0.50f }; // TreeLines
        theme.Colors[55] = new float[] { 0.50f, 1.00f, 0.00f, 0.90f }; // DragDropTarget - yellow-green
        theme.Colors[56] = new float[] { 0.17f, 0.98f, 0.12f, 1.00f }; // NavCursor
        theme.Colors[57] = new float[] { 0.80f, 1.00f, 0.80f, 0.70f }; // NavWindowingHighlight - pale green
        theme.Colors[58] = new float[] { 0.02f, 0.10f, 0.02f, 0.20f }; // NavWindowingDimBg
        theme.Colors[59] = new float[] { 0.02f, 0.10f, 0.02f, 0.35f }; // ModalWindowDimBg

        return theme;
    }
}
