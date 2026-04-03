using System;
using System.Collections.Generic;
using MeowSci.SteelyEyedMissileKittenLib.Events;

namespace MeowSci.SteelyEyedMissileKittenLib.Persistence;

/// <summary>Subscribes to EventBus and batches pending events to SQLite.</summary>
public sealed class EventWriter : IDisposable
{
    private readonly EventDatabase _db;
    private readonly List<FlightEvent> _pendingWrites = new();
    private readonly object _lock = new();

    public EventWriter(EventDatabase db, EventBus eventBus)
    {
        _db = db;
        eventBus.OnEvent += OnEvent;
    }

    private void OnEvent(FlightEvent evt)
    {
        lock (_lock) { _pendingWrites.Add(evt); }
    }

    /// <summary>Flush all pending events to SQLite. Call periodically (e.g., every 5 seconds).</summary>
    public void Flush()
    {
        List<FlightEvent> toWrite;
        lock (_lock)
        {
            if (_pendingWrites.Count == 0) return;
            toWrite = new List<FlightEvent>(_pendingWrites);
            _pendingWrites.Clear();
        }
        foreach (var evt in toWrite)
        {
            try { _db.InsertEvent(evt); }
            catch (Exception ex) { Console.WriteLine($"[EventWriter] Failed to write event: {ex.Message}"); }
        }
    }

    public void Dispose() => Flush();
}
