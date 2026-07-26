using System;
using HarmonyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.PartsNow;

/// <summary>
/// Harmony instance for the standalone parts-now mod. parts-now patches nothing of its own —
/// the only patch is the mandatory <see cref="HotkeyGuard"/> (parts-now has heavy text input,
/// so blocking game hotkeys while an ImGui field has focus is not optional).
/// </summary>
[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("parts-now");

    /// <summary>Applies the HotkeyGuard patch.</summary>
    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            if (_harmony != null) HotkeyGuard.Patch(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: Error applying patches: {ex.Message}");
        }
    }

    /// <summary>Removes the HotkeyGuard patch and drops the Harmony instance.</summary>
    public static void Unload()
    {
        try
        {
            if (_harmony != null) HotkeyGuard.Unpatch(_harmony);
            _harmony?.UnpatchAll("parts-now");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: Error removing patches: {ex.Message}");
        }
    }
}
