using System.Text.Json;
using MechaTrader.Core.Ai;
using MechaTrader.Core.Commands;
using Xunit;

namespace MechaTrader.Core.Tests;

public class DeterminismTests
{
    private static readonly Command[] Script =
    {
        new BuyCommand("steel", 25),
        new DepartCommand("zurich"),
        new WaitCommand(4),
        new SellCommand("steel", 25),
        new BuyCommand("cells", 30),
        new DepartCommand("milano"),
        new WaitCommand(5),
        new SellCommand("cells", 30),
        new WaitCommand(12)
    };

    private static string RunScript(ulong seed)
    {
        var game = Game.New(TestWorld.Shipping, seed);
        foreach (var command in Script) game.Apply(command);
        return JsonSerializer.Serialize(game.State);
    }

    [Fact]
    public void SameSeedAndSameCommandsProduceIdenticalState()
    {
        // Everything downstream depends on this: replays, save files, the balance
        // harness, and any future rival AI all assume the sim is a pure function.
        Assert.Equal(RunScript(9001), RunScript(9001));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentHistories()
    {
        Assert.NotEqual(RunScript(9001), RunScript(9002));
    }

    [Fact]
    public void StateSurvivesASaveLoadRoundTrip()
    {
        var game = Game.New(TestWorld.Shipping, 555);
        foreach (var command in Script) game.Apply(command);

        var saved = JsonSerializer.Serialize(game.State);
        var restored = JsonSerializer.Deserialize<State.GameState>(saved)!;
        var resumed = Game.Resume(TestWorld.Shipping, restored);

        game.Apply(new WaitCommand(20));
        resumed.Apply(new WaitCommand(20));

        Assert.Equal(JsonSerializer.Serialize(game.State), JsonSerializer.Serialize(resumed.State));
    }
}

public class SkillExpressionTests
{
    private const int Days = 60;
    private const int Seeds = 3;

    private static double MeanProfit(Func<ITraderPolicy> factory)
    {
        double total = 0;
        for (var i = 0; i < Seeds; i++)
        {
            total += BotRunner.Run(TestWorld.Shipping, factory(), Days, (ulong)(1000 + i * 7919)).Profit;
        }
        return total / Seeds;
    }

    [Fact]
    public void PlayingWellIsProfitable()
    {
        var greedy = MeanProfit(() => new GreedyTrader());
        Assert.True(greedy > 0,
            $"A greedy trader averaged {greedy:N0} cr over {Days} days. Skilled play must pay.");
    }

    [Fact]
    public void PlayingCarelesslyLosesMoney()
    {
        var random = MeanProfit(() => new RandomTrader());
        Assert.True(random < 0,
            $"A random trader averaged {random:N0} cr over {Days} days. Careless play must cost.");
    }

    [Fact]
    public void SkillBeatsLuck()
    {
        Assert.True(MeanProfit(() => new GreedyTrader()) > MeanProfit(() => new RandomTrader()));
    }

    [Fact]
    public void TheEconomySurvivesALongUnattendedRun()
    {
        var game = Game.New(TestWorld.Shipping, 31337);
        var world = TestWorld.Shipping;
        var events = new List<Events.GameEvent>();

        for (var day = 0; day < 1000; day++)
        {
            Sim.DayTick.Advance(game.State, world, events);
            events.Clear();
        }

        foreach (var city in world.Cities)
        {
            foreach (var good in world.Goods)
            {
                var stock = game.State.StockOf(city.Id, good.Id);
                Assert.True(double.IsFinite(stock) && stock > 0,
                    $"{city.Id}/{good.Id} stock became {stock} after 1000 days.");
            }
        }
    }
}
