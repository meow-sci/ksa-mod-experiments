using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using mod.Animation;

namespace mod.UI;

/// <summary>
/// ImGui panel for managing and visualizing keyframe sequences.
/// Provides controls for playback, display of sequence state, and keyframe list management.
/// </summary>
public static class KeyframeSequencePanel
{
    // Track selected keyframe for move operations
    private static int _selectedKeyframeIndex = -1;
    
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
        
        if (ImGui.CollapsingHeader("Keyframe Sequence"))
        {
            ImGui.Indent();
            
            RenderStatusDisplay(player);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            RenderControlButtons(player);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            RenderKeyframesList(player);
            
            ImGui.Unindent();
        }
    }
    
    /// <summary>
    /// Render the status display section showing current state, time, and progress.
    /// </summary>
    private static void RenderStatusDisplay(KeyframeSequencePlayer player)
    {
        // State text with color coding
        string stateText = player.State switch
        {
            PlaybackState.Playing => $"Playing [{player.CurrentKeyframeIndex + 1}/{player.Keyframes.Count}]",
            PlaybackState.Paused => $"Paused [{player.CurrentKeyframeIndex + 1}/{player.Keyframes.Count}]",
            PlaybackState.Stopped => "Stopped",
            _ => "Unknown"
        };
        
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
        float progress = totalDuration > 0 
            ? (float)(player.TotalElapsedTime / totalDuration)
            : 0.0f;
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
        
        // Play button
        if (!hasKeyframes || isPlaying)
        {
            ImGui.BeginDisabled();
        }
        if (ImGui.Button("▶ Play"))
        {
            player.Play();
        }
        if (!hasKeyframes || isPlaying)
        {
            ImGui.EndDisabled();
        }
        
        ImGui.SameLine();
        
        // Pause button
        if (!isPlaying)
        {
            ImGui.BeginDisabled();
        }
        if (ImGui.Button("⏸ Pause"))
        {
            player.Pause();
        }
        if (!isPlaying)
        {
            ImGui.EndDisabled();
        }
        
        ImGui.SameLine();
        
        // Resume button
        if (!isPaused)
        {
            ImGui.BeginDisabled();
        }
        if (ImGui.Button("▶ Resume"))
        {
            player.Resume();
        }
        if (!isPaused)
        {
            ImGui.EndDisabled();
        }
        
        ImGui.SameLine();
        
        // Stop button
        if (isStopped)
        {
            ImGui.BeginDisabled();
        }
        if (ImGui.Button("⏹ Stop"))
        {
            player.Stop();
        }
        if (isStopped)
        {
            ImGui.EndDisabled();
        }
        
        ImGui.SameLine();
        
        // Clear All button
        if (!hasKeyframes)
        {
            ImGui.BeginDisabled();
        }
        if (ImGui.Button("Clear All"))
        {
            // Simple confirmation - could be enhanced with a modal dialog
            player.Clear();
            _selectedKeyframeIndex = -1;
        }
        if (!hasKeyframes)
        {
            ImGui.EndDisabled();
        }
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
        
        // Highlight currently playing keyframe
        float4 titleColor = isCurrentKeyframe 
            ? new float4(0.0f, 1.0f, 0.0f, 1.0f)  // Green
            : new float4(1.0f, 1.0f, 1.0f, 1.0f); // White
        
        // Display indicator and title
        string indicator = isCurrentKeyframe ? "[►]" : "[ ]";
        string title = $"{indicator} {index + 1}. {animation.Name} ({animation.DurationSeconds:F1}s)";
        
        // Make title selectable/clickable
        if (ImGui.Selectable(title, _selectedKeyframeIndex == index))
        {
            _selectedKeyframeIndex = index;
        }
        
        // Apply color to title text if needed
        if (isCurrentKeyframe)
        {
            // Re-render with color (selectable doesn't support color directly)
            ImGui.SameLine(0, -ImGui.CalcTextSize(title).X - 4);
            ImGui.TextColored(titleColor, title);
        }
        
        ImGui.Indent();
        
        // Show transition info if enabled
        if (keyframe.IncludeTransitionIn && index > 0)
        {
            ImGui.TextColored(
                new float4(0.6f, 0.8f, 1.0f, 1.0f), 
                $"↕ Transition: {keyframe.TransitionInDurationSeconds:F1}s {keyframe.TransitionInEasing}"
            );
        }
        
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
