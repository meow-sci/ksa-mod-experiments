using MeowSci.FlexoLib.Data;
using KSA;

namespace MeowSci.FlexoLib.Editor;

public enum FlexoEditorMode
{
    Idle,
    SelectFixed,
    SelectMoving,
    ConfigureHinge,
    ReadyToSave
}

public sealed class FlexoEditorState
{
    public FlexoEditorMode Mode { get; set; } = FlexoEditorMode.Idle;
    public Vehicle? LoadedVehicle { get; set; }
    public Part? FixedPart { get; set; }
    public Part? MovingPart { get; set; }
    public HingeDefinition WorkingHinge { get; } = new();
    public string DisplayName { get; set; } = "";
    public float PreviewAngle { get; set; }
    public string? StatusMessage { get; set; }
    public bool StatusIsError { get; set; }

    public void StartNewHinge()
    {
        FixedPart = null;
        MovingPart = null;
        WorkingHinge.FixedPartTemplateId = "";
        WorkingHinge.MovingPartTemplateId = "";
        WorkingHinge.AxisX = 0;
        WorkingHinge.AxisY = 1;
        WorkingHinge.AxisZ = 0;
        WorkingHinge.MinDegrees = 0;
        WorkingHinge.MaxDegrees = 180;
        WorkingHinge.RestingDegrees = 0;
        WorkingHinge.SpeedDegreesPerSecond = 45;
        DisplayName = "";
        PreviewAngle = 0f;
        StatusMessage = null;
        StatusIsError = false;
        Mode = FlexoEditorMode.SelectFixed;
    }

    public void OnPartSelected(Part? part)
    {
        if (part == null) return;

        switch (Mode)
        {
            case FlexoEditorMode.SelectFixed:
                FixedPart = part;
                WorkingHinge.FixedPartTemplateId = part.Template.Id;
                StatusMessage = $"Fixed part: {part.Template.Id}. Now select the moving part.";
                StatusIsError = false;
                Mode = FlexoEditorMode.SelectMoving;
                break;

            case FlexoEditorMode.SelectMoving:
                if (part == FixedPart)
                {
                    StatusMessage = "Cannot use the same part as both fixed and moving.";
                    StatusIsError = true;
                    return;
                }
                MovingPart = part;
                WorkingHinge.MovingPartTemplateId = part.Template.Id;
                StatusMessage = "Both parts selected. Configure hinge parameters.";
                StatusIsError = false;
                PreviewAngle = 0f;
                Mode = FlexoEditorMode.ConfigureHinge;
                break;
        }
    }

    public bool IsValid()
    {
        if (FixedPart == null || MovingPart == null) return false;
        if (string.IsNullOrWhiteSpace(DisplayName)) return false;
        if (string.IsNullOrWhiteSpace(WorkingHinge.FixedPartTemplateId)) return false;
        if (string.IsNullOrWhiteSpace(WorkingHinge.MovingPartTemplateId)) return false;

        double axisLenSq = WorkingHinge.AxisX * WorkingHinge.AxisX
            + WorkingHinge.AxisY * WorkingHinge.AxisY
            + WorkingHinge.AxisZ * WorkingHinge.AxisZ;
        if (axisLenSq < 0.001) return false;

        return true;
    }

    public void Reset()
    {
        Mode = FlexoEditorMode.Idle;
        FixedPart = null;
        MovingPart = null;
        DisplayName = "";
        PreviewAngle = 0f;
        StatusMessage = null;
        StatusIsError = false;
    }
}
