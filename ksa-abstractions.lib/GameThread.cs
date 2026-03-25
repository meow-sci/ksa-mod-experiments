namespace MeowSci.KsaAbstractions;

/// <summary>
/// Static singleton providing access to the game-thread scheduler.
/// Feature modules use <see cref="Scheduler"/> to enqueue game-state mutations.
/// </summary>
public static class GameThread
{
    private static readonly GameStateQueue _instance = new();

    /// <summary>The game-state scheduler. Always available — backed by the singleton queue.</summary>
    public static IGameStateScheduler Scheduler => _instance;

    /// <summary>
    /// Call this on the game thread (e.g. in <c>OnBeforeUi</c>) to run all pending
    /// game-state mutations enqueued by web-server threads.
    /// </summary>
    public static void DrainOnGameThread() => _instance.DrainOnGameThread();
}
