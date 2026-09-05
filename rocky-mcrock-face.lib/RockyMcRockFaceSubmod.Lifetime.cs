using System;
using System.Linq;
namespace MeowSci.RockyMcRockFaceLib;
public sealed partial class RockyMcRockFaceSubmod
{
    public void ReleaseLiveState()
    {
        if (_appliedSelections.Count == 0) return;
        foreach (var body in _controller.Bodies) _controller.Restore(body); if (!_controller.RebuildRenderer(out var message)) throw new InvalidOperationException(message); _appliedSelections.Clear(); PruneUnusedMeshClones();
    }
}
