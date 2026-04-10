using System;
using HarmonyLib;

namespace MeowSci.FlexoLib;

public static class FlexoPatches
{
    public static void Apply(Harmony harmony)
    {
        try
        {
            harmony.PatchAll(typeof(FlexoPatches).Assembly);
            Console.WriteLine("flexo: Harmony patches applied");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Error applying patches: {ex.Message}");
        }
    }

    public static void Remove(Harmony harmony)
    {
        try
        {
            harmony.UnpatchAll(typeof(FlexoPatches).Assembly.GetName().Name);
            Console.WriteLine("flexo: Harmony patches removed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Error removing patches: {ex.Message}");
        }
    }
}
