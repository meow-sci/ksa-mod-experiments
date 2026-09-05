using System;
using MeowSci.Unscience.Contracts;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.SkittlesLib;

public class ThemeDefinition
{
    // Metadata
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    // Colors: 60 entries, each [R, G, B, A] float[4], indexed by (int)ImGuiCol
    public float[][] Colors { get; set; } = new float[60][];

    // Style floats
    public float Alpha { get; set; } = 1.0f;
    public float DisabledAlpha { get; set; } = 0.6f;
    public float WindowRounding { get; set; } = 0.0f;
    public float WindowBorderSize { get; set; } = 1.0f;
    public float WindowBorderHoverPadding { get; set; } = 4.0f;
    public float ChildRounding { get; set; } = 0.0f;
    public float ChildBorderSize { get; set; } = 1.0f;
    public float PopupRounding { get; set; } = 0.0f;
    public float PopupBorderSize { get; set; } = 1.0f;
    public float FrameRounding { get; set; } = 0.0f;
    public float FrameBorderSize { get; set; } = 0.0f;
    public float IndentSpacing { get; set; } = 21.0f;
    public float ColumnsMinSpacing { get; set; } = 6.0f;
    public float ScrollbarSize { get; set; } = 14.0f;
    public float ScrollbarRounding { get; set; } = 9.0f;
    public float GrabMinSize { get; set; } = 12.0f;
    public float GrabRounding { get; set; } = 0.0f;
    public float LogSliderDeadzone { get; set; } = 4.0f;
    public float ImageBorderSize { get; set; } = 0.0f;
    public float TabRounding { get; set; } = 4.0f;
    public float TabBorderSize { get; set; } = 0.0f;
    public float TabMinWidthBase { get; set; } = 0.0f;
    public float TabMinWidthShrink { get; set; } = 0.0f;
    public float TabCloseButtonMinWidthSelected { get; set; } = 0.0f;
    public float TabCloseButtonMinWidthUnselected { get; set; } = 0.0f;
    public float TabBarBorderSize { get; set; } = 1.0f;
    public float TabBarOverlineSize { get; set; } = 2.0f;
    public float TableAngledHeadersAngle { get; set; } = 0.6108652f; // 35 degrees in radians
    public float TreeLinesSize { get; set; } = 1.0f;
    public float TreeLinesRounding { get; set; } = 0.0f;
    public float SeparatorTextBorderSize { get; set; } = 3.0f;
    public float DockingSeparatorSize { get; set; } = 2.0f;
    public float MouseCursorScale { get; set; } = 1.0f;
    public float CurveTessellationTol { get; set; } = 1.25f;
    public float CircleTessellationMaxError { get; set; } = 0.3f;

    // Float2 values — stored as float[2]
    public float[] WindowPadding { get; set; } = new float[] { 8f, 8f };
    public float[] WindowMinSize { get; set; } = new float[] { 32f, 32f };
    public float[] WindowTitleAlign { get; set; } = new float[] { 0f, 0.5f };
    public float[] FramePadding { get; set; } = new float[] { 4f, 3f };
    public float[] ItemSpacing { get; set; } = new float[] { 8f, 4f };
    public float[] ItemInnerSpacing { get; set; } = new float[] { 4f, 4f };
    public float[] CellPadding { get; set; } = new float[] { 4f, 2f };
    public float[] TouchExtraPadding { get; set; } = new float[] { 0f, 0f };
    public float[] ButtonTextAlign { get; set; } = new float[] { 0.5f, 0.5f };
    public float[] SelectableTextAlign { get; set; } = new float[] { 0f, 0f };
    public float[] SeparatorTextAlign { get; set; } = new float[] { 0f, 0.5f };
    public float[] SeparatorTextPadding { get; set; } = new float[] { 20f, 3f };
    public float[] DisplayWindowPadding { get; set; } = new float[] { 19f, 19f };
    public float[] DisplaySafeAreaPadding { get; set; } = new float[] { 3f, 3f };
    public float[] TableAngledHeadersTextAlign { get; set; } = new float[] { 0.5f, 0f };

    // Bool values
    public bool AntiAliasedLines { get; set; } = true;
    public bool AntiAliasedLinesUseTex { get; set; } = true;
    public bool AntiAliasedFill { get; set; } = true;

    public void Validate()
    {
        if (Colors == null || Colors.Length > (int)ImGuiCol.COUNT) throw new InvalidOperationException("Invalid theme color count.");
        foreach (var color in Colors)
            if (color != null) { if (color.Length != 4) throw new InvalidOperationException("Invalid theme color."); foreach (float c in color) DraftValueValidation.Range(c, 0, 1, "Color"); }
        DraftValueValidation.Range(Alpha, 0, 1, nameof(Alpha));
        DraftValueValidation.Range(DisabledAlpha, 0, 1, nameof(DisabledAlpha));
        DraftValueValidation.Range(WindowRounding, 0, float.MaxValue, nameof(WindowRounding));
        DraftValueValidation.Range(WindowBorderSize, 0, float.MaxValue, nameof(WindowBorderSize));
        DraftValueValidation.Range(WindowBorderHoverPadding, 0, float.MaxValue, nameof(WindowBorderHoverPadding));
        DraftValueValidation.Range(ChildRounding, 0, float.MaxValue, nameof(ChildRounding));
        DraftValueValidation.Range(ChildBorderSize, 0, float.MaxValue, nameof(ChildBorderSize));
        DraftValueValidation.Range(PopupRounding, 0, float.MaxValue, nameof(PopupRounding));
        DraftValueValidation.Range(PopupBorderSize, 0, float.MaxValue, nameof(PopupBorderSize));
        DraftValueValidation.Range(FrameRounding, 0, float.MaxValue, nameof(FrameRounding));
        DraftValueValidation.Range(FrameBorderSize, 0, float.MaxValue, nameof(FrameBorderSize));
        DraftValueValidation.Range(IndentSpacing, 0, float.MaxValue, nameof(IndentSpacing));
        DraftValueValidation.Range(ColumnsMinSpacing, 0, float.MaxValue, nameof(ColumnsMinSpacing));
        DraftValueValidation.Range(ScrollbarSize, 0, float.MaxValue, nameof(ScrollbarSize));
        DraftValueValidation.Range(ScrollbarRounding, 0, float.MaxValue, nameof(ScrollbarRounding));
        DraftValueValidation.Range(GrabMinSize, 0, float.MaxValue, nameof(GrabMinSize));
        DraftValueValidation.Range(GrabRounding, 0, float.MaxValue, nameof(GrabRounding));
        DraftValueValidation.Range(LogSliderDeadzone, 0, float.MaxValue, nameof(LogSliderDeadzone));
        DraftValueValidation.Range(ImageBorderSize, 0, float.MaxValue, nameof(ImageBorderSize));
        DraftValueValidation.Range(TabRounding, 0, float.MaxValue, nameof(TabRounding));
        DraftValueValidation.Range(TabBorderSize, 0, float.MaxValue, nameof(TabBorderSize));
        DraftValueValidation.Range(TabMinWidthBase, 0, float.MaxValue, nameof(TabMinWidthBase));
        DraftValueValidation.Range(TabMinWidthShrink, 0, float.MaxValue, nameof(TabMinWidthShrink));
        DraftValueValidation.Range(TabCloseButtonMinWidthSelected, 0, float.MaxValue, nameof(TabCloseButtonMinWidthSelected));
        DraftValueValidation.Range(TabCloseButtonMinWidthUnselected, 0, float.MaxValue, nameof(TabCloseButtonMinWidthUnselected));
        DraftValueValidation.Range(TabBarBorderSize, 0, float.MaxValue, nameof(TabBarBorderSize));
        DraftValueValidation.Range(TabBarOverlineSize, 0, float.MaxValue, nameof(TabBarOverlineSize));
        DraftValueValidation.Range(TableAngledHeadersAngle, -1.56, 1.56, nameof(TableAngledHeadersAngle));
        DraftValueValidation.Range(TreeLinesSize, 0, float.MaxValue, nameof(TreeLinesSize));
        DraftValueValidation.Range(TreeLinesRounding, 0, float.MaxValue, nameof(TreeLinesRounding));
        DraftValueValidation.Range(SeparatorTextBorderSize, 0, float.MaxValue, nameof(SeparatorTextBorderSize));
        DraftValueValidation.Range(DockingSeparatorSize, 0, float.MaxValue, nameof(DockingSeparatorSize));
        DraftValueValidation.Range(MouseCursorScale, 0.0001, float.MaxValue, nameof(MouseCursorScale));
        DraftValueValidation.Range(CurveTessellationTol, 0.0001, float.MaxValue, nameof(CurveTessellationTol));
        DraftValueValidation.Range(CircleTessellationMaxError, 0.0001, float.MaxValue, nameof(CircleTessellationMaxError));
        if (WindowPadding == null || WindowPadding.Length != 2) throw new InvalidOperationException("Invalid WindowPadding.");
        if (WindowMinSize == null || WindowMinSize.Length != 2) throw new InvalidOperationException("Invalid WindowMinSize.");
        if (WindowTitleAlign == null || WindowTitleAlign.Length != 2) throw new InvalidOperationException("Invalid WindowTitleAlign.");
        if (FramePadding == null || FramePadding.Length != 2) throw new InvalidOperationException("Invalid FramePadding.");
        if (ItemSpacing == null || ItemSpacing.Length != 2) throw new InvalidOperationException("Invalid ItemSpacing.");
        if (ItemInnerSpacing == null || ItemInnerSpacing.Length != 2) throw new InvalidOperationException("Invalid ItemInnerSpacing.");
        if (CellPadding == null || CellPadding.Length != 2) throw new InvalidOperationException("Invalid CellPadding.");
        if (TouchExtraPadding == null || TouchExtraPadding.Length != 2) throw new InvalidOperationException("Invalid TouchExtraPadding.");
        if (ButtonTextAlign == null || ButtonTextAlign.Length != 2) throw new InvalidOperationException("Invalid ButtonTextAlign.");
        if (SelectableTextAlign == null || SelectableTextAlign.Length != 2) throw new InvalidOperationException("Invalid SelectableTextAlign.");
        if (SeparatorTextAlign == null || SeparatorTextAlign.Length != 2) throw new InvalidOperationException("Invalid SeparatorTextAlign.");
        if (SeparatorTextPadding == null || SeparatorTextPadding.Length != 2) throw new InvalidOperationException("Invalid SeparatorTextPadding.");
        if (DisplayWindowPadding == null || DisplayWindowPadding.Length != 2) throw new InvalidOperationException("Invalid DisplayWindowPadding.");
        if (DisplaySafeAreaPadding == null || DisplaySafeAreaPadding.Length != 2) throw new InvalidOperationException("Invalid DisplaySafeAreaPadding.");
        if (TableAngledHeadersTextAlign == null || TableAngledHeadersTextAlign.Length != 2) throw new InvalidOperationException("Invalid TableAngledHeadersTextAlign.");
    }

    public ThemeDefinition()
    {
        Colors = new float[60][];
        for (int i = 0; i < 60; i++)
            Colors[i] = new float[] { 0f, 0f, 0f, 1f };
    }

    public static ThemeDefinition CaptureFromImGui()
    {
        var theme = new ThemeDefinition();
        ImGuiStylePtr style = ImGui.GetStyle();

        // Colors
        for (int i = 0; i < 60; i++)
        {
            float4 c = style.Colors[i];
            theme.Colors[i] = new float[] { c.X, c.Y, c.Z, c.W };
        }

        // Float vars
        theme.Alpha = style.Alpha;
        theme.DisabledAlpha = style.DisabledAlpha;
        theme.WindowRounding = style.WindowRounding;
        theme.WindowBorderSize = style.WindowBorderSize;
        theme.WindowBorderHoverPadding = style.WindowBorderHoverPadding;
        theme.ChildRounding = style.ChildRounding;
        theme.ChildBorderSize = style.ChildBorderSize;
        theme.PopupRounding = style.PopupRounding;
        theme.PopupBorderSize = style.PopupBorderSize;
        theme.FrameRounding = style.FrameRounding;
        theme.FrameBorderSize = style.FrameBorderSize;
        theme.IndentSpacing = style.IndentSpacing;
        theme.ColumnsMinSpacing = style.ColumnsMinSpacing;
        theme.ScrollbarSize = style.ScrollbarSize;
        theme.ScrollbarRounding = style.ScrollbarRounding;
        theme.GrabMinSize = style.GrabMinSize;
        theme.GrabRounding = style.GrabRounding;
        theme.LogSliderDeadzone = style.LogSliderDeadzone;
        theme.ImageBorderSize = style.ImageBorderSize;
        theme.TabRounding = style.TabRounding;
        theme.TabBorderSize = style.TabBorderSize;
        theme.TabMinWidthBase = style.TabMinWidthBase;
        theme.TabMinWidthShrink = style.TabMinWidthShrink;
        theme.TabCloseButtonMinWidthSelected = style.TabCloseButtonMinWidthSelected;
        theme.TabCloseButtonMinWidthUnselected = style.TabCloseButtonMinWidthUnselected;
        theme.TabBarBorderSize = style.TabBarBorderSize;
        theme.TabBarOverlineSize = style.TabBarOverlineSize;
        theme.TableAngledHeadersAngle = style.TableAngledHeadersAngle;
        theme.TreeLinesSize = style.TreeLinesSize;
        theme.TreeLinesRounding = style.TreeLinesRounding;
        theme.SeparatorTextBorderSize = style.SeparatorTextBorderSize;
        theme.DockingSeparatorSize = style.DockingSeparatorSize;
        theme.MouseCursorScale = style.MouseCursorScale;
        theme.CurveTessellationTol = style.CurveTessellationTol;
        theme.CircleTessellationMaxError = style.CircleTessellationMaxError;

        // Float2 vars
        float2 wp = style.WindowPadding;
        theme.WindowPadding = new float[] { wp.X, wp.Y };
        float2 wms = style.WindowMinSize;
        theme.WindowMinSize = new float[] { wms.X, wms.Y };
        float2 wta = style.WindowTitleAlign;
        theme.WindowTitleAlign = new float[] { wta.X, wta.Y };
        float2 fp = style.FramePadding;
        theme.FramePadding = new float[] { fp.X, fp.Y };
        float2 isp = style.ItemSpacing;
        theme.ItemSpacing = new float[] { isp.X, isp.Y };
        float2 iis = style.ItemInnerSpacing;
        theme.ItemInnerSpacing = new float[] { iis.X, iis.Y };
        float2 cp = style.CellPadding;
        theme.CellPadding = new float[] { cp.X, cp.Y };
        float2 tep = style.TouchExtraPadding;
        theme.TouchExtraPadding = new float[] { tep.X, tep.Y };
        float2 bta = style.ButtonTextAlign;
        theme.ButtonTextAlign = new float[] { bta.X, bta.Y };
        float2 sta = style.SelectableTextAlign;
        theme.SelectableTextAlign = new float[] { sta.X, sta.Y };
        float2 septa = style.SeparatorTextAlign;
        theme.SeparatorTextAlign = new float[] { septa.X, septa.Y };
        float2 septp = style.SeparatorTextPadding;
        theme.SeparatorTextPadding = new float[] { septp.X, septp.Y };
        float2 dwp = style.DisplayWindowPadding;
        theme.DisplayWindowPadding = new float[] { dwp.X, dwp.Y };
        float2 dsap = style.DisplaySafeAreaPadding;
        theme.DisplaySafeAreaPadding = new float[] { dsap.X, dsap.Y };
        float2 taha = style.TableAngledHeadersTextAlign;
        theme.TableAngledHeadersTextAlign = new float[] { taha.X, taha.Y };

        // Bool vars
        theme.AntiAliasedLines = style.AntiAliasedLines;
        theme.AntiAliasedLinesUseTex = style.AntiAliasedLinesUseTex;
        theme.AntiAliasedFill = style.AntiAliasedFill;

        return theme;
    }

    public void ApplyToImGui()
    {
        ImGuiStylePtr style = ImGui.GetStyle();

        // Colors
        for (int i = 0; i < 60; i++)
        {
            float[] c = Colors[i];
            style.Colors[i] = new float4(c[0], c[1], c[2], c[3]);
        }

        // Float vars
        style.Alpha = Alpha;
        style.DisabledAlpha = DisabledAlpha;
        style.WindowRounding = WindowRounding;
        style.WindowBorderSize = WindowBorderSize;
        style.WindowBorderHoverPadding = WindowBorderHoverPadding;
        style.ChildRounding = ChildRounding;
        style.ChildBorderSize = ChildBorderSize;
        style.PopupRounding = PopupRounding;
        style.PopupBorderSize = PopupBorderSize;
        style.FrameRounding = FrameRounding;
        style.FrameBorderSize = FrameBorderSize;
        style.IndentSpacing = IndentSpacing;
        style.ColumnsMinSpacing = ColumnsMinSpacing;
        style.ScrollbarSize = ScrollbarSize;
        style.ScrollbarRounding = ScrollbarRounding;
        style.GrabMinSize = GrabMinSize;
        style.GrabRounding = GrabRounding;
        style.LogSliderDeadzone = LogSliderDeadzone;
        style.ImageBorderSize = ImageBorderSize;
        style.TabRounding = TabRounding;
        style.TabBorderSize = TabBorderSize;
        style.TabMinWidthBase = TabMinWidthBase;
        style.TabMinWidthShrink = TabMinWidthShrink;
        style.TabCloseButtonMinWidthSelected = TabCloseButtonMinWidthSelected;
        style.TabCloseButtonMinWidthUnselected = TabCloseButtonMinWidthUnselected;
        style.TabBarBorderSize = TabBarBorderSize;
        style.TabBarOverlineSize = TabBarOverlineSize;
        style.TableAngledHeadersAngle = TableAngledHeadersAngle;
        style.TreeLinesSize = TreeLinesSize;
        style.TreeLinesRounding = TreeLinesRounding;
        style.SeparatorTextBorderSize = SeparatorTextBorderSize;
        style.DockingSeparatorSize = DockingSeparatorSize;
        style.MouseCursorScale = MouseCursorScale;
        style.CurveTessellationTol = CurveTessellationTol;
        style.CircleTessellationMaxError = CircleTessellationMaxError;

        // Float2 vars
        style.WindowPadding = new float2(WindowPadding[0], WindowPadding[1]);
        style.WindowMinSize = new float2(WindowMinSize[0], WindowMinSize[1]);
        style.WindowTitleAlign = new float2(WindowTitleAlign[0], WindowTitleAlign[1]);
        style.FramePadding = new float2(FramePadding[0], FramePadding[1]);
        style.ItemSpacing = new float2(ItemSpacing[0], ItemSpacing[1]);
        style.ItemInnerSpacing = new float2(ItemInnerSpacing[0], ItemInnerSpacing[1]);
        style.CellPadding = new float2(CellPadding[0], CellPadding[1]);
        style.TouchExtraPadding = new float2(TouchExtraPadding[0], TouchExtraPadding[1]);
        style.ButtonTextAlign = new float2(ButtonTextAlign[0], ButtonTextAlign[1]);
        style.SelectableTextAlign = new float2(SelectableTextAlign[0], SelectableTextAlign[1]);
        style.SeparatorTextAlign = new float2(SeparatorTextAlign[0], SeparatorTextAlign[1]);
        style.SeparatorTextPadding = new float2(SeparatorTextPadding[0], SeparatorTextPadding[1]);
        style.DisplayWindowPadding = new float2(DisplayWindowPadding[0], DisplayWindowPadding[1]);
        style.DisplaySafeAreaPadding = new float2(DisplaySafeAreaPadding[0], DisplaySafeAreaPadding[1]);
        style.TableAngledHeadersTextAlign = new float2(TableAngledHeadersTextAlign[0], TableAngledHeadersTextAlign[1]);

        // Bool vars
        style.AntiAliasedLines = AntiAliasedLines;
        style.AntiAliasedLinesUseTex = AntiAliasedLinesUseTex;
        style.AntiAliasedFill = AntiAliasedFill;
    }
}
