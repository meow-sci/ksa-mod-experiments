using System;
using System.Collections.Generic;

namespace MeowSci.Unscience.Contracts;

public static class WorkspaceRestore
{
    /// <summary>All decoding finishes before any participant is changed. No lifecycle operations.</summary>
    public static Action Prepare(WorkspaceDocument document, IReadOnlyList<IWorkspaceParticipant> participants,
        IReadOnlyDictionary<string, DraftState> defaults)
    {
        var prepared = new List<Action>();
        var rollback = new List<Action>();
        foreach (var participant in participants)
        {
            var state = document.Features.TryGetValue(participant.FeatureId, out var saved)
                ? saved.Draft : defaults[participant.FeatureId];
            prepared.Add(participant.PrepareRestore(state.Clone()));
            rollback.Add(participant.PrepareRestore(participant.CaptureDraft()));
        }
        return () =>
        {
            int index = 0;
            try { for (; index < prepared.Count; index++) prepared[index](); }
            catch
            {
                for (int i = Math.Min(index, rollback.Count - 1); i >= 0; i--) rollback[i]();
                throw;
            }
        };
    }
}
