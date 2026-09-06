using System;
using System.IO;

namespace MeowSci.KsaAbstractions;

public static class KsaPaths
{
    /// <summary>The base KSA user data directory (e.g. My Documents\My Games\Kitten Space Agency)</summary>
    public static string UserDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "My Games",
        "Kitten Space Agency");

    /// <summary>The shared custom-data directory used by the Unscience mod suite.</summary>
    public static string ModDataDir { get; } = Path.Combine(UserDataDir, ".unscience");
}
