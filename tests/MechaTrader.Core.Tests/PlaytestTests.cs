using MechaTrader.Core.Ai;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Sim;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// The headless play-tester talks to the game only through Apply / View. These tests
/// hold the policy to that, and hold the un-crewed greedy baseline still un-crewed.
/// </summary>
public class PlaytestTests
{
    private const int Days = 60;
    private const ulong Seed = 1000;

    private static readonly World.WorldData World = TestWorld.Shipping;

    [Fact]
    public void SameSeedProducesIdenticalHouseRuns()
    {
        var a = BotRunner.Run(World, new HouseTrader(), Days, Seed);
        var b = BotRunner.Run(World, new HouseTrader(), Days, Seed);

        Assert.Equal(a.Profit, b.Profit);
        Assert.Equal(a.CommandsIssued, b.CommandsIssued);
        Assert.Equal(a.CommandsRejected, b.CommandsRejected);
        Assert.Equal(a.EndCrewCount, b.EndCrewCount);
        Assert.Equal(a.EndTruckCount, b.EndTruckCount);
        Assert.Equal(a.CitiesVisited, b.CitiesVisited);
        Assert.Equal(a.GoodsTraded, b.GoodsTraded);
        Assert.Equal(a.UsedCrew, b.UsedCrew);
        Assert.Equal(a.UsedTrucks, b.UsedTrucks);
        Assert.Equal(a.UsedFavor, b.UsedFavor);
        Assert.Equal(CommandMixKey(a), CommandMixKey(b));
    }

    [Fact]
    public void GreedyTraderStillNeverHires()
    {
        var result = BotRunner.Run(World, new GreedyTrader(), Days, Seed);

        Assert.False(result.UsedCrew);
        Assert.False(result.UsedTrucks);
        Assert.False(result.UsedFavor);
        Assert.Equal(0, result.EndCrewCount);
        Assert.DoesNotContain("hirecrew", result.CommandMix.Keys);
        Assert.DoesNotContain("buytruck", result.CommandMix.Keys);
        Assert.DoesNotContain("favor", result.CommandMix.Keys);
    }

    [Fact]
    public void HouseTraderHiresWhenCashAndARunAreWaiting()
    {
        var game = Game.New(World, Seed);
        game.State.Cash = World.Config.StartCash * 3;

        var rngBefore = game.State.RngState;
        var command = new HouseTrader().Decide(game, new Rng(1));

        Assert.Equal(rngBefore, game.State.RngState);
        Assert.IsType<HireCrewCommand>(command);
    }

    [Fact]
    public void ReadingDecideDoesNotConsumeTheGameRng()
    {
        var game = Game.New(World, Seed);
        var before = game.State.RngState;

        _ = new HouseTrader().Decide(game, new Rng(99));
        _ = new GreedyTrader().Decide(game, new Rng(99));

        Assert.Equal(before, game.State.RngState);
    }

    [Fact]
    public void HouseTraderTouchesANonHaulageSystemInPlay()
    {
        var used = false;

        for (var i = 0; i < 5; i++)
        {
            var result = BotRunner.Run(World, new HouseTrader(), Days, (ulong)(1000 + i * 7919));
            if (result.UsedCrew || result.UsedTrucks || result.UsedFavor)
            {
                used = true;
                break;
            }
        }

        Assert.True(used,
            "HouseTrader never hired, bought a truck, or donated across five seeds. " +
            "The play-tester is then not covering those systems.");
    }

    [Fact]
    public void HouseTraderVisitsMoreThanOneCity()
    {
        var result = BotRunner.Run(World, new HouseTrader(), Days, Seed);
        Assert.True(result.CitiesVisited.Count > 1,
            $"HouseTrader stayed in {result.CitiesVisited.Count} city/cities.");
    }

    private static string CommandMixKey(BotRunResult result)
        => string.Join(",", result.CommandMix.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}"));
}
