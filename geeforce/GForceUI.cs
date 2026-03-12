using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;

namespace mod;

public static class GForceUI
{
    // History duration options in seconds
    private static readonly float[] HistoryOptions = { 30f, 60f, 120f, 300f };
    private static int _selectedHistoryIdx = 1; // default 60s

    // Graph colors
    private static readonly float4 ColorGreen  = new float4(0.0f, 1.0f, 0.0f, 1.0f);
    private static readonly float4 ColorYellow = new float4(1.0f, 1.0f, 0.0f, 1.0f);
    private static readonly float4 ColorRed    = new float4(1.0f, 0.2f, 0.2f, 1.0f);
    private static readonly float4 ColorCyan   = new float4(0.0f, 0.8f, 1.0f, 1.0f);
    private static readonly float4 ColorWhite  = new float4(1.0f, 1.0f, 1.0f, 1.0f);
    private static readonly float4 ColorGrey   = new float4(0.5f, 0.5f, 0.5f, 1.0f);
    private static readonly float4 ColorDimGrey = new float4(0.3f, 0.3f, 0.3f, 1.0f);
    private static readonly float4 ColorBg     = new float4(0.1f, 0.1f, 0.12f, 1.0f);

    // Axis line colors
    private static readonly float4 ColorAxisX = new float4(1.0f, 0.4f, 0.4f, 0.8f); // Longitudinal - red
    private static readonly float4 ColorAxisY = new float4(0.4f, 1.0f, 0.4f, 0.8f); // Lateral - green
    private static readonly float4 ColorAxisZ = new float4(0.4f, 0.6f, 1.0f, 0.8f); // Normal - blue

    private static bool _showAxes = false;

    public static float GetSelectedHistorySeconds() => HistoryOptions[_selectedHistoryIdx];

    /// <summary>
    /// Returns the required buffer capacity for the current history setting at the given sample rate.
    /// </summary>
    public static int GetRequiredCapacity(double sampleIntervalSec)
    {
        return (int)(HistoryOptions[_selectedHistoryIdx] / sampleIntervalSec);
    }

    public static void Render(ref bool visible, GForceRecorder recorder, double sampleIntervalSec)
    {
        ImGui.SetNextWindowSize(new float2(420, 380), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("G-Force Monitor###geeforce", ref visible, ImGuiWindowFlags.NoSavedSettings))
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

        // --- Per-axis readout ---
        ImGui.TextColored(ColorAxisX, $"X: {recorder.Latest.Longitudinal:F2}g");
        ImGui.SameLine(0, 10);
        ImGui.TextColored(ColorAxisY, $"Y: {recorder.Latest.Lateral:F2}g");
        ImGui.SameLine(0, 10);
        ImGui.TextColored(ColorAxisZ, $"Z: {recorder.Latest.Normal:F2}g");

        ImGui.Separator();

        // --- Graph ---
        DrawGraph(recorder, sampleIntervalSec);

        ImGui.Separator();

        // --- Controls row ---
        // History duration selector
        ImGui.Text("History:");
        for (int i = 0; i < HistoryOptions.Length; i++)
        {
            ImGui.SameLine(0, 4);
            string label = HistoryOptions[i] >= 60 ? $"{(int)(HistoryOptions[i] / 60)}m" : $"{(int)HistoryOptions[i]}s";
            if (_selectedHistoryIdx == i)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ColorGreen));
            }
            if (ImGui.Button(label + "###hist" + i))
            {
                _selectedHistoryIdx = i;
                int newCapacity = GetRequiredCapacity(sampleIntervalSec);
                recorder.Resize(newCapacity);
            }
            if (_selectedHistoryIdx == i)
            {
                ImGui.PopStyleColor(1);
            }
        }

        // Recording / Clear controls
        ImGui.SameLine(0, 20);
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

        ImGui.SameLine(0, 4);
        ImGui.Checkbox("Axes", ref _showAxes);

        ImGui.End();
    }

    private static void DrawGraph(GForceRecorder recorder, double sampleIntervalSec)
    {
        float availWidth = ImGui.GetContentRegionAvail().X;
        float graphHeight = 180f;
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
            // Nothing to draw; show placeholder text
            ImColor8 grey = ImGui.GetColorU32(ColorGrey);
            float2 textPos = new float2(plotMin.X + 10, plotMin.Y + graphHeight * 0.5f - 7);
            drawList.AddText(in textPos, grey, "Waiting for data...");
            drawList.PopClipRect();
            return;
        }

        // Y-axis scale: dynamic based on peak, with a minimum of 1g
        double yMax = Math.Max(recorder.PeakG * 1.15, 1.0);
        // Round up to a nice number
        yMax = CeilToNice(yMax);

        float padLeft = 36f;  // space for Y-axis labels
        float padRight = 4f;
        float plotInnerWidth = plotSize.X - padLeft - padRight;
        float plotInnerHeight = plotSize.Y - 4f;

        float2 innerMin = new float2(plotMin.X + padLeft, plotMin.Y + 2f);

        // --- Grid lines ---
        ImColor8 gridColor = ImGui.GetColorU32(ColorDimGrey);
        ImColor8 labelColor = ImGui.GetColorU32(ColorGrey);

        // Horizontal grid lines + Y labels
        int gridLines = CalculateGridLines(yMax);
        for (int i = 0; i <= gridLines; i++)
        {
            double gValue = yMax * i / gridLines;
            float yPx = innerMin.Y + plotInnerHeight - (float)(gValue / yMax) * plotInnerHeight;

            float2 lineStart = new float2(innerMin.X, yPx);
            float2 lineEnd = new float2(innerMin.X + plotInnerWidth, yPx);
            drawList.AddLine(in lineStart, in lineEnd, gridColor);

            // Y-axis label
            string label = gValue >= 10 ? $"{gValue:F0}" : $"{gValue:F1}";
            float2 labelPos = new float2(plotMin.X + 2, yPx - 6);
            drawList.AddText(in labelPos, labelColor, label);
        }

        // --- Plot the magnitude line ---
        double timeSpan = HistoryOptions[_selectedHistoryIdx];
        double newestTime = recorder[count - 1].TimeSec;
        double oldestVisibleTime = newestTime - timeSpan;

        ImColor8 lineColorMag = ImGui.GetColorU32(ColorGreen);
        ImColor8 lineColorX = ImGui.GetColorU32(ColorAxisX);
        ImColor8 lineColorY = ImGui.GetColorU32(ColorAxisY);
        ImColor8 lineColorZ = ImGui.GetColorU32(ColorAxisZ);

        float2 prevMag = default;
        float2 prevX = default;
        float2 prevY = default;
        float2 prevZ = default;
        bool hasPrev = false;

        for (int i = 0; i < count; i++)
        {
            GForceSample s = recorder[i];
            if (s.TimeSec < oldestVisibleTime) continue;

            float xNorm = (float)((s.TimeSec - oldestVisibleTime) / timeSpan);
            float xPx = innerMin.X + xNorm * plotInnerWidth;

            // Magnitude
            float yMagPx = innerMin.Y + plotInnerHeight - (float)(s.Magnitude / yMax) * plotInnerHeight;
            float2 ptMag = new float2(xPx, yMagPx);

            if (hasPrev)
            {
                drawList.AddLine(in prevMag, in ptMag, lineColorMag);
            }
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

            hasPrev = true;
        }

        // --- Color-coded threshold lines ---
        // 3g warning line (yellow)
        if (yMax > 3.0)
        {
            DrawThresholdLine(drawList, innerMin, plotInnerWidth, plotInnerHeight, yMax, 3.0, ColorYellow, "3g");
        }
        // 6g danger line (red)
        if (yMax > 6.0)
        {
            DrawThresholdLine(drawList, innerMin, plotInnerWidth, plotInnerHeight, yMax, 6.0, ColorRed, "6g");
        }

        // --- Current value indicator (right edge dot) ---
        if (count > 0)
        {
            float curYPx = innerMin.Y + plotInnerHeight - (float)(recorder.Latest.Magnitude / yMax) * plotInnerHeight;
            float2 curPt = new float2(innerMin.X + plotInnerWidth, curYPx);
            ImColor8 dotColor = ImGui.GetColorU32(GetGForceColor(recorder.Latest.Magnitude));
            drawList.AddCircleFilled(in curPt, 3f, dotColor);
        }

        drawList.PopClipRect();

        // Y-axis unit label
        ImGui.Text("g");
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
        return Math.Ceiling(value / 10.0) * 10.0;
    }

    private static int CalculateGridLines(double yMax)
    {
        if (yMax <= 2.0) return 4;
        if (yMax <= 5.0) return 5;
        if (yMax <= 10.0) return 5;
        if (yMax <= 20.0) return 4;
        return 5;
    }
}
