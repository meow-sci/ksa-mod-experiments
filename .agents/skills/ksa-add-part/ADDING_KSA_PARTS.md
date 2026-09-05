# Adding New Parts to KSA (Kitten Space Agency)

> **Assembled from**: strategic analysis of `Content/Core/` (the built-in Core mod) and decompiled game source (`decomp/ksa/`).

---

## Table of Contents

1. [Overview](#overview)
2. [Mod Structure and Discovery](#mod-structure-and-discovery)
3. [File Types Required](#file-types-required)
4. [3D Mesh Format and Naming Conventions](#3d-mesh-format-and-naming-conventions)
5. [Texture Format and Naming Conventions](#texture-format-and-naming-conventions)
6. [XML Schema: Assets File (Visual)](#xml-schema-assets-file-visual)
7. [XML Schema: GameData File (Simulation)](#xml-schema-gamedata-file-simulation)
8. [How Assets and GameData Are Joined](#how-assets-and-gamedata-are-joined)
9. [Physics Data Reference](#physics-data-reference)
10. [Propulsion Data Reference](#propulsion-data-reference)
11. [Connectors and Attachment Nodes](#connectors-and-attachment-nodes)
12. [Worked Example: Minimal New Part](#worked-example-minimal-new-part)
13. [Worked Example: RCS Thruster](#worked-example-rcs-thruster)
14. [Worked Example: Structural Part](#worked-example-structural-part)
15. [Editor Tags (Part Browser Categorization)](#editor-tags-part-browser-categorization)
16. [Cross-Part SubPart Reuse](#cross-part-subpart-reuse)
17. [Size and Naming Conventions](#size-and-naming-conventions)
18. [Part Loading Pipeline (Internal)](#part-loading-pipeline-internal)
19. [Common Pitfalls](#common-pitfalls)

---

## Overview

KSA parts are data-driven — no C# code is needed to add a part. A new part consists of:

| File | Purpose |
|------|---------|
| `*.glb` | 3D mesh(es) — binary glTF, one per category "atlas" |
| `*_TextureAtlas_Diffuse.ktx2` | Albedo/colour texture atlas |
| `*_TextureAtlas_Normal.ktx2` | Normal map texture atlas |
| `*_TextureAtlas_PBR.ktx2` | AO / Roughness / Metallic packed texture atlas |
| `*Assets.xml` | Visual definitions: meshes, materials, SubPart templates, Part prefabs |
| `*GameData.xml` | Simulation definitions: mass, physics, propulsion, connector flags, editor tags |
| `mod.toml` | Mod manifest that lists which XML files to load |

The game's internal **Core** mod (`Content/Core/`) is the canonical reference for all of the above.

---

## Mod Structure and Discovery

The game discovers mods by scanning:
1. `Content/` — built-in mods (Core, etc.)
2. `[Documents]/KSA/mods/` — user-installed mods

Each mod directory must contain a `mod.toml`. The `assets` key in `mod.toml` lists the XML asset bundle files to load:

```toml
# mod.toml
id = "my-mod"
name = "My Mod"
version = "1.0.0"
author = "You"

assets = [
    "MyPartsAssets.xml",
    "MyPartsGameData.xml",
]
```

> The `assets` array lists paths relative to the mod's own directory.

The game loads mods in manifest order and merges them all into a single `ModLibrary` registry.  Parts from different mods can be mixed freely.

---

## File Types Required

### Minimum for a Visible Part

1. A `.glb` file with at least one named mesh node
2. Texture atlases: Diffuse, Normal, AoRoughMetal (all `.ktx2`)
3. An `*Assets.xml` file registered in `mod.toml`
4. An `*GameData.xml` file registered in `mod.toml`

> **Note:** You can share textures and GLBs with existing categories to avoid creating your own atlases, but only if your UV layout fits within that atlas. In practice, custom parts need their own atlas unless explicitly test-fitting into an existing one.

---

## 3D Mesh Format and Naming Conventions

### Format

- **Use `.glb` (binary glTF 2.0)** exclusively. This is the only format the game loads for parts.
- `.gltf` + `.bin` (text-based glTF) is technically possible but not used for parts.

### Mesh Atlas Concept

All parts in a category share a **single GLB file** called a "mesh atlas." The GLB contains multiple named mesh nodes — one per SubPart visual variant. The XML references them by node name.

| Category | Atlas File |
|----------|------------|
| Command Module | `CoreCommandA_MeshAtlas.glb` |
| Propulsion A | `CorePropulsionA_MeshAtlas.glb` |
| Propulsion B (RCS etc.) | `CorePropulsionB_MeshAtlas.glb` |
| Structural | `CoreStructuralA_MeshAtlas.glb` |
| Fuel Tanks | `CoreFuelTankA_MeshAtlas.glb` |
| Service Module | `CoreServiceModuleA_MeshAtlas.glb` |
| Fairing | `CoreFairingA_MeshAtlas.glb` |
| Passage/Hatches | `CorePassageA_MeshAtlas.glb` |

**Convention:** `{ModId}_{CategoryName}_MeshAtlas.glb`

### View Model (`_VM`) Meshes

Each SubPart should have **two mesh nodes** in the GLB:
- **Main mesh** — used for in-game rendering (full detail)
- **View model** (`_VM` suffix) — used in the editor part browser thumbnail (can be lower detail or identical)

Example node names inside a GLB:
```
CoreStructuralA_Subpart_BracketA
CoreStructuralA_Subpart_BracketA_VM
```

These exact node names become the `Id` values used in the XML.

### Mesh Scale and Orientation

- All dimensions are in **metres**.
- The game's coordinate system: **Y-up**, **Z-forward** (standard glTF orientation).
- Part origin should be at the part's geometric centre or a logical mounting point.

---

## Texture Format and Naming Conventions

### Primary Formats

| Format | Usage |
|--------|-------|
| **`.ktx2`** | Primary GPU texture format — required for Diffuse, Normal, AoRoughMetal, Emissive |
| **`.dds`** | Alternative — used for ThinFilm (heat shield) and some Emissive textures |
| **`.png`** | Uncompressed — usable during development only; GPU efficiency is lower |

> The game natively loads `.ktx2` (Khronos GPU texture format). For mod development, start with `.png` during iteration and convert to `.ktx2` for release using the `toktx` or `basisu` tool.

### Texture Atlas Concept

Like meshes, **all parts in a category share one texture atlas per channel.** Part surfaces are differentiated by their UV layout within the atlas.

### PBR Channels and File Names

**Convention:** `{ModId}_{CategoryName}_TextureAtlas_{Channel}.ktx2`

| Channel | File suffix | Content |
|---------|------------|---------|
| Diffuse | `_Diffuse.ktx2` | Base colour (albedo) — RGBA |
| Normal | `_Normal.ktx2` | Tangent-space normal map |
| AoRoughMetal | `_PBR.ktx2` | R=AO, G=Roughness, B=Metallic |
| Emissive | `_Emissive.ktx2` | Self-illumination (optional) |
| ThinFilm | `_TFI.dds` | Heat-shield interference (optional) |

Example for a mod called "MyMod" with category "WidgetA":
```
MyMod_WidgetA_TextureAtlas_Diffuse.ktx2
MyMod_WidgetA_TextureAtlas_Normal.ktx2
MyMod_WidgetA_TextureAtlas_PBR.ktx2
```

### Texture Category Tag

All vessel textures use `Category="Vessel"` in the XML. This is the only value observed for part textures.

---

## XML Schema: Assets File (Visual)

The `*Assets.xml` file defines visual appearance. Its root element is `<Assets>`.

### Full Structure

```xml
<?xml version="1.0" encoding="utf-8"?>
<Assets>

  <!-- 1. Declare the mesh atlas GLB file -->
  <MeshAtlas Path="Meshes/MyMod_WidgetA_MeshAtlas.glb"/>

  <!-- 2. Declare the PBR material (textures) -->
  <PbrMaterial Id="MyMod_WidgetA_Material">
    <Diffuse     Path="Textures/MyMod_WidgetA_TextureAtlas_Diffuse.ktx2" Category="Vessel"/>
    <Normal      Path="Textures/MyMod_WidgetA_TextureAtlas_Normal.ktx2"  Category="Vessel"/>
    <AoRoughMetal Path="Textures/MyMod_WidgetA_TextureAtlas_PBR.ktx2"   Category="Vessel"/>
    <!-- Optional: emissive, thin film -->
    <Emissive    Path="Textures/MyMod_WidgetA_TextureAtlas_Emissive.ktx2" Category="Vessel"/>
    <ThinFilm    Path="Textures/MyMod_WidgetA_TextureAtlas_TFI.dds"      Category="Vessel"/>
  </PbrMaterial>

  <!-- 3. Define SubPart templates (reusable visual building blocks) -->
  <SubPart Id="MyMod_WidgetA_Subpart_MainBody">
    <PartModel Id="MyMod_WidgetA_Subpart_MainBody_Model">
      <Mesh Id="MyMod_WidgetA_Subpart_MainBody"/>       <!-- Name of GLB mesh node -->
      <Material Id="MyMod_WidgetA_Material"/>
    </PartModel>
    <MeshView>
      <Mesh Id="MyMod_WidgetA_Subpart_MainBody_VM"/>    <!-- _VM GLB node for editor thumb -->
    </MeshView>
  </SubPart>

  <!-- 4. Assemble Parts from SubPart instances and Connectors -->
  <Part Id="MyMod_WidgetA_Prefab_SmallA">
    <SubPart Id="MyMod_WidgetA_Subpart_MainBody1"
             InstanceOf="MyMod_WidgetA_Subpart_MainBody">
      <Transform>
        <Position X="0" Y="0" Z="0"/>
      </Transform>
    </SubPart>
    <!-- Attachment node (top) -->
    <Connector Id="_connector_top">
      <Transform>
        <Position Y="0.5"/>
        <Scale X="1.0" Y="1.0" Z="1.0"/>
      </Transform>
    </Connector>
    <!-- Attachment node (bottom) -->
    <Connector Id="_connector_bottom">
      <Transform>
        <Position Y="-0.5"/>
        <Rotation Z="3.14159"/>
        <Scale X="1.0" Y="1.0" Z="1.0"/>
      </Transform>
    </Connector>
  </Part>

</Assets>
```

### `<MeshAtlas>` Element

| Attribute | Required | Description |
|-----------|----------|-------------|
| `Path` | Yes | Relative path to the `.glb` file from the mod's content root |
| `Id` | Optional | If set, also registers the atlas as a named `MeshFile` asset |

### `<PbrMaterial>` Element

| Attribute | Required | Description |
|-----------|----------|-------------|
| `Id` | Yes | Unique identifier referenced by `<Material Id="..."/>` in SubParts |

Child elements: `<Diffuse>`, `<Normal>`, `<AoRoughMetal>` are required. `<Emissive>` and `<ThinFilm>` are optional.

Each texture element has:
| Attribute | Required | Description |
|-----------|----------|-------------|
| `Path` | Yes | Relative path to the texture file |
| `Category` | Yes | Always `"Vessel"` for part textures |

### `<SubPart>` Template Element

| Attribute | Required | Description |
|-----------|----------|-------------|
| `Id` | Yes | Globally unique identifier for this SubPart template |

Child elements:
- `<PartModel Id="...">` — static rigid mesh renderer
  - `<Mesh Id="..."/>` — references a named node in the MeshAtlas GLB
  - `<Material Id="..."/>` — references a `<PbrMaterial>` by Id
  - `<ShadowCaster>false</ShadowCaster>` — optional, disables shadow casting (use for transparencies like windows)
- `<PartModelDynamic Id="...">` — same as PartModel but for deformable/procedural geometry (e.g., SRB segments that stretch)
- `<MeshView>` — editor thumbnail mesh; contains `<Mesh Id="..."/>`
- `<Light>` — attaches a light source (see Light Reference below)

### `<Part>` Prefab Element

| Attribute | Required | Description |
|-----------|----------|-------------|
| `Id` | Yes | Globally unique part identifier — must match the `<PartGameData Id="...">` entry |

Child elements:
- `<SubPart Id="instanceId" InstanceOf="templateId">` — places a SubPart template
  - `<Transform>` — local transform override (Position/Rotation/Scale in metres/radians)
  - `<Gimbal>` — TVC pivot point (for gimbaling engines only)
- `<Connector Id="...">` — attachment node (further configured in GameData)
  - `<Transform>` — position/rotation of the connector; Scale X encodes the port diameter in metres
- `<EditorTag Value="..."/>` — part browser category (can also be set in GameData)

### Lights on SubParts

```xml
<Light>
  <Type>Point</Type>        <!-- or Spot -->
  <Transform>
    <Position X="0" Y="0.1" Z="0"/>
  </Transform>
  <Range Value="5"/>        <!-- metres -->
  <Intensity Value="10"/>
  <Color R="1" G="0.9" B="0.8"/>
</Light>

<!-- Spot light additional fields: -->
<InnerAngle Value="0.3927"/>  <!-- radians (~22.5°) -->
<OuterAngle Value="0.7854"/>  <!-- radians (~45°) -->
```

---

## XML Schema: GameData File (Simulation)

The `*GameData.xml` file defines physical and simulation properties. Its root element is also `<Assets>`.

### Full Structure

```xml
<?xml version="1.0" encoding="utf-8"?>
<Assets>

  <!-- SubPart physics/behavior (matched by Id to SubPart template in Assets.xml) -->
  <SubPartGameData Id="MyMod_WidgetA_Subpart_MainBody">
    <SolidSphereMass>
      <Mass Kg="50"/>
      <Radius M="0.5"/>
    </SolidSphereMass>
  </SubPartGameData>

  <!-- Part physics/behavior (matched by Id to Part prefab in Assets.xml) -->
  <PartGameData Id="MyMod_WidgetA_Prefab_SmallA">
    <EditorTag Value="Structural"/>
    <Connector Id="_connector_top">
      <Flags>FromSurface</Flags>
    </Connector>
    <Connector Id="_connector_bottom">
      <Flags>FromSurface</Flags>
    </Connector>
  </PartGameData>

</Assets>
```

---

## How Assets and GameData Are Joined

The two XML files are linked **only by matching `Id` attributes** at runtime — there is no XML cross-file reference mechanism.

| Assets.xml element | GameData.xml element | Join key |
|-------------------|---------------------|----------|
| `<SubPart Id="X">` | `<SubPartGameData Id="X">` | X |
| `<Part Id="Y">` | `<PartGameData Id="Y">` | Y |
| `<Connector Id="Z">` in Part | `<Connector Id="Z">` in PartGameData | Z |

**Rules:**
- A `<SubPart>` without a matching `<SubPartGameData>` is purely visual (no mass, no physics).
- A `<Part>` without a matching `<PartGameData>` will still appear but will have no mass or editor tag.
- A matching `<PartGameData>` **merges** its data into the base part (connectors, mass, tags, etc.) — it does not replace it.
- Duplicate Ids are silently dropped; the first registration wins for base parts. For `PartGameData`, a second occurrence merges (like the split `*Assets.xml` / `*GameData.xml` files in Core do).

---

## Physics Data Reference

### Mass Definitions

#### Solid Sphere Approximation (simple parts)

```xml
<SolidSphereMass>
  <Mass Kg="100"/>
  <Radius M="0.5"/>    <!-- used to compute inertia tensor -->
</SolidSphereMass>
```

#### Custom Inertia Tensor (precise parts)

```xml
<CustomMass>
  <LocationBody Z="-0.117"/>   <!-- centre of mass offset from part origin, metres -->
  <Mass Kg="50"/>
  <MassSpecificInertia Ixx="0.0256" Iyy="0.0231" Izz="0.0099"/>
</CustomMass>
```

`MassSpecificInertia` values are the principal moments of inertia divided by total mass (`I/m`, units: m²).

### Tank Definitions

```xml
<Tank>
  <SphericalTank>
    <Material Id="Aluminum.2014(s)"/>
    <OuterRadius M="0.276"/>
    <WallThickness Mm="4"/>
  </SphericalTank>
</Tank>
```

The `Material Id` references a substance defined in `Substances.xml` (built-in). Known material Ids include `Aluminum.2014(s)`.

---

## Propulsion Data Reference

Propulsion is built from a hierarchy: `RocketThrusterController → Rocket → Combustor + DeLavalNozzle`.

### Single-Nozzle Engine

```xml
<SubPartGameData Id="MyMod_Engine_SubPart">
  <CustomMass>
    <Mass Kg="200"/>
    <LocationBody Z="-0.2"/>
    <MassSpecificInertia Ixx="0.04" Iyy="0.04" Izz="0.01"/>
  </CustomMass>

  <RocketEngineController Id="EngineController">
    <RocketReference Id="MainEngine"/>
  </RocketEngineController>

  <Rocket Id="MainEngine">
    <Core Id="Chamber"/>
    <Nozzle Id="Nozzle"/>
  </Rocket>

  <Combustor Id="Chamber">
    <Combustion Id="LOX_RP1_2.56"/>     <!-- propellant combination from Combustion.xml -->
    <MaxPressure Bar="70"/>
    <ThermalEfficiency Value="0.97"/>
    <MinimumPulseTime Seconds="0.5"/>
  </Combustor>

  <DeLavalNozzle Id="Nozzle">
    <ExitDiameter M="1.2"/>
    <AreaRatio Value="25"/>
    <FlowEfficiency Value="1"/>
    <ExpansionEfficiency Value="0.98"/>
    <ExhaustLocation X="0" Y="-1.5" Z="0"/>    <!-- physics exhaust origin (metres) -->
    <ExhaustDirection X="0" Y="-1" Z="0"/>     <!-- exhaust unit vector -->
    <FxExhaustLocation X="0" Y="-1.4" Z="0"/> <!-- visual effect origin -->
    <FxExhaustDirection X="0" Y="-1" Z="0"/>
    <VolumetricExhaust Id="ApolloRCS"/>        <!-- exhaust plume template from ExhaustAssets.xml -->
    <SoundEvent Action="On" SoundId="DefaultRcsThruster"/>
  </DeLavalNozzle>
</SubPartGameData>
```

### RCS Thruster (MultiAxis Control)

```xml
<SubPartGameData Id="MyMod_RCSThrust_Subpart">
  <SolidSphereMass>
    <Mass Kg="5"/>
    <Radius M="0.1"/>
  </SolidSphereMass>

  <RocketThrusterController Id="RD-4">
    <ControlMap CSV="PitchDown,RollLeft,YawRight"/>   <!-- control axes this thruster assists -->
    <RocketReference Id="Thruster"/>
  </RocketThrusterController>

  <Rocket Id="Thruster">
    <Core Id="Chamber"/>
    <Nozzle Id="Nozzle"/>
  </Rocket>

  <Combustor Id="Chamber">
    <Combustion Id="MMH_NTO_1.6"/>
    <MaxPressure Bar="7"/>
    <ThermalEfficiency Value="0.95"/>
    <MinimumPulseTime Seconds="0.00545"/>
  </Combustor>

  <DeLavalNozzle Id="Nozzle">
    <ExitDiameter M="1.1"/>
    <AreaRatio Value="164"/>
    <FlowEfficiency Value="1"/>
    <ExpansionEfficiency Value="0.70"/>
    <ExhaustLocation X="-0.15" Y="0" Z="0"/>
    <ExhaustDirection X="-1" Y="0" Z="0"/>
    <VolumetricExhaust Id="ApolloRCS"/>
    <SoundEvent Action="On" SoundId="DefaultRcsThruster"/>
  </DeLavalNozzle>
</SubPartGameData>
```

Known `Combustion` Ids (from `Combustion.xml`):
- `MMH_NTO_1.6` — storable bipropellant (RCS)
- `LOX_RP1_2.56` — kerolox (main engines)
- Others visible in `Combustion.xml` in the Core mod

### Gimbaling Engines

For TVC (thrust vector control), add a `<Gimbal>` to the SubPart instance in the Part prefab:

```xml
<Part Id="MyMod_Engine_Prefab">
  <SubPart Id="MyMod_Engine_Subpart1"
           InstanceOf="MyMod_Engine_SubPart">
    <Transform>
      <Position Y="-0.2"/>
    </Transform>
    <Gimbal>
      <Transform>
        <Position Y="1.1"/>   <!-- pivot point relative to subpart local space -->
      </Transform>
    </Gimbal>
  </SubPart>
  <Connector Id="_connector_top">
    <Transform>
      <Position Y="0.5"/>
      <Scale X="2.0" Y="2.0" Z="2.0"/>   <!-- 2-metre port -->
    </Transform>
  </Connector>
</Part>
```

---

## Connectors and Attachment Nodes

Connectors are attachment points. They are declared in the `<Part>` in `*Assets.xml` (position/rotation/scale) and augmented in `<PartGameData>` (flags).

### Connector Sizing

The `<Scale>` of a connector encodes the **port diameter in metres**. Matching ports must have the same scale for a valid attachment.

| Scale | Diameter |
|-------|----------|
| `X="0.5"` | 0.5 m (small RCS) |
| `X="1.0"` | 1 m |
| `X="2.0"` | 2 m |
| `X="2.5"` | 2.5 m |
| `X="3.0"` | 3 m |

### Connector Flags

Set in `<PartGameData>`:

| Flag | Meaning |
|------|---------|
| `FromSurface` | Other parts' `ToSurface` connectors can attach here (receive surface attachments) |
| `ToSurface` | This connector attaches radially to another part's surface |

Both flags can appear on the same connector for bidirectional surface mounting.

Standard axial (stack) connectors have no flags — they connect opposing standard connectors of matching scale.

### Decoupler

```xml
<PartGameData Id="MyMod_Decoupler_Prefab">
  <EditorTag Value="Decouplers"/>
  <Decoupler ConnectorId="_connector_bottom" Force="500"/>
  <SolidSphereMass>
    <Mass Kg="50"/>
    <Radius M="0.5"/>
  </SolidSphereMass>
</PartGameData>
```

`ConnectorId` references the connector that separates on staging. `Force` is the separation impulse.

---

## Worked Example: Minimal New Part

A simple static structural ring with no propulsion.

### `mod.toml`
```toml
id = "my-structural-mod"
name = "My Structural Parts"
version = "1.0.0"
author = "You"

assets = [
    "MyStructuralAssets.xml",
    "MyStructuralGameData.xml",
]
```

### `MyStructuralAssets.xml`
```xml
<?xml version="1.0" encoding="utf-8"?>
<Assets>

  <MeshAtlas Path="Meshes/MyStructural_MeshAtlas.glb"/>

  <PbrMaterial Id="MyStructural_Material">
    <Diffuse     Path="Textures/MyStructural_TextureAtlas_Diffuse.ktx2" Category="Vessel"/>
    <Normal      Path="Textures/MyStructural_TextureAtlas_Normal.ktx2"  Category="Vessel"/>
    <AoRoughMetal Path="Textures/MyStructural_TextureAtlas_PBR.ktx2"   Category="Vessel"/>
  </PbrMaterial>

  <SubPart Id="MyStructural_Subpart_Ring1m">
    <PartModel Id="MyStructural_Subpart_Ring1m_Model">
      <Mesh Id="MyStructural_Subpart_Ring1m"/>
      <Material Id="MyStructural_Material"/>
    </PartModel>
    <MeshView>
      <Mesh Id="MyStructural_Subpart_Ring1m_VM"/>
    </MeshView>
  </SubPart>

  <Part Id="MyStructural_Prefab_Ring1mA">
    <SubPart Id="MyStructural_Subpart_Ring1m1"
             InstanceOf="MyStructural_Subpart_Ring1m">
      <Transform/>
    </SubPart>
    <Connector Id="_top">
      <Transform>
        <Position Y="0.25"/>
        <Scale X="1.0" Y="1.0" Z="1.0"/>
      </Transform>
    </Connector>
    <Connector Id="_bottom">
      <Transform>
        <Position Y="-0.25"/>
        <Rotation Z="3.14159"/>
        <Scale X="1.0" Y="1.0" Z="1.0"/>
      </Transform>
    </Connector>
  </Part>

</Assets>
```

### `MyStructuralGameData.xml`
```xml
<?xml version="1.0" encoding="utf-8"?>
<Assets>

  <SubPartGameData Id="MyStructural_Subpart_Ring1m">
    <SolidSphereMass>
      <Mass Kg="25"/>
      <Radius M="0.5"/>
    </SolidSphereMass>
  </SubPartGameData>

  <PartGameData Id="MyStructural_Prefab_Ring1mA">
    <EditorTag Value="Structural"/>
    <Connector Id="_top">
      <Flags>FromSurface</Flags>
    </Connector>
    <Connector Id="_bottom">
      <Flags>FromSurface</Flags>
    </Connector>
  </PartGameData>

</Assets>
```

---

## Worked Example: RCS Thruster

An RCS thruster assembled from a base body and a nozzle SubPart, each with their own physics data.

### `MyRCSAssets.xml`
```xml
<?xml version="1.0" encoding="utf-8"?>
<Assets>

  <MeshAtlas Path="Meshes/MyRCS_MeshAtlas.glb"/>

  <PbrMaterial Id="MyRCS_Material">
    <Diffuse     Path="Textures/MyRCS_TextureAtlas_Diffuse.ktx2" Category="Vessel"/>
    <Normal      Path="Textures/MyRCS_TextureAtlas_Normal.ktx2"  Category="Vessel"/>
    <AoRoughMetal Path="Textures/MyRCS_TextureAtlas_PBR.ktx2"   Category="Vessel"/>
  </PbrMaterial>

  <!-- Thruster block (visual body) -->
  <SubPart Id="MyRCS_Subpart_Base">
    <PartModel Id="MyRCS_Subpart_Base_Model">
      <Mesh Id="MyRCS_Subpart_Base"/>
      <Material Id="MyRCS_Material"/>
    </PartModel>
    <MeshView>
      <Mesh Id="MyRCS_Subpart_Base_VM"/>
    </MeshView>
  </SubPart>

  <!-- Thruster nozzle (has propulsion data) -->
  <SubPart Id="MyRCS_Subpart_Thruster">
    <PartModel Id="MyRCS_Subpart_Thruster_Model">
      <Mesh Id="MyRCS_Subpart_Thruster"/>
      <Material Id="MyRCS_Material"/>
    </PartModel>
    <MeshView>
      <Mesh Id="MyRCS_Subpart_Thruster_VM"/>
    </MeshView>
  </SubPart>

  <!-- Assembled part: one base + one thruster, surface-mount connector -->
  <Part Id="MyRCS_Prefab_ThrusterA">
    <SubPart Id="MyRCS_Subpart_Base1"
             InstanceOf="MyRCS_Subpart_Base">
      <Transform/>
    </SubPart>
    <SubPart Id="MyRCS_Subpart_Thruster1"
             InstanceOf="MyRCS_Subpart_Thruster">
      <Transform>
        <Position X="0.1"/>
      </Transform>
    </SubPart>
    <Connector Id="_mount">
      <Transform>
        <Position Z="0.1"/>
        <Rotation Y="-1.5708"/>
        <Scale X="0.5" Y="0.5" Z="0.5"/>
      </Transform>
    </Connector>
  </Part>

</Assets>
```

### `MyRCSGameData.xml`
```xml
<?xml version="1.0" encoding="utf-8"?>
<Assets>

  <SubPartGameData Id="MyRCS_Subpart_Base">
    <SolidSphereMass>
      <Mass Kg="3"/>
      <Radius M="0.1"/>
    </SolidSphereMass>
  </SubPartGameData>

  <SubPartGameData Id="MyRCS_Subpart_Thruster">
    <SolidSphereMass>
      <Mass Kg="2"/>
      <Radius M="0.08"/>
    </SolidSphereMass>

    <RocketThrusterController Id="ThrCtrl">
      <ControlMap CSV="PitchUp,YawLeft"/>
      <RocketReference Id="Thruster"/>
    </RocketThrusterController>

    <Rocket Id="Thruster">
      <Core Id="Chamber"/>
      <Nozzle Id="Nozzle"/>
    </Rocket>

    <Combustor Id="Chamber">
      <Combustion Id="MMH_NTO_1.6"/>
      <MaxPressure Bar="7"/>
      <ThermalEfficiency Value="0.95"/>
      <MinimumPulseTime Seconds="0.00545"/>
    </Combustor>

    <DeLavalNozzle Id="Nozzle">
      <ExitDiameter M="0.5"/>
      <AreaRatio Value="100"/>
      <FlowEfficiency Value="1"/>
      <ExpansionEfficiency Value="0.72"/>
      <ExhaustLocation X="0.2" Y="0" Z="0"/>
      <ExhaustDirection X="1" Y="0" Z="0"/>
      <VolumetricExhaust Id="ApolloRCS"/>
      <SoundEvent Action="On" SoundId="DefaultRcsThruster"/>
    </DeLavalNozzle>
  </SubPartGameData>

  <PartGameData Id="MyRCS_Prefab_ThrusterA">
    <EditorTag Value="RCS"/>
    <Connector Id="_mount">
      <Flags>ToSurface</Flags>
    </Connector>
  </PartGameData>

</Assets>
```

---

## Worked Example: Structural Part

> See [Worked Example: Minimal New Part](#worked-example-minimal-new-part) — that is already a structural part.

For a structural part with **surface-attachment receive** (so other parts can radial-mount to it), set `FromSurface` on its connectors. For radial parts that **attach to surfaces**, set `ToSurface`.

---

## Editor Tags (Part Browser Categorization)

`<EditorTag Value="..."/>` controls which category the part appears under in the part browser. Any string value is accepted; the game auto-creates categories for new values. Known values from Core:

| Value | Category |
|-------|---------|
| `Propulsion` | Engines |
| `RCS` | RCS Thrusters |
| `FuelTank` | Fuel Tanks |
| `Structural` | Structural parts |
| `Fairing` | Fairings |
| `Interstage` | Interstage adapters |
| `Command` | Command modules |
| `Passage` | Hatches/passages |
| `ServiceModule` | Service modules |
| `Decouplers` | Decouplers |
| `Hidden` | Hidden from the part browser (internal use) |

> You can define your own new category string and it will appear in the browser.

---

## Cross-Part SubPart Reuse

SubParts from one XML file can be instanced in a Part defined in a different XML file, **provided both asset bundles are loaded**. Order of loading within the same mod is determined by the order in the `assets` array in `mod.toml`. If a referenced Id is not yet registered at parse time (i.e., the referenced file loads later), the game will still resolve it during the binding phase after all XML is parsed.

Example: Core's capsule parts in `CoreCommandAAssets.xml` instance hatch SubParts defined in `CorePassageAAssets.xml`.

---

## Size and Naming Conventions

The Core mod encodes dimensions in part names. Adopt these for discoverability:

| Suffix | Meaning |
|--------|---------|
| `1W`, `2W`, `3W` | 1 / 2 / 3 metre diameter class |
| `HalfW` | Half-width |
| `1H`, `2H`, `3H`, `6H` | Height multiples (1× / 2× / 3× / 6×) |
| `HalfH` | Half-height |
| `SizeA`, `SizeB` | Different absolute diameters (e.g., SizeA ≈ 1 m, SizeB ≈ 1.5 m) |
| `VariantA`, `VariantB` | Visual variant (same dimensions, different appearance) |

Example: `MyMod_FuelTank_Prefab_2W3H` = 2-metre diameter, 3× height.

---

## Part Loading Pipeline (Internal)

Understanding the internal pipeline explains why things work (or don't):

```
Game startup
  │
  ├─ ModLibrary.PrepareManifest()
  │    Scans Content/ and Documents/KSA/mods/ for dirs with mod.toml
  │
  ├─ ModLibrary.LoadAll()  [in manifest order]
  │    For each enabled mod:
  │    ├─ mod.LoadAssetBundles()
  │    │    For each path in mod.toml assets[]:
  │    │    └─ XmlLoader.Load<AssetBundle>(filePath, mod)
  │    │         └─ For each XML element in <Assets>:
  │    │              └─ asset.OnDataLoad(mod)
  │    │                   ├─ PartTemplate.OnDataLoad()     → ModLibrary.Register(part)
  │    │                   ├─ SubPartTemplate.OnDataLoad()  → registered by parent part
  │    │                   ├─ PartGameDataReference.OnDataLoad()
  │    │                   │    → if part exists: existingPart.ApplyGameData(this)
  │    │                   │    → else: register as PartGameDataReference for later merge
  │    │                   ├─ MeshFileReference.OnDataLoad() → GltfLoader reads .glb
  │    │                   │    → ModLibrary.RegisterBinder(mesh) [queues GPU upload]
  │    │                   └─ TextureReference.OnDataLoad() → queues GPU upload
  │
  └─ Parallel GPU upload phase
       Parallel.ForEachAsync over all IBinder registrations
       → uploads meshes and textures to Vulkan device
```

**Key implications:**
- Parts with duplicate Ids are silently dropped (first registration wins).
- `PartGameData` for an Id that has no matching `Part` is held and merged if/when the Part is loaded later (order-independent within a single load pass).
- Meshes and textures are loaded from disk synchronously during `OnDataLoad` but GPU-uploaded asynchronously after all XML parsing is complete.

---

## Common Pitfalls

| Pitfall | Detail |
|---------|--------|
| **Duplicate Id** | If two elements share the same `Id`, the second is silently ignored. Always use globally unique Ids (prefix with your mod Id). |
| **Mesh node name mismatch** | The `Id` in `<Mesh Id="..."/>` must exactly match the mesh node name inside the GLB. Case-sensitive. |
| **Missing `_VM` mesh** | If `<MeshView>` references a node not in the GLB the part may fail to render in the editor thumbnail. Always include a `_VM` node. |
| **No `mod.toml`** | A mod directory without `mod.toml` is ignored entirely. |
| **No Mass defined** | A part without any mass definition behaves as massless. Always define at least `<SolidSphereMass>` somewhere. |
| **Wrong connector scale** | Connectors only mate with matching scale values. If parts won't attach, check that connector scales match on both sides. |
| **Wrong texture format** | The game expects `.ktx2` for GPU textures. PNG works but is inefficient. DDS works for some channels. |
| **Texture Category missing** | All texture references need `Category="Vessel"` on vessel parts — omitting it may cause a load error or incorrect rendering. |
| **Exhaust Id not found** | `<VolumetricExhaust Id="..."/>` must reference an Id registered in `ExhaustAssets.xml` (Core). Use `ApolloRCS` as a safe default. |
| **Combustion Id not found** | `<Combustion Id="..."/>` must reference an Id from `Combustion.xml` (Core). Use `MMH_NTO_1.6` for RCS or `LOX_RP1_2.56` for liquid engines. |
| **Sound Id not found** | `<SoundEvent SoundId="..."/>` must reference a registered sound. Use `DefaultRcsThruster` as a safe default. |
