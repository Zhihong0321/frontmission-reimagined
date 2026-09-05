using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Commands;

public static partial class CommandProcessor
{
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
}
