# Fix IVA Parts Rendering

## Problem

After a recent KSA game update, interior (IVA) parts no longer render unless the camera is in IVA mode. Previously they always rendered regardless of camera mode. The kitchen-sink mod has a "Force IVA Rendering" toggle that attempts to fix this but **has no visible effect** — interior parts stay invisible.

## Deep Dive Analysis

### The Single Control Point

The entire IVA visibility decision is made in **one place**: `PartModel.AddInstance()` (PartModel.cs line 380):

```csharp
if (Template.RayTracing != PartModelModule.RaytracingMode.ShadowProxy
    && (!Template.Internal || Program.MainViewport.Mode == CameraMode.IVA))
{
    viewportData.InstanceList.Add(instanceData);
}
```

**Translation:** Interior parts (`Template.Internal == true`) are only added to the render list when `Program.MainViewport.Mode == CameraMode.IVA`. In all other camera modes (Orbit, Free, Map, Fixed), they're silently skipped.

### Key Facts

- `PartModel.Template` is a **public field** of type `PartModelModule.Template` (a reference type/class)
- `Template.Internal` is a **public bool field** (`[XmlElement("Internal")] public bool Internal = false;`)
- `PartModel.Instances` is a **public static `List<PartModel>`** containing all loaded part models
- `PartModel.Get()` caches by template ID — each unique template maps to one `PartModel` instance
- `PartModelDynamic.AddInstance()` has **NO Internal filter** (always renders)
- `PartModelGlass.AddInstance()` has **NO Internal filter** (always renders)
- `Viewport.Mode` is a **public field** on `Viewport`
- The call chain is: `PartTree.UpdateRenderData()` → `PartModelModule.UpdateRenderData()` → `PartModel.AddInstance()`
- No filtering happens before `AddInstance` — all modules call it unconditionally

### Raytracing Interaction

When raytracing is active AND in IVA mode, there's an early-return path (lines 371-378):

```csharp
if (Program.Instance.IsRaytracingActive
    && Program.MainViewport.Mode == CameraMode.IVA
    && Template.RayTracing != PartModelModule.RaytracingMode.Disabled)
{
    viewportData.RayTraceTransforms.Add(instanceData.ModelMatrix);
    if (Template.RayTracing != PartModelModule.RaytracingMode.ShadowProxy)
    {
        Program.Instance.RaytracingRenderer?.PartRaytracingDataOpaque.AddInstance(...);
        return;  // ← Early exit, skips rasterization
    }
}
```

This is only relevant when the camera IS in IVA mode — it doesn't affect the fix.

### StateBitFlag (Shader Side)

`PartModelModule.UpdateRenderData()` sets bit 5 (`0x20`) on `StateBitFlag` when in IVA mode. This flag is passed to GPU shaders and may affect lighting/rendering style. Our fix uses rasterization only (no IVA shader flag), which is correct for exterior camera viewing.

## Why the Current Patch Fails

The current `IvaForceRenderPatch.cs` patches `PartModel.AddInstance` with a Harmony prefix/postfix that temporarily flips `Template.Internal = false` before the method runs.

**This approach is logically sound** but likely fails because:

1. **Harmony method resolution**: `PartModel.AddInstance` may not resolve correctly at runtime — the decompiled sources may not match the actual binary's method signature or name exactly
2. **JIT inlining**: The method is small enough that the JIT compiler may have already inlined it into `PartModelModule.UpdateRenderData()` before the mod loads, making the standalone method unreachable
3. **Silent patch failure**: The `Patcher.cs` catches exceptions but the patch may silently not match

## Recommended Fix: Direct Template Mutation

**Instead of per-frame Harmony patching, directly mutate `Template.Internal` on all `PartModel` instances when the toggle is flipped.** This is the simplest possible approach — no Harmony rendering patches needed.

### Why This Is Better

- **No Harmony method resolution risk** — we don't patch `AddInstance` at all
- **No per-frame overhead** — the mutation happens once at toggle time
- **Guaranteed to work** — we modify the exact same field the game checks
- **Minimal code** — ~30 lines total

### Implementation

#### Step 1: Rewrite `IvaForceRender.cs` (kitchen-sink.lib)

Replace the simple bool toggle with a class that tracks and mutates template state:

```csharp
namespace MeowSci.KitchenSinkLib;

public static class IvaForceRender
{
    private static bool _enabled;
    private static readonly List<PartModelModule.Template> _mutatedTemplates = new();

    public static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value)
                ForceInternalVisible();
            else
                RestoreInternalHidden();
        }
    }

    private static void ForceInternalVisible()
    {
        _mutatedTemplates.Clear();
        foreach (var pm in PartModel.Instances)
        {
            if (pm.Template.Internal)
            {
                _mutatedTemplates.Add(pm.Template);
                pm.Template.Internal = false;
            }
        }
        Console.WriteLine($"kitchen-sink: Forced {_mutatedTemplates.Count} internal templates visible");
    }

    private static void RestoreInternalHidden()
    {
        foreach (var t in _mutatedTemplates)
            t.Internal = true;
        Console.WriteLine($"kitchen-sink: Restored {_mutatedTemplates.Count} internal templates");
        _mutatedTemplates.Clear();
    }
}
```

#### Step 2: Delete `IvaForceRenderPatch.cs` (kitchen-sink)

The Harmony patch on `PartModel.AddInstance` is no longer needed. Remove the entire file.

#### Step 3: Handle newly loaded parts (optional Harmony patch)

To catch parts that load AFTER the toggle is enabled (e.g., new vehicles entering physics range), add a small Harmony patch on the `PartModel` constructor:

```csharp
[HarmonyPatch(typeof(PartModel))]
[HarmonyPatch(MethodType.Constructor)]
[HarmonyPatch(new[] { typeof(PartModelModule.Template) })]
internal static class IvaNewPartModelPatch
{
    static void Postfix(PartModel __instance)
    {
        if (!IvaForceRender.Enabled) return;
        if (__instance.Template.Internal)
        {
            __instance.Template.Internal = false;
            IvaForceRender.TrackMutated(__instance.Template);
        }
    }
}
```

#### Step 4: Update UI text (KitchenSinkLib.cs)

No UI changes needed — the checkbox already calls `IvaForceRender.Enabled = value` which now triggers the mutation.

#### Step 5: Clean up on unload

Ensure `IvaForceRender.Enabled = false` is called during mod unload to restore template state.

## Todos

1. **rewrite-iva-force-render** — Rewrite `IvaForceRender.cs` in kitchen-sink.lib with direct template mutation logic (set/restore `Template.Internal` on `PartModel.Instances`)
2. **delete-old-patch** — Delete `IvaForceRenderPatch.cs` from kitchen-sink (the Harmony prefix/postfix on AddInstance)
3. **add-constructor-patch** — Add a Harmony postfix on `PartModel` constructor to catch newly created parts when toggle is ON
4. **ensure-cleanup-on-unload** — Call `IvaForceRender.Enabled = false` in `Mod.Unload()` to restore state
5. **build-and-verify** — `dotnet build` the solution to verify compilation

## Risk Assessment

- **Low risk**: `Template.Internal` is a simple bool field on a reference type; mutation is safe
- **Template sharing**: `PartModel.Get()` ensures one PartModel per template ID; no duplicate mutations
- **Restore safety**: If the mod unloads or crashes, templates stay mutated until game restart — acceptable tradeoff
- **Decompiled source accuracy**: The `PartModel.Instances` list and `Template.Internal` field need to exist at runtime. If the binary differs from decompiled sources, this approach will fail gracefully (null reference → caught by try/catch)

## Fallback: Viewport Mode Spoofing

If direct template mutation doesn't work (runtime fields don't match decompiled sources), the fallback is to temporarily set `Program.MainViewport.Mode = CameraMode.IVA` during `PartTree.UpdateRenderData()` via Harmony prefix/postfix. This makes all Internal checks pass naturally but has side effects:
- Sets shader IVA flag (bit 5) — may change part appearance
- Activates raytracing path for raytracing-enabled parts (which then skip rasterization but won't have a raytrace pass to display them)

This fallback is more complex and should only be attempted if Approach A fails.

## Files to Modify

| File | Action |
|------|--------|
| `kitchen-sink.lib/IvaForceRender.cs` | Rewrite with template mutation logic |
| `kitchen-sink/IvaForceRenderPatch.cs` | Delete (or replace with constructor patch) |
| `kitchen-sink/Mod.cs` | Ensure cleanup calls `IvaForceRender.Enabled = false` on unload |
| `kitchen-sink/Patcher.cs` | No changes needed (PatchAll picks up new patch class) |

## Reference: Decompiled Source Locations

- `PartModel.AddInstance()`: `decomp/ksa/KSA/PartModel.cs:368-384`
- `PartModel.Instances`: `decomp/ksa/KSA/PartModel.cs:318`
- `PartModel.Template`: `decomp/ksa/KSA/PartModel.cs:322`
- `PartModelModule.Template.Internal`: `decomp/ksa/KSA/PartModelModule.cs:35-36`
- `PartTree.UpdateRenderData()`: `decomp/ksa/KSA/PartTree.cs:411-437`
- `CameraMode enum`: `decomp/ksa/KSA/CameraMode.cs:5-17`
- `Viewport.Mode`: `decomp/ksa/KSA/Viewport.cs:14`
