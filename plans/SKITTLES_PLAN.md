# Skittles — Global ImGui Theme Manager

## Overview

Skittles is a KSA game mod that provides global ImGui theming across the entire application. It modifies the shared `ImGuiStyle` struct via `ImGui.GetStyle()`, which affects all game UI and all mod UIs application-wide. Changes persist for the process lifetime without requiring Harmony patching.

### Core Features

- **Theme Picker**: Main window (F11 toggle) with a filterable combobox listing all available themes (built-in + custom saved themes)
- **Theme Editor**: Second window wrapping ImGui's built-in `ImGui.ShowStyleEditor()` with a custom "Save Theme" button to persist edits to disk
- **Full Style Control**: Both color slots (all 60 `ImGuiCol` values) and style variables (padding, rounding, border sizes, spacing, etc.)
- **Built-in Themes**: ImGui's Dark/Light/Classic + a custom "Inanimate Carbon Rod" theme using RadioactiveGreen
- **Persistence**: Custom themes saved as TOML files; last-active theme remembered across sessions
- **Startup Restore**: On game start, automatically loads and applies the last-used theme

### Architecture

```
skittles.lib/          — Core logic (no UI, no StarMap, no Harmony)
  ThemeDefinition.cs   — Data model for a complete theme
  ThemeSerializer.cs   — TOML serialization/deserialization
  ThemeManager.cs      — Load/save/apply/list themes + built-in themes

skittles/              — Mod entry point + UI
  Mod.cs               — StarMap lifecycle, F11 window, theme picker, editor window
  Patcher.cs           — Harmony skeleton (minimal — no patches needed for style API)
  mod.toml             — StarMap mod descriptor
```

### File System Layout (User Data)

```
%USERPROFILE%\Documents\My Games\Kitten Space Agency\skittles\
  config.toml                      — Mod preferences (active theme name)
  themes\
    inanimate-carbon-rod.toml      — Shipped preset theme
    <user-created-themes>.toml     — User-saved custom themes
```

---

## Task 1: Project Scaffolding

**Expert**: C# / .NET project setup  
**Goal**: Create all project files, register in solution, establish the mod + lib structure.

### 1.1 Create `skittles.lib/skittles.lib.csproj`

Create the file `skittles.lib/skittles.lib.csproj` with the standard `.lib` project pattern. Follow the exact pattern from `garys-torch.lib/garys-torch.lib.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.SkittlesLib</AssemblyName>
    <RootNamespace>MeowSci.SkittlesLib</RootNamespace>
  </PropertyGroup>

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
    <Reference Include="Brutal.ImGui.Abstractions" Condition="Exists('$(KSAFolder)Brutal.ImGui.Abstractions.dll')">
      <HintPath>$(KSAFolder)Brutal.ImGui.Abstractions.dll</HintPath>
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

</Project>
```

No `ksa-abstractions.lib` reference is needed — Skittles only interacts with ImGui, not game vehicles/parts.

### 1.2 Create `skittles/skittles.csproj`

Create the file `skittles/skittles.csproj` following the `garys-torch/garys-torch.csproj` pattern:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.Skittles</AssemblyName>
    <RootNamespace>MeowSci.Skittles</RootNamespace>
    <DistDir>$(SelectedDistModDir)skittles\</DistDir>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\skittles.lib\skittles.lib.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="StarMap.API" Version="0.3.6" PrivateAssets="all" />
    <PackageReference Include="Lib.Harmony" Version="2.4.2" PrivateAssets="all" />
  </ItemGroup>

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
    <Reference Include="Brutal.ImGui.Abstractions" Condition="Exists('$(KSAFolder)Brutal.ImGui.Abstractions.dll')">
      <HintPath>$(KSAFolder)Brutal.ImGui.Abstractions.dll</HintPath>
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

  <ItemGroup>
    <None Update="mod.toml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
  </ItemGroup>

  <Target Name="CopyCustomContent" AfterTargets="AfterBuild">
    <MakeDir Directories="$(DistDir)" />
    <ItemGroup>
      <FilesToCopy Include="$(OutputPath)mod.toml" />
      <FilesToCopy Include="$(OutputPath)$(AssemblyName).dll" />
      <FilesToCopy Include="$(OutputPath)$(AssemblyName).pdb" />
      <FilesToCopy Include="$(OutputPath)$(AssemblyName).deps.json" />
    </ItemGroup>
    <Copy SourceFiles="@(FilesToCopy)" DestinationFolder="$(DistDir)" />
    <Message Text="Copied mod files to $(DistDir)" Importance="high" />

    <ItemGroup>
      <MeowSciAssemblies Include="$(TargetDir)MeowSci.*.dll;$(TargetDir)MeowSci.*.pdb" />
    </ItemGroup>
    <Copy SourceFiles="@(MeowSciAssemblies)"
          DestinationFolder="$(DistDir)"
          Condition="'@(MeowSciAssemblies)' != ''" />
  </Target>

</Project>
```

### 1.3 Create `skittles/mod.toml`

```toml
name = "skittles"
description = "Global ImGui theme manager"
version = "0.1.0"
author = "meow sci"

[StarMap]
EntryAssembly = "MeowSci.Skittles"
```

### 1.4 Update `ksa-mod-experiments.slnx`

Add these two lines inside the `<Solution>` element, after the existing project entries:

```xml
    <Project Path="skittles/skittles.csproj" />
    <Project Path="skittles.lib/skittles.lib.csproj" />
```

### 1.5 Verify

Run `dotnet build` from the repository root. Both projects must compile with zero errors. At this point, the projects will have empty/stub source files — that is expected. Create minimal stub files if needed for compilation:

- `skittles.lib/ThemeDefinition.cs` — empty namespace `MeowSci.SkittlesLib` with a placeholder class
- `skittles/Mod.cs` — copy from `fixme-mod-name/Mod.cs` template, replace `fixme-mod-name` → `skittles` and `FixmeModName` → `Skittles` and `MeowSci.FixmeModName` → `MeowSci.Skittles`
- `skittles/Patcher.cs` — copy from `fixme-mod-name/Patcher.cs` template, replace `fixme-mod-name` → `skittles` and `MeowSci.FixmeModName` → `MeowSci.Skittles`

---

## Task 2: Core Library — Theme Data Model (`skittles.lib/ThemeDefinition.cs`)

**Expert**: C# data modeling  
**Goal**: Define the data structure that fully represents an ImGui theme (all 60 colors + all style variables).

### 2.1 `ThemeDefinition` Class

Create `skittles.lib/ThemeDefinition.cs` in namespace `MeowSci.SkittlesLib`.

This class must capture the **complete** ImGui visual state so it can be serialized/deserialized and applied. It should be a plain C# class (POCO) with no ImGui dependencies in its data — use standard types (`float`, `float[]`, `bool`, `string`) so it can be serialized to TOML without ImGui-specific types.

#### Required Fields

**Metadata:**
- `string Name` — display name of the theme
- `string Description` — optional description

**Colors** (60 entries — one per `ImGuiCol` value):

Store as a `float[][]` array of 60 entries, each a 4-element float array `[R, G, B, A]` (range 0.0–1.0). Index by `(int)ImGuiCol.X`.

The full `ImGuiCol` enum has 60 values (indices 0–59):

| Index | ImGuiCol Name | Description |
|-------|---------------|-------------|
| 0 | Text | Default text color |
| 1 | TextDisabled | Grayed-out text |
| 2 | WindowBg | Window background |
| 3 | ChildBg | Child window background |
| 4 | PopupBg | Popup/tooltip background |
| 5 | Border | Border color |
| 6 | BorderShadow | Border shadow |
| 7 | FrameBg | Frame background (checkbox, radio, slider, input) |
| 8 | FrameBgHovered | Frame background when hovered |
| 9 | FrameBgActive | Frame background when active/pressed |
| 10 | TitleBg | Title bar background (unfocused) |
| 11 | TitleBgActive | Title bar background (focused) |
| 12 | TitleBgCollapsed | Title bar background (collapsed) |
| 13 | MenuBarBg | Menu bar background |
| 14 | ScrollbarBg | Scrollbar background |
| 15 | ScrollbarGrab | Scrollbar grab |
| 16 | ScrollbarGrabHovered | Scrollbar grab hovered |
| 17 | ScrollbarGrabActive | Scrollbar grab active |
| 18 | CheckMark | Checkmark color |
| 19 | SliderGrab | Slider grab |
| 20 | SliderGrabActive | Slider grab active |
| 21 | Button | Button background |
| 22 | ButtonHovered | Button hovered |
| 23 | ButtonActive | Button active/pressed |
| 24 | Header | Header (collapsing header, tree node, selectable, menu item) |
| 25 | HeaderHovered | Header hovered |
| 26 | HeaderActive | Header active |
| 27 | Separator | Separator line |
| 28 | SeparatorHovered | Separator hovered |
| 29 | SeparatorActive | Separator active |
| 30 | ResizeGrip | Resize grip |
| 31 | ResizeGripHovered | Resize grip hovered |
| 32 | ResizeGripActive | Resize grip active |
| 33 | InputTextCursor | Text input cursor |
| 34 | TabHovered | Tab hovered |
| 35 | Tab | Tab background |
| 36 | TabSelected | Selected tab |
| 37 | TabSelectedOverline | Selected tab overline |
| 38 | TabDimmed | Dimmed/inactive tab |
| 39 | TabDimmedSelected | Dimmed but selected tab |
| 40 | TabDimmedSelectedOverline | Dimmed selected tab overline |
| 41 | DockingPreview | Docking preview overlay |
| 42 | DockingEmptyBg | Docking empty area background |
| 43 | PlotLines | Plot line color |
| 44 | PlotLinesHovered | Plot line hovered |
| 45 | PlotHistogram | Plot histogram bar |
| 46 | PlotHistogramHovered | Plot histogram hovered |
| 47 | TableHeaderBg | Table header background |
| 48 | TableBorderStrong | Table outer/header borders |
| 49 | TableBorderLight | Table inner borders |
| 50 | TableRowBg | Table row background |
| 51 | TableRowBgAlt | Table alternating row background |
| 52 | TextLink | Hyperlink text |
| 53 | TextSelectedBg | Text selection background |
| 54 | TreeLines | Tree node connector lines |
| 55 | DragDropTarget | Drag-drop target highlight |
| 56 | NavCursor | Keyboard/gamepad navigation cursor |
| 57 | NavWindowingHighlight | Window selection highlight (Alt+Tab) |
| 58 | NavWindowingDimBg | Dimmed background during window selection |
| 59 | ModalWindowDimBg | Dimmed background behind modal windows |

**Style Variables** (all properties from `ImGuiStylePtr`):

Store each as named fields with appropriate types. The full set:

Float values:
- `float Alpha` (default: 1.0)
- `float DisabledAlpha` (default: 0.6)
- `float WindowRounding` (default: 0.0)
- `float WindowBorderSize` (default: 1.0)
- `float WindowBorderHoverPadding` (default: 4.0)
- `float ChildRounding` (default: 0.0)
- `float ChildBorderSize` (default: 1.0)
- `float PopupRounding` (default: 0.0)
- `float PopupBorderSize` (default: 1.0)
- `float FrameRounding` (default: 0.0)
- `float FrameBorderSize` (default: 0.0)
- `float IndentSpacing` (default: 21.0)
- `float ColumnsMinSpacing` (default: 6.0)
- `float ScrollbarSize` (default: 14.0)
- `float ScrollbarRounding` (default: 9.0)
- `float GrabMinSize` (default: 12.0)
- `float GrabRounding` (default: 0.0)
- `float LogSliderDeadzone` (default: 4.0)
- `float ImageBorderSize` (default: 0.0)
- `float TabRounding` (default: 4.0)
- `float TabBorderSize` (default: 0.0)
- `float TabMinWidthBase` (default: varies)
- `float TabMinWidthShrink` (default: varies)
- `float TabCloseButtonMinWidthSelected` (default: varies)
- `float TabCloseButtonMinWidthUnselected` (default: varies)
- `float TabBarBorderSize` (default: 1.0)
- `float TabBarOverlineSize` (default: 2.0)
- `float TableAngledHeadersAngle` (default: 35° in radians)
- `float TreeLinesSize` (default: 1.0)
- `float TreeLinesRounding` (default: 0.0)
- `float SeparatorTextBorderSize` (default: 3.0)
- `float DockingSeparatorSize` (default: 2.0)
- `float MouseCursorScale` (default: 1.0)
- `float CurveTessellationTol` (default: 1.25)
- `float CircleTessellationMaxError` (default: 0.3)

Float2 values (store as `float[]` of length 2):
- `float[] WindowPadding` (default: [8, 8])
- `float[] WindowMinSize` (default: [32, 32])
- `float[] WindowTitleAlign` (default: [0, 0.5])
- `float[] FramePadding` (default: [4, 3])
- `float[] ItemSpacing` (default: [8, 4])
- `float[] ItemInnerSpacing` (default: [4, 4])
- `float[] CellPadding` (default: [4, 2])
- `float[] TouchExtraPadding` (default: [0, 0])
- `float[] ButtonTextAlign` (default: [0.5, 0.5])
- `float[] SelectableTextAlign` (default: [0, 0])
- `float[] SeparatorTextAlign` (default: [0, 0.5])
- `float[] SeparatorTextPadding` (default: [20, 3])
- `float[] DisplayWindowPadding` (default: [19, 19])
- `float[] DisplaySafeAreaPadding` (default: [3, 3])
- `float[] TableAngledHeadersTextAlign` (default: [0.5, 0])

Bool values:
- `bool AntiAliasedLines` (default: true)
- `bool AntiAliasedLinesUseTex` (default: true)
- `bool AntiAliasedFill` (default: true)

#### Methods

- `static ThemeDefinition CaptureFromImGui()` — reads all values from `ImGui.GetStyle()` and populates a new `ThemeDefinition`. This requires a `using Brutal.ImGuiApi;` and `using Brutal.Numerics;` in the implementation. Access the global style with `ImGuiStylePtr style = ImGui.GetStyle();` then read each property via `style.WindowRounding`, `style.Colors[(int)ImGuiCol.Text]`, etc. The `Colors` field on `ImGuiStylePtr` is of type `ref float4_60` (an inline array). Access individual colors by casting: `style.Colors[(int)ImGuiCol.Text]` returns a `float4`. Convert `float4` to `float[]` as `new float[] { color.x, color.y, color.z, color.w }`. Convert `float2` to `float[]` as `new float[] { val.x, val.y }`.

- `void ApplyToImGui()` — writes all values from this `ThemeDefinition` to `ImGui.GetStyle()`. Get the style pointer, then set each property. For colors: `style.Colors[(int)ImGuiCol.Text] = new float4(Colors[0][0], Colors[0][1], Colors[0][2], Colors[0][3]);`. For float2 values: `style.WindowPadding = new float2(WindowPadding[0], WindowPadding[1]);`. For float values: direct assignment like `style.WindowRounding = WindowRounding;`.

---

## Task 3: Core Library — Theme Serialization (`skittles.lib/ThemeSerializer.cs`)

**Expert**: C# file I/O and serialization  
**Goal**: Serialize `ThemeDefinition` to/from TOML format files.

### 3.1 TOML Format Specification

Do NOT use external NuGet packages. Write a simple, purpose-built TOML serializer/deserializer. The format is completely known and structured — no arbitrary nesting or complex types.

Each theme file uses this exact TOML structure:

```toml
[meta]
name = "Theme Name Here"
description = "Optional description"

[colors]
# Each key is the ImGuiCol enum name, value is [R, G, B, A] with floats 0.0-1.0
Text = [1.00, 1.00, 1.00, 1.00]
TextDisabled = [0.50, 0.50, 0.50, 1.00]
WindowBg = [0.06, 0.06, 0.06, 0.94]
ChildBg = [0.00, 0.00, 0.00, 0.00]
PopupBg = [0.08, 0.08, 0.08, 0.94]
Border = [0.43, 0.43, 0.50, 0.50]
BorderShadow = [0.00, 0.00, 0.00, 0.00]
FrameBg = [0.16, 0.29, 0.48, 0.54]
FrameBgHovered = [0.26, 0.59, 0.98, 0.40]
FrameBgActive = [0.26, 0.59, 0.98, 0.67]
# ... all 60 colors listed by their ImGuiCol enum name
TitleBg = [0.04, 0.04, 0.04, 1.00]
TitleBgActive = [0.16, 0.29, 0.48, 1.00]
TitleBgCollapsed = [0.00, 0.00, 0.00, 0.51]
MenuBarBg = [0.14, 0.14, 0.14, 1.00]
ScrollbarBg = [0.02, 0.02, 0.02, 0.53]
ScrollbarGrab = [0.31, 0.31, 0.31, 1.00]
ScrollbarGrabHovered = [0.41, 0.41, 0.41, 1.00]
ScrollbarGrabActive = [0.51, 0.51, 0.51, 1.00]
CheckMark = [0.26, 0.59, 0.98, 1.00]
SliderGrab = [0.24, 0.52, 0.88, 1.00]
SliderGrabActive = [0.26, 0.59, 0.98, 1.00]
Button = [0.26, 0.59, 0.98, 0.40]
ButtonHovered = [0.26, 0.59, 0.98, 1.00]
ButtonActive = [0.06, 0.53, 0.98, 1.00]
Header = [0.26, 0.59, 0.98, 0.31]
HeaderHovered = [0.26, 0.59, 0.98, 0.80]
HeaderActive = [0.26, 0.59, 0.98, 1.00]
Separator = [0.43, 0.43, 0.50, 0.50]
SeparatorHovered = [0.10, 0.40, 0.75, 0.78]
SeparatorActive = [0.10, 0.40, 0.75, 1.00]
ResizeGrip = [0.26, 0.59, 0.98, 0.20]
ResizeGripHovered = [0.26, 0.59, 0.98, 0.67]
ResizeGripActive = [0.26, 0.59, 0.98, 0.95]
InputTextCursor = [1.00, 1.00, 1.00, 1.00]
TabHovered = [0.26, 0.59, 0.98, 0.80]
Tab = [0.18, 0.35, 0.58, 0.86]
TabSelected = [0.20, 0.41, 0.68, 1.00]
TabSelectedOverline = [0.26, 0.59, 0.98, 1.00]
TabDimmed = [0.07, 0.10, 0.15, 0.97]
TabDimmedSelected = [0.14, 0.26, 0.42, 1.00]
TabDimmedSelectedOverline = [0.50, 0.50, 0.50, 1.00]
DockingPreview = [0.26, 0.59, 0.98, 0.70]
DockingEmptyBg = [0.20, 0.20, 0.20, 1.00]
PlotLines = [0.61, 0.61, 0.61, 1.00]
PlotLinesHovered = [1.00, 0.43, 0.35, 1.00]
PlotHistogram = [0.90, 0.70, 0.00, 1.00]
PlotHistogramHovered = [1.00, 0.60, 0.00, 1.00]
TableHeaderBg = [0.19, 0.19, 0.20, 1.00]
TableBorderStrong = [0.31, 0.31, 0.35, 1.00]
TableBorderLight = [0.23, 0.23, 0.25, 1.00]
TableRowBg = [0.00, 0.00, 0.00, 0.00]
TableRowBgAlt = [1.00, 1.00, 1.00, 0.06]
TextLink = [0.26, 0.59, 0.98, 1.00]
TextSelectedBg = [0.26, 0.59, 0.98, 0.35]
TreeLines = [0.43, 0.43, 0.50, 0.50]
DragDropTarget = [1.00, 1.00, 0.00, 0.90]
NavCursor = [0.26, 0.59, 0.98, 1.00]
NavWindowingHighlight = [1.00, 1.00, 1.00, 0.70]
NavWindowingDimBg = [0.80, 0.80, 0.80, 0.20]
ModalWindowDimBg = [0.80, 0.80, 0.80, 0.35]

[style]
# Float values
Alpha = 1.00
DisabledAlpha = 0.60
WindowRounding = 0.00
WindowBorderSize = 1.00
WindowBorderHoverPadding = 4.00
ChildRounding = 0.00
ChildBorderSize = 1.00
PopupRounding = 0.00
PopupBorderSize = 1.00
FrameRounding = 0.00
FrameBorderSize = 0.00
IndentSpacing = 21.00
ColumnsMinSpacing = 6.00
ScrollbarSize = 14.00
ScrollbarRounding = 9.00
GrabMinSize = 12.00
GrabRounding = 0.00
LogSliderDeadzone = 4.00
ImageBorderSize = 0.00
TabRounding = 4.00
TabBorderSize = 0.00
TabBarBorderSize = 1.00
TabBarOverlineSize = 2.00
TableAngledHeadersAngle = 0.611
TreeLinesSize = 1.00
TreeLinesRounding = 0.00
SeparatorTextBorderSize = 3.00
DockingSeparatorSize = 2.00
MouseCursorScale = 1.00
CurveTessellationTol = 1.25
CircleTessellationMaxError = 0.30

# Float2 values (stored as arrays)
WindowPadding = [8.00, 8.00]
WindowMinSize = [32.00, 32.00]
WindowTitleAlign = [0.00, 0.50]
FramePadding = [4.00, 3.00]
ItemSpacing = [8.00, 4.00]
ItemInnerSpacing = [4.00, 4.00]
CellPadding = [4.00, 2.00]
TouchExtraPadding = [0.00, 0.00]
ButtonTextAlign = [0.50, 0.50]
SelectableTextAlign = [0.00, 0.00]
SeparatorTextAlign = [0.00, 0.50]
SeparatorTextPadding = [20.00, 3.00]
DisplayWindowPadding = [19.00, 19.00]
DisplaySafeAreaPadding = [3.00, 3.00]
TableAngledHeadersTextAlign = [0.50, 0.00]

# Bool values
AntiAliasedLines = true
AntiAliasedLinesUseTex = true
AntiAliasedFill = true
```

### 3.2 `ThemeSerializer` Class

Create `skittles.lib/ThemeSerializer.cs` in namespace `MeowSci.SkittlesLib`.

**Methods:**

- `static string Serialize(ThemeDefinition theme)` — Converts a `ThemeDefinition` to a TOML string. Format floats with `F2` (2 decimal places). Write the `[meta]`, `[colors]`, and `[style]` sections in order. Use the `ImGuiCol` enum name as the key for colors (map index to name using a static `string[]` array of all 60 names in order). String values must be quoted. Arrays use `[val1, val2, ...]` syntax. Booleans are `true`/`false` (lowercase).

- `static ThemeDefinition Deserialize(string toml)` — Parses a TOML string into a `ThemeDefinition`. Implementation approach:
  1. Split into lines
  2. Track current section (`[meta]`, `[colors]`, `[style]`)
  3. For each non-empty, non-comment line, parse `key = value`
  4. For `[meta]` section: parse string values (strip quotes)
  5. For `[colors]` section: parse `[R, G, B, A]` arrays → map key name to `ImGuiCol` index → set in Colors array
  6. For `[style]` section: parse float, float-array `[X, Y]`, or bool values → set on the matching field
  7. Use `float.Parse` with `CultureInfo.InvariantCulture` for locale-independent parsing
  8. Skip unknown keys gracefully (log warning, don't crash)
  9. Handle comment lines (`#` prefix) and blank lines

- `static void SaveToFile(ThemeDefinition theme, string filePath)` — Calls `Serialize`, then writes the string to `filePath` using `File.WriteAllText`. Creates the directory if it doesn't exist via `Directory.CreateDirectory(Path.GetDirectoryName(filePath))`.

- `static ThemeDefinition LoadFromFile(string filePath)` — Reads the file via `File.ReadAllText`, then calls `Deserialize`. Wraps in try/catch — if parsing fails, log the error with `Console.WriteLine` and return null.

**Mapping `ImGuiCol` index ↔ string name:**

Include a static `string[]` array with all 60 names in order:
```csharp
private static readonly string[] ColorNames = {
    "Text", "TextDisabled", "WindowBg", "ChildBg", "PopupBg",
    "Border", "BorderShadow", "FrameBg", "FrameBgHovered", "FrameBgActive",
    "TitleBg", "TitleBgActive", "TitleBgCollapsed", "MenuBarBg",
    "ScrollbarBg", "ScrollbarGrab", "ScrollbarGrabHovered", "ScrollbarGrabActive",
    "CheckMark", "SliderGrab", "SliderGrabActive",
    "Button", "ButtonHovered", "ButtonActive",
    "Header", "HeaderHovered", "HeaderActive",
    "Separator", "SeparatorHovered", "SeparatorActive",
    "ResizeGrip", "ResizeGripHovered", "ResizeGripActive",
    "InputTextCursor", "TabHovered", "Tab", "TabSelected", "TabSelectedOverline",
    "TabDimmed", "TabDimmedSelected", "TabDimmedSelectedOverline",
    "DockingPreview", "DockingEmptyBg",
    "PlotLines", "PlotLinesHovered", "PlotHistogram", "PlotHistogramHovered",
    "TableHeaderBg", "TableBorderStrong", "TableBorderLight",
    "TableRowBg", "TableRowBgAlt",
    "TextLink", "TextSelectedBg", "TreeLines",
    "DragDropTarget", "NavCursor", "NavWindowingHighlight", "NavWindowingDimBg",
    "ModalWindowDimBg"
};
```

And a reverse lookup `Dictionary<string, int>` built from it for deserialization.

### 3.3 `ModConfig` class (small)

Create a small config class (can be in the same file or a separate `ModConfig.cs`) for the mod preferences file:

```csharp
public sealed class ModConfig
{
    public string ActiveThemeName { get; set; } = "";
    
    // Config file is always: <configDir>/config.toml
    // Format:
    // active_theme = "Theme Name"
}
```

Provide `SerializeConfig` / `DeserializeConfig` methods (very small — single key-value pair in TOML). And corresponding `SaveConfig` / `LoadConfig` file I/O methods.

---

## Task 4: Core Library — Theme Manager (`skittles.lib/ThemeManager.cs`)

**Expert**: C# application logic  
**Goal**: Orchestrate theme loading, saving, listing, applying, and built-in theme management.

### 4.1 `ThemeManager` Class

Create `skittles.lib/ThemeManager.cs` in namespace `MeowSci.SkittlesLib`.

#### Directory Paths

Calculate paths in the constructor or via a static helper:

```csharp
// Base config directory
var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
var productionConfigRoot = Path.Combine(myDocuments, "My Games", "Kitten Space Agency");
var configDirectory = Path.Combine(productionConfigRoot, "skittles");

// Specific paths
var configFilePath = Path.Combine(configDirectory, "config.toml");
var themesDirectory = Path.Combine(configDirectory, "themes");
```

#### State

- `ThemeDefinition? DefaultTheme` — captured from ImGui on first init (before any theme is applied). This is the "Game Default" snapshot.
- `List<ThemeEntry> AvailableThemes` — combined list of built-in + custom themes
- `string? ActiveThemeName` — name of currently active theme
- `ModConfig Config` — loaded config

Where `ThemeEntry` is:
```csharp
public sealed class ThemeEntry
{
    public string Name { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? FilePath { get; set; } // null for built-in themes
}
```

#### Initialization Method: `void Initialize()`

Called once from `Mod.OnFullyLoaded()`. Must:

1. **Capture game default style**: Call `ThemeDefinition.CaptureFromImGui()` and store as `DefaultTheme` with name "Game Default"
2. **Ensure directories exist**: `Directory.CreateDirectory(themesDirectory)`
3. **Ship the "Inanimate Carbon Rod" preset**: Check if `Path.Combine(themesDirectory, "inanimate-carbon-rod.toml")` exists. If not, create it by calling `ThemeSerializer.SaveToFile(BuiltInThemes.InanimateCarbonRod(), filePath)`. This ensures the preset is saved on first run.
4. **Load config**: Read `config.toml` if it exists; parse `active_theme` value
5. **Discover themes**: Build the `AvailableThemes` list:
   - Add "Game Default" (IsBuiltIn=true)
   - Add "Dark" (IsBuiltIn=true)
   - Add "Light" (IsBuiltIn=true)
   - Add "Classic" (IsBuiltIn=true)
   - Scan `themesDirectory` for `*.toml` files. For each, read only the `[meta]` section to get the `name`. Add as custom theme with `IsBuiltIn=false` and `FilePath` set.
6. **Apply startup theme**: If `ActiveThemeName` from config matches an available theme, apply it. Otherwise, do nothing (keep game default).

#### Core Methods

- `string[] GetThemeNames()` — returns array of all available theme names (for UI combobox). Order: "Game Default" first, then built-ins ("Dark", "Light", "Classic"), then custom themes alphabetically.

- `void ApplyTheme(string themeName)` — applies the named theme:
  - If "Game Default": apply `DefaultTheme` via `ApplyToImGui()`
  - If "Dark": call `ImGui.StyleColorsDark()`; apply `DefaultTheme`'s style vars (non-color properties) to reset vars
  - If "Light": call `ImGui.StyleColorsLight()`; apply DefaultTheme style vars
  - If "Classic": call `ImGui.StyleColorsClassic()`; apply DefaultTheme style vars
  - If custom: load full `ThemeDefinition` from its `.toml` file, call `ApplyToImGui()`
  - Update `ActiveThemeName`
  - Save config with new `active_theme` value

- `void SaveCurrentAsTheme(string name)` — captures current ImGui style, sets `Name`, saves to `themesDirectory/<slugified-name>.toml`. Slugify: lowercase, replace spaces with `-`, remove non-alphanumeric except `-`. After saving, refresh `AvailableThemes` list.

- `void RefreshThemeList()` — re-scans `themesDirectory` and rebuilds `AvailableThemes`

- `void RestoreDefaults()` — applies `DefaultTheme` (called on mod unload to restore game's original style)

### 4.2 Built-in Theme: "Inanimate Carbon Rod"

Create `skittles.lib/BuiltInThemes.cs` in namespace `MeowSci.SkittlesLib`.

This class has a static method `static ThemeDefinition InanimateCarbonRod()` that returns a fully populated `ThemeDefinition`.

**Design concept**: "Radioactive terminal" — dark backgrounds with RadioactiveGreen (`rgba(0.173, 0.980, 0.122, 1.0)`) as the dominant accent color.

The RadioactiveGreen base color is approximately `float4(0.17f, 0.98f, 0.12f, 1.0f)` (XKCD radioactive green = #2CFA1F).

**Color palette derivation** (from RadioactiveGreen base):

| Role | Color | RGBA |
|------|-------|------|
| Primary (full brightness) | RadioactiveGreen | (0.17, 0.98, 0.12, 1.0) |
| Dimmed (disabled text) | 40% brightness | (0.07, 0.39, 0.05, 1.0) |
| Background Dark | Near-black with green tint | (0.02, 0.05, 0.02, 0.94) |
| Background Medium | Slightly lighter | (0.04, 0.10, 0.04, 0.94) |
| Frame/Control BG | Dark green | (0.05, 0.14, 0.05, 0.54) |
| Frame Hovered | Brighter green | (0.10, 0.60, 0.08, 0.40) |
| Frame Active | Full green | (0.12, 0.80, 0.10, 0.67) |
| Border | Green at reduced alpha | (0.17, 0.98, 0.12, 0.50) |
| Button | Medium green | (0.10, 0.55, 0.08, 0.40) |
| Button Hovered | Bright green | (0.17, 0.98, 0.12, 0.80) |
| Button Active | Full green | (0.14, 0.85, 0.10, 1.0) |
| Header | Semi-transparent green | (0.17, 0.98, 0.12, 0.31) |
| Title Active | Dark green background | (0.06, 0.30, 0.05, 1.0) |
| Separator | Green at half alpha | (0.17, 0.98, 0.12, 0.50) |
| CheckMark, SliderGrab | Full RadioactiveGreen | (0.17, 0.98, 0.12, 1.0) |
| ScrollbarGrab | Medium green | (0.10, 0.55, 0.08, 1.0) |
| Tab Selected | Dark green | (0.08, 0.40, 0.06, 1.0) |
| Plot/Chart | Bright green | (0.17, 0.98, 0.12, 1.0) |
| DragDropTarget | Yellow-green highlight | (0.50, 1.00, 0.00, 0.90) |
| ModalWindowDimBg | Dark green dim | (0.02, 0.10, 0.02, 0.35) |

The implementing agent should fill in ALL 60 color values following this design language. Text and accent elements use RadioactiveGreen or variants. Backgrounds are very dark with slight green tinting. Interactive states (hover, active) use progressively brighter green.

For style variables, start from ImGui Dark defaults but apply:
- `WindowRounding = 4.0` (slightly rounded windows)
- `FrameRounding = 2.0` (slightly rounded controls)
- `GrabRounding = 2.0`
- `TabRounding = 4.0`
- `WindowBorderSize = 1.0` (visible green borders)
- `FrameBorderSize = 1.0` (visible green frame borders for that "terminal" look)
- All other style vars: use ImGui Dark defaults

---

## Task 5: Mod — Lifecycle, Main Window & Theme Editor (`skittles/`)

**Expert**: C# / ImGui / KSA mod development  
**Goal**: Implement the StarMap mod entry point, the theme picker window, and the theme editor window.

### 5.1 `skittles/Patcher.cs`

Minimal Harmony skeleton — no actual patches are needed since global ImGui style is accessed directly. Follow the `fixme-mod-name/Patcher.cs` template exactly, replacing `fixme-mod-name` → `skittles` and `MeowSci.FixmeModName` → `MeowSci.Skittles`:

```csharp
using System;
using HarmonyLib;

namespace MeowSci.Skittles;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("skittles");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("skittles");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error removing patches: {ex.Message}");
        }
    }
}
```

### 5.2 `skittles/Mod.cs` — Lifecycle

Namespace: `MeowSci.Skittles`  
Requires: `using MeowSci.SkittlesLib;`

State fields:
```csharp
private bool _isInitialized = false;
private bool _isDisposed = false;
private bool _windowVisible = false;      // Main theme picker window
private bool _editorVisible = false;      // Theme editor window
private ThemeManager _themeManager = null!;
private int _selectedThemeIndex = 0;      // Index into the theme names array
private string _saveThemeName = "";       // Text input for saving theme name
private bool _showSaveInput = false;      // Whether the save-name input is visible
```

**`OnFullyLoaded()`:**
```
1. Call Patcher.Patch()
2. Create ThemeManager instance
3. Call _themeManager.Initialize()
4. Set _selectedThemeIndex to match the active theme from config
5. Set _isInitialized = true
6. Log: Console.WriteLine("skittles: Initialized successfully")
```

**`OnBeforeUi(double dt)`:** Empty — no per-frame work needed.

**`OnAfterUi(double dt)`:**
```
1. Guard: if (!_isInitialized || _isDisposed) return;
2. Check F11 keypress: if (ImGui.IsKeyPressed(ImGuiKey.F11)) toggle _windowVisible
3. If _windowVisible, call RenderMainWindow()
4. If _editorVisible, call RenderEditorWindow()
```

**`Unload()`:**
```
1. Call _themeManager.RestoreDefaults() — restore the game's original style
2. Call Patcher.Unload()
3. Set _isDisposed = true
```

All lifecycle methods wrapped in try/catch with `Console.WriteLine` error logging.

### 5.3 Main Window — `RenderMainWindow()`

This is the theme picker window.

```
ImGui.SetNextWindowSize(new float2(400, 350), ImGuiCond.FirstUseEver);
if (ImGui.Begin("Skittles — Theme Manager", ref _windowVisible))
{
    // 1. Header
    ImGui.TextColored(new float4(0.17f, 0.98f, 0.12f, 1.0f), "Skittles");
    ImGui.SameLine();
    ImGui.TextDisabled("Global Theme Manager");
    ImGui.Separator();

    // 2. Current theme display
    ImGui.Text($"Active: {_themeManager.ActiveThemeName ?? "Game Default"}");
    ImGui.Separator();

    // 3. Theme selector — filterable combobox
    //    Use the ImGui BeginCombo + filter pattern from the imgui skill:
    string[] themeNames = _themeManager.GetThemeNames();
    string previewValue = _selectedThemeIndex >= 0 && _selectedThemeIndex < themeNames.Length
        ? themeNames[_selectedThemeIndex]
        : "Select Theme...";

    // Filterable combobox (see imgui skill "Combobox with filter example"):
    // - _themeFilter is a class-level field: ImGuiTextFilter (needs to figure out if Brutal has this)
    // - If ImGuiTextFilter is not available, use a simple string filter approach:
    //   A text input at the top of the combo dropdown, then filter items by string.Contains
    
    // ALTERNATIVE simpler approach if ImGuiTextFilter is unavailable:
    // Use a regular BeginCombo + manual filter input:
    if (ImGui.BeginCombo("Theme##selector", previewValue))
    {
        // Filter input at the top
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##filter", ref _filterText, 256);
        
        ImGui.Separator();
        
        for (int i = 0; i < themeNames.Length; i++)
        {
            // Filter: show if filter is empty or name contains filter text (case-insensitive)
            if (!string.IsNullOrEmpty(_filterText) && 
                !themeNames[i].Contains(_filterText, StringComparison.OrdinalIgnoreCase))
                continue;

            bool isSelected = _selectedThemeIndex == i;
            if (ImGui.Selectable(themeNames[i], isSelected))
            {
                _selectedThemeIndex = i;
                _themeManager.ApplyTheme(themeNames[i]);
            }
        }
        ImGui.EndCombo();
    }

    ImGui.Spacing();

    // 4. Open Editor button
    if (ImGui.Button("Open Theme Editor"))
    {
        _editorVisible = true;
    }

    ImGui.Spacing();
    ImGui.Separator();

    // 5. Quick preset buttons row
    ImGui.Text("Quick Apply:");
    if (ImGui.Button("Dark")) { _themeManager.ApplyTheme("Dark"); UpdateSelectedIndex(); }
    ImGui.SameLine();
    if (ImGui.Button("Light")) { _themeManager.ApplyTheme("Light"); UpdateSelectedIndex(); }
    ImGui.SameLine();
    if (ImGui.Button("Classic")) { _themeManager.ApplyTheme("Classic"); UpdateSelectedIndex(); }
    ImGui.SameLine();
    if (ImGui.Button("Reset")) { _themeManager.ApplyTheme("Game Default"); UpdateSelectedIndex(); }
}
ImGui.End();
```

Add a `_filterText` string field initialized to `""` for the combobox filter.

Add a helper `UpdateSelectedIndex()` that finds the index of `_themeManager.ActiveThemeName` in `GetThemeNames()` and sets `_selectedThemeIndex`.

### 5.4 Theme Editor Window — `RenderEditorWindow()`

This window wraps ImGui's built-in style editor with a custom save button.

Key API: `ImGui.ShowStyleEditor()` renders the complete ImGui style editor (tabs: Sizes, Colors, Fonts, Rendering) as content inside whatever window is currently active. It modifies the global style in real-time.

```
ImGui.SetNextWindowSize(new float2(700, 800), ImGuiCond.FirstUseEver);
if (ImGui.Begin("Skittles — Theme Editor", ref _editorVisible))
{
    // 1. Save button and name input at the top
    if (!_showSaveInput)
    {
        if (ImGui.Button("Save Current Style as Theme"))
        {
            _showSaveInput = true;
            _saveThemeName = "";
        }
    }
    else
    {
        ImGui.Text("Theme Name:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(250);
        ImGui.InputText("##themename", ref _saveThemeName, 128);
        ImGui.SameLine();
        
        bool nameValid = !string.IsNullOrWhiteSpace(_saveThemeName);
        if (!nameValid) ImGui.BeginDisabled();
        if (ImGui.Button("Save"))
        {
            _themeManager.SaveCurrentAsTheme(_saveThemeName.Trim());
            _showSaveInput = false;
            _saveThemeName = "";
            UpdateSelectedIndex();
            Console.WriteLine($"skittles: Saved theme '{_saveThemeName}'");
        }
        if (!nameValid) ImGui.EndDisabled();
        
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            _showSaveInput = false;
            _saveThemeName = "";
        }
    }
    
    ImGui.Separator();

    // 2. Built-in ImGui style editor
    //    This renders the full editor with Sizes, Colors, Fonts, and Rendering tabs.
    //    All edits apply in real-time to the global style.
    ImGui.ShowStyleEditor();
}
ImGui.End();
```

### 5.5 Notes on `ImGuiTextFilter`

Check in the decompiled sources whether `Brutal.ImGuiApi` exposes `ImGuiTextFilter`. Look in `decomp/ksa/Brutal.ImGuiApi/` for `ImGuiTextFilter.cs`. If it exists, use it for the combobox filter. If not, use the manual `InputText` + `string.Contains` approach described above.

The `ImGui.InputText` call in Brutal's C# wrapper may use `ImString` or `ref string` — check the decompiled `ImGui.InputText` signature to determine the correct parameter type. It may be:
```csharp
public static bool InputText(ImString label, ref ImString buf, uint bufSize, ...)
```
or it may accept `byte[]` buffers. If `ref string` doesn't work directly, allocate a `byte[]` buffer and convert. Look at how other mods in the repository handle text input for reference.

**IMPORTANT**: The implementing agent should verify the `ImGui.InputText` signature in the decompiled sources at `decomp/ksa/Brutal.ImGuiApi/ImGui.cs` and adapt accordingly. Search for `InputText` in that file.

### 5.6 `ImGui.BeginDisabled` / `ImGui.EndDisabled`

These should be available in Brutal's ImGui wrapper. Check decompiled sources to confirm. If not available, skip the disabled state for the Save button and just don't act on empty names.

---

## Task 6: Documentation & Repository Index Updates

**Expert**: Technical writing  
**Goal**: Create README and update the repository index.

### 6.1 Create `skittles/README.md`

Follow the pattern from `glass/README.md`. Content should cover:

- **Title**: Skittles — Global ImGui Theme Manager
- **Overview**: What the mod does (global ImGui theming)
- **Features**: Theme picker, editor, persistence, built-in themes, custom theme creation
- **Usage**:
  - Press F11 to open the theme manager
  - Select a theme from the dropdown to apply it
  - Click "Open Theme Editor" to customize and tweak all style values
  - Click "Save Current Style as Theme" in the editor to save custom themes
  - Custom themes are stored in `My Documents\My Games\Kitten Space Agency\skittles\themes\`
- **Built-in Themes**: Game Default, Dark, Light, Classic, Inanimate Carbon Rod
- **Technical Details**: Uses `ImGui.GetStyle()` to modify the global style — no Harmony patching required for theming itself

### 6.2 Create `skittles.lib/README.md`

Brief description of the library's public API:
- `ThemeDefinition` — data model
- `ThemeSerializer` — TOML I/O
- `ThemeManager` — orchestration

### 6.3 Update `REPOSITORY_INDEX.md`

Add a new section under an appropriate category (e.g., "UI & Customization Mods"):

```markdown
### [skittles](skittles) / [skittles.lib](skittles.lib)
Global ImGui theme manager. Provides a theme picker (filterable combobox) and a built-in style editor for customizing all 60 ImGui color slots and all style variables (padding, rounding, borders, spacing, etc.) globally across the entire application.
- Theme picker with filterable combobox (F11 toggle)
- Built-in themes: Game Default, Dark, Light, Classic, Inanimate Carbon Rod
- Full theme editor wrapping ImGui.ShowStyleEditor()
- Save/load custom themes as TOML files
- Persistent theme selection across game sessions
- **skittles.lib**: `ThemeDefinition` (data model), `ThemeSerializer` (TOML I/O), `ThemeManager` (load/save/apply/list)
```

---

## Implementation Order

Tasks should be implemented in this order due to dependencies:

1. **Task 1**: Project Scaffolding — creates the project structure
2. **Task 2**: Theme Data Model — needed by all other lib code
3. **Task 3**: Theme Serialization — needed by ThemeManager
4. **Task 4**: Theme Manager + Built-in Themes — needed by the mod UI
5. **Task 5**: Mod Lifecycle & UI — the main mod code
6. **Task 6**: Documentation

Each task should be committed separately via the git-commit skill after successful `dotnet build`.

---

## Key Technical References

- **ImGui style access**: `ImGuiStylePtr style = ImGui.GetStyle();` — returns ref-wrapped pointer to global style
- **Style colors**: `style.Colors[(int)ImGuiCol.X]` — read/write `float4` (RGBA 0-1)
- **Style variables**: `style.WindowRounding`, `style.FramePadding`, etc. — direct ref properties
- **Built-in themes**: `ImGui.StyleColorsDark()`, `ImGui.StyleColorsLight()`, `ImGui.StyleColorsClassic()` — apply to current style (colors only)
- **Editor**: `ImGui.ShowStyleEditor()` — renders full editor content inside current window
- **Numerics**: `using Brutal.Numerics;` for `float2`, `float4`
- **ImGui API**: `using Brutal.ImGuiApi;` for `ImGui`, `ImGuiCol`, `ImGuiStyleVar`, `ImGuiStylePtr`
- **inline array**: `Colors` is `float4_60` (inline array of 60 float4s from `Brutal.ImGuiApi.InlineArrays`)
- **RadioactiveGreen**: XKCD #2CFA1F → `float4(0.173f, 0.980f, 0.122f, 1.0f)`
