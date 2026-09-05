# Nonshipping experiments — game integration

Bundled feature standalone entry projects are retired. Marque, steely-eyed-missile-kitten, mesh-deform and stampy are removed; none contributes active patches, shaders or assets. Space-tape, flexo, grant and inanimate-carbon-rod remain retired. The shipping graph is in [REPOSITORY_INDEX](../REPOSITORY_INDEX.md).

The following music experiment remains outside the shipping solution.

## byo-music

**Purpose** — "Bring Your Own Music" — load a `MusicPlayList` asset by id from `ModLibrary` and play it via the KSA/FMOD audio API. Currently a one-button demo wired to the id `"SabotageMusic"`.

**Standalone entry (class+file)** — `MeowSci.ByoMusic.Mod` (`byo-music/Mod.cs`, `[StarMapMod]`). Playback helper: `MeowSci.ByoMusicLib.MusicPlayer` (`byo-music.lib/MusicPlayer.cs`). Not an `ISubmod`; not in unscience.

**UI/hotkeys** — F11 toggles a window with a single "Listen all ya'll" button (`Mod.cs`) that fetches and plays the `SabotageMusic` playlist.

**Persistence** — None.

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|-----------|
| 1 | Direct API | `byo-music.lib/MusicPlayer.cs` | `ModLibrary.Get<MusicPlayList>(string)` | `KSA/ModLibrary.cs`, `KSA/MusicPlayList.cs` (`: SoundReference`) | Yes | None | Generic asset fetch; returns null on miss. |
| 2 | Direct API | `byo-music.lib/MusicPlayer.cs` | `MusicPlayList.PlayMusic(out ChannelWrapper? iChannel, ulong delaySamples = 0)` | `KSA/MusicPlayList.cs` | Yes | None | Mod calls `PlayMusic(out _)`; out-param + optional arg match. Routes through `GameAudio.System` (FMOD `Brutal.FmodApi`). |
| 3 | Asset (sound) | `byo-music/Mod.cs` | `MusicPlayList` asset id `"SabotageMusic"` | `Content/Core/Sounds.xml` (stock `<MusicPlaylist>` blocks) | **No** (not stock) | None — never stock in 4680 or 4750 | Stock playlist ids are location-based: `EarthSOIMusic`, `LunaSOIMusic`, … (`Sounds.xml:522+`). `Get<>("SabotageMusic")` returns null → guarded no-op (`Mod.cs`). Dead unless the user ships their own `SabotageMusic` asset. Pre-existing, not a regression. |
| 4 | Harmony prefix (HotkeyGuard) | `byo-music/Patcher.cs` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool` | `KSA/GameSettings.cs` | Yes | None | Uses shared `MeowSci.KsaAbstractions.HotkeyGuard`. |

**Game assets referenced** — `MusicPlayList "SabotageMusic"` (placeholder; not present in stock Content). Stock alternatives that *do* resolve: `EarthSOIMusic`, `LunaSOIMusic`, and three more in `Content/Core/Sounds.xml`.

**Update-risk findings (4680→4750)** — No breaking deltas detected. `MusicPlayList.PlayMusic` signature and `ModLibrary.Get<T>` are unchanged. The only non-functional condition (missing `SabotageMusic` asset) predates 4680 and is handled by a null check.

---
