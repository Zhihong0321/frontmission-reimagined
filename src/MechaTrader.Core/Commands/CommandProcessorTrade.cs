using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Commands;

public static partial class CommandProcessor
{
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
}
