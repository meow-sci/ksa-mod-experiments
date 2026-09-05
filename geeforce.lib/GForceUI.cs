using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;

namespace MeowSci.GeeForceLib;

public sealed class GForceUI
{
    // Time window (view width) options in seconds
    private readonly float[] WindowOptions = { 30f, 60f, 120f, 300f, 600f, 1800f, 3600f };
    private readonly string[] WindowLabels = { " 30s ", " 1m ", " 2m ", " 5m ", " 10m ", " 30m ", " 1h " };
    private int _selectedWindowIdx = 0; // default 30s

    // Scroll offset: fractional seconds from oldest recorded data to view start
    private float _scrollOffsetSec = 0f;
    private bool _isLive = true;

    // Graph colors
    private readonly float4 ColorGreen   = new float4(0.0f, 1.0f, 0.0f, 1.0f);
    private readonly float4 ColorYellow  = new float4(1.0f, 1.0f, 0.0f, 1.0f);
    private readonly float4 ColorRed     = new float4(1.0f, 0.2f, 0.2f, 1.0f);
    private readonly float4 ColorWhite   = new float4(1.0f, 1.0f, 1.0f, 1.0f);
    private readonly float4 ColorGrey    = new float4(0.5f, 0.5f, 0.5f, 1.0f);
    private readonly float4 ColorDimGrey = new float4(0.3f, 0.3f, 0.3f, 1.0f);
    private readonly float4 ColorBg      = new float4(0.1f, 0.1f, 0.12f, 1.0f);

    // Axis line colors
    private readonly float4 ColorAxisX = new float4(1.0f, 0.4f, 0.4f, 0.8f);
    private readonly float4 ColorAxisY = new float4(0.4f, 1.0f, 0.4f, 0.8f);
    private readonly float4 ColorAxisZ = new float4(0.4f, 0.6f, 1.0f, 0.8f);

    // Jerk color
    private readonly float4 ColorJerk = new float4(1.0f, 0.6f, 0.0f, 0.7f);

    // Peak marker color
    private readonly float4 ColorPeakMarker = new float4(1.0f, 0.0f, 0.5f, 1.0f);

    private bool _showAxes = false;
    private bool _showJerk = false;
    private float _killGeesThreshold = 9.0f;

    public float Threshold { get => _killGeesThreshold; set => _killGeesThreshold = value; }
    public bool ShowAxes { get => _showAxes; set => _showAxes = value; }
    public bool ShowJerk { get => _showJerk; set => _showJerk = value; }
    public int WindowIndex { get => _selectedWindowIdx; set => _selectedWindowIdx = Math.Clamp(value, 0, WindowOptions.Length - 1); }
    public void RenderContent(GForceRecorder recorder, double sampleIntervalSec)
    {
        // --- 2-column stats table ---
        DrawStatsTable(recorder);

        ImGui.Separator();

        // --- Graph ---
        DrawGraph(recorder);


        // --- Kill gees line (right after graph) ---
        DrawKillGeesLine();

        // --- Scroll offset slider (disabled until data exceeds window) ---
        DrawScrollSlider(recorder);

        ImGui.Separator();

        // --- Controls ---
        DrawControls(recorder);
    }

    private void DrawStatsTable(GForceRecorder recorder)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 3f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##gf_stats", 2, flags))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##val", ImGuiTableColumnFlags.WidthStretch, 2f);

            StatsRow("Current",       $"{recorder.Latest.Magnitude:F2} g",    GetGForceColor(recorder.Latest.Magnitude));
            StatsRow("Peak",          $"{recorder.PeakG:F2} g",               ColorRed);
            StatsRow("Avg",           $"{recorder.AvgG:F2} g",                ColorWhite);
            StatsRow("Max Jerk",      $"{recorder.MaxJerk:F1} g/s",           ColorJerk);
            StatsRow("Breaches",      $"{recorder.KillGeesBreaches}",         ColorRed);
            StatsRow("Jerk Breaches", $"{recorder.JerkBreaches}",             ColorJerk);
            StatsRow("X",             $"{recorder.Latest.Longitudinal:F2} g", ColorAxisX);
            StatsRow("Y",             $"{recorder.Latest.Lateral:F2} g",      ColorAxisY);
            StatsRow("Z",             $"{recorder.Latest.Normal:F2} g",       ColorAxisZ);
            if (_showJerk)
                StatsRow("Jerk",      $"{recorder.Latest.Jerk:F1} g/s",       ColorJerk);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }

    private void StatsRow(string label, string value, float4 valueColor)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextColored(ColorGrey, label);
        ImGui.TableNextColumn();
        ImGui.TextColored(valueColor, value);
    }

    private void DrawKillGeesLine()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##gf_killgees", 2, flags))
        {
            ImGui.TableSetupColumn("##lbl",    ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##slider", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("kill gees line");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.DragFloat("##gf_kgline", ref _killGeesThreshold, 0.1f, 1.0f, 250.0f);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }

    private void DrawScrollSlider(GForceRecorder recorder)
    {
        int count = recorder.Count;
        float windowSec = WindowOptions[_selectedWindowIdx];

        bool sliderNeeded = false;
        float sliderMax = 0f;

        if (count >= 2)
        {
            double oldestTime = recorder[0].TimeSec;
            double newestTime = recorder[count - 1].TimeSec;
            double totalTime = newestTime - oldestTime;
            sliderMax = Math.Max(0f, (float)totalTime - windowSec);
            sliderNeeded = sliderMax > 0f;
        }

        if (!sliderNeeded)
        {
            _isLive = true;
            _scrollOffsetSec = 0f;
        }
        else if (_isLive && recorder.IsRecording)
        {
            // Auto-follow latest data when live
            _scrollOffsetSec = sliderMax;
        }

        // Clamp in case window size changed
        _scrollOffsetSec = Math.Max(0f, Math.Min(_scrollOffsetSec, Math.Max(0f, sliderMax)));

        float prevVal = _scrollOffsetSec;
        if (!sliderNeeded) ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(-1);
        ImGui.SliderFloat("##gf_scroll", ref _scrollOffsetSec, 0f, Math.Max(1f, sliderMax), "");
        if (!sliderNeeded) ImGui.EndDisabled();

        if (sliderNeeded && Math.Abs(prevVal - _scrollOffsetSec) > 0.001f)
        {
            _isLive = _scrollOffsetSec >= sliderMax - 0.05f;
        }
    }

    private (double viewStart, double viewEnd) GetViewWindow(GForceRecorder recorder)
    {
        float windowSec = WindowOptions[_selectedWindowIdx];
        int count = recorder.Count;

        if (count < 2)
            return (0.0, windowSec);

        double oldestTime = recorder[0].TimeSec;
        double viewStart = oldestTime + (double)_scrollOffsetSec;
        double viewEnd = viewStart + windowSec;
        return (viewStart, viewEnd);
    }

    private void DrawGraph(GForceRecorder recorder)
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
        double yMax = Math.Max(visiblePeakG * 1.15, 1.0);
        yMax = CeilToNice(yMax);

        double jerkMax = Math.Max(visiblePeakJerk * 1.15, 1.0);
        jerkMax = CeilToNice(jerkMax);

        float padLeft = 40f;
        float padRight = _showJerk ? 120f : 4f;
        float padBottom = 30f;
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
            float2 lineEnd   = new float2(innerMin.X + plotInnerWidth, yPx);
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

        // --- X-axis time labels ---
        double recordingStart = recorder[0].TimeSec;
        DrawXAxisLabels(drawList, innerMin, plotInnerWidth, plotInnerHeight, viewStart, viewEnd, viewSpan, recordingStart, labelColor, gridColor);

        // --- Plot lines ---
        ImColor8 lineColorMag  = ImGui.GetColorU32(ColorGreen);
        ImColor8 lineColorX    = ImGui.GetColorU32(ColorAxisX);
        ImColor8 lineColorY    = ImGui.GetColorU32(ColorAxisY);
        ImColor8 lineColorZ    = ImGui.GetColorU32(ColorAxisZ);
        ImColor8 lineColorJerk = ImGui.GetColorU32(ColorJerk);

        float2 prevMag  = default;
        float2 prevX    = default;
        float2 prevY    = default;
        float2 prevZ    = default;
        float2 prevJerk = default;
        bool hasPrev = false;

        for (int i = firstVisible; i < count; i++)
        {
            GForceSample s = recorder[i];
            if (s.TimeSec > viewEnd) break;

            float xNorm = (float)((s.TimeSec - viewStart) / viewSpan);
            float xPx   = innerMin.X + xNorm * plotInnerWidth;

            float yMagPx = innerMin.Y + plotInnerHeight - (float)(s.Magnitude / yMax) * plotInnerHeight;
            float2 ptMag = new float2(xPx, yMagPx);
            if (hasPrev)
                drawList.AddLine(in prevMag, in ptMag, lineColorMag, 2f);
            prevMag = ptMag;

            if (_showAxes)
            {
                float yXPx = innerMin.Y + plotInnerHeight - (float)(Math.Abs(s.Longitudinal) / yMax) * plotInnerHeight;
                float yYPx = innerMin.Y + plotInnerHeight - (float)(Math.Abs(s.Lateral)      / yMax) * plotInnerHeight;
                float yZPx = innerMin.Y + plotInnerHeight - (float)(Math.Abs(s.Normal)       / yMax) * plotInnerHeight;
                float2 ptX = new float2(xPx, yXPx);
                float2 ptY = new float2(xPx, yYPx);
                float2 ptZ = new float2(xPx, yZPx);
                if (hasPrev)
                {
                    drawList.AddLine(in prevX, in ptX, lineColorX, 2f);
                    drawList.AddLine(in prevY, in ptY, lineColorY, 2f);
                    drawList.AddLine(in prevZ, in ptZ, lineColorZ, 2f);
                }
                prevX = ptX;
                prevY = ptY;
                prevZ = ptZ;
            }

            if (_showJerk)
            {
                float yJerkPx = innerMin.Y + plotInnerHeight - (float)(Math.Abs(s.Jerk) / jerkMax) * plotInnerHeight;
                float2 ptJerk = new float2(xPx, yJerkPx);
                if (hasPrev)
                    drawList.AddLine(in prevJerk, in ptJerk, lineColorJerk, 2f);
                prevJerk = ptJerk;
            }

            hasPrev = true;
        }

        // --- Threshold lines ---
        if (yMax > 3.0)
            DrawThresholdLine(drawList, innerMin, plotInnerWidth, plotInnerHeight, yMax, 3.0, ColorYellow, "3g");
        if (yMax > 6.0)
            DrawThresholdLine(drawList, innerMin, plotInnerWidth, plotInnerHeight, yMax, 6.0, ColorRed, "6g");
        if (yMax > (double)_killGeesThreshold)
            DrawThresholdLine(drawList, innerMin, plotInnerWidth, plotInnerHeight, yMax, (double)_killGeesThreshold, ColorRed, $"{_killGeesThreshold:F0}g");

        // --- Peak marker within visible viewport ---
        if (visiblePeakIdx >= 0)
        {
            GForceSample peakSample = recorder[visiblePeakIdx];
            float peakXNorm = (float)((peakSample.TimeSec - viewStart) / viewSpan);
            float peakXPx   = innerMin.X + peakXNorm * plotInnerWidth;
            float peakYPx   = innerMin.Y + plotInnerHeight - (float)(peakSample.Magnitude / yMax) * plotInnerHeight;
            float2 peakPt   = new float2(peakXPx, peakYPx);

            ImColor8 peakLineColor = ImGui.GetColorU32(new float4(ColorPeakMarker.X, ColorPeakMarker.Y, ColorPeakMarker.Z, 0.3f));
            float2 dashBottom = new float2(peakXPx, innerMin.Y + plotInnerHeight);
            drawList.AddLine(in peakPt, in dashBottom, peakLineColor);

            ImColor8 peakColor = ImGui.GetColorU32(ColorPeakMarker);
            float d = 4f;
            float2 top    = new float2(peakXPx,     peakYPx - d);
            float2 right  = new float2(peakXPx + d, peakYPx);
            float2 bottom = new float2(peakXPx,     peakYPx + d);
            float2 left   = new float2(peakXPx - d, peakYPx);
            drawList.AddLine(in top,    in right,  peakColor);
            drawList.AddLine(in right,  in bottom, peakColor);
            drawList.AddLine(in bottom, in left,   peakColor);
            drawList.AddLine(in left,   in top,    peakColor);

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

    private void DrawXAxisLabels(ImDrawListPtr drawList, float2 innerMin, float plotInnerWidth, float plotInnerHeight, double viewStart, double viewEnd, double viewSpan, double recordingStart, ImColor8 labelColor, ImColor8 gridColor)
    {
        double labelIntervalSec = ChooseTimeInterval(viewSpan);
        double firstLabel = Math.Ceiling(viewStart / labelIntervalSec) * labelIntervalSec;

        float yBase = innerMin.Y + plotInnerHeight;
        ImColor8 tickColor = ImGui.GetColorU32(new float4(0.4f, 0.4f, 0.4f, 0.6f));

        for (double t = firstLabel; t <= viewEnd; t += labelIntervalSec)
        {
            float xNorm = (float)((t - viewStart) / viewSpan);
            float xPx   = innerMin.X + xNorm * plotInnerWidth;

            float2 tickTop = new float2(xPx, innerMin.Y);
            float2 tickBot = new float2(xPx, yBase);
            drawList.AddLine(in tickTop, in tickBot, tickColor);

            // Label is offset from recording start so it stays stable as the view scrolls
            double relSec = t - recordingStart;
            string label = FormatTimeLabel(relSec);
            float2 labelPos = new float2(xPx - 10, yBase + 2);
            drawList.AddText(in labelPos, labelColor, label);
        }
    }

    private double ChooseTimeInterval(double viewSpan)
    {
        double targetInterval = viewSpan / 6.0;
        double[] niceIntervals = { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600 };
        foreach (double ni in niceIntervals)
        {
            if (ni >= targetInterval) return ni;
        }
        return 600;
    }

    private string FormatTimeLabel(double seconds)
    {
        if (seconds < 60)
            return $"{seconds:F0}s";
        int mins = (int)(seconds / 60);
        int secs = (int)(seconds % 60);
        return secs == 0 ? $"{mins}m" : $"{mins}:{secs:D2}";
    }

    private void DrawControls(GForceRecorder recorder)
    {
        // Time window selector
        ImGui.Text("Window:");
        for (int i = 0; i < WindowOptions.Length; i++)
        {
            ImGui.SameLine(0, 3);
            bool isSelected = _selectedWindowIdx == i;
            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ColorGreen));
                ImGui.PushStyleColor(ImGuiCol.Text, new float4(0f, 0f, 0f, 1f));
            }

            if (ImGui.Button(WindowLabels[i] + "###win" + i))
                _selectedWindowIdx = i;

            if (isSelected)
                ImGui.PopStyleColor(2);
        }

        // Recording controls + toggles
        if (recorder.IsRecording)
        {
            if (ImGui.Button(" ■ Pause "))
                recorder.IsRecording = false;
        }
        else
        {
            if (ImGui.Button(" ▶ Record "))
            {
                recorder.IsRecording = true;
                _isLive = true; // resume live follow when recording resumes
            }
        }

        ImGui.SameLine(0, 4);
        if (ImGui.Button(" Clear "))
        {
            recorder.Clear();
            _scrollOffsetSec = 0f;
            _isLive = true;
        }

        ImGui.SameLine(0, 12);
        ImGui.Checkbox("Axes", ref _showAxes);

        ImGui.SameLine(0, 8);
        ImGui.Checkbox("Jerk", ref _showJerk);
    }

    private void DrawThresholdLine(ImDrawListPtr drawList, float2 innerMin, float plotInnerWidth, float plotInnerHeight, double yMax, double threshold, float4 color, string label)
    {
        float yPx    = innerMin.Y + plotInnerHeight - (float)(threshold / yMax) * plotInnerHeight;
        ImColor8 col     = ImGui.GetColorU32(new float4(color.X, color.Y, color.Z, 0.4f));
        ImColor8 textCol = ImGui.GetColorU32(new float4(color.X, color.Y, color.Z, 0.6f));
        float2 p1 = new float2(innerMin.X, yPx);
        float2 p2 = new float2(innerMin.X + plotInnerWidth, yPx);
        drawList.AddLine(in p1, in p2, col);
        float2 labelPos = new float2(innerMin.X + plotInnerWidth - 24, yPx - 12);
        drawList.AddText(in labelPos, textCol, label);
    }

    private float4 GetGForceColor(double g)
    {
        if (g < 3.0) return ColorGreen;
        if (g < 6.0) return ColorYellow;
        return ColorRed;
    }

    private double CeilToNice(double value)
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

    private int CalculateGridLines(double yMax)
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
