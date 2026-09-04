namespace MechaTrader.Core.Model;

/// <summary>A knowledge domain crew specialise in. Content, loaded from goods.json.</summary>
public sealed class CategoryDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";
}

/// <summary>
/// How a shop's pile is graded, and what an S-tier crate is worth on the sell.
/// Knowledge never rewrites the pile — it only chooses which crates to take.
/// </summary>
public sealed class QualityConfig
{
    /// <summary>The grade that sells at exactly 1.0x. Below it a crate sells poorly; above it the multiplier rises toward S-tier.</summary>
    public double Nominal { get; init; } = 70;

    /// <summary>Floor of a freshly made crate's grade, before the random roll and the city's craft.</summary>
    public double Base { get; init; } = 50;

    /// <summary>Width of the uniform random roll added to <see cref="Base"/> on production.</summary>
    public double Random { get; init; } = 15;

    /// <summary>The city vital that lifts production grade. Empty means cities do not differ.</summary>
    public string CityVitalId { get; init; } = "";

    /// <summary>Grade added at a vital reading of 100; scales linearly below it.</summary>
    public double CityVitalWeight { get; init; } = 0;

    /// <summary>Half-range of the uniform pile around the average, so 70 ± 22 is 48–92.</summary>
    public double Spread { get; init; } = 22;

    /// <summary>Selected quality at or above this grades S-tier.</summary>
    public double STierAt { get; init; } = 90;

    /// <summary>Sell-price multiplier bonus at S-tier (0.30 = +30%).</summary>
    public double STierSellBonus { get; init; } = 0.30;
}

/// <summary>A rented storeroom. Tuning lives on <see cref="GameConfig"/>; the rooms themselves are state.</summary>
public sealed class WarehouseConfig
{
    /// <summary>One-time fee to rent a storeroom in a city.</summary>
    public long RentCost { get; init; } = 800;

    /// <summary>Charged every day the room is held, whether the convoy is there or not.</summary>
    public long DailyRent { get; init; } = 35;

    /// <summary>Hold volume the room can keep.</summary>
    public double Capacity { get; init; } = 400;
}

/// <summary>A tradeable commodity. Content, loaded from goods.json.</summary>
public sealed class GoodDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Knowledge domain this good belongs to. Empty means uncategorised.</summary>
    public string Category { get; init; } = "";

    /// <summary>Product grade, 1 (common) to 5 (masterwork). Declared in goods.json under tiers.</summary>
    public int Tier { get; init; } = 1;
    public double BasePrice { get; init; }
    public double UnitVolume { get; init; } = 1.0;

    /// <summary>How hard price reacts to scarcity. Higher = more volatile.</summary>
    public double Elasticity { get; init; } = 0.6;

    /// <summary>Base price per unit of hold. The loader holds every tier to a rising floor on this.</summary>
    public double PricePerVolume => UnitVolume > 0 ? BasePrice / UnitVolume : double.PositiveInfinity;
}

/// <summary>
/// One product grade. Content, loaded from goods.json. The colour is a display hint the
/// front-end may use; the standing threshold is a rule the buy command enforces.
/// </summary>
public sealed class TierDef
{
    public int Tier { get; init; }
    public string Name { get; init; } = "";

    /// <summary>Display colour for names of this grade. Content, not CSS.</summary>
    public string Color { get; init; } = "";

    /// <summary>Total standing (every segment summed) a city demands before it sells this grade to you.</summary>
    public double MinStanding { get; init; }

    /// <summary>Every good of this tier must have basePrice / unitVolume at or above this, and below the next tier's.</summary>
    public double MinPricePerVolume { get; init; }

    /// <summary>Multiplies the economy's minimum equilibrium, so rare goods do not rest in piles of 150.</summary>
    public double EquilibriumScale { get; init; } = 1.0;
}
