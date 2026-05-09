using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.RedAlertLib;

/// <summary>Per-plan UI state for the "Add Action" form, so each plan's combos
/// and inputs are independent of every other plan rendered in the same frame.</summary>
internal sealed class PlanFormState
{
    public int VehicleIndex = -1;
    public int PartIndex = -1;
    public int ActionTypeIndex = -1;
    public float4 Color = new(1f, 1f, 1f, 1f);
    public float Actuate = 0.5f;

    public readonly ImInputString VehicleFilter = new(64);
    public readonly ImInputString PartFilter = new(64);

    /// <summary>Cached scan results — refreshed when the selected vehicle changes.</summary>
    public readonly System.Collections.Generic.List<ActionablePart> ScannedParts = new();
    public int PrevVehicleIndex = -2;
}
