using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.View;

/// <summary>
/// Projects game state into a display snapshot. All presentation-oriented derivation
/// happens here so no front-end has to understand the price model to render a market.
/// </summary>
public static class ViewBuilder
{
    public static GameView Build(GameState state, WorldData world)
    {
        var caravan = state.Caravan;

        var location = caravan.LocationId is { } id ? world.City(id) : null;
        var site = caravan.SiteId is { } siteId ? state.Site(siteId) : null;
        var parked = caravan.Travel is null;

        return new GameView(
            Day: state.Day,
            Cash: state.Cash,
            NetWorth: NetWorth(state, world),
            Bankrupt: state.Bankrupt,
            Location: location is null ? null : BuildLocation(state, world, location),
            Site: site is null || !parked ? null : BuildSite(state, world, site),
            Field: location is null && site is null && parked ? BuildField(state, world) : null,
            Travel: BuildTravel(state, world),
            Convoy: BuildConvoy(state, world),
            Market: location is null ? Array.Empty<MarketRowView>() : BuildMarket(state, world, location),
            Cargo: BuildCargo(state, world),
            Routes: parked ? BuildDestinations(state, world, location) : Array.Empty<RouteView>(),
            Shipyard: location is null ? Array.Empty<TruckOfferView>() : BuildShipyard(world),
            Outfitters: location is null ? Array.Empty<GearOfferView>() : BuildOutfitters(state, world),
            Station: BuildStation(state, world, location),
            Crew: BuildCrew(state, world, location),
            Warehouse: BuildWarehouse(state, world, location),
            Contracts: BuildContracts(state, world, location),
            Expo: location is null ? null : BuildExpo(state, world, location),
            Tiers: world.Tiers.Select(t => new TierView(t.Tier, t.Name, t.Color, t.MinStanding)).ToList(),
            EventCityIds: WorldEvents.EventCityIds(state),
            MiningSites: BuildMiningSites(state, world),
            CrewBrief: BuildCrewBrief(state, world),
            SellOutlook: BuildSellOutlook(state, world));
    }

    /// <summary>
    /// Cash plus what the hold would fetch here and now. A truer score than cash alone,
    /// since a convoy mid-run is usually cash-poor and cargo-rich.
    /// </summary>
    public static long NetWorth(GameState state, WorldData world)
    {
        var total = (double)state.Cash;
        var eco = world.Config.Economy;
        var cityId = state.Caravan.LocationId;

        foreach (var (goodId, lot) in state.Caravan.Cargo)
        {
            if (lot.Units <= 0) continue;

            if (cityId is not null)
            {
                var good = world.Good(goodId);
                var profile = world.City(cityId).Market[goodId];
                var stock = state.StockOf(cityId, goodId);
                var eventMult = WorldEvents.PriceMultiplier(state, world, cityId, goodId);
                var terms = CrewMath.Terms(state.Caravan, world, good.Category);
                var revenue = Economy.EstimateSellRevenue(
                    good, profile, stock, lot.Units, eco, terms, eventMult);
                total += revenue * QualityMath.SellMultiplier(lot.Quality, world.Quality);
            }
            else
            {
                // On the road there is no market to price against; fall back to cost basis.
                total += lot.TotalCost;
            }
        }

        foreach (var (whCityId, warehouse) in state.Warehouses)
        {
            if (!world.CitiesById.TryGetValue(whCityId, out var whCity)) continue;
            foreach (var (goodId, lot) in warehouse.Stock)
            {
                if (lot.Units <= 0) continue;
                var good = world.Good(goodId);
                var profile = whCity.Market[goodId];
                var stock = state.StockOf(whCityId, goodId);
                var eventMult = WorldEvents.PriceMultiplier(state, world, whCityId, goodId);
                var revenue = Economy.EstimateSellRevenue(
                    good, profile, stock, lot.Units, eco, TradeTerms.Market, eventMult);
                total += revenue * QualityMath.SellMultiplier(lot.Quality, world.Quality);
            }
        }

        return (long)Math.Round(total);
    }

    private static TravelView? BuildTravel(GameState state, WorldData world)
    {
        if (state.Caravan.Travel is not { } t) return null;

        var path = new List<MapPointView>();
        foreach (var w in t.Waypoints)
        {
            path.Add(new MapPointView(Math.Round(w.X, 1), Math.Round(w.Y, 1)));
        }

        var (cx, cy) = MapMath.TravelCoords(t);

        return new TravelView(
            t.FromName,
            t.ToName,
            t.TotalDays,
            t.DaysRemaining,
            Math.Round(t.FuelPerDay, 1),
            path,
            Math.Round(cx, 1),
            Math.Round(cy, 1));
    }

    private static ConvoyView BuildConvoy(GameState state, WorldData world)
    {
        var caravan = state.Caravan;

        return new ConvoyView(
            Capacity: CaravanMath.Capacity(caravan, world),
            Used: Math.Round(CaravanMath.UsedVolume(caravan, world), 1),
            Free: Math.Round(CaravanMath.FreeVolume(caravan, world), 1),
            SpeedKmPerDay: CaravanMath.SpeedKmPerDay(caravan, world),
            DailyUpkeep: CaravanMath.DailyUpkeep(caravan, world),
            Trucks: caravan.Trucks.Select(t => world.Truck(t.TypeId).Name).ToList(),
            Gear: caravan.GearIds
                .Select(id => world.GearById.TryGetValue(id, out var g) ? g.Name : id)
                .ToList(),
            CanMine: CaravanMath.CanMine(caravan, world),
            MineYield: CaravanMath.MineYield(caravan, world));
    }

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

    private static List<RouteView> BuildDestinations(GameState state, WorldData world, City? city)
    {
        var rows = new List<RouteView>();
        var here = MapMath.Position(state, world);

        if (city is not null)
        {
            foreach (var route in world.Routes.From(city.Id))
            {
                var otherId = route.Other(city.Id);
                var other = world.City(otherId);
                var destCell = world.Map.CellOfCity(otherId);
                var plan = MapMath.Pathfind(state.Caravan, world, here, destCell);
                var days = plan?.Days ?? CaravanMath.TravelDays(state.Caravan, world, route);
                var fuel = plan?.Fuel ?? CaravanMath.TravelFuel(state.Caravan, world, route);
                var best = BestCargoFor(state, world, city, other, days, fuel);

                rows.Add(new RouteView(
                    ToId: otherId,
                    ToName: other.Name,
                    ToRegion: other.Region,
                    DistanceKm: Math.Round(plan?.DistanceKm ?? route.DistanceKm),
                    TerrainName: route.Terrain.Name,
                    Days: days,
                    EstimatedFuel: Math.Round(fuel),
                    BestGoodId: best.GoodId,
                    BestGoodName: best.GoodName,
                    BestUnits: best.Units,
                    BestProfit: best.Profit));
            }
        }
        else
        {
            foreach (var other in world.Cities)
            {
                var destCell = world.Map.CellOfCity(other.Id);
                if (destCell.Col == here.Col && destCell.Row == here.Row) continue;
                var plan = MapMath.Pathfind(state.Caravan, world, here, destCell);
                if (plan is null) continue;

                rows.Add(new RouteView(
                    ToId: other.Id,
                    ToName: other.Name,
                    ToRegion: other.Region,
                    DistanceKm: Math.Round(plan.DistanceKm),
                    TerrainName: plan.Layer,
                    Days: plan.Days,
                    EstimatedFuel: Math.Round(plan.Fuel),
                    BestGoodId: null,
                    BestGoodName: null,
                    BestUnits: 0,
                    BestProfit: 0));
            }
        }

        foreach (var site in state.MiningSites)
        {
            if (state.Caravan.SiteId == site.Id) continue;
            var cell = world.Map[site.Col, site.Row];
            var plan = MapMath.Pathfind(state.Caravan, world, here, cell);
            if (plan is null) continue;
            var good = world.GoodsById.TryGetValue(site.GoodId, out var g) ? g.Name : site.GoodId;
            var status = site.Remaining <= 0 ? "played out" : $"{site.Remaining:0} left";

            rows.Add(new RouteView(
                ToId: site.Id,
                ToName: $"{good} deposit",
                ToRegion: status,
                DistanceKm: Math.Round(plan.DistanceKm),
                TerrainName: "claim",
                Days: plan.Days,
                EstimatedFuel: Math.Round(plan.Fuel),
                BestGoodId: null,
                BestGoodName: null,
                BestUnits: 0,
                BestProfit: 0));
        }

        return rows.OrderByDescending(r => r.BestProfit).ThenBy(r => r.Days).ToList();
    }

    private readonly record struct CargoAdvice(string? GoodId, string? GoodName, int Units, long Profit);

    /// <summary>
    /// What is worth hauling down one road, sized to what the convoy can actually pay
    /// for and carry.
    ///
    /// The player can only see the market they are standing in, so without this they
    /// would be choosing roads blind. Both legs are priced against the depth the order
    /// consumes, and fuel and upkeep are deducted, so the number shown is what the run
    /// would really clear rather than a headline margin.
    /// </summary>
    private static CargoAdvice BestCargoFor(
        GameState state, WorldData world, City origin, City destination, int days, double fuel)
    {
        if (days <= 0 || days == int.MaxValue) return new CargoAdvice(null, null, 0, 0);

        var eco = world.Config.Economy;
        var free = CaravanMath.FreeVolume(state.Caravan, world);
        var fixedCost = fuel + CaravanMath.DailyUpkeep(state.Caravan, world) * days;
        var regard = Standing.Of(state, origin.Id);

        var best = new CargoAdvice(null, null, 0, 0);

        foreach (var good in world.Goods)
        {
            // Never recommend a grade the city will not sell us.
            if (!Standing.TierOpen(world.TierOf(good), regard)) continue;

            var originProfile = origin.Market[good.Id];
            var originStock = state.StockOf(origin.Id, good.Id);
            var originMult = WorldEvents.PriceMultiplier(state, world, origin.Id, good.Id);

            var destinationProfile = destination.Market[good.Id];
            var destinationStock = state.StockOf(destination.Id, good.Id);
            var destMult = WorldEvents.PriceMultiplier(state, world, destination.Id, good.Id);
            var terms = CrewMath.Terms(state.Caravan, world, good.Category);

            var saleable = Economy.UnitsOnTheShelf(originStock, eco);
            var selection = CrewMath.SelectionFactor(state.Caravan.Crew, world.Crew, good.Category);
            var bestCrate = QualityMath.SellMultiplier(
                QualityMath.SelectedQuality(originStock.OutQuality, saleable, 1, selection, world.Quality), world.Quality);
            var maxUnits = Economy.MaxAffordableUnits(
                good, originProfile, originStock, state.Cash, free, eco, terms, originMult, bestCrate);
            if (maxUnits <= 0) continue;

            foreach (var fraction in OrderSizes)
            {
                var units = (int)(maxUnits * fraction);
                if (units <= 0) continue;

                var cost = Economy.ApproximateBuyCost(
                    good, originProfile, originStock, units, eco, terms, originMult);
                var revenue = Economy.ApproximateSellRevenue(
                    good, destinationProfile, destinationStock, units, eco, terms, destMult);

                var pickQ = QualityMath.SelectedQuality(
                    originStock.OutQuality, saleable, units, selection, world.Quality);
                var gradeMult = QualityMath.SellMultiplier(pickQ, world.Quality);
                cost *= gradeMult;
                revenue *= gradeMult;

                var profit = (long)Math.Round(revenue - cost - fixedCost);
                if (profit > best.Profit)
                    best = new CargoAdvice(good.Id, good.Name, units, profit);
            }
        }

        return best;
    }

    private static readonly double[] OrderSizes = { 1.0, 0.75, 0.5, 0.3, 0.15 };

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

    /// <summary>
    /// The crew board: who is aboard, what the roster is actually worth, and who is
    /// waiting at the local recruitment centre.
    /// </summary>
    private static CrewView BuildCrew(GameState state, WorldData world, City? location)
    {
        var cfg = world.Crew;
        var roster = state.Caravan.Crew;

        var members = roster.Select(m => new CrewMemberView(
            Id: m.Id,
            Name: m.Name,
            RoleName: RoleName(cfg, m.RoleId),
            PostId: m.PostId,
            PostName: cfg.Post(m.PostId)?.Name ?? "",
            DailyWage: m.DailyWage,
            Severance: m.DailyWage * Math.Max(0, cfg.SeveranceDays),
            HiredDay: m.HiredDay,
            HiredAt: world.CitiesById.TryGetValue(m.HiredAtCityId, out var hiredAt) ? hiredAt.Name : "",
            Skills: LevelsOf(cfg, m.Skills),
            Knowledge: KnowledgeOf(world, cfg, m),
            Traits: TraitsOf(cfg, m.TraitIds))).ToList();

        var skills = cfg.Skills.Select(skill =>
        {
            // Posts decide who counts: a broker riding in the back leads nothing.
            var level = CrewMath.Level(roster, cfg, skill.Id);
            var leader = CrewMath.Leader(roster, cfg, skill.Id)?.Name;

            return new CrewSkillView(
                Id: skill.Id,
                Name: skill.Name,
                Lever: skill.Lever,
                Blurb: skill.Blurb,
                Level: level,
                MaxLevel: cfg.MaxSkill,
                LeaderName: leader,
                EffectText: EffectText(state, world, skill, level));
        }).ToList();

        var posts = cfg.Posts.Select(post =>
        {
            var hands = roster.Where(m => m.PostId == post.Id).ToList();
            var gated = cfg.Skills.Where(s => post.Levers.Contains(s.Lever)).Select(s => s.Name);

            // The post's leader is whoever leads its first gated skill, so the roster
            // card can say "Trading · led by X" without the shell ranking anyone.
            var leadSkill = cfg.Skills.FirstOrDefault(s => post.Levers.Contains(s.Lever));
            var leader = leadSkill is null ? null : CrewMath.Leader(roster, cfg, leadSkill.Id)?.Name;

            return new CrewPostView(
                Id: post.Id,
                Name: post.Name,
                Blurb: post.Blurb,
                SkillNames: string.Join(", ", gated),
                Hands: hands.Count,
                LeaderName: leader);
        }).ToList();

        return new CrewView(
            Size: roster.Count,
            Capacity: cfg.CrewCapacity,
            DailyWages: CrewMath.DailyWages(roster),
            Roster: members,
            Skills: skills,
            Posts: posts,
            Intel: BuildIntel(state, world),
            Recruitment: location is null ? null : BuildRecruitment(state, world, location));
    }

    private static IntelView BuildIntel(GameState state, WorldData world)
    {
        var cfg = world.Crew;
        var roster = state.Caravan.Crew;
        var level = Intel.Level(roster, cfg);
        var reach = Intel.Reach(roster, cfg);
        var error = Intel.Error(roster, cfg);
        var informant = Intel.Informant(roster, cfg);

        return new IntelView(
            Active: reach > 0,
            InformantName: informant?.Name,
            Level: level,
            MaxLevel: cfg.MaxSkill,
            Reach: reach,
            MaxReach: Math.Max(cfg.Intel.MinCities, cfg.Intel.MaxCities),
            ErrorPct: Math.Round(error * 100, 1),
            Summary: IntelSummary(reach, error));
    }

    private static string IntelSummary(int reach, double error)
        => reach <= 0
            ? "nobody gathering: no word from other markets"
            : $"reads the {reach} nearest markets within ±{error:P0}";

    /// <summary>
    /// The local hiring board, minus anyone who has already taken a contract this run.
    /// The pool itself is derived from the seed, not stored, so this is a pure read.
    /// </summary>
    private static RecruitmentView BuildRecruitment(GameState state, WorldData world, City city)
    {
        var cfg = world.Crew;
        var roomAboard = state.Caravan.Crew.Count < cfg.CrewCapacity;

        var candidates = Recruitment.PoolFor(world, city, state.Seed, state.Day)
            .Where(c => !state.RecruitedIds.Contains(c.Id))
            .Select(c => new CandidateView(
                Id: c.Id,
                Name: c.Name,
                RoleName: c.RoleName,
                PostName: cfg.Post(cfg.DefaultPost(cfg.Role(c.RoleId)))?.Name ?? "",
                DailyWage: c.DailyWage,
                SigningFee: c.SigningFee,
                Affordable: state.Cash >= c.SigningFee,
                RoomAboard: roomAboard,
                Skills: LevelsOf(cfg, c.Skills),
                Knowledge: KnowledgeOf(world, cfg, c.Knowledge, c.TraitIds),
                Traits: TraitsOf(cfg, c.TraitIds)))
            .ToList();

        return new RecruitmentView(
            CityName: city.Name,
            RefreshInDays: Recruitment.DaysUntilRefresh(state.Day, cfg),
            Candidates: candidates);
    }

    private static List<SkillLevelView> LevelsOf(CrewConfig cfg, IReadOnlyDictionary<string, int> skills)
        => cfg.Skills
            .Select(s => new SkillLevelView(s.Id, s.Name, skills.TryGetValue(s.Id, out var level) ? level : 0))
            .ToList();

    private static List<KnowledgeView> KnowledgeOf(WorldData world, CrewConfig cfg, CrewMember member)
        => KnowledgeOf(world, cfg, member.Knowledge, member.TraitIds);

    private static List<KnowledgeView> KnowledgeOf(
        WorldData world, CrewConfig cfg,
        IReadOnlyDictionary<string, double> knowledge,
        IReadOnlyList<string> traitIds)
    {
        var rows = new List<KnowledgeView>();
        foreach (var category in world.Categories)
        {
            var dummy = new CrewMember
            {
                Knowledge = new Dictionary<string, double>(knowledge),
                TraitIds = traitIds.ToList()
            };
            var level = (int)Math.Round(CrewMath.KnowledgeOf(dummy, cfg, category.Id));
            if (level <= 0) continue;
            rows.Add(new KnowledgeView(category.Id, category.Name, level, cfg.MaxKnowledge));
        }
        return rows;
    }

    private static List<TraitView> TraitsOf(CrewConfig cfg, IReadOnlyList<string> ids)
    {
        var rows = new List<TraitView>();
        foreach (var id in ids)
        {
            var trait = cfg.Trait(id);
            rows.Add(trait is null
                ? new TraitView(id, id, "", "")
                : new TraitView(trait.Id, trait.Name, trait.Kind, trait.Blurb));
        }
        return rows;
    }

    private static WarehouseView BuildWarehouse(GameState state, WorldData world, City? location)
    {
        var cfg = world.Config.Warehouse;
        if (location is null)
        {
            return new WarehouseView(false, cfg.RentCost, cfg.DailyRent, cfg.Capacity, 0,
                Array.Empty<WarehouseLotView>());
        }

        if (!state.Warehouses.TryGetValue(location.Id, out var warehouse))
        {
            return new WarehouseView(false, cfg.RentCost, cfg.DailyRent, cfg.Capacity, 0,
                Array.Empty<WarehouseLotView>());
        }

        var lots = new List<WarehouseLotView>();
        foreach (var good in world.Goods)
        {
            warehouse.Stock.TryGetValue(good.Id, out var lot);
            var units = lot?.Units ?? 0;
            warehouse.AutoSellPrice.TryGetValue(good.Id, out var ask);
            warehouse.AutoProcurePrice.TryGetValue(good.Id, out var bid);
            if (units <= 0 && ask <= 0 && bid <= 0) continue;

            var q = lot?.Quality ?? world.Quality.Nominal;
            lots.Add(new WarehouseLotView(
                good.Id,
                good.Name,
                units,
                Math.Round(q, 1),
                lot is not null && units > 0 && QualityMath.IsSTier(q, world.Quality),
                ask,
                bid));
        }

        return new WarehouseView(
            true,
            cfg.RentCost,
            cfg.DailyRent,
            cfg.Capacity,
            Math.Round(WarehouseMath.UsedVolume(warehouse, world), 1),
            lots);
    }

    private static string RoleName(CrewConfig cfg, string roleId)
    {
        foreach (var role in cfg.Roles)
        {
            if (role.Id == roleId) return string.IsNullOrWhiteSpace(role.Name) ? role.Id : role.Name;
        }
        return roleId;
    }

    /// <summary>
    /// What a skill is worth, stated in the terms the player is already reading
    /// elsewhere on the screen. The price levers erode the market's spread rather than
    /// moving the mid price, so the honest figure to show is the change to the quote.
    /// </summary>
    private static string EffectText(GameState state, WorldData world, CrewSkillDef skill, int level)
    {
        var cfg = world.Crew;
        var spread = world.Config.Economy.Spread;
        var effect = cfg.MaxSkill > 0
            ? skill.MaxEffect * Math.Clamp((double)level / cfg.MaxSkill, 0.0, 1.0)
            : 0.0;

        switch (skill.Lever)
        {
            case CrewLever.Speed:
                var speed = CaravanMath.SpeedKmPerDay(state.Caravan, world);
                return level == 0
                    ? $"nobody reading the road: {speed:N0} km/day"
                    : $"+{effect:P0} convoy speed, now {speed:N0} km/day";

            case CrewLever.Buy:
                return level == 0
                    ? "nobody haggling: the market keeps its full cut"
                    : $"-{spread * effect / (1.0 + spread):P1} off every buy quote";

            case CrewLever.Sell:
                return level == 0
                    ? "nobody closing: the market keeps its full cut"
                    : $"+{spread * effect / (1.0 - spread):P1} on every sell quote";

            case CrewLever.Upkeep:
                var upkeep = CaravanMath.TruckUpkeep(state.Caravan, world);
                return level == 0
                    ? $"nobody on the books: {upkeep:N0} cr/day of truck upkeep"
                    : $"-{effect:P0} truck upkeep and fuel, {upkeep * effect:N0} cr/day";

            case CrewLever.Intel:
                return IntelSummary(
                    Intel.Reach(state.Caravan.Crew, cfg),
                    Intel.Error(state.Caravan.Crew, cfg));

            default:
                return "carried, but nothing in the simulation reads it yet";
        }
    }

    /// <summary>
    /// The map, in the projected kilometre coordinates the loader already computed.
    /// Normalising to a viewport is the front-end's business, not the simulation's.
    /// </summary>
    public static MapView BuildMap(WorldData world)
    {
        var cities = world.Cities
            .Select(c => new MapCityView(c.Id, c.Name, c.Region, Math.Round(c.X, 1), Math.Round(c.Y, 1)))
            .ToList();

        var roads = world.Routes.All
            .Select(r => new MapRoadView(r.FromId, r.ToId, r.Terrain.Id, r.Terrain.Name))
            .ToList();

        var map = world.Map;
        var biomes = new char[map.Cells.Count];
        var mask = new char[map.Cells.Count];
        for (var i = 0; i < map.Cells.Count; i++)
        {
            biomes[i] = MapBiome.Code(map.Cells[i].Biome);
            mask[i] = map.Cells[i].HasRoad ? '1' : '0';
        }

        return new MapView(
            cities, roads,
            map.Width, map.Height, map.CellKm,
            Math.Round(map.OriginX, 1), Math.Round(map.OriginY, 1),
            new string(biomes), new string(mask));
    }

    private static List<TruckOfferView> BuildShipyard(WorldData world)
        => world.Trucks
            .Select(t => new TruckOfferView(
                t.Id, t.Name, t.EffectiveKind, t.Price, t.Capacity, t.SpeedKmPerDay,
                t.UpkeepPerDay, t.FuelPerKm, t.MineYield))
            .ToList();

    private static List<GearOfferView> BuildOutfitters(GameState state, WorldData world)
    {
        var free = CaravanMath.FreeVolume(state.Caravan, world);
        return world.Gear.Select(g => new GearOfferView(
            g.Id, g.Name, g.Price, g.Volume, g.MineYield,
            Affordable: state.Cash >= g.Price,
            Fits: g.Volume <= free + 1e-9)).ToList();
    }

    private static SiteView BuildSite(GameState state, WorldData world, MiningSite site)
    {
        var good = world.GoodsById.TryGetValue(site.GoodId, out var g) ? g : null;
        var can = CaravanMath.CanMine(state.Caravan, world);
        var yield = CaravanMath.MineYield(state.Caravan, world);
        var free = CaravanMath.FreeVolume(state.Caravan, world);
        var room = good is null || good.UnitVolume <= 0 ? 0 : (int)Math.Floor(free / good.UnitVolume);
        var expected = (int)Math.Min(Math.Min(site.Remaining, yield), room);

        string hint;
        if (!can) hint = "The convoy has no mining gear or machine.";
        else if (site.Remaining <= 0) hint = "This claim is played out.";
        else if (room <= 0) hint = "The hold is full.";
        else hint = $"Waiting a day will extract about {expected:N0} {good?.Name ?? site.GoodId}.";

        return new SiteView(
            site.Id,
            $"{good?.Name ?? site.GoodId} deposit",
            site.GoodId,
            good?.Name ?? site.GoodId,
            site.Remaining,
            expected,
            can,
            hint);
    }

    private static FieldView BuildField(GameState state, WorldData world)
    {
        var cell = MapMath.Position(state, world);
        return new FieldView(cell.Id, cell.Biome, Math.Round(cell.X, 1), Math.Round(cell.Y, 1));
    }

    private static List<MiningSiteView> BuildMiningSites(GameState state, WorldData world)
    {
        var views = new List<MiningSiteView>(state.MiningSites.Count);
        foreach (var site in state.MiningSites)
        {
            var cell = world.Map[site.Col, site.Row];
            var good = world.GoodsById.TryGetValue(site.GoodId, out var g) ? g.Name : site.GoodId;
            views.Add(new MiningSiteView(
                site.Id,
                $"{good} deposit",
                Math.Round(cell.X, 1),
                Math.Round(cell.Y, 1),
                site.Remaining,
                site.Remaining <= 0));
        }
        return views;
    }

    /// <summary>
    /// The station: offers (only while parked), and the fleet with what each vehicle
    /// could take and what the yard would pay. Effects are worded here so the shell
    /// never reads an upgrade's numbers.
    /// </summary>
    private static StationView BuildStation(GameState state, WorldData world, City? location)
    {
        var caravan = state.Caravan;
        var fleet = new List<FleetTruckView>(caravan.Trucks.Count);

        foreach (var truck in caravan.Trucks)
        {
            var type = world.Truck(truck.TypeId);
            var spec = CaravanMath.Spec(truck, world);
            var blocker = location is null ? "The station only trades while parked in a city." : CaravanMath.SellBlocker(caravan, world, truck);

            var fittings = world.TruckUpgrades.Select(u => new TruckFittingView(
                Id: u.Id,
                Name: u.Name,
                Blurb: u.Blurb,
                Price: u.Price,
                EffectText: UpgradeEffectText(u),
                Installed: truck.UpgradeIds.Contains(u.Id),
                Fits: u.Fits(type.EffectiveKind),
                Affordable: state.Cash >= u.Price)).ToList();

            fleet.Add(new FleetTruckView(
                Id: truck.Id,
                TypeId: truck.TypeId,
                Name: type.Name,
                Kind: type.EffectiveKind,
                Capacity: Math.Round(spec.Capacity, 1),
                SpeedKmPerDay: Math.Round(spec.SpeedKmPerDay, 1),
                UpkeepPerDay: Math.Round(spec.UpkeepPerDay, 1),
                FuelPerKm: Math.Round(spec.FuelPerKm, 3),
                MineYield: Math.Round(spec.MineYield, 1),
                Upgrades: truck.UpgradeIds
                    .Select(id => world.TruckUpgradesById.TryGetValue(id, out var u) ? u.Name : id)
                    .ToList(),
                ResaleValue: CaravanMath.ResaleValue(truck, world),
                CanSell: blocker is null,
                SellBlocker: blocker ?? "",
                Fittings: fittings));
        }

        return new StationView(
            Open: location is not null,
            Offers: location is null ? Array.Empty<TruckOfferView>() : BuildShipyard(world),
            Fleet: fleet,
            ResaleFraction: world.ResaleFraction);
    }

    private static string UpgradeEffectText(TruckUpgradeDef u)
    {
        var parts = new List<string>();
        if (Math.Abs(u.CapacityBonus) > 1e-9) parts.Add($"{(u.CapacityBonus > 0 ? "+" : "")}{u.CapacityBonus:0.#} hold");
        if (Math.Abs(u.SpeedMult - 1.0) > 1e-9) parts.Add($"{(u.SpeedMult - 1.0) * 100:+0;-0}% speed");
        if (Math.Abs(u.FuelMult - 1.0) > 1e-9) parts.Add($"{(u.FuelMult - 1.0) * 100:+0;-0}% fuel");
        if (Math.Abs(u.UpkeepDelta) > 1e-9) parts.Add($"{u.UpkeepDelta:+0.#;-0.#} cr/day upkeep");
        if (Math.Abs(u.MineYieldBonus) > 1e-9) parts.Add($"+{u.MineYieldBonus:0.#} u/day mining");
        return parts.Count == 0 ? u.Blurb : string.Join(" · ", parts);
    }

    /// <summary>
    /// The board here and every contract held. Offers are derived from the seed, so
    /// this is a pure read; the hold is checked against each line so the page can say
    /// "deliverable" without knowing what a lot is.
    /// </summary>
    private static ContractsView BuildContracts(GameState state, WorldData world, City? location)
    {
        var cfg = world.Contracts;
        var board = new List<ContractOfferView>();

        if (location is not null)
        {
            foreach (var offer in Contracts.BoardFor(world, location, state.Seed, state.Day))
            {
                board.Add(new ContractOfferView(
                    Id: offer.Id,
                    CityId: offer.CityId,
                    CityName: location.Name,
                    KindId: offer.KindId,
                    KindName: offer.KindName,
                    Blurb: offer.Blurb,
                    Lines: ContractLines(state, world, offer),
                    MinGrade: offer.MinGrade,
                    Reward: offer.Reward,
                    Standing: offer.Standing,
                    DeadlineDays: offer.DeadlineDays,
                    Held: state.Contract(offer.Id) is not null,
                    Closed: state.ContractsClosed.Contains(offer.Id)));
            }
        }

        var held = new List<HeldContractView>();
        foreach (var contract in state.Contracts)
        {
            var offer = Contracts.Resolve(world, state.Seed, contract.Id);
            if (offer is null) continue;
            var city = world.CitiesById.TryGetValue(contract.CityId, out var c) ? c : null;
            var here = location is not null && location.Id == contract.CityId;
            var blocker = Contracts.DeliveryBlocker(state, world, offer);
            var reason = !here ? $"Settled in {city?.Name ?? contract.CityId}." : blocker ?? "";

            held.Add(new HeldContractView(
                Id: contract.Id,
                CityId: contract.CityId,
                CityName: city?.Name ?? contract.CityId,
                KindName: offer.KindName,
                Blurb: offer.Blurb,
                Lines: ContractLines(state, world, offer),
                MinGrade: offer.MinGrade,
                Reward: offer.Reward,
                Standing: offer.Standing,
                Deadline: contract.Deadline,
                DaysLeft: Math.Max(0, contract.Deadline - state.Day),
                Here: here,
                Deliverable: here && blocker is null,
                Blocker: reason));
        }

        return new ContractsView(
            BoardCity: location?.Name ?? "",
            RefreshInDays: Contracts.DaysUntilRefresh(state.Day, cfg),
            Board: board,
            Held: held);
    }

    private static List<ContractLineView> ContractLines(GameState state, WorldData world, ContractOffer offer)
    {
        var lines = new List<ContractLineView>(offer.Lines.Count);
        foreach (var line in offer.Lines)
        {
            var good = world.Good(line.GoodId);
            state.Caravan.Cargo.TryGetValue(line.GoodId, out var lot);
            var heldUnits = lot?.Units ?? 0;
            var q = lot?.Quality ?? 0;
            lines.Add(new ContractLineView(
                GoodId: line.GoodId,
                Name: good.Name,
                TierColor: world.TierOf(good).Color,
                Units: line.Units,
                Held: heldUnits,
                HeldQuality: Math.Round(q, 1),
                Satisfied: heldUnits >= line.Units && (offer.MinGrade <= 0 || q + 1e-9 >= offer.MinGrade)));
        }
        return lines;
    }

    /// <summary>
    /// The expo here. Schedule and theme are derived from the seed; the stall and the
    /// report are state. Suggested asks are what a typical buyer would just pay, so the
    /// player has a number to argue up from.
    /// </summary>
    private static ExpoView BuildExpo(GameState state, WorldData world, City city)
    {
        var cfg = world.Expos;
        var running = Expos.Running(world, city, state.Seed, state.Day);
        var next = running ?? Expos.Next(world, city, state.Seed, state.Day);
        var theme = next?.Theme;
        var buff = theme is null ? 0.0 : Expos.Buff(cfg, theme);
        var passHeld = next is not null && state.ExpoPasses.Contains(next.PassId);
        var eco = world.Config.Economy;

        var listings = new List<ExpoListingView>();
        foreach (var good in world.Goods)
        {
            if (!state.Caravan.Cargo.TryGetValue(good.Id, out var lot) || lot.Units <= 0) continue;
            var makes = Expos.CityMakes(city, good.Id);
            var covered = theme is not null && Expos.ThemeCovers(theme, good);
            var reason = makes ? $"{city.Name} makes this; not allowed on a stall here."
                : theme is null ? "No expo scheduled."
                : !covered ? $"Not in this expo's theme."
                : running is null ? "The expo has not opened yet."
                : !passHeld ? "Buy a pass to list it."
                : "";
            state.Caravan.ExpoAsks.TryGetValue(good.Id, out var ask);
            // A shade under the typical buyer, so "try N" clears most of the hall rather than half of it.
            var suggested = theme is null
                ? 0
                : (long)Math.Round(Expos.TypicalWillingness(cfg, good, buff, lot.Quality, world.Quality) * (1.0 - cfg.Noise * 0.5));
            var profile = city.Market[good.Id];
            var stock = state.StockOf(city.Id, good.Id);
            var terms = CrewMath.Terms(state.Caravan, world, good.Category);
            var mult = WorldEvents.PriceMultiplier(state, world, city.Id, good.Id);
            var localSell = Economy.SellUnitPrice(good, profile, stock, eco, terms, mult) * QualityMath.SellMultiplier(lot.Quality, world.Quality);

            listings.Add(new ExpoListingView(
                GoodId: good.Id,
                Name: good.Name,
                Category: world.CategoryName(good.Category),
                TierColor: world.TierOf(good).Color,
                Held: lot.Units,
                Quality: Math.Round(lot.Quality, 1),
                Ask: ask,
                Suggested: suggested,
                LocalSell: Math.Round(localSell, 1),
                CityMakes: makes,
                Covered: covered,
                Eligible: reason.Length == 0,
                Reason: reason));
        }

        ExpoReportView? report = null;
        if (state.LastExpoDay is { } day && day.CityId == city.Id)
        {
            report = new ExpoReportView(
                Day: day.Day,
                Revenue: day.Revenue,
                UnitsSold: day.UnitsSold,
                Buyers: day.Visits.Count,
                Visits: day.Visits.Select(v => new ExpoVisitView(
                    v.Sequence,
                    v.Buyer,
                    v.GoodId,
                    world.GoodsById.TryGetValue(v.GoodId, out var g) ? g.Name : "",
                    v.Outcome,
                    v.Units,
                    v.Price,
                    v.Remark)).ToList());
        }

        return new ExpoView(
            CityName: city.Name,
            Running: running is not null,
            ThemeId: theme?.Id ?? "",
            Title: theme?.Title ?? "",
            Categories: theme is null ? Array.Empty<string>() : theme.Categories.Select(world.CategoryName).ToList(),
            StartsIn: next is null ? 0 : Math.Max(0, next.StartDay - state.Day),
            DaysLeft: running is null ? 0 : Math.Max(0, running.EndDay - state.Day),
            DurationDays: theme?.DurationDays ?? 0,
            Fee: Expos.Fee(cfg, city),
            PassHeld: passHeld,
            Buff: Math.Round(buff, 3),
            BuyersPerDay: theme is null ? 0 : Expos.BuyersPerDay(cfg, city, buff),
            Listings: listings,
            Report: report);
    }
}
