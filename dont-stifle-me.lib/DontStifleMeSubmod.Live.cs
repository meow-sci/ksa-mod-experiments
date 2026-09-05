using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.DontStifleMeLib;

public sealed partial class DontStifleMeSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        yield return new LiveStateItem<string>("editor-policy", "Editor limits", "Global editor policy", "policy", _ =>
        {
            bool enabled = EditorScaleSettings.Enabled, snap = EditorScaleSettings.Snap, expanded = EditorLimitSettings.JplSaidNoClamps;
            bool changed = ImGui.Checkbox("Enabled", ref enabled);
            changed |= ImGui.Checkbox("Snap scaling", ref snap);
            changed |= ImGui.Checkbox("Expanded configurable limits", ref expanded);
            if (changed) ApplyPolicy(enabled, snap, expanded);
            if (ImGui.Button(" Restore stock ")) ApplyPolicy(false, true, false);
        });
    }

}
