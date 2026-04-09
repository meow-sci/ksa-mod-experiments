# Space Tape: Fix Tank XML Elements — Implementation Plan

## Problem Summary

The tank XML serialization in space-tape produces XML that KSA cannot parse correctly. Two categories of issues exist:

### Issue 1: Missing `<Tank>` Wrapper Element

Current output (WRONG):
```xml
<PartGameData Id="MyPart" DisplayName="My Part">
  <CylindricalTank>
    ...
  </CylindricalTank>
</PartGameData>
```

Correct output (matches KSA game data):
```xml
<PartGameData Id="MyPart" DisplayName="My Part">
  <Tank>
    <CylindricalTank>
      ...
    </CylindricalTank>
  </Tank>
</PartGameData>
```

**Evidence**: Every tank definition in `decomp/ksa/Content/Core/PartGameData.xml` uses a `<Tank>` wrapper. The wrapper can optionally have an `Id` attribute (e.g. `<Tank Id="Tank1">`). User confirmed a manually-fixed XML with `<Tank>` wrapper loads correctly in-game.

### Issue 2: Bogus XML Elements That Don't Exist in KSA Data

The following elements are currently written but **never appear** in any KSA game data XML:

| Bad Element | Why It's Wrong |
|---|---|
| `<LocationAsmb X="0" Y="0" Z="0" />` | Never appears in PartGameData.xml; inherited C# field defaults to zero |
| `<Paf2Asmb X="0" Y="0" Z="0" />` | Never appears in PartGameData.xml; inherited C# field defaults to zero |
| `<Density KgPerM3="1400" />` | Never appears in PartGameData.xml; game computes density from material |
| `<DomeHeightFraction Value="0.5" />` | Never appears in PartGameData.xml; C# default is `1/√2 ≈ 0.707` |
| `<Mass Kg="..." />` (inside tank) | Never appears inside tank elements in PartGameData.xml |

### Issue 3: Wrong Material Attribute Name

Current output (WRONG):
```xml
<Material Value="Aluminum.2014(s)" />
```

Correct output (matches KSA game data):
```xml
<Material Id="Aluminum.2014(s)" />
```

**Evidence**: `decomp/ksa/KSA/SerializedReference.cs` declares `[XmlAttribute] public string Id`. Every material reference in `decomp/ksa/Content/Core/PartGameData.xml` uses `Id`.

---

## Reference: Correct KSA Tank XML

From `decomp/ksa/Content/Core/PartGameData.xml`, here are representative real game examples:

### Cylindrical Tank (1m × 1m)
```xml
<SubPartGameData Id="CoreFuelTankA_Subpart_Skin1W1HA" DisplayName="Fuel Tank 1m x 1m A">
  <Tank>
    <CylindricalTank>
      <Material Id="Aluminum.2014(s)" />
      <Length M="1" />
      <OuterRadius M="1" />
      <WallThickness Mm="2" />
    </CylindricalTank>
  </Tank>
</SubPartGameData>
```

### Cylindrical Tank (2m × 4m)
```xml
<SubPartGameData Id="CoreFuelTankA_Subpart_Skin2W4HA" DisplayName="Fuel Tank 2m x 4m A">
  <Tank>
    <CylindricalTank>
      <Material Id="Aluminum.2014(s)" />
      <Length M="4" />
      <OuterRadius M="2" />
      <WallThickness Mm="3" />
    </CylindricalTank>
  </Tank>
</SubPartGameData>
```

### Spherical Tank (Radial Half, with Tank Id)
```xml
<SubPartGameData Id="CoreFuelTankA_Subpart_RadialMPHalfWA">
  <Tank Id="Tank1">
    <SphericalTank>
      <Material Id="Aluminum.2014(s)" />
      <OuterRadius M="0.5" />
      <WallThickness Mm="4" />
    </SphericalTank>
  </Tank>
</SubPartGameData>
```

### Spherical Tank (KittenBackPack, with CustomMass sibling)
```xml
<SubPartGameData Id="KittenBackPackSubPart">
  <CustomMass>
    <LocationBody Z="-0.11692" />
    <Mass Kg="50" />
    <MassSpecificInertia Ixx="0.0256970833" Iyy="0.0231508333" Izz="0.0099664166" />
  </CustomMass>
  <Tank>
    <SphericalTank>
      <Material Id="Aluminum.2014(s)" />
      <OuterRadius M="0.276" />
      <WallThickness Mm="4" />
    </SphericalTank>
  </Tank>
</SubPartGameData>
```

### User-Verified Working PartGameData
```xml
<PartGameData Id="RansomTank1A" DisplayName="RansomTankOne">
  <EditorTag Value="Fuel Tanks" />
  <CustomMass>
    <Mass Kg="100" />
  </CustomMass>
  <Tank>
    <CylindricalTank>
      <Material Id="Aluminum.2014(s)" />
      <Length M="3" />
      <OuterRadius M="4" />
      <WallThickness Mm="4" />
    </CylindricalTank>
  </Tank>
  <Connector Id="_connector1" />
  <Connector Id="_connector2" />
</PartGameData>
```

### Summary: Valid CylindricalTank Child Elements
Only these elements appear inside `<CylindricalTank>` in KSA data:
```
<Material Id="..." />      ← required, attribute is "Id"
<Length M="..." />          ← required, meters
<OuterRadius M="..." />    ← required, meters
<WallThickness Mm="..." /> ← required, millimeters
```

### Summary: Valid SphericalTank Child Elements
Only these elements appear inside `<SphericalTank>` in KSA data:
```
<Material Id="..." />      ← required, attribute is "Id"
<OuterRadius M="..." />    ← required, meters
<WallThickness Mm="..." /> ← required, millimeters
```

---

## Files to Change

All files are under `space-tape.lib/`:

| # | File | Change Description |
|---|---|---|
| 1 | `GameDataModels.cs` | Remove bogus fields from `TankState` |
| 2 | `GameDataXmlSerializer.cs` | Add `<Tank>` wrapper; remove bad elements; fix Material attribute |
| 3 | `GameDataEditorUi.cs` | Remove UI controls for removed fields |
| 4 | `PartImporter.cs` | Remove reads of removed fields from template import |
| 5 | `HotReloadSpike.cs` | Remove setting removed fields on PartTemplate |
| 6 | `PartModWriter.cs` | Parse `<Tank>` wrapper; remove bad element parsing; fix Material attribute |

---

## Task 1: Remove Bogus Fields from TankState

**File:** `space-tape.lib/GameDataModels.cs`

### Current TankState (lines 9–22)
```csharp
public sealed class TankState
{
    public TankShape Shape { get; set; } = TankShape.Cylindrical;
    public double3 LocationAsmb { get; set; } = double3.Zero;
    public double3 Paf2Asmb { get; set; } = double3.Zero;
    public double? WallMassKg { get; set; }
    public double WallDensityKgPerM3 { get; set; } = 1400.0;
    public string WallMaterialId { get; set; } = "";
    public double LengthM { get; set; } = 2.0;
    public double OuterRadiusM { get; set; } = 0.5;
    public double WallThicknessMm { get; set; } = 2.0;
    public double DomeHeightFraction { get; set; } = 0.5;
    // ... Clone()
}
```

### Target TankState
```csharp
public sealed class TankState
{
    public TankShape Shape { get; set; } = TankShape.Cylindrical;
    public string WallMaterialId { get; set; } = "Aluminum.2014(s)";
    public double LengthM { get; set; } = 2.0;
    public double OuterRadiusM { get; set; } = 0.5;
    public double WallThicknessMm { get; set; } = 2.0;

    public TankState Clone() => new()
    {
        Shape = Shape,
        WallMaterialId = WallMaterialId,
        LengthM = LengthM,
        OuterRadiusM = OuterRadiusM,
        WallThicknessMm = WallThicknessMm,
    };
}
```

### Fields REMOVED (with rationale)

| Removed Field | Reason |
|---|---|
| `LocationAsmb` | Never appears in KSA game data XML; defaults to zero in C# |
| `Paf2Asmb` | Never appears in KSA game data XML; defaults to zero in C# |
| `WallMassKg` | Never appears inside tank elements in KSA game data XML |
| `WallDensityKgPerM3` | Never appears in KSA game data XML; game derives from Material |
| `DomeHeightFraction` | Never appears in KSA game data XML; C# default handles it |

### Fields CHANGED

| Field | Change |
|---|---|
| `WallMaterialId` default | Changed from `""` to `"Aluminum.2014(s)"` — the material used in every KSA tank definition. A sensible default for new tanks. |

### Clone() method
Update to only clone the remaining fields (shown above).

---

## Task 2: Fix GameData XML Serializer

**File:** `space-tape.lib/GameDataXmlSerializer.cs`

### 2a: Wrap tank in `<Tank>` element

In `SerializeGameData()` (line 28), the current code calls:
```csharp
if (gameData.Tank != null)
    el.Add(SerializeTank(gameData.Tank));
```

Change to wrap the return value in a `<Tank>` element:
```csharp
if (gameData.Tank != null)
    el.Add(new XElement("Tank", SerializeTank(gameData.Tank)));
```

This produces:
```xml
<Tank>
  <CylindricalTank>...</CylindricalTank>
</Tank>
```

### 2b: Rewrite SerializeTank() to only emit valid elements

Replace the entire `SerializeTank()` method (lines 57–82).

**Current SerializeTank (WRONG):**
```csharp
private static XElement SerializeTank(TankState tank)
{
    string elName = tank.Shape == TankShape.Cylindrical ? "CylindricalTank" : "SphericalTank";
    var el = new XElement(elName);

    el.Add(SerializeVector3Element("LocationAsmb", tank.LocationAsmb));
    el.Add(SerializeVector3Element("Paf2Asmb", tank.Paf2Asmb));

    if (tank.WallMassKg.HasValue)
        el.Add(new XElement("Mass", new XAttribute("Kg", tank.WallMassKg.Value.ToString("G6"))));
    el.Add(new XElement("Density", new XAttribute("KgPerM3", tank.WallDensityKgPerM3.ToString("G6"))));
    if (!string.IsNullOrWhiteSpace(tank.WallMaterialId))
        el.Add(new XElement("Material", new XAttribute("Value", tank.WallMaterialId)));

    el.Add(new XElement("OuterRadius", new XAttribute("M", tank.OuterRadiusM.ToString("G6"))));
    el.Add(new XElement("WallThickness", new XAttribute("Mm", tank.WallThicknessMm.ToString("G6"))));

    if (tank.Shape == TankShape.Cylindrical)
    {
        el.Add(new XElement("Length", new XAttribute("M", tank.LengthM.ToString("G6"))));
        el.Add(new XElement("DomeHeightFraction", new XAttribute("Value", tank.DomeHeightFraction.ToString("G6"))));
    }

    return el;
}
```

**Target SerializeTank (CORRECT):**
```csharp
private static XElement SerializeTank(TankState tank)
{
    string elName = tank.Shape == TankShape.Cylindrical ? "CylindricalTank" : "SphericalTank";
    var el = new XElement(elName);

    if (!string.IsNullOrWhiteSpace(tank.WallMaterialId))
        el.Add(new XElement("Material", new XAttribute("Id", tank.WallMaterialId)));

    if (tank.Shape == TankShape.Cylindrical)
        el.Add(new XElement("Length", new XAttribute("M", tank.LengthM.ToString("G6"))));

    el.Add(new XElement("OuterRadius", new XAttribute("M", tank.OuterRadiusM.ToString("G6"))));
    el.Add(new XElement("WallThickness", new XAttribute("Mm", tank.WallThicknessMm.ToString("G6"))));

    return el;
}
```

### Changes in SerializeTank:

| Change | Detail |
|---|---|
| REMOVED `LocationAsmb` element | Never in KSA data |
| REMOVED `Paf2Asmb` element | Never in KSA data |
| REMOVED `Mass` element | Never inside tank in KSA data |
| REMOVED `Density` element | Never in KSA data |
| REMOVED `DomeHeightFraction` element | Never in KSA data |
| FIXED `Material` attribute | `Value` → `Id` to match `SerializedReference.Id` |
| REORDERED elements | Material first, then Length (cyl only), OuterRadius, WallThickness — matches KSA ordering |

### 2c: Remove unused helper

The `SerializeVector3Element` method (lines 84–91) is only used by `SerializeTank`. After removing `LocationAsmb` and `Paf2Asmb` serialization, check if it's used elsewhere. If not, **remove it** to avoid dead code.

---

## Task 3: Remove UI Controls for Removed Fields

**File:** `space-tape.lib/GameDataEditorUi.cs`

### Current RenderTankSection fields (lines 27–100)
The method currently renders UI for all these fields:
1. Enable Tank checkbox ✓ keep
2. Shape combo ✓ keep
3. Length (m) — Cylindrical only ✓ keep
4. Dome Frac — **REMOVE** (field removed from TankState)
5. Outer Radius (m) ✓ keep
6. Wall Thick (mm) ✓ keep
7. Density (kg/m³) — **REMOVE** (field removed from TankState)
8. Wall Mass (kg) — **REMOVE** (field removed from TankState)
9. Tank Position (m) 3-drag — **REMOVE** (field removed from TankState)
10. Tank Rotation (rad) 3-drag — **REMOVE** (field removed from TankState)

### Add Material input
Add a text input for `WallMaterialId` in the table, after the Shape combo. Example:

```csharp
// Material input
ImGui.TableNextRow();
ImGui.TableNextColumn();
ImGui.Text("Material");
ImGui.TableNextColumn();
// Use ImInputString or InputText for the material ID
```

### UI to REMOVE
Remove these blocks from the table:
- `DomeHeightFraction` / "Dome Frac" row
- `WallDensityKgPerM3` / "Density (kg/m³)" row
- `WallMassKg` / "Wall Mass (kg)" row

Remove these blocks after the table:
- "Tank Position (m)" — entire `TextDisabled` + `Drag3` block for `LocationAsmb`
- "Tank Rotation (rad)" — entire `TextDisabled` + `Drag3` block for `Paf2Asmb`

### Target UI layout after changes:
```
[x] Enable Tank
Shape:    [Cylindrical ▼]
Material:        [Aluminum.2014(s)    ]
Length (m):      [ 2.0    ]            ← Cylindrical only
Outer Radius (m):[ 0.5    ]
Wall Thick (mm): [ 2.0    ]
```

---

## Task 4: Fix PartImporter Tank Reading

**File:** `space-tape.lib/PartImporter.cs`

### Current ImportTank method (lines 131–157)
```csharp
private static TankState ImportTank(AsmbTankTemplate tank)
{
    var state = new TankState
    {
        LocationAsmb = tank.LocationAsmb.ToDouble3(),
        Paf2Asmb = tank.Paf2Asmb.ToDouble3(),
        WallDensityKgPerM3 = (double)tank.Density,
        WallMaterialId = tank.Material?.Id ?? "",
    };

    if (tank.Mass.IsValid())
        state.WallMassKg = (double)tank.Mass;

    if (tank is CylindricalTankTemplate cyl)
    {
        state.Shape = TankShape.Cylindrical;
        state.LengthM = (double)cyl.Length;
        state.OuterRadiusM = (double)cyl.OuterRadius;
        state.WallThicknessMm = (double)cyl.WallThickness * 1000.0;
        state.DomeHeightFraction = cyl.DomeHeightFraction;
    }
    else if (tank is SphericalTankTemplate sph)
    {
        state.Shape = TankShape.Spherical;
        state.OuterRadiusM = (double)sph.OuterRadius;
        state.WallThicknessMm = (double)sph.WallThickness * 1000.0;
    }

    return state;
}
```

### Target ImportTank
```csharp
private static TankState ImportTank(AsmbTankTemplate tank)
{
    var state = new TankState
    {
        WallMaterialId = tank.Material?.Id ?? "Aluminum.2014(s)",
    };

    if (tank is CylindricalTankTemplate cyl)
    {
        state.Shape = TankShape.Cylindrical;
        state.LengthM = (double)cyl.Length;
        state.OuterRadiusM = (double)cyl.OuterRadius;
        state.WallThicknessMm = (double)cyl.WallThickness * 1000.0;
    }
    else if (tank is SphericalTankTemplate sph)
    {
        state.Shape = TankShape.Spherical;
        state.OuterRadiusM = (double)sph.OuterRadius;
        state.WallThicknessMm = (double)sph.WallThickness * 1000.0;
    }

    return state;
}
```

### Changes:
- REMOVED: `LocationAsmb`, `Paf2Asmb`, `WallDensityKgPerM3` reads from template
- REMOVED: `WallMassKg` conditional read
- REMOVED: `DomeHeightFraction` read
- CHANGED: `WallMaterialId` fallback from `""` to `"Aluminum.2014(s)"` (default if template material is null)

---

## Task 5: Fix HotReloadSpike Template Building

**File:** `space-tape.lib/HotReloadSpike.cs`

### Current tank section in BuildTemplate (around lines 133–165)
```csharp
if (gd.Tank.Shape == TankShape.Cylindrical)
{
    template.Tank = new CylindricalTankTemplate
    {
        LocationAsmb = new Vector3Reference(gd.Tank.LocationAsmb),
        Paf2Asmb = new Vector3Reference(gd.Tank.Paf2Asmb),
        Density = new DensityReference(gd.Tank.WallDensityKgPerM3),
        Length = new DistanceReference(gd.Tank.LengthM),
        OuterRadius = new DistanceReference(gd.Tank.OuterRadiusM),
        WallThickness = new DistanceReference(gd.Tank.WallThicknessMm / 1000.0),
        DomeHeightFraction = gd.Tank.DomeHeightFraction,
    };
}
else
{
    template.Tank = new SphericalTankTemplate
    {
        LocationAsmb = new Vector3Reference(gd.Tank.LocationAsmb),
        Paf2Asmb = new Vector3Reference(gd.Tank.Paf2Asmb),
        Density = new DensityReference(gd.Tank.WallDensityKgPerM3),
        OuterRadius = new DistanceReference(gd.Tank.OuterRadiusM),
        WallThickness = new DistanceReference(gd.Tank.WallThicknessMm / 1000.0),
    };
}
if (gd.Tank.WallMassKg.HasValue)
    template.Tank.Mass = new MassReference(gd.Tank.WallMassKg.Value);
if (!string.IsNullOrWhiteSpace(gd.Tank.WallMaterialId))
    template.Tank.Material = new SerializedReference(gd.Tank.WallMaterialId);
```

### Target tank section
```csharp
if (gd.Tank.Shape == TankShape.Cylindrical)
{
    template.Tank = new CylindricalTankTemplate
    {
        Length = new DistanceReference(gd.Tank.LengthM),
        OuterRadius = new DistanceReference(gd.Tank.OuterRadiusM),
        WallThickness = new DistanceReference(gd.Tank.WallThicknessMm / 1000.0),
    };
}
else
{
    template.Tank = new SphericalTankTemplate
    {
        OuterRadius = new DistanceReference(gd.Tank.OuterRadiusM),
        WallThickness = new DistanceReference(gd.Tank.WallThicknessMm / 1000.0),
    };
}
if (!string.IsNullOrWhiteSpace(gd.Tank.WallMaterialId))
    template.Tank.Material = new SerializedReference(gd.Tank.WallMaterialId);
```

### Changes:
- REMOVED: `LocationAsmb` assignment (both Cylindrical and Spherical)
- REMOVED: `Paf2Asmb` assignment (both Cylindrical and Spherical)
- REMOVED: `Density` assignment (both Cylindrical and Spherical)
- REMOVED: `DomeHeightFraction` assignment (Cylindrical)
- REMOVED: `WallMassKg` conditional assignment block
- KEPT: `Material` assignment (with `Id` via `SerializedReference`)

---

## Task 6: Fix PartModWriter XML Parsing

**File:** `space-tape.lib/PartModWriter.cs`

### 6a: Parse `<Tank>` wrapper element

In `LoadGameData()` method (around lines 239–270), the tank parsing currently looks for `<CylindricalTank>` and `<SphericalTank>` as direct children of `<PartGameData>`:

```csharp
var cylEl = gdEl.Element("CylindricalTank");
var sphEl = gdEl.Element("SphericalTank");
var tankEl = cylEl ?? sphEl;
```

Change to first look inside a `<Tank>` wrapper, with fallback to direct children for backward compatibility:

```csharp
// Look for <Tank> wrapper first (correct KSA format), then fall back to direct children
var tankWrapper = gdEl.Element("Tank");
var cylEl = tankWrapper?.Element("CylindricalTank") ?? gdEl.Element("CylindricalTank");
var sphEl = tankWrapper?.Element("SphericalTank") ?? gdEl.Element("SphericalTank");
var tankEl = cylEl ?? sphEl;
```

### 6b: Remove parsing of removed fields

Inside the tank parsing block (after `if (tankEl != null)`), remove these lines:

```csharp
// REMOVE these lines:
tank.LocationAsmb = ParseVector3(tankEl.Element("LocationAsmb"), double3.Zero);
tank.Paf2Asmb = ParseVector3(tankEl.Element("Paf2Asmb"), double3.Zero);
if (TryParseDouble(tankEl.Element("Mass"), "Kg", out double wallMass))
    tank.WallMassKg = wallMass;
if (TryParseDouble(tankEl.Element("Density"), "KgPerM3", out double density))
    tank.WallDensityKgPerM3 = density;
// ... and the DomeHeightFraction line inside the cylEl block
```

### 6c: Fix Material attribute name

Change:
```csharp
tank.WallMaterialId = tankEl.Element("Material")?.Attribute("Value")?.Value ?? "";
```
To:
```csharp
tank.WallMaterialId = tankEl.Element("Material")?.Attribute("Id")?.Value
                    ?? tankEl.Element("Material")?.Attribute("Value")?.Value
                    ?? "Aluminum.2014(s)";
```

The fallback to `Value` attribute provides backward compatibility with previously saved files. The fallback to `"Aluminum.2014(s)"` provides a sensible default if no material is specified.

### 6d: Remove DomeHeightFraction parsing

Inside the `if (cylEl != null)` block, remove:
```csharp
if (TryParseDouble(tankEl.Element("DomeHeightFraction"), "Value", out double dome))
    tank.DomeHeightFraction = dome;
```

### Target tank parsing block:
```csharp
var tankWrapper = gdEl.Element("Tank");
var cylEl = tankWrapper?.Element("CylindricalTank") ?? gdEl.Element("CylindricalTank");
var sphEl = tankWrapper?.Element("SphericalTank") ?? gdEl.Element("SphericalTank");
var tankEl = cylEl ?? sphEl;
if (tankEl != null)
{
    var tank = new TankState
    {
        Shape = cylEl != null ? TankShape.Cylindrical : TankShape.Spherical,
    };
    tank.WallMaterialId = tankEl.Element("Material")?.Attribute("Id")?.Value
                        ?? tankEl.Element("Material")?.Attribute("Value")?.Value
                        ?? "Aluminum.2014(s)";
    if (TryParseDouble(tankEl.Element("OuterRadius"), "M", out double outerR))
        tank.OuterRadiusM = outerR;
    if (TryParseDouble(tankEl.Element("WallThickness"), "Mm", out double wallMm))
        tank.WallThicknessMm = wallMm;
    if (cylEl != null)
    {
        if (TryParseDouble(tankEl.Element("Length"), "M", out double length))
            tank.LengthM = length;
    }
    part.GameData.Tank = tank;
}
```

---

## Task 7: Verify No Other References to Removed Fields

After making the above changes, search the entire `space-tape.lib/` directory for any remaining references to the removed field names. These should produce zero results:

```
grep -r "LocationAsmb\|Paf2Asmb\|WallDensityKgPerM3\|WallMassKg\|DomeHeightFraction" space-tape.lib/
```

Also check `space-tape/Mod.cs` (the standalone mod entry) in case it references any TankState fields directly (unlikely but verify).

---

## Task 8: Build and Verify

Run `dotnet build` from the repo root to ensure all changes compile cleanly. Fix any compilation errors arising from removed fields.

Expected errors if any reference was missed:
- `CS1061`: 'TankState' does not contain a definition for 'LocationAsmb' (etc.)

These errors will point directly to any remaining code that needs updating.

---

## Execution Order

Tasks should be executed in this order:

1. **Task 1** — Remove fields from `TankState` (this will cause compile errors in all dependent code)
2. **Task 2** — Fix `GameDataXmlSerializer.cs`
3. **Task 3** — Fix `GameDataEditorUi.cs`
4. **Task 4** — Fix `PartImporter.cs`
5. **Task 5** — Fix `HotReloadSpike.cs`
6. **Task 6** — Fix `PartModWriter.cs`
7. **Task 7** — Verify no remaining references
8. **Task 8** — Build and verify compilation

Tasks 2–6 can be done in any order after Task 1, but all must be complete before Task 7/8.

---

## Backward Compatibility Notes

- **PartModWriter.LoadGameData**: The `<Tank>` wrapper lookup falls back to direct children, so previously-saved XML files without the wrapper will still load.
- **Material attribute**: Falls back from `Id` → `Value` → default, so previously-saved files with `Value` attribute will still load.
- **Removed fields**: Previously-saved XML files that contain `<LocationAsmb>`, `<Paf2Asmb>`, `<Density>`, `<DomeHeightFraction>`, or `<Mass>` inside tank elements will have those elements silently ignored on re-load (they'll exist in the XML file but won't be parsed into TankState). This is acceptable — the data was incorrect anyway.

---

## Also Update SPACE_TAPE_FROM_PARTS_TANKS_PLAN.md

The original plan (`plans/SPACE_TAPE_FROM_PARTS_TANKS_PLAN.md`) contains incorrect XML examples and TankState definitions. After this fix is implemented, update that document's:
- XML structure examples to include `<Tank>` wrapper
- XML element list to remove LocationAsmb, Paf2Asmb, Density, DomeHeightFraction
- Material attribute from `Value` to `Id`
- TankState code samples to remove the bogus fields
