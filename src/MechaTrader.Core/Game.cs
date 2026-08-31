using MechaTrader.Core.Commands;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.View;
using MechaTrader.Core.World;

namespace MechaTrader.Core;

/// <summary>
/// The whole simulation behind one small surface: create, apply a command, read a view.
///
/// Every front-end talks to exactly this. The browser reaches it over HTTP today; a
/// Godot scene will call it in-process later. Because the class holds no reference to a
/// renderer, a clock or a filesystem, swapping the presentation layer costs nothing.
/// </summary>
public sealed class Game
{
    public WorldData World { get; }
    public GameState State { get; private set; }

    private Game(WorldData world, GameState state)
    {
        World = world;
        State = state;
    }

    /// <summary>Starts a new run. The same seed always produces the same world history.</summary>
    public static Game New(WorldData world, ulong seed)
    {
        var config = world.Config;
        var eco = config.Economy;

        var state = new GameState
        {
            Day = 1,
            Seed = seed,
            RngState = seed,
            Cash = config.StartCash,
            Bankrupt = false,
            Caravan = new CaravanState
            {
                LocationId = config.StartCityId,
                TruckTypeIds = new List<string>(config.StartTruckIds)
            }
        };

        // Open on a settled economy rather than a flat day zero, so the first day of
        // play already has real price gradients to read.
        foreach (var city in world.Cities)
        {
            var market = new Dictionary<string, double>(world.Goods.Count);
            foreach (var good in world.Goods)
            {
                market[good.Id] = Economy.InitialStock(city.Market[good.Id], eco);
            }
            state.Stock[city.Id] = market;
        }

        return new Game(world, state);
    }

    /// <summary>Restores a run from persisted state.</summary>
    public static Game Resume(WorldData world, GameState state) => new(world, state);

    public CommandResult Apply(Command command) => CommandProcessor.Execute(State, World, command);

    public GameView View() => ViewBuilder.Build(State, World);

    public long NetWorth() => ViewBuilder.NetWorth(State, World);
}
