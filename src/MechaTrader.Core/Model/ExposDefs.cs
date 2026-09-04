namespace MechaTrader.Core.Model;

/// <summary>One expo theme. Content, loaded from expos.json.</summary>
public sealed class ExpoThemeDef
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public List<string> Categories { get; init; } = new();
    public int DurationDays { get; init; } = 5;
    public double Weight { get; init; } = 1;
}

/// <summary>How trade expos run. Loaded from expos.json.</summary>
public sealed class ExposConfig
{
    public int CycleDays { get; init; } = 24;
    public long FeeBase { get; init; } = 600;
    public long FeePerPop { get; init; } = 400;
    public double BuyersBase { get; init; } = 4;
    public double BuyersPerPop { get; init; } = 5;

    /// <summary>Buff at the narrowest theme (two categories).</summary>
    public double BuffMax { get; init; } = 0.6;

    /// <summary>Buff at the broadest theme (five categories).</summary>
    public double BuffMin { get; init; } = 0.15;

    /// <summary>Premium over base price a buyer will pay, per point of buff.</summary>
    public double PremiumMult { get; init; } = 0.3;

    /// <summary>Half-width of the uniform noise on a buyer's willingness.</summary>
    public double Noise { get; init; } = 0.15;

    /// <summary>An ask within this fraction above willingness reads as "close" rather than "too dear".</summary>
    public double CloseBand { get; init; } = 0.2;

    /// <summary>Largest lot one buyer takes.</summary>
    public int LotMax { get; init; } = 10;

    public List<ExpoThemeDef> Themes { get; init; } = new();

    /// <summary>Outcome id to the lines a buyer may say. Content, so the animation never invents copy.</summary>
    public Dictionary<string, List<string>> Remarks { get; init; } = new();
}
