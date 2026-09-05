using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;

namespace MeowSci.GraffitiLib;

/// <summary>
/// Click / hold-to-spray placement + the file browser window. Runs from <see cref="RenderFloatingWindows"/>
/// so it works every frame regardless of whether the submod's section (or host window) is open —
/// the click lands in the 3D world, not on the panel.
/// </summary>
public sealed partial class GraffitiSubmod
{
    private readonly FileBrowser _fileBrowser = new();

    private bool _sprayMode;
    private int _sprayIntervalMs = 150;
    private readonly SprayCadence _sprayCadence = new();
    private bool _armedSpray;
    private int _armedInterval;
    private float _armedRange, _armedWidth, _armedHeight, _armedRoll, _armedAlpha, _armedBrightness, _armedDepth;
    private bool _armed;
    private string _armedDecalName = "";
    private string? _placeStatus;
    private bool _placeStatusIsError;

    /// <summary>Snapshots and arms the selected click/spray mode for <paramref name="decalName"/>.</summary>
    public void Arm(string decalName)
    {
        _armed = true;
        _armedSpray = _sprayMode;
        _armedInterval = Math.Clamp(_sprayIntervalMs, 10, 60_000);
        _armedRange = _range; _armedWidth = _width; _armedHeight = _height;
        _armedRoll = _rollDeg; _armedAlpha = _alpha; _armedBrightness = _brightness; _armedDepth = _depth;
        _sprayCadence.Reset();
        _armedDecalName = decalName;
        _placeStatus = null;
    }

    /// <summary>Disarms placement mode with a status message for the panel.</summary>
    public void Disarm(string? status = null)
    {
        _armed = false;
        _sprayCadence.Reset();
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
        bool captured = ImGui.GetIO().WantCaptureMouse;
        bool pressed = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        bool stamp = _armedSpray
            ? _sprayCadence.Tick(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency,
                pressed, ImGui.IsMouseDown(ImGuiMouseButton.Left), captured, _armedInterval)
            : !captured && pressed;
        if (stamp)
        {
            var (decal, error) = PlaceAtCursor(_armedDecalName, _armedRange,
                _armedWidth, _armedHeight, _armedRoll, _armedAlpha, _armedBrightness,
                _armedDepth > 0f ? _armedDepth : null);
            if (decal != null)
            {
                // Click mode completes; spray remains armed for further strokes.
                _armed = _armedSpray;
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
        ImString hint = $"{(_armedSpray ? "hold to spray" : "place")} '{_armedDecalName}' — click a vehicle, parachute, or terrain (Esc cancels)";
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
