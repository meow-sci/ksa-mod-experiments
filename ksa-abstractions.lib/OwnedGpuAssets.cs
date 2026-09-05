using System;
using Brutal.VulkanApi;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Core;
using KSA;

namespace MeowSci.KsaAbstractions;

/// <summary>Only assets created by the caller may be registered. Detach consumers before disposal.</summary>
public sealed class OwnedGpuAssets : IDisposable
{
    private readonly List<Action> _release = new();
    public T Own<T>(AssetManager<T> manager, T asset) where T : LoadedAssetRef
    {
        var field = typeof(AssetManager<T>).GetField("AssetMap", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AssetManager<T>).FullName, "AssetMap");
        var map = (ConcurrentDictionary<AssetName, T>)field.GetValue(manager)!;
        _release.Add(() =>
        {
            // Remove only this exact reference, never a replacement or stock asset with the same name.
            ((ICollection<KeyValuePair<AssetName, T>>)map).Remove(new(asset.Id, asset));
            asset.Dispose();
        });
        return asset;
    }
    public void Dispose()
    {
        if (_release.Count == 0) return;
        Program.GetRenderer().Device.WaitIdle();
        for (int i = _release.Count - 1; i >= 0; i--)
        {
            _release[i]();
            _release.RemoveAt(i);
        }
    }
}
