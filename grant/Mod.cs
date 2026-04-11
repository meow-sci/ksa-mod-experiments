using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.KsaAbstractions;
using MeowSci.AverageTwrLib;
using MeowSci.BlinkyLib;
using MeowSci.EternalFlameLib;
using MeowSci.GarrysTorchLib;
using MeowSci.GeeForceLib;
using MeowSci.GlassLib;
using MeowSci.IFeelSeenLib;
using MeowSci.CameraControllerOverrideLib;
using MeowSci.ConManLib;
using MeowSci.KittenAnimationsLib;
using MeowSci.KiwisMarblesLib;
using MeowSci.SkittlesLib;
using MeowSci.UnladenSwallowLib;
using MeowSci.ZippoLib;
using MeowSci.InanimateCarbonRodLib;
using MeowSci.HumbleArteestLib;
using MeowSci.DohLib;
using MeowSci.SpaceTapeLib;
using MeowSci.FlexoLib;

namespace MeowSci.Grant;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized = false;
    private bool _isDisposed = false;
    private bool _windowVisible = false;

    private readonly List<ISubmod> _submods = new();
    private readonly Dictionary<string, bool> _submodVisibility = new();
    private readonly Dictionary<string, bool> _headerOpen = new();
    private bool _collapseAll;
    private bool _expandAll;
    private double _timeSinceLastSave;
    private bool _autoSaveEnabled = false;
    private bool _showModTooltips = true;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            // Create all submods in display order
            var iFeelSeen = new IFeelSeenSubmod();
            var skittles = new SkittlesSubmod();
            var cameraOverride = new CameraControllerOverrideSubmod();

            _submods.Add(new AverageTwrSubmod());
            _submods.Add(new BlinkySubmod());
            _submods.Add(cameraOverride);
            _submods.Add(new ConManSubmod());
            _submods.Add(new DohSubmod());
            _submods.Add(new EternalFlameSubmod());
            _submods.Add(new GarrysTorchSubmod());
            _submods.Add(new GlassSubmod());
            _submods.Add(new GeeForceSubmod());
            _submods.Add(iFeelSeen);
            _submods.Add(new InanimateCarbonRodSubmod());
            _submods.Add(new KittenAnimationsSubmod());
            _submods.Add(new KiwisMarblesSubmod());
            _submods.Add(skittles);
            _submods.Add(new UnladenSwallowSubmod());
            _submods.Add(new HumbleArteestSubmod());
            _submods.Add(new ZippoSubmod());
            _submods.Add(new SpaceTapeSubmod());
            _submods.Add(new FlexoSubmod());

            // Initialize all submods so Tracker is populated before patching
            foreach (var submod in _submods)
            {
                submod.Initialize();
                _submodVisibility[submod.Name] = true;
            }

            // Restore persisted state
            GrantState.LoadImGuiWindowState();
            var (savedHeaders, savedVisibility) = GrantState.LoadSubmodState();
            foreach (var kvp in savedHeaders)
                _headerOpen[kvp.Key] = kvp.Value;
            foreach (var kvp in savedVisibility)
                if (_submodVisibility.ContainsKey(kvp.Key))
                    _submodVisibility[kvp.Key] = kvp.Value;
            _autoSaveEnabled = GrantState.AutoSaveEnabled;
            _showModTooltips = GrantState.ShowModTooltips;

            // Wire up Patcher dependencies and apply patches
            Patcher.IFeelSeenTracker = iFeelSeen.Tracker;
            Patcher.CameraSequencePlayer = cameraOverride.SequencePlayer;
            Patcher.MenuBarToggle = () => _windowVisible = !_windowVisible;

            Patcher.Patch();

            _isInitialized = true;
            Console.WriteLine($"grant: Initialized with {_submods.Count} submods");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"grant: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;

        // Update ALL submods every frame, even hidden ones
        foreach (var submod in _submods)
        {
            try { submod.Update(dt); }
            catch (Exception ex) { Console.WriteLine($"grant/{submod.Name}: Update error: {ex.Message}"); }
        }
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;

            if (ImGui.IsKeyPressed(ImGuiKey.F11))
                _windowVisible = !_windowVisible;

            if (_windowVisible)
            {
                RenderWindow();

                if (_autoSaveEnabled)
                {
                    _timeSinceLastSave += dt;
                    if (_timeSinceLastSave >= GrantState.SaveIntervalSeconds)
                    {
                        _timeSinceLastSave = 0;
                        SaveAll();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"grant: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            if (_autoSaveEnabled)
                SaveAll();

            foreach (var submod in _submods)
            {
                try { submod.Dispose(); }
                catch (Exception ex) { Console.WriteLine($"grant/{submod.Name}: Dispose error: {ex.Message}"); }
            }

            Patcher.Unload();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"grant: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(600, 800), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Grants Toolbox", ref _windowVisible, ImGuiWindowFlags.MenuBar))
        {
            // Menu bar
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("View"))
                {
                    ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);

                    if (ImGui.MenuItem("Show All"))
                        foreach (var s in _submods)
                            _submodVisibility[s.Name] = true;
                    if (ImGui.MenuItem("Hide All"))
                        foreach (var s in _submods)
                            _submodVisibility[s.Name] = false;
                    ImGui.Separator();

                    if (ImGui.MenuItem("Submod Tooltips", "", ref _showModTooltips))
                        GrantState.ShowModTooltips = _showModTooltips;
                    ImGui.Separator();

                    var sorted = new List<ISubmod>(_submods);
                    sorted.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    foreach (var s in sorted)
                    {
                        bool visible = _submodVisibility[s.Name];
                        if (ImGui.MenuItem(s.Name, "", ref visible))
                            _submodVisibility[s.Name] = visible;
                    }

                    ImGui.PopItemFlag();
                    ImGui.EndMenu();
                }

                if (ImGui.MenuItem("Collapse"))
                    _collapseAll = true;
                if (ImGui.MenuItem("Expand"))
                    _expandAll = true;

                if (ImGui.BeginMenu("State"))
                {
                    if (ImGui.MenuItem("Auto save enabled", "", ref _autoSaveEnabled))
                        GrantState.AutoSaveEnabled = _autoSaveEnabled;

                    ImGui.PushItemWidth(120f);
                    int interval = GrantState.SaveIntervalSeconds;
                    if (ImGui.DragInt("Auto-save interval (s)", ref interval, 1.0f, 1, 30))
                        GrantState.SaveIntervalSeconds = interval;
                    ImGui.PopItemWidth();

                    if (ImGui.MenuItem("Save window state now"))
                    {
                        _timeSinceLastSave = 0;
                        SaveAll();
                    }
                    ImGui.EndMenu();
                }

                ImGui.EndMenuBar();
            }

            // Render visible submods
            foreach (var submod in _submods)
            {
                if (!_submodVisibility[submod.Name]) continue;

                if (_expandAll)
                    ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                else if (_collapseAll)
                    ImGui.SetNextItemOpen(false, ImGuiCond.Always);
                else
                    ImGui.SetNextItemOpen(_headerOpen.GetValueOrDefault(submod.Name, true), ImGuiCond.Once);

                var headerLabel = _showModTooltips ? $"{submod.Name}  (?)" : submod.Name;
                bool isOpen = ImGui.CollapsingHeader(headerLabel, ImGuiTreeNodeFlags.DefaultOpen);
                _headerOpen[submod.Name] = isOpen;
                if (_showModTooltips && ImGui.IsItemHovered(ImGuiHoveredFlags.DelayNormal))
                    ImGui.SetTooltip(submod.Tooltip);

                if (isOpen)
                {
                    try { submod.RenderContent(); }
                    catch (Exception ex) { ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"Error: {ex.Message}"); }
                }
                ImGui.Separator();
            }
            _collapseAll = false;
            _expandAll = false;

            // Close button
            ImGui.Spacing();
            if (ImGui.Button("Close##grant"))
                _windowVisible = false;
        }
        ImGui.End();

        // Floating windows (e.g. editor popups) are rendered unconditionally so they
        // are not affected by whether the parent collapsing section is open.
        foreach (var submod in _submods)
        {
            try { submod.RenderFloatingWindows(); }
            catch (Exception ex) { Console.WriteLine($"grant/{submod.Name}: RenderFloatingWindows error: {ex.Message}"); }
        }
    }

    private void SaveAll()
    {
        GrantState.SaveImGuiWindowState();
        GrantState.SaveSubmodState(_headerOpen, _submodVisibility);
    }
}

