using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>
/// One line of the crew's market brief: a held good, and what selling it in the
/// parked city would clear over what was paid for it. An empty <see cref="MarginPct"/>
/// means the lot cost nothing (mined), so any offer is pure gain.
/// </summary>
public sealed record BriefRow(
    string GoodId,
    string Name,
    string Category,
    int Units,
    double AverageCost,
    double Sell,
    double? MarginPct,
    long Profit);

/// <summary>
/// The crew's quick market brief when the convoy parks in a city: every good in the
/// hold that would clear a worthwhile margin if sold here, biggest margin first.
///
/// A pure read over state, like <c>Intel</c>: building the report cannot advance the
/// world. Gates on <see cref="GameConfig.CrewBrief"/> — the toggle is content, so a
/// future crew passive skill can flip it without touching this class.
/// </summary>
public static class CrewBrief
{
    public static bool Enabled(WorldData world) => world.Config.CrewBrief.Enabled;

    /// <summary>
    /// The brief while parked in a city; empty on the road, at a claim, or when the
    /// feature is off. Each held good is priced at what the city would actually pay
    /// today (crew terms, event multiplier, the lot's own grade) against its weighted
    /// average cost, and only goods clearing the configured margin floor are listed.
    /// </summary>
    public static IReadOnlyList<BriefRow> For(GameState state, WorldData world)
    {
        var rows = new List<BriefRow>();
        if (!Enabled(world)) return rows;

        var cityId = state.Caravan.LocationId;
        if (cityId is null || state.Caravan.Travel is not null) return rows;

        var city = world.City(cityId);
        var eco = world.Config.Economy;
        var minMargin = world.Config.CrewBrief.MinMargin;
        var qualityCfg = world.Quality;

        foreach (var (goodId, lot) in state.Caravan.Cargo)
        {
            if (lot.Units <= 0) continue;
            if (!world.GoodsById.TryGetValue(goodId, out var good)) continue;

            var profile = city.Market[good.Id];
            var stock = state.StockOf(city.Id, good.Id);
            var eventMult = WorldEvents.PriceMultiplier(state, world, city.Id, good.Id);
            var terms = CrewMath.Terms(state.Caravan, world, good.Category);
            var sell = Economy.SellUnitPrice(good, profile, stock, eco, terms, eventMult)
                       * QualityMath.SellMultiplier(lot.Quality, qualityCfg);

            // Mined lots cost nothing: any offer is pure gain.
            if (lot.AverageCost <= 0)
            {
                rows.Add(new BriefRow(
                    good.Id, good.Name, world.CategoryName(good.Category),
                    lot.Units, 0.0, Math.Round(sell, 1), null,
                    (long)Math.Round(sell * lot.Units)));
                continue;
            }

            var margin = (sell - lot.AverageCost) / lot.AverageCost;
            if (margin <= minMargin + 1e-12) continue;

            rows.Add(new BriefRow(
                good.Id, good.Name, world.CategoryName(good.Category),
                lot.Units, Math.Round(lot.AverageCost, 1), Math.Round(sell, 1),
                Math.Round(margin * 100.0, 1),
                (long)Math.Round((sell - lot.AverageCost) * lot.Units)));
        }

        // Most profitable first; free lots (no cost basis) lead the list.
        rows.Sort((a, b) => (b.MarginPct ?? double.MaxValue).CompareTo(a.MarginPct ?? double.MaxValue));
        return rows;
    }

    /// <summary>
    /// One human line for the wire: which goods clear the fuel line here. Top three by
    /// margin, then "+N more". Empty rows yield an empty string so callers can stay silent.
    /// </summary>
    public static string Summary(IReadOnlyList<BriefRow> rows, double minMargin)
    {
        if (rows.Count == 0) return "";

        var shown = rows.Take(3).Select(r => r.MarginPct is { } m
            ? $"{r.Name} +{m:0.#}%"
            : $"{r.Name} (mined, free)");
        var head = string.Join(", ", shown);
        var rest = rows.Count > 3 ? $", +{rows.Count - 3} more" : "";
        return $"{rows.Count} good(s) clear the {minMargin:P0} fuel line here: {head}{rest}.";
    }
}