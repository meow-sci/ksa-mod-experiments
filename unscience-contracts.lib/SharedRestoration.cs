using System;
using System.Collections.Generic;
namespace MeowSci.Unscience.Contracts;

/// <summary>Shared-scope baseline; the last owner restores, and failed restoration remains retryable.</summary>
public sealed class SharedRestoration
{
    private sealed class Entry(Action restore) { public readonly Action Restore = restore; public int Owners; }
    private readonly Dictionary<object, Entry> _entries = new(ReferenceEqualityComparer.Instance);
    public IDisposable Acquire(object key, Func<Action> capture)
    {
        if (!_entries.TryGetValue(key, out var entry)) _entries.Add(key, entry = new(capture()));
        entry.Owners++;
        return new Lease(this, key, entry);
    }
    private sealed class Lease(SharedRestoration owner, object key, Entry entry) : IDisposable
    {
        private bool _released;
        public void Dispose()
        {
            if (_released) return;
            if (entry.Owners == 1) { entry.Restore(); owner._entries.Remove(key); }
            entry.Owners--;
            _released = true;
        }
    }
}
