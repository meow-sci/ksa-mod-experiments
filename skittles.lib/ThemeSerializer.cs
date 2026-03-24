using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Tomlyn;
using Tomlyn.Model;

namespace MeowSci.SkittlesLib;

public static class ThemeSerializer
{
    internal static readonly string[] ColorNames = {
        "Text", "TextDisabled", "WindowBg", "ChildBg", "PopupBg",
        "Border", "BorderShadow", "FrameBg", "FrameBgHovered", "FrameBgActive",
        "TitleBg", "TitleBgActive", "TitleBgCollapsed", "MenuBarBg",
        "ScrollbarBg", "ScrollbarGrab", "ScrollbarGrabHovered", "ScrollbarGrabActive",
        "CheckMark", "SliderGrab", "SliderGrabActive",
        "Button", "ButtonHovered", "ButtonActive",
        "Header", "HeaderHovered", "HeaderActive",
        "Separator", "SeparatorHovered", "SeparatorActive",
        "ResizeGrip", "ResizeGripHovered", "ResizeGripActive",
        "InputTextCursor", "TabHovered", "Tab", "TabSelected", "TabSelectedOverline",
        "TabDimmed", "TabDimmedSelected", "TabDimmedSelectedOverline",
        "DockingPreview", "DockingEmptyBg",
        "PlotLines", "PlotLinesHovered", "PlotHistogram", "PlotHistogramHovered",
        "TableHeaderBg", "TableBorderStrong", "TableBorderLight",
        "TableRowBg", "TableRowBgAlt",
        "TextLink", "TextSelectedBg", "TreeLines",
        "DragDropTarget", "NavCursor", "NavWindowingHighlight", "NavWindowingDimBg",
        "ModalWindowDimBg"
    };

    private static readonly Dictionary<string, int> ColorNameToIndex = BuildColorIndex();

    private static Dictionary<string, int> BuildColorIndex()
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < ColorNames.Length; i++)
            d[ColorNames[i]] = i;
        return d;
    }

    public static string Serialize(ThemeDefinition theme)
    {
        var root = new TomlTable();

        // [meta]
        var meta = new TomlTable();
        meta["name"] = theme.Name;
        meta["description"] = theme.Description;
        root["meta"] = meta;

        // [colors]
        var colors = new TomlTable();
        for (int i = 0; i < 60; i++)
        {
            float[] c = theme.Colors[i];
            var arr = new TomlArray();
            arr.Add(Math.Round(c[0], 2));
            arr.Add(Math.Round(c[1], 2));
            arr.Add(Math.Round(c[2], 2));
            arr.Add(Math.Round(c[3], 2));
            colors[ColorNames[i]] = arr;
        }
        root["colors"] = colors;

        // [style]
        var style = new TomlTable();

        // float vars
        style["Alpha"] = (double)theme.Alpha;
        style["DisabledAlpha"] = (double)theme.DisabledAlpha;
        style["WindowRounding"] = (double)theme.WindowRounding;
        style["WindowBorderSize"] = (double)theme.WindowBorderSize;
        style["WindowBorderHoverPadding"] = (double)theme.WindowBorderHoverPadding;
        style["ChildRounding"] = (double)theme.ChildRounding;
        style["ChildBorderSize"] = (double)theme.ChildBorderSize;
        style["PopupRounding"] = (double)theme.PopupRounding;
        style["PopupBorderSize"] = (double)theme.PopupBorderSize;
        style["FrameRounding"] = (double)theme.FrameRounding;
        style["FrameBorderSize"] = (double)theme.FrameBorderSize;
        style["IndentSpacing"] = (double)theme.IndentSpacing;
        style["ColumnsMinSpacing"] = (double)theme.ColumnsMinSpacing;
        style["ScrollbarSize"] = (double)theme.ScrollbarSize;
        style["ScrollbarRounding"] = (double)theme.ScrollbarRounding;
        style["GrabMinSize"] = (double)theme.GrabMinSize;
        style["GrabRounding"] = (double)theme.GrabRounding;
        style["LogSliderDeadzone"] = (double)theme.LogSliderDeadzone;
        style["ImageBorderSize"] = (double)theme.ImageBorderSize;
        style["TabRounding"] = (double)theme.TabRounding;
        style["TabBorderSize"] = (double)theme.TabBorderSize;
        style["TabMinWidthBase"] = (double)theme.TabMinWidthBase;
        style["TabMinWidthShrink"] = (double)theme.TabMinWidthShrink;
        style["TabCloseButtonMinWidthSelected"] = (double)theme.TabCloseButtonMinWidthSelected;
        style["TabCloseButtonMinWidthUnselected"] = (double)theme.TabCloseButtonMinWidthUnselected;
        style["TabBarBorderSize"] = (double)theme.TabBarBorderSize;
        style["TabBarOverlineSize"] = (double)theme.TabBarOverlineSize;
        style["TableAngledHeadersAngle"] = (double)theme.TableAngledHeadersAngle;
        style["TreeLinesSize"] = (double)theme.TreeLinesSize;
        style["TreeLinesRounding"] = (double)theme.TreeLinesRounding;
        style["SeparatorTextBorderSize"] = (double)theme.SeparatorTextBorderSize;
        style["DockingSeparatorSize"] = (double)theme.DockingSeparatorSize;
        style["MouseCursorScale"] = (double)theme.MouseCursorScale;
        style["CurveTessellationTol"] = (double)theme.CurveTessellationTol;
        style["CircleTessellationMaxError"] = (double)theme.CircleTessellationMaxError;

        // float2 vars
        void AddFloat2(string key, float[] v)
        {
            var arr = new TomlArray();
            arr.Add((double)v[0]);
            arr.Add((double)v[1]);
            style[key] = arr;
        }
        AddFloat2("WindowPadding", theme.WindowPadding);
        AddFloat2("WindowMinSize", theme.WindowMinSize);
        AddFloat2("WindowTitleAlign", theme.WindowTitleAlign);
        AddFloat2("FramePadding", theme.FramePadding);
        AddFloat2("ItemSpacing", theme.ItemSpacing);
        AddFloat2("ItemInnerSpacing", theme.ItemInnerSpacing);
        AddFloat2("CellPadding", theme.CellPadding);
        AddFloat2("TouchExtraPadding", theme.TouchExtraPadding);
        AddFloat2("ButtonTextAlign", theme.ButtonTextAlign);
        AddFloat2("SelectableTextAlign", theme.SelectableTextAlign);
        AddFloat2("SeparatorTextAlign", theme.SeparatorTextAlign);
        AddFloat2("SeparatorTextPadding", theme.SeparatorTextPadding);
        AddFloat2("DisplayWindowPadding", theme.DisplayWindowPadding);
        AddFloat2("DisplaySafeAreaPadding", theme.DisplaySafeAreaPadding);
        AddFloat2("TableAngledHeadersTextAlign", theme.TableAngledHeadersTextAlign);

        // bool vars
        style["AntiAliasedLines"] = theme.AntiAliasedLines;
        style["AntiAliasedLinesUseTex"] = theme.AntiAliasedLinesUseTex;
        style["AntiAliasedFill"] = theme.AntiAliasedFill;

        root["style"] = style;

        return Toml.FromModel(root);
    }

    public static ThemeDefinition? Deserialize(string toml)
    {
        if (!Toml.TryToModel<TomlTable>(toml, out var root, out var diagnostics))
        {
            foreach (var d in diagnostics)
                Console.WriteLine($"skittles: TOML parse error: {d}");
            return null;
        }

        var theme = new ThemeDefinition();

        // [meta]
        if (root.TryGetValue("meta", out var metaObj) && metaObj is TomlTable meta)
        {
            if (meta.TryGetValue("name", out var nameObj)) theme.Name = nameObj?.ToString() ?? "";
            if (meta.TryGetValue("description", out var descObj)) theme.Description = descObj?.ToString() ?? "";
        }

        // [colors]
        if (root.TryGetValue("colors", out var colorsObj) && colorsObj is TomlTable colors)
        {
            foreach (var kvp in colors)
            {
                if (!ColorNameToIndex.TryGetValue(kvp.Key, out int idx)) continue;
                if (kvp.Value is TomlArray arr && arr.Count >= 4)
                {
                    theme.Colors[idx] = new float[]
                    {
                        Convert.ToSingle(arr[0], CultureInfo.InvariantCulture),
                        Convert.ToSingle(arr[1], CultureInfo.InvariantCulture),
                        Convert.ToSingle(arr[2], CultureInfo.InvariantCulture),
                        Convert.ToSingle(arr[3], CultureInfo.InvariantCulture),
                    };
                }
            }
        }

        // [style]
        if (root.TryGetValue("style", out var styleObj) && styleObj is TomlTable style)
        {
            float GetF(string key, float def) => style.TryGetValue(key, out var v) ? Convert.ToSingle(v, CultureInfo.InvariantCulture) : def;
            bool GetB(string key, bool def) => style.TryGetValue(key, out var v) ? Convert.ToBoolean(v) : def;
            float[] GetF2(string key, float[] def)
            {
                if (style.TryGetValue(key, out var v) && v is TomlArray arr && arr.Count >= 2)
                    return new float[] { Convert.ToSingle(arr[0], CultureInfo.InvariantCulture), Convert.ToSingle(arr[1], CultureInfo.InvariantCulture) };
                return def;
            }

            theme.Alpha = GetF("Alpha", theme.Alpha);
            theme.DisabledAlpha = GetF("DisabledAlpha", theme.DisabledAlpha);
            theme.WindowRounding = GetF("WindowRounding", theme.WindowRounding);
            theme.WindowBorderSize = GetF("WindowBorderSize", theme.WindowBorderSize);
            theme.WindowBorderHoverPadding = GetF("WindowBorderHoverPadding", theme.WindowBorderHoverPadding);
            theme.ChildRounding = GetF("ChildRounding", theme.ChildRounding);
            theme.ChildBorderSize = GetF("ChildBorderSize", theme.ChildBorderSize);
            theme.PopupRounding = GetF("PopupRounding", theme.PopupRounding);
            theme.PopupBorderSize = GetF("PopupBorderSize", theme.PopupBorderSize);
            theme.FrameRounding = GetF("FrameRounding", theme.FrameRounding);
            theme.FrameBorderSize = GetF("FrameBorderSize", theme.FrameBorderSize);
            theme.IndentSpacing = GetF("IndentSpacing", theme.IndentSpacing);
            theme.ColumnsMinSpacing = GetF("ColumnsMinSpacing", theme.ColumnsMinSpacing);
            theme.ScrollbarSize = GetF("ScrollbarSize", theme.ScrollbarSize);
            theme.ScrollbarRounding = GetF("ScrollbarRounding", theme.ScrollbarRounding);
            theme.GrabMinSize = GetF("GrabMinSize", theme.GrabMinSize);
            theme.GrabRounding = GetF("GrabRounding", theme.GrabRounding);
            theme.LogSliderDeadzone = GetF("LogSliderDeadzone", theme.LogSliderDeadzone);
            theme.ImageBorderSize = GetF("ImageBorderSize", theme.ImageBorderSize);
            theme.TabRounding = GetF("TabRounding", theme.TabRounding);
            theme.TabBorderSize = GetF("TabBorderSize", theme.TabBorderSize);
            theme.TabMinWidthBase = GetF("TabMinWidthBase", theme.TabMinWidthBase);
            theme.TabMinWidthShrink = GetF("TabMinWidthShrink", theme.TabMinWidthShrink);
            theme.TabCloseButtonMinWidthSelected = GetF("TabCloseButtonMinWidthSelected", theme.TabCloseButtonMinWidthSelected);
            theme.TabCloseButtonMinWidthUnselected = GetF("TabCloseButtonMinWidthUnselected", theme.TabCloseButtonMinWidthUnselected);
            theme.TabBarBorderSize = GetF("TabBarBorderSize", theme.TabBarBorderSize);
            theme.TabBarOverlineSize = GetF("TabBarOverlineSize", theme.TabBarOverlineSize);
            theme.TableAngledHeadersAngle = GetF("TableAngledHeadersAngle", theme.TableAngledHeadersAngle);
            theme.TreeLinesSize = GetF("TreeLinesSize", theme.TreeLinesSize);
            theme.TreeLinesRounding = GetF("TreeLinesRounding", theme.TreeLinesRounding);
            theme.SeparatorTextBorderSize = GetF("SeparatorTextBorderSize", theme.SeparatorTextBorderSize);
            theme.DockingSeparatorSize = GetF("DockingSeparatorSize", theme.DockingSeparatorSize);
            theme.MouseCursorScale = GetF("MouseCursorScale", theme.MouseCursorScale);
            theme.CurveTessellationTol = GetF("CurveTessellationTol", theme.CurveTessellationTol);
            theme.CircleTessellationMaxError = GetF("CircleTessellationMaxError", theme.CircleTessellationMaxError);

            theme.WindowPadding = GetF2("WindowPadding", theme.WindowPadding);
            theme.WindowMinSize = GetF2("WindowMinSize", theme.WindowMinSize);
            theme.WindowTitleAlign = GetF2("WindowTitleAlign", theme.WindowTitleAlign);
            theme.FramePadding = GetF2("FramePadding", theme.FramePadding);
            theme.ItemSpacing = GetF2("ItemSpacing", theme.ItemSpacing);
            theme.ItemInnerSpacing = GetF2("ItemInnerSpacing", theme.ItemInnerSpacing);
            theme.CellPadding = GetF2("CellPadding", theme.CellPadding);
            theme.TouchExtraPadding = GetF2("TouchExtraPadding", theme.TouchExtraPadding);
            theme.ButtonTextAlign = GetF2("ButtonTextAlign", theme.ButtonTextAlign);
            theme.SelectableTextAlign = GetF2("SelectableTextAlign", theme.SelectableTextAlign);
            theme.SeparatorTextAlign = GetF2("SeparatorTextAlign", theme.SeparatorTextAlign);
            theme.SeparatorTextPadding = GetF2("SeparatorTextPadding", theme.SeparatorTextPadding);
            theme.DisplayWindowPadding = GetF2("DisplayWindowPadding", theme.DisplayWindowPadding);
            theme.DisplaySafeAreaPadding = GetF2("DisplaySafeAreaPadding", theme.DisplaySafeAreaPadding);
            theme.TableAngledHeadersTextAlign = GetF2("TableAngledHeadersTextAlign", theme.TableAngledHeadersTextAlign);

            theme.AntiAliasedLines = GetB("AntiAliasedLines", theme.AntiAliasedLines);
            theme.AntiAliasedLinesUseTex = GetB("AntiAliasedLinesUseTex", theme.AntiAliasedLinesUseTex);
            theme.AntiAliasedFill = GetB("AntiAliasedFill", theme.AntiAliasedFill);
        }

        return theme;
    }

    public static void SaveToFile(ThemeDefinition theme, string filePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, Serialize(theme));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error saving theme to {filePath}: {ex.Message}");
        }
    }

    public static ThemeDefinition? LoadFromFile(string filePath)
    {
        try
        {
            string toml = File.ReadAllText(filePath);
            return Deserialize(toml);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error loading theme from {filePath}: {ex.Message}");
            return null;
        }
    }
}
