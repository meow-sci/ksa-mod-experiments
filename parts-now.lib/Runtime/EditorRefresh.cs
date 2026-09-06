// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do not introduce background access to KSA state; parts-now must remain safe standalone.
//
// EditorRefresh.AfterLoad() touches VehicleEditor state and is game-thread only.

using System;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// The one nudge the vehicle editor needs after parts-now registers new Parts.
/// </summary>
/// <remarks>
/// <para>
/// Nothing else is required. <c>VehicleEditor.PartWindow.OnDrawUi</c> iterates
/// <c>ModLibrary.AllParts.GetList()</c> fresh every frame, so newly registered Parts appear
/// immediately under <b>All</b> and under their category — which, per validation rule V7, must
/// already exist, because <c>VehicleEditor.RegisterTag</c> stops appending to <c>_editorTags</c>
/// once <c>MarkEditorTagDefinitionsLoaded()</c> has run at boot.
/// </para>
/// <para>
/// The single exception is <c>PartWindow._diameterCache</c>, which is built lazily and then reused,
/// so the diameter filter would not include new Parts until the window is toggled.
/// <c>VehicleEditor.ResetPartDiameterCache()</c> is public static and clears exactly that.
/// </para>
/// </remarks>
public static class EditorRefresh
{
    /// <summary>
    /// Rebuilds the vehicle editor's part-diameter cache so the diameter filter includes Parts
    /// registered after boot. Safe to call whether or not the editor is open; never throws.
    /// </summary>
    public static void AfterLoad()
    {
        try
        {
            VehicleEditor.ResetPartDiameterCache();
            Console.WriteLine("parts-now: vehicle editor part diameter cache reset.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: failed to reset the vehicle editor part diameter cache: {ex.Message}");
        }
    }
}
