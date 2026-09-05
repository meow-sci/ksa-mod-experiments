using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MeowSci.Unscience.Contracts;

/// <summary>A detached, explicitly populated authoring snapshot. Never contains runtime objects.</summary>
public sealed class DraftState
{
    public int Version { get; set; } = 1;
    public Dictionary<string, JsonElement> Fields { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, JsonElement> Targets { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, bool> Sections { get; set; } = new(StringComparer.Ordinal);

    public DraftState Clone() => JsonSerializer.Deserialize<DraftState>(JsonSerializer.Serialize(this))!;
}

public interface IWorkspaceParticipant
{
    string FeatureId { get; }
    DraftState CaptureDraft();
    /// <summary>Decode and validate NOW; returned action only replaces authoring fields.</summary>
    Action PrepareRestore(DraftState state);
}

public sealed class FeatureSnapshot
{
    public string SelectedPreset { get; set; } = "";
    public string PresetFilter { get; set; } = "";
    public bool Visible { get; set; } = true;
    public DraftState Draft { get; set; } = new();
}

public sealed class WindowPlacement
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; } = 800;
    public float Height { get; set; } = 700;
}

public sealed class WorkspaceDocument
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled";
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;
    public string SelectedFeature { get; set; } = "";
    public bool ShowTooltips { get; set; } = true;
    public bool MainWindowVisible { get; set; } = true;
    public bool LoadWindowVisible { get; set; }
    public string LoadFilter { get; set; } = "";
    public string SelectedSave { get; set; } = "";
    public bool LiveWindowVisible { get; set; }
    public string SelectedLiveItem { get; set; } = "";
    public string FeatureFilter { get; set; } = "";
    public string LiveFilter { get; set; } = "";
    public Dictionary<string, WindowPlacement> Windows { get; set; } = new();
    public Dictionary<string, FeatureSnapshot> Features { get; set; } = new(StringComparer.Ordinal);
}
