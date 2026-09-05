using System;
using HarmonyLib;
using MeowSci.KsaAbstractions;
namespace MeowSci.Unscience;
internal static class Patcher
{
    private static Harmony? _harmony;
    public static Action? MenuBarToggle { get; set; }
    public static void Patch()
    {
        _harmony = new Harmony("MeowSci.Unscience");
        try
        {
            HotkeyGuard.Patch(_harmony);
            HiddenUiFrameHook.Patch(_harmony);
            MenuBarPatch.ToggleWindow = MenuBarToggle;
            MenuBarPatch.Apply(_harmony);
        }
        catch { Unload(); throw; }
    }
    public static void Unload()
    {
        if (_harmony == null) return;
        try
        {
            HotkeyGuard.Unpatch(_harmony);
            HiddenUiFrameHook.Unpatch(_harmony);
            MenuBarPatch.Remove(_harmony);
        }
        finally { _harmony.UnpatchAll(_harmony.Id); _harmony = null; }
    }
}
