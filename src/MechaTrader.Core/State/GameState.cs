namespace MechaTrader.Core.State;

/// <summary>
/// A holding of one good. Cost is tracked in total rather than per-unit so that
/// weighted average cost stays exact across many partial buys and sells.
/// </summary>
public sealed class CargoLot
{
    public int Units { get; set; }
    public long TotalCost { get; set; }

    public double AverageCost => Units > 0 ? (double)TotalCost / Units : 0.0;
}

/// <summary>An in-progress journey along one route edge.</summary>
public sealed class TravelState
{
    public string FromId { get; set; } = "";
    public string ToId { get; set; } = "";
    public int TotalDays { get; set; }
    public int DaysRemaining { get; set; }
    public double KmPerDay { get; set; }
    public double FuelPerDay { get; set; }
}

/// <summary>The player's convoy: what it hauls with, where it is, and what it carries.</summary>
public sealed class CaravanState
{
    public List<string> TruckTypeIds { get; set; } = new();

    /// <summary>Current city, or null while on the road.</summary>
    public string? LocationId { get; set; }

    public TravelState? Travel { get; set; }

    public Dictionary<string, CargoLot> Cargo { get; set; } = new();

    public bool IsTraveling => Travel is not null;

    public int Held(string goodId)
        => Cargo.TryGetValue(goodId, out var lot) ? lot.Units : 0;
}

/// <summary>
/// The complete mutable game state. Everything needed to resume a game lives here and
/// nowhere else, which is what makes save/load and deterministic replay straightforward.
/// </summary>
public sealed class GameState
{
    public int Day { get; set; }
    public ulong Seed { get; set; }
    public ulong RngState { get; set; }
    public long Cash { get; set; }
    public bool Bankrupt { get; set; }

    /// <summary>cityId to goodId to current stock level.</summary>
    public Dictionary<string, Dictionary<string, double>> Stock { get; set; } = new();

    public CaravanState Caravan { get; set; } = new();

    public double StockOf(string cityId, string goodId)
        => Stock.TryGetValue(cityId, out var market) && market.TryGetValue(goodId, out var s) ? s : 0.0;

    public void SetStock(string cityId, string goodId, double value)
    {
        if (!Stock.TryGetValue(cityId, out var market))
            Stock[cityId] = market = new Dictionary<string, double>();
        market[goodId] = value;
    }
}
