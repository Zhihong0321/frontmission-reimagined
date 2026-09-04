namespace MechaTrader.Core.Model;

/// <summary>
/// One paper the governor will sign once standing is high enough. Holding it is the
/// grant; actually putting up a shop or a factory is a later act.
/// </summary>
public sealed class PermitDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";

    /// <summary>Standing at which this permit is granted. Sticky once earned.</summary>
    public double Standing { get; init; }
}

/// <summary>
/// One way to raise standing with a city. Cost and effects are content, so adding a
/// fourth gesture is a JSON line rather than a new command.
/// </summary>
public sealed class FavorActionDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";
    public long Cost { get; init; }
    public double Standing { get; init; }

    /// <summary>Relationship segment the standing lands in. Empty means the first segment content declares.</summary>
    public string SegmentId { get; init; } = "";

    /// <summary>Vital this action moves, or empty if it only buys goodwill.</summary>
    public string VitalId { get; init; } = "";
    public double VitalDelta { get; init; }

    /// <summary>
    /// Units added to the intake of each good in the city's shortest supply. Zero means
    /// this action does not ship anything. Intake, not shelf: aid must not cheapen a buy.
    /// </summary>
    public double StockPerGood { get; init; }
}

/// <summary>One slice of a city's regard for the player: the office, the streets, the houses.</summary>
public sealed class StandingSegmentDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";
}

/// <summary>How the player relates to a city. Loaded from standing.json.</summary>
public sealed class StandingConfig
{
    /// <summary>Ceiling on the total: every segment summed.</summary>
    public double Max { get; init; } = 100;

    /// <summary>Ceiling on any one segment.</summary>
    public double SegmentMax { get; init; } = 100;

    /// <summary>Fraction of the shelf reserved for the player, per point of total standing.</summary>
    public double ReservePerPoint { get; init; }

    /// <summary>Cap on that fraction, so a patron cannot lock the whole market.</summary>
    public double ReserveMax { get; init; } = 0.4;

    /// <summary>Traders standing earned per thousand credits sold into a city.</summary>
    public double TradersPerThousandCr { get; init; }

    /// <summary>Traders standing lost when an accepted contract runs past its deadline.</summary>
    public double ContractLapsePenalty { get; init; }

    public List<StandingSegmentDef> Segments { get; init; } = new();

    public List<StatBandDef> Ranks { get; init; } = new();
    public List<PermitDef> Permits { get; init; } = new();
    public List<FavorActionDef> Actions { get; init; } = new();

    public FavorActionDef? Action(string id)
    {
        foreach (var action in Actions)
        {
            if (string.Equals(action.Id, id, StringComparison.OrdinalIgnoreCase)) return action;
        }
        return null;
    }

    public bool HasSegment(string id)
    {
        foreach (var segment in Segments)
        {
            if (segment.Id == id) return true;
        }
        return false;
    }

    /// <summary>The segment an action or grant lands in when it names none: the first declared.</summary>
    public string DefaultSegmentId => Segments.Count > 0 ? Segments[0].Id : "governor";

    /// <summary>The segment id content uses for a purpose, falling back to the default when it is absent.</summary>
    public string SegmentOr(string preferred)
        => HasSegment(preferred) ? preferred : DefaultSegmentId;
}
