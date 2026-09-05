using System;
using System.Collections.Generic;
using MeowSci.Unscience.Contracts;

namespace MeowSci.KsaAbstractions;

public interface IWorkspaceFeature : ISubmod, IWorkspaceParticipant
{
    DraftBindings Draft { get; }
    IEnumerable<ILiveStateItem> GetLiveItems();
    void CancelAuthoringGesture() { }
}

public interface ILiveStateItem
{
    string Id { get; }
    string Label { get; }
    string Target { get; }
    string Status { get; }
    void RenderInspector();
}

/// <summary>Feature-owned typed state projected into the common live inspector.</summary>
public sealed class LiveStateItem<T>(string id, string label, string target, T state,
    Action<T> render, string status = "Active") : ILiveStateItem
{
    public LiveStateItem(string id, string label, string target, string status, T state, Action<T> render)
        : this(id, label, target, state, render, status) { }
    public string Id => id;
    public string Label => label;
    public string Target => target;
    public string Status => status;
    public T State => state;
    public void RenderInspector() => render(State);
}
