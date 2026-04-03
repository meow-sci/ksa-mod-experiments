using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.JplRepo;

internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("jplrepo");

    // Cursor pos saved just before the version string jumps to the right edge.
    private static float _savedCursorX;
    private static float _savedCursorY;

    public static void Patch()
    {
        try
        {
            if (_harmony == null) return;
            // Transpiler snapshots cursor pos before SetCursorPosX blasts it to the right edge.
            _harmony.Patch(
                AccessTools.Method(typeof(Program), "DrawMenuBar"),
                transpiler: new HarmonyMethod(typeof(Patcher), nameof(DrawMenuBar_Transpiler)));
            // DrawProgramMenusHook prefix restores cursor and draws our menus.
            _harmony.Patch(
                AccessTools.Method(typeof(Program), nameof(Program.DrawProgramMenusHook)),
                prefix: new HarmonyMethod(typeof(Patcher), nameof(DrawProgramMenusHook_Prefix)));
            HotkeyGuard.Patch(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"jplrepo: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null) HotkeyGuard.Unpatch(_harmony);
            _harmony?.UnpatchAll("jplrepo");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"jplrepo: Error removing patches: {ex.Message}");
        }
    }

    static IEnumerable<CodeInstruction> DrawMenuBar_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // Inject SaveMenuCursorPos() right before SetCursorPosY — the first call in the
        // version-string positioning block — to snapshot cursor X/Y while it still
        // reflects the natural left-to-right menu flow.
        var setCursorPosY = AccessTools.Method(typeof(ImGui), "SetCursorPosY");
        bool injected = false;
        foreach (var instr in instructions)
        {
            if (!injected && instr.Calls(setCursorPosY))
            {
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(Patcher), nameof(SaveMenuCursorPos)));
                injected = true;
            }
            yield return instr;
        }
        if (!injected)
            Console.WriteLine("jplrepo: WARNING - could not find SetCursorPosY in DrawMenuBar IL");
    }

    static void SaveMenuCursorPos()
    {
        _savedCursorX = ImGui.GetCursorPosX();
        _savedCursorY = ImGui.GetCursorPosY();
    }

    static void DrawProgramMenusHook_Prefix()
    {
        // Restore the cursor to where it was after the last built-in menu,
        // before SetCursorPosX pushed it to the far right for the version string.
        ImGui.SetCursorPosX(_savedCursorX);
        ImGui.SetCursorPosY(_savedCursorY);

        if (ImGui.BeginMenu("Menu One"u8))
        {
            if (ImGui.MenuItem("one"u8)) Console.WriteLine("jplrepo: Menu One > one");
            if (ImGui.MenuItem("two"u8)) Console.WriteLine("jplrepo: Menu One > two");
            if (ImGui.MenuItem("three"u8)) Console.WriteLine("jplrepo: Menu One > three");
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Menu Two"u8))
        {
            if (ImGui.MenuItem("one"u8)) Console.WriteLine("jplrepo: Menu Two > one");
            if (ImGui.MenuItem("two"u8)) Console.WriteLine("jplrepo: Menu Two > two");
            if (ImGui.MenuItem("three"u8)) Console.WriteLine("jplrepo: Menu Two > three");
            ImGui.EndMenu();
        }
    }
}
