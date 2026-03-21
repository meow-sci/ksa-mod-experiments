using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;

namespace MeowSci.GeeForceLib;

public static class GForceUI
{
    // History (buffer) duration options in seconds
    private static readonly float[] HistoryOptions = { 30f, 60f, 120f, 300f, 600f, 1800f, 3600f };
    private static readonly string[] HistoryLabels = { "30s", "1m", "2m", "5m", "10m", "30m", "1h" };
    private static int _selectedHistoryIdx = 3; // default 5m

    // Viewport span (visible window width) in seconds
    private const float ViewportSpanSec = 300f; // 5 minutes

    // Scrub slider: 0 = oldest data, 1 = live (rightmost)
    private static float _scrubNorm = 1.0f;
    private static bool _isLive = true;

    // Graph colors
    private static readonly float4 ColorGreen   = new float4(0.0f, 1.0f, 0.0f, 1.0f);
    private static readonly float4 ColorYellow  = new float4(1.0f, 1.0f, 0.0f, 1.0f);
    private static readonly float4 ColorRed     = new float4(1.0f, 0.2f, 0.2f, 1.0f);
    private static readonly float4 ColorWhite   = new float4(1.0f, 1.0f, 1.0f, 1.0f);
    private static readonly float4 ColorGrey    = new float4(0.5f, 0.5f, 0.5f, 1.0f);
    private static readonly float4 ColorDimGrey = new float4(0.3f, 0.3f, 0.3f, 1.0f);
    private static readonly float4 ColorBg      = new float4(0.1f, 0.1f, 0.12f, 1.0f);

    // Axis line colors
    private static readonly float4 ColorAxisX = new float4(1.0f, 0.4f, 0.4f, 0.8f);
    private static readonly float4 ColorAxisY = new float4(0.4f, 1.0f, 0.4f, 0.8f);
    private static readonly float4 ColorAxisZ = new float4(0.4f, 0.6f, 1.0f, 0.8f);

    // Jerk color
    private static readonly float4 ColorJerk = new float4(1.0f, 0.6f, 0.0f, 0.7f);

    // Peak marker color
    private static readonly float4 ColorPeakMarker = new float4(1.0f, 0.0f, 0.5f, 1.0f);

    private static bool _showAxes = false;
    private static bool _showJerk = false;
    private static float _killGeesThreshold = 9.0f;

    public static float GetSelectedHistorySeconds() => HistoryOptions[_selectedHistoryIdx];

    public static int GetRequiredCapacity(double sampleIntervalSec)
    {
        return (int)(HistoryOptions[_selectedHistoryIdx] / sampleIntervalSec);
    }

    public static void Render(ref bool visible, GForceRecorder recorder, double sampleIntervalSec)
    {
        ImGui.SetNextWindowSize(new float2(520, 440), ImGuiCond.FirstUseEver);

        // Feature #7: show history duration in title
        string histLabel = HistoryLabels[_selectedHistoryIdx];
        string liveTag = _isLive ? " - LIVE" : "";
        string title = $"G-Force Monitor ({histLabel}{liveTag})###geeforce";

        if (!ImGui.Begin(title, ref visible, ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.End();
            return;
        }

        // --- Stats row ---
        float4 currentColor = GetGForceColor(recorder.Latest.Magnitude);
        ImGui.TextColored(currentColor, $"Current: {recorder.Latest.Magnitude:F2} g");
        ImGui.SameLine(0, 20);
        ImGui.TextColored(ColorRed, $"Peak: {recorder.PeakG:F2} g");
        ImGui.SameLine(0, 20);
        ImGui.Text($"Avg: {recorder.AvgG:F2} g");
        ImGui.SameLine(0, 20);
        ImGui.TextColored(ColorJerk, $"Max Jerk: {recorder.MaxJerk:F1} g/s");
        ImGui.SameLine(0, 20);
        ImGui.TextColored(ColorRed, $"Breaches: {recorder.KillGeesBreaches}");
        ImGui.SameLine(0, 20);
        ImGui.TextColored(ColorJerk, $"Jerk Breaches: {recorder.JerkBreaches}");

        // --- Per-axis readout ---
        ImGui.TextColored(ColorAxisX, $"X: {recorder.Latest.Longitudinal:F2}g");
        ImGui.SameLine(0, 10);
        ImGui.TextColored(ColorAxisY, $"Y: {recorder.Latest.Lateral:F2}g");
        ImGui.SameLine(0, 10);
        ImGui.TextColored(ColorAxisZ, $"Z: {recorder.Latest.Normal:F2}g");
        if (_showJerk)
        {
            ImGui.SameLine(0, 20);
            ImGui.TextColored(ColorJerk, $"Jerk: {recorder.Latest.Jerk:F1} g/s");
        }

        ImGui.Separator();

        // --- Graph ---
        DrawGraph(recorder, sampleIntervalSec);
        recorder.CheckKillGeesBreaches((double)_killGeesThreshold);
        recorder.CheckJerkBreaches((double)_killGeesThreshold);

        // --- Scrub slider ---
        DrawScrubSlider(recorder);

        ImGui.Separator();

        // --- Controls row ---
        DrawControls(recorder, sampleIntervalSec);

        ImGui.End();
    }

    private static void DrawScrubSlider(GForceRecorder recorder)
    {
        int count = recorder.Count;
        if (count < 2)
        {
            _isLive = true;
            _scrubNorm = 1.0f;
            return;
        }

        double oldestTime = recorder[0].TimeSec;
        double newestTime = recorder[count - 1].TimeSec;
        double totalSpan = newestTime - oldestTime;

        // If total data is less than viewport, no need for scrubbing
        if (totalSpan <= ViewportSpanSec)
        {
            _isLive = true;
            _scrubNorm = 1.0f;
            return;
        }

        // Slider maps [0,1] to [oldestTime, newestTime - ViewportSpan] for viewport start
        float prevScrub = _scrubNorm;
        ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X - 60);
        ImGui.SliderFloat("###scrub", ref _scrubNorm, 0.0f, 1.0f, "");
        ImGui.PopItemWidth();

        // If user moved the slider, check if they went to the end
        if (Math.Abs(prevScrub - _scrubNorm) > 0.0001f)
        {
            _isLive = _scrubNorm >= 0.99f;
        }

        // Live button
        ImGui.SameLine(0, 4);
        if (_isLive)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ColorGreen));
        }
        if (ImGui.Button("Live"))
        {
            _isLive = true;
            _scrubNorm = 1.0f;
        }
        if (_isLive)
        {
            ImGui.PopStyleColor(1);
        }

        // Auto-follow when live
        if (_isLive)
        {
            _scrubNorm = 1.0f;
        }
    }

    /// <summary>
    /// Computes the visible time window based on scrub position.
    /// Returns (viewStart, viewEnd) in sim time seconds.
    /// </summary>
    private static (double viewStart, double viewEnd) GetViewWindow(GForceRecorder recorder)
    {
        int count = recorder.Count;
        if (count < 2)
            return (0, ViewportSpanSec);

        double oldestTime = recorder[0].TimeSec;
        double newestTime = recorder[count - 1].TimeSec;
        double totalSpan = newestTime - oldestTime;

        if (totalSpan <= ViewportSpanSec)
            return (oldestTime, oldestTime + ViewportSpanSec);

        double maxStart = newestTime - ViewportSpanSec;
        double viewStart = oldestTime + _scrubNorm * (maxStart - oldestTime);
        double viewEnd = viewStart + ViewportSpanSec;
        return (viewStart, viewEnd);
    }

    private static void DrawGraph(GForceRecorder recorder, double sampleIntervalSec)
    {
        float availWidth = ImGui.GetContentRegionAvail().X;
        float graphHeight = 300f;
        float2 plotSize = new float2(availWidth, graphHeight);
        float2 plotMin = ImGui.GetCursorScreenPos();
        float2 plotMax = plotMin + plotSize;

        ImDrawListPtr drawList = ImGui.GetWindowDrawList();

        // Background
        ImColor8 bgColor = ImGui.GetColorU32(ColorBg);
        drawList.AddRectFilled(in plotMin, in plotMax, bgColor);

        // Reserve space in layout
        ImGui.Dummy(plotSize);

        // Clip to plot area
        drawList.PushClipRect(in plotMin, in plotMax, true);

        int count = recorder.Count;
        if (count < 2)
        {
            ImColor8 grey = ImGui.GetColorU32(ColorGrey);
            float2 textPos = new float2(plotMin.X + 10, plotMin.Y + graphHeight * 0.5f - 7);
            drawList.AddText(in textPos, grey, "Waiting for data...");
            drawList.PopClipRect();
            return;
        }

        var (viewStart, viewEnd) = GetViewWindow(recorder);
        double viewSpan = viewEnd - viewStart;

        // Find visible data range with binary search
        int firstVisible = recorder.FindIndexAtOrAfter(viewStart);
        // Include one sample before viewport for line continuity
        if (firstVisible > 0) firstVisible--;

        // Y-axis scale: dynamic based on peak within visible range
        double visiblePeakG = 0.0;
        double visiblePeakJerk = 0.0;
        int visiblePeakIdx = -1;
        for (int i = firstVisible; i < count; i++)
        {
            GForceSample s = recorder[i];
            if (s.TimeSec > viewEnd) break;
            if (s.Magnitude > visiblePeakG)
            {
                visiblePeakG = s.Magnitude;
                visiblePeakIdx = i;
            }
            double absJerk = Math.Abs(s.Jerk);
            if (absJerk > visiblePeakJerk) visiblePeakJerk = absJerk;
        }
        // Use global peak if larger (keeps scale stable when scrolling back)
        double yMax = Math.Max(visiblePeakG * 1.15, 1.0);
        yMax = CeilToNice(yMax);

        double jerkMax = Math.Max(visiblePeakJerk * 1.15, 1.0);
        jerkMax = CeilToNice(jerkMax);

        float padLeft = 40f;
        float padRight = _showJerk ? 120f : 4f; // extra right pad for jerk labels
        float padBottom = 30f; // space for X-axis labels
        float plotInnerWidth = plotSize.X - padLeft - padRight;
        float plotInnerHeight = plotSize.Y - 4f - padBottom;

        float2 innerMin = new float2(plotMin.X + padLeft, plotMin.Y + 2f);

        // --- Grid lines (horizontal, Y-axis) ---
        ImColor8 gridColor = ImGui.GetColorU32(ColorDimGrey);
        ImColor8 labelColor = ImGui.GetColorU32(ColorGrey);

        int gridLines = CalculateGridLines(yMax);
        for (int i = 0; i <= gridLines; i++)
        {
            double gValue = yMax * i / gridLines;
            float yPx = innerMin.Y + plotInnerHeight - (float)(gValue / yMax) * plotInnerHeight;

            float2 lineStart = new float2(innerMin.X, yPx);
            float2 lineEnd = new float2(innerMin.X + plotInnerWidth, yPx);
            drawList.AddLine(in lineStart, in lineEnd, gridColor);

            string label = gValue >= 10 ? $"{gValue:F0}" : $"{gValue:F1}";
            float2 labelPos = new float2(plotMin.X + 2, yPx - 6);
            drawList.AddText(in labelPos, labelColor, label);
        }

        // --- Jerk Y-axis labels (right side) ---
        if (_showJerk)
        {
            ImColor8 jerkLabelColor = ImGui.GetColorU32(ColorJerk);
            int jerkGridLines = CalculateGridLines(jerkMax);
            for (int i = 0; i <= jerkGridLines; i++)
            {
                double jVal = jerkMax * i / jerkGridLines;
                float yPx = innerMin.Y + plotInnerHeight - (float)(jVal / jerkMax) * plotInnerHeight;
                string jLabel = jVal >= 10 ? $"{jVal:F0}" : $"{jVal:F1}";
                float2 jLabelPos = new float2(innerMin.X + plotInnerWidth + 4, yPx - 6);
                drawList.AddText(in jLabelPos, jerkLabelColor, jLabel);
            }
        }

        // --- Feature #1: X-axis time labels ---
        DrawXAxisLabels(drawList, innerMin, plotInnerWidth, plotInnerHeight, viewStart, viewEnd, viewSpan, labelColor, gridColor);

        // --- Plot lines ---
        ImColor8 lineColorMag = ImGui.GetColorU32(ColorGreen);
        ImColor8 lineColorX = ImGui.GetColorU32(ColorAxisX);
        ImColor8 lineColorY = ImGui.GetColorU32(ColorAxisY);
        ImColor8 lineColorZ = ImGui.GetColorU32(ColorAxisZ);
        ImColor8 lineColorJerk = ImGui.GetColorU32(ColorJerk);

        float2 prevMag = default;
        float2 prevX = default;
        float2 prevY = default;
        float2 prevZ = default;
        float2 prevJerk = default;
        bool hasPrev = false;

        for (int i = firstVisible; i < count; i++)
        {
            GForceSample s = recorder[i];
            if (s.TimeSec > viewEnd) break;

            float xNorm = (float)((s.TimeSec - viewStart) / viewSpan);
            float xPx = innerMin.X + xNorm * plotInnerWidth;

            // Magnitude line
            float yMagPx = innerMin.Y + plotInnerHeight - (float)(s.Magnitude / yMax) * plotInnerHeight;
            float2 ptMag = new float2(xPx, yMagPx);

            if (hasPrev)
                drawList.AddLine(in prevMag, in ptMag, lineColorMag);
            prevMag = ptMag;

            // Per-axis lines
            if (_showAxes)
            {
                float yXPx = innerMin.Y + plotInnerHeight - (float)(Math.Abs(s.Longitudinal) / yMax) * plotInnerHeight;
                float yYPx = innerMin.Y + plotInnerHeight - (float)(Math.Abs(s.Lateral) / yMax) * plotInnerHeight;
                float yZPx = innerMin.Y + plotInnerHeight - (float)(Math.Abs(s.Normal) / yMax) * plotInnerHeight;
                float2 ptX = new float2(xPx, yXPx);
                float2 ptY = new float2(xPx, yYPx);
                float2 ptZ = new float2(xPx, yZPx);

                if (hasPrev)
                {
                    drawList.AddLine(in prevX, in ptX, lineColorX);
                    drawList.AddLine(in prevY, in ptY, lineColorY);
                    drawList.AddLine(in prevZ, in ptZ, lineColorZ);
                }
                prevX = ptX;
                prevY = ptY;
                prevZ = ptZ;
            }

            // Feature #6: Jerk line (secondary Y axis)
            if (_showJerk)
            {
                float yJerkPx = innerMin.Y + plotInnerHeight - (float)(Math.Abs(s.Jerk) / jerkMax) * plotInnerHeight;
                float2 ptJerk = new float2(xPx, yJerkPx);

                if (hasPrev)
                    drawList.AddLine(in prevJerk, in ptJerk, lineColorJerk);
                prevJerk = ptJerk;
            }

            hasPrev = true;
        }

        // --- Threshold lines ---
        if (yMax > 3.0)
            DrawThresholdLine(drawList, innerMin, plotInnerWidth, plotInnerHeight, yMax, 3.0, ColorYellow, "3g");
        if (yMax > 6.0)
            DrawThresholdLine(drawList, innerMin, plotInnerWidth, plotInnerHeight, yMax, 6.0, ColorRed, "6g");

        // Kill-gees user-defined threshold line
        if (yMax > (double)_killGeesThreshold)
            DrawThresholdLine(drawList, innerMin, plotInnerWidth, plotInnerHeight, yMax, (double)_killGeesThreshold, ColorRed, $"{_killGeesThreshold:F0}g");

        // --- Feature #3: Peak marker within visible viewport ---
        if (visiblePeakIdx >= 0)
        {
            GForceSample peakSample = recorder[visiblePeakIdx];
            float peakXNorm = (float)((peakSample.TimeSec - viewStart) / viewSpan);
            float peakXPx = innerMin.X + peakXNorm * plotInnerWidth;
            float peakYPx = innerMin.Y + plotInnerHeight - (float)(peakSample.Magnitude / yMax) * plotInnerHeight;
            float2 peakPt = new float2(peakXPx, peakYPx);

            // Vertical dashed line from peak to bottom
            ImColor8 peakLineColor = ImGui.GetColorU32(new float4(ColorPeakMarker.X, ColorPeakMarker.Y, ColorPeakMarker.Z, 0.3f));
            float2 dashBottom = new float2(peakXPx, innerMin.Y + plotInnerHeight);
            drawList.AddLine(in peakPt, in dashBottom, peakLineColor);

            // Diamond marker at peak
            ImColor8 peakColor = ImGui.GetColorU32(ColorPeakMarker);
            float d = 4f;
            float2 top    = new float2(peakXPx, peakYPx - d);
            float2 right  = new float2(peakXPx + d, peakYPx);
            float2 bottom = new float2(peakXPx, peakYPx + d);
            float2 left   = new float2(peakXPx - d, peakYPx);
            drawList.AddLine(in top, in right, peakColor);
            drawList.AddLine(in right, in bottom, peakColor);
            drawList.AddLine(in bottom, in left, peakColor);
            drawList.AddLine(in left, in top, peakColor);

            // Peak label
            string peakLabel = $"{peakSample.Magnitude:F1}g";
            float2 peakLabelPos = new float2(peakXPx + 6, peakYPx - 12);
            drawList.AddText(in peakLabelPos, peakColor, peakLabel);
        }

        // --- Current value dot (only when live and latest is visible) ---
        if (_isLive && count > 0)
        {
            float curYPx = innerMin.Y + plotInnerHeight - (float)(recorder.Latest.Magnitude / yMax) * plotInnerHeight;
            float2 curPt = new float2(innerMin.X + plotInnerWidth, curYPx);
            ImColor8 dotColor = ImGui.GetColorU32(GetGForceColor(recorder.Latest.Magnitude));
            drawList.AddCircleFilled(in curPt, 3f, dotColor);
        }

        drawList.PopClipRect();
    }

    private static void DrawXAxisLabels(ImDrawListPtr drawList, float2 innerMin, float plotInnerWidth, float plotInnerHeight, double viewStart, double viewEnd, double viewSpan, ImColor8 labelColor, ImColor8 gridColor)
    {
        // Choose a nice time interval for labels
        double labelIntervalSec = ChooseTimeInterval(viewSpan);
        // Align first label to a multiple of the interval
        double firstLabel = Math.Ceiling(viewStart / labelIntervalSec) * labelIntervalSec;

        float yBase = innerMin.Y + plotInnerHeight;
        ImColor8 tickColor = ImGui.GetColorU32(new float4(0.4f, 0.4f, 0.4f, 0.6f));

        for (double t = firstLabel; t <= viewEnd; t += labelIntervalSec)
        {
            float xNorm = (float)((t - viewStart) / viewSpan);
            float xPx = innerMin.X + xNorm * plotInnerWidth;

            // Vertical grid tick
            float2 tickTop = new float2(xPx, innerMin.Y);
            float2 tickBot = new float2(xPx, yBase);
            drawList.AddLine(in tickTop, in tickBot, tickColor);

            // Time label below graph
            // Show as relative offset from viewStart
            double relSec = t - viewStart;
            string label = FormatTimeLabel(relSec);
            float2 labelPos = new float2(xPx - 10, yBase + 2);
            drawList.AddText(in labelPos, labelColor, label);
        }
    }

    private static double ChooseTimeInterval(double viewSpan)
    {
        // Target roughly 5-8 labels across the viewport
        double targetInterval = viewSpan / 6.0;
        double[] niceIntervals = { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600 };
        foreach (double ni in niceIntervals)
        {
            if (ni >= targetInterval) return ni;
        }
        return 600;
    }

    private static string FormatTimeLabel(double seconds)
    {
        if (seconds < 60)
            return $"{seconds:F0}s";
        int mins = (int)(seconds / 60);
        int secs = (int)(seconds % 60);
        return secs == 0 ? $"{mins}m" : $"{mins}:{secs:D2}";
    }

    private static void DrawControls(GForceRecorder recorder, double sampleIntervalSec)
    {
        // History duration selector
        ImGui.Text("Buffer:");
        for (int i = 0; i < HistoryOptions.Length; i++)
        {
            ImGui.SameLine(0, 3);
            bool isSelected = _selectedHistoryIdx == i;
            if (isSelected)
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ColorGreen));

            if (ImGui.Button(HistoryLabels[i] + "###hist" + i))
            {
                _selectedHistoryIdx = i;
                int newCapacity = GetRequiredCapacity(sampleIntervalSec);
                recorder.Resize(newCapacity);
            }
            if (isSelected)
                ImGui.PopStyleColor(1);
        }

        // Second row: recording controls + toggles
        if (recorder.IsRecording)
        {
            if (ImGui.Button("Pause"))
                recorder.IsRecording = false;
        }
        else
        {
            if (ImGui.Button("Record"))
                recorder.IsRecording = true;
        }

        ImGui.SameLine(0, 4);
        if (ImGui.Button("Clear"))
            recorder.Clear();

        ImGui.SameLine(0, 12);
        ImGui.Checkbox("Axes", ref _showAxes);

        ImGui.SameLine(0, 8);
        ImGui.Checkbox("Jerk", ref _showJerk);

        ImGui.Separator();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 150);
        ImGui.SliderFloat("kill gees", ref _killGeesThreshold, 1.0f, 250.0f);
    }

    private static void DrawThresholdLine(ImDrawListPtr drawList, float2 innerMin, float plotInnerWidth, float plotInnerHeight, double yMax, double threshold, float4 color, string label)
    {
        float yPx = innerMin.Y + plotInnerHeight - (float)(threshold / yMax) * plotInnerHeight;
        ImColor8 col = ImGui.GetColorU32(new float4(color.X, color.Y, color.Z, 0.4f));
        float2 p1 = new float2(innerMin.X, yPx);
        float2 p2 = new float2(innerMin.X + plotInnerWidth, yPx);
        drawList.AddLine(in p1, in p2, col);

        ImColor8 textCol = ImGui.GetColorU32(new float4(color.X, color.Y, color.Z, 0.6f));
        float2 labelPos = new float2(innerMin.X + plotInnerWidth - 24, yPx - 12);
        drawList.AddText(in labelPos, textCol, label);
    }

    private static float4 GetGForceColor(double g)
    {
        if (g < 3.0) return ColorGreen;
        if (g < 6.0) return ColorYellow;
        return ColorRed;
    }

    private static double CeilToNice(double value)
    {
        if (value <= 1.0) return 1.0;
        if (value <= 2.0) return 2.0;
        if (value <= 3.0) return 3.0;
        if (value <= 5.0) return 5.0;
        if (value <= 10.0) return 10.0;
        if (value <= 20.0) return 20.0;
        if (value <= 50.0) return 50.0;
        if (value <= 100.0) return 100.0;
        return Math.Ceiling(value / 50.0) * 50.0;
    }

    private static int CalculateGridLines(double yMax)
    {
        if (yMax <= 2.0) return 4;
        if (yMax <= 5.0) return 5;
        if (yMax <= 10.0) return 5;
        if (yMax <= 20.0) return 4;
        if (yMax <= 50.0) return 5;
        if (yMax <= 100.0) return 5;
        return 5;
    }
}
