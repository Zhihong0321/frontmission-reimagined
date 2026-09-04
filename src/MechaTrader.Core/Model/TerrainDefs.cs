namespace MechaTrader.Core.Model;

/// <summary>Road type on a route edge. Content, loaded from terrain.json.</summary>
public sealed class TerrainDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double SpeedMultiplier { get; init; } = 1.0;
    public double CostMultiplier { get; init; } = 1.0;
}
