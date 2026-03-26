using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.GlassLib;

namespace MeowSci.Grant.Submods;

internal sealed class GlassSubmod : IGrantSubmod
{
    public string Name => "Glass \u2014 Camera Lens";

    private int _selectedPreset;
    private float _manualFov = 50f;
    private bool _manualMode;

    private static readonly (string Name, float Fov)[] Presets = new[]
    {
        ("Game Default", 50f),
        ("Super Telephoto (200mm)", 15f),
        ("Telephoto (135mm)", 20f),
        ("Portrait (85mm)", 30f),
        ("Standard (50mm)", 50f),
        ("Wide Angle (28mm)", 75f),
        ("Ultra Wide (16mm)", 100f),
        ("Fisheye (10mm)", 120f),
    };

    public void Initialize() { }

    public void Update(double dt)
    {
        try { FovController.ApplyFov(); }
        catch (Exception ex) { Console.WriteLine($"grant/glass: Error applying FOV override: {ex.Message}"); }
    }

    public void RenderContent()
    {
        ImGui.TextColored(new float4(0.0f, 1.0f, 1.0f, 1.0f), "Glass");
        ImGui.Separator();

        float currentFovRad = Program.GetCamera().GetFieldOfView();
        float currentFovDeg = currentFovRad * (180f / MathF.PI);
        ImGui.Text($"Current FOV: {currentFovDeg:F1}\u00b0");
        ImGui.Separator();

        ImGui.Text("Lens Presets");
        for (int i = 0; i < Presets.Length; i++)
        {
            if (ImGui.RadioButton(Presets[i].Name + "##glass", _selectedPreset == i && !_manualMode))
            {
                _selectedPreset = i;
                _manualMode = false;
                FovController.OverrideFovDegrees = Presets[i].Fov;
                FovController.IsOverrideActive = true;
            }
        }
        ImGui.Separator();

        ImGui.Text("Manual FOV");
        bool manualChecked = _manualMode;
        if (ImGui.Checkbox("Manual mode##glass", ref manualChecked))
        {
            _manualMode = manualChecked;
            if (_manualMode)
            {
                _selectedPreset = -1;
                FovController.OverrideFovDegrees = _manualFov;
                FovController.IsOverrideActive = true;
            }
        }
        if (_manualMode)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat("FOV\u00b0##glass", ref _manualFov, 0.25f, 1f, 179f))
            {
                _manualFov = MathF.Max(1f, MathF.Min(179f, _manualFov));
                FovController.OverrideFovDegrees = _manualFov;
                FovController.IsOverrideActive = true;
            }
        }
        ImGui.Separator();

        if (ImGui.Button("Reset to Game Default##glass"))
        {
            _selectedPreset = 0;
            _manualMode = false;
            FovController.OverrideFovDegrees = 50f;
            FovController.IsOverrideActive = true;
        }
    }

    public void Dispose()
    {
        FovController.DisableOverride();
    }
}
