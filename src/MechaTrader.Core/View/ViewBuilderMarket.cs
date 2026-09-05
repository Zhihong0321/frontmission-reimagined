using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.View;

public static partial class ViewBuilder
{
    private static List<MarketRowView> BuildMarket(GameState state, WorldData world, City city)
    {
        var eco = world.Config.Economy;
        var rows = new List<MarketRowView>(world.Goods.Count);
        var regard = Standing.Of(state, city.Id);

        // The informant's coverage is the same for every good, so the road search runs once.
        var coverage = Intel.Coverage(state, world, city.Id);
        var freeVolume = CaravanMath.FreeVolume(state.Caravan, world);

        foreach (var good in world.Goods)
        {
            var profile = city.Market[good.Id];
            var tier = world.TierOf(good);
            var reliefPerUnit = WorldEvents.ReliefPerUnit(state, world, city.Id, good.Id);
            var reliefHint = reliefPerUnit > 0
                ? string.Join(" · ", WorldEvents.ReliefFor(state, world, city.Id, good.Id).Select(d => d.Name))
                : "";
            var stock = state.StockOf(city.Id, good.Id);
            var net = profile.Production - profile.Consumption;

            var flow = net > 0.5 ? "surplus" : net < -0.5 ? "deficit" : "balanced";

            var lot = state.Caravan.Cargo.TryGetValue(good.Id, out var l) ? l : null;
            var shelfUnits = Economy.UnitsOnTheShelf(stock, eco);
            var reserved = Standing.ReservedUnits(
                shelfUnits, Standing.ReservedRatio(world.Standing, Standing.Of(state, city.Id)));
            var eventMult = WorldEvents.PriceMultiplier(state, world, city.Id, good.Id);
            var terms = CrewMath.Terms(state.Caravan, world, good.Category);
            var knowledge = CrewMath.BestKnowledge(state.Caravan.Crew, world.Crew, good.Category);
            var selection = CrewMath.SelectionFactor(state.Caravan.Crew, world.Crew, good.Category);
            var avgQ = stock.OutQuality;
            var pickQ = QualityMath.SelectedQuality(avgQ, Math.Max(1, shelfUnits), 1, selection, world.Quality);
            var categoryName = world.CategoriesById.TryGetValue(good.Category, out var cat) ? cat.Name : good.Category;
            var heldQ = lot?.Quality ?? 0;

            // What the quote is made of (see Economy): the city's own price off the shelf
            // and off its whole holding, the full-spread counterfactual a crewless trader
            // would face, and the grade multiplier the pick adds on the buy side.
            var marketBuy = Economy.UnitPrice(good, profile, stock.PriceShelf, eco, eventMult);
            var marketSell = Economy.UnitPrice(good, profile, stock.PriceTotal, eco, eventMult);
            var pickMult = QualityMath.SellMultiplier(pickQ, world.Quality);

            // Each purchase limit on its own, then the binding minimum: hold space, cash
            // at the quoted (best-grade) unit price, and what is actually on the shelf.
            var buyUnit = marketBuy * (1.0 + eco.Spread * terms.BuySpreadShare) * pickMult;
            var maxByHold = good.UnitVolume > 0 ? (int)Math.Floor(freeVolume / good.UnitVolume) : int.MaxValue;
            var maxByShelf = Economy.UnitsOnTheShelf(stock, eco);
            var maxByCash = buyUnit > 0 ? (int)Math.Floor(state.Cash / buyUnit) : int.MaxValue;
            var maxBuy = Economy.MaxAffordableUnits(good, profile, stock, state.Cash, freeVolume, eco, terms, eventMult, pickMult);

            rows.Add(new MarketRowView(
                GoodId: good.Id,
                Name: good.Name,
                CategoryId: good.Category,
                Category: categoryName,
                Tier: good.Tier,
                TierName: tier.Name,
                TierColor: tier.Color,
                Locked: !Standing.TierOpen(tier, regard),
                UnlockStanding: tier.MinStanding,
                Buy: Math.Round(Economy.BuyUnitPrice(good, profile, stock, eco, terms, eventMult) * QualityMath.SellMultiplier(pickQ, world.Quality), 1),
                Sell: Math.Round(Economy.SellUnitPrice(good, profile, stock, eco, terms, eventMult), 1),
                BasePrice: good.BasePrice,
                MarketBuy: Math.Round(marketBuy, 2),
                MarketSell: Math.Round(marketSell, 2),
                NoCrewBuy: Math.Round(marketBuy * (1.0 + eco.Spread), 2),
                NoCrewSell: Math.Round(marketSell * (1.0 - eco.Spread), 2),
                PickMult: Math.Round(pickMult, 3),
                Stock: Math.Round(stock.Total),
                Shelf: Math.Round(stock.Out),
                Reserved: reserved,
                Intake: Math.Round(stock.In),
                Held: lot?.Units ?? 0,
                AverageCost: Math.Round(lot?.AverageCost ?? 0, 1),
                HeldQuality: Math.Round(heldQ, 1),
                HeldSTier: lot is not null && lot.Units > 0 && QualityMath.IsSTier(heldQ, world.Quality),
                AverageQuality: Math.Round(avgQ, 1),
                PickQuality: Math.Round(pickQ, 1),
                Knowledge: Math.Round(knowledge, 1),
                STierPossible: QualityMath.IsSTier(pickQ, world.Quality),
                UnitVolume: good.UnitVolume,
                MaxBuy: maxBuy,
                MaxByHold: maxByHold,
                MaxByCash: maxByCash,
                MaxByShelf: maxByShelf,
                Flow: flow,
                EventHint: WorldEvents.PriceHint(state, world, city.Id, good.Id),
                ReliefPerUnit: Math.Round(reliefPerUnit, 4),
                ReliefHint: reliefHint,
                Elsewhere: Intel.Reports(state, world, coverage, good)
                    .Select(r => new PriceReportView(
                        CityId: r.City.Id,
                        CityName: r.City.Name,
                        Region: r.City.Region,
                        DistanceKm: Math.Round(r.DistanceKm),
                        Days: r.Days,
                        Buy: Math.Round(r.Buy, 1),
                        Sell: Math.Round(r.Sell, 1),
                        Flow: r.Flow,
                        ErrorPct: Math.Round(r.Error * 100, 1)))
                    .ToList()));
        }

        return rows;
    }

    private static CrewBriefView BuildCrewBrief(GameState state, WorldData world)
        => new(
            world.Config.CrewBrief.MinMargin,
            CrewBrief.For(state, world)
                .Select(r => new CrewBriefRowView(
                    r.GoodId,
                    r.Name,
                    r.Category,
                    r.Units,
                    r.AverageCost,
                    r.Sell,
                    r.MarginPct,
                    r.Profit))
                .ToList());

    /// <summary>
    /// What the convoy's hold would clear in every city, priced at that city's market
    /// today (crew terms, event multipliers and each lot's own grade) against the cost
    /// basis of what was paid for it. Only cities that would turn a profit are listed;
    /// the chart decides which are worth highlighting. A pure read over state.
    /// </summary>
    private static IReadOnlyList<SellOutlookView> BuildSellOutlook(GameState state, WorldData world)
    {
        var rows = new List<SellOutlookView>();
        var eco = world.Config.Economy;
        var qualityCfg = world.Quality;

        foreach (var city in world.Cities)
        {
            long profit = 0;
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
                profit += (long)Math.Round(sell * lot.Units) - lot.TotalCost;
            }

            if (profit > 0) rows.Add(new SellOutlookView(city.Id, profit));
        }

        return rows;
    }

    private static List<CargoRowView> BuildCargo(GameState state, WorldData world)
    {
        var rows = new List<CargoRowView>();

        foreach (var good in world.Goods)
        {
            if (!state.Caravan.Cargo.TryGetValue(good.Id, out var lot) || lot.Units <= 0) continue;

            var tier = world.TierOf(good);
            rows.Add(new CargoRowView(
                good.Id,
                good.Name,
                world.CategoriesById.TryGetValue(good.Category, out var cat) ? cat.Name : good.Category,
                good.Tier,
                tier.Name,
                tier.Color,
                lot.Units,
                Math.Round(lot.AverageCost, 1),
                Math.Round(lot.Quality, 1),
                QualityMath.IsSTier(lot.Quality, world.Quality),
                Math.Round(lot.Units * good.UnitVolume, 1)));
        }

        return rows;
    }

}
