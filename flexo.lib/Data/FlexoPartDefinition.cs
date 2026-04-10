namespace MeowSci.FlexoLib.Data;

public sealed class FlexoPartDefinition
{
    public string FileName { get; set; } = "";
    public FlexoPartType PartType { get; set; } = FlexoPartType.Hinge;
    public string DisplayName { get; set; } = "";
    public string CreatedFromVehicle { get; set; } = "";

    public HingeDefinition? Hinge { get; set; }
}
