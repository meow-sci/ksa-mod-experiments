using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace MeowSci.KsaAbstractions;

/// <summary>Feature-defined patch demand; independent owners make rollback and last-use teardown local.</summary>
public sealed class FeatureRuntime
{
    private static readonly ConditionalWeakTable<IWorkspaceFeature, FeatureRuntime> Instances = new();
    public static FeatureRuntime For(IWorkspaceFeature feature) => Instances.GetValue(feature, f => new(f.FeatureId));
    private readonly string _id;
    private readonly List<(RuntimePatchLease Lease, Func<bool> Needed)> _patches = new();
    public string? Error { get; private set; }
    private FeatureRuntime(string id) => _id = id;
    public void Patches(string group, Func<bool> needed, Action<Harmony> apply, Action<Harmony> remove)
        => _patches.Add((new RuntimePatchLease($"{_id}/{group}", apply, remove), needed));
    public void Sync()
    {
        foreach (var (lease, needed) in _patches)
        {
            try { lease.Sync(needed()); }
            catch (Exception ex) { if (Error != ex.Message) Console.WriteLine($"unscience/{_id}: {ex}"); Error = ex.Message; }
        }
    }
    public void ReportError(Exception error) => Error = error.Message;
    public void Retry() { Error = null; foreach (var (lease, _) in _patches) lease.Retry(); }
    public void ReleasePatches()
    {
        foreach (var (lease, _) in _patches) lease.Sync(false);
    }
}

public sealed class RuntimePatchLease
{
    private Harmony? _harmony;
    private readonly MeowSci.Unscience.Contracts.RuntimeActivation _activation;
    public RuntimePatchLease(string id, Action<Harmony> apply, Action<Harmony> remove)
    {
        _activation = new(() =>
        {
            _harmony = new Harmony("MeowSci.Unscience/" + id);
            apply(_harmony);
        }, () =>
        {
            if (_harmony == null) return;
            // Always roll back installed hooks, including when a feature-specific cleanup throws.
            try { remove(_harmony); }
            finally { _harmony.UnpatchAll(_harmony.Id); }
            _harmony = null;
        });
    }
    public bool Active => _activation.Active;
    public void Retry() => _activation.Retry();
    public void Sync(bool needed) => _activation.Sync(needed);
}
