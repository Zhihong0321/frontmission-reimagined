using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>One line on a contract: this good, this many, at least this grade.</summary>
public sealed record ContractLine(string GoodId, int Units);

/// <summary>
/// One offer on a city's contract board, fully resolved. Reward and standing are the
/// terms; the deadline is what an acceptance on a given day would carry.
/// </summary>
public sealed record ContractOffer(
    string Id,
    string CityId,
    string KindId,
    string KindName,
    string Blurb,
    IReadOnlyList<ContractLine> Lines,
    double MinGrade,
    long Reward,
    double Standing,
    int Round,
    int DeadlineDays);

/// <summary>
/// The contract board every city keeps.
///
/// Offers are a pure function of (world seed, city, round) and are never stored: the
/// view derives them to draw the board and the command processor derives the same
/// list again to validate an acceptance or a delivery, so the two can never disagree.
/// That is also why this never touches <see cref="GameState.RngState"/> — building a
/// page must not advance the world.
///
/// A city only asks for what it does not make, so a contract is always a haul from
/// somewhere else. Reward anchors on the city's resting mid price (content), so a
/// contract accepted on day 3 pays the same as one accepted on day 300.
/// </summary>
public static class Contracts
{
    public static int RoundFor(int day, ContractsConfig cfg)
        => Math.Max(0, day - 1) / Math.Max(1, cfg.RefreshDays);

    public static int DaysUntilRefresh(int day, ContractsConfig cfg)
    {
        var refresh = Math.Max(1, cfg.RefreshDays);
        return refresh - Math.Max(0, day - 1) % refresh;
    }

    public static IReadOnlyList<ContractOffer> BoardFor(WorldData world, City city, ulong seed, int day)
        => BoardForRound(world, city, seed, RoundFor(day, world.Contracts));

    public static IReadOnlyList<ContractOffer> BoardForRound(WorldData world, City city, ulong seed, int round)
    {
        var cfg = world.Contracts;
        var offers = new List<ContractOffer>(cfg.OffersPerCity);
        if (cfg.Kinds.Count == 0) return offers;

        var wanted = Wanted(world, city);
        if (wanted.Count == 0) return offers;

        for (var index = 0; index < cfg.OffersPerCity; index++)
        {
            var offer = Generate(world, city, wanted, seed, round, index);
            if (offer is not null) offers.Add(offer);
        }

        return offers;
    }

    /// <summary>Resolve an offer from its id, or null if no board ever carried it.</summary>
    public static ContractOffer? Resolve(WorldData world, ulong seed, string id)
    {
        // id = "{cityId}-c{round}-{index}"
        var marker = id.LastIndexOf("-c", StringComparison.Ordinal);
        if (marker <= 0) return null;
        var cityId = id[..marker];
        var rest = id[(marker + 2)..];
        var dash = rest.IndexOf('-');
        if (dash <= 0) return null;
        if (!int.TryParse(rest[..dash], out var round) || !int.TryParse(rest[(dash + 1)..], out var index)) return null;
        if (!world.CitiesById.TryGetValue(cityId, out var city)) return null;

        var wanted = Wanted(world, city);
        if (wanted.Count == 0) return null;
        return Generate(world, city, wanted, seed, round, index);
    }

    /// <summary>Goods the city does not make, weighted by tier. Content order, so the RNG walk is stable.</summary>
    private static List<(GoodDef Good, double Weight)> Wanted(WorldData world, City city)
    {
        var list = new List<(GoodDef, double)>();
        foreach (var good in world.Goods)
        {
            if (!city.Market.TryGetValue(good.Id, out var profile)) continue;
            if (profile.Production > 1e-9) continue;
            var weight = world.Contracts.TierWeights.TryGetValue(good.Tier.ToString(), out var w) ? w : 1.0;
            if (weight <= 0) continue;
            list.Add((good, weight));
        }
        return list;
    }

    private static ContractOffer? Generate(
        WorldData world, City city, List<(GoodDef Good, double Weight)> wanted, ulong seed, int round, int index)
    {
        var cfg = world.Contracts;
        var eco = world.Config.Economy;
        var rng = new Rng(Hash(seed, city.Id, round, index));

        var kind = PickKind(cfg, rng);
        var goodsWanted = Math.Clamp(kind.Goods, 1, wanted.Count);

        var lines = new List<ContractLine>(goodsWanted);
        var taken = new HashSet<string>();
        for (var i = 0; i < goodsWanted; i++)
        {
            var good = PickGood(wanted, taken, rng);
            if (good is null) break;
            taken.Add(good.Id);
            var units = kind.UnitsMin + rng.NextInt(Math.Max(1, kind.UnitsMax - kind.UnitsMin + 1));
            lines.Add(new ContractLine(good.Id, units));
        }
        if (lines.Count == 0) return null;

        double value = 0;
        foreach (var line in lines)
        {
            var good = world.Good(line.GoodId);
            var profile = city.Market[good.Id];
            var mid = Economy.UnitPrice(good, profile, Economy.InitialStock(profile, eco), eco);
            value += mid * line.Units;
        }

        var reward = kind.RewardMult > 0 ? value * kind.RewardMult : value * kind.PriceMult;
        var deadline = cfg.DeadlineDaysMin + rng.NextInt(Math.Max(1, cfg.DeadlineDaysMax - cfg.DeadlineDaysMin + 1));

        return new ContractOffer(
            Id: $"{city.Id}-c{round}-{index}",
            CityId: city.Id,
            KindId: kind.Id,
            KindName: kind.Name,
            Blurb: kind.Blurb.Replace("{grade}", kind.MinGrade.ToString("0")),
            Lines: lines,
            MinGrade: kind.MinGrade,
            Reward: (long)Math.Round(reward),
            Standing: kind.Standing,
            Round: round,
            DeadlineDays: deadline);
    }

    private static ContractKindDef PickKind(ContractsConfig cfg, Rng rng)
    {
        double total = 0;
        foreach (var kind in cfg.Kinds) total += kind.Weight;
        var roll = rng.NextDouble() * total;
        foreach (var kind in cfg.Kinds)
        {
            roll -= kind.Weight;
            if (roll < 0) return kind;
        }
        return cfg.Kinds[^1];
    }

    private static GoodDef? PickGood(List<(GoodDef Good, double Weight)> wanted, HashSet<string> taken, Rng rng)
    {
        double total = 0;
        foreach (var (good, weight) in wanted)
        {
            if (!taken.Contains(good.Id)) total += weight;
        }
        if (total <= 0) return null;

        var roll = rng.NextDouble() * total;
        foreach (var (good, weight) in wanted)
        {
            if (taken.Contains(good.Id)) continue;
            roll -= weight;
            if (roll < 0) return good;
        }
        return null;
    }

    /// <summary>
    /// Whether the hold can settle this offer right now, and if not, why. Every line
    /// must be aboard in full at or above the grade asked.
    /// </summary>
    public static string? DeliveryBlocker(GameState state, WorldData world, ContractOffer offer)
    {
        foreach (var line in offer.Lines)
        {
            var good = world.Good(line.GoodId);
            if (!state.Caravan.Cargo.TryGetValue(line.GoodId, out var lot) || lot.Units < line.Units)
                return $"Only {state.Caravan.Held(line.GoodId):N0} of {line.Units:N0} {good.Name} in the hold.";
            if (offer.MinGrade > 0 && lot.Quality + 1e-9 < offer.MinGrade)
                return $"{good.Name} in the hold grades {lot.Quality:0}%; the contract wants {offer.MinGrade:0}%.";
        }
        return null;
    }

    /// <summary>FNV-1a over the city id, folded with the seed, round and index.</summary>
    private static ulong Hash(ulong seed, string cityId, int round, int index)
    {
        var hash = 0xCBF29CE484222325UL;
        foreach (var c in cityId)
        {
            hash ^= c;
            hash *= 0x100000001B3UL;
        }
        hash ^= seed + 0x7F4A7C159E3779B9UL;
        hash *= 0x100000001B3UL;
        hash ^= (ulong)round * 0xD1B54A32D192ED03UL;
        hash *= 0x100000001B3UL;
        hash ^= (ulong)index * 0xA24BAED4963EE407UL;
        hash *= 0x100000001B3UL;
        hash ^= 0x5C0A7C3E1D2B4F97UL;
        return hash == 0 ? 0x9E3779B97F4A7C15UL : hash;
    }
}
