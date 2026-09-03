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

/// <summary>Road type on a route edge. Content, loaded from terrain.json.</summary>
public sealed class TerrainDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double SpeedMultiplier { get; init; } = 1.0;
    public double CostMultiplier { get; init; } = 1.0;
}

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

/// <summary>A portable tool. Content, loaded from gear.json. Occupies hold volume.</summary>
public sealed class GearDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public long Price { get; init; }
    public double Volume { get; init; }
    public List<string> Capabilities { get; init; } = new();
    public double MineYield { get; init; }

    public bool HasCapability(string capability)
    {
        foreach (var cap in Capabilities)
        {
            if (string.Equals(cap, capability, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

/// <summary>
/// An industry archetype. A city's whole market is generated by summing the
/// archetypes it declares, so adding a city is a few lines of data, not a table.
/// </summary>
public sealed class IndustryDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public Dictionary<string, double> Production { get; init; } = new();
    public Dictionary<string, double> Consumption { get; init; } = new();
}

public sealed class EconomyConfig
{
    /// <summary>Pull toward equilibrium per day, representing trade with the outside world.</summary>
    public double DriftRate { get; init; } = 0.25;

    /// <summary>Equilibrium stock = this many days of total throughput.</summary>
    public double EquilibriumDays { get; init; } = 10.0;

    public double MinEquilibrium { get; init; } = 40.0;
    public double MinStock { get; init; } = 5.0;
    public double NoiseSigma { get; init; } = 0.02;

    /// <summary>Buy/sell margin taken by the market, so you cannot round-trip in place.</summary>
    public double Spread { get; init; } = 0.06;

    public double MinPriceMult { get; init; } = 0.4;
    public double MaxPriceMult { get; init; } = 2.5;

    /// <summary>Roads are not straight lines; scales great-circle distance up.</summary>
    public double RoadDetourFactor { get; init; } = 1.25;

    /// <summary>
    /// Share of a city's intake that reaches its shelf each day. Lower means goods sold
    /// into a city take longer to come back onto the market.
    /// </summary>
    public double RestockRate { get; init; } = 0.35;
}

public sealed class GameConfig
{
    public long StartCash { get; init; } = 20000;
    public string StartCityId { get; init; } = "";
    public List<string> StartTruckIds { get; init; } = new();
    public EconomyConfig Economy { get; init; } = new();
    public WarehouseConfig Warehouse { get; init; } = new();
    public CrewBriefConfig CrewBrief { get; init; } = new();
}

/// <summary>
/// The crew's quick market brief when the convoy parks in a city: which goods in the
/// hold would clear a worthwhile margin if sold here, biggest margin first.
///
/// The toggle is content so it can later bind to a crew passive skill; the first cut
/// ships with it simply on.
/// </summary>
public sealed class CrewBriefConfig
{
    /// <summary>Master switch. True for the first cut; a crew passive skill will gate it later.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Floor on the margin over cost basis, as a fraction. 0.035 = 3.5%, meant to cover fuel.</summary>
    public double MinMargin { get; init; } = 0.035;
}

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

/// <summary>The catalogue of world events. Loaded from events.json.</summary>
public sealed class EventsConfig
{
    public int MaxConcurrent { get; init; } = 3;
    public double DailyChance { get; init; } = 0.2;
    public List<EventDef> Events { get; init; } = new();

    public EventDef? ById(string id)
    {
        foreach (var evt in Events)
        {
            if (evt.Id == id) return evt;
        }
        return null;
    }
}
