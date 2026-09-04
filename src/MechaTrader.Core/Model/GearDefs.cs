namespace MechaTrader.Core.Model;

/// <summary>A portable tool. Content, loaded from gear.json. Occupies hold volume.</summary>
public sealed class GearDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public long Price { get; init; }
    public double Volume { get; init; }
    public List<string> Capabilities { get; init; } = new();
    public double MineYield { get; init; }

    public bool HasCapability(string capability)
    {
        foreach (var cap in Capabilities)
        {
            if (string.Equals(cap, capability, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
