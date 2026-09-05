using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.KitchenSinkLib;

public sealed partial class KitchenSinkSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        if (IvaForceRender.Enabled)
            yield return new LiveStateItem<string>("iva", "Force IVA rendering", "Global", "iva", _ =>
            { if (ImGui.Button(" Restore normal rendering ")) IvaForceRender.Enabled = false; });
    }

}
