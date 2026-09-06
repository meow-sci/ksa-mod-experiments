// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do not introduce background access to KSA state; parts-now must remain safe standalone.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Validates a candidate mod id before parts-now creates a folder for it, and resolves where that
/// folder would live.
/// </summary>
/// <remarks>
/// Every rule is an error — there are no warnings. Each rule runs inside its own try/catch so a
/// single failing lookup cannot mask the others, and a rule that <i>cannot</i> run is reported as a
/// validation failure rather than silently passing (fail closed): creating a folder that collides
/// with an existing mod is unrecoverable from inside the game.
/// </remarks>
public static class ModIdValidator
{
    /// <summary>Shortest accepted mod id.</summary>
    public const int MinLength = 3;

    /// <summary>Longest accepted mod id.</summary>
    public const int MaxLength = 48;

    /// <summary>Mod ids parts-now refuses to create, compared case-insensitively.</summary>
    public static readonly IReadOnlyList<string> ReservedIds =
        new[] { "Core", "Sample", "parts-now", "unscience" };

    // Same kebab-case shape mkmod.ts enforces when it scaffolds a mod on disk.
    private static readonly Regex KebabCase =
        new Regex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    private static string? _modsDirectory;

    /// <summary>
    /// The mods directory KSA itself discovered (<see cref="ModLibrary.LocalModsFolderPath" />).
    /// Empty when the game could not resolve it. Never hardcode a path in its place.
    /// </summary>
    public static string ModsDirectory => _modsDirectory ??= ResolveModsDirectory();

    /// <summary>
    /// The absolute folder a given mod id would be written to. Returns an empty string when the
    /// mods directory is unknown or the id cannot form a path.
    /// </summary>
    /// <param name="modId">The candidate mod id.</param>
    public static string ResolveTargetPath(string modId)
    {
        string root = ModsDirectory;
        string id = (modId ?? string.Empty).Trim();

        if (root.Length == 0 || id.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(Path.Combine(root, id));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: could not resolve a folder for mod id '{id}': {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Returns every reason the mod id is unusable. An empty list means the id is valid and the
    /// folder can be created.
    /// </summary>
    /// <param name="modId">The candidate mod id.</param>
    public static List<string> Validate(string modId)
    {
        var problems = new List<string>();
        string id = (modId ?? string.Empty).Trim();

        if (id.Length == 0)
        {
            problems.Add("mod id is empty.");
            return problems;
        }

        CheckShape(id, problems);
        Check(problems, "reserved names", () => CheckReserved(id));
        Check(problems, "the mods folder", () => CheckModsFolder(id));
        Check(problems, "the game's Content folder", () => CheckContentFolder(id));
        Check(problems, "loaded mods", () => CheckLoadedMods(id));
        Check(problems, "the mod manifest", () => CheckManifest(id));

        return problems;
    }

    /// <summary>True when <see cref="Validate" /> found no problems.</summary>
    /// <param name="modId">The candidate mod id.</param>
    public static bool IsValid(string modId) => Validate(modId).Count == 0;

    private static void CheckShape(string id, List<string> problems)
    {
        if (id.Length < MinLength || id.Length > MaxLength)
        {
            problems.Add($"mod id must be {MinLength}–{MaxLength} characters (this one is {id.Length}).");
        }

        try
        {
            if (!KebabCase.IsMatch(id))
            {
                problems.Add(
                    "mod id must be lower-case kebab-case: letters a–z, digits, and single hyphens "
                    + "between them (e.g. 'my-new-parts').");
            }
        }
        catch (Exception ex)
        {
            problems.Add($"could not check the mod id's shape ({ex.Message}) — refusing to continue.");
            Console.WriteLine($"parts-now: kebab-case check failed for '{id}': {ex}");
        }
    }

    private static string? CheckReserved(string id)
    {
        foreach (string reserved in ReservedIds)
        {
            if (string.Equals(id, reserved, StringComparison.OrdinalIgnoreCase))
            {
                return $"'{reserved}' is a reserved mod id and cannot be used.";
            }
        }

        return null;
    }

    private static string? CheckModsFolder(string id)
    {
        string root = ModsDirectory;
        if (root.Length == 0)
        {
            return "the game's mods folder could not be resolved, so a collision cannot be ruled out.";
        }

        string target = Path.Combine(root, id);
        return Directory.Exists(target) ? $"a folder already exists at {target}." : null;
    }

    private static string? CheckContentFolder(string id)
    {
        // Relative on purpose: the game's working directory is its install folder, so this is the
        // same "Content/<id>" path ModLibrary.PrepareAll() probes for core mods.
        string target = Path.Combine(ModLibrary.CONTENT_FOLDER, id);
        return Directory.Exists(target)
            ? $"'{id}' collides with the game's built-in mod at {target}."
            : null;
    }

    private static string? CheckLoadedMods(string id)
    {
        return ModLibrary.Find(id) != null
            ? $"a mod with id '{id}' is already loaded."
            : null;
    }

    private static string? CheckManifest(string id)
    {
        // ModLibrary.Manifest is a public static field initialised to null; only PrepareManifest()
        // fills it. If it is still null we cannot prove the id is free, so we fail closed.
        ModManifest? manifest = ModLibrary.Manifest;
        if (manifest?.Mods == null)
        {
            return "the game's mod manifest is not available yet, so a collision cannot be ruled out.";
        }

        foreach (ModEntry entry in manifest.Mods)
        {
            if (entry != null && string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return $"'{id}' is already listed in the game's mod manifest.";
            }
        }

        return null;
    }

    private static void Check(List<string> problems, string what, Func<string?> rule)
    {
        try
        {
            string? problem = rule();
            if (problem != null)
            {
                problems.Add(problem);
            }
        }
        catch (Exception ex)
        {
            problems.Add($"could not check {what} ({ex.Message}) — refusing to continue.");
            Console.WriteLine($"parts-now: mod id check against {what} failed: {ex}");
        }
    }

    private static string ResolveModsDirectory()
    {
        string fromGame;
        try
        {
            fromGame = ModLibrary.LocalModsFolderPath ?? string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: the game could not resolve its mods folder: {ex.Message}");
            return string.Empty;
        }

        if (fromGame.Length == 0)
        {
            Console.WriteLine("parts-now: the game reported an empty mods folder path.");
            return string.Empty;
        }

        try
        {
            string expected = Path.Combine(KsaPaths.UserDataDir, "mods");
            if (!SamePath(fromGame, expected))
            {
                Console.WriteLine(
                    $"parts-now: the game's mods folder '{fromGame}' differs from the expected "
                    + $"'{expected}' — using the game's value.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: could not sanity-check the mods folder: {ex.Message}");
        }

        return fromGame;
    }

    private static bool SamePath(string a, string b)
    {
        string normalisedA = Path.TrimEndingDirectorySeparator(Path.GetFullPath(a));
        string normalisedB = Path.TrimEndingDirectorySeparator(Path.GetFullPath(b));
        return string.Equals(normalisedA, normalisedB, StringComparison.OrdinalIgnoreCase);
    }
}
