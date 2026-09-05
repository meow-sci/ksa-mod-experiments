using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.AverageTwrLib;

public sealed partial class AverageTwrSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        yield return new LiveStateItem<TwrSampleAccumulator>("recorder", "TWR recording", "Controlled vehicle", _accumulator,
            _ => RenderRecorder(), _isCollecting ? "Recording" : "Paused");
    }

}
