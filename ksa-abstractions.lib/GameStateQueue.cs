using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace MeowSci.KsaAbstractions;

/// <summary>
/// Thread-safe queue that collects game-state mutations enqueued by web-server
/// threads and executes them sequentially on the game thread when
/// <see cref="DrainOnGameThread"/> is called.
/// </summary>
public sealed class GameStateQueue : IGameStateScheduler
{
    private readonly ConcurrentQueue<WorkItem> _queue = new();

    /// <inheritdoc />
    public Task Schedule(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(new WorkItem(() =>
        {
            action();
            tcs.SetResult();
        }, ex => tcs.SetException(ex)));
        return tcs.Task;
    }

    /// <inheritdoc />
    public Task<T> Schedule<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(new WorkItem(() =>
        {
            var result = func();
            tcs.SetResult(result);
        }, ex => tcs.SetException(ex)));
        return tcs.Task;
    }

    /// <summary>
    /// Must be called on the game thread (e.g. in <c>OnBeforeUi</c>).
    /// Drains all pending work items and runs them in enqueue order.
    /// </summary>
    public void DrainOnGameThread()
    {
        while (_queue.TryDequeue(out var item))
            item.Run();
    }

    private sealed class WorkItem
    {
        private readonly Action _run;
        private readonly Action<Exception> _fail;

        public WorkItem(Action run, Action<Exception> fail)
        {
            _run = run;
            _fail = fail;
        }

        public void Run()
        {
            try
            {
                _run();
            }
            catch (Exception ex)
            {
                _fail(ex);
            }
        }
    }
}
