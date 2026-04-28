# Steely-Eyed Missile Kitten — Implementation Plan

> **Mission monitoring mod for KSA**: Passively monitors vehicle telemetry, detects interesting flight events, evaluates missions from YAML definitions, and persists everything to a local SQLite database.

---

## Table of Contents

1. [Overview & Architecture](#1-overview--architecture)
2. [Project Structure](#2-project-structure)
3. [Task 1 — Scaffold Projects](#task-1--scaffold-projects)
4. [Task 2 — Vehicle Telemetry Provider](#task-2--vehicle-telemetry-provider)
5. [Task 3 — Configurable Monitoring Loop](#task-3--configurable-monitoring-loop)
6. [Task 4 — Event Detection System](#task-4--event-detection-system)
7. [Task 5 — SQLite Persistence Layer](#task-5--sqlite-persistence-layer)
8. [Task 6 — YAML Mission Definition & Evaluation](#task-6--yaml-mission-definition--evaluation)
9. [Task 7 — ImGui UI](#task-7--imgui-ui)
10. [Task 8 — Standalone Mod Entry Point](#task-8--standalone-mod-entry-point)
11. [Task 9 — Integration Testing & Polish](#task-9--integration-testing--polish)
12. [Appendix A — KSA API Reference](#appendix-a--ksa-api-reference)
13. [Appendix B — Event Type Catalog](#appendix-b--event-type-catalog)
14. [Appendix C — Example Mission YAML](#appendix-c--example-mission-yaml)

---

## 1. Overview & Architecture

### Design Principles

1. **Abstracted Telemetry Access** — All KSA game-state reads are co-located in a single `VehicleTelemetry` class. When KSA changes its APIs, only this one file needs updating.
2. **Passive Monitoring** — The monitor samples telemetry at a configurable rate (default 500 ms) and compares state snapshots to detect events. No Harmony patches needed for data reading.
3. **All Vehicles** — The system monitors every vehicle in `Universe.CurrentSystem.Vehicles` simultaneously, each with its own telemetry snapshot and event history.
4. **Event-Driven Architecture** — Detected events are published through a simple `Action<FlightEvent>` delegate. Consumers (SQLite writer, mission evaluator, UI) subscribe independently.
5. **Local Persistence** — Events and mission progress are persisted to a SQLite database in the mod's data directory (`~/.unscience/steely-eyed-missile-kitten/`).
6. **YAML Missions** — Mission definitions are loaded from `.yaml` files with JSON Schema support for IDE validation.
7. **Standalone Mod** — Runs independently with its own F11 ImGui window. No ISubmod/unscience integration for now.

### High-Level Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                    steely-eyed-missile-kitten (Mod)               │
│  Mod.cs ─ StarMap lifecycle, F11 window, orchestrates everything │
│  Patcher.cs ─ Harmony init + HotkeyGuard only                   │
└──────────┬───────────────────────────────────────────────────────┘
           │ references
┌──────────▼───────────────────────────────────────────────────────┐
│              steely-eyed-missile-kitten.lib                       │
│                                                                   │
│  ┌─────────────────┐   ┌──────────────────┐                      │
│  │ VehicleTelemetry │   │  TelemetrySnapshot│                     │
│  │  (KSA API reads) │──▶│  (POCO per vehicle)│                    │
│  └─────────────────┘   └──────────────────┘                      │
│           │                                                       │
│  ┌────────▼────────┐                                              │
│  │ MonitoringLoop   │  Configurable interval, iterates vehicles   │
│  │  (accumulator)   │  Produces TelemetrySnapshot per vehicle     │
│  └────────┬────────┘                                              │
│           │ compares previous ↔ current snapshot                  │
│  ┌────────▼────────┐                                              │
│  │ EventDetector    │  Rule-based event detection                 │
│  │  (stateless fns) │  Emits FlightEvent objects                  │
│  └────────┬────────┘                                              │
│           │ publishes via Action<FlightEvent>                     │
│     ┌─────┴──────┬───────────────┐                                │
│     ▼            ▼               ▼                                │
│  ┌──────┐  ┌───────────┐  ┌────────────┐                         │
│  │SQLite│  │ Mission    │  │ UI Event   │                         │
│  │Writer│  │ Evaluator  │  │ Feed       │                         │
│  └──────┘  └───────────┘  └────────────┘                         │
│                │                                                  │
│         ┌──────▼──────┐                                           │
│         │ MissionDef  │  YAML loader + evaluator                  │
│         │ (conditions)│                                            │
│         └─────────────┘                                           │
└───────────────────────────────────────────────────────────────────┘
```

---

## 2. Project Structure

```
steely-eyed-missile-kitten/             # Standalone mod (entry point)
├── Mod.cs                              # StarMap lifecycle + ImGui window
├── Patcher.cs                          # Harmony + HotkeyGuard
├── mod.toml                            # Mod metadata
├── steely-eyed-missile-kitten.csproj   # Project file
└── missions/                           # Bundled example mission YAML files
    ├── reach-space.yaml
    ├── orbit-kerbin.yaml
    └── land-on-mun.yaml

steely-eyed-missile-kitten.lib/         # Headless library (all logic)
├── steely-eyed-missile-kitten.lib.csproj
├── README.md
│
├── Telemetry/
│   ├── VehicleTelemetry.cs             # Centralized KSA API reads (THE abstraction layer)
│   ├── TelemetrySnapshot.cs            # Immutable POCO: all vehicle metrics at a point in time
│   └── CoordinateFrames.cs             # Enum for speed reference frames
│
├── Monitoring/
│   ├── MonitoringLoop.cs               # Accumulator-based sampling loop
│   ├── VehicleMonitorState.cs          # Per-vehicle: previous snapshot, event state, timers
│   └── MonitoringConfig.cs             # Configurable intervals, thresholds
│
├── Events/
│   ├── FlightEvent.cs                  # Event data record (type, vehicle, timestamp, details)
│   ├── FlightEventType.cs              # Enum of all event types
│   ├── EventDetector.cs                # Stateless detection: compare two snapshots → events
│   └── EventBus.cs                     # Simple Action<FlightEvent> pub/sub
│
├── Persistence/
│   ├── EventDatabase.cs                # SQLite database manager (schema, insert, query)
│   ├── EventWriter.cs                  # EventBus subscriber → SQLite inserts
│   └── DatabaseSchema.cs               # Schema definitions and migrations
│
├── Missions/
│   ├── MissionDefinition.cs            # POCO: deserialized from YAML
│   ├── MissionCondition.cs             # Condition types (threshold, event-based, composite)
│   ├── MissionLoader.cs                # YAML file discovery + deserialization
│   ├── MissionEvaluator.cs             # Evaluates conditions against telemetry/events
│   ├── MissionState.cs                 # Runtime state: active, progress, completed
│   └── MissionManager.cs              # Lifecycle: load, activate, evaluate, complete
│
└── UI/
    ├── MonitorUI.cs                    # Live telemetry display for all vehicles
    ├── EventFeedUI.cs                  # Scrolling event log with filters
    └── MissionUI.cs                    # Mission list, progress, details
```

---

## Task 1 — Scaffold Projects

### Goal
Create both project directories, `.csproj` files, `mod.toml`, `Patcher.cs`, and a minimal `Mod.cs` that compiles and loads into KSA.

### Steps

#### 1.1 Create `steely-eyed-missile-kitten.lib/` project

Create `steely-eyed-missile-kitten.lib/steely-eyed-missile-kitten.lib.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.SteelyEyedMissileKittenLib</AssemblyName>
    <RootNamespace>MeowSci.SteelyEyedMissileKittenLib</RootNamespace>
    <Description>Headless library for Steely-Eyed Missile Kitten mission monitoring mod</Description>
    <PackageId>MeowSci.SteelyEyedMissileKittenLib</PackageId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
  </ItemGroup>

  <!-- SQLite dependency -->
  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.4" />
  </ItemGroup>

  <!-- YAML parsing -->
  <ItemGroup>
    <PackageReference Include="YamlDotNet" Version="16.3.0" />
  </ItemGroup>

  <!-- KSA game DLL references (not bundled) -->
  <ItemGroup>
    <Reference Include="Brutal.Core.Common" Condition="Exists('$(KSAFolder)Brutal.Core.Common.dll')">
      <HintPath>$(KSAFolder)Brutal.Core.Common.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Brutal.Core.Numerics" Condition="Exists('$(KSAFolder)Brutal.Core.Numerics.dll')">
      <HintPath>$(KSAFolder)Brutal.Core.Numerics.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="KSA" Condition="Exists('$(KSAFolder)KSA.dll')">
      <HintPath>$(KSAFolder)KSA.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

Create subdirectories: `Telemetry/`, `Monitoring/`, `Events/`, `Persistence/`, `Missions/`, `UI/`.

#### 1.2 Create `steely-eyed-missile-kitten/` mod project

Create `steely-eyed-missile-kitten/steely-eyed-missile-kitten.csproj` following the pattern from [`fixme-mod-name/fixme-mod-name.csproj`](fixme-mod-name/fixme-mod-name.csproj):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.SteelyEyedMissileKitten</AssemblyName>
    <DistDir>$(SelectedDistModDir)steely-eyed-missile-kitten\</DistDir>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="StarMap.API" Version="0.3.6" PrivateAssets="all" />
    <PackageReference Include="Lib.Harmony" Version="2.4.2" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\steely-eyed-missile-kitten.lib\steely-eyed-missile-kitten.lib.csproj" />
    <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
  </ItemGroup>

  <!-- KSA game DLL references -->
  <ItemGroup>
    <Reference Include="Brutal.Core.Common" Condition="Exists('$(KSAFolder)Brutal.Core.Common.dll')">
      <HintPath>$(KSAFolder)Brutal.Core.Common.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Brutal.Core.Numerics" Condition="Exists('$(KSAFolder)Brutal.Core.Numerics.dll')">
      <HintPath>$(KSAFolder)Brutal.Core.Numerics.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Brutal.ImGui" Condition="Exists('$(KSAFolder)Brutal.ImGui.dll')">
      <HintPath>$(KSAFolder)Brutal.ImGui.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Brutal.Core.Strings" Condition="Exists('$(KSAFolder)Brutal.Core.Strings.dll')">
      <HintPath>$(KSAFolder)Brutal.Core.Strings.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="KSA" Condition="Exists('$(KSAFolder)KSA.dll')">
      <HintPath>$(KSAFolder)KSA.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <!-- Post-build: copy mod to game directory -->
  <Target Name="CopyCustomContent" AfterTargets="AfterBuild">
    <MakeDir Directories="$(DistDir)" />
    <ItemGroup>
      <FilesToCopy Include="$(OutputPath)mod.toml" />
      <FilesToCopy Include="$(OutputPath)$(AssemblyName).dll" />
      <FilesToCopy Include="$(OutputPath)$(AssemblyName).pdb" />
      <FilesToCopy Include="$(OutputPath)$(AssemblyName).deps.json" />
    </ItemGroup>
    <Copy SourceFiles="@(FilesToCopy)" DestinationFolder="$(DistDir)" />

    <ItemGroup>
      <MeowSciAssemblies Include="$(TargetDir)MeowSci.*.dll;$(TargetDir)MeowSci.*.pdb" />
    </ItemGroup>
    <Copy SourceFiles="@(MeowSciAssemblies)" DestinationFolder="$(DistDir)"
          Condition="'@(MeowSciAssemblies)' != ''" />

    <!-- Copy SQLite native + managed assemblies -->
    <ItemGroup>
      <SqliteAssemblies Include="$(TargetDir)Microsoft.Data.Sqlite*.dll;$(TargetDir)SQLitePCLRaw*.dll" />
    </ItemGroup>
    <Copy SourceFiles="@(SqliteAssemblies)" DestinationFolder="$(DistDir)"
          Condition="'@(SqliteAssemblies)' != ''" />

    <!-- Copy YamlDotNet -->
    <ItemGroup>
      <YamlAssemblies Include="$(TargetDir)YamlDotNet.dll" />
    </ItemGroup>
    <Copy SourceFiles="@(YamlAssemblies)" DestinationFolder="$(DistDir)"
          Condition="'@(YamlAssemblies)' != ''" />

    <!-- Copy mission YAML files -->
    <ItemGroup>
      <MissionFiles Include="missions\*.yaml" />
    </ItemGroup>
    <Copy SourceFiles="@(MissionFiles)" DestinationFolder="$(DistDir)missions\"
          Condition="'@(MissionFiles)' != ''" />
  </Target>
</Project>
```

#### 1.3 Create `mod.toml`

```toml
name = "steely-eyed-missile-kitten"
description = "Mission monitoring, event detection, and achievement tracking for KSA"
version = "0.1.0"
author = "meow sci"

[StarMap]
EntryAssembly = "MeowSci.SteelyEyedMissileKitten"
```

#### 1.4 Create `Patcher.cs`

Follow the canonical pattern from [`fixme-mod-name/Patcher.cs`](fixme-mod-name/Patcher.cs):

```csharp
using HarmonyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.SteelyEyedMissileKitten;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("steely-eyed-missile-kitten");

    public static void Patch()
    {
        _harmony?.PatchAll(typeof(Patcher).Assembly);
        if (_harmony != null) HotkeyGuard.Patch(_harmony);
    }

    public static void Unload()
    {
        if (_harmony != null) HotkeyGuard.Unpatch(_harmony);
        _harmony?.UnpatchAll("steely-eyed-missile-kitten");
        _harmony = null;
    }
}
```

#### 1.5 Create minimal `Mod.cs`

```csharp
using StarMap;
using Brutal.ImGuiApi;

namespace MeowSci.SteelyEyedMissileKitten;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;
    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        Patcher.Patch();
        // TODO: Initialize monitoring, events, missions, persistence
        _isInitialized = true;
        Console.WriteLine("steely-eyed-missile-kitten: loaded");
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        // TODO: MonitoringLoop.Update(dt)
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        if (ImGui.IsKeyPressed(ImGuiKey.F11))
            _windowVisible = !_windowVisible;
        if (_windowVisible)
            RenderWindow();
    }

    [StarMapUnload]
    public void Unload()
    {
        // TODO: Dispose monitoring, close database
        Patcher.Unload();
        _isDisposed = true;
        Console.WriteLine("steely-eyed-missile-kitten: unloaded");
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new Brutal.Numerics.float2(800, 600), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Steely-Eyed Missile Kitten", ref _windowVisible))
        {
            ImGui.Text("Mission Monitor — Coming Soon");
        }
        ImGui.End();
    }
}
```

#### 1.6 Add both projects to the solution

```bash
dotnet sln ksa-mod-experiments.slnx add steely-eyed-missile-kitten.lib/steely-eyed-missile-kitten.lib.csproj
dotnet sln ksa-mod-experiments.slnx add steely-eyed-missile-kitten/steely-eyed-missile-kitten.csproj
```

#### 1.7 Verify

Run `dotnet build` and ensure both projects compile successfully.

### Acceptance Criteria
- [ ] Both `.csproj` files exist and are in the solution
- [ ] `dotnet build` succeeds
- [ ] All subdirectory structure created in `.lib`
- [ ] `mod.toml` has correct metadata
- [ ] `Patcher.cs` applies HotkeyGuard

---

## Task 2 — Vehicle Telemetry Provider

### Goal
Create the centralized abstraction layer that reads ALL vehicle metrics from KSA APIs. This is **the single file that needs updating when KSA changes its API**.

### File: `steely-eyed-missile-kitten.lib/Telemetry/VehicleTelemetry.cs`

This static class reads raw data from a `Vehicle` instance and returns a `TelemetrySnapshot`. All KSA API calls are co-located here.

### Steps

#### 2.1 Create `CoordinateFrames.cs`

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Telemetry;

/// <summary>Speed reference frame for display purposes.</summary>
public enum SpeedFrame
{
    Orbital,   // CCI-frame velocity magnitude
    Surface,   // Velocity relative to rotating body surface
    Inertial   // Ecliptic-frame velocity magnitude
}
```

#### 2.2 Create `TelemetrySnapshot.cs`

An immutable POCO capturing ALL monitored metrics at a single point in time:

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Telemetry;

/// <summary>
/// Immutable snapshot of a vehicle's telemetry at a specific point in time.
/// All distances in meters, speeds in m/s, masses in kg, times in seconds.
/// </summary>
public sealed class TelemetrySnapshot
{
    // Identity
    public required string VehicleId { get; init; }
    public required string VehicleName { get; init; }
    public required double TimestampSec { get; init; }  // SimTime in seconds

    // Parent body
    public required string ParentBodyId { get; init; }
    public required string ParentBodyName { get; init; }
    public required bool ParentHasAtmosphere { get; init; }
    public required double ParentAtmosphereHeightM { get; init; } // 0 if no atmosphere

    // Altitude
    public required double BarometricAltitudeM { get; init; }
    public required double RadarAltitudeM { get; init; }

    // Speed (multiple frames)
    public required double OrbitalSpeedMps { get; init; }
    public required double SurfaceSpeedMps { get; init; }
    public required double InertialSpeedMps { get; init; }

    // Orbital parameters
    public required double ApoapsisM { get; init; }         // from body center
    public required double PeriapsisM { get; init; }         // from body center
    public required double ApoapsisAltitudeM { get; init; }  // above surface
    public required double PeriapsisAltitudeM { get; init; } // above surface
    public required double Eccentricity { get; init; }
    public required double Inclination { get; init; }
    public required double OrbitalPeriodSec { get; init; }
    public required double SemiMajorAxisM { get; init; }

    // Mass
    public required double TotalMassKg { get; init; }
    public required double InertMassKg { get; init; }
    public required double PropellantMassKg { get; init; }

    // G-forces
    public required double GForceMagnitude { get; init; }  // in g's (divided by 9.80665)
    public required double AccelX { get; init; }           // body-frame longitudinal
    public required double AccelY { get; init; }           // body-frame lateral
    public required double AccelZ { get; init; }           // body-frame normal

    // Vehicle state
    public required string Situation { get; init; }        // Situation enum as string
    public required bool HasSurfaceContact { get; init; }
    public required bool IsLanded { get; init; }           // Situation == Landed || Floating
    public required bool IsInAtmosphere { get; init; }     // altitude < atmosphere height
    public required double AtmosphericPressurePa { get; init; }
    public required double AtmosphericDensity { get; init; }

    // Position (ecliptic, for inter-vehicle distance)
    public required double PosEclX { get; init; }
    public required double PosEclY { get; init; }
    public required double PosEclZ { get; init; }
}
```

#### 2.3 Create `VehicleTelemetry.cs`

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Telemetry;

/// <summary>
/// Centralized KSA API access layer. ALL game-state reads go through here.
/// When KSA changes its API, only this file needs updating.
/// </summary>
public static class VehicleTelemetry
{
    private const double StandardGravity = 9.80665;

    /// <summary>
    /// Reads all telemetry from a Vehicle and returns an immutable snapshot.
    /// </summary>
    public static TelemetrySnapshot CaptureSnapshot(Vehicle vehicle, double simTimeSec)
    {
        // ... implementation reading from Vehicle APIs
        // See Appendix A for all API calls needed
    }

    /// <summary>
    /// Computes the ecliptic-frame distance between two vehicles in meters.
    /// Works regardless of whether vehicles share the same SOI parent.
    /// </summary>
    public static double ComputeDistance(Vehicle a, Vehicle b)
    {
        // Use GetPositionEcl() for universal distance calculation
        // See: decomp/ksa/KSA/Vehicle.cs:490-493
    }
}
```

**Key KSA API calls** (all documented in [Appendix A](#appendix-a--ksa-api-reference)):

| Metric | KSA API Call | Source File |
|--------|-------------|-------------|
| Barometric altitude | `vehicle.GetBarometricAltitude()` | [`decomp/ksa/KSA/Vehicle.cs:1449-1452`](decomp/ksa/KSA/Vehicle.cs) |
| Radar altitude | `vehicle.GetRadarAltitude()` | [`decomp/ksa/KSA/Vehicle.cs:1454-1479`](decomp/ksa/KSA/Vehicle.cs) |
| Orbital speed | `vehicle.OrbitalSpeed` | [`decomp/ksa/KSA/Vehicle.cs:439`](decomp/ksa/KSA/Vehicle.cs) |
| Surface speed | `vehicle.GetSurfaceSpeed()` | [`decomp/ksa/KSA/Vehicle.cs:1435-1447`](decomp/ksa/KSA/Vehicle.cs) |
| Inertial speed | `vehicle.GetInertialSpeed()` | [`decomp/ksa/KSA/Vehicle.cs:1430-1433`](decomp/ksa/KSA/Vehicle.cs) |
| Parent body | `vehicle.Orbit.Parent` | [`decomp/ksa/KSA/IOrbiter.cs:17`](decomp/ksa/KSA/IOrbiter.cs) |
| Apoapsis | `vehicle.Orbit.Apoapsis` | [`decomp/ksa/KSA/Orbit.cs:1055`](decomp/ksa/KSA/Orbit.cs) |
| Periapsis | `vehicle.Orbit.Periapsis` | [`decomp/ksa/KSA/Orbit.cs:1053`](decomp/ksa/KSA/Orbit.cs) |
| Eccentricity | `vehicle.Orbit.Eccentricity` | [`decomp/ksa/KSA/Orbit.cs:1041`](decomp/ksa/KSA/Orbit.cs) |
| Inclination | `vehicle.Orbit.Inclination` | [`decomp/ksa/KSA/Orbit.cs:1043`](decomp/ksa/KSA/Orbit.cs) |
| Period | `vehicle.Orbit.Period` | [`decomp/ksa/KSA/Orbit.cs:1057`](decomp/ksa/KSA/Orbit.cs) |
| Semi-major axis | `vehicle.Orbit.SemiMajorAxis` | [`decomp/ksa/KSA/Orbit.cs:1045`](decomp/ksa/KSA/Orbit.cs) |
| Total mass | `vehicle.TotalMass` | [`decomp/ksa/KSA/Vehicle.cs:421`](decomp/ksa/KSA/Vehicle.cs) |
| Inert mass | `vehicle.InertMass` | [`decomp/ksa/KSA/Vehicle.cs:423`](decomp/ksa/KSA/Vehicle.cs) |
| Propellant mass | `vehicle.PropellantMass` | [`decomp/ksa/KSA/Vehicle.cs:425`](decomp/ksa/KSA/Vehicle.cs) |
| Acceleration (body frame) | `vehicle.AccelerationBody` | [`decomp/ksa/KSA/Vehicle.cs`](decomp/ksa/KSA/Vehicle.cs) |
| Situation enum | `vehicle.Situation` | [`decomp/ksa/KSA/Situation.cs`](decomp/ksa/KSA/Situation.cs) |
| Surface contact | `vehicle.Situation.HasAnyContact()` | [`decomp/ksa/KSA/SituationEx.cs`](decomp/ksa/KSA/SituationEx.cs) |
| Position (ecliptic) | `vehicle.GetPositionEcl()` | [`decomp/ksa/KSA/Vehicle.cs:490-493`](decomp/ksa/KSA/Vehicle.cs) |
| Atmosphere ref | `vehicle.Parent.GetAtmosphereReference()` | [`decomp/ksa/KSA/Astronomical.cs:274-277`](decomp/ksa/KSA/Astronomical.cs) |
| Atm. pressure | `atmosphereRef.Physical.GetAtmosphericPressureAtAltitude(alt)` | [`decomp/ksa/KSA/PhysicalAtmosphereReference.cs`](decomp/ksa/KSA/PhysicalAtmosphereReference.cs) |
| Atm. height | `atmosphereRef.Physical.Height` | [`decomp/ksa/KSA/PhysicalAtmosphereReference.cs`](decomp/ksa/KSA/PhysicalAtmosphereReference.cs) |
| Parent mean radius | `vehicle.Parent.MeanRadius` | [`decomp/ksa/KSA/IParentBody.cs`](decomp/ksa/KSA/IParentBody.cs) |
| Parent SOI | `vehicle.Parent.SphereOfInfluence` | [`decomp/ksa/KSA/IParentBody.cs`](decomp/ksa/KSA/IParentBody.cs) |

**Important notes for implementation:**
- `Orbit.Apoapsis` may return `NaN` or `double.PositiveInfinity` for unbound (hyperbolic/parabolic) orbits — handle gracefully with `double.IsFinite()` checks
- `GetRadarAltitude()` only works when parent is `Celestial` — falls back to barometric
- `GetSurfaceSpeed()` returns inertial speed when parent has no angular velocity
- `GetAtmosphereReference()` returns `null` for bodies without atmosphere
- Cast `vehicle.Parent` to get atmosphere: `(vehicle.Orbit.Parent as Astronomical)?.GetAtmosphereReference()`
- Wrap all reads in try/catch to handle null/disposed vehicle references gracefully

### Acceptance Criteria
- [ ] `VehicleTelemetry.CaptureSnapshot()` reads all metrics from a `Vehicle`
- [ ] `TelemetrySnapshot` captures all required data fields
- [ ] All KSA API access is in `VehicleTelemetry.cs` only — no other file touches KSA Vehicle APIs directly
- [ ] NaN/Infinity orbital parameters handled gracefully
- [ ] Null parent body handled gracefully
- [ ] Distance calculation works across different SOI parents via ecliptic coordinates
- [ ] Compiles successfully

---

## Task 3 — Configurable Monitoring Loop

### Goal
Build the accumulator-based monitoring loop that samples telemetry for ALL vehicles at a configurable interval.

### Pattern Reference
Follow the pattern from [`geeforce.lib/GeeForceSubmod.cs`](geeforce.lib/GeeForceSubmod.cs) lines 12-35 — accumulator-based timer that decouples sampling from frame rate.

### Steps

#### 3.1 Create `MonitoringConfig.cs`

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Monitoring;

public sealed class MonitoringConfig
{
    /// <summary>Interval between telemetry samples in seconds. Default 0.5s (2 Hz).</summary>
    public double SampleIntervalSec { get; set; } = 0.5;

    /// <summary>Minimum allowed interval (50ms = 20 Hz max).</summary>
    public const double MinIntervalSec = 0.05;

    /// <summary>Maximum allowed interval (10s).</summary>
    public const double MaxIntervalSec = 10.0;
}
```

#### 3.2 Create `VehicleMonitorState.cs`

Per-vehicle state tracking for event detection (stores "previous" snapshot for comparison):

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Monitoring;

/// <summary>
/// Tracks per-vehicle monitoring state across sample ticks.
/// Stores the previous snapshot for event comparison.
/// </summary>
public sealed class VehicleMonitorState
{
    public string VehicleId { get; }
    public TelemetrySnapshot? PreviousSnapshot { get; set; }
    public TelemetrySnapshot? CurrentSnapshot { get; set; }

    // Debounce timers for events that shouldn't fire repeatedly
    public double LastSoiChangeTimeSec { get; set; }
    public double LastLandingTimeSec { get; set; }
    public double LastLiftoffTimeSec { get; set; }
    public double LastAtmosphereEntryTimeSec { get; set; }
    public double LastAtmosphereExitTimeSec { get; set; }

    public VehicleMonitorState(string vehicleId) => VehicleId = vehicleId;
}
```

#### 3.3 Create `MonitoringLoop.cs`

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Monitoring;

/// <summary>
/// Accumulator-based monitoring loop. Call Update(dt) every frame.
/// Samples telemetry for all vehicles at the configured interval.
/// </summary>
public sealed class MonitoringLoop
{
    private readonly MonitoringConfig _config;
    private readonly EventDetector _detector;
    private readonly EventBus _eventBus;
    private readonly Dictionary<string, VehicleMonitorState> _vehicleStates = new();
    private double _accumulator;

    public MonitoringLoop(MonitoringConfig config, EventDetector detector, EventBus eventBus) { ... }

    /// <summary>Called every frame from Mod.OnBeforeUi(dt).</summary>
    public void Update(double dt)
    {
        _accumulator += dt;
        while (_accumulator >= _config.SampleIntervalSec)
        {
            _accumulator -= _config.SampleIntervalSec;
            SampleAllVehicles();
        }
    }

    private void SampleAllVehicles()
    {
        var simTime = SimTimeProvider.GetElapsedTime().Seconds();
        var vehicles = VehicleProvider.GetAllVehicles();

        // Prune states for vehicles that no longer exist
        PruneStaleVehicles(vehicles);

        foreach (var vehicle in vehicles)
        {
            var state = GetOrCreateState(vehicle.Id);
            state.PreviousSnapshot = state.CurrentSnapshot;
            state.CurrentSnapshot = VehicleTelemetry.CaptureSnapshot(vehicle, simTime);

            if (state.PreviousSnapshot != null)
            {
                var events = _detector.DetectEvents(state);
                foreach (var evt in events)
                    _eventBus.Publish(evt);
            }
        }
    }

    // ... helper methods
}
```

**Key design decisions:**
- Accumulator pattern ensures consistent sampling regardless of frame rate
- `Dictionary<string, VehicleMonitorState>` keyed by vehicle ID tracks per-vehicle state
- Stale vehicles (destroyed/removed) are pruned each sample tick
- New vehicles are automatically picked up on next tick
- Event detection runs after each sample, comparing previous → current snapshot

### Acceptance Criteria
- [ ] Accumulator decouples sampling from frame rate
- [ ] All vehicles in `Universe.CurrentSystem.Vehicles` are monitored
- [ ] New vehicles automatically detected, stale vehicles pruned
- [ ] Config allows runtime interval adjustment (clamped to min/max)
- [ ] Compiles successfully

---

## Task 4 — Event Detection System

### Goal
Build stateless event detection that compares two consecutive `TelemetrySnapshot`s and emits `FlightEvent` objects.

### Steps

#### 4.1 Create `FlightEventType.cs`

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Events;

public enum FlightEventType
{
    // SOI changes
    SoiChanged,              // Parent body changed

    // Surface transitions
    Liftoff,                 // Was landed/floating → no longer has surface contact
    Landed,                  // Was no surface contact → now Landed/Floating
    SplashDown,              // Specifically: contact type became Ocean

    // Atmosphere transitions
    AtmosphereEntered,       // Was NOT in atmosphere → now in atmosphere
    AtmosphereExited,        // Was in atmosphere → now NOT in atmosphere

    // Milestone thresholds (configurable)
    AltitudeReached,         // Crossed an altitude threshold upward
    SpeedReached,            // Crossed a speed threshold

    // Orbital milestones
    StableOrbitAchieved,     // Periapsis went above atmosphere (or surface if no atm)
    OrbitEscaped,            // Eccentricity went >= 1.0 (hyperbolic)
}
```

#### 4.2 Create `FlightEvent.cs`

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Events;

/// <summary>
/// Represents a detected flight event. Immutable.
/// </summary>
public sealed class FlightEvent
{
    public required FlightEventType Type { get; init; }
    public required string VehicleId { get; init; }
    public required string VehicleName { get; init; }
    public required double TimestampSec { get; init; }
    public required string ParentBodyId { get; init; }
    public required string Description { get; init; }

    /// <summary>Optional structured details (e.g., old/new SOI, altitude reached).</summary>
    public Dictionary<string, string> Details { get; init; } = new();
}
```

#### 4.3 Create `EventBus.cs`

Simple pub/sub:

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Events;

public sealed class EventBus
{
    public event Action<FlightEvent>? OnEvent;

    public void Publish(FlightEvent evt)
    {
        OnEvent?.Invoke(evt);
    }
}
```

#### 4.4 Create `EventDetector.cs`

Stateless functions that compare previous and current snapshots:

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Events;

/// <summary>
/// Compares two consecutive TelemetrySnapshots for a vehicle and returns detected events.
/// All detection logic is stateless — state lives in VehicleMonitorState.
/// </summary>
public sealed class EventDetector
{
    private const double EventDebounceSec = 2.0; // Min time between same event type

    public List<FlightEvent> DetectEvents(VehicleMonitorState state)
    {
        var events = new List<FlightEvent>();
        var prev = state.PreviousSnapshot!;
        var curr = state.CurrentSnapshot!;

        CheckSoiChange(prev, curr, state, events);
        CheckLiftoff(prev, curr, state, events);
        CheckLanding(prev, curr, state, events);
        CheckAtmosphereTransition(prev, curr, state, events);
        CheckStableOrbit(prev, curr, state, events);
        CheckOrbitEscape(prev, curr, state, events);

        return events;
    }

    // Each method is a focused, testable detection function
}
```

**Detection logic for each event:**

| Event | Detection Rule | KSA Reference |
|-------|---------------|---------------|
| `SoiChanged` | `prev.ParentBodyId != curr.ParentBodyId` | SOI transition in [`KinematicStates.cs:292-338`](decomp/ksa/KSA/KinematicStates.cs) |
| `Liftoff` | `prev.IsLanded == true && curr.IsLanded == false && !curr.HasSurfaceContact` | Situation enum: `Landed` → `Maneuvering` / `Freefall`. See [`Situation.cs`](decomp/ksa/KSA/Situation.cs) |
| `Landed` | `prev.HasSurfaceContact == false && curr.IsLanded == true` | `SurfaceContact.Terrain` + `IsOnRails`. See [`SituationEx.cs`](decomp/ksa/KSA/SituationEx.cs) |
| `SplashDown` | `prev.HasSurfaceContact == false && curr.Situation == "Floating"` or `"Sailing"` | `SurfaceContact.Ocean`. See [`SurfaceContact.cs`](decomp/ksa/KSA/SurfaceContact.cs) |
| `AtmosphereEntered` | `prev.IsInAtmosphere == false && curr.IsInAtmosphere == true` | Altitude < `AtmosphereReference.Physical.Height`. See [`PhysicalAtmosphereReference.cs`](decomp/ksa/KSA/PhysicalAtmosphereReference.cs) |
| `AtmosphereExited` | `prev.IsInAtmosphere == true && curr.IsInAtmosphere == false` | Same check, reversed |
| `StableOrbitAchieved` | `prev.PeriapsisAltitudeM` was below atmosphere/surface AND `curr.PeriapsisAltitudeM` is above AND `curr.Eccentricity < 1.0` | Orbit is bound (`e < 1`) and periapsis clears the surface/atmosphere |
| `OrbitEscaped` | `prev.Eccentricity < 1.0 && curr.Eccentricity >= 1.0` | Hyperbolic orbit. See [`Orbit.cs`](decomp/ksa/KSA/Orbit.cs) |

**Debouncing:** Each event type on `VehicleMonitorState` has a `Last*TimeSec` field. An event only fires if `currTime - lastEventTime > EventDebounceSec`. This prevents rapid-fire events during physics settling.

### Acceptance Criteria
- [ ] All 8+ event types detected correctly
- [ ] Debouncing prevents duplicate events
- [ ] Events include meaningful descriptions and structured details
- [ ] Detection is purely comparison-based (no KSA API calls — uses snapshots only)
- [ ] Compiles successfully

---

## Task 5 — SQLite Persistence Layer

### Goal
Persist all flight events and telemetry snapshots to a local SQLite database.

### Data Directory
`{USERPROFILE}/Documents/My Games/Kitten Space Agency/.steely-eyed-missile-kitten/`

This matches the pattern used by other mods (e.g., con-man uses `.con-man/`).

### Steps

#### 5.1 Create `DatabaseSchema.cs`

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Persistence;

public static class DatabaseSchema
{
    public const int CurrentVersion = 1;

    public const string CreateEventsTable = @"
        CREATE TABLE IF NOT EXISTS flight_events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            event_type TEXT NOT NULL,
            vehicle_id TEXT NOT NULL,
            vehicle_name TEXT NOT NULL,
            timestamp_sec REAL NOT NULL,
            parent_body_id TEXT NOT NULL,
            description TEXT NOT NULL,
            details_json TEXT,              -- JSON serialized Dictionary<string,string>
            created_at TEXT DEFAULT (datetime('now'))
        );

        CREATE INDEX IF NOT EXISTS idx_events_vehicle ON flight_events(vehicle_id);
        CREATE INDEX IF NOT EXISTS idx_events_type ON flight_events(event_type);
        CREATE INDEX IF NOT EXISTS idx_events_timestamp ON flight_events(timestamp_sec);
    ";

    public const string CreateMissionProgressTable = @"
        CREATE TABLE IF NOT EXISTS mission_progress (
            mission_id TEXT NOT NULL,
            vehicle_id TEXT NOT NULL,
            status TEXT NOT NULL DEFAULT 'active',  -- active, completed, failed, abandoned
            started_at_sec REAL NOT NULL,
            completed_at_sec REAL,
            progress_json TEXT,                      -- JSON: per-condition completion state
            PRIMARY KEY (mission_id, vehicle_id)
        );
    ";

    public const string CreateSchemaVersionTable = @"
        CREATE TABLE IF NOT EXISTS schema_version (
            version INTEGER PRIMARY KEY
        );
    ";
}
```

#### 5.2 Create `EventDatabase.cs`

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Persistence;

/// <summary>
/// SQLite database manager. Handles schema creation, event insertion, and queries.
/// Uses Microsoft.Data.Sqlite.
/// </summary>
public sealed class EventDatabase : IDisposable
{
    private SqliteConnection _connection;

    public EventDatabase(string databasePath) { ... }

    public void Initialize() { /* Create tables, run migrations */ }
    public void InsertEvent(FlightEvent evt) { ... }
    public List<FlightEvent> QueryEvents(string? vehicleId = null, FlightEventType? type = null, int limit = 100) { ... }
    public void SaveMissionProgress(string missionId, string vehicleId, MissionState state) { ... }
    public MissionState? LoadMissionProgress(string missionId, string vehicleId) { ... }
    public void Dispose() { _connection?.Dispose(); }
}
```

**Important:** Use parameterized queries to avoid SQL injection. Batch inserts during high-frequency events.

#### 5.3 Create `EventWriter.cs`

Subscribes to `EventBus` and writes to SQLite:

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Persistence;

/// <summary>
/// Subscribes to the EventBus and persists events to SQLite.
/// Batches writes to avoid per-event I/O overhead.
/// </summary>
public sealed class EventWriter : IDisposable
{
    private readonly EventDatabase _db;
    private readonly List<FlightEvent> _pendingWrites = new();
    private readonly object _lock = new();
    private const int BatchSize = 10;

    public EventWriter(EventDatabase db, EventBus eventBus)
    {
        _db = db;
        eventBus.OnEvent += OnEvent;
    }

    private void OnEvent(FlightEvent evt)
    {
        lock (_lock) { _pendingWrites.Add(evt); }
    }

    /// <summary>Call periodically (e.g., every few seconds) to flush pending writes.</summary>
    public void Flush()
    {
        List<FlightEvent> toWrite;
        lock (_lock)
        {
            if (_pendingWrites.Count == 0) return;
            toWrite = new List<FlightEvent>(_pendingWrites);
            _pendingWrites.Clear();
        }
        foreach (var evt in toWrite)
            _db.InsertEvent(evt);
    }

    public void Dispose() { Flush(); }
}
```

### Acceptance Criteria
- [ ] SQLite database created in correct mod data directory
- [ ] Schema versioning supports future migrations
- [ ] Events are persisted with all fields
- [ ] Batch writing reduces I/O overhead
- [ ] Database properly disposed on mod unload
- [ ] Thread-safe write batching (EventBus may fire from game thread)
- [ ] Compiles successfully

---

## Task 6 — YAML Mission Definition & Evaluation

### Goal
Load mission definitions from YAML files and evaluate them against live telemetry and events.

### Steps

#### 6.1 Create `MissionCondition.cs`

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Missions;

/// <summary>The type of condition to evaluate.</summary>
public enum ConditionType
{
    // Threshold conditions (compare telemetry value to target)
    AltitudeAbove,          // BarometricAltitudeM > value
    AltitudeBelow,          // BarometricAltitudeM < value
    SpeedAbove,             // Speed (in specified frame) > value
    SpeedBelow,             // Speed < value
    ApoapsisAbove,          // ApoapsisAltitudeM > value
    PeriapsisAbove,         // PeriapsisAltitudeM > value
    PeriapsisBelow,         // PeriapsisAltitudeM < value
    EccentricityBelow,      // Eccentricity < value (for circular orbit check)
    InclinationBetween,     // min <= Inclination <= max

    // Event conditions (an event of this type must have occurred)
    EventOccurred,          // A specific FlightEventType fired

    // Location conditions
    InSoiOf,                // Currently in SOI of specified body
    OnSurfaceOf,            // Landed on specified body

    // Composite
    AllOf,                  // All sub-conditions must be met simultaneously
    AnyOf,                  // Any one sub-condition met
    Sequence,               // Sub-conditions must be met in order (sequential)
}

/// <summary>A single mission condition deserialized from YAML.</summary>
public sealed class MissionCondition
{
    public required ConditionType Type { get; set; }

    // For threshold conditions
    public double? Value { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public SpeedFrame? SpeedFrame { get; set; }  // Which speed frame for speed conditions

    // For event conditions
    public FlightEventType? EventType { get; set; }

    // For location conditions
    public string? BodyId { get; set; }

    // For composite conditions
    public List<MissionCondition>? SubConditions { get; set; }

    // Description for UI display
    public string? Description { get; set; }
}
```

#### 6.2 Create `MissionDefinition.cs`

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Missions;

/// <summary>A complete mission definition loaded from YAML.</summary>
public sealed class MissionDefinition
{
    public required string Id { get; set; }          // Unique identifier (filename-based)
    public required string Name { get; set; }        // Display name
    public string Description { get; set; } = "";    // Longer description
    public string? Category { get; set; }            // Optional grouping (e.g., "orbital", "landing")
    public int Difficulty { get; set; } = 1;         // 1-5 difficulty rating

    /// <summary>The root condition tree. All conditions must be satisfied.</summary>
    public required MissionCondition Objective { get; set; }
}
```

#### 6.3 Create `MissionLoader.cs`

Discovers and loads YAML files using YamlDotNet:

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Missions;

/// <summary>
/// Discovers and deserializes mission YAML files.
/// Searches both bundled missions (mod directory) and user missions (data directory).
/// </summary>
public static class MissionLoader
{
    public static List<MissionDefinition> LoadAllMissions(string bundledDir, string userDir)
    {
        var missions = new List<MissionDefinition>();

        // Load from bundled missions directory
        if (Directory.Exists(bundledDir))
        {
            foreach (var file in Directory.GetFiles(bundledDir, "*.yaml"))
                missions.Add(LoadMission(file));
        }

        // Load from user missions directory (override bundled if same ID)
        if (Directory.Exists(userDir))
        {
            foreach (var file in Directory.GetFiles(userDir, "*.yaml"))
                missions.Add(LoadMission(file));
        }

        return missions;
    }

    public static MissionDefinition LoadMission(string filePath)
    {
        var yaml = File.ReadAllText(filePath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var mission = deserializer.Deserialize<MissionDefinition>(yaml);
        mission.Id ??= Path.GetFileNameWithoutExtension(filePath);
        return mission;
    }
}
```

#### 6.4 Create `MissionState.cs`

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Missions;

public enum MissionStatus { Active, Completed, Failed, Abandoned }

/// <summary>Runtime state for an active mission on a specific vehicle.</summary>
public sealed class MissionState
{
    public MissionStatus Status { get; set; } = MissionStatus.Active;
    public double StartedAtSec { get; set; }
    public double? CompletedAtSec { get; set; }

    /// <summary>Per-condition completion state (for Sequence conditions).</summary>
    public Dictionary<int, bool> ConditionProgress { get; set; } = new();
}
```

#### 6.5 Create `MissionEvaluator.cs`

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Missions;

/// <summary>
/// Evaluates mission conditions against current telemetry and event history.
/// Called each monitoring tick for active missions.
/// </summary>
public static class MissionEvaluator
{
    /// <summary>
    /// Evaluates a condition tree against the current telemetry snapshot.
    /// </summary>
    public static bool Evaluate(
        MissionCondition condition,
        TelemetrySnapshot snapshot,
        List<FlightEvent> eventHistory,
        MissionState state)
    {
        return condition.Type switch
        {
            ConditionType.AltitudeAbove => snapshot.BarometricAltitudeM > condition.Value,
            ConditionType.AltitudeBelow => snapshot.BarometricAltitudeM < condition.Value,
            ConditionType.SpeedAbove => GetSpeed(snapshot, condition.SpeedFrame) > condition.Value,
            ConditionType.ApoapsisAbove => snapshot.ApoapsisAltitudeM > condition.Value,
            ConditionType.PeriapsisAbove => snapshot.PeriapsisAltitudeM > condition.Value,
            ConditionType.EccentricityBelow => snapshot.Eccentricity < condition.Value,
            ConditionType.InSoiOf => snapshot.ParentBodyId == condition.BodyId,
            ConditionType.OnSurfaceOf => snapshot.IsLanded && snapshot.ParentBodyId == condition.BodyId,
            ConditionType.EventOccurred => eventHistory.Any(e => e.Type == condition.EventType),
            ConditionType.AllOf => condition.SubConditions!.All(c => Evaluate(c, snapshot, eventHistory, state)),
            ConditionType.AnyOf => condition.SubConditions!.Any(c => Evaluate(c, snapshot, eventHistory, state)),
            ConditionType.Sequence => EvaluateSequence(condition, snapshot, eventHistory, state),
            // ... other types
            _ => false
        };
    }
}
```

#### 6.6 Create `MissionManager.cs`

Lifecycle management for active missions:

```csharp
namespace MeowSci.SteelyEyedMissileKittenLib.Missions;

/// <summary>
/// Manages mission lifecycle: loading definitions, activating missions on vehicles,
/// evaluating progress each tick, and persisting completion.
/// </summary>
public sealed class MissionManager
{
    private readonly List<MissionDefinition> _definitions;
    private readonly Dictionary<(string missionId, string vehicleId), MissionState> _activeMissions;
    private readonly EventDatabase _db;
    private readonly List<FlightEvent> _recentEvents;

    public void ActivateMission(string missionId, string vehicleId) { ... }
    public void AbandonMission(string missionId, string vehicleId) { ... }

    /// <summary>Called each monitoring tick. Evaluates all active missions.</summary>
    public void EvaluateAll(Dictionary<string, TelemetrySnapshot> currentSnapshots)
    {
        foreach (var ((missionId, vehicleId), state) in _activeMissions)
        {
            if (state.Status != MissionStatus.Active) continue;
            if (!currentSnapshots.TryGetValue(vehicleId, out var snapshot)) continue;

            var definition = _definitions.First(d => d.Id == missionId);
            var vehicleEvents = _recentEvents.Where(e => e.VehicleId == vehicleId).ToList();

            if (MissionEvaluator.Evaluate(definition.Objective, snapshot, vehicleEvents, state))
            {
                state.Status = MissionStatus.Completed;
                state.CompletedAtSec = snapshot.TimestampSec;
                _db.SaveMissionProgress(missionId, vehicleId, state);
            }
        }
    }

    public IReadOnlyList<MissionDefinition> Definitions => _definitions;
    public IReadOnlyDictionary<(string, string), MissionState> ActiveMissions => _activeMissions;
}
```

#### 6.7 Create JSON Schema for mission YAML

Create `steely-eyed-missile-kitten/missions/mission-schema.json` for IDE autocompletion and validation.

### Acceptance Criteria
- [ ] YAML missions load and deserialize correctly
- [ ] Condition tree supports threshold, event, location, and composite types
- [ ] `Sequence` conditions enforce order
- [ ] Missions can be activated/abandoned per vehicle
- [ ] Completed missions are persisted to SQLite
- [ ] Example missions compile and evaluate correctly
- [ ] JSON Schema provides IDE validation
- [ ] Compiles successfully

---

## Task 7 — ImGui UI

### Goal
Build the ImGui interface with three tabs: Live Telemetry, Event Feed, and Missions.

### File locations: `steely-eyed-missile-kitten.lib/UI/`

### Steps

#### 7.1 Create `MonitorUI.cs` — Live Telemetry Tab

Displays a table of all monitored vehicles with their current telemetry:

- **Vehicle table** with columns: Name, Parent Body, Altitude, Speed (orbital/surface), Ap/Pe, Mass, G-Force, Situation
- **Configurable sample interval** slider (50ms–10s, drag)
- **Vehicle detail expand** — click a row to see full telemetry breakdown
- **Distance matrix** — show distances between vehicles in same SOI

**ImGui patterns to use:**
- `ImGui.BeginTable()` / `EndTable()` for the vehicle grid
- `ImGui.SliderDouble()` for interval config
- `ImGui.CollapsingHeader()` for vehicle detail sections
- Format large numbers with SI prefixes (km, Mm, Gm)

#### 7.2 Create `EventFeedUI.cs` — Event Feed Tab

Scrolling log of all detected events:

- **Scrollable list** of events, newest first
- **Filter by event type** (checkboxes for each `FlightEventType`)
- **Filter by vehicle** (combobox)
- **Color coding** per event type (green = success, yellow = transition, red = warning)
- **Auto-scroll** toggle to follow newest events
- **Clear button** (clears display only, not database)

#### 7.3 Create `MissionUI.cs` — Missions Tab

Mission management interface:

- **Available missions list** — loaded from YAML files
- **Active missions** — per-vehicle, with progress indicators
- **Mission details panel** — shows description, conditions, and current evaluation state
- **Activate/Abandon buttons** per vehicle+mission combo
- **Completed missions history** — loaded from SQLite

### Acceptance Criteria
- [ ] Three-tab layout (Telemetry, Events, Missions)
- [ ] Live telemetry table updates at sample rate
- [ ] Event feed scrolls with filtering
- [ ] Mission UI shows progress and allows activation
- [ ] Interval slider adjusts monitoring config in real-time
- [ ] Clean, readable ImGui layout
- [ ] Compiles successfully

---

## Task 8 — Standalone Mod Entry Point

### Goal
Wire everything together in `Mod.cs` — initialize all systems, drive the monitoring loop, and render the UI.

### File: `steely-eyed-missile-kitten/Mod.cs`

### Steps

#### 8.1 Full `Mod.cs` Implementation

```csharp
[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;
    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;

    // Core systems
    private MonitoringConfig _config = null!;
    private EventBus _eventBus = null!;
    private EventDetector _detector = null!;
    private MonitoringLoop _monitoringLoop = null!;

    // Persistence
    private EventDatabase _database = null!;
    private EventWriter _eventWriter = null!;

    // Missions
    private MissionManager _missionManager = null!;

    // UI state
    private List<FlightEvent> _uiEventFeed = new();
    private double _flushTimer;
    private const double FlushIntervalSec = 5.0;

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        Patcher.Patch();

        _config = new MonitoringConfig();
        _eventBus = new EventBus();
        _detector = new EventDetector();
        _monitoringLoop = new MonitoringLoop(_config, _detector, _eventBus);

        // Initialize database
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "Kitten Space Agency",
            ".steely-eyed-missile-kitten", "events.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _database = new EventDatabase(dbPath);
        _database.Initialize();

        _eventWriter = new EventWriter(_database, _eventBus);

        // Subscribe UI feed
        _eventBus.OnEvent += evt => _uiEventFeed.Add(evt);

        // Load missions
        var bundledDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "missions");
        var userDir = Path.Combine(Path.GetDirectoryName(dbPath)!, "missions");
        _missionManager = new MissionManager(
            MissionLoader.LoadAllMissions(bundledDir, userDir),
            _database);

        _isInitialized = true;
        Console.WriteLine("steely-eyed-missile-kitten: loaded");
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;

        _monitoringLoop.Update(dt);
        _missionManager.EvaluateAll(_monitoringLoop.CurrentSnapshots);

        // Periodic database flush
        _flushTimer += dt;
        if (_flushTimer >= FlushIntervalSec)
        {
            _flushTimer = 0;
            _eventWriter.Flush();
        }
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        if (ImGui.IsKeyPressed(ImGuiKey.F11))
            _windowVisible = !_windowVisible;
        if (_windowVisible)
            RenderWindow();
    }

    [StarMapUnload]
    public void Unload()
    {
        _eventWriter?.Dispose();
        _database?.Dispose();
        Patcher.Unload();
        _isDisposed = true;
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(900, 700), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Steely-Eyed Missile Kitten 🐱🚀", ref _windowVisible))
        {
            if (ImGui.BeginTabBar("##semk_tabs"))
            {
                if (ImGui.BeginTabItem("Telemetry"))
                {
                    MonitorUI.Render(_monitoringLoop, _config);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Events"))
                {
                    EventFeedUI.Render(_uiEventFeed);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Missions"))
                {
                    MissionUI.Render(_missionManager, _monitoringLoop);
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
        ImGui.End();
    }
}
```

### Acceptance Criteria
- [ ] All systems initialized in correct order
- [ ] Monitoring loop driven from `OnBeforeUi`
- [ ] Database flushed periodically (not every frame)
- [ ] Proper cleanup on Unload (database closed, writer flushed)
- [ ] F11 toggle works
- [ ] Tab bar with all three sections
- [ ] Compiles and links correctly with all dependencies

---

## Task 9 — Integration Testing & Polish

### Goal
Verify everything compiles, create example missions, update repository documentation.

### Steps

#### 9.1 `dotnet build` passes for the entire solution

#### 9.2 Create example mission YAML files

**`missions/reach-space.yaml`:**
```yaml
# yaml-language-server: $schema=mission-schema.json
name: "Reach Space"
description: "Launch a vehicle above 100 km altitude (the Kármán line)"
category: "orbital"
difficulty: 1
objective:
  type: altitude_above
  value: 100000
  description: "Reach 100 km altitude"
```

**`missions/orbit-kerbin.yaml`:**
```yaml
name: "Orbit Kerbin"
description: "Achieve a stable orbit around Kerbin with periapsis above the atmosphere"
category: "orbital"
difficulty: 2
objective:
  type: all_of
  sub_conditions:
    - type: in_soi_of
      body_id: "Kerbin"
      description: "Be in Kerbin's sphere of influence"
    - type: periapsis_above
      value: 70000
      description: "Periapsis above 70 km (atmosphere)"
    - type: eccentricity_below
      value: 1.0
      description: "In a bound (non-escape) orbit"
```

**`missions/land-on-mun.yaml`:**
```yaml
name: "Land on the Mun"
description: "Successfully land a vehicle on the surface of the Mun"
category: "landing"
difficulty: 3
objective:
  type: sequence
  sub_conditions:
    - type: event_occurred
      event_type: atmosphere_exited
      description: "Leave Kerbin's atmosphere"
    - type: event_occurred
      event_type: soi_changed
      description: "Enter the Mun's sphere of influence"
    - type: on_surface_of
      body_id: "Mun"
      description: "Land on the Mun"
```

#### 9.3 Create JSON Schema

Create `missions/mission-schema.json` for IDE validation of YAML files.

#### 9.4 Update `REPOSITORY_INDEX.md`

Add entry for `steely-eyed-missile-kitten` and `steely-eyed-missile-kitten.lib` following the existing format.

#### 9.5 Create `steely-eyed-missile-kitten/README.md`

Detailed documentation of the mod, its features, event types, mission YAML format, and configuration.

#### 9.6 Create `steely-eyed-missile-kitten.lib/README.md`

Library documentation for consumers.

### Acceptance Criteria
- [ ] `dotnet build` succeeds for the full solution
- [ ] 3 example mission YAML files created and loadable
- [ ] JSON Schema validates example missions
- [ ] `REPOSITORY_INDEX.md` updated
- [ ] Both README.md files created
- [ ] No compiler warnings (TreatWarningsAsErrors is enabled)

---

## Appendix A — KSA API Reference

### Vehicle Telemetry APIs

All references are relative to `decomp/ksa/`.

#### Altitude
| API | Returns | File |
|-----|---------|------|
| `vehicle.GetBarometricAltitude()` | `double` — meters above parent body mean radius | [`KSA/Vehicle.cs:1449-1452`](decomp/ksa/KSA/Vehicle.cs) |
| `vehicle.GetRadarAltitude()` | `double` — meters above terrain/ocean | [`KSA/Vehicle.cs:1454-1479`](decomp/ksa/KSA/Vehicle.cs) |

#### Speed
| API | Returns | File |
|-----|---------|------|
| `vehicle.OrbitalSpeed` | `double` — m/s in CCI frame | [`KSA/Vehicle.cs:439`](decomp/ksa/KSA/Vehicle.cs) |
| `vehicle.GetSurfaceSpeed()` | `double` — m/s relative to rotating surface | [`KSA/Vehicle.cs:1435-1447`](decomp/ksa/KSA/Vehicle.cs) |
| `vehicle.GetInertialSpeed()` | `double` — m/s in inertial (non-rotating) frame | [`KSA/Vehicle.cs:1430-1433`](decomp/ksa/KSA/Vehicle.cs) |

#### Orbital Parameters
| API | Returns | File |
|-----|---------|------|
| `vehicle.Orbit.Apoapsis` | `double` — meters from body center | [`KSA/Orbit.cs:1055`](decomp/ksa/KSA/Orbit.cs) |
| `vehicle.Orbit.Periapsis` | `double` — meters from body center | [`KSA/Orbit.cs:1053`](decomp/ksa/KSA/Orbit.cs) |
| `vehicle.Orbit.Eccentricity` | `double` — 0=circle, <1=ellipse, ≥1=escape | [`KSA/Orbit.cs:1041`](decomp/ksa/KSA/Orbit.cs) |
| `vehicle.Orbit.Inclination` | `double` — radians | [`KSA/Orbit.cs:1043`](decomp/ksa/KSA/Orbit.cs) |
| `vehicle.Orbit.SemiMajorAxis` | `double` — meters | [`KSA/Orbit.cs:1045`](decomp/ksa/KSA/Orbit.cs) |
| `vehicle.Orbit.Period` | `double` — seconds (NaN for unbound) | [`KSA/Orbit.cs:1057`](decomp/ksa/KSA/Orbit.cs) |

#### Mass
| API | Returns | File |
|-----|---------|------|
| `vehicle.TotalMass` | `float` — kg total | [`KSA/Vehicle.cs:421`](decomp/ksa/KSA/Vehicle.cs) |
| `vehicle.InertMass` | `float` — kg dry mass | [`KSA/Vehicle.cs:423`](decomp/ksa/KSA/Vehicle.cs) |
| `vehicle.PropellantMass` | `float` — kg propellant | [`KSA/Vehicle.cs:425`](decomp/ksa/KSA/Vehicle.cs) |

#### G-Forces & Acceleration
| API | Returns | File |
|-----|---------|------|
| `vehicle.AccelerationBody` | `double3` — m/s² in body frame | [`KSA/Vehicle.cs`](decomp/ksa/KSA/Vehicle.cs) |
| G-force = `AccelerationBody.Length() / 9.80665` | `double` — g's | Pattern from [`geeforce.lib/GForceRecorder.cs:102-103`](geeforce.lib/GForceRecorder.cs) |

#### Vehicle State
| API | Returns | File |
|-----|---------|------|
| `vehicle.Situation` | `Situation` enum: `Freefall, Maneuvering, Rolling, Landed, Sailing, Floating` | [`KSA/Situation.cs`](decomp/ksa/KSA/Situation.cs) |
| `vehicle.Situation.HasAnyContact()` | `bool` — touching surface | [`KSA/SituationEx.cs`](decomp/ksa/KSA/SituationEx.cs) |
| `vehicle.Situation.GetSurfaceContact()` | `SurfaceContact` enum: `None, Terrain, Ocean` | [`KSA/SituationEx.cs`](decomp/ksa/KSA/SituationEx.cs) |

#### Parent Body & Atmosphere
| API | Returns | File |
|-----|---------|------|
| `vehicle.Orbit.Parent` | `IParentBody` — the SOI parent | [`KSA/IOrbiter.cs:17`](decomp/ksa/KSA/IOrbiter.cs) |
| `vehicle.Parent.Mass` | `double` — kg | [`KSA/IParentBody.cs`](decomp/ksa/KSA/IParentBody.cs) |
| `vehicle.Parent.MeanRadius` | `double` — meters | [`KSA/IParentBody.cs`](decomp/ksa/KSA/IParentBody.cs) |
| `vehicle.Parent.SphereOfInfluence` | `double` — meters | [`KSA/IParentBody.cs`](decomp/ksa/KSA/IParentBody.cs) |
| `(parent as Astronomical)?.GetAtmosphereReference()` | `AtmosphereReference?` | [`KSA/Astronomical.cs:274-277`](decomp/ksa/KSA/Astronomical.cs) |
| `atmRef.Physical.Height` | `DistanceReference` — atmosphere boundary | [`KSA/PhysicalAtmosphereReference.cs`](decomp/ksa/KSA/PhysicalAtmosphereReference.cs) |
| `atmRef.Physical.GetAtmosphericPressureAtAltitude(alt)` | `double` — Pascals | [`KSA/PhysicalAtmosphereReference.cs`](decomp/ksa/KSA/PhysicalAtmosphereReference.cs) |

#### Position (for inter-vehicle distances)
| API | Returns | File |
|-----|---------|------|
| `vehicle.GetPositionEcl()` | `double3` — ecliptic position (universal) | [`KSA/Vehicle.cs:490-493`](decomp/ksa/KSA/Vehicle.cs) |

#### Celestial Body Hierarchy
| API | Returns | File |
|-----|---------|------|
| `Celestial.Children` | `List<IOrbiter>` — child orbiters | [`KSA/Celestial.cs`](decomp/ksa/KSA/Celestial.cs) |
| `StellarBody.SphereOfInfluence` | `double.PositiveInfinity` | [`KSA/StellarBody.cs`](decomp/ksa/KSA/StellarBody.cs) |

#### Existing Provider Helpers
| API | Returns | File |
|-----|---------|------|
| `VehicleProvider.GetControlledVehicle()` | `Vehicle?` | [`ksa-abstractions.lib/VehicleProvider.cs`](ksa-abstractions.lib/VehicleProvider.cs) |
| `VehicleProvider.GetAllVehicles()` | `List<Vehicle>` | [`ksa-abstractions.lib/VehicleProvider.cs`](ksa-abstractions.lib/VehicleProvider.cs) |
| `CelestialProvider.GetAllCelestials()` | `List<Celestial>` | [`ksa-abstractions.lib/CelestialProvider.cs`](ksa-abstractions.lib/CelestialProvider.cs) |
| `SimTimeProvider.GetElapsedTime()` | `SimTime` | [`ksa-abstractions.lib/SimTimeProvider.cs`](ksa-abstractions.lib/SimTimeProvider.cs) |

### SOI Transition Internals
The game handles SOI transitions in [`KSA/KinematicStates.cs:292-338`](decomp/ksa/KSA/KinematicStates.cs) via `CheckSoiTransitions()`. There is **no explicit event/callback** — the parent simply changes. Our mod detects this by comparing `ParentBodyId` between snapshots.

### Atmosphere Boundary Calculation
Atmosphere height is calculated in [`KSA/PhysicalAtmosphereReference.cs:43-48`](decomp/ksa/KSA/PhysicalAtmosphereReference.cs) using: `height = max(-H·ln(ρ_min/ρ₀), -H·ln(P_min/P₀))` where H is scale height.

---

## Appendix B — Event Type Catalog

| Event Type | Trigger Condition | Details Dict Keys |
|-----------|-------------------|-------------------|
| `SoiChanged` | `prev.ParentBodyId != curr.ParentBodyId` | `old_body`, `new_body` |
| `Liftoff` | `prev.IsLanded && !curr.HasSurfaceContact` | `body`, `altitude_m` |
| `Landed` | `!prev.HasSurfaceContact && curr.IsLanded` | `body`, `speed_mps` |
| `SplashDown` | `!prev.HasSurfaceContact && curr.Situation ∈ {Floating, Sailing}` | `body`, `speed_mps` |
| `AtmosphereEntered` | `!prev.IsInAtmosphere && curr.IsInAtmosphere` | `body`, `altitude_m` |
| `AtmosphereExited` | `prev.IsInAtmosphere && !curr.IsInAtmosphere` | `body`, `altitude_m` |
| `StableOrbitAchieved` | `prev.PeAlt < atm_height && curr.PeAlt >= atm_height && e < 1` | `body`, `periapsis_m`, `apoapsis_m` |
| `OrbitEscaped` | `prev.Eccentricity < 1.0 && curr.Eccentricity >= 1.0` | `body`, `speed_mps` |

---

## Appendix C — Example Mission YAML

### Full Format Reference

```yaml
# yaml-language-server: $schema=mission-schema.json

# Required fields
name: "Mission Display Name"
description: "Longer description of what the player must accomplish"
category: "orbital"       # Optional: orbital, landing, exploration, speed
difficulty: 2             # Optional: 1-5

# The root objective (condition tree)
objective:
  type: all_of            # Top-level composite: all must be met
  sub_conditions:
    # Simple threshold
    - type: altitude_above
      value: 100000       # meters
      description: "Reach 100 km"

    # Location check
    - type: in_soi_of
      body_id: "Kerbin"
      description: "Stay in Kerbin's SOI"

    # Speed check with frame
    - type: speed_above
      value: 2200
      speed_frame: orbital  # orbital | surface | inertial
      description: "Reach orbital velocity"

    # Event check
    - type: event_occurred
      event_type: stable_orbit_achieved
      description: "Achieve stable orbit"

    # Nested composite
    - type: any_of
      sub_conditions:
        - type: eccentricity_below
          value: 0.1
          description: "Nearly circular orbit"
        - type: periapsis_above
          value: 200000
          description: "High orbit"

# Sequence type (conditions must be met in order)
# objective:
#   type: sequence
#   sub_conditions:
#     - type: event_occurred
#       event_type: liftoff
#     - type: altitude_above
#       value: 70000
#     - type: event_occurred
#       event_type: landed
```

### Condition Type Reference

| YAML `type` | Maps to `ConditionType` | Required Fields |
|-------------|------------------------|-----------------|
| `altitude_above` | `AltitudeAbove` | `value` (meters) |
| `altitude_below` | `AltitudeBelow` | `value` (meters) |
| `speed_above` | `SpeedAbove` | `value` (m/s), optional `speed_frame` |
| `speed_below` | `SpeedBelow` | `value` (m/s), optional `speed_frame` |
| `apoapsis_above` | `ApoapsisAbove` | `value` (meters altitude) |
| `periapsis_above` | `PeriapsisAbove` | `value` (meters altitude) |
| `periapsis_below` | `PeriapsisBelow` | `value` (meters altitude) |
| `eccentricity_below` | `EccentricityBelow` | `value` |
| `inclination_between` | `InclinationBetween` | `min_value`, `max_value` (radians) |
| `event_occurred` | `EventOccurred` | `event_type` |
| `in_soi_of` | `InSoiOf` | `body_id` |
| `on_surface_of` | `OnSurfaceOf` | `body_id` |
| `all_of` | `AllOf` | `sub_conditions` (list) |
| `any_of` | `AnyOf` | `sub_conditions` (list) |
| `sequence` | `Sequence` | `sub_conditions` (list, order matters) |

### Event Type Reference (for `event_occurred`)

| YAML `event_type` | Maps to `FlightEventType` |
|-------------------|--------------------------|
| `soi_changed` | `SoiChanged` |
| `liftoff` | `Liftoff` |
| `landed` | `Landed` |
| `splash_down` | `SplashDown` |
| `atmosphere_entered` | `AtmosphereEntered` |
| `atmosphere_exited` | `AtmosphereExited` |
| `stable_orbit_achieved` | `StableOrbitAchieved` |
| `orbit_escaped` | `OrbitEscaped` |
