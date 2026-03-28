# con-man

game ui layout manager.  can save the current game ui state to disk under a inputtable name.

can select saved layouts from a combobox and apply them from the games Documents/My Games/Kitten Space Agency/.con-man folder (create if doesnt exist) like this

```csharp
        var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var productionConfigRoot = Path.Combine(myDocuments, "My Games", "Kitten Space Agency");
        _configDirectory = Path.Combine(productionConfigRoot, ".con-man");

```

allow selecting a "startup default" value, when the mod initially starts, apply it if set. should also be un-settable.

all comboboxes should be filtered and values sorted case-insensitive alphanumerically (cache this so its not re-computed on every game tick render)

# plan

## research findings

### GaugeCanvas — the gauge window system

The game's HUD gauges are **not** regular ImGui windows. They are `GaugeCanvas` instances — each one renders custom GPU content to a texture, then displays it inside an ImGui window via `ImGui.ImageWithBg()`.

**Class hierarchy:**

```
SerializedId (abstract)  — base with Id, IsReferenceable, Hash, Mod
  └─ GaugeBase (abstract) — Anchor, Pivot, Offset, Size, scale/aspect logic
       ├─ GaugeCanvas   — a gauge "window" containing components
       └─ GaugeComponent — an individual gauge element rendered inside a canvas
```

Key source files: `decomp/ksa/KSA/GaugeCanvas.cs`, `GaugeBase.cs`, `GaugeComponent.cs`, `SerializedId.cs`

### how to identify each gauge canvas

Each `GaugeCanvas` has a **stable XML-defined Id** from the `SerializedId` base class:

```csharp
[XmlAttribute]
public string Id { get; set; } = string.Empty;  // e.g. "navball", "altimeter"
```

This is the **only truly stable identifier** across sessions. The `Id` is defined by the game's XML data files and never changes.

There is also:

- `DisplayName` — human-readable label (`StringReference`, from XML, e.g. `"Navball"`). Not guaranteed unique.
- `_windowTitle` — ImGui window title, built as `"{DisplayName}###GaugeCanvas_{N}"` where N is a runtime counter. The `###` part is **session-deterministic** (same if mod load order is stable) but not persisted anywhere.

**Use `Id` as the key for per-gauge persistence.**

### how gauge canvas tracks position and size

GaugeCanvas has **private instance fields** that capture user repositioning relative to the XML baseline:

```csharp
private float2 _customOffset;                    // drag offset from base position
private float2 _customScale = float2.One;        // resize scale relative to base size
```

When the ImGui window is created each frame in `OnDrawUi()`:

```csharp
float2 pos = ImGui.GetMainViewport().Pos + GetPixelsMin();   // base pos + offset
float2 size = GetPixelsSize();                                 // base size × scale

ImGui.SetNextWindowPos(in pos, ImGuiCond.Appearing);          // only on first appear
ImGui.SetNextWindowSize(in size, ImGuiCond.Appearing);
ImGui.SetNextWindowSizeConstraints(new float2(2f), new float2(2048f), _aspectRatioConstraint);
ImGui.Begin(_windowTitle, imGuiWindowFlags);
```

**Key flags include `ImGuiWindowFlags.NoSavedSettings`** — ImGui does NOT persist gauge positions via its INI system. The position/size is entirely controlled by `_customOffset` and `_customScale`.

When dragging/resizing:
```csharp
// NOT dragging — record baseline
_windowPosition = ImGui.GetWindowPos() - _customOffset;
_windowSize = ImGui.GetWindowSize() / _customScale;

// Dragging — compute delta
_customOffset = ImGui.GetWindowPos() - _windowPosition;
_customScale = ImGui.GetWindowSize() / _windowSize;
```

**Saving `_customOffset` (float2) and `_customScale` (float2) per canvas fully captures user layout.**

### how gauge enable/disable works

Each `GaugeCanvas` has a **private instance field**:

```csharp
[XmlIgnore]
private bool _enabled = true;
```

When `_enabled` is `false`, `OnDrawUi()` returns immediately — the gauge is not drawn.

**The game has a built-in toggle** — `GaugeCanvas.OnDrawMenuBar()` iterates `_canvases` and renders each as a checkable `ImGui.MenuItem`:

```csharp
public static void OnDrawMenuBar()
{
    foreach (GaugeCanvas canvase in _canvases)
    {
        ImString label = canvase._windowTitle;
        bool enabled = canvase._enabled;
        if (ImGui.MenuItem(label, default(ImString), enabled))
        {
            canvase._enabled = !canvase._enabled;
        }
    }
}
```

There is also a per-canvas right-click context menu with an "Enabled" toggle.

**Critical: the game does NOT persist `_enabled` state.** It resets to `true` every launch. This means con-man is the **only** way to save/restore which gauges are visible.

### how to access private fields from a mod

All three key fields (`_canvases`, `_enabled`, `_customOffset`, `_customScale`) are `private`. Access via **reflection** (established pattern in this codebase):

```csharp
// Static field: _canvases
var canvasesField = typeof(GaugeCanvas).GetField("_canvases",
    BindingFlags.NonPublic | BindingFlags.Static);
var canvases = canvasesField?.GetValue(null) as List<GaugeCanvas>;

// Instance fields: _enabled, _customOffset, _customScale
var enabledField = typeof(GaugeCanvas).GetField("_enabled",
    BindingFlags.NonPublic | BindingFlags.Instance);
var offsetField = typeof(GaugeCanvas).GetField("_customOffset",
    BindingFlags.NonPublic | BindingFlags.Instance);
var scaleField = typeof(GaugeCanvas).GetField("_customScale",
    BindingFlags.NonPublic | BindingFlags.Instance);

// Read
bool enabled = (bool)enabledField.GetValue(canvas);
float2 offset = (float2)offsetField.GetValue(canvas);
float2 scale = (float2)scaleField.GetValue(canvas);

// Write
enabledField.SetValue(canvas, true);
offsetField.SetValue(canvas, savedOffset);
scaleField.SetValue(canvas, savedScale);
```

The codebase already has `ReflectionHelpers` in `ksa-abstractions.lib` and Harmony `AccessTools` patterns for this.

### base position system (for reference)

Gauge base positions come from XML data using an anchor/offset/pivot system:

```csharp
// GaugeBase fields (from XML)
public ScreenReference Anchor;   // UV (0-1) or Pixel — screen anchor point
public ScreenReference Offset;   // UV or Pixel — offset from anchor
public PixelReference Pivot;     // pivot point
public PixelReference Size;      // base size (UV or Pixel)
```

Position formula: `Anchor × screenSize + Offset - Pivot × Size × screenSize`

The `_customOffset` and `_customScale` represent deltas from this base. `Recalculate()` resets them to zero/one. **We save/restore only the deltas** — the base position is the game's responsibility.

### filtered combobox pattern (established in codebase)

Multiple existing mods use the same pattern:

```csharp
private ImGuiTextFilter _filter = new ImGuiTextFilter();

if (ImGui.BeginCombo("Label##id", preview))
{
    if (ImGui.IsWindowAppearing())
    {
        ImGui.SetKeyboardFocusHere();
        _filter.Clear();
    }
    _filter.Draw("##filter", -1f);

    for (int i = 0; i < items.Length; i++)
    {
        if (!_filter.PassFilter(items[i])) continue;
        bool sel = selectedIndex == i;
        if (ImGui.Selectable(items[i], sel)) selectedIndex = i;
        if (sel) ImGui.SetItemDefaultFocus();
    }
    ImGui.EndCombo();
}
```

### persistence pattern (established in codebase)

The repository uses **TOML via Tomlyn** for all persistence (see skittles.lib for reference). Standard path pattern:

```csharp
var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
var root = Path.Combine(myDocuments, "My Games", "Kitten Space Agency", ".con-man");
Directory.CreateDirectory(root);
```

All I/O wrapped in try-catch with `Console.WriteLine` logging and graceful fallback to defaults.

---

## implementation approach

### scope: gauge canvases only

con-man manages **only** `GaugeCanvas` windows — the game's HUD gauges. It does NOT manage regular ImGui windows (mod windows, debug tools, etc.). This keeps the scope focused and avoids interfering with other mods.

### what gets saved per layout

For each `GaugeCanvas` in the game, a layout captures:

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `string` | Stable XML-defined gauge identifier (the key) |
| `_enabled` | `bool` | Whether the gauge is visible |
| `_customOffset` | `float2` | Drag offset from base position (X, Y) |
| `_customScale` | `float2` | Resize scale relative to base size (X, Y) |

A layout is a **complete snapshot** of all gauge canvases — their visibility and positioning.

### file structure

```
con-man/          # mod entry point (thin, references con-man.lib)
con-man.lib/      # all logic lives here
  ConManSubmod.cs           # main submod: UI rendering, lifecycle
  LayoutManager.cs          # save/load/delete/list layouts, manage startup default
  GaugeStateAccessor.cs     # reflection-based access to GaugeCanvas private fields
  LayoutSerializer.cs       # TOML serialization for layout files and config
```

### persistence format

**One TOML config file** (`config.toml`) in the `.con-man` directory:

```toml
[settings]
startup_default = "my-flight-layout"   # empty string or missing = no default
```

**One TOML file per saved layout** in `.con-man/layouts/`:

```
.con-man/
  config.toml
  layouts/
    my-flight-layout.toml
    map-view-clean.toml
    docking-debug.toml
```

**Layout TOML format** — each gauge is a section keyed by its stable `Id`:

```toml
[gauges.navball]
enabled = true
offset_x = 12.5
offset_y = -3.0
scale_x = 1.2
scale_y = 1.2

[gauges.altimeter]
enabled = false
offset_x = 0.0
offset_y = 0.0
scale_x = 1.0
scale_y = 1.0

[gauges.throttle_gauge]
enabled = true
offset_x = 50.0
offset_y = 0.0
scale_x = 0.8
scale_y = 0.8
```

The layout display name is derived from the filename (strip `.toml`).

### GaugeStateAccessor — reflection wrapper

Encapsulates all reflection access to `GaugeCanvas` private fields. Caches `FieldInfo` objects at construction time so reflection lookup happens once:

```csharp
public class GaugeStateAccessor
{
    // Cached FieldInfo (resolved once in constructor)
    private readonly FieldInfo _canvasesField;   // static: List<GaugeCanvas>
    private readonly FieldInfo _enabledField;    // instance: bool
    private readonly FieldInfo _offsetField;     // instance: float2
    private readonly FieldInfo _scaleField;      // instance: float2

    public List<GaugeCanvas> GetCanvases();
    public bool GetEnabled(GaugeCanvas canvas);
    public void SetEnabled(GaugeCanvas canvas, bool value);
    public float2 GetCustomOffset(GaugeCanvas canvas);
    public void SetCustomOffset(GaugeCanvas canvas, float2 value);
    public float2 GetCustomScale(GaugeCanvas canvas);
    public void SetCustomScale(GaugeCanvas canvas, float2 value);
}
```

### core operations

| Operation | Implementation |
|-----------|---------------|
| **Save current layout** | Iterate `_canvases` → read `Id`, `_enabled`, `_customOffset`, `_customScale` per canvas → write to `layouts/{name}.toml` |
| **Load/apply layout** | Read `layouts/{name}.toml` → for each gauge entry, find canvas by `Id` → set `_enabled`, `_customOffset`, `_customScale` via reflection |
| **List layouts** | `Directory.GetFiles(layoutDir, "*.toml")` → sort case-insensitive → cache |
| **Delete layout** | `File.Delete(path)` → invalidate cache |
| **Set startup default** | Write name to `config.toml` `[settings].startup_default` |
| **Clear startup default** | Set `startup_default = ""` in `config.toml` |
| **Apply on startup** | In mod init, if startup default is set and file exists, load and apply it |

### applying a layout — detailed flow

1. Read TOML file → deserialize into `Dictionary<string, GaugeState>` (keyed by `Id`)
2. Get `_canvases` list via reflection
3. For each canvas:
   - Look up canvas `Id` in the dictionary
   - If found: set `_enabled`, `_customOffset`, `_customScale` from saved values
   - If NOT found in layout (new gauge added since layout was saved): leave as-is (default enabled, default position)
4. Gauges in the layout file that no longer exist in the game are silently ignored (forward compatibility)

### UI design

The mod window contains:

1. **Layout selector** — filtered combobox of saved layouts (sorted case-insensitive, cached)
   - "Apply" button next to it
2. **Save section** — `ImInputString` text field for layout name + "Save" button
   - Overwrites if name already exists (with confirmation if desired)
3. **Startup default section** — filtered combobox showing saved layouts + "(None)" option
   - Selecting a layout sets it as startup default
   - Selecting "(None)" clears the startup default
   - Current default shown as label
4. **Delete button** — deletes the currently selected layout (with confirmation popup)
5. **Live gauge summary** — collapsible table showing all detected `GaugeCanvas` instances with live state:

   | Column | Source | Description |
   |--------|--------|-------------|
   | Name | `DisplayName` | Human-readable gauge name (e.g. "Navball") |
   | Id | `SerializedId.Id` | Stable identifier used as persistence key |
   | Enabled | `_enabled` | Current visibility — shown as checkbox (read-only, reflects game state) |
   | Offset | `_customOffset` | Current drag offset (X, Y) |
   | Scale | `_customScale` | Current resize scale (X, Y) |

   This section is wrapped in `ImGui.CollapsingHeader("Gauges")` so it can be collapsed when not needed. Values are read via `GaugeStateAccessor` each frame (reflection is cached, so the per-frame cost is just field reads). The table provides:
   - Visibility into what gauges exist in the current game session
   - Quick verification that a layout was applied correctly
   - Debugging aid when creating/tweaking layouts

### caching strategy

- `_sortedLayoutNames` — `string[]` cached on first access and invalidated on save/delete
- Sort via `Array.Sort(names, StringComparer.OrdinalIgnoreCase)`
- Rebuild only when filesystem changes (save, delete, or manual refresh button)

### startup flow

1. Mod initializes → `GaugeStateAccessor` resolves all `FieldInfo` (fail fast if game structure changed)
2. Create `.con-man/` and `layouts/` directories if missing
3. Load `config.toml` (or create default)
4. If `startup_default` is set and `layouts/{name}.toml` exists:
   - Read and deserialize layout TOML
   - Apply per-gauge state via reflection (enabled, offset, scale)
5. Cache layout name list

### error handling

- All file I/O in try-catch with `Console.WriteLine($"[con-man] ...")` logging
- Missing/corrupt `config.toml` → treat as empty config (no startup default)
- Missing layout `.toml` file referenced as startup default → log warning, skip apply
- Invalid layout name (empty, filesystem-unsafe chars) → reject with UI feedback
- Reflection failure (field not found) → log error on startup, disable mod gracefully
- Unknown gauge Ids in layout file → silently ignore (gauge was removed from game)
- Missing gauge Ids in layout file → leave at defaults (gauge was added after layout was saved)

### key implementation notes

- **Gauge identification:** Use `SerializedId.Id` (the XML `Id` attribute) as the stable key. This survives across sessions, mod load order changes, and game updates (as long as the game doesn't rename gauge Ids).
- **Reflection caching:** Resolve all `FieldInfo` objects once at startup. Store them in `GaugeStateAccessor`. This avoids per-frame reflection overhead.
- **`_customOffset`/`_customScale` are deltas:** They represent user modifications on top of the XML base position. Setting them to `float2.Zero`/`float2.One` resets to defaults. The base positions are resolution-aware (UV-based), so saved offsets should work across resolution changes (the base adapts, the delta stays the same).
- **`_enabled` is runtime-only:** The game resets all gauges to `_enabled = true` on every launch. Con-man's startup default is the only way to persist visibility preferences.
- **No Harmony needed:** Pure reflection is sufficient — we read/write fields, we don't need to intercept method calls. This avoids Harmony dependency and keeps the mod simple.
- **Layout names** should be sanitized for filesystem safety (strip path separators, limit length)
