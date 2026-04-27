namespace MeowSci.FlexoLib.Data;

public sealed class HingeDefinition
{
    public string FixedPartTemplateId { get; set; } = "";
    public string MovingPartTemplateId { get; set; } = "";

    // Rotation axis in moving part's local space
    public double AxisX { get; set; } = 0;
    public double AxisY { get; set; } = 1;
    public double AxisZ { get; set; } = 0;

    // Degree constraints
    public double MinDegrees { get; set; } = 0;
    public double MaxDegrees { get; set; } = 180;
    public double RestingDegrees { get; set; } = 0;

    // Motor
    public double SpeedDegreesPerSecond { get; set; } = 45;
}
