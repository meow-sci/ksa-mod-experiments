using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;
namespace MeowSci.Unscience;

/// <summary>Retains failed feature identity and detached saved data without invoking broken game code.</summary>
internal sealed class UnavailableFeature(string id, string error) : IWorkspaceFeature
{
    public string FeatureId => id;
    public string Name => id + " (unavailable)";
    public string Tooltip => error;
    public DraftBindings Draft { get; } = new();
    private DraftState _state = new();
    public DraftState CaptureDraft() => DraftJson.Clone(_state);
    public Action PrepareRestore(DraftState state)
    { var detached = DraftJson.Clone(state); return () => _state = detached; }
    public void Initialize() { }
    public void Update(double dt) { }
    public void RenderContent() => ImGui.TextWrapped(error);
    public void Dispose() { }
    public void ReleaseLiveState() { }
    public IEnumerable<ILiveStateItem> GetLiveItems() => Array.Empty<ILiveStateItem>();
}
