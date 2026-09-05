using System.Collections.Generic;
using Brutal.Numerics;
namespace MeowSci.HumbleArteestLib;
public sealed class PaintDraft
{
    public float3 Color = new(1f, .25f, .2f);
    public float4 KittenColor = new(1f, 1f, 1f, 1f);
    public int Blend;
    public int Scope;
    public float Temperature;
    public float Tfi;
    public bool AllEngines = true;
    public bool AllMaterials = true;
    [System.Text.Json.Serialization.JsonIgnore]
    public HashSet<string> Engines = new();
    public HashSet<string> Materials = new();
    [System.Text.Json.Serialization.JsonIgnore]
    public HashSet<string> Parts = new();
    public HashSet<string> Templates = new();
    public string Filter = "";
}
