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
using MeowSci.GarysTorchLib;
using MeowSci.GeeForceLib;
using MeowSci.GlassLib;
using MeowSci.IFeelSeenLib;
using MeowSci.CameraControllerOverrideLib;
using MeowSci.KittenAnimationsLib;
using MeowSci.KiwisMarblesLib;
using MeowSci.SkittlesLib;
using MeowSci.UnladenSwallowLib;
using MeowSci.ZippoLib;

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
    private bool _collapseAll;
    private bool _expandAll;

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
            _submods.Add(new EternalFlameSubmod());
            _submods.Add(new GarysTorchSubmod());
            _submods.Add(new GlassSubmod());
            _submods.Add(new GeeForceSubmod());
            _submods.Add(iFeelSeen);
            _submods.Add(new KittenAnimationsSubmod());
            _submods.Add(new KiwisMarblesSubmod());
            _submods.Add(skittles);
            _submods.Add(new UnladenSwallowSubmod());
            _submods.Add(new ZippoSubmod());

            // Initialize all submods so Tracker is populated before patching
            foreach (var submod in _submods)
            {
                submod.Initialize();
                _submodVisibility[submod.Name] = true;
            }

            // Wire up Patcher dependencies and apply patches
            Patcher.IFeelSeenTracker = iFeelSeen.Tracker;
            Patcher.SkittlesHasFocusedTextInput = () => skittles.HasFocusedTextInput;
            Patcher.CameraSequencePlayer = cameraOverride.SequencePlayer;

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
                RenderWindow();
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

        if (ImGui.Begin("Grants Toolbox", ref _windowVisible))
        {
            // Min/Max/Mods buttons — right-aligned
            {
                var style = ImGui.GetStyle();
                float pad = style.FramePadding.X * 2f;
                float spacing = style.ItemSpacing.X;
                float w1 = ImGui.CalcTextSize("min").X + pad;
                float w2 = ImGui.CalcTextSize("max").X + pad;
                float w3 = ImGui.CalcTextSize("mods").X + pad;
                float totalW = w1 + w2 + w3 + spacing * 2f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - totalW);
                if (ImGui.Button("min##grant_min"))
                    _collapseAll = true;
                ImGui.SameLine();
                if (ImGui.Button("max##grant_max"))
                    _expandAll = true;
                ImGui.SameLine();
                if (ImGui.Button("mods##grant_mods"))
                    ImGui.OpenPopup("##grant_context");
            }

            // Context menu popup
            if (ImGui.BeginPopup("##grant_context"))
            {
                ImGui.TextDisabled("Submod Visibility");
                ImGui.Separator();
                foreach (var submod in _submods)
                {
                    bool visible = _submodVisibility[submod.Name];
                    if (ImGui.Checkbox(submod.Name, ref visible))
                        _submodVisibility[submod.Name] = visible;
                }
                ImGui.EndPopup();
            }

            ImGui.Separator();

            // Render visible submods
            foreach (var submod in _submods)
            {
                if (!_submodVisibility[submod.Name]) continue;

                if (_expandAll)
                    ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                else if (_collapseAll)
                    ImGui.SetNextItemOpen(false, ImGuiCond.Always);

                if (ImGui.CollapsingHeader(submod.Name, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Indent();
                    try { submod.RenderContent(); }
                    catch (Exception ex) { ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"Error: {ex.Message}"); }
                    ImGui.Unindent();
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
    }
}

