using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Commands;

/// <summary>
/// The single place game state is allowed to change. Validates first and mutates only
/// once a command is known to be legal, so a rejected command leaves state untouched.
/// </summary>
public static class CommandProcessor
{
    public static CommandResult Execute(GameState state, WorldData world, Command command) => command switch
    {
        BuyCommand c => Buy(state, world, c),
        SellCommand c => Sell(state, world, c),
        DepartCommand c => Depart(state, world, c),
        WaitCommand c => Wait(state, world, c),
        BuyTruckCommand c => BuyTruck(state, world, c),
        SellTruckCommand c => SellTruck(state, world, c),
        UpgradeTruckCommand c => UpgradeTruck(state, world, c),
        BuyGearCommand c => BuyGear(state, world, c),
        HireCrewCommand c => HireCrew(state, world, c),
        DismissCrewCommand c => DismissCrew(state, world, c),
        AssignCrewCommand c => AssignCrew(state, world, c),
        CityFavorCommand c => Favor(state, world, c),
        RentWarehouseCommand => RentWarehouse(state, world),
        WarehouseDepositCommand c => WarehouseDeposit(state, world, c),
        WarehouseWithdrawCommand c => WarehouseWithdraw(state, world, c),
        SetWarehouseSellCommand c => SetWarehousePrice(state, world, c.GoodId, c.Price, sell: true),
        SetWarehouseProcureCommand c => SetWarehousePrice(state, world, c.GoodId, c.Price, sell: false),
        AcceptContractCommand c => AcceptContract(state, world, c),
        DeliverContractCommand c => DeliverContract(state, world, c),
        ExpoRegisterCommand => ExpoRegister(state, world),
        ExpoListCommand c => ExpoList(state, world, c),
        _ => CommandResult.Fail($"Unsupported command '{command.GetType().Name}'.")
    };

    private static CommandResult Buy(GameState state, WorldData world, BuyCommand cmd)
    {
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail("The convoy is on the road; it cannot trade until it arrives.");

        var cityId = state.Caravan.LocationId;
        if (cityId is null) return CommandResult.Fail("The convoy has no location.");

        if (cmd.Units <= 0) return CommandResult.Fail("Quantity must be at least 1.");
        if (!world.GoodsById.TryGetValue(cmd.GoodId, out var good))
            return CommandResult.Fail($"No such commodity '{cmd.GoodId}'.");

        var city = world.City(cityId);
        var profile = city.Market[good.Id];
        var eco = world.Config.Economy;

        // Higher grades are for people the city knows. The gate reads the total across
        // every segment, so any road to the city's regard opens the shelf.
        var tier = world.TierOf(good);
        var regard = Standing.Of(state, cityId);
        if (!Standing.TierOpen(tier, regard))
        {
            var rank = Standing.Rank(world.Standing, regard)?.Name ?? "stranger";
            return CommandResult.Fail(
                $"{city.Name} does not sell {tier.Name.ToLowerInvariant()} goods to a {rank.ToLowerInvariant()}: " +
                $"standing {regard:0} of {tier.MinStanding:0} needed.");
        }

        var needed = cmd.Units * good.UnitVolume;
        var free = CaravanMath.FreeVolume(state.Caravan, world);
        if (needed > free + 1e-9)
            return CommandResult.Fail(
                $"Not enough hold space: need {needed:0.#} of {free:0.#} free.");

        var stock = state.StockOf(cityId, good.Id);

        // You can only be sold what is on the shelf. Goods other caravans have dumped
        // here sit in the city's intake and are not for sale yet.
        var onTheShelf = Economy.UnitsOnTheShelf(stock, eco);
        if (cmd.Units > onTheShelf)
            return CommandResult.Fail($"Only {onTheShelf:N0} {good.Name} on the shelf at {city.Name}.");

        var terms = CrewMath.Terms(state.Caravan, world, good.Category);
        var eventMult = WorldEvents.PriceMultiplier(state, world, cityId, good.Id);
        var quote = Economy.QuoteBuy(good, profile, stock, cmd.Units, eco, terms, eventMult);

        // The shop charges for the grade you walk out with. The same multiplier sits on
        // the sell side, so a finer crate is worth more everywhere and free nowhere:
        // cherry-picking cannot turn a shelf into an in-place income.
        var knowledge = CrewMath.SelectionFactor(state.Caravan.Crew, world.Crew, good.Category);
        var (selected, resulting) = QualityMath.Take(stock, onTheShelf, cmd.Units, knowledge, world.Quality, quote.ResultingStock);
        var total = (long)Math.Round(quote.Total * QualityMath.SellMultiplier(selected, world.Quality));

        if (total > state.Cash)
            return CommandResult.Fail($"Not enough credits: {total:N0} needed, {state.Cash:N0} held.");

        state.Cash -= total;
        state.SetStock(cityId, good.Id, resulting);

        if (!state.Caravan.Cargo.TryGetValue(good.Id, out var lot))
            state.Caravan.Cargo[good.Id] = lot = new CargoLot();
        lot.Add(cmd.Units, total, selected);

        TradeXp.Grant(state, world, good.Category, CrewLever.Buy, cmd.Units);

        var grade = QualityMath.IsSTier(selected, world.Quality)
            ? $"S-tier {selected:0.#}%"
            : $"{selected:0.#}% grade";

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Trade,
                $"Bought {cmd.Units:N0} {good.Name} at {city.Name} for {total:N0} cr " +
                $"({(double)total / cmd.Units:0.#}/unit, {grade}).")
        });
    }

    private static CommandResult Sell(GameState state, WorldData world, SellCommand cmd)
    {
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail("The convoy is on the road; it cannot trade until it arrives.");

        var cityId = state.Caravan.LocationId;
        if (cityId is null) return CommandResult.Fail("The convoy has no location.");

        if (cmd.Units <= 0) return CommandResult.Fail("Quantity must be at least 1.");
        if (!world.GoodsById.TryGetValue(cmd.GoodId, out var good))
            return CommandResult.Fail($"No such commodity '{cmd.GoodId}'.");

        if (!state.Caravan.Cargo.TryGetValue(good.Id, out var lot) || lot.Units < cmd.Units)
            return CommandResult.Fail(
                $"Only {state.Caravan.Held(good.Id):N0} {good.Name} in the hold.");

        var city = world.City(cityId);
        var profile = city.Market[good.Id];
        var eco = world.Config.Economy;

        var stock = state.StockOf(cityId, good.Id);
        var terms = CrewMath.Terms(state.Caravan, world, good.Category);
        var eventMult = WorldEvents.PriceMultiplier(state, world, cityId, good.Id);
        var quote = Economy.QuoteSell(good, profile, stock, cmd.Units, eco, terms, eventMult);
        var sellMult = QualityMath.SellMultiplier(lot.Quality, world.Quality);
        var total = (long)Math.Round(quote.Total * sellMult);

        // Cost basis leaves at the weighted average, so profit reporting stays honest
        // across partial sales of a lot built up over several purchases.
        var costBasis = (long)Math.Round(lot.AverageCost * cmd.Units);

        // Read before the write: what this sale relieves is judged against the shortage
        // that was running when the goods came off the truck.
        var reliefPerUnit = WorldEvents.ReliefPerUnit(state, world, cityId, good.Id);

        state.Cash += total;
        state.SetStock(cityId, good.Id, quote.ResultingStock);

        lot.Units -= cmd.Units;
        lot.TotalCost = Math.Max(0, lot.TotalCost - costBasis);
        if (lot.Units == 0)
        {
            state.Caravan.Cargo.Remove(good.Id);
            state.Caravan.ExpoAsks.Remove(good.Id);
        }

        TradeXp.Grant(state, world, good.Category, CrewLever.Sell, cmd.Units);

        var profit = total - costBasis;
        var verdict = profit >= 0 ? $"profit {profit:N0}" : $"loss {Math.Abs(profit):N0}";
        var grade = QualityMath.IsSTier(lot.Quality, world.Quality) ? " S-tier" : "";

        var events = new List<GameEvent>
        {
            new(state.Day, GameEventKind.Trade,
                $"Sold {cmd.Units:N0} {good.Name}{grade} into {city.Name}'s stores for {total:N0} cr ({verdict}).")
        };

        var standingCfg = world.Standing;

        if (reliefPerUnit > 0)
        {
            var citizens = standingCfg.SegmentOr("citizens");
            var landed = Standing.Grant(state, standingCfg, cityId, citizens, reliefPerUnit * cmd.Units);
            if (landed > 0)
            {
                events.Add(new GameEvent(state.Day, GameEventKind.Standing,
                    $"{city.Name}'s streets remember the {good.Name}: citizen standing +{landed:0.#}."));
            }
        }

        if (standingCfg.TradersPerThousandCr > 0 && total > 0)
        {
            var traders = standingCfg.SegmentOr("traders");
            var landed = Standing.Grant(state, standingCfg, cityId, traders, standingCfg.TradersPerThousandCr * total / 1000.0);
            if (landed >= 0.05)
            {
                events.Add(new GameEvent(state.Day, GameEventKind.Standing,
                    $"The {city.Name} houses note the volume: traders standing +{landed:0.#}."));
            }
        }

        GrantDuePermits(state, world, city, events);

        return CommandResult.Success(events);
    }

    private static CommandResult Depart(GameState state, WorldData world, DepartCommand cmd)
    {
        if (!MapMath.TryResolve(state, world, cmd.ToCityId, out var dest))
            return CommandResult.Fail($"No such destination '{cmd.ToCityId}'.");

        var fromCell = MapMath.Position(state, world);
        if (dest.Cell.Col == fromCell.Col && dest.Cell.Row == fromCell.Row)
        {
            if (state.Caravan.Travel is null)
                return CommandResult.Fail("The convoy is already there.");

            MapMath.Park(state, world, fromCell);
            return CommandResult.Success(Array.Empty<GameEvent>());
        }

        // From the convoy's exact position to the destination point. A map click
        // ("s<sc>,<sr>") may land just off walkable ground and gets a gentle snap;
        // a named cell, city or claim must be reached as-is.
        var (startX, startY) = MapMath.PositionPoint(state, world);
        var (endX, endY) = dest.Point;
        var snapEnd = cmd.ToCityId is { Length: > 1 } && cmd.ToCityId[0] == 's';

        var plan = MapMath.PathfindFine(state.Caravan, world, (startX, startY), (endX, endY), snapEnd);
        if (plan is null)
            return CommandResult.Fail($"No route the convoy can travel reaches {dest.Name}.");

        var from = DescribeHere(state, world);
        var reroute = state.Caravan.Travel is not null;

        state.Caravan.Travel = new TravelState
        {
            FromId = from.Id,
            ToId = dest.Id,
            FromKind = from.Kind,
            ToKind = dest.Kind,
            FromName = from.Name,
            ToName = dest.Name,
            TotalDays = plan.Days,
            DaysRemaining = plan.Days,
            KmPerDay = plan.DistanceKm / Math.Max(1, plan.Days),
            FuelPerDay = plan.Fuel / Math.Max(1, plan.Days),
            ToCellId = dest.Cell.Id,
            Waypoints = plan.Path.ToList()
        };
        state.Caravan.LocationId = null;
        state.Caravan.SiteId = null;
        state.Caravan.CellId = null;

        // The stall does not travel: leaving town takes every listing down.
        state.Caravan.ExpoAsks.Clear();

        if (dest.Kind == "cell")
            return CommandResult.Success(Array.Empty<GameEvent>());

        var verb = reroute ? "Rerouted toward" : "Departed for";
        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Travel,
                $"{verb} {dest.Name} via {plan.Layer}: " +
                $"{plan.DistanceKm:N0} km, {plan.Days} day(s), about {plan.Fuel:N0} cr of fuel.")
        });
    }

    private static MapDestination DescribeHere(GameState state, WorldData world)
    {
        if (state.Caravan.LocationId is { } cityId)
        {
            var city = world.City(cityId);
            return new MapDestination(city.Id, "city", city.Name, world.Map.CellOfCity(city.Id));
        }

        if (state.Caravan.SiteId is { } siteId && state.Site(siteId) is { } site)
        {
            var good = world.GoodsById.TryGetValue(site.GoodId, out var g) ? g.Name : site.GoodId;
            return new MapDestination(site.Id, "site", $"{good} deposit", world.Map[site.Col, site.Row]);
        }

        var cell = MapMath.Position(state, world);
        return new MapDestination(cell.Id, "cell", "open country", cell);
    }

    private static CommandResult Wait(GameState state, WorldData world, WaitCommand cmd)
    {
        if (cmd.Days <= 0) return CommandResult.Fail("Days must be at least 1.");
        if (cmd.Days > 365) return CommandResult.Fail("Cannot skip more than a year at once.");

        var events = new List<GameEvent>();
        for (var i = 0; i < cmd.Days; i++)
        {
            DayTick.Advance(state, world, events);
        }

        return CommandResult.Success(events);
    }

    /// <summary>
    /// Sign on a candidate from the city's current recruitment pool.
    ///
    /// The pool is re-derived here from the seed rather than read from anywhere the
    /// front-end could have touched, so a hire can only ever be for somebody the
    /// simulation itself is offering today.
    /// </summary>
    private static CommandResult HireCrew(GameState state, WorldData world, HireCrewCommand cmd)
    {
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail("Nobody signs on mid-road; hire in a city.");

        var cityId = state.Caravan.LocationId;
        if (cityId is null) return CommandResult.Fail("The convoy has no location.");

        var crewConfig = world.Crew;

        if (state.Caravan.Crew.Count >= crewConfig.CrewCapacity)
        {
            return CommandResult.Fail(
                $"The convoy already carries {crewConfig.CrewCapacity} crew; pay somebody off first.");
        }

        if (state.RecruitedIds.Contains(cmd.CandidateId))
            return CommandResult.Fail("That hand has already taken a contract.");

        var city = world.City(cityId);
        var pool = Recruitment.PoolFor(world, city, state.Seed, state.Day);

        var candidate = pool.FirstOrDefault(c => c.Id == cmd.CandidateId);
        if (candidate is null)
            return CommandResult.Fail($"Nobody by that reference is at the {city.Name} recruitment centre.");

        if (state.Cash < candidate.SigningFee)
        {
            return CommandResult.Fail(
                $"Not enough credits: {candidate.SigningFee:N0} signing fee, {state.Cash:N0} held.");
        }

        state.Cash -= candidate.SigningFee;
        state.RecruitedIds.Add(candidate.Id);

        // A hand signs on to the post their trade implies: a broker goes to the counter,
        // a scout to information. The player can move them afterwards.
        var post = crewConfig.DefaultPost(crewConfig.Role(candidate.RoleId));

        state.Caravan.Crew.Add(new CrewMember
        {
            Id = candidate.Id,
            Name = candidate.Name,
            RoleId = candidate.RoleId,
            PostId = post,
            DailyWage = candidate.DailyWage,
            HiredDay = state.Day,
            HiredAtCityId = cityId,
            Skills = new Dictionary<string, int>(candidate.Skills),
            Knowledge = new Dictionary<string, double>(candidate.Knowledge),
            TraitIds = new List<string>(candidate.TraitIds)
        });

        var postDef = crewConfig.Post(post);
        var posted = postDef is null ? "" : $" Posted to {postDef.Name.ToLowerInvariant()}.";

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Crew,
                $"{candidate.Name} signed on at {city.Name} as {candidate.RoleName}: " +
                $"{candidate.SigningFee:N0} cr down, {candidate.DailyWage:N0} cr/day.{posted}")
        });
    }

    /// <summary>
    /// Move a hand between posts. Costs nothing and works on the road, because it is a
    /// matter of who sits where, not of what the city will do for you.
    /// </summary>
    private static CommandResult AssignCrew(GameState state, WorldData world, AssignCrewCommand cmd)
    {
        var member = state.Caravan.Crew.FirstOrDefault(c => c.Id == cmd.CrewId);
        if (member is null) return CommandResult.Fail("Nobody by that reference is on the payroll.");

        var postId = cmd.PostId?.Trim() ?? "";
        var post = world.Crew.Post(postId);
        if (postId.Length > 0 && post is null)
            return CommandResult.Fail($"No such post '{cmd.PostId}'.");

        if (string.Equals(member.PostId, postId, StringComparison.Ordinal))
        {
            return CommandResult.Fail(post is null
                ? $"{member.Name} already holds no post."
                : $"{member.Name} is already on {post.Name.ToLowerInvariant()}.");
        }

        member.PostId = postId;

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Crew, post is null
                ? $"{member.Name} stood down from every post."
                : $"{member.Name} posted to {post.Name.ToLowerInvariant()}.")
        });
    }

    private static CommandResult DismissCrew(GameState state, WorldData world, DismissCrewCommand cmd)
    {
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail("Nobody is put off the convoy mid-road; pay them off in a city.");

        var member = state.Caravan.Crew.FirstOrDefault(c => c.Id == cmd.CrewId);
        if (member is null) return CommandResult.Fail("Nobody by that reference is on the payroll.");

        var severance = member.DailyWage * Math.Max(0, world.Crew.SeveranceDays);
        if (state.Cash < severance)
            return CommandResult.Fail($"Not enough credits: {severance:N0} severance, {state.Cash:N0} held.");

        state.Cash -= severance;
        state.Caravan.Crew.Remove(member);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Crew,
                $"{member.Name} paid off for {severance:N0} cr. " +
                $"Payroll is now {CrewMath.DailyWages(state.Caravan.Crew):N0} cr/day.")
        });
    }

    private static CommandResult BuyTruck(GameState state, WorldData world, BuyTruckCommand cmd)
    {
        var parked = RequireParkedCity(state, out _, "Trucks can only be bought in a city.");
        if (parked is not null) return parked;

        if (!world.TrucksById.TryGetValue(cmd.TruckTypeId, out var truck))
            return CommandResult.Fail($"No such truck type '{cmd.TruckTypeId}'.");

        if (state.Cash < truck.Price)
            return CommandResult.Fail($"Not enough credits: {truck.Price:N0} needed, {state.Cash:N0} held.");

        state.Cash -= truck.Price;
        var instance = CaravanMath.NewTruck(state.Caravan, truck.Id);
        state.Caravan.Trucks.Add(instance);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Expense,
                $"Acquired a {truck.Name} for {truck.Price:N0} cr.")
        });
    }

    /// <summary>
    /// Sell a vehicle back to the station. The convoy must still be able to move and
    /// still be able to carry what is in the hold; the station pays the resale fraction
    /// of the vehicle and everything bolted to it.
    /// </summary>
    private static CommandResult SellTruck(GameState state, WorldData world, SellTruckCommand cmd)
    {
        var parked = RequireParkedCity(state, out _, "Trucks can only be sold at a city station.");
        if (parked is not null) return parked;

        var truck = state.Truck(cmd.TruckId);
        if (truck is null) return CommandResult.Fail("No vehicle by that reference is in the convoy.");

        var blocker = CaravanMath.SellBlocker(state.Caravan, world, truck);
        if (blocker is not null) return CommandResult.Fail(blocker);

        var value = CaravanMath.ResaleValue(truck, world);
        var type = world.Truck(truck.TypeId);

        state.Cash += value;
        state.Caravan.Trucks.Remove(truck);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Trade,
                $"Sold a {type.Name} back to the station for {value:N0} cr.")
        });
    }

    private static CommandResult UpgradeTruck(GameState state, WorldData world, UpgradeTruckCommand cmd)
    {
        var parked = RequireParkedCity(state, out _, "Fittings are done at a city station.");
        if (parked is not null) return parked;

        var truck = state.Truck(cmd.TruckId);
        if (truck is null) return CommandResult.Fail("No vehicle by that reference is in the convoy.");

        if (!world.TruckUpgradesById.TryGetValue(cmd.UpgradeId, out var upgrade))
            return CommandResult.Fail($"The station does not stock '{cmd.UpgradeId}'.");

        var type = world.Truck(truck.TypeId);
        if (!upgrade.Fits(type.EffectiveKind))
            return CommandResult.Fail($"{upgrade.Name} does not fit a {type.Name}.");

        if (truck.UpgradeIds.Contains(upgrade.Id))
            return CommandResult.Fail($"That {type.Name} already carries {upgrade.Name}.");

        if (state.Cash < upgrade.Price)
            return CommandResult.Fail($"Not enough credits: {upgrade.Price:N0} needed, {state.Cash:N0} held.");

        state.Cash -= upgrade.Price;
        truck.UpgradeIds.Add(upgrade.Id);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Expense,
                $"Fitted {upgrade.Name} to the {type.Name} for {upgrade.Price:N0} cr.")
        });
    }

    private static CommandResult BuyGear(GameState state, WorldData world, BuyGearCommand cmd)
    {
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail("Tools are sold in a city, not on the road.");

        if (state.Caravan.LocationId is null)
            return CommandResult.Fail("Tools are sold in a city.");

        if (!world.GearById.TryGetValue(cmd.GearId, out var gear))
            return CommandResult.Fail($"No such tool '{cmd.GearId}'.");

        if (state.Cash < gear.Price)
            return CommandResult.Fail($"Not enough credits: {gear.Price:N0} needed, {state.Cash:N0} held.");

        var free = CaravanMath.FreeVolume(state.Caravan, world);
        if (gear.Volume > free + 1e-9)
            return CommandResult.Fail($"Not enough hold space: need {gear.Volume:0.#} of {free:0.#} free.");

        state.Cash -= gear.Price;
        state.Caravan.GearIds.Add(gear.Id);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Expense,
                $"Bought {gear.Name} for {gear.Price:N0} cr.")
        });
    }

    /// <summary>
    /// Court the city. The action is looked up from content rather than branched on
    /// here, so donate / invest / aid share one command and a fourth gesture is a JSON
    /// line. Each action names the segment its standing lands in.
    /// </summary>
    private static CommandResult Favor(GameState state, WorldData world, CityFavorCommand cmd)
    {
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail("The governor's office is back in a city.");

        var cityId = state.Caravan.LocationId;
        if (cityId is null) return CommandResult.Fail("The convoy has no location.");

        var action = world.Standing.Action(cmd.ActionId);
        if (action is null)
            return CommandResult.Fail($"The governor's office does not take '{cmd.ActionId}' as a petition.");

        if (state.Cash < action.Cost)
        {
            return CommandResult.Fail(
                $"Not enough credits: {action.Cost:N0} needed, {state.Cash:N0} held.");
        }

        var city = world.City(cityId);
        var standingCfg = world.Standing;
        var segment = standingCfg.SegmentOr(action.SegmentId);
        var totalBefore = Standing.Of(state, cityId);
        var standingGain = Math.Min(action.Standing, Standing.Room(state, standingCfg, cityId, segment));
        var movesVital = !string.IsNullOrWhiteSpace(action.VitalId) && action.VitalDelta != 0.0;
        var shipsStock = action.StockPerGood > 0;

        if (standingGain <= 0 && !movesVital && !shipsStock)
        {
            return CommandResult.Fail(
                $"{city.GovernorTitle} {city.GovernorName} already holds you in the highest regard.");
        }

        state.Cash -= action.Cost;
        Standing.Grant(state, standingCfg, cityId, segment, standingGain);
        var totalAfter = Standing.Of(state, cityId);

        var events = new List<GameEvent>
        {
            new(state.Day, GameEventKind.Standing,
                $"{action.Name} in {city.Name}: {action.Cost:N0} cr to {city.GovernorTitle} {city.GovernorName}. " +
                $"Standing {totalBefore:0} → {totalAfter:0}.")
        };

        if (movesVital)
        {
            var def = world.CityStats.Vital(action.VitalId);
            if (def is not null)
            {
                var before = CityStats.Vital(state, city, def.Id);
                var after = Math.Clamp(before + action.VitalDelta, def.Min, def.Max);
                state.SetVital(cityId, def.Id, after);
                events.Add(new GameEvent(state.Day, GameEventKind.Standing,
                    $"{city.Name}'s {def.Name} {before:0.#} → {after:0.#}."));
            }
        }

        if (shipsStock && world.CityStats.Supplies.Count > 0)
        {
            var weakest = world.CityStats.Supplies
                .Select(s => (Def: s, Reading: CityStats.Supply(state, world, city, s)))
                .OrderBy(x => x.Reading.Index)
                .First();

            foreach (var goodId in weakest.Def.Goods)
            {
                if (!world.GoodsById.ContainsKey(goodId)) continue;
                var stock = state.StockOf(cityId, goodId);
                state.SetStock(cityId, goodId, stock with { In = stock.In + action.StockPerGood });
            }

            events.Add(new GameEvent(state.Day, GameEventKind.Standing,
                $"Shipped {action.StockPerGood:0.#} of each {weakest.Def.Name} good into {city.Name}'s intake."));
        }

        GrantDuePermits(state, world, city, events);

        return CommandResult.Success(events);
    }

    /// <summary>Permits fall out of the total, whichever segment moved it.</summary>
    private static void GrantDuePermits(GameState state, WorldData world, City city, List<GameEvent> events)
    {
        var total = Standing.Of(state, city.Id);
        foreach (var permit in Standing.Due(world.Standing, total))
        {
            if (state.HasPermit(city.Id, permit.Id)) continue;
            state.GrantPermit(city.Id, permit.Id);
            events.Add(new GameEvent(state.Day, GameEventKind.Standing,
                $"{city.GovernorTitle} {city.GovernorName} granted a {permit.Name.ToLowerInvariant()} in {city.Name}."));
        }
    }

    private static CommandResult? RequireParkedCity(GameState state, out string cityId, string? roadMessage = null)
    {
        cityId = "";
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail(roadMessage ?? "The convoy is on the road.");
        if (state.Caravan.LocationId is null)
            return CommandResult.Fail(roadMessage ?? "The convoy has no location.");
        cityId = state.Caravan.LocationId;
        return null;
    }

    private static CommandResult RentWarehouse(GameState state, WorldData world)
    {
        var parked = RequireParkedCity(state, out var cityId);
        if (parked is not null) return parked;

        if (state.Warehouses.ContainsKey(cityId))
            return CommandResult.Fail("The house already rents a storeroom here.");

        var cfg = world.Config.Warehouse;
        if (state.Cash < cfg.RentCost)
            return CommandResult.Fail($"Not enough credits: {cfg.RentCost:N0} needed, {state.Cash:N0} held.");

        state.Cash -= cfg.RentCost;
        state.Warehouses[cityId] = new WarehouseState { CityId = cityId };
        var city = world.City(cityId);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Expense,
                $"Rented a storeroom in {city.Name} for {cfg.RentCost:N0} cr " +
                $"({cfg.Capacity:0.#} vol, {cfg.DailyRent:N0} cr/day).")
        });
    }

    private static CommandResult WarehouseDeposit(GameState state, WorldData world, WarehouseDepositCommand cmd)
    {
        var parked = RequireParkedCity(state, out var cityId);
        if (parked is not null) return parked;
        if (!state.Warehouses.TryGetValue(cityId, out var warehouse))
            return CommandResult.Fail("The house does not rent a storeroom here.");
        if (cmd.Units <= 0) return CommandResult.Fail("Quantity must be at least 1.");
        if (!world.GoodsById.TryGetValue(cmd.GoodId, out var good))
            return CommandResult.Fail($"No such commodity '{cmd.GoodId}'.");
        if (!state.Caravan.Cargo.TryGetValue(good.Id, out var lot) || lot.Units < cmd.Units)
            return CommandResult.Fail($"Only {state.Caravan.Held(good.Id):N0} {good.Name} in the hold.");

        var volume = cmd.Units * good.UnitVolume;
        if (volume > WarehouseMath.FreeVolume(warehouse, world) + 1e-9)
            return CommandResult.Fail("The storeroom has no room for that.");

        if (!warehouse.Stock.TryGetValue(good.Id, out var stored))
            warehouse.Stock[good.Id] = stored = new CargoLot();

        var costBasis = (long)Math.Round(lot.AverageCost * cmd.Units);
        stored.Add(cmd.Units, costBasis, lot.Quality);

        lot.Units -= cmd.Units;
        lot.TotalCost = Math.Max(0, lot.TotalCost - costBasis);
        if (lot.Units == 0)
        {
            state.Caravan.Cargo.Remove(good.Id);
            state.Caravan.ExpoAsks.Remove(good.Id);
        }

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Trade,
                $"Deposited {cmd.Units:N0} {good.Name} into the {world.City(cityId).Name} storeroom.")
        });
    }

    private static CommandResult WarehouseWithdraw(GameState state, WorldData world, WarehouseWithdrawCommand cmd)
    {
        var parked = RequireParkedCity(state, out var cityId);
        if (parked is not null) return parked;
        if (!state.Warehouses.TryGetValue(cityId, out var warehouse))
            return CommandResult.Fail("The house does not rent a storeroom here.");
        if (cmd.Units <= 0) return CommandResult.Fail("Quantity must be at least 1.");
        if (!world.GoodsById.TryGetValue(cmd.GoodId, out var good))
            return CommandResult.Fail($"No such commodity '{cmd.GoodId}'.");
        if (!warehouse.Stock.TryGetValue(good.Id, out var stored) || stored.Units < cmd.Units)
            return CommandResult.Fail($"Only {warehouse.Held(good.Id):N0} {good.Name} in the storeroom.");

        var volume = cmd.Units * good.UnitVolume;
        if (volume > CaravanMath.FreeVolume(state.Caravan, world) + 1e-9)
            return CommandResult.Fail("The hold has no room for that.");

        if (!state.Caravan.Cargo.TryGetValue(good.Id, out var lot))
            state.Caravan.Cargo[good.Id] = lot = new CargoLot();

        var costBasis = (long)Math.Round(stored.AverageCost * cmd.Units);
        lot.Add(cmd.Units, costBasis, stored.Quality);

        stored.Units -= cmd.Units;
        stored.TotalCost = Math.Max(0, stored.TotalCost - costBasis);
        if (stored.Units == 0) warehouse.Stock.Remove(good.Id);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Trade,
                $"Withdrew {cmd.Units:N0} {good.Name} from the {world.City(cityId).Name} storeroom.")
        });
    }

    private static CommandResult SetWarehousePrice(
        GameState state, WorldData world, string goodId, long price, bool sell)
    {
        var parked = RequireParkedCity(state, out var cityId);
        if (parked is not null) return parked;
        if (!state.Warehouses.TryGetValue(cityId, out var warehouse))
            return CommandResult.Fail("The house does not rent a storeroom here.");
        if (price < 0) return CommandResult.Fail("Price cannot be negative.");
        if (!world.GoodsById.TryGetValue(goodId, out var good))
            return CommandResult.Fail($"No such commodity '{goodId}'.");

        var book = sell ? warehouse.AutoSellPrice : warehouse.AutoProcurePrice;
        if (price == 0) book.Remove(good.Id);
        else book[good.Id] = price;

        var verb = sell ? "auto-sell" : "auto-procure";
        var detail = price == 0 ? "order cleared" : $"at {price:N0} cr";
        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Info,
                $"{world.City(cityId).Name} storeroom {verb} {good.Name}: {detail}.")
        });
    }

    /// <summary>
    /// Take a contract off the board. The offer is re-derived from the seed, so it can
    /// only ever be one the city is showing today, and its terms cannot have been
    /// touched by anything the front-end sent.
    /// </summary>
    private static CommandResult AcceptContract(GameState state, WorldData world, AcceptContractCommand cmd)
    {
        var parked = RequireParkedCity(state, out var cityId, "Contracts are signed at a city board.");
        if (parked is not null) return parked;

        var offer = Contracts.Resolve(world, state.Seed, cmd.ContractId);
        if (offer is null || offer.CityId != cityId)
            return CommandResult.Fail("No such contract is on this city's board.");

        if (offer.Round != Contracts.RoundFor(state.Day, world.Contracts))
            return CommandResult.Fail("That offer has been taken down; the board has moved on.");

        if (state.Contract(offer.Id) is not null)
            return CommandResult.Fail("The house already holds that contract.");

        if (state.ContractsClosed.Contains(offer.Id))
            return CommandResult.Fail("That contract has already been settled or torn up.");

        var city = world.City(cityId);
        var deadline = state.Day + offer.DeadlineDays;
        state.Contracts.Add(new ContractState
        {
            Id = offer.Id,
            CityId = cityId,
            AcceptedDay = state.Day,
            Deadline = deadline
        });

        var lines = string.Join(", ", offer.Lines.Select(l => $"{l.Units:N0} {world.Good(l.GoodId).Name}"));
        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Standing,
                $"Signed {offer.KindName.ToLowerInvariant()} at {city.Name}: {lines} by day {deadline} for {offer.Reward:N0} cr.")
        });
    }

    private static CommandResult DeliverContract(GameState state, WorldData world, DeliverContractCommand cmd)
    {
        var parked = RequireParkedCity(state, out var cityId, "Contracts are settled in the city that issued them.");
        if (parked is not null) return parked;

        var held = state.Contract(cmd.ContractId);
        if (held is null) return CommandResult.Fail("The house holds no such contract.");

        if (held.CityId != cityId)
        {
            var issuer = world.CitiesById.TryGetValue(held.CityId, out var c) ? c.Name : held.CityId;
            return CommandResult.Fail($"That contract is settled in {issuer}, not here.");
        }

        var offer = Contracts.Resolve(world, state.Seed, held.Id);
        if (offer is null) return CommandResult.Fail("That contract's terms can no longer be read.");

        var blocker = Contracts.DeliveryBlocker(state, world, offer);
        if (blocker is not null) return CommandResult.Fail(blocker);

        var city = world.City(cityId);
        foreach (var line in offer.Lines)
        {
            var lot = state.Caravan.Cargo[line.GoodId];
            var costBasis = (long)Math.Round(lot.AverageCost * line.Units);
            lot.Units -= line.Units;
            lot.TotalCost = Math.Max(0, lot.TotalCost - costBasis);
            if (lot.Units == 0)
            {
                state.Caravan.Cargo.Remove(line.GoodId);
                state.Caravan.ExpoAsks.Remove(line.GoodId);
            }
        }

        state.Cash += offer.Reward;
        state.Contracts.Remove(held);
        state.ContractsClosed.Add(held.Id);

        var traders = world.Standing.SegmentOr("traders");
        var landed = Standing.Grant(state, world.Standing, cityId, traders, offer.Standing);

        var events = new List<GameEvent>
        {
            new(state.Day, GameEventKind.Trade,
                $"Delivered {offer.KindName.ToLowerInvariant()} at {city.Name} for {offer.Reward:N0} cr. Traders standing +{landed:0.#}.")
        };
        GrantDuePermits(state, world, city, events);
        return CommandResult.Success(events);
    }

    private static CommandResult ExpoRegister(GameState state, WorldData world)
    {
        var parked = RequireParkedCity(state, out var cityId, "Expo passes are sold at the hall door.");
        if (parked is not null) return parked;

        var city = world.City(cityId);
        var expo = Expos.Running(world, city, state.Seed, state.Day);
        if (expo is null) return CommandResult.Fail($"No expo is open in {city.Name} today.");

        if (state.ExpoPasses.Contains(expo.PassId))
            return CommandResult.Fail("The house already holds a pass for this expo.");

        var fee = Expos.Fee(world.Expos, city);
        if (state.Cash < fee)
            return CommandResult.Fail($"Not enough credits: {fee:N0} needed, {state.Cash:N0} held.");

        state.Cash -= fee;
        state.ExpoPasses.Add(expo.PassId);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Expense,
                $"Took a stall at the {expo.Theme.Title} in {city.Name} for {fee:N0} cr; {expo.EndDay - state.Day} day(s) left to trade.")
        });
    }

    private static CommandResult ExpoList(GameState state, WorldData world, ExpoListCommand cmd)
    {
        var parked = RequireParkedCity(state, out var cityId, "The stall is in the expo hall, not on the road.");
        if (parked is not null) return parked;

        if (cmd.Price < 0) return CommandResult.Fail("Price cannot be negative.");
        if (!world.GoodsById.TryGetValue(cmd.GoodId, out var good))
            return CommandResult.Fail($"No such commodity '{cmd.GoodId}'.");

        if (cmd.Price == 0)
        {
            if (!state.Caravan.ExpoAsks.Remove(good.Id))
                return CommandResult.Fail($"{good.Name} is not on the stall.");
            return CommandResult.Success(new[]
            {
                new GameEvent(state.Day, GameEventKind.Info, $"Took {good.Name} off the stall.")
            });
        }

        var city = world.City(cityId);
        var expo = Expos.Running(world, city, state.Seed, state.Day);
        if (expo is null) return CommandResult.Fail($"No expo is open in {city.Name} today.");
        if (!state.ExpoPasses.Contains(expo.PassId))
            return CommandResult.Fail("Buy a pass before setting out a stall.");

        if (state.Caravan.Held(good.Id) <= 0)
            return CommandResult.Fail($"No {good.Name} in the hold to list.");

        if (Expos.CityMakes(city, good.Id))
            return CommandResult.Fail($"{city.Name} makes {good.Name}; a city's own produce is never allowed on a stall at its own expo.");

        if (!Expos.ThemeCovers(expo.Theme, good))
            return CommandResult.Fail($"The {expo.Theme.Title} does not admit {world.CategoryName(good.Category).ToLowerInvariant()}.");

        state.Caravan.ExpoAsks[good.Id] = cmd.Price;

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Info,
                $"{good.Name} on the stall at {cmd.Price:N0} cr a unit.")
        });
    }
}
