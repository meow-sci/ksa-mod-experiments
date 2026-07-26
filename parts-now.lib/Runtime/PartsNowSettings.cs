// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Globalization;
using System.IO;
using KSA;
using Tomlyn;
using Tomlyn.Model;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Persisted parts-now configuration, stored as TOML next to the mod's own folder in the
/// discovered KSA mods directory (<c>&lt;mods&gt;/parts-now/parts-now.toml</c>).
/// </summary>
/// <remarks>
/// The mesh headroom values are consumed by <see cref="MeshBudget.Reserve" />, which runs once
/// during startup, so <b>changes to the headroom take effect on the NEXT launch of the game</b>.
/// The UI must say so. Loading is lazy and idempotent; nothing here ever throws.
/// </remarks>
public static class PartsNowSettings
{
    /// <summary>Default shared vertex-buffer headroom, in MiB.</summary>
    public const int DefaultVertexHeadroomMiB = 48;

    /// <summary>Default shared index-buffer headroom, in MiB.</summary>
    public const int DefaultIndexHeadroomMiB = 12;

    /// <summary>Default window hotkey.</summary>
    public const string DefaultHotkey = "F10";

    /// <summary>Smallest accepted headroom value, in MiB.</summary>
    public const int MinHeadroomMiB = 4;

    /// <summary>Largest accepted headroom value, in MiB.</summary>
    public const int MaxHeadroomMiB = 512;

    private const string VertexHeadroomKey = "vertexHeadroomMiB";
    private const string IndexHeadroomKey = "indexHeadroomMiB";
    private const string HotkeyKey = "hotkey";

    private static bool _loaded;
    private static string? _filePath;
    private static int _vertexHeadroomMiB = DefaultVertexHeadroomMiB;
    private static int _indexHeadroomMiB = DefaultIndexHeadroomMiB;
    private static string _hotkey = DefaultHotkey;

    /// <summary>
    /// Absolute path of <c>parts-now.toml</c>, derived from
    /// <see cref="ModLibrary.LocalModsFolderPath" />. Empty when the mods folder cannot be resolved.
    /// </summary>
    public static string FilePath
    {
        get
        {
            if (_filePath != null)
                return _filePath;

            try
            {
                _filePath = Path.Combine(ModLibrary.LocalModsFolderPath, "parts-now", "parts-now.toml");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"parts-now: could not resolve the mods folder: {ex.Message}");
                _filePath = string.Empty;
            }

            return _filePath;
        }
    }

    /// <summary>
    /// Shared vertex-buffer headroom, in MiB. Clamped to
    /// <see cref="MinHeadroomMiB" />..<see cref="MaxHeadroomMiB" />.
    /// Takes effect on the next launch of the game.
    /// </summary>
    public static int VertexHeadroomMiB
    {
        get { Load(); return _vertexHeadroomMiB; }
        set { Load(); _vertexHeadroomMiB = ClampHeadroom(value); }
    }

    /// <summary>
    /// Shared index-buffer headroom, in MiB. Clamped to
    /// <see cref="MinHeadroomMiB" />..<see cref="MaxHeadroomMiB" />.
    /// Takes effect on the next launch of the game.
    /// </summary>
    public static int IndexHeadroomMiB
    {
        get { Load(); return _indexHeadroomMiB; }
        set { Load(); _indexHeadroomMiB = ClampHeadroom(value); }
    }

    /// <summary>Key name that toggles the standalone parts-now window.</summary>
    public static string Hotkey
    {
        get { Load(); return _hotkey; }
        set { Load(); _hotkey = string.IsNullOrWhiteSpace(value) ? DefaultHotkey : value.Trim(); }
    }

    /// <summary>
    /// Reads <c>parts-now.toml</c> if it has not been read yet. Idempotent and non-throwing;
    /// a missing file simply leaves the defaults in place and writes nothing to disk.
    /// </summary>
    public static void Load()
    {
        if (_loaded)
            return;

        // Set first: a failure below must not cause an infinite retry from every property getter.
        _loaded = true;

        var path = FilePath;
        if (path.Length == 0 || !File.Exists(path))
            return;

        try
        {
            var table = Toml.ToModel(File.ReadAllText(path));
            _vertexHeadroomMiB = ClampHeadroom(ReadInt(table, VertexHeadroomKey, DefaultVertexHeadroomMiB));
            _indexHeadroomMiB = ClampHeadroom(ReadInt(table, IndexHeadroomKey, DefaultIndexHeadroomMiB));
            _hotkey = ReadString(table, HotkeyKey, DefaultHotkey);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: failed to read {path}: {ex.Message} — using defaults.");
        }
    }

    /// <summary>
    /// Writes the current values to <c>parts-now.toml</c>, creating the folder if needed.
    /// Non-throwing; failures are logged.
    /// </summary>
    public static void Save()
    {
        Load();

        var path = FilePath;
        if (path.Length == 0)
            return;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var table = new TomlTable
            {
                [VertexHeadroomKey] = (long)_vertexHeadroomMiB,
                [IndexHeadroomKey] = (long)_indexHeadroomMiB,
                [HotkeyKey] = _hotkey
            };

            File.WriteAllText(path, Toml.FromModel(table));
            Console.WriteLine($"parts-now: saved settings to {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: failed to save {path}: {ex.Message}");
        }
    }

    private static int ReadInt(TomlTable table, string key, int fallback)
    {
        if (!table.TryGetValue(key, out var value) || value == null)
            return fallback;

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: '{key}' is not a number ({ex.Message}) — using {fallback}.");
            return fallback;
        }
    }

    private static string ReadString(TomlTable table, string key, string fallback)
    {
        if (!table.TryGetValue(key, out var value) || value is not string text || text.Trim().Length == 0)
            return fallback;

        return text.Trim();
    }

    private static int ClampHeadroom(int megabytes)
    {
        return Math.Clamp(megabytes, MinHeadroomMiB, MaxHeadroomMiB);
    }
}
