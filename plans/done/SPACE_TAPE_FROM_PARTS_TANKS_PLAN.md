# Space Tape: Import From Parts, Tanks, & Connectors — Implementation Plan

## Overview

Expand space-tape's part editor so it can:

1. **Import from existing game parts** — combobox to select any Part from ModLibrary, deep-clone all fields into the editor
2. **Fuel tanks** — define CylindricalTank or SphericalTank GameData with full material/density/mass configuration
3. **Connectors** — define attachment points with position, rotation, scale, and flag types (Internal/ToSurface/FromSurface)
4. **Expanded GameData** — add Decoupler, DockingPort, EVADoor, PowerConsumer, and multiple Battery/Generator support

Engine-related features (Combustors, Nozzles, Rockets, RocketControllers) are deferred to Phase 2.

---

## KSA Reference Architecture

### PartTemplate fields (decomp/ksa/KSA/PartTemplate.cs)

```
SubPartInstances: List<PartInstance>          — 3D model components (Assets XML)
Connectors: List<Part.Connector.TemplateBase> — attachment points
Tank: AsmbTankTemplate?                       — single tank (CylindricalTank or SphericalTank)
Batteries: List<BatteryTemplate>              — multiple batteries
Generators: List<GeneratorTemplate>           — multiple generators
PowerConsumers: List<PowerConsumerTemplate>    — multiple power consumers
Decoupler: DecouplerTemplate?                 — single decoupler
DockingPort: DockingPortTemplate?             — single docking port
EVADoor: EVADoorTemplate?                     — single EVA door
InertMasses: List<AsmbMassTemplate>           — 11 mass types (CustomMass, etc.)
EditorTagsStrings: List<StringReference>      — editor category tags
RocketEngineControllers, RocketThrusterControllers, Rockets, RocketCores, RocketNozzles — (Phase 2)
```

### Tank inheritance chain

```
AsmbTankTemplate (decomp/ksa/KSA/AsmbTankTemplate.cs)
  └─ AsmbVolumetricMassTemplate (decomp/ksa/KSA/AsmbVolumetricMassTemplate.cs)
       ├─ Mass: MassReference (Kg attr)            — wall mass
       ├─ Density: DensityReference (KgPerM3 attr)  — wall density
       ├─ Material: SerializedReference             — wall material ref
       └─ AsmbTransformTemplate (decomp/ksa/KSA/AsmbTransformTemplate.cs)
            ├─ LocationAsmb: Vector3Reference (X,Y,Z)  — position offset
            └─ Paf2Asmb: Vector3Reference (X,Y,Z)      — rotation (euler radians)

CylindricalTankTemplate (decomp/ksa/KSA/CylindricalTankTemplate.cs)
  ├─ Length: DistanceReference (M or Mm)
  ├─ OuterRadius: DistanceReference
  ├─ WallThickness: DistanceReference
  └─ DomeHeightFraction: DoubleReference (0.0–1.0, default 0.5)

SphericalTankTemplate (decomp/ksa/KSA/SphericalTankTemplate.cs)
  ├─ OuterRadius: DistanceReference
  └─ WallThickness: DistanceReference
```

### Connector system (decomp/ksa/KSA/Part.cs lines 32–133)

```
Part.Connector.TemplateBase:
  Id: string (XmlAttribute)
  Transform: TransformReference (Position + Rotation + Scale in assembly space)
  Flags: Part.Connector.Flag enum
    Internal = 1   (stack attachment)
    ToSurface = 2  (radial: this connector attaches TO another part's surface)
    FromSurface = 4 (radial: other parts attach FROM this part's surface)
```

Connectors live in BOTH Assets XML (geometry position) and GameData XML (flags + overrides).
When GameData has a connector with the same Id as Assets, flags are OR'd together (PartTemplate.ApplyGameData lines 204–233).

### Decoupler / DockingPort / EVADoor

```
DecouplerTemplate (decomp/ksa/KSA/DecouplerTemplate.cs):
  ConnectorId: string  — references a Connector.Id
  Force: float         — separation force in Newtons

DockingPortTemplate (decomp/ksa/KSA/DockingPortTemplate.cs):
  ConnectorId: string  — references a Connector.Id
  Force: float         — capture/undock force

EVADoorTemplate (decomp/ksa/KSA/EVADoorTemplate.cs):
  ConnectorId: string  — references a Connector.Id
```

### Battery / Generator / PowerConsumer

```
BatteryTemplate (decomp/ksa/KSA/BatteryTemplate.cs):
  MaximumCapacity: JoulesReference (KWh attr)

GeneratorTemplate (decomp/ksa/KSA/GeneratorTemplate.cs):
  Produced: WattsReference (W attr)

PowerConsumerTemplate (decomp/ksa/KSA/PowerConsumerTemplate.cs):
  Consumed: WattsReference (W attr)
```

### XML structure examples

**Assets XML (Part definition with connectors):**
```xml
<Assets>
  <Part Id="MyMod.MyTank">
    <SubPart Id="body_1" InstanceOf="Core.TankBody.A">
      <Transform><Position X="0" Y="0" Z="0"/></Transform>
    </SubPart>
    <Connector Id="top">
      <Transform><Position X="0" Y="1.5" Z="0"/></Transform>
      <Flags>Internal</Flags>
    </Connector>
    <Connector Id="bottom">
      <Transform><Position X="0" Y="-1.5" Z="0"/></Transform>
      <Flags>Internal</Flags>
    </Connector>
  </Part>
</Assets>
```

**GameData XML (adds tank, mass, battery, and connector flag overrides):**
```xml
<Assets>
  <PartGameData Id="MyMod.MyTank" DisplayName="My Fuel Tank">
    <EditorTag Value="Propulsion"/>
    <CylindricalTank>
      <LocationAsmb X="0" Y="0" Z="0"/>
      <Density KgPerM3="1400"/>
      <Length M="2.0"/>
      <OuterRadius M="0.5"/>
      <WallThickness Mm="2"/>
      <DomeHeightFraction Value="0.5"/>
    </CylindricalTank>
    <CustomMass><Mass Kg="5.0"/></CustomMass>
    <Battery><MaximumCapacity KWh="0.01"/></Battery>
    <Generator><Produced W="5.0"/></Generator>
    <PowerConsumer><Consumed W="2.0"/></PowerConsumer>
    <Connector Id="top"><Flags>Internal</Flags></Connector>
    <Connector Id="bottom"><Flags>Internal</Flags></Connector>
    <Decoupler ConnectorId="bottom" Force="500"/>
  </PartGameData>
</Assets>
```

---

## Task Breakdown

### Task 1: Expand PartGameDataState with new fields

**File:** `space-tape.lib/PartEditorState.cs`

Add the following new state model classes and properties to support the full GameData scope.

#### 1a. New state classes

Create these new classes (can be in a new file `GameDataModels.cs` if `PartEditorState.cs` gets too large):

```csharp
/// <summary>Tank shape type.</summary>
public enum TankShape { Cylindrical, Spherical }

/// <summary>Fuel tank definition state.</summary>
public sealed class TankState
{
    public TankShape Shape { get; set; } = TankShape.Cylindrical;

    // Shared (AsmbVolumetricMassTemplate fields)
    public double3 LocationAsmb { get; set; } = double3.Zero;
    public double3 Paf2Asmb { get; set; } = double3.Zero;    // euler radians
    public double? WallMassKg { get; set; }                   // MassReference.Kg
    public double WallDensityKgPerM3 { get; set; } = 1400.0;  // DensityReference.KgPerM3
    public string WallMaterialId { get; set; } = "";           // SerializedReference (material)

    // Cylindrical-specific
    public double LengthM { get; set; } = 2.0;                // DistanceReference.M
    public double OuterRadiusM { get; set; } = 0.5;           // DistanceReference.M
    public double WallThicknessMm { get; set; } = 2.0;        // DistanceReference.Mm
    public double DomeHeightFraction { get; set; } = 0.5;     // DoubleReference (0..1)

    // Spherical uses OuterRadiusM and WallThicknessMm above (shared names)

    public TankState Clone() => new()
    {
        Shape = Shape,
        LocationAsmb = LocationAsmb,
        Paf2Asmb = Paf2Asmb,
        WallMassKg = WallMassKg,
        WallDensityKgPerM3 = WallDensityKgPerM3,
        WallMaterialId = WallMaterialId,
        LengthM = LengthM,
        OuterRadiusM = OuterRadiusM,
        WallThicknessMm = WallThicknessMm,
        DomeHeightFraction = DomeHeightFraction
    };
}

/// <summary>Connector attachment point state.</summary>
public sealed class ConnectorState
{
    public string Id { get; set; } = "";
    public double3 Position { get; set; } = double3.Zero;
    public doubleQuat Rotation { get; set; } = doubleQuat.Identity;
    public double3 Scale { get; set; } = double3.One;
    public bool FlagInternal { get; set; }    // Part.Connector.Flag.Internal (stack)
    public bool FlagToSurface { get; set; }   // Part.Connector.Flag.ToSurface (radial out)
    public bool FlagFromSurface { get; set; } // Part.Connector.Flag.FromSurface (radial in)

    public ConnectorState Clone() => new()
    {
        Id = Id,
        Position = Position,
        Rotation = Rotation,
        Scale = Scale,
        FlagInternal = FlagInternal,
        FlagToSurface = FlagToSurface,
        FlagFromSurface = FlagFromSurface
    };
}

/// <summary>Decoupler state.</summary>
public sealed class DecouplerState
{
    public string ConnectorId { get; set; } = "";
    public double Force { get; set; } = 500.0;

    public DecouplerState Clone() => new() { ConnectorId = ConnectorId, Force = Force };
}

/// <summary>Docking port state.</summary>
public sealed class DockingPortState
{
    public string ConnectorId { get; set; } = "";
    public double Force { get; set; } = 500.0;

    public DockingPortState Clone() => new() { ConnectorId = ConnectorId, Force = Force };
}

/// <summary>EVA door state.</summary>
public sealed class EVADoorState
{
    public string ConnectorId { get; set; } = "";

    public EVADoorState Clone() => new() { ConnectorId = ConnectorId };
}

/// <summary>Battery state (multiple allowed per part).</summary>
public sealed class BatteryState
{
    public double CapacityKWh { get; set; } = 0.01;

    public BatteryState Clone() => new() { CapacityKWh = CapacityKWh };
}

/// <summary>Generator state (multiple allowed per part).</summary>
public sealed class GeneratorState
{
    public double OutputWatts { get; set; } = 5.0;

    public GeneratorState Clone() => new() { OutputWatts = OutputWatts };
}

/// <summary>Power consumer state (multiple allowed per part).</summary>
public sealed class PowerConsumerState
{
    public double ConsumedWatts { get; set; } = 2.0;

    public PowerConsumerState Clone() => new() { ConsumedWatts = ConsumedWatts };
}
```

#### 1b. Update PartGameDataState

Replace the single `BatteryCapacity` and `GeneratorOutput` doubles with lists and add new fields:

```csharp
public sealed class PartGameDataState
{
    public string DisplayName { get; set; } = "";
    public List<string> EditorTags { get; set; } = new();

    // Mass
    public double? CustomMass { get; set; }

    // Power (now lists — multiple batteries/generators/consumers per part)
    public List<BatteryState> Batteries { get; set; } = new();
    public List<GeneratorState> Generators { get; set; } = new();
    public List<PowerConsumerState> PowerConsumers { get; set; } = new();

    // Tank (optional, single per part)
    public TankState? Tank { get; set; }

    // Connectors (list)
    public List<ConnectorState> Connectors { get; set; } = new();

    // Coupling
    public DecouplerState? Decoupler { get; set; }
    public DockingPortState? DockingPort { get; set; }
    public EVADoorState? EVADoor { get; set; }
}
```

**Migration note:** The current `BatteryCapacity` (double?) and `GeneratorOutput` (double?) must be replaced with the new `List<BatteryState>` and `List<GeneratorState>`. All callsites that read/write `BatteryCapacity` / `GeneratorOutput` must be updated — there are exactly 4 touchpoints:
- `PartEditorState.cs` — the property definitions (replaced above)
- `PartEditorUi.cs` — RenderGameDataSection reads/writes these (Task 5 updates)
- `GameDataXmlSerializer.cs` — serializes to XML (Task 3 updates)
- `PartModWriter.cs` — LoadGameData() parses from XML (Task 4 updates)
- `EditingPart.Clone()` — must deep-clone new fields (below)
- `HotReloadSpike.cs` — BuildTemplate() maps to BatteryTemplate (Task 6 updates)

#### 1c. Update EditingPart.Clone()

The `Clone()` method (PartEditorState.cs line 63) must deep-clone all new fields:

```csharp
public EditingPart Clone()
{
    var clone = new EditingPart { PartId = PartId };
    clone.GameData.DisplayName = GameData.DisplayName;
    clone.GameData.EditorTags.AddRange(GameData.EditorTags);
    clone.GameData.CustomMass = GameData.CustomMass;

    // Power
    foreach (var b in GameData.Batteries)
        clone.GameData.Batteries.Add(b.Clone());
    foreach (var g in GameData.Generators)
        clone.GameData.Generators.Add(g.Clone());
    foreach (var pc in GameData.PowerConsumers)
        clone.GameData.PowerConsumers.Add(pc.Clone());

    // Tank
    clone.GameData.Tank = GameData.Tank?.Clone();

    // Connectors
    foreach (var c in GameData.Connectors)
        clone.GameData.Connectors.Add(c.Clone());

    // Coupling
    clone.GameData.Decoupler = GameData.Decoupler?.Clone();
    clone.GameData.DockingPort = GameData.DockingPort?.Clone();
    clone.GameData.EVADoor = GameData.EVADoor?.Clone();

    // SubParts
    foreach (var p in Placements)
        clone.Placements.Add(new SubPartPlacement
        {
            InstanceId = p.InstanceId,
            SubPartTemplateId = p.SubPartTemplateId,
            Position = p.Position,
            Rotation = p.Rotation,
            Scale = p.Scale
        });
    return clone;
}
```

---

### Task 2: Import From Existing Part

**New file:** `space-tape.lib/PartCatalog.cs`  
**Modified file:** `space-tape.lib/PartEditorUi.cs`

#### 2a. Build a Part catalog from ModLibrary

Create `PartCatalog.cs` that loads all non-hidden, non-SubPart parts from ModLibrary:

```csharp
public sealed class PartCatalog
{
    public List<(string id, string displayName)> Parts { get; } = new();
    public bool IsLoaded { get; private set; }

    public void Load()
    {
        Parts.Clear();
        // Access ModLibrary.AllParts via reflection (same pattern as SubPartCatalog.cs)
        var allPartsField = typeof(ModLibrary).GetField("AllParts",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (allPartsField?.GetValue(null) is not SerializedCollection<PartTemplate> allParts)
            return;

        foreach (var pt in allParts)
        {
            if (pt.IsSubPart || pt.IsHidden) continue;
            Parts.Add((pt.Id, pt.DisplayName ?? pt.Id));
        }
        Parts.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
        IsLoaded = true;
    }
}
```

Reference: `space-tape.lib/SubPartCatalog.cs` uses the same reflection pattern for SubParts.

#### 2b. Import logic — reading PartTemplate into EditingPart

Create a static method (can be in `PartCatalog.cs` or a new `PartImporter.cs`) that reads a `PartTemplate` from ModLibrary and populates an `EditingPart`:

```csharp
public static class PartImporter
{
    /// <summary>
    /// Deep-reads a PartTemplate from ModLibrary and creates an EditingPart
    /// pre-populated with all SubParts, Connectors, Tank, Batteries, etc.
    /// </summary>
    public static EditingPart? ImportFromTemplate(string partId)
    {
        var template = ModLibrary.Get<PartTemplate>(partId);
        if (template == null) return null;

        var part = new EditingPart
        {
            PartId = partId + ".Custom",  // Avoid ID collision
        };

        // SubParts
        foreach (var sp in template.SubPartInstances)
        {
            part.Placements.Add(new SubPartPlacement
            {
                InstanceId = sp.Id,
                SubPartTemplateId = sp.InstanceOf,
                Position = sp.Transform.PositionValue,
                Rotation = sp.Transform.RotationValue,
                Scale = sp.Transform.ScaleValue,
            });
        }

        var gd = part.GameData;
        gd.DisplayName = template.DisplayName ?? partId;

        // EditorTags
        foreach (var tag in template.EditorTags)
            gd.EditorTags.Add(tag.ToString());

        // Tank
        if (template.Tank != null)
            gd.Tank = ImportTank(template.Tank);

        // Connectors
        foreach (var c in template.Connectors)
            gd.Connectors.Add(ImportConnector(c));

        // Batteries
        foreach (var b in template.Batteries)
            gd.Batteries.Add(new BatteryState { CapacityKWh = (double)b.MaximumCapacity });

        // Generators
        foreach (var g in template.Generators)
            gd.Generators.Add(new GeneratorState { OutputWatts = (double)g.Produced });

        // PowerConsumers
        foreach (var pc in template.PowerConsumers)
            gd.PowerConsumers.Add(new PowerConsumerState { ConsumedWatts = (double)pc.Consumed });

        // Mass — take the first CustomMassTemplate found
        foreach (var mass in template.InertMasses)
        {
            if (mass is CustomMassTemplate cm)
            {
                gd.CustomMass = (double)cm.Mass;
                break;
            }
        }

        // Decoupler
        if (template.Decoupler != null)
            gd.Decoupler = new DecouplerState
            {
                ConnectorId = template.Decoupler.ConnectorId,
                Force = template.Decoupler.Force
            };

        // DockingPort
        if (template.DockingPort != null)
            gd.DockingPort = new DockingPortState
            {
                ConnectorId = template.DockingPort.ConnectorId,
                Force = template.DockingPort.Force
            };

        // EVADoor
        if (template.EVADoor != null)
            gd.EVADoor = new EVADoorState
            {
                ConnectorId = template.EVADoor.ConnectorId
            };

        return part;
    }

    private static TankState ImportTank(AsmbTankTemplate tank)
    {
        var state = new TankState();

        // Common volumetric mass fields (AsmbVolumetricMassTemplate)
        state.LocationAsmb = tank.LocationAsmb;    // Vector3Reference → double3
        state.Paf2Asmb = tank.Paf2Asmb;            // Vector3Reference → double3
        state.WallDensityKgPerM3 = (double)tank.Density;
        state.WallMaterialId = tank.Material?.Id ?? "";
        if (tank.Mass != null)
            state.WallMassKg = (double)tank.Mass;

        if (tank is CylindricalTankTemplate cyl)
        {
            state.Shape = TankShape.Cylindrical;
            state.LengthM = (double)cyl.Length;
            state.OuterRadiusM = (double)cyl.OuterRadius;
            state.WallThicknessMm = (double)cyl.WallThickness * 1000.0; // M → Mm
            state.DomeHeightFraction = (double)cyl.DomeHeightFraction;
        }
        else if (tank is SphericalTankTemplate sph)
        {
            state.Shape = TankShape.Spherical;
            state.OuterRadiusM = (double)sph.OuterRadius;
            state.WallThicknessMm = (double)sph.WallThickness * 1000.0;
        }

        return state;
    }

    private static ConnectorState ImportConnector(Part.Connector.TemplateBase c)
    {
        var flags = c.Flags;
        return new ConnectorState
        {
            Id = c.Id,
            Position = c.Transform.PositionValue,
            Rotation = c.Transform.RotationValue,
            Scale = c.Transform.ScaleValue,
            FlagInternal = flags.HasFlag(Part.Connector.Flag.Internal),
            FlagToSurface = flags.HasFlag(Part.Connector.Flag.ToSurface),
            FlagFromSurface = flags.HasFlag(Part.Connector.Flag.FromSurface),
        };
    }
}
```

**Important KSA reference notes:**
- `JoulesReference` (battery capacity) casts to `double` — see `decomp/ksa/KSA/BatteryTemplate.cs`
- `WattsReference` (generator/consumer) casts to `double` — see `decomp/ksa/KSA/GeneratorTemplate.cs`
- `DistanceReference` casts to `double` (always in meters) — but `WallThickness` is typically in mm in the XML (`Mm` attribute), so when reading from the template it's already in meters and must be converted to mm for display
- `MassReference` casts to `double` (kg) — see `decomp/ksa/KSA/MassReference.cs`
- `DensityReference` casts to `double` (kg/m³) — see `decomp/ksa/KSA/DensityReference.cs`
- The `PartTemplate.EditorTags` field (populated during OnDataLoad) contains `EditorTag` enum values, not strings. Use `tag.ToString()` for display. The `EditorTagsStrings` field is `List<StringReference>` used in XML.
- `AsmbTankTemplate` inherits from `AsmbVolumetricMassTemplate` which inherits from `AsmbTransformTemplate`. Access `LocationAsmb` and `Paf2Asmb` from the transform base; `Mass`, `Density`, `Material` from the volumetric mass base.

#### 2c. Add "Import From Game" UI section

In `PartEditorUi.cs`, add a new collapsible section "Import From Game Part" near the existing "Load Existing Part" section. The UI should:

1. Have a "Load Part List" button that calls `PartCatalog.Load()`
2. Show a filterable combobox (with a text filter input) listing all parts by display name
3. On selection, show a "Import" button
4. On Import, call `PartImporter.ImportFromTemplate()`, then `controller.LoadPart()`, then `scene.SyncParts()`
5. Show a warning: "This will replace the current part in the editor"

UI state to add to `PartEditorUi`:
```csharp
private readonly PartCatalog _gameParts = new();
private int _selectedGamePartIndex = -1;
private readonly ImInputString _gamePartFilter = new ImInputString(128);
private List<int> _filteredGamePartIndices = new();
```

The filter should be a case-insensitive substring match on both Part ID and DisplayName, recomputed each frame (the list is small enough).

---

### Task 3: Expand GameDataXmlSerializer

**File:** `space-tape.lib/GameDataXmlSerializer.cs`

The serializer must now emit all new GameData fields. Add serialization methods for each:

#### 3a. Tank serialization

```csharp
private static XElement? SerializeTank(TankState tank)
{
    string elName = tank.Shape == TankShape.Cylindrical ? "CylindricalTank" : "SphericalTank";
    var el = new XElement(elName);

    // AsmbTransformTemplate fields
    el.Add(SerializeVector3Element("LocationAsmb", tank.LocationAsmb));
    el.Add(SerializeVector3Element("Paf2Asmb", tank.Paf2Asmb));

    // AsmbVolumetricMassTemplate fields
    if (tank.WallMassKg.HasValue)
        el.Add(new XElement("Mass", new XAttribute("Kg", tank.WallMassKg.Value.ToString("G6"))));
    el.Add(new XElement("Density", new XAttribute("KgPerM3", tank.WallDensityKgPerM3.ToString("G6"))));
    if (!string.IsNullOrWhiteSpace(tank.WallMaterialId))
        el.Add(new XElement("Material", new XAttribute("Value", tank.WallMaterialId)));

    // Shape-specific fields
    if (tank.Shape == TankShape.Cylindrical)
    {
        el.Add(new XElement("Length", new XAttribute("M", tank.LengthM.ToString("G6"))));
        el.Add(new XElement("OuterRadius", new XAttribute("M", tank.OuterRadiusM.ToString("G6"))));
        el.Add(new XElement("WallThickness", new XAttribute("Mm", tank.WallThicknessMm.ToString("G6"))));
        el.Add(new XElement("DomeHeightFraction", new XAttribute("Value", tank.DomeHeightFraction.ToString("G6"))));
    }
    else // Spherical
    {
        el.Add(new XElement("OuterRadius", new XAttribute("M", tank.OuterRadiusM.ToString("G6"))));
        el.Add(new XElement("WallThickness", new XAttribute("Mm", tank.WallThicknessMm.ToString("G6"))));
    }

    return el;
}
```

**KSA XML reference:**
- `DistanceReference` reads `M` for meters or `Mm` for millimeters — see `decomp/ksa/KSA/DistanceReference.cs`
- `DomeHeightFraction` is a `DoubleReference` with `Value` attribute — see `decomp/ksa/KSA/CylindricalTankTemplate.cs` line 18
- `DensityReference` reads `KgPerM3` — see `decomp/ksa/KSA/DensityReference.cs`
- `LocationAsmb` / `Paf2Asmb` are `Vector3Reference` with `X`, `Y`, `Z` attributes

#### 3b. Connector serialization (into GameData XML)

Connectors in GameData primarily provide flag overrides. The geometric position comes from the Assets XML. However, for space-tape parts where we define everything, connectors need full definitions in both Assets and GameData:

```csharp
private static XElement SerializeConnector(ConnectorState c)
{
    var el = new XElement("Connector", new XAttribute("Id", c.Id));

    var flags = new List<string>();
    if (c.FlagInternal) flags.Add("Internal");
    if (c.FlagToSurface) flags.Add("ToSurface");
    if (c.FlagFromSurface) flags.Add("FromSurface");
    if (flags.Count > 0)
        el.Add(new XElement("Flags", string.Join(", ", flags)));

    return el;
}
```

**Note:** Connectors also need serialization into the Assets XML (the `<Part>` element) — see Task 3e.

#### 3c. Decoupler / DockingPort / EVADoor serialization

```csharp
private static XElement? SerializeDecoupler(DecouplerState d)
    => new XElement("Decoupler",
        new XAttribute("ConnectorId", d.ConnectorId),
        new XAttribute("Force", d.Force.ToString("G6")));

private static XElement? SerializeDockingPort(DockingPortState dp)
    => new XElement("DockingPort",
        new XAttribute("ConnectorId", dp.ConnectorId),
        new XAttribute("Force", dp.Force.ToString("G6")));

private static XElement? SerializeEVADoor(EVADoorState e)
    => new XElement("EVADoor",
        new XAttribute("ConnectorId", e.ConnectorId));
```

**KSA XML reference:**
- `DecouplerTemplate`: `ConnectorId` and `Force` are `[XmlAttribute]` — see `decomp/ksa/KSA/DecouplerTemplate.cs`
- `DockingPortTemplate`: same pattern — see `decomp/ksa/KSA/DockingPortTemplate.cs`
- `EVADoorTemplate`: only `ConnectorId` — see `decomp/ksa/KSA/EVADoorTemplate.cs`

#### 3d. Updated SerializeGameData method

The main `SerializeGameData` method must be expanded to emit all fields:

```csharp
public static XElement SerializeGameData(string partId, PartGameDataState gameData)
{
    var el = new XElement("PartGameData", new XAttribute("Id", partId));

    if (!string.IsNullOrWhiteSpace(gameData.DisplayName))
        el.Add(new XAttribute("DisplayName", gameData.DisplayName));

    foreach (var tag in gameData.EditorTags)
        if (!string.IsNullOrWhiteSpace(tag))
            el.Add(new XElement("EditorTag", new XAttribute("Value", tag)));

    // Mass
    if (gameData.CustomMass.HasValue && gameData.CustomMass.Value > 0)
        el.Add(new XElement("CustomMass",
            new XElement("Mass", new XAttribute("Kg", gameData.CustomMass.Value.ToString("G6")))));

    // Tank
    if (gameData.Tank != null)
        el.Add(SerializeTank(gameData.Tank));

    // Power
    foreach (var b in gameData.Batteries)
        el.Add(new XElement("Battery",
            new XElement("MaximumCapacity", new XAttribute("KWh", b.CapacityKWh.ToString("G6")))));

    foreach (var g in gameData.Generators)
        el.Add(new XElement("Generator",
            new XElement("Produced", new XAttribute("W", g.OutputWatts.ToString("G6")))));

    foreach (var pc in gameData.PowerConsumers)
        el.Add(new XElement("PowerConsumer",
            new XElement("Consumed", new XAttribute("W", pc.ConsumedWatts.ToString("G6")))));

    // Connectors (flags into GameData)
    foreach (var c in gameData.Connectors)
        el.Add(SerializeConnector(c));

    // Coupling
    if (gameData.Decoupler != null)
        el.Add(SerializeDecoupler(gameData.Decoupler));
    if (gameData.DockingPort != null)
        el.Add(SerializeDockingPort(gameData.DockingPort));
    if (gameData.EVADoor != null)
        el.Add(SerializeEVADoor(gameData.EVADoor));

    return el;
}
```

#### 3e. Update PartXmlSerializer to include Connectors in Assets XML

**File:** `space-tape.lib/PartXmlSerializer.cs`

Connectors must also be written into the `<Part>` element in the Assets XML (position, rotation, scale — the geometric data):

Update `SerializePart()` to append connector elements after SubPart elements:

```csharp
public static XElement SerializePart(EditingPart part)
{
    var partEl = new XElement("Part", new XAttribute("Id", part.PartId));

    // SubParts (existing)
    foreach (var placement in part.Placements)
    {
        // ... existing SubPart serialization ...
    }

    // Connectors — geometric position goes in Assets XML
    foreach (var c in part.GameData.Connectors)
    {
        var connEl = new XElement("Connector", new XAttribute("Id", c.Id));
        var transformEl = SerializeTransform(c.Position, c.Rotation, c.Scale);
        if (transformEl != null)
            connEl.Add(transformEl);

        // Flags also go here (they get OR'd with GameData flags during ApplyGameData)
        var flags = new List<string>();
        if (c.FlagInternal) flags.Add("Internal");
        if (c.FlagToSurface) flags.Add("ToSurface");
        if (c.FlagFromSurface) flags.Add("FromSurface");
        if (flags.Count > 0)
            connEl.Add(new XElement("Flags", string.Join(", ", flags)));

        partEl.Add(connEl);
    }

    return partEl;
}
```

---

### Task 4: Expand PartModWriter.LoadGameData()

**File:** `space-tape.lib/PartModWriter.cs`

The `LoadGameData()` method (line 200) currently only parses CustomMass, Battery, and Generator. Expand to parse all new fields.

#### 4a. Tank parsing

```csharp
// CylindricalTank or SphericalTank
var cylEl = gdEl.Element("CylindricalTank");
var sphEl = gdEl.Element("SphericalTank");
var tankEl = cylEl ?? sphEl;
if (tankEl != null)
{
    var tank = new TankState
    {
        Shape = cylEl != null ? TankShape.Cylindrical : TankShape.Spherical,
    };

    // AsmbTransformTemplate
    tank.LocationAsmb = ParseVector3(tankEl.Element("LocationAsmb"), double3.Zero);
    tank.Paf2Asmb = ParseVector3(tankEl.Element("Paf2Asmb"), double3.Zero);

    // AsmbVolumetricMassTemplate
    if (TryParseDouble(tankEl.Element("Mass"), "Kg", out double wallMass))
        tank.WallMassKg = wallMass;
    if (TryParseDouble(tankEl.Element("Density"), "KgPerM3", out double density))
        tank.WallDensityKgPerM3 = density;
    tank.WallMaterialId = tankEl.Element("Material")?.Attribute("Value")?.Value ?? "";

    // Shape-specific
    if (TryParseDouble(tankEl.Element("OuterRadius"), "M", out double outerR))
        tank.OuterRadiusM = outerR;
    if (TryParseDouble(tankEl.Element("WallThickness"), "Mm", out double wallMm))
        tank.WallThicknessMm = wallMm;
    if (cylEl != null)
    {
        if (TryParseDouble(tankEl.Element("Length"), "M", out double length))
            tank.LengthM = length;
        if (TryParseDouble(tankEl.Element("DomeHeightFraction"), "Value", out double dome))
            tank.DomeHeightFraction = dome;
    }

    part.GameData.Tank = tank;
}
```

Add helper:
```csharp
private static bool TryParseDouble(XElement? el, string attrName, out double value)
{
    value = 0;
    return el != null && double.TryParse(
        el.Attribute(attrName)?.Value,
        NumberStyles.Any, CultureInfo.InvariantCulture, out value);
}
```

#### 4b. Multiple Battery/Generator/PowerConsumer parsing

Replace the single-battery/generator parsing with list-based parsing:

```csharp
// Batteries (multiple)
foreach (var battEl in gdEl.Elements("Battery"))
{
    var capEl = battEl.Element("MaximumCapacity");
    if (capEl != null && TryParseDouble(capEl, "KWh", out double kwh))
        part.GameData.Batteries.Add(new BatteryState { CapacityKWh = kwh });
}

// Generators (multiple)
foreach (var genEl in gdEl.Elements("Generator"))
{
    var prodEl = genEl.Element("Produced");
    if (prodEl != null && TryParseDouble(prodEl, "W", out double watts))
        part.GameData.Generators.Add(new GeneratorState { OutputWatts = watts });
}

// PowerConsumers (multiple)
foreach (var pcEl in gdEl.Elements("PowerConsumer"))
{
    var consEl = pcEl.Element("Consumed");
    if (consEl != null && TryParseDouble(consEl, "W", out double watts))
        part.GameData.PowerConsumers.Add(new PowerConsumerState { ConsumedWatts = watts });
}
```

#### 4c. Connector parsing from GameData

```csharp
foreach (var connEl in gdEl.Elements("Connector"))
{
    var connState = new ConnectorState
    {
        Id = connEl.Attribute("Id")?.Value ?? "",
    };

    var flagsStr = connEl.Element("Flags")?.Value ?? "";
    connState.FlagInternal = flagsStr.Contains("Internal");
    connState.FlagToSurface = flagsStr.Contains("ToSurface");
    connState.FlagFromSurface = flagsStr.Contains("FromSurface");

    part.GameData.Connectors.Add(connState);
}
```

#### 4d. Connector parsing from Assets XML

Also update `LoadPart()` (which parses the Assets XML) to read connector geometry:

```csharp
// After parsing SubParts, also parse Connectors from Assets XML
foreach (var connEl in partEl.Elements("Connector"))
{
    var connId = connEl.Attribute("Id")?.Value ?? "";
    if (string.IsNullOrEmpty(connId)) continue;

    // Check if this connector already exists from GameData parsing
    var existing = editingPart.GameData.Connectors.FirstOrDefault(c => c.Id == connId);
    if (existing != null)
    {
        // Merge geometric data from Assets into existing GameData connector
        var transformEl = connEl.Element("Transform");
        if (transformEl != null)
        {
            existing.Position = ParseVector3(transformEl.Element("Position"), double3.Zero);
            existing.Rotation = ParseRotation(transformEl.Element("Rotation"));
            existing.Scale = ParseVector3(transformEl.Element("Scale"), double3.One);
        }
    }
    else
    {
        // New connector not in GameData — add it
        var conn = new ConnectorState { Id = connId };
        var transformEl = connEl.Element("Transform");
        if (transformEl != null)
        {
            conn.Position = ParseVector3(transformEl.Element("Position"), double3.Zero);
            conn.Rotation = ParseRotation(transformEl.Element("Rotation"));
            conn.Scale = ParseVector3(transformEl.Element("Scale"), double3.One);
        }
        // Parse flags if present in assets
        var flagsStr = connEl.Element("Flags")?.Value ?? "";
        conn.FlagInternal = flagsStr.Contains("Internal");
        conn.FlagToSurface = flagsStr.Contains("ToSurface");
        conn.FlagFromSurface = flagsStr.Contains("FromSurface");

        editingPart.GameData.Connectors.Add(conn);
    }
}
```

**Load order:** Since `LoadPart()` loads Assets XML first and then calls `LoadGameData()`, the connector merge must handle both orderings. The cleanest approach is to load Assets XML connectors first (geometric data), then have `LoadGameData()` merge flags on top. Update `LoadGameData()` to find-and-merge rather than always add-new.

#### 4e. Decoupler / DockingPort / EVADoor parsing

```csharp
var decEl = gdEl.Element("Decoupler");
if (decEl != null)
{
    part.GameData.Decoupler = new DecouplerState
    {
        ConnectorId = decEl.Attribute("ConnectorId")?.Value ?? "",
        Force = double.TryParse(decEl.Attribute("Force")?.Value,
            NumberStyles.Any, CultureInfo.InvariantCulture, out double f) ? f : 500.0
    };
}

var dpEl = gdEl.Element("DockingPort");
if (dpEl != null)
{
    part.GameData.DockingPort = new DockingPortState
    {
        ConnectorId = dpEl.Attribute("ConnectorId")?.Value ?? "",
        Force = double.TryParse(dpEl.Attribute("Force")?.Value,
            NumberStyles.Any, CultureInfo.InvariantCulture, out double f) ? f : 500.0
    };
}

var evaEl = gdEl.Element("EVADoor");
if (evaEl != null)
{
    part.GameData.EVADoor = new EVADoorState
    {
        ConnectorId = evaEl.Attribute("ConnectorId")?.Value ?? "",
    };
}
```

---

### Task 5: Expand PartEditorUi — GameData sections

**File:** `space-tape.lib/PartEditorUi.cs`

The `RenderGameDataSection()` (line 495) currently shows Mass, Battery, Generator, and EditorTags. This needs expansion into multiple collapsible sub-sections. Because PartEditorUi.cs is already 644 lines, split the new UI rendering into a new file.

#### 5a. New file: `GameDataEditorUi.cs`

Create `space-tape.lib/GameDataEditorUi.cs` containing static or instance methods for each sub-section. This keeps `PartEditorUi.cs` manageable.

Sub-sections to implement:

1. **Basic Info** — DisplayName, EditorTags (existing, moved here)
2. **Mass** — CustomMass (existing)
3. **Tank** — toggle to enable/disable, shape selector, all tank fields
4. **Power** — list of Batteries, Generators, PowerConsumers with add/remove
5. **Connectors** — list with add/remove/edit, per-connector fields (id, position, rotation, scale, flags)
6. **Coupling** — optional Decoupler, DockingPort, EVADoor (toggle to enable, then fields)

#### 5b. Tank UI

```
[x] Enable Tank
Shape: [Cylindrical ▼ | Spherical]

--- Cylindrical Tank ---
Length (m):       [____]
Outer Radius (m): [____]
Wall Thickness (mm): [____]
Dome Height Frac:  [____]  (slider 0.0–1.0)

--- Common ---
Location X/Y/Z:  [____] [____] [____]
Rotation X/Y/Z:  [____] [____] [____]
Wall Density (kg/m³): [____]
Wall Mass (kg):   [____]  (optional override)
Wall Material:    [____]  (text input for material ID)
```

Use `ImGui.Checkbox` for the enable toggle. When unchecked, set `gameData.Tank = null`.
When checked and Tank is null, create a new `TankState()` with defaults.
Use `ImGui.Combo` for shape selection.
Use `ImGui.InputDouble` for all numeric fields.
Use `ImGui.SliderFloat` for `DomeHeightFraction` (clamped 0.0–1.0).

#### 5c. Connector list UI

```
Connectors (3):
  [top]      Internal ✓  ToSurface ✗  FromSurface ✗   [Edit] [Delete]
  [bottom]   Internal ✓  ToSurface ✗  FromSurface ✗   [Edit] [Delete]
  [radial_1] Internal ✗  ToSurface ✓  FromSurface ✗   [Edit] [Delete]

[+ Add Connector]

--- Editing: top ---
Id:       [________]
Position: [____] [____] [____]
Rotation: [____] [____] [____]
Scale:    [____] [____] [____]
[x] Internal (Stack)
[x] To Surface (Radial out)
[ ] From Surface (Radial in)
```

- Use `ImGui.Selectable` for each connector row
- Track `_selectedConnectorIndex` for the editing panel
- Provide add/remove buttons
- Auto-generate connector Ids: `conn_1`, `conn_2`, etc.

#### 5d. Coupling UI (Decoupler, DockingPort, EVADoor)

```
[x] Decoupler
Connector: [bottom ▼]     ← Combo populated from defined Connectors
Force (N): [500]

[x] Docking Port
Connector: [dock_1 ▼]
Force (N): [500]

[x] EVA Door
Connector: [eva_1 ▼]
```

The Connector combobox should list all currently defined ConnectorState Ids. If no connectors are defined, show a warning "Define connectors first".

#### 5e. Power list UI

Replace the single battery/generator inputs with list editors:

```
--- Batteries ---
  #1: 0.01 kWh   [x]   ← [x] removes
  #2: 0.05 kWh   [x]
[+ Add Battery]

--- Generators ---
  #1: 5.0 W   [x]
[+ Add Generator]

--- Power Consumers ---
  #1: 2.0 W   [x]
[+ Add Power Consumer]
```

Each item is editable inline with `ImGui.InputDouble`.

---

### Task 6: Expand HotReloadSpike.BuildTemplate()

**File:** `space-tape.lib/HotReloadSpike.cs`

The `BuildTemplate()` method (line 85) must map all new GameData fields onto the `PartTemplate`:

```csharp
private static PartTemplate BuildTemplate(EditingPart editingPart)
{
    var template = new PartTemplate { ... };  // existing SubPart + EditorTag + CustomMass logic

    var gd = editingPart.GameData;

    // Tank
    if (gd.Tank != null)
    {
        if (gd.Tank.Shape == TankShape.Cylindrical)
        {
            template.Tank = new CylindricalTankTemplate
            {
                LocationAsmb = new Vector3Reference(gd.Tank.LocationAsmb),
                Paf2Asmb = new Vector3Reference(gd.Tank.Paf2Asmb),
                Density = new DensityReference(gd.Tank.WallDensityKgPerM3),
                Length = new DistanceReference(gd.Tank.LengthM),
                OuterRadius = new DistanceReference(gd.Tank.OuterRadiusM),
                WallThickness = new DistanceReference(gd.Tank.WallThicknessMm / 1000.0), // mm → m
                DomeHeightFraction = new DoubleReference(gd.Tank.DomeHeightFraction),
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
            template.Tank.Material = new SerializedReference { Id = gd.Tank.WallMaterialId };
    }

    // Connectors
    foreach (var c in gd.Connectors)
    {
        var flags = Part.Connector.Flag.Internal; // clear default
        flags = 0;
        if (c.FlagInternal) flags |= Part.Connector.Flag.Internal;
        if (c.FlagToSurface) flags |= Part.Connector.Flag.ToSurface;
        if (c.FlagFromSurface) flags |= Part.Connector.Flag.FromSurface;

        template.Connectors.Add(new Part.Connector.TemplateBase
        {
            Id = c.Id,
            Transform = new TransformReference
            {
                PositionValue = c.Position,
                RotationValue = c.Rotation,
                ScaleValue = c.Scale,
            },
            Flags = flags,
        });
    }

    // Batteries
    foreach (var b in gd.Batteries)
    {
        template.Batteries.Add(new BatteryTemplate
        {
            MaximumCapacity = new JoulesReference(b.CapacityKWh),
        });
    }

    // Generators
    foreach (var g in gd.Generators)
    {
        template.Generators.Add(new GeneratorTemplate
        {
            Produced = new WattsReference(g.OutputWatts),
        });
    }

    // PowerConsumers
    foreach (var pc in gd.PowerConsumers)
    {
        template.PowerConsumers.Add(new PowerConsumerTemplate
        {
            Consumed = new WattsReference(pc.ConsumedWatts),
        });
    }

    // Decoupler
    if (gd.Decoupler != null)
    {
        template.Decoupler = new DecouplerTemplate
        {
            ConnectorId = gd.Decoupler.ConnectorId,
            Force = (float)gd.Decoupler.Force,
        };
    }

    // DockingPort
    if (gd.DockingPort != null)
    {
        template.DockingPort = new DockingPortTemplate
        {
            ConnectorId = gd.DockingPort.ConnectorId,
            Force = (float)gd.DockingPort.Force,
        };
    }

    // EVADoor
    if (gd.EVADoor != null)
    {
        template.EVADoor = new EVADoorTemplate
        {
            ConnectorId = gd.EVADoor.ConnectorId,
        };
    }

    return template;
}
```

**KSA API notes:**
- `Vector3Reference` constructor likely takes `double3` — verify in decompiled source. If not, set `PositionValue` directly on a default-constructed instance.
- `DistanceReference` constructor likely takes `double` (meters) — verify. Alternative: set the internal value directly.
- `JoulesReference` constructor takes `double` in kWh — check if it expects kWh or joules (the XML attribute is `KWh`). The `BatteryTemplate` stores `MaximumCapacity` as a `JoulesReference` with `KWh` attribute, so the reference type handles the unit.
- `WattsReference` constructor takes `double` in watts.
- `DensityReference` constructor takes `double` in kg/m³.
- `MassReference` constructor takes `double` in kg — confirmed in existing code (HotReloadSpike line 121).

**Important:** The exact constructor signatures of `DistanceReference`, `JoulesReference`, `WattsReference`, `DensityReference`, `Vector3Reference` etc. need to be verified from the decompiled source before implementation. The implementor should check `decomp/ksa/KSA/DistanceReference.cs`, `JoulesReference.cs`, `WattsReference.cs`, `DensityReference.cs`, and `Vector3Reference.cs` for available constructors and adapt accordingly (some may require setting properties rather than passing to constructors).

---

### Task 7: Connector 3D Gizmo Visualization

**File:** `space-tape.lib/PartEditorScene.cs` (and possibly a new `ConnectorGizmo.cs`)

#### 7a. Create ConnectorGizmo helper

Create `space-tape.lib/ConnectorGizmo.cs` that manages `GenericGizmo` instances for rendering connector positions in the 3D scene.

Design:
- Each connector is rendered as a small gizmo with directional arrow showing orientation
- Use `GenericGizmo` with the `Box` mesh (same as origin gizmo) — 2 segments per connector:
  - **Sphere segment:** a small cube at the connector position, colored by connector type
  - **Direction segment:** an elongated arrow showing the connector's facing direction
- Colors:
  - **Internal (stack):** Yellow
  - **ToSurface (radial out):** Cyan
  - **FromSurface (radial in):** Magenta
  - **Multiple flags:** White
  - **Selected connector:** Bright green (highlighted)

```csharp
public sealed class ConnectorGizmo : IDisposable
{
    private GenericGizmo? _gizmo;
    private int _maxConnectors;

    // 2 segments per connector: body + direction arrow
    private const int SegmentsPerConnector = 2;

    public void EnsureCapacity(int connectorCount)
    {
        if (_gizmo != null && _maxConnectors >= connectorCount) return;

        _gizmo?.Dispose();
        _maxConnectors = Math.Max(connectorCount, 8); // allocate some headroom
        _gizmo = new GenericGizmo(
            ModLibrary.Get<MeshReference>("Box"),
            GenericGizmo.Static.GenericGizmoRenderData,
            _maxConnectors * SegmentsPerConnector);
    }

    public void Update(
        Viewport viewport,
        IReadOnlyList<ConnectorState> connectors,
        int selectedIndex,
        double4x4 matrixAsmb2Ego)
    {
        if (_gizmo == null) return;
        var seg = _gizmo.GetSegmentDataByViewport(viewport);

        for (int i = 0; i < _maxConnectors; i++)
        {
            int bodyIdx = i * SegmentsPerConnector;
            int arrowIdx = bodyIdx + 1;

            if (i >= connectors.Count)
            {
                seg[bodyIdx].Active = false;
                seg[arrowIdx].Active = false;
                continue;
            }

            var c = connectors[i];
            double3 posEgo = c.Position.Transform(matrixAsmb2Ego);
            double4 color = GetConnectorColor(c, i == selectedIndex);

            // Body — small cube at connector position
            seg[bodyIdx].Active = true;
            seg[bodyIdx].PositionEgo = posEgo;
            seg[bodyIdx].Body2Cce = c.Rotation;
            seg[bodyIdx].Scale = new double3(0.06, 0.06, 0.06);
            seg[bodyIdx].Color = color;

            // Direction arrow — elongated box pointing in connector's local -X (same convention as nozzles)
            double3 arrowOffset = new double3(-0.15, 0, 0).Transform(c.Rotation);
            seg[arrowIdx].Active = true;
            seg[arrowIdx].PositionEgo = posEgo + arrowOffset.Transform(matrixAsmb2Ego - posEgo); // approximate
            seg[arrowIdx].Body2Cce = c.Rotation;
            seg[arrowIdx].Scale = new double3(0.15, 0.02, 0.02);
            seg[arrowIdx].Color = color * new double4(1, 1, 1, 0.6); // slightly transparent
        }
    }

    private static double4 GetConnectorColor(ConnectorState c, bool selected)
    {
        if (selected) return new double4(0.2, 1.0, 0.2, 1.0); // bright green

        int flagCount = (c.FlagInternal ? 1 : 0) + (c.FlagToSurface ? 1 : 0) + (c.FlagFromSurface ? 1 : 0);
        if (flagCount > 1) return new double4(1.0, 1.0, 1.0, 0.9); // white for multi-flag

        if (c.FlagInternal) return new double4(1.0, 1.0, 0.0, 0.9);  // yellow
        if (c.FlagToSurface) return new double4(0.0, 1.0, 1.0, 0.9); // cyan
        if (c.FlagFromSurface) return new double4(1.0, 0.0, 1.0, 0.9); // magenta

        return new double4(0.7, 0.7, 0.7, 0.7); // gray (no flags set)
    }

    public void Dispose()
    {
        _gizmo?.Dispose();
        _gizmo = null;
    }
}
```

#### 7b. Integrate ConnectorGizmo into PartEditorScene

Add a `ConnectorGizmo` field to `PartEditorScene`. Update the `UpdateGizmo()` method (or add a new `UpdateConnectorGizmos()`) to call `ConnectorGizmo.Update()` each frame.

The `PartEditorUi` must pass the selected connector index to the scene so the gizmo can highlight it.

Add to `PartEditorScene`:
```csharp
private ConnectorGizmo? _connectorGizmo;
public int SelectedConnectorIndex { get; set; } = -1;
```

In `UpdateGizmo()`, after the origin gizmo update:
```csharp
// Connector gizmos
if (_connectorGizmo != null && _currentPart?.GameData.Connectors.Count > 0)
{
    _connectorGizmo.EnsureCapacity(_currentPart.GameData.Connectors.Count);
    _connectorGizmo.Update(viewport, _currentPart.GameData.Connectors, SelectedConnectorIndex, matrix);
}
```

**Note on the matrix:** The connectors are positioned in assembly space (same coordinate space as SubPart positions). The matrix `matrixAsmb2Ego` from `_editingSpace.GetMatrixAsmb2Ego(camera)` converts assembly-space to eye-space — use this same matrix for connector positions.

#### 7c. PartEditorScene needs a reference to the current EditingPart

Currently `PartEditorScene` gets the `EditingPart` only during `SyncParts()`. For connector gizmo rendering, it needs ongoing access. Options:
- Pass the `EditingPart` reference to `UpdateGizmo()` — cleanest, no stored reference
- Store a reference during `SyncParts()` — simpler callsite

Recommended: pass `EditingPart` to the gizmo update method. Update the signature:
```csharp
public void UpdateGizmo(Viewport viewport, EditingPart? editingPart)
```

The caller in `Mod.cs` or wherever `UpdateGizmo` is invoked needs to pass the current editing part.

---

### Task 8: Plumbing — wire everything together

#### 8a. Update Mod.cs / top-level wiring

**File:** `space-tape/Mod.cs`

Ensure the new `PartCatalog` is instantiated and passed to `PartEditorUi`. The `UpdateGizmo` call must pass the `EditingPart`.

#### 8b. Update PartEditorUi.Render() method signature

The `Render()` method needs access to the `PartCatalog` and must pass the selected connector index to the scene. Add parameters or store references as needed.

#### 8c. Scene cleanup

When `PartEditorScene.Exit()` is called, dispose the `ConnectorGizmo`.

When `PartEditorScene.SyncParts()` is called, also trigger a connector gizmo capacity update.

---

## File Summary

### New files
| File | Purpose |
|------|---------|
| `space-tape.lib/GameDataModels.cs` | State classes: TankState, ConnectorState, DecouplerState, etc. |
| `space-tape.lib/PartCatalog.cs` | Loads all non-SubPart parts from ModLibrary |
| `space-tape.lib/PartImporter.cs` | Deep-reads PartTemplate → EditingPart |
| `space-tape.lib/GameDataEditorUi.cs` | ImGui rendering for tank/connector/coupling/power sections |
| `space-tape.lib/ConnectorGizmo.cs` | 3D gizmo visualization for connectors |

### Modified files
| File | Changes |
|------|---------|
| `space-tape.lib/PartEditorState.cs` | Replace BatteryCapacity/GeneratorOutput with new model, update Clone() |
| `space-tape.lib/GameDataXmlSerializer.cs` | Add tank/connector/coupling/power serialization |
| `space-tape.lib/PartXmlSerializer.cs` | Add connector geometry to Assets XML |
| `space-tape.lib/PartModWriter.cs` | Expand LoadGameData() and LoadPart() for all new fields |
| `space-tape.lib/PartEditorUi.cs` | Add "Import From Game" section, delegate GameData UI to GameDataEditorUi |
| `space-tape.lib/HotReloadSpike.cs` | Expand BuildTemplate() with all new fields |
| `space-tape.lib/PartEditorScene.cs` | Add ConnectorGizmo, update UpdateGizmo() |
| `space-tape/Mod.cs` | Wire PartCatalog, pass EditingPart to gizmo updates |

---

## Implementation Order

Tasks should be implemented in this order due to dependencies:

1. **Task 1** — GameData state models (foundation for everything else)
2. **Task 3** — XML serialization (depends on Task 1 models)
3. **Task 4** — XML deserialization (depends on Task 1 models)
4. **Task 6** — HotReloadSpike expansion (depends on Task 1 models)
5. **Task 2** — Import from existing part (depends on Task 1 models)
6. **Task 5** — Editor UI expansion (depends on Tasks 1-4)
7. **Task 7** — Connector 3D gizmo (depends on Task 1 + 5 for connector state/selection)
8. **Task 8** — Wiring/integration (depends on all above)

After each task, run `dotnet build` to verify compilation.

---

## Decompiled Source Reference Index

| File | Key Content |
|------|-------------|
| `decomp/ksa/KSA/PartTemplate.cs` | All PartTemplate fields, ApplyGameData merge logic |
| `decomp/ksa/KSA/Part.cs` (lines 32-133) | Connector class, TemplateBase, Flag enum |
| `decomp/ksa/KSA/CylindricalTankTemplate.cs` | Length, OuterRadius, WallThickness, DomeHeightFraction |
| `decomp/ksa/KSA/SphericalTankTemplate.cs` | OuterRadius, WallThickness |
| `decomp/ksa/KSA/AsmbTankTemplate.cs` | Tank base class |
| `decomp/ksa/KSA/AsmbVolumetricMassTemplate.cs` | Mass, Density, Material fields |
| `decomp/ksa/KSA/AsmbTransformTemplate.cs` | LocationAsmb, Paf2Asmb fields |
| `decomp/ksa/KSA/DecouplerTemplate.cs` | ConnectorId, Force attributes |
| `decomp/ksa/KSA/DockingPortTemplate.cs` | ConnectorId, Force attributes |
| `decomp/ksa/KSA/EVADoorTemplate.cs` | ConnectorId attribute |
| `decomp/ksa/KSA/BatteryTemplate.cs` | MaximumCapacity (JoulesReference) |
| `decomp/ksa/KSA/GeneratorTemplate.cs` | Produced (WattsReference) |
| `decomp/ksa/KSA/PowerConsumerTemplate.cs` | Consumed (WattsReference) |
| `decomp/ksa/KSA/CustomMassTemplate.cs` | Mass (MassReference) |
| `decomp/ksa/KSA/PartGameDataReference.cs` | _isGameData = true, separate registration |
| `decomp/ksa/KSA/AssetBundle.cs` (lines 26-29) | XML element → type mappings |
| `decomp/ksa/KSA/GenericGizmo.cs` | Gizmo rendering API, segments, per-viewport data |
| `decomp/ksa/KSA/RocketControllerTemplate.cs` | (Phase 2 — deferred) |
| `decomp/ksa/KSA/RocketTemplate.cs` | (Phase 2 — deferred) |
| `decomp/ksa/KSA/CombustorTemplate.cs` | (Phase 2 — deferred) |
| `decomp/ksa/KSA/DeLavalNozzleTemplate.cs` | (Phase 2 — deferred) |
| `decomp/ksa/KSA/RocketNozzleTemplate.cs` | (Phase 2 — deferred) |

---

## Open Questions / Risks

1. **Reference type constructors** — The exact constructor signatures of `DistanceReference`, `JoulesReference`, `WattsReference`, `DensityReference`, `Vector3Reference`, `SerializedReference` need verification from decompiled sources before implementation. Some may require property-setting rather than constructor params.

2. **EditorTag enum vs string** — `PartTemplate.EditorTags` is `List<EditorTag>` (enum populated by `VehicleEditor.RegisterTag()`). When importing, we need `tag.ToString()` → string. When building templates, we use `StringReference` which gets registered during `OnDataLoad`. This should work as-is.

3. **Tank Material reference** — `SerializedReference` for wall material needs a valid material ID from the game's substance/material library. The UI should ideally show a dropdown of available materials, but for Phase 1 a text input is sufficient. A later enhancement could enumerate available materials.

4. **Connector gizmo performance** — Creating/disposing GenericGizmo instances during editing should be fine for small connector counts (<20). The `EnsureCapacity` pattern avoids frequent reallocations.

5. **Part ID collision on import** — When importing an existing part, we append `.Custom` to avoid ID collision. The user should be able to rename it, which they already can via the Part ID field.

6. **Connector geometry in Assets vs GameData** — KSA splits connector data between Assets (position) and GameData (flags). Space-tape writes both files, so we write full connector data to both. During `ApplyGameData`, the game OR's flags from both sources (PartTemplate.cs lines 204-233), which is correct behavior.
