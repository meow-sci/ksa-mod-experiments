using System;
using System.Collections.Generic;
using KSA;
using Brutal.Numerics;

namespace MeowSci.ZippoLib;

/// <summary>
/// Manages per-part animation queues. At most one animation runs per part at a time;
/// additional animations queue up to <see cref="MaxQueueDepth"/>.
/// </summary>
public class LightAnimationManager
{
    public const int MaxQueueDepth = 25;

    // Key = Part.Id string (not Part reference, which can go stale across frames)
    private readonly Dictionary<string, LightAnimation> _active = new();
    private readonly Dictionary<string, Queue<LightAnimation>> _queues = new();

    /// <summary>Returns the active animation for the part, or null.</summary>
    public LightAnimation? GetActiveAnimation(string partId) =>
        _active.TryGetValue(partId, out var anim) ? anim : null;

    /// <summary>Returns the number of queued (not yet active) animations for the part.</summary>
    public int GetQueueCount(string partId) =>
        _queues.TryGetValue(partId, out var q) ? q.Count : 0;

    /// <summary>Returns true if an animation is currently active for the part.</summary>
    public bool IsAnimating(string partId) => _active.ContainsKey(partId);

    /// <summary>
    /// Enqueues an animation for the part. If no animation is active, starts immediately.
    /// Returns false if the queue is full (MaxQueueDepth reached).
    /// </summary>
    public bool Enqueue(string partId, LightAnimation animation)
    {
        if (!_active.ContainsKey(partId))
        {
            _active[partId] = animation;
            return true;
        }

        if (!_queues.TryGetValue(partId, out var queue))
        {
            queue = new Queue<LightAnimation>();
            _queues[partId] = queue;
        }

        if (queue.Count >= MaxQueueDepth)
            return false;

        queue.Enqueue(animation);
        return true;
    }

    /// <summary>
    /// Ticks all active animations. For each completed animation, applies end values to the
    /// part and promotes the next queued animation (re-capturing start state from current part values).
    /// </summary>
    /// <param name="dt">Delta time in seconds.</param>
    /// <param name="partResolver">Resolves a Part from its ID. Return null if the part is unavailable.</param>
    public void Update(double dt, Func<string, Part?> partResolver)
    {
        var keys = new List<string>(_active.Keys);
        foreach (var partId in keys)
        {
            var part = partResolver(partId);
            if (part == null)
            {
                // Part no longer available — cancel all animations for it
                _active.Remove(partId);
                _queues.Remove(partId);
                continue;
            }

            var anim = _active[partId];
            var (color, intensity) = anim.Update(dt);

            LightController.ApplyColor(part, color);
            LightController.ApplyIntensity(part, intensity);

            if (anim.IsComplete)
            {
                _active.Remove(partId);
                PromoteNext(partId, part);
            }
        }
    }

    /// <summary>Cancels all animations (active and queued) for a specific part.</summary>
    public void CancelAll(string partId)
    {
        _active.Remove(partId);
        _queues.Remove(partId);
    }

    /// <summary>Cancels all animations across all parts.</summary>
    public void Clear()
    {
        _active.Clear();
        _queues.Clear();
    }

    private void PromoteNext(string partId, Part part)
    {
        if (!_queues.TryGetValue(partId, out var queue) || queue.Count == 0)
        {
            _queues.Remove(partId);
            return;
        }

        var next = queue.Dequeue();
        if (queue.Count == 0)
            _queues.Remove(partId);

        // Re-capture current part values as start state
        var currentColor = LightController.ReadColor(part.Template);
        var currentIntensity = LightController.ReadIntensity(part.Template);

        var corrected = new LightAnimation(
            currentColor, next.EndColor,
            currentIntensity, next.EndIntensity,
            next.DurationSeconds, next.Easing,
            next.EasingPowerStart, next.EasingPowerEnd);

        _active[partId] = corrected;
    }
}
