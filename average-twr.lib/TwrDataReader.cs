using KSA;
namespace MeowSci.AverageTwrLib;

public static class TwrDataReader
{
    public static double ReadTwr(Vehicle vehicle) => vehicle.NavBallData.ThrustWeightRatio;

    public static double ComputeSurfaceGravity(Vehicle vehicle)
    {
        const double G = 6.6743e-11;
        var r = vehicle.Parent.MeanRadius;
        return G * vehicle.Parent.Mass / (r * r);
    }

    /// <summary>
    /// Maximum acceleration from the engines that can actually produce thrust right now, corrected
    /// for ambient pressure.
    ///
    /// KSA build 2026.8.3.5117 (rev 5114) removed <c>FlightComputer.VehicleConfig.TotalEngineVacuumThrust</c>
    /// along with the rest of the vacuum-referenced aggregates, and moved the navball's own TWR onto
    /// <c>Vehicle.ComputeActiveThrust(ambientPressure)</c> — which skips engines that are out of
    /// propellant. Reading the same value keeps this figure consistent with <see cref="ReadTwr"/>
    /// (which comes from <c>NavBallData.ThrustWeightRatio</c>) and with the in-game gauge.
    /// </summary>
    public static double ComputeMaxAcceleration(Vehicle vehicle)
    {
        double maxThrustN = vehicle.ComputeActiveThrust(vehicle.FlightComputer.AmbientPressure);
        double totalMass = (double)vehicle.TotalMass;
        return totalMass > 0.0 ? maxThrustN / totalMass : 0.0;
    }
}
