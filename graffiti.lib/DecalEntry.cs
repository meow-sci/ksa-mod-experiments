using Brutal.Numerics;
using KSA;

namespace MeowSci.GraffitiLib;

/// <summary>Which frame a decal's anchor is stored in.</summary>
public enum DecalAnchorKind
{
    /// <summary>Anchored to a vehicle part (or sub-part) in that part's local frame.</summary>
    Vehicle,
    /// <summary>Anchored to a celestial's terrain at geodetic (lat, lon).</summary>
    Terrain,
}

/// <summary>How the decal's PNG resolved on the GPU.</summary>
public enum DecalTextureState
{
    Ready,
    Missing,
    Failed,
}

/// <summary>
/// One placed decal: the user's desired state, the anchor resolution refreshed once per frame,
/// and the per-frame matrices the render pass consumes. A mutable class (not a record) because
/// the published array holds the same objects the frame driver mutates in place.
/// </summary>
/// <remarks>
/// Mutated on the game thread only (UI + <c>GraffitiSubmod.Update</c>); read by the render
/// postfix on the same main thread, so no locking is required. Nothing here is stored in
/// ecliptic or ego coordinates: a vehicle anchor lives in its part's local frame and a terrain
/// anchor in geodetic lat/lon, so both survive bubble frame switches, floating-origin shifts
/// and planet rotation.
/// </remarks>
public sealed class DecalEntry
{
    private static int _nextId = 1;

    public int Id { get; } = _nextId++;

    /// <summary>The decal library file name (e.g. <c>cat.png</c>) this decal draws.</summary>
    public string ImageName = "";

    /// <summary>Which frame <see cref="Position"/>/<see cref="Normal"/> are expressed in.</summary>
    public DecalAnchorKind Kind { get; init; }

    /// <summary>The anchor vehicle id or celestial body id (stable across despawn/respawn).</summary>
    public string TargetId { get; init; } = "";

    /// <summary>The anchor part's <c>InstanceId</c> (vehicle anchors only; 0 for a terrain anchor).</summary>
    public uint PartInstanceId { get; init; }

    /// <summary>Vehicle: part-local metres. Terrain: <c>(latitudeDeg, longitudeDeg, 0)</c>.</summary>
    public double3 Position;

    /// <summary>Vehicle: the part-local surface normal the decal box points down. Terrain: zero (up is the radial).</summary>
    public double3 Normal;

    /// <summary>Vehicle: roll about <see cref="Normal"/>. Terrain: compass heading. Degrees.</summary>
    public double RotationDeg;

    /// <summary>Decal width in metres (the decal-space x extent).</summary>
    public double Width = 1.0;

    /// <summary>Decal height in metres (the decal-space y extent, "up" in the PNG).</summary>
    public double Height = 1.0;

    /// <summary>Projection-box depth along the normal, in metres (the decal-space z extent).</summary>
    public double Depth = 0.3;

    /// <summary>Opacity in [0, 1], multiplied into the sampled alpha.</summary>
    public double Alpha = 1.0;

    /// <summary>Gain on the lighting term in (0, 8].</summary>
    public double Brightness = 1.0;

    /// <summary>False hides the decal without removing it from the registry.</summary>
    public bool Visible = true;

    // ---- resolved once per frame on the game thread ------------------------------------------

    /// <summary>The resolved anchor vehicle, or null while it is despawned (dormant, not pruned).</summary>
    public Vehicle? Vehicle;

    /// <summary>The resolved anchor part (or sub-part), re-resolved by <see cref="PartInstanceId"/> each frame.</summary>
    public Part? Part;

    /// <summary>The resolved anchor celestial, or null while the system does not contain it.</summary>
    public Celestial? Body;

    /// <summary>The bindless slot the image occupies, or -1 while it has none.</summary>
    public int TextureHandle = -1;

    /// <summary>How the image resolved (shown in the placed-decals list).</summary>
    public DecalTextureState TextureState = DecalTextureState.Missing;

    /// <summary>Anchor resolved AND texture resident — the only state that draws.</summary>
    public bool Live;

    // ---- per-frame render outputs, filled by DecalAnchors on the game thread ----------------

    /// <summary>Decal unit cube → ego, row-vector convention (<c>v * M</c>). Rebuilt every frame.</summary>
    public float4x4 DecalToEgo = float4x4.Identity;

    /// <summary>The exact inverse of <see cref="DecalToEgo"/>, computed in double before packing.</summary>
    public float4x4 EgoToDecal = float4x4.Identity;

    /// <summary>The decal's outward (+z) axis in ego, normalised — the fragment shader's facing reference.</summary>
    public float3 AxisZEgo = float3.UnitZ;

    /// <summary>Distance from the camera to the decal origin, metres (the distance cull).</summary>
    public double DistanceEgo;
}
