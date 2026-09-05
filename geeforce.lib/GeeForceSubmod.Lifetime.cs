using System;
using System.Linq;
namespace MeowSci.GeeForceLib;
public sealed partial class GeeForceSubmod
{
    public void ReleaseLiveState()
    {
        if (_recorder != null) { _recorder.IsRecording = false; _recorder.Clear(); } _accumulator = 0;
    }
}
