using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.View;

public static partial class ViewBuilder
{
    /// <summary>
    /// The city page. Identity, how the place is doing, and its wire.
    ///
    /// The vitals are the authored ones carried live in state; the supply figures are
    /// read straight off this city's market, so they move on their own every day without
    /// anything having to write them.
    /// </summary>
    private static LocationView BuildLocation(GameState state, WorldData world, City city)
        => new(
            Id: city.Id,
            Name: city.Name,
            Region: city.Region,
            Industries: city.Industries,
            Standing: BuildStanding(state, world, city),
            Vitals: BuildVitals(state, world, city),
            Supplies: BuildSupplies(state, world, city),
            News: BuildNews(state, world, city));

    private static CityStandingView BuildStanding(GameState state, WorldData world, City city)
    {
        var config = world.Standing;
        var value = Standing.Of(state, city.Id);
        var rank = Standing.Rank(config, value);
        var ratio = Standing.ReservedRatio(config, value);
        var fill = config.Max > 0 ? Math.Clamp(value / config.Max, 0.0, 1.0) : 0.0;

        var permits = config.Permits.Select(p => new CityPermitView(
            Id: p.Id,
            Name: p.Name,
            Blurb: p.Blurb,
            StandingRequired: p.Standing,
            Granted: state.HasPermit(city.Id, p.Id))).ToList();

        var actions = config.Actions.Select(a => new CityFavorActionView(
            Id: a.Id,
            Name: a.Name,
            Blurb: a.Blurb,
            Cost: a.Cost,
            Affordable: state.Cash >= a.Cost,
            SegmentName: SegmentName(config, config.SegmentOr(a.SegmentId)),
            EffectText: FavorEffectText(world, a))).ToList();

        var segments = config.Segments.Select(seg =>
        {
            var v = Standing.Segment(state, city.Id, seg.Id);
            return new StandingSegmentView(
                Id: seg.Id,
                Name: seg.Name,
                Blurb: seg.Blurb,
                Value: Math.Round(v, 1),
                Max: config.SegmentMax,
                Fill: config.SegmentMax > 0 ? Math.Clamp(v / config.SegmentMax, 0.0, 1.0) : 0.0);
        }).ToList();

        var gates = world.Tiers.Select(t => new TierGateView(
            Tier: t.Tier,
            Name: t.Name,
            Color: t.Color,
            MinStanding: t.MinStanding,
            Open: Standing.TierOpen(t, value),
            ToGo: Math.Max(0.0, Math.Round(t.MinStanding - value, 1)))).ToList();

        var reservedPct = (int)Math.Round(ratio * 100);
        var reservedDisplay = reservedPct > 0
            ? $"{reservedPct}% of the shelf held for you"
            : "No shelf reserved yet";

        return new CityStandingView(
            GovernorName: city.GovernorName,
            GovernorTitle: city.GovernorTitle,
            Value: Math.Round(value, 1),
            Max: config.Max,
            Rank: rank?.Name ?? "",
            Tone: rank?.Tone ?? "muted",
            Fill: fill,
            ReservedDisplay: reservedDisplay,
            ReservedRatio: Math.Round(ratio, 4),
            Segments: segments,
            Permits: permits,
            Actions: actions,
            TierGates: gates);
    }

    private static string SegmentName(StandingConfig config, string segmentId)
    {
        foreach (var segment in config.Segments)
        {
            if (segment.Id == segmentId) return string.IsNullOrWhiteSpace(segment.Name) ? segment.Id : segment.Name;
        }
        return segmentId;
    }

    private static string FavorEffectText(WorldData world, FavorActionDef action)
    {
        var parts = new List<string>();
        if (action.Standing > 0)
            parts.Add($"+{action.Standing:0.#} {SegmentName(world.Standing, world.Standing.SegmentOr(action.SegmentId)).ToLowerInvariant()} standing");

        if (!string.IsNullOrWhiteSpace(action.VitalId) && action.VitalDelta != 0)
        {
            var vital = world.CityStats.Vital(action.VitalId);
            var name = vital?.Name ?? action.VitalId;
            var sign = action.VitalDelta > 0 ? "+" : "";
            var unit = vital?.Unit ?? "";
            parts.Add($"{name} {sign}{action.VitalDelta:0.#}{unit}");
        }

        if (action.StockPerGood > 0)
            parts.Add($"ships in the shortest supply");

        return parts.Count > 0 ? string.Join(" · ", parts) : action.Blurb;
    }

    private static List<CityVitalView> BuildVitals(GameState state, WorldData world, City city)
    {
        var catalogue = world.CityStats;
        var views = new List<CityVitalView>(catalogue.Vitals.Count);

        foreach (var def in catalogue.Vitals)
        {
            var value = CityStats.Vital(state, world, city, def.Id);
            var founding = CityStats.Founding(city, def.Id);
            var delta = value - founding;
            var band = CityStats.Band(def.Bands, value);

            var span = def.Max - def.Min;

            views.Add(new CityVitalView(
                Id: def.Id,
                Name: def.Name,
                Display: FormatVital(def, value),
                FoundingDisplay: FormatVital(def, founding),
                Unit: def.Unit,
                Blurb: def.Blurb,
                Band: band?.Name ?? "",
                Tone: band?.Tone ?? "muted",
                Value: Math.Round(value, 3),
                Founding: Math.Round(founding, 3),
                Delta: Math.Round(delta, 3),
                DeltaDisplay: FormatDelta(def, delta),
                Fill: span > 0 ? Math.Clamp((value - def.Min) / span, 0.0, 1.0) : 0.0));
        }

        return views;
    }

    /// <summary>
    /// Content owns the units, so the raw number is scaled and formatted the way the
    /// catalogue asked. A stat allowed to go negative is shown signed, because for those
    /// the direction is the whole message.
    /// </summary>
    private static string FormatVital(CityVitalDef def, double value)
    {
        var scaled = value * def.DisplayScale;
        var sign = def.Signed && scaled > 0 ? "+" : "";
        return $"{sign}{scaled.ToString("N" + Math.Max(0, def.Decimals))}{def.Unit}";
    }

    /// <summary>How far a city has moved since its founding, or nothing at all if it has not.</summary>
    private static string FormatDelta(CityVitalDef def, double delta)
    {
        var scaled = delta * def.DisplayScale;
        if (Math.Abs(scaled) < 0.05) return "";

        var sign = scaled > 0 ? "+" : "-";
        return $"{sign}{Math.Abs(scaled).ToString("N" + Math.Max(0, def.Decimals))}{def.Unit}";
    }

    private static List<CitySupplyView> BuildSupplies(GameState state, WorldData world, City city)
    {
        var catalogue = world.CityStats;
        var views = new List<CitySupplyView>(catalogue.Supplies.Count);

        foreach (var def in catalogue.Supplies)
        {
            var reading = CityStats.Supply(state, world, city, def);
            var band = CityStats.Band(catalogue.SupplyBands, reading.Index);
            var net = reading.NetFlow;

            views.Add(new CitySupplyView(
                Id: def.Id,
                Name: def.Name,
                Blurb: def.Blurb,
                Band: band?.Name ?? "",
                Tone: band?.Tone ?? "muted",
                Index: Math.Round(reading.Index),
                Fill: Math.Clamp(reading.Index / 200.0, 0.0, 1.0),
                Production: Math.Round(reading.Production, 1),
                Consumption: Math.Round(reading.Consumption, 1),
                NetFlow: Math.Round(net, 1),
                Stock: Math.Round(reading.Stock),
                DaysOfCover: reading.DaysOfCover is { } days ? Math.Round(days, 1) : null,
                Flow: net > 0.5 ? "surplus" : net < -0.5 ? "deficit" : "balanced",
                Goods: def.Goods
                    .Where(world.GoodsById.ContainsKey)
                    .Select(id => world.Good(id).Name)
                    .ToList()));
        }

        return views;
    }

    /// <summary>
    /// The city wire: every active event that targets this city, or the whole map.
    /// Headlines are resolved here so the front-end never sees a template.
    /// </summary>
    private static List<CityNewsView> BuildNews(GameState state, WorldData world, City city)
    {
        var news = new List<CityNewsView>();

        foreach (var evt in state.ActiveEvents)
        {
            var def = world.Events.ById(evt.DefId);
            if (def is null) continue;
            if (!def.Global && evt.CityId != city.Id) continue;

            City? target = city;
            if (!string.IsNullOrEmpty(evt.CityId) && world.CitiesById.TryGetValue(evt.CityId, out var named))
                target = named;

            var good = WorldEvents.HeadlineGood(world, def);
            var category = WorldEvents.HeadlineCategory(world, def);

            news.Add(new CityNewsView(
                Day: evt.StartDay,
                Kind: def.Kind,
                Tone: def.Tone,
                Headline: WorldEvents.Format(def.Headline, target, good, category),
                Detail: WorldEvents.Format(def.Detail, target, good, category),
                DaysLeft: Math.Max(0, evt.EndDay - state.Day)));
        }

        return news;
    }

}
