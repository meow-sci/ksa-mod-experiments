using System;
using System.Collections.Generic;
using System.Linq;
using KSA;

namespace MeowSci.BlinkenLib;

/// <summary>
/// Manages the LCD scrolling animation: maintains scroll state and applies pixel on/off to engine controllers.
/// </summary>
public class LcdAnimation
{
    private float _scrollOffset;
    private int _lastScrollCol = -1;
    private int _totalScroll;
    private int _imageWidth;
    private int _imageHeight;
    private HashSet<(int x, int y)> _pixelSet = new();
    private PixelGrid? _grid;

    public float ScrollSpeed { get; set; } = 3f;
    public int GridRows { get; private set; }
    public int GridCols { get; private set; }
    public int ImageWidth => _imageWidth;
    public int ImageHeight => _imageHeight;
    public float ScrollOffset => _scrollOffset;

    /// <summary>Initialises (or re-initialises) the animation for the given pixel grid.</summary>
    public void Init(PixelGrid grid)
    {
        _grid = grid;
        GridRows = grid.Rows;
        GridCols = grid.Cols;

        var pixels = LcdAnimationPixels.Pixels;
        if (pixels.Length == 0)
        {
            _imageWidth = 0;
            _imageHeight = 0;
            Console.WriteLine("blinken: LCD animation has no pixel data");
            return;
        }

        _imageWidth  = pixels.Max(p => p.x) + 1;
        _imageHeight = pixels.Max(p => p.y) + 1;
        _pixelSet = new HashSet<(int x, int y)>(pixels);

        // Total scroll distance: image width + half-grid-width gap before repeat
        _totalScroll = _imageWidth + (GridCols / 2);
        _scrollOffset = 0f;
        _lastScrollCol = -1; // force first frame to apply

        Console.WriteLine($"blinken: LCD init — grid {GridCols}x{GridRows}, image {_imageWidth}x{_imageHeight}, totalScroll {_totalScroll}");
    }

    /// <summary>Advances the animation by dt seconds and updates engine active states when the integer column changes.</summary>
    public void Update(double dt)
    {
        if (_grid == null) return;

        _scrollOffset += ScrollSpeed * (float)dt;

        // Wrap around when we've scrolled the full cycle
        if (_scrollOffset >= _totalScroll)
            _scrollOffset -= _totalScroll;

        int scrollCol = (int)_scrollOffset;

        // Skip update if we haven't moved to a new integer column
        if (scrollCol == _lastScrollCol) return;
        _lastScrollCol = scrollCol;

        foreach (var (key, engines) in _grid.Engines)
        {
            int srcX = scrollCol + key.col;
            int srcY = key.row;

            bool on = srcX >= 0 && srcX < _imageWidth
                   && srcY >= 0 && srcY < _imageHeight
                   && _pixelSet.Contains((srcX, srcY));

            for (int i = 0; i < engines.Length; i++)
                engines[i].SetIsActive(null, on);
        }
    }
}
