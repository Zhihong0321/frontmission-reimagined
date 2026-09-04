namespace MechaTrader.Core.Model;

/// <summary>
/// One world event template. Content, loaded from events.json. Live instances are
/// carried in <c>GameState</c>; price and vital effects are derived from the active
/// set, so they vanish when the event ends. A stock shock is the exception: it writes
/// the shelf once, because goods do not teleport back.
/// </summary>
public sealed class EventDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Dispatch tag for the city wire: city, market, supply. Content, not CSS.</summary>
    public string Kind { get; init; } = "city";

    public string Headline { get; init; } = "";
    public string Detail { get; init; } = "";
    public string Tone { get; init; } = "warn";
    public int DurationDays { get; init; } = 7;
    public double Weight { get; init; } = 1;

    /// <summary>When true, the event applies to every city rather than one pick.</summary>
    public bool Global { get; init; }

    public List<string> Industries { get; init; } = new();
    public List<string> Regions { get; init; } = new();
    public List<string> Cities { get; init; } = new();
    public List<string> Goods { get; init; } = new();

    /// <summary>Whole categories this event covers, in addition to any named goods.</summary>
    public List<string> Categories { get; init; } = new();

    /// <summary>
    /// Citizen standing a convoy earns per <see cref="ReliefUnits"/> of a covered good it
    /// sells into the afflicted city while the event runs. Zero means this is not a shortage.
    /// </summary>
    public double ReliefStanding { get; init; }

    public double ReliefUnits { get; init; } = 40;

    /// <summary>1 means this event does not touch the price.</summary>
    public double PriceMult { get; init; } = 1.0;

    /// <summary>vitalId to a temporary delta, overlaid while the event is active.</summary>
    public Dictionary<string, double> VitalDeltas { get; init; } = new();

    /// <summary>1 means this event does not shock the shelf. Applied once, on fire.</summary>
    public double StockMult { get; init; } = 1.0;

    /// <summary>Units added to (or drained from) the shelf on fire. Zero means none.</summary>
    public double StockDelta { get; init; }

    /// <summary>When true, the stock shock hits intake instead of the shelf.</summary>
    public bool ShockIntake { get; init; }

    public bool TouchesPrice => Math.Abs(PriceMult - 1.0) > 1e-9;
    public bool TouchesStock => Math.Abs(StockMult - 1.0) > 1e-9 || Math.Abs(StockDelta) > 1e-9;
    public bool TouchesVitals => VitalDeltas.Count > 0;
    public bool IsShortage => ReliefStanding > 0;

    /// <summary>True when the template names a good or a category rather than covering every good.</summary>
    public bool NamesGoods => Goods.Count > 0 || Categories.Count > 0;
}
