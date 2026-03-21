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

    public static double ComputeMaxAcceleration(Vehicle vehicle)
    {
        double maxThrustN = (double)vehicle.FlightComputer.VehicleConfig.TotalEngineVacuumThrust;
        double totalMass = (double)vehicle.TotalMass;
        return totalMass > 0.0 ? maxThrustN / totalMass : 0.0;
    }
}
