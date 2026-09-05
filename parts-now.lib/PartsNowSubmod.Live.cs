using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.PartsNowLib;

public sealed partial class PartsNowSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        foreach (var record in RuntimeModRegistry.All())
            yield return new LiveStateItem<LoadedModRecord>(record.ModId, record.ModId, record.ModDir, record, r =>
            { ImGui.Text($"{r.NewParts.Count} part templates loaded"); _liveModPanel.Inspect(r.ModId, CanLoad); });
        yield return new LiveStateItem<ResultsPanel>("loader", "Runtime loader and GPU budget", "Game resources", _resultsPanel, panel =>
        { _statusPanel.Render(_selfTestProblems); panel.Render(); });
    }

}
