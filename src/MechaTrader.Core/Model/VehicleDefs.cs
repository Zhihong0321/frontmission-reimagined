namespace MechaTrader.Core.Model;

/// <summary>
/// What a vehicle or a piece of gear can do. Travel layers (land / air / water) gate
/// pathfinding; <see cref="Mine"/> is an activity, not a layer.
/// </summary>
public static class VehicleCapability
{
    public const string Land = "land";
    public const string Air = "air";
    public const string Water = "water";
    public const string Mine = "mine";

    public static readonly IReadOnlyList<string> Layers = new[] { Land, Air, Water };
}

public static class VehicleKind
{
    public const string Truck = "truck";
    public const string Machine = "machine";
}

/// <summary>A haulage vehicle or a working machine. Content, loaded from trucks.json.</summary>
public sealed class TruckDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double Capacity { get; init; }
    public double SpeedKmPerDay { get; init; }
    public double UpkeepPerDay { get; init; }
    public double FuelPerKm { get; init; }
    public long Price { get; init; }

    /// <summary><see cref="VehicleKind.Truck"/> or <see cref="VehicleKind.Machine"/>. Empty means truck.</summary>
    public string Kind { get; init; } = "";

    public List<string> Capabilities { get; init; } = new();

    /// <summary>Ore units extracted per day when parked on a deposit. Zero for haulers.</summary>
    public double MineYield { get; init; }

    public string EffectiveKind => string.IsNullOrWhiteSpace(Kind) ? VehicleKind.Truck : Kind;

    public bool HasCapability(string capability)
    {
        if (Capabilities.Count == 0) return capability == VehicleCapability.Land;
        foreach (var cap in Capabilities)
        {
            if (string.Equals(cap, capability, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

/// <summary>
/// A fitting the station can bolt onto one vehicle. Content, loaded from trucks.json.
/// Effects are read by <c>CaravanMath</c> per truck instance, never stored on the truck.
/// </summary>
public sealed class TruckUpgradeDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";
    public long Price { get; init; }

    /// <summary>Vehicle kinds this fits. Empty means every kind.</summary>
    public List<string> Kinds { get; init; } = new();

    public double CapacityBonus { get; init; }
    public double SpeedMult { get; init; } = 1.0;
    public double FuelMult { get; init; } = 1.0;
    public double UpkeepDelta { get; init; }
    public double MineYieldBonus { get; init; }

    public bool Fits(string kind)
    {
        if (Kinds.Count == 0) return true;
        foreach (var k in Kinds)
        {
            if (string.Equals(k, kind, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
