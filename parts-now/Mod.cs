using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.PartsNowLib;
using StarMap.API;

namespace MeowSci.PartsNow;

/// <summary>
/// Standalone StarMap entry point for parts-now. The whole implementation lives in
/// <c>parts-now.lib</c>; this class only owns the lifecycle and the floating window.
/// </summary>
[StarMapMod]
public class Mod
{
    /// <summary>StarMap unload mode — parts-now holds GPU resources, so never unload immediately.</summary>
    public bool ImmediateUnload => false;

    private readonly PartsNowSubmod _submod = new();

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;

    /// <summary>
    /// Key that toggles the parts-now window, resolved once from <c>parts-now.toml</c>'s
    /// <c>hotkey</c> setting during <see cref="OnFullyLoaded"/>.
    /// </summary>
    private ImGuiKey _hotkey = ImGuiKey.F10;

    /// <summary>StarMap: earliest hook. Nothing to do — reservation happens in OnFullyLoaded.</summary>
    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    /// <summary>
    /// StarMap fires this as a Harmony postfix on <c>ModLibrary.LoadAll()</c>, i.e. BEFORE
    /// <c>ModLibrary.Bind()</c> allocates the shared interleaved mesh buffer. That ordering is what
    /// makes the mesh-headroom reservation in <see cref="PartsNowSubmod.Initialize"/> possible.
    /// </summary>
    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            Patcher.Patch();
            _submod.Initialize();
            _hotkey = ResolveHotkey();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: Error during initialization: {ex.Message}");
        }
    }

    /// <summary>StarMap: pre-GUI frame hook — drives the load job state machine.</summary>
    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;

        try
        {
            _submod.Update(dt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: Error in OnBeforeUi: {ex.Message}");
        }
    }

    /// <summary>StarMap: post-GUI frame hook — renders the standalone window.</summary>
    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;

            if (ImGui.IsKeyPressed(_hotkey))
                _windowVisible = !_windowVisible;

            if (_windowVisible)
                RenderWindow();

            _submod.RenderFloatingWindows();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: Error in OnAfterUi: {ex.Message}");
        }
    }

    /// <summary>StarMap: teardown — releases GPU resources and removes Harmony patches.</summary>
    [StarMapUnload]
    public void Unload()
    {
        try
        {
            _submod.Dispose();
            Patcher.Unload();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: Error during unload: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads the <c>hotkey</c> setting from <c>parts-now.toml</c> and turns it into an
    /// <see cref="ImGuiKey"/>. Anything unrecognised falls back to <see cref="ImGuiKey.F10"/>, with
    /// one log line so a typo is visible instead of silently doing nothing.
    /// </summary>
    private static ImGuiKey ResolveHotkey()
    {
        string configured = PartsNowSettings.Hotkey;

        if (Enum.TryParse(configured, ignoreCase: true, out ImGuiKey parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        Console.WriteLine(
            $"parts-now: '{configured}' is not a recognised ImGuiKey name — falling back to F10. "
            + "Use a member name such as F10, F12 or Home in parts-now.toml.");
        return ImGuiKey.F10;
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(700, 900), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Parts Now##pn_window", ref _windowVisible))
        {
            try
            {
                _submod.RenderContent();
            }
            catch (Exception ex)
            {
                ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"Error: {ex.Message}");
            }
        }
        ImGui.End();
    }
}
