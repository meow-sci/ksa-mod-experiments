using Brutal.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.ConManLib;

public sealed partial class ConManSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
yield return new LiveStateItem<LayoutManager>("gauges", "Console layout", "Game gauges", "Live", _layoutManager, manager =>
        { if (ImGui.Button("Copy layout to workspace", new float2(-1, 0))) CaptureLayout(); RenderLiveLayout(); });
    }

}
