using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.Numerics;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// One placed SubPart instance within the Part being edited.
/// </summary>
public sealed class SubPartPlacement
{
    /// <summary>Unique instance name within this Part (e.g. "panel_1", "screw_2").</summary>
    public string InstanceId { get; set; } = "";

    /// <summary>References a SubPart template ID from ModLibrary (e.g. "Core.Screw.A").</summary>
    public string SubPartTemplateId { get; set; } = "";

    /// <summary>Position relative to Part origin in assembly space.</summary>
    public double3 Position { get; set; } = double3.Zero;

    /// <summary>Rotation in assembly space.</summary>
    public doubleQuat Rotation { get; set; } = doubleQuat.Identity;

    /// <summary>Scale (default 1,1,1).</summary>
    public double3 Scale { get; set; } = double3.One;
}

/// <summary>
/// Metadata fields matching PartTemplate attributes. These are the optional GameData
/// fields a Part can have.
/// </summary>
public sealed class PartGameDataState
{
    public string DisplayName { get; set; } = "";
    public List<string> EditorTags { get; set; } = new();

    /// <summary>Mass in kg (null = no custom mass defined).</summary>
    public double? CustomMass { get; set; }

    /// <summary>Battery capacity in watt-hours (null = no battery).</summary>
    public double? BatteryCapacity { get; set; }

    /// <summary>Generator output in watts (null = no generator).</summary>
    public double? GeneratorOutput { get; set; }
}

/// <summary>
/// The full Part being assembled in the editor. Contains all placements and metadata.
/// </summary>
public sealed class EditingPart
{
    /// <summary>The Part ID used in XML (must be unique, e.g. "MyMod.MyPart").</summary>
    public string PartId { get; set; } = "MyMod.NewPart";

    /// <summary>All placed SubPart instances.</summary>
    public List<SubPartPlacement> Placements { get; set; } = new();

    /// <summary>Optional GameData metadata.</summary>
    public PartGameDataState GameData { get; set; } = new();

    /// <summary>Deep clone for undo.</summary>
    public EditingPart Clone()
    {
        var clone = new EditingPart { PartId = PartId };
        clone.GameData.DisplayName = GameData.DisplayName;
        clone.GameData.EditorTags.AddRange(GameData.EditorTags);
        clone.GameData.CustomMass = GameData.CustomMass;
        clone.GameData.BatteryCapacity = GameData.BatteryCapacity;
        clone.GameData.GeneratorOutput = GameData.GeneratorOutput;
        foreach (var p in Placements)
            clone.Placements.Add(new SubPartPlacement
            {
                InstanceId = p.InstanceId,
                SubPartTemplateId = p.SubPartTemplateId,
                Position = p.Position,
                Rotation = p.Rotation,
                Scale = p.Scale
            });
        return clone;
    }
}

/// <summary>
/// Editor state machine. Owns the current <see cref="EditingPart"/> and manages
/// undo/redo history, selection, and high-level mutation helpers.
/// </summary>
public sealed class PartEditorController
{
    private const int MaxUndoDepth = 50;

    private readonly Stack<EditingPart> _undoStack = new();
    private readonly Stack<EditingPart> _redoStack = new();

    public EditingPart CurrentPart { get; private set; } = new();

    /// <summary>Index of the currently selected SubPartPlacement, or -1 for none.</summary>
    public int SelectedPlacementIndex { get; set; } = -1;

    /// <summary>Convenience getter for the selected placement (null if none selected).</summary>
    public SubPartPlacement? SelectedPlacement =>
        SelectedPlacementIndex >= 0 && SelectedPlacementIndex < CurrentPart.Placements.Count
            ? CurrentPart.Placements[SelectedPlacementIndex]
            : null;

    /// <summary>Whether there are unsaved changes.</summary>
    public bool IsDirty { get; private set; } = false;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Start a new empty Part. Resets undo history.</summary>
    public void NewPart()
    {
        CurrentPart = new EditingPart();
        SelectedPlacementIndex = -1;
        _undoStack.Clear();
        _redoStack.Clear();
        IsDirty = false;
    }

    /// <summary>Replace the current Part (e.g. when loading from file). Resets undo history.</summary>
    public void LoadPart(EditingPart part)
    {
        CurrentPart = part;
        SelectedPlacementIndex = -1;
        _undoStack.Clear();
        _redoStack.Clear();
        IsDirty = false;
    }

    /// <summary>Save a snapshot to the undo stack (call before making a mutation).</summary>
    public void PushUndo()
    {
        if (_undoStack.Count >= MaxUndoDepth)
        {
            // Remove the oldest undo state by rebuilding the stack without the bottom item
            var temp = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = temp.Length - 1; i > 0; i--)
                _undoStack.Push(temp[i]);
        }
        _undoStack.Push(CurrentPart.Clone());
        _redoStack.Clear();
        IsDirty = true;
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _redoStack.Push(CurrentPart.Clone());
        CurrentPart = _undoStack.Pop();
        SelectedPlacementIndex = Math.Clamp(SelectedPlacementIndex, -1, CurrentPart.Placements.Count - 1);
        IsDirty = true;
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _undoStack.Push(CurrentPart.Clone());
        CurrentPart = _redoStack.Pop();
        SelectedPlacementIndex = Math.Clamp(SelectedPlacementIndex, -1, CurrentPart.Placements.Count - 1);
        IsDirty = true;
    }

    /// <summary>Mark the part as saved (clears dirty flag).</summary>
    public void MarkSaved()
    {
        IsDirty = false;
    }

    /// <summary>
    /// Add a new SubPart from the catalog. Automatically generates a unique instance ID
    /// and selects the new placement.
    /// </summary>
    public void AddSubPart(string subPartTemplateId)
    {
        PushUndo();
        string baseName = subPartTemplateId.Split('.').Last().ToLowerInvariant();
        int count = CurrentPart.Placements.Count(p => p.SubPartTemplateId == subPartTemplateId);
        var placement = new SubPartPlacement
        {
            InstanceId = $"{baseName}_{count + 1}",
            SubPartTemplateId = subPartTemplateId,
            Position = double3.Zero,
            Rotation = doubleQuat.Identity,
            Scale = double3.One
        };
        CurrentPart.Placements.Add(placement);
        SelectedPlacementIndex = CurrentPart.Placements.Count - 1;
    }

    /// <summary>Remove the currently selected placement.</summary>
    public void RemoveSelected()
    {
        if (SelectedPlacement == null) return;
        PushUndo();
        CurrentPart.Placements.RemoveAt(SelectedPlacementIndex);
        SelectedPlacementIndex = Math.Clamp(SelectedPlacementIndex - 1, -1, CurrentPart.Placements.Count - 1);
    }

    /// <summary>Duplicate the currently selected placement with a slight position offset.</summary>
    public void DuplicateSelected()
    {
        if (SelectedPlacement == null) return;
        PushUndo();
        var src = SelectedPlacement;
        string baseName = src.SubPartTemplateId.Split('.').Last().ToLowerInvariant();
        int count = CurrentPart.Placements.Count(p => p.SubPartTemplateId == src.SubPartTemplateId);
        var copy = new SubPartPlacement
        {
            InstanceId = $"{baseName}_{count + 1}",
            SubPartTemplateId = src.SubPartTemplateId,
            Position = src.Position + new double3(0.5, 0, 0),
            Rotation = src.Rotation,
            Scale = src.Scale
        };
        CurrentPart.Placements.Add(copy);
        SelectedPlacementIndex = CurrentPart.Placements.Count - 1;
    }

    /// <summary>
    /// Update the selected placement's transform. Call after finishing a gizmo drag
    /// (does NOT push undo — caller should push before starting the drag).
    /// </summary>
    public void UpdateSelectedTransform(double3 position, doubleQuat rotation, double3 scale)
    {
        if (SelectedPlacement == null) return;
        SelectedPlacement.Position = position;
        SelectedPlacement.Rotation = rotation;
        SelectedPlacement.Scale = scale;
        IsDirty = true;
    }
}
