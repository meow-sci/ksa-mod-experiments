using System;
using System.IO;

namespace MeowSci.HumbleArteestLib.Experiments;

/// <summary>
/// Shared game directory resolution for experiments.
/// The game is launched via StarMap (C:\StarMap), so Process.MainModule points there.
/// We resolve the real KSA directory from the KSA.dll assembly location.
/// </summary>
internal static class GamePaths
{
    private const string DefaultKsaDir = @"C:\Program Files\Kitten Space Agency";
    private static string? _gameDir;
    private static bool _resolved;

    public static string? GameDir
    {
        get
        {
            if (!_resolved) Resolve();
            return _gameDir;
        }
    }

    public static string GetShaderPath(string relPath)
    {
        return Path.Combine(GameDir ?? DefaultKsaDir, "Content", "Core", "Shaders", relPath);
    }

    private static void Resolve()
    {
        _resolved = true;

        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "KSA" && !string.IsNullOrEmpty(asm.Location))
                {
                    var dir = Path.GetDirectoryName(asm.Location);
                    if (dir != null && Directory.Exists(Path.Combine(dir, "Content", "Core", "Shaders")))
                    {
                        _gameDir = dir;
                        Console.WriteLine($"humble-arteest: Game directory (from KSA.dll): {_gameDir}");
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"humble-arteest: Assembly lookup failed: {ex.Message}");
        }

        _gameDir = DefaultKsaDir;
        Console.WriteLine($"humble-arteest: Game directory (fallback): {_gameDir}");
    }
}
