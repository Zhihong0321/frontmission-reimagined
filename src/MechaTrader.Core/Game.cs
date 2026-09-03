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
                LocationId = config.StartCityId
            }
        };

        foreach (var typeId in config.StartTruckIds)
            state.Caravan.Trucks.Add(CaravanMath.NewTruck(state.Caravan, typeId));

        // Open on a settled economy rather than a flat day zero, so the first day of
        // play already has real price gradients to read.
        foreach (var city in world.Cities)
        {
            var market = new Dictionary<string, CityStock>(world.Goods.Count);
            var craft = string.IsNullOrWhiteSpace(world.Quality.CityVitalId)
                ? 50.0
                : CityStats.Founding(city, world.Quality.CityVitalId);
            var grade = QualityMath.OpeningQuality(world.Quality, craft);
            foreach (var good in world.Goods)
            {
                // A new world opens with nothing in any city's intake: nobody has sold
                // into these markets yet. The shelf grades the way this city's works
                // floors grade, on an average day.
                market[good.Id] = CityStock.Shelved(
                    Economy.InitialStock(city.Market[good.Id], eco), grade);
            }
            state.Stock[city.Id] = market;

            // Every city opens on its founding stats. From here they are state, not
            // content: whatever moves them later writes here and nowhere else.
            state.CityVitals[city.Id] = new Dictionary<string, double>(city.Vitals);
        }

        state.MiningSites = MapMath.PlaceDeposits(world, seed);

        return new Game(world, state);
    }

    /// <summary>Restores a run from persisted state.</summary>
    public static Game Resume(WorldData world, GameState state) => new(world, state);

    public CommandResult Apply(Command command) => CommandProcessor.Execute(State, World, command);

    public GameView View() => ViewBuilder.Build(State, World);

    public long NetWorth() => ViewBuilder.NetWorth(State, World);
}
