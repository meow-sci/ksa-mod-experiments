using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;

namespace MeowSci.GraffitiLib;

/// <summary>
/// One-shot click placement + the file browser window. Runs from <see cref="RenderFloatingWindows"/>
/// so it works every frame regardless of whether the submod's section (or host window) is open —
/// the click lands in the 3D world, not on the panel.
/// </summary>
public sealed partial class GraffitiSubmod
{
    private readonly FileBrowser _fileBrowser = new();

    private bool _armed;
    private string _armedDecalName = "";
    private string? _placeStatus;
    private bool _placeStatusIsError;

    /// <summary>Arms one-shot placement: the next world click places <paramref name="decalName"/>.</summary>
    public void Arm(string decalName)
    {
        _armed = true;
        _armedDecalName = decalName;
        _placeStatus = null;
    }

    /// <summary>Disarms placement mode with a status message for the panel.</summary>
    public void Disarm(string? status = null)
    {
        _armed = false;
        _placeStatus = status;
        _placeStatusIsError = false;
    }

    public void RenderFloatingWindows()
    {
        _fileBrowser.Render(OnImportPicked);

        if (!_armed)
            return;

        if (Program.EditorFlag)
        {
            Disarm("Placement cancelled — decals are a flight-scene feature.");
            return;
        }
        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Disarm("Placement cancelled.");
            return;
        }

        DrawCursorHint();

        // A click on any ImGui window (including our own panel) must not place a decal.
        if (!ImGui.GetIO().WantCaptureMouse && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var (decal, error) = PlaceAtCursor(_armedDecalName, _range,
                _width, _height, _rollDeg, _alpha, _brightness,
                _depth > 0f ? _depth : null);
            if (decal != null)
            {
                // One-shot: back to normal after a successful placement.
                _armed = false;
                _placeStatus = $"Placed #{decal.Id} on {DescribeTarget(decal)}.";
                _placeStatusIsError = false;
            }
            else
            {
                // A miss keeps placement armed so a slightly-off click isn't a whole round trip.
                _placeStatus = $"{error}  Click again or press Esc.";
                _placeStatusIsError = true;
            }
        }
    }

    /// <summary>A follow-the-cursor hint so armed mode is unmistakable (shadowed for readability).</summary>
    private void DrawCursorHint()
    {
        var dl = ImGui.GetForegroundDrawList();
        var pos = ImGui.GetMousePos() + new float2(18f, 18f);
        ImString hint = $"place '{_armedDecalName}' — click a vehicle, parachute, or terrain (Esc cancels)";
        dl.AddText(pos + new float2(1f, 1f), ImColor8.Black, hint);
        dl.AddText(pos, ImColor8.White, hint);
    }

    /// <summary>File-browser pick: copy into the library, rescan, and select the import.</summary>
    private void OnImportPicked(string fullPath)
    {
        var name = DecalLibrary.Import(fullPath, out var error);
        if (name == null)
        {
            _placeStatus = error;
            _placeStatusIsError = true;
            return;
        }

        RefreshLibrary();
        _selectedLibraryIndex = Array.IndexOf(_libraryNames, name);
        Draft.Select("Decal", name);
        _placeStatus = $"Imported '{name}'.";
        _placeStatusIsError = false;
    }
}
