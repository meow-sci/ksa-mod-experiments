using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;

namespace MeowSci.Glass;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized = false;
    private bool _isDisposed = false;
    private bool _windowVisible = false;

    // Lens preset index (0 = Game Default, then each preset)
    private int _selectedPreset = 0;
    // Manual FOV slider value
    private float _manualFov = 50f;
    // Whether manual mode is active (vs preset)
    private bool _manualMode = false;

    private static readonly (string Name, float Fov)[] Presets = new[]
    {
        ("Game Default", 0f),
        ("Super Telephoto (200mm)", 15f),
        ("Telephoto (135mm)", 20f),
        ("Portrait (85mm)", 30f),
        ("Standard (50mm)", 50f),
        ("Wide Angle (28mm)", 75f),
        ("Ultra Wide (16mm)", 100f),
        ("Fisheye (10mm)", 120f),
    };

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
            Console.WriteLine($"glass: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt) { }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;

            if (ImGui.IsKeyPressed(ImGuiKey.F9))
                _windowVisible = !_windowVisible;

            if (_windowVisible)
                RenderWindow();

            if (Patcher.IsOverrideActive)
            {
                try
                {
                    Program.GetCamera().SetFieldOfView(Patcher.OverrideFovDegrees);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"glass: Error applying FOV override: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"glass: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            Patcher.Unload();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"glass: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(350, 400), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Glass — Camera Lens", ref _windowVisible))
        {
            // 1. Header: cyan colored text
            ImGui.TextColored(new float4(0.0f, 1.0f, 1.0f, 1.0f), "Glass");
            ImGui.Separator();

            // 2. Current FOV display
            float currentFovRad = Program.GetCamera().GetFieldOfView();
            float currentFovDeg = currentFovRad * (180f / MathF.PI);
            ImGui.Text($"Current FOV: {currentFovDeg:F1}°");
            ImGui.Separator();

            // 3. Lens Presets section
            ImGui.Text("Lens Presets");
            for (int i = 0; i < Presets.Length; i++)
            {
                if (ImGui.RadioButton(Presets[i].Name, _selectedPreset == i && !_manualMode))
                {
                    _selectedPreset = i;
                    _manualMode = false;
                    if (i == 0)
                    {
                        // Game Default
                        Patcher.IsOverrideActive = false;
                    }
                    else
                    {
                        Patcher.OverrideFovDegrees = Presets[i].Fov;
                        Patcher.IsOverrideActive = true;
                    }
                }
            }
            ImGui.Separator();

            // 4. Manual FOV section
            ImGui.Text("Manual FOV");
            bool manualChecked = _manualMode;
            if (ImGui.Checkbox("Manual mode", ref manualChecked))
            {
                _manualMode = manualChecked;
                if (_manualMode)
                {
                    _selectedPreset = -1;
                    Patcher.OverrideFovDegrees = _manualFov;
                    Patcher.IsOverrideActive = true;
                }
            }
            if (_manualMode)
            {
                if (ImGui.SliderFloat("FOV°", ref _manualFov, 15f, 120f))
                {
                    Patcher.OverrideFovDegrees = _manualFov;
                    Patcher.IsOverrideActive = true;
                }
            }
            ImGui.Separator();

            // 5. Reset button
            if (ImGui.Button("Reset to Game Default"))
            {
                _selectedPreset = 0;
                _manualMode = false;
                Patcher.IsOverrideActive = false;
            }
        }
        ImGui.End();
    }
}

