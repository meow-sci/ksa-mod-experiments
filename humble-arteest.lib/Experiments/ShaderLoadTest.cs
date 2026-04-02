using System;
using System.IO;
using System.Reflection;

namespace MeowSci.HumbleArteestLib.Experiments;

/// <summary>
/// Experiment 0.1: Shader File Loading Test
///
/// Tests whether KSA loads GLSL shader sources from disk at runtime.
/// Changes the Highlighted part color from red (1.0, 0.0, 0.0) to bright green (0.0, 1.0, 0.0)
/// in MeshIndirect.frag. If hovering over a part in the editor shows green instead of red,
/// shaders are loaded from source files → Approach A is viable.
///
/// Requires a game restart after applying/restoring the shader modification.
/// </summary>
public static class ShaderLoadTest
{
    private const string ShaderRelPath = @"Content\Core\Shaders\Mesh\MeshIndirect.frag";
    private const string BackupSuffix = ".humble-arteest-backup";

    // The exact string to find and replace in the shader
    private const string OriginalHighlight = "preColor = mix(preColor, vec4(1.0, 0.0, 0.0, preColor.a), 0.5);";
    private const string ModifiedHighlight = "preColor = mix(preColor, vec4(0.0, 1.0, 0.0, preColor.a), 0.5);";

    private const string DefaultKsaDir = @"C:\Program Files\Kitten Space Agency";

    private static string? _gameDir;
    private static string? _shaderPath;
    private static string? _backupPath;
    private static string? _lastError;

    public static string? LastError => _lastError;

    /// <summary>Current state of the shader file on disk.</summary>
    public enum ShaderState
    {
        Unknown,
        Original,
        Modified,
        BackupExists,
        FileNotFound,
        Error
    }

    /// <summary>
    /// Resolves the KSA game directory. Strategy:
    /// 1. Try the KSA.dll assembly location (loaded from the game directory).
    /// 2. Fall back to the well-known default install path.
    /// Note: Process.MainModule points to the StarMap mod loader, not KSA.
    /// </summary>
    private static bool ResolvePaths()
    {
        if (_shaderPath != null) return true;

        try
        {
            // Try to find game dir from the KSA assembly location
            _gameDir = TryGetGameDirFromAssembly();

            // Fall back to default install path
            if (_gameDir == null)
            {
                Console.WriteLine("humble-arteest: Could not resolve game dir from KSA assembly, using default path.");
                _gameDir = DefaultKsaDir;
            }

            _shaderPath = Path.Combine(_gameDir, ShaderRelPath);
            _backupPath = _shaderPath + BackupSuffix;

            Console.WriteLine($"humble-arteest: Game directory: {_gameDir}");
            Console.WriteLine($"humble-arteest: Shader path: {_shaderPath}");

            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Error resolving paths: {ex.Message}";
            Console.WriteLine($"humble-arteest: {_lastError}");
            return false;
        }
    }

    private static string? TryGetGameDirFromAssembly()
    {
        try
        {
            // Look for the KSA assembly among loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "KSA" && !string.IsNullOrEmpty(asm.Location))
                {
                    var dir = Path.GetDirectoryName(asm.Location);
                    if (dir != null && File.Exists(Path.Combine(dir, ShaderRelPath)))
                    {
                        Console.WriteLine($"humble-arteest: Found KSA assembly at: {asm.Location}");
                        return dir;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"humble-arteest: Assembly location lookup failed: {ex.Message}");
        }
        return null;
    }

    /// <summary>Checks the current state of the shader file.</summary>
    public static ShaderState GetState()
    {
        _lastError = null;

        if (!ResolvePaths()) return ShaderState.Error;

        try
        {
            if (!File.Exists(_shaderPath))
            {
                _lastError = $"Shader file not found at: {_shaderPath}";
                return ShaderState.FileNotFound;
            }

            var content = File.ReadAllText(_shaderPath);

            if (content.Contains(ModifiedHighlight))
                return ShaderState.Modified;

            if (content.Contains(OriginalHighlight))
                return File.Exists(_backupPath) ? ShaderState.BackupExists : ShaderState.Original;

            _lastError = "Shader file exists but does not contain expected highlight code. Game version may differ.";
            return ShaderState.Unknown;
        }
        catch (Exception ex)
        {
            _lastError = $"Error reading shader: {ex.Message}";
            return ShaderState.Error;
        }
    }

    /// <summary>
    /// Applies the shader modification: backs up original, writes modified version.
    /// Returns true on success.
    /// </summary>
    public static bool ApplyModification()
    {
        _lastError = null;

        if (!ResolvePaths()) return false;

        try
        {
            if (!File.Exists(_shaderPath))
            {
                _lastError = $"Shader file not found: {_shaderPath}";
                return false;
            }

            var content = File.ReadAllText(_shaderPath);

            if (!content.Contains(OriginalHighlight))
            {
                if (content.Contains(ModifiedHighlight))
                {
                    _lastError = "Shader is already modified.";
                    return false;
                }
                _lastError = "Could not find expected highlight code in shader. Game version may differ.";
                return false;
            }

            // Backup original
            if (!File.Exists(_backupPath!))
            {
                File.Copy(_shaderPath!, _backupPath!, overwrite: false);
                Console.WriteLine($"humble-arteest: Backed up shader to: {_backupPath}");
            }

            // Apply modification
            var modified = content.Replace(OriginalHighlight, ModifiedHighlight);
            File.WriteAllText(_shaderPath, modified);
            Console.WriteLine("humble-arteest: Shader modified — highlight color changed from RED to GREEN.");
            Console.WriteLine("humble-arteest: RESTART THE GAME to see the effect.");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _lastError = "Permission denied. The game may need to run as administrator to modify shaders in the install directory.";
            Console.WriteLine($"humble-arteest: {_lastError}");
            return false;
        }
        catch (Exception ex)
        {
            _lastError = $"Error applying modification: {ex.Message}";
            Console.WriteLine($"humble-arteest: {_lastError}");
            return false;
        }
    }

    /// <summary>
    /// Restores the original shader from backup.
    /// Returns true on success.
    /// </summary>
    public static bool RestoreOriginal()
    {
        _lastError = null;

        if (!ResolvePaths()) return false;

        try
        {
            if (_backupPath != null && File.Exists(_backupPath))
            {
                File.Copy(_backupPath, _shaderPath!, overwrite: true);
                File.Delete(_backupPath);
                Console.WriteLine("humble-arteest: Original shader restored from backup.");
                Console.WriteLine("humble-arteest: RESTART THE GAME to see the effect.");
                return true;
            }

            // No backup — try to undo by string replacement
            if (!File.Exists(_shaderPath))
            {
                _lastError = "Shader file not found.";
                return false;
            }

            var content = File.ReadAllText(_shaderPath!);
            if (content.Contains(ModifiedHighlight))
            {
                var restored = content.Replace(ModifiedHighlight, OriginalHighlight);
                File.WriteAllText(_shaderPath!, restored);
                Console.WriteLine("humble-arteest: Shader restored by reverting the modification.");
                Console.WriteLine("humble-arteest: RESTART THE GAME to see the effect.");
                return true;
            }

            _lastError = "Shader does not appear to be modified.";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            _lastError = "Permission denied. The game may need to run as administrator.";
            Console.WriteLine($"humble-arteest: {_lastError}");
            return false;
        }
        catch (Exception ex)
        {
            _lastError = $"Error restoring shader: {ex.Message}";
            Console.WriteLine($"humble-arteest: {_lastError}");
            return false;
        }
    }

    /// <summary>Returns a human-readable description of the current state.</summary>
    public static string GetStateDescription()
    {
        var state = GetState();
        return state switch
        {
            ShaderState.Original => "Shader is ORIGINAL (unmodified). Ready to apply test.",
            ShaderState.Modified => "Shader is MODIFIED (green highlight). Restart game to verify. Hover a part in the editor — if highlight is GREEN, shaders load from disk!",
            ShaderState.BackupExists => "Shader is ORIGINAL but a backup exists from a previous test.",
            ShaderState.FileNotFound => $"Shader file not found. Path: {_shaderPath ?? "unknown"}",
            ShaderState.Unknown => "Shader file exists but content is unexpected.",
            ShaderState.Error => $"Error: {_lastError}",
            _ => "Unknown state."
        };
    }

    /// <summary>Returns the resolved shader file path (or null if not yet resolved).</summary>
    public static string? GetShaderPath() { ResolvePaths(); return _shaderPath; }
}
