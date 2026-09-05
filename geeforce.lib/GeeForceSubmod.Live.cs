using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.GeeForceLib;

public sealed partial class GeeForceSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        yield return new LiveStateItem<GForceRecorder>("recorder", "G-force recording", "Controlled vehicle", _recorder,
            recorder => _liveView.RenderContent(recorder, SampleIntervalSec), "Recording");
    }

}
