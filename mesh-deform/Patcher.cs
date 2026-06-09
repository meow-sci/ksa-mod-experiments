using System;
using HarmonyLib;
using MeowSci.KsaAbstractions;
using MeowSci.MeshDeformLib;

namespace MeowSci.MeshDeform;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        try
        {
            _harmony = new Harmony("mesh-deform");
            HotkeyGuard.Patch(_harmony);
            MeshDeformPatches.Apply(_harmony);
            Console.WriteLine("mesh-deform: Harmony patches applied");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"mesh-deform: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            MeshDeformManager.Cleanup();
            MeshDeformShaders.Cleanup();

            if (_harmony != null)
            {
                MeshDeformPatches.Remove(_harmony);
                HotkeyGuard.Unpatch(_harmony);
            }
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"mesh-deform: Error removing patches: {ex.Message}");
        }
    }
}
