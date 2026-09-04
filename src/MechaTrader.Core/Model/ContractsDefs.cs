namespace MechaTrader.Core.Model;

/// <summary>One shape a city's contract board can offer. Content, loaded from contracts.json.</summary>
public sealed class ContractKindDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";
    public double Weight { get; init; } = 1;

    /// <summary>Distinct goods on the list. One for quality and supply contracts.</summary>
    public int Goods { get; init; } = 1;

    public int UnitsMin { get; init; } = 10;
    public int UnitsMax { get; init; } = 40;

    /// <summary>Minimum lot grade, 0 to 100. Zero means any grade is accepted.</summary>
    public double MinGrade { get; init; }

    /// <summary>Reward as a multiple of resting mid price x units. Zero means <see cref="PriceMult"/> applies instead.</summary>
    public double RewardMult { get; init; }

    /// <summary>Per-unit price as a multiple of the resting mid price. Used when <see cref="RewardMult"/> is zero.</summary>
    public double PriceMult { get; init; }

    /// <summary>Traders standing paid on delivery.</summary>
    public double Standing { get; init; }
}

/// <summary>The contract board. Loaded from contracts.json.</summary>
public sealed class ContractsConfig
{
    public int RefreshDays { get; init; } = 12;
    public int OffersPerCity { get; init; } = 3;
    public int DeadlineDaysMin { get; init; } = 14;
    public int DeadlineDaysMax { get; init; } = 30;

    /// <summary>Tier number (as a string key) to how often that tier is asked for.</summary>
    public Dictionary<string, double> TierWeights { get; init; } = new();

    public List<ContractKindDef> Kinds { get; init; } = new();

    public ContractKindDef? Kind(string id)
    {
        foreach (var kind in Kinds)
        {
            if (kind.Id == id) return kind;
        }
        return null;
    }
}
