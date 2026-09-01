using MechaTrader.Core.Events;
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
        HireCrewCommand c => HireCrew(state, world, c),
        DismissCrewCommand c => DismissCrew(state, world, c),
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

        var terms = CrewMath.Terms(state.Caravan, world);
        var quote = Economy.QuoteBuy(good, profile, stock, cmd.Units, eco, terms);

        if (quote.Total > state.Cash)
            return CommandResult.Fail($"Not enough credits: {quote.Total:N0} needed, {state.Cash:N0} held.");

        state.Cash -= quote.Total;
        state.SetStock(cityId, good.Id, quote.ResultingStock);

        if (!state.Caravan.Cargo.TryGetValue(good.Id, out var lot))
            state.Caravan.Cargo[good.Id] = lot = new CargoLot();

        lot.Units += cmd.Units;
        lot.TotalCost += quote.Total;

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Trade,
                $"Bought {cmd.Units:N0} {good.Name} at {city.Name} for {quote.Total:N0} cr " +
                $"({quote.UnitAverage:0.#}/unit).")
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
        var terms = CrewMath.Terms(state.Caravan, world);
        var quote = Economy.QuoteSell(good, profile, stock, cmd.Units, eco, terms);

        // Cost basis leaves at the weighted average, so profit reporting stays honest
        // across partial sales of a lot built up over several purchases.
        var costBasis = (long)Math.Round(lot.AverageCost * cmd.Units);

        state.Cash += quote.Total;
        state.SetStock(cityId, good.Id, quote.ResultingStock);

        lot.Units -= cmd.Units;
        lot.TotalCost = Math.Max(0, lot.TotalCost - costBasis);
        if (lot.Units == 0)
        {
            state.Caravan.Cargo.Remove(good.Id);
        }

        var profit = quote.Total - costBasis;
        var verdict = profit >= 0 ? $"profit {profit:N0}" : $"loss {Math.Abs(profit):N0}";

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Trade,
                $"Sold {cmd.Units:N0} {good.Name} into {city.Name}'s stores for {quote.Total:N0} cr ({verdict}).")
        });
    }

    private static CommandResult Depart(GameState state, WorldData world, DepartCommand cmd)
    {
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail("The convoy is already on the road.");

        var cityId = state.Caravan.LocationId;
        if (cityId is null) return CommandResult.Fail("The convoy has no location.");

        if (!world.CitiesById.TryGetValue(cmd.ToCityId, out var destination))
            return CommandResult.Fail($"No such city '{cmd.ToCityId}'.");

        if (cmd.ToCityId == cityId)
            return CommandResult.Fail("The convoy is already there.");

        var route = world.Routes.Between(cityId, cmd.ToCityId);
        if (route is null)
            return CommandResult.Fail($"No road links {world.City(cityId).Name} to {destination.Name}.");

        var days = CaravanMath.TravelDays(state.Caravan, world, route);
        if (days == int.MaxValue)
            return CommandResult.Fail("The convoy has no working trucks.");

        var fuel = CaravanMath.TravelFuel(state.Caravan, world, route);

        state.Caravan.Travel = new TravelState
        {
            FromId = cityId,
            ToId = cmd.ToCityId,
            TotalDays = days,
            DaysRemaining = days,
            KmPerDay = route.DistanceKm / days,
            FuelPerDay = fuel / days
        };
        state.Caravan.LocationId = null;

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Travel,
                $"Departed for {destination.Name} via {route.Terrain.Name}: " +
                $"{route.DistanceKm:N0} km, {days} day(s), about {fuel:N0} cr of fuel.")
        });
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

        state.Caravan.Crew.Add(new CrewMember
        {
            Id = candidate.Id,
            Name = candidate.Name,
            RoleId = candidate.RoleId,
            DailyWage = candidate.DailyWage,
            HiredDay = state.Day,
            HiredAtCityId = cityId,
            Skills = new Dictionary<string, int>(candidate.Skills)
        });

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Crew,
                $"{candidate.Name} signed on at {city.Name} as {candidate.RoleName}: " +
                $"{candidate.SigningFee:N0} cr down, {candidate.DailyWage:N0} cr/day.")
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
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail("Trucks can only be bought in a city.");

        if (!world.TrucksById.TryGetValue(cmd.TruckTypeId, out var truck))
            return CommandResult.Fail($"No such truck type '{cmd.TruckTypeId}'.");

        if (state.Cash < truck.Price)
            return CommandResult.Fail($"Not enough credits: {truck.Price:N0} needed, {state.Cash:N0} held.");

        state.Cash -= truck.Price;
        state.Caravan.TruckTypeIds.Add(truck.Id);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Expense,
                $"Acquired a {truck.Name} for {truck.Price:N0} cr.")
        });
    }
}
