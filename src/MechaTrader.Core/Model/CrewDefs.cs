namespace MechaTrader.Core.Model;

/// <summary>
/// The levers a crew skill is allowed to pull. A skill declares one of these in
/// content; the simulation reads the lever rather than the skill id, so renaming or
/// retuning a skill is a data change and adding one that does nothing yet is legal.
/// </summary>
public static class CrewLever
{
    public const string Speed = "speed";
    public const string Buy = "buy";
    public const string Sell = "sell";
    public const string Upkeep = "upkeep";

    /// <summary>Price intelligence: how far and how accurately a hand reads other markets.</summary>
    public const string Intel = "intel";

    public const string None = "none";

    public static readonly IReadOnlyList<string> All = new[] { Speed, Buy, Sell, Upkeep, Intel, None };
}

/// <summary>
/// One crew ability. <see cref="MaxEffect"/> is what the lever gives at <c>maxSkill</c>
/// and scales linearly below it; its meaning depends on the lever:
/// speed = fractional speed bonus, buy/sell = share of the market spread erased,
/// upkeep = share of running costs cut.
/// </summary>
public sealed class CrewSkillDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Lever { get; init; } = CrewLever.None;
    public double MaxEffect { get; init; }
    public string Blurb { get; init; } = "";
}

/// <summary>A hiring archetype. <see cref="Primary"/> is the skill it rolls high in.</summary>
public sealed class CrewRoleDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Skill id this role specialises in; empty means a generalist.</summary>
    public string Primary { get; init; } = "";

    /// <summary>Category this role is steeped in; empty means no category spike.</summary>
    public string CategoryId { get; init; } = "";

    /// <summary>
    /// Post a hand of this role takes on signing. Empty means "the post that claims the
    /// primary skill's lever, if any"; a role can name one explicitly to override that.
    /// </summary>
    public string Post { get; init; } = "";
}

/// <summary>
/// A job aboard the convoy that somebody has to be put on. A post claims levers: while
/// a lever is claimed, only hands on that post pull it, so a broker riding in the back
/// haggles for nobody. A lever no post claims is convoy-wide, the way it always was.
/// </summary>
public sealed class CrewPostDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";
    public List<string> Levers { get; init; } = new();
}

/// <summary>
/// What the information post delivers: price reports from the nearest markets. Reach
/// runs from <see cref="MinCities"/> to <see cref="MaxCities"/> with the informant's
/// intelligence, and every reported price is off by up to <see cref="MaxError"/> at
/// level zero, shrinking to nothing at max skill.
/// </summary>
public sealed class IntelConfig
{
    public int MinCities { get; init; } = 2;
    public int MaxCities { get; init; } = 8;
    public double MaxError { get; init; } = 0.4;
}

/// <summary>
/// A special trait a hand can carry. Kinds: product (a category, or any), traveling,
/// repair, bargain. Effects are content; the simulation reads the numbers, not the id.
/// </summary>
public sealed class CrewTraitDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Blurb { get; init; } = "";

    /// <summary>Product traits may name a category; empty means the bonus applies everywhere.</summary>
    public string CategoryId { get; init; } = "";

    public double KnowledgeBonus { get; init; }
    public double QualityBonus { get; init; }
    public double SpeedBonus { get; init; }
    public double UpkeepCut { get; init; }
    public double BuyBargain { get; init; }
    public double SellBargain { get; init; }
    public int WagePoints { get; init; }
}

public static class TraitKind
{
    public const string Product = "product";
    public const string Traveling = "traveling";
    public const string Repair = "repair";
    public const string Bargain = "bargain";

    public static readonly IReadOnlyList<string> All = new[] { Product, Traveling, Repair, Bargain };
}

public sealed class CrewWageDef
{
    public long Base { get; init; } = 5;
    public long PerSkillPoint { get; init; } = 6;

    /// <summary>Wage per ten points of category knowledge the hand carries.</summary>
    public long PerKnowledgeTen { get; init; } = 2;
}

/// <summary>Shape of a city's recruitment pool.</summary>
public sealed class CandidateGenDef
{
    public int BasePerCity { get; init; } = 1;
    public double PerPopulation { get; init; } = 2.0;
    public int MaxPerCity { get; init; } = 5;
    public int PrimaryMin { get; init; } = 5;
    public int PrimaryMax { get; init; } = 10;
    public int SecondaryMin { get; init; } = 1;
    public int SecondaryMax { get; init; } = 5;
}

/// <summary>Everything about crew that is content. Loaded from crew.json.</summary>
public sealed class CrewConfig
{
    public int MaxSkill { get; init; } = 10;

    /// <summary>Category knowledge cap, 0–this. Displayed as a whole number.</summary>
    public int MaxKnowledge { get; init; } = 100;

    /// <summary>How many people the convoy can carry on the books at once.</summary>
    public int CrewCapacity { get; init; } = 4;

    /// <summary>Knowledge XP granted to each hand on a buy or sell, scaled by log of units.</summary>
    public double TradeKnowledgeXp { get; init; } = 0.45;

    /// <summary>Skill XP granted to the related lever (negotiation on buy, sales on sell).</summary>
    public double TradeSkillXp { get; init; } = 0.10;

    /// <summary>How much of the market spread category knowledge can still erode, at max knowledge.</summary>
    public double KnowledgeBargain { get; init; } = 0.22;

    /// <summary>Chance a candidate walks in with one special trait.</summary>
    public double TraitChance { get; init; } = 0.55;

    public int SpecialistKnowledgeMin { get; init; } = 32;
    public int SpecialistKnowledgeMax { get; init; } = 68;
    public int GeneralKnowledgeMin { get; init; } = 4;
    public int GeneralKnowledgeMax { get; init; } = 22;

    /// <summary>A city's recruitment pool re-rolls this often.</summary>
    public int RefreshDays { get; init; } = 10;

    /// <summary>Signing fee, expressed as a multiple of the daily wage.</summary>
    public int SigningFeeDays { get; init; } = 20;

    /// <summary>Severance on dismissal, expressed as a multiple of the daily wage.</summary>
    public int SeveranceDays { get; init; } = 5;

    public CrewWageDef Wage { get; init; } = new();
    public CandidateGenDef Candidates { get; init; } = new();

    public List<CrewSkillDef> Skills { get; init; } = new();
    public List<CrewRoleDef> Roles { get; init; } = new();
    public List<CrewTraitDef> Traits { get; init; } = new();
    public List<CrewPostDef> Posts { get; init; } = new();
    public IntelConfig Intel { get; init; } = new();

    /// <summary>industryId to skillId to bonus points, applied to that city's candidates.</summary>
    public Dictionary<string, Dictionary<string, int>> IndustryAffinity { get; init; } = new();

    public List<string> FirstNames { get; init; } = new();
    public List<string> Surnames { get; init; } = new();

    /// <summary>The skill wired to a given lever, or null if content declares none.</summary>
    public CrewSkillDef? SkillFor(string lever)
    {
        foreach (var skill in Skills)
        {
            if (skill.Lever == lever) return skill;
        }
        return null;
    }

    public CrewTraitDef? Trait(string id)
    {
        foreach (var trait in Traits)
        {
            if (trait.Id == id) return trait;
        }
        return null;
    }

    public CrewPostDef? Post(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        foreach (var post in Posts)
        {
            if (post.Id == id) return post;
        }
        return null;
    }

    /// <summary>The post that claims a lever, or null if the lever is convoy-wide.</summary>
    public CrewPostDef? PostFor(string lever)
    {
        foreach (var post in Posts)
        {
            if (post.Levers.Contains(lever)) return post;
        }
        return null;
    }

    public CrewRoleDef? Role(string id)
    {
        foreach (var role in Roles)
        {
            if (role.Id == id) return role;
        }
        return null;
    }

    /// <summary>
    /// The post a hand of this role takes on signing: the role's own choice, else the
    /// post that claims the lever of its primary skill, else none.
    /// </summary>
    public string DefaultPost(CrewRoleDef? role)
    {
        if (role is null) return "";
        if (!string.IsNullOrWhiteSpace(role.Post)) return role.Post;
        if (string.IsNullOrWhiteSpace(role.Primary)) return "";

        foreach (var skill in Skills)
        {
            if (skill.Id == role.Primary) return PostFor(skill.Lever)?.Id ?? "";
        }
        return "";
    }
}
