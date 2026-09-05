using System;
using System.Runtime.CompilerServices;
namespace MeowSci.KsaAbstractions;
public static class LiveIdentity
{
    private sealed class Key { public string Id { get; } = Guid.NewGuid().ToString("N"); }
    private static readonly ConditionalWeakTable<object, Key> Keys = new();
    public static string Get(object instance) => Keys.GetValue(instance, _ => new Key()).Id;
}
