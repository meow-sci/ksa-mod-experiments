using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace MeowSci.JplRepo;

/// <summary>
/// Animates three cheer sequences as top-level menu bar items, cycling through
/// hold → slide-left → hold → ... with per-sequence text colours.
/// </summary>
internal static class MenuBarAnimation
{
    private static readonly string[][] Sequences =
    [
        ["JPLRepo!", "he's",  "our",  "man,"],
        ["if",      "he",    "can't", "do",   "it..."],
        ["no",      "one",   "can!"],
    ];

    // One Color.Preset per sequence, passed directly to GetColorU32.
    private static readonly Color.Preset[] Colors =
    [
        KSAColor.Xkcd.RadioactiveGreen,
        KSAColor.Xkcd.Custard,
        KSAColor.Xkcd.ReddishPink,
    ];

    private static readonly float4 Black = new float4(0f, 0f, 0f, 1f);
    private static readonly float4 White = new float4(1f, 1f, 1f, 1f);

    private static readonly float4[] TextColors =
    [
        Black, // RadioactiveGreen
        Black, // Custard
        White, // ReddishPink
    ];

    private const double HoldSeconds     = 2.0;
    private const double SlideStepSeconds = 0.05; // 50 ms per word shift

    private static int      _currentSeq  = 0;
    private static bool     _isSliding   = false;
    private static DateTime _phaseStart  = DateTime.UtcNow;

    public static void Draw()
    {
        double elapsed = (DateTime.UtcNow - _phaseStart).TotalSeconds;
        int    nextSeq = (_currentSeq + 1) % Sequences.Length;

        if (!_isSliding)
        {
            if (elapsed < HoldSeconds)
            {
                DrawWords(Sequences[_currentSeq], 0, Sequences[_currentSeq].Length - 1, _currentSeq);
                return;
            }
            // Transition to slide phase
            _isSliding  = true;
            _phaseStart = DateTime.UtcNow;
            elapsed     = 0.0;
        }

        // --- Slide phase ---
        int slideStep  = (int)(elapsed / SlideStepSeconds);
        int currentLen = Sequences[_currentSeq].Length;

        if (slideStep >= currentLen)
        {
            // Slide complete: snap to next sequence and start holding
            _currentSeq = nextSeq;
            _isSliding  = false;
            _phaseStart = DateTime.UtcNow;
            DrawWords(Sequences[_currentSeq], 0, Sequences[_currentSeq].Length - 1, _currentSeq);
            return;
        }

        // Tail of current sequence (words that haven't slid off yet)
        DrawWords(Sequences[_currentSeq], slideStep, currentLen - 1, _currentSeq);

        // Head of next sequence filling in from the right
        int fillCount = Math.Min(slideStep, Sequences[nextSeq].Length);
        if (fillCount > 0)
            DrawWords(Sequences[nextSeq], 0, fillCount - 1, nextSeq);
    }

    private static void DrawWords(string[] words, int from, int to, int seqIndex)
    {
        uint bgColor   = ImGui.GetColorU32(Colors[seqIndex]);
        uint textColor = ImGui.GetColorU32(TextColors[seqIndex]);

        var  drawList  = ImGui.GetWindowDrawList();
        var  style     = ImGui.GetStyle();
        float padX     = style.FramePadding.X;
        float itemH    = ImGui.GetTextLineHeightWithSpacing();

        // Header/Hovered/Active all match so the bg stays consistent when opened/hovered.
        ImGui.PushStyleColor(ImGuiCol.Header,        bgColor);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, bgColor);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive,  bgColor);
        ImGui.PushStyleColor(ImGuiCol.Text,          textColor);

        for (int i = from; i <= to; i++)
        {
            // Draw the background rect before BeginMenu so it sits behind the text.
            float2 pos   = ImGui.GetCursorScreenPos();
            float  itemW = ImGui.CalcTextSize(words[i]).X + padX * 2f;
            float2 pMax  = pos + new float2(itemW, itemH);
            drawList.AddRectFilled(in pos, in pMax, bgColor);

            if (ImGui.BeginMenu(words[i]))
                ImGui.EndMenu();
        }

        ImGui.PopStyleColor(4);
    }
}
