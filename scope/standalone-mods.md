<!-- These mods live in the repo but are NOT bundled in the unscience supermod; cataloged as secondary reference. -->
# Standalone Mods — Game Integration Scope

These mods live in the repo but are **NOT** bundled in the unscience supermod; cataloged as secondary reference for KSA game-update breakage.

Covered: `byo-music`.

**Verified game versions**

- NEW decomp `2026.9.7.5402`: `~/repos/meow-sci/ksa-game-assemblies/current/decomp`
- OLD decomp `2026.8.22.5348`: `~/repos/meow-sci/ksa-game-assemblies_prev/current/decomp`
- NEW Content: `~/repos/meow-sci/ksa-game-assemblies/current/Content`

`Decomp path (NEW)` is relative to the NEW decomp root (namespace-foldered, e.g. `KSA/Vehicle.cs`); line numbers are 5402 and "OLD" means 5348.
`Mod code` paths are relative to the repo root `~/repos/meow-sci/unscience`.
The mod **compiles clean against NEW (5402)**, so every *typed* member below is implicitly present in NEW; the focus is asset ids and behavioral deltas that the compiler cannot check.

**Headline risk summary**

- `byo-music` — NO breaking deltas (asset id `SabotageMusic` is a placeholder, never stock in either version).

---

## byo-music

**Purpose** — "Bring Your Own Music" — load a `MusicPlayList` asset by id from `ModLibrary` and play it via the KSA/FMOD audio API. Currently a one-button demo wired to the id `"SabotageMusic"`.

**Standalone entry (class+file)** — `MeowSci.ByoMusic.Mod` (`byo-music/Mod.cs`, `[StarMapMod]`). Playback helper: `MeowSci.ByoMusicLib.MusicPlayer` (`byo-music.lib/MusicPlayer.cs`). Not an `ISubmod`; not in unscience.

**UI/hotkeys** — F11 toggles a window with a single "Listen all ya'll" button (`Mod.cs:90-105`) that fetches and plays the `SabotageMusic` playlist.

**Persistence** — None.

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|-----------|
| 1 | Direct API | `byo-music.lib/MusicPlayer.cs:8` | `ModLibrary.Get<MusicPlayList>(string)` | `KSA/ModLibrary.cs`, `KSA/MusicPlayList.cs:6` (`: SoundReference`) | Yes | None | Generic asset fetch; returns null on miss. |
| 2 | Direct API | `byo-music.lib/MusicPlayer.cs:10` | `MusicPlayList.PlayMusic(out ChannelWrapper? iChannel, ulong delaySamples = 0)` | `KSA/MusicPlayList.cs:21` | Yes | None | Mod calls `PlayMusic(out _)`; out-param + optional arg match. Routes through `GameAudio.System` (FMOD `Brutal.FmodApi`). |
| 3 | Asset (sound) | `byo-music/Mod.cs:99` | `MusicPlayList` asset id `"SabotageMusic"` | `Content/Core/Sounds.xml` (stock `<MusicPlaylist>` blocks) | **No** (not stock) | None — never stock in 4680 or 4750 | Stock playlist ids are location-based: `EarthSOIMusic`, `LunaSOIMusic`, … (`Sounds.xml:522+`). `Get<>("SabotageMusic")` returns null → guarded no-op (`Mod.cs:101`). Dead unless the user ships their own `SabotageMusic` asset. Pre-existing, not a regression. |
| 4 | Harmony prefix (HotkeyGuard) | `byo-music/Patcher.cs:19` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool` | `KSA/GameSettings.cs:3301` | Yes | None | Uses shared `MeowSci.KsaAbstractions.HotkeyGuard`. |

**Game assets referenced** — `MusicPlayList "SabotageMusic"` (placeholder; not present in stock Content). Stock alternatives that *do* resolve: `EarthSOIMusic`, `LunaSOIMusic`, and three more in `Content/Core/Sounds.xml`.

**Update-risk findings (4680→4750)** — No breaking deltas detected. `MusicPlayList.PlayMusic` signature and `ModLibrary.Get<T>` are unchanged. The only non-functional condition (missing `SabotageMusic` asset) predates 4680 and is handled by a null check.

## Current area summary

- `byo-music` owns one null-guarded `ModLibrary.Get<MusicPlayList>("SabotageMusic")` lookup plus
  game-audio play/stop calls. The placeholder id is not present in stock Content.

---
