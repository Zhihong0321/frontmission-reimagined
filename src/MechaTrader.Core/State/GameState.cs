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

    /// <summary>Everyone on the payroll. Travels with the convoy; hired and paid off in cities.</summary>
    public List<CrewMember> Crew { get; set; } = new();

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

    /// <summary>cityId to goodId to what the city holds, shelf and intake.</summary>
    public Dictionary<string, Dictionary<string, CityStock>> Stock { get; set; } = new();

    public CaravanState Caravan { get; set; } = new();

    /// <summary>
    /// Candidate ids already taken out of the market. Recruitment pools are re-derived
    /// from the seed rather than stored, so this is the only record that someone was
    /// hired; it also stops a dismissed hand reappearing in the same pool.
    /// </summary>
    public HashSet<string> RecruitedIds { get; set; } = new();

    public CityStock StockOf(string cityId, string goodId)
        => Stock.TryGetValue(cityId, out var market) && market.TryGetValue(goodId, out var s)
            ? s
            : default;

    /// <summary>Everything the city owns of a good. This is what the sell price reads.</summary>
    public double TotalStockOf(string cityId, string goodId) => StockOf(cityId, goodId).Total;

    /// <summary>What is on the shelf, and so all a convoy can buy and all the buy price reads.</summary>
    public double ShelfOf(string cityId, string goodId) => StockOf(cityId, goodId).Out;

    public void SetStock(string cityId, string goodId, CityStock value)
    {
        if (!Stock.TryGetValue(cityId, out var market))
            Stock[cityId] = market = new Dictionary<string, CityStock>();
        market[goodId] = value;
    }
}
