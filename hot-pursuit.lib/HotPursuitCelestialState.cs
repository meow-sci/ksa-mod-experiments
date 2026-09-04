using Brutal.Numerics;
using KSA;

namespace MeowSci.HotPursuitLib;

/// <summary>Mirrors KSA's main-camera nearby-celestial state for a mounted camera.</summary>
internal static class HotPursuitCelestialState
{
    private const double MetersToKilometers = 0.001;
    private const double MaxSurfaceDistanceKm = 80000.0;

    internal static void Synchronize(Camera camera)
    {
        Celestial? nearby = Program.FindNearbyCelestial(camera);
        if (nearby == null)
        {
            camera.NearbyCelestial = null;
            return;
        }

        double distanceKm = camera.DistanceTo(nearby.GetPositionEcl()) * MetersToKilometers;
        double surfaceDistanceKm = distanceKm - nearby.MeanRadius * MetersToKilometers;
        if (surfaceDistanceKm > MaxSurfaceDistanceKm)
        {
            camera.NearbyCelestial = null;
            return;
        }

        camera.NearbyCelestial = nearby;
        camera.DistanceToNearbyCelestialKm = distanceKm;
        camera.DistanceToNearbyCelestialSurfaceMeanKm = surfaceDistanceKm;
        camera.NearbyCelestialTerrainHeight = nearby.GetTerrainHeight(camera) * MetersToKilometers;

        double3 positionCce = nearby.GetPositionCce(camera);
        double terrainRadius = nearby.MeanRadius +
            nearby.GetTerrainHeightFromDirCce(positionCce.Normalized());
        camera.CurrentAltitudeKm = (positionCce.Length() - terrainRadius) * MetersToKilometers;
    }
}
