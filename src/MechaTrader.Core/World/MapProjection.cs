namespace MechaTrader.Core.World;

/// <summary>
/// Shared equirectangular projection for cities and the terrain grid. Same numbers the
/// city loader has always used, so a cell and a city that share a lon/lat share a point.
/// </summary>
public static class MapProjection
{
    public const double KmPerDegreeLat = 111.32;
    public const double ReferenceLat = 47.5;

    public static double KmPerDegreeLon { get; } =
        KmPerDegreeLat * Math.Cos(ReferenceLat * Math.PI / 180.0);

    public static double X(double lon) => lon * KmPerDegreeLon;

    public static double Y(double lat) => -lat * KmPerDegreeLat;
}
