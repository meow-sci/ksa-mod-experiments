using System;

namespace MeowSci.SteelyEyedMissileKittenLib.Events;

/// <summary>Simple event bus for publishing and subscribing to flight events.</summary>
public sealed class EventBus
{
    public event Action<FlightEvent>? OnEvent;

    /// <summary>Publishes a flight event to all subscribers.</summary>
    public void Publish(FlightEvent evt)
    {
        try
        {
            OnEvent?.Invoke(evt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventBus] Error publishing event {evt.Type}: {ex.Message}");
        }
    }
}
