namespace MechaTrader.Core.Model;

/// <summary>
/// A named slice of a stat's range, so a bare number can say what it means. Bands are
/// declared in ascending order and the last one carries no <see cref="UpTo"/>, which is
/// what makes it the catch-all at the top of the range.
/// </summary>
public sealed class StatBandDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Exclusive upper bound. Null means this band runs to the top.</summary>
    public double? UpTo { get; init; }

    /// <summary>How the band should read: bad, warn, ok, good or muted. Content, not CSS.</summary>
    public string Tone { get; init; } = "muted";
}

/// <summary>
/// One authored city stat. The founding value is written per city in cities.json; the
/// live one is carried in <c>GameState</c>, so an event can move it without touching
/// content. Everything about how it reads - unit, precision, the scale factor between
/// the simulation's number and the displayed one - is content, so adding a stat is a
/// data change.
/// </summary>
public sealed class CityVitalDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Unit { get; init; } = "";
    public string Blurb { get; init; } = "";

    /// <summary>Used when a city does not author this stat.</summary>
    public double Default { get; init; }

    public double Min { get; init; }
    public double Max { get; init; } = 100;
    public int Decimals { get; init; }

    /// <summary>
    /// The raw value is multiplied by this before display, so the simulation can hold
    /// population as an industry scale while the player reads it in millions.
    /// </summary>
    public double DisplayScale { get; init; } = 1.0;

    public List<StatBandDef> Bands { get; init; } = new();

    /// <summary>A stat allowed to go negative is shown signed, so "+2.4" reads as a direction.</summary>
    public bool Signed => Min < 0;
}

/// <summary>
/// A slice of a city's own market, read as one supply figure. Nothing about it is
/// authored per city: it is derived from what the city makes, eats and currently holds,
/// which is what lets it move on its own every day.
/// </summary>
public sealed class CitySupplyDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";
    public List<string> Goods { get; init; } = new();
}

/// <summary>The catalogue of city stats. Loaded from citystats.json.</summary>
public sealed class CityStatsConfig
{
    /// <summary>
    /// The vital that scales a city's industry. Named here rather than hardcoded so the
    /// loader has one place to look for the number it feeds into market generation.
    /// </summary>
    public string PopulationVitalId { get; init; } = "population";

    public List<CityVitalDef> Vitals { get; init; } = new();
    public List<CitySupplyDef> Supplies { get; init; } = new();

    /// <summary>Shared by every supply figure; they are all read on the same 100 = nominal scale.</summary>
    public List<StatBandDef> SupplyBands { get; init; } = new();

    public CityVitalDef? Vital(string id)
    {
        foreach (var vital in Vitals)
        {
            if (vital.Id == id) return vital;
        }
        return null;
    }
}
