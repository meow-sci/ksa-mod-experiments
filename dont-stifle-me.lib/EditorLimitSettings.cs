namespace MeowSci.DontStifleMeLib;

/// <summary>
/// Runtime switches for editor value limits that are unrelated to part scaling.
/// </summary>
public static class EditorLimitSettings
{
    /// <summary>
    /// Expands selected vehicle-editor controls beyond their authored limits.
    /// Disabled by default because these ranges can produce intentionally extreme results.
    /// </summary>
    public static bool JplSaidNoClamps;
}
