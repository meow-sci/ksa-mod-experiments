using System;
using StarMap.API;
using MeowSci.DontStifleMeLib;

namespace MeowSci.DontStifleMe;

/// <summary>
/// Standalone entry. All UI is the "Don't Stifle Me" top-level menu registered by
/// <see cref="MenuBarPatch"/>; there is no separate window.
/// </summary>
[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            Patcher.Patch();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"dont-stifle-me: Error during initialization: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;
            Patcher.Unload();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"dont-stifle-me: Error during unload: {ex.Message}");
        }
    }
}
