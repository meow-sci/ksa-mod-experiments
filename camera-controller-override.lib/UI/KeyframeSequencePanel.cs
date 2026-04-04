using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using MeowSci.CameraControllerOverrideLib.Animation;

namespace MeowSci.CameraControllerOverrideLib.UI;

/// <summary>
/// ImGui panel for managing and visualizing keyframe sequences.
/// Provides controls for playback, display of sequence state, and keyframe list management.
/// </summary>
public static class KeyframeSequencePanel
{
    // Track selected keyframe for move operations
    private static int _selectedKeyframeIndex = -1;
    
    // Track selected return-to-start easing type for conditional UI
    private static EasingType _returnToStartEasing = EasingType.Linear;
    
    /// <summary>
    /// Render the keyframe sequence panel.
    /// </summary>
    /// <param name="player">The keyframe sequence player to display and control.</param>
    public static void Render(KeyframeSequencePlayer player)
    {
        if (player == null)
        {
            ImGui.Text("Keyframe Sequence Player not available");
            return;
        }
        
        RenderStatusDisplay(player);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        RenderControlButtons(player);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        RenderReturnToStartControls(player);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        RenderKeyframesList(player);
    }
    
    /// <summary>
    /// Render the status display section showing current state, time, and progress.
    /// </summary>
    private static void RenderStatusDisplay(KeyframeSequencePlayer player)
    {
        // State text with color coding
        string stateText;
        if (player.IsReturningToStart)
        {
            stateText = "Returning to start...";
        }
        else
        {
            stateText = player.State switch
            {
                PlaybackState.Playing => $"Playing [{player.CurrentKeyframeIndex + 1}/{player.Keyframes.Count}]",
                PlaybackState.Paused => $"Paused [{player.CurrentKeyframeIndex + 1}/{player.Keyframes.Count}]",
                PlaybackState.Stopped => "Stopped",
                _ => "Unknown"
            };
        }
        
        float4 stateColor = player.State switch
        {
            PlaybackState.Playing => new float4(0.0f, 1.0f, 0.0f, 1.0f), // Green
            PlaybackState.Paused => new float4(1.0f, 1.0f, 0.0f, 1.0f),  // Yellow
            PlaybackState.Stopped => new float4(0.7f, 0.7f, 0.7f, 1.0f), // Gray
            _ => new float4(1.0f, 1.0f, 1.0f, 1.0f)
        };
        
        ImGui.Text("Status: ");
        ImGui.SameLine();
        ImGui.TextColored(stateColor, stateText);
        
        // Elapsed time display
        double totalDuration = player.TotalDuration;
        string timeText = totalDuration > 0
            ? $"{player.TotalElapsedTime:F1}s / {totalDuration:F1}s"
            : "0.0s / 0.0s";
        ImGui.Text($"Elapsed: {timeText}");
        
        // Progress bar
        float progress;
        if (player.IsReturningToStart)
        {
            // Show return progress based on return elapsed time / duration
            progress = player.ReturnToStartDuration > 0
                ? (float)(player.ReturnElapsedTime / player.ReturnToStartDuration)
                : 0.0f;
        }
        else
        {
            // Show keyframe sequence progress
            progress = totalDuration > 0 
                ? (float)(player.TotalElapsedTime / totalDuration)
                : 0.0f;
        }
        progress = Math.Clamp(progress, 0.0f, 1.0f);
        ImGui.ProgressBar(progress, new float2(-1, 0));
    }
    
    /// <summary>
    /// Render control buttons for playback management.
    /// </summary>
    private static void RenderControlButtons(KeyframeSequencePlayer player)
    {
        bool hasKeyframes = player.Keyframes.Count > 0;
        bool isPlaying = player.State == PlaybackState.Playing;
        bool isPaused = player.State == PlaybackState.Paused;
        bool isStopped = player.State == PlaybackState.Stopped;

        ImGui.BeginDisabled(!hasKeyframes || isPlaying);
        if (ImGui.Button(" ▶ Play ##cco")) player.Play();
        ImGui.EndDisabled();

        ImGui.SameLine(0, 8);

        ImGui.BeginDisabled(!isPlaying);
        if (ImGui.Button(" ■ Pause ##cco")) player.Pause();
        ImGui.EndDisabled();

        ImGui.SameLine(0, 8);

        ImGui.BeginDisabled(!isPaused);
        if (ImGui.Button(" ▶ Resume ##cco")) player.Resume();
        ImGui.EndDisabled();

        ImGui.SameLine(0, 8);

        ImGui.BeginDisabled(isStopped);
        if (ImGui.Button(" ■ Stop ##cco")) player.Stop();
        ImGui.EndDisabled();

        ImGui.SameLine(0, 8);

        ImGui.BeginDisabled(!hasKeyframes);
        if (ImGui.Button(" ✕ Clear All ##cco"))
        {
            player.Clear();
            _selectedKeyframeIndex = -1;
        }
        ImGui.EndDisabled();
    }
    
    /// <summary>
    /// Render the list of keyframes with properties and controls.
    /// </summary>
    private static void RenderKeyframesList(KeyframeSequencePlayer player)
    {
        if (player.Keyframes.Count == 0)
        {
            ImGui.TextColored(new float4(0.7f, 0.7f, 0.7f, 1.0f), "No keyframes added");
            return;
        }
        
        ImGui.Text($"Keyframes ({player.Keyframes.Count}):");
        ImGui.Spacing();
        
        for (int i = 0; i < player.Keyframes.Count; i++)
        {
            RenderKeyframeItem(player, i);
            
            // Add spacing between keyframes
            if (i < player.Keyframes.Count - 1)
            {
                ImGui.Spacing();
            }
        }
        
        // Selection and move controls
        if (_selectedKeyframeIndex >= 0 && _selectedKeyframeIndex < player.Keyframes.Count)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            RenderMoveControls(player);
        }
    }
    
    /// <summary>
    /// Render a single keyframe item with properties and controls.
    /// </summary>
    private static void RenderKeyframeItem(KeyframeSequencePlayer player, int index)
    {
        var keyframe = player.Keyframes[index];
        var animation = keyframe.Animation;
        
        // Determine if this is the currently playing keyframe
        bool isCurrentKeyframe = player.State == PlaybackState.Playing && 
                                  player.CurrentKeyframeIndex == index;
        
        // Display indicator and title
        string indicator = isCurrentKeyframe ? "[►]" : "[ ]";
        string title = $"{indicator} {index + 1}. {animation.Name} ({animation.DurationSeconds:F1}s)";

        ImGui.PushID(index);
        if (isCurrentKeyframe)
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(new float4(0.0f, 1.0f, 0.0f, 1.0f)));
        if (ImGui.Selectable(title, _selectedKeyframeIndex == index))
            _selectedKeyframeIndex = index;
        if (isCurrentKeyframe)
            ImGui.PopStyleColor();
        ImGui.PopID();
        
        ImGui.Indent();
        
        // Show animation properties
        var properties = animation.GetDisplayProperties();
        if (properties != null && properties.Count > 0)
        {
            foreach (var prop in properties)
            {
                ImGui.TextColored(
                    new float4(0.8f, 0.8f, 0.8f, 1.0f),
                    $"  {prop.Key}: {prop.Value}"
                );
            }
        }
        
        // Remove button
        if (ImGui.Button($"✕ Remove##{index}"))
        {
            player.RemoveKeyframe(index);
            
            // Adjust selected index if needed
            if (_selectedKeyframeIndex == index)
            {
                _selectedKeyframeIndex = -1;
            }
            else if (_selectedKeyframeIndex > index)
            {
                _selectedKeyframeIndex--;
            }
        }
        
        ImGui.Unindent();
    }
    
    /// <summary>
    /// Render return-to-start configuration controls.
    /// </summary>
    private static void RenderReturnToStartControls(KeyframeSequencePlayer player)
    {
        var tableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##cco_rts", 2, tableFlags))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Return to Start");
            ImGui.TableNextColumn();
            bool returnEnabled = player.ReturnToStartEnabled;
            if (ImGui.Checkbox("##cco_rts_chk", ref returnEnabled))
                player.ReturnToStartEnabled = returnEnabled;

            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Duration (s)");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            float returnDuration = (float)player.ReturnToStartDuration;
            if (ImGui.DragFloat("##cco_rts_dur", ref returnDuration, 0.1f, 1.0f, 10.0f))
                player.ReturnToStartDuration = returnDuration;

            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Easing");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            int returnEasing = (int)player.ReturnToStartEasing;
            string[] returnEasingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
            if (ImGui.Combo("##cco_rts_eas", ref returnEasing, returnEasingNames, returnEasingNames.Length))
            {
                _returnToStartEasing = (EasingType)returnEasing;
                player.ReturnToStartEasing = _returnToStartEasing;
            }
            else
            {
                _returnToStartEasing = player.ReturnToStartEasing;
            }

            if (_returnToStartEasing == EasingType.EaseIn || _returnToStartEasing == EasingType.EaseInOut)
            {
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Power (Start)");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                float powerStart = (float)player.ReturnToStartEasingPowerStart;
                if (ImGui.DragFloat("##cco_rts_ps", ref powerStart, 0.1f, 1.0f, 6.0f))
                    player.ReturnToStartEasingPowerStart = powerStart;
            }

            if (_returnToStartEasing == EasingType.EaseOut || _returnToStartEasing == EasingType.EaseInOut)
            {
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Power (End)");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                float powerEnd = (float)player.ReturnToStartEasingPowerEnd;
                if (ImGui.DragFloat("##cco_rts_pe", ref powerEnd, 0.1f, 1.0f, 6.0f))
                    player.ReturnToStartEasingPowerEnd = powerEnd;
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }
    
    /// <summary>
    /// Render controls for moving the selected keyframe.
    /// </summary>
    private static void RenderMoveControls(KeyframeSequencePlayer player)
    {
        ImGui.Text($"Selected: Keyframe {_selectedKeyframeIndex + 1}");
        
        // Move Up button
        bool canMoveUp = _selectedKeyframeIndex > 0;
        if (!canMoveUp)
        {
            ImGui.BeginDisabled();
        }
        if (ImGui.Button("↑ Move Up"))
        {
            player.MoveKeyframe(_selectedKeyframeIndex, _selectedKeyframeIndex - 1);
            _selectedKeyframeIndex--;
        }
        if (!canMoveUp)
        {
            ImGui.EndDisabled();
        }
        
        ImGui.SameLine();
        
        // Move Down button
        bool canMoveDown = _selectedKeyframeIndex < player.Keyframes.Count - 1;
        if (!canMoveDown)
        {
            ImGui.BeginDisabled();
        }
        if (ImGui.Button("↓ Move Down"))
        {
            player.MoveKeyframe(_selectedKeyframeIndex, _selectedKeyframeIndex + 1);
            _selectedKeyframeIndex++;
        }
        if (!canMoveDown)
        {
            ImGui.EndDisabled();
        }
        
        ImGui.SameLine();
        
        // Deselect button
        if (ImGui.Button("Deselect"))
        {
            _selectedKeyframeIndex = -1;
        }
    }
}
