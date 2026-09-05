using System;
namespace MeowSci.Unscience.Contracts;

/// <summary>One runtime capability: retryable activation, transactional rollback, and retained failed release.</summary>
public sealed class RuntimeActivation(Action activate, Action release)
{
    public bool Active { get; private set; }
    public bool Failed { get; private set; }
    public void Retry() => Failed = false;
    public void Sync(bool needed)
    {
        if (!needed)
        {
            if (!Active) return;
            release();
            Active = false;
            return;
        }
        if (Active || Failed) return;
        try { activate(); Active = true; }
        catch
        {
            Failed = true;
            // The adapter owns rollback of partial activation; failed rollback retains ownership.
            Active = true;
            release();
            Active = false;
            throw;
        }
    }
}
