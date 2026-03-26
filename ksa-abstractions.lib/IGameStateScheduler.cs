using System;
using System.Threading.Tasks;

namespace MeowSci.KsaAbstractions;

/// <summary>
/// Schedules work to be executed on the game thread.
/// Implementations must be thread-safe: callers enqueue from web-server threads;
/// the game thread drains the queue in OnBeforeUi.
/// </summary>
public interface IGameStateScheduler
{
    /// <summary>Schedules <paramref name="action"/> to run on the game thread and returns a Task that completes when it has run.</summary>
    Task Schedule(Action action);

    /// <summary>Schedules <paramref name="func"/> to run on the game thread and returns a Task that resolves to the return value.</summary>
    Task<T> Schedule<T>(Func<T> func);
}
