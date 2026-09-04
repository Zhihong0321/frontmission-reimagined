using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>One expo as it falls on a city's calendar: which theme, which days.</summary>
public sealed record ExpoInstance(
    string CityId,
    int Round,
    ExpoThemeDef Theme,
    int StartDay,
    int EndDay)
{
    public string PassId => $"{CityId}:{Round}";
    public bool RunsOn(int day) => day >= StartDay && day < EndDay;
}

/// <summary>
/// Trade expos: every city's own fair, on its own calendar.
///
/// The schedule is a pure function of (world seed, city, round) and is never stored,
/// the same way a recruitment pool is. What is state: the pass the convoy bought, the
/// asking prices on its stall, and the report of the last day's visits. The stall
/// itself trades on the day tick, because buyers walk the hall while the day passes;
/// that is the one write here and it draws the day's RNG.
///
/// Buyers come from across the map, so their willingness anchors on a good's base
/// price rather than the local shelf. A city's own produce is never allowed on a stall
/// in its own expo — that is the guard against buying off the shelf and selling it back
/// across the hall.
/// </summary>
public static class Expos
{
    /// <summary>The expo running in a city on a day, or null.</summary>
    public static ExpoInstance? Running(WorldData world, City city, ulong seed, int day)
    {
        var current = InstanceFor(world, city, seed, RoundFor(world, city, seed, day));
        return current is not null && current.RunsOn(day) ? current : null;
    }

    /// <summary>The next expo that has not yet ended, running or upcoming.</summary>
    public static ExpoInstance? Next(WorldData world, City city, ulong seed, int day)
    {
        var round = RoundFor(world, city, seed, day);
        for (var r = Math.Max(0, round); r < round + 3; r++)
        {
            var instance = InstanceFor(world, city, seed, r);
            if (instance is not null && instance.EndDay > day) return instance;
        }
        return null;
    }

    /// <summary>Which cycle a day falls in for this city (cities are staggered by a stable offset).</summary>
    public static int RoundFor(WorldData world, City city, ulong seed, int day)
    {
        var cycle = Math.Max(1, world.Expos.CycleDays);
        var shifted = day - 1 - Offset(world, city, seed);
        return shifted < 0 ? -1 : shifted / cycle;
    }

    public static ExpoInstance? InstanceFor(WorldData world, City city, ulong seed, int round)
    {
        var cfg = world.Expos;
        if (cfg.Themes.Count == 0 || round < 0) return null;

        var rng = new Rng(Hash(seed, city.Id, round));
        var theme = PickTheme(cfg, rng);
        var cycle = Math.Max(1, cfg.CycleDays);
        var start = round * cycle + Offset(world, city, seed) + 1;
        return new ExpoInstance(city.Id, round, theme, start, start + theme.DurationDays);
    }

    /// <summary>Buff for a theme: narrow themes buff hard, broad ones barely.</summary>
    public static double Buff(ExposConfig cfg, ExpoThemeDef theme)
    {
        var n = Math.Clamp(theme.Categories.Count, 2, 5);
        var t = (n - 2) / 3.0;
        return cfg.BuffMax + (cfg.BuffMin - cfg.BuffMax) * t;
    }

    public static long Fee(ExposConfig cfg, City city)
        => (long)Math.Round(cfg.FeeBase + cfg.FeePerPop * city.Population);

    /// <summary>How many buyers walk the hall in a day, before the roll.</summary>
    public static int BuyersPerDay(ExposConfig cfg, City city, double buff)
        => Math.Max(0, (int)Math.Round((cfg.BuyersBase + cfg.BuyersPerPop * city.Population) * (1.0 + buff)));

    /// <summary>The ask at which a typical buyer says yes, before noise: base price plus the premium.</summary>
    public static double TypicalWillingness(ExposConfig cfg, GoodDef good, double buff, double quality, QualityConfig qcfg)
        => good.BasePrice * (1.0 + buff * cfg.PremiumMult) * QualityMath.SellMultiplier(quality, qcfg);

    /// <summary>A city never lets its own produce onto a stall in its own expo.</summary>
    public static bool CityMakes(City city, string goodId)
        => city.Market.TryGetValue(goodId, out var profile) && profile.Production > 1e-9;

    public static bool ThemeCovers(ExpoThemeDef theme, GoodDef good)
    {
        foreach (var category in theme.Categories)
        {
            if (string.Equals(category, good.Category, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// One day at the stall. Only runs when the convoy is parked in a city whose expo is
    /// on, holds a pass for it, and has something listed. Draws the day's RNG.
    /// </summary>
    public static void Tick(GameState state, WorldData world, Rng rng, List<GameEvent> events)
    {
        var caravan = state.Caravan;
        if (caravan.Travel is not null || caravan.LocationId is null) return;
        if (caravan.ExpoAsks.Count == 0) return;

        var city = world.City(caravan.LocationId);
        var expo = Running(world, city, state.Seed, state.Day);
        if (expo is null || !state.ExpoPasses.Contains(expo.PassId)) return;

        var cfg = world.Expos;
        var buff = Buff(cfg, expo.Theme);
        var buyers = BuyersPerDay(cfg, city, buff);
        var report = new ExpoDayState { Day = state.Day, CityId = city.Id };

        // Listings in content order so the walk is stable whatever the dictionary did.
        var listed = new List<GoodDef>();
        foreach (var good in world.Goods)
        {
            if (!caravan.ExpoAsks.TryGetValue(good.Id, out var ask) || ask <= 0) continue;
            if (!ThemeCovers(expo.Theme, good)) continue;
            if (CityMakes(city, good.Id)) continue;
            listed.Add(good);
        }

        for (var i = 0; i < buyers; i++)
        {
            var visit = Visit(state, world, city, expo, buff, listed, rng, i);
            report.Visits.Add(visit);
            if (visit.Outcome != ExpoOutcome.Bought) continue;
            report.Revenue += visit.Price * visit.Units;
            report.UnitsSold += visit.Units;
        }

        state.LastExpoDay = report;

        if (report.UnitsSold > 0)
        {
            events.Add(new GameEvent(state.Day, GameEventKind.Trade,
                $"{expo.Theme.Title} stall in {city.Name}: {report.UnitsSold:N0} units sold to {report.Visits.Count(v => v.Outcome == ExpoOutcome.Bought)} buyers for {report.Revenue:N0} cr."));
        }
        else
        {
            events.Add(new GameEvent(state.Day, GameEventKind.Info,
                $"{expo.Theme.Title} stall in {city.Name}: {report.Visits.Count} buyers came by, nobody bought."));
        }
    }

    private static ExpoVisit Visit(
        GameState state, WorldData world, City city, ExpoInstance expo, double buff,
        List<GoodDef> listed, Rng rng, int sequence)
    {
        var cfg = world.Expos;
        var caravan = state.Caravan;
        var buyer = BuyerName(world, rng);

        // A buyer is here for one category of the theme; they only look at that.
        var category = expo.Theme.Categories[rng.NextInt(expo.Theme.Categories.Count)];
        var choices = listed.Where(g => string.Equals(g.Category, category, StringComparison.OrdinalIgnoreCase)
                                        && caravan.Held(g.Id) > 0).ToList();
        if (choices.Count == 0)
        {
            var anything = listed.Any(g => caravan.Held(g.Id) > 0);
            return new ExpoVisit
            {
                Sequence = sequence, Buyer = buyer, GoodId = "", Outcome = anything ? ExpoOutcome.Browse : ExpoOutcome.NoStall,
                Remark = Remark(cfg, anything ? ExpoOutcome.Browse : ExpoOutcome.NoStall, rng)
            };
        }

        var good = choices[rng.NextInt(choices.Count)];
        var lot = caravan.Cargo[good.Id];
        var ask = caravan.ExpoAsks[good.Id];
        var noise = 1.0 + rng.NextSigned() * cfg.Noise;
        var willing = TypicalWillingness(cfg, good, buff, lot.Quality, world.Quality) * noise;

        if (ask > willing)
        {
            var close = ask <= willing * (1.0 + cfg.CloseBand);
            var outcome = close ? ExpoOutcome.Close : ExpoOutcome.TooDear;
            return new ExpoVisit
            {
                Sequence = sequence, Buyer = buyer, GoodId = good.Id, Outcome = outcome, Price = ask,
                Remark = Remark(cfg, outcome, rng)
            };
        }

        var units = Math.Min(lot.Units, 1 + rng.NextInt(Math.Max(1, cfg.LotMax)));
        var total = ask * units;
        var costBasis = (long)Math.Round(lot.AverageCost * units);

        state.Cash += total;
        lot.Units -= units;
        lot.TotalCost = Math.Max(0, lot.TotalCost - costBasis);
        if (lot.Units == 0)
        {
            caravan.Cargo.Remove(good.Id);
            caravan.ExpoAsks.Remove(good.Id);
        }

        return new ExpoVisit
        {
            Sequence = sequence, Buyer = buyer, GoodId = good.Id, Outcome = ExpoOutcome.Bought,
            Units = units, Price = ask, Remark = Remark(cfg, ExpoOutcome.Bought, rng)
        };
    }

    private static string Remark(ExposConfig cfg, string outcome, Rng rng)
    {
        if (!cfg.Remarks.TryGetValue(outcome, out var lines) || lines.Count == 0) return "";
        return lines[rng.NextInt(lines.Count)];
    }

    private static string BuyerName(WorldData world, Rng rng)
    {
        var crew = world.Crew;
        if (crew.FirstNames.Count == 0 || crew.Surnames.Count == 0) return "a buyer";
        return $"{crew.FirstNames[rng.NextInt(crew.FirstNames.Count)]} {crew.Surnames[rng.NextInt(crew.Surnames.Count)]}";
    }

    private static ExpoThemeDef PickTheme(ExposConfig cfg, Rng rng)
    {
        double total = 0;
        foreach (var theme in cfg.Themes) total += theme.Weight;
        var roll = rng.NextDouble() * total;
        foreach (var theme in cfg.Themes)
        {
            roll -= theme.Weight;
            if (roll < 0) return theme;
        }
        return cfg.Themes[^1];
    }

    private static int Offset(WorldData world, City city, ulong seed)
        => (int)(Hash(seed, city.Id, -1) % (ulong)Math.Max(1, world.Expos.CycleDays));

    private static ulong Hash(ulong seed, string cityId, int round)
    {
        var hash = 0xCBF29CE484222325UL;
        foreach (var c in cityId)
        {
            hash ^= c;
            hash *= 0x100000001B3UL;
        }
        hash ^= seed + 0x3C6EF372FE94F82AUL;
        hash *= 0x100000001B3UL;
        hash ^= unchecked((ulong)round) * 0xD1B54A32D192ED03UL;
        hash *= 0x100000001B3UL;
        hash ^= 0x1B873593CC9E2D51UL;
        return hash == 0 ? 0x9E3779B97F4A7C15UL : hash;
    }
}

public static class ExpoOutcome
{
    public const string Browse = "browse";
    public const string TooDear = "tooDear";
    public const string Close = "close";
    public const string Bought = "bought";
    public const string NoStall = "noStall";
}
