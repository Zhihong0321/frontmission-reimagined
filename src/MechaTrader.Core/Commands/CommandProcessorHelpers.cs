using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Commands;

public static partial class CommandProcessor
{
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
}
