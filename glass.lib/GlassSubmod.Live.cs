using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.GlassLib;

public sealed partial class GlassSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        if (FovController.IsOverrideActive)
            yield return new LiveStateItem<string>("fov", "Camera lens override", "Global camera FOV", "fov", _ =>
            {
                float fov = FovController.OverrideFovDegrees;
                if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("FOV"), ref fov, .5f, 1f, 179f)) FovController.SetFov(fov);
                if (ImGui.Button(" Copy settings to form ")) { _fov = (int)fov; _selectedPresetIndex = FindPresetIndex(_fov); }
                if (ImGui.Button(" Disable override ")) FovController.DisableOverride();
            });
    }

}
