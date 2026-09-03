using System.Text.Json;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// Trader contracts: derived boards, honest terms, delivery that checks grade and
/// count, a reward and traders standing on settlement, and a penalty on lapse.
/// </summary>
public class ContractTests
{
    private const ulong Seed = 717;

    private static readonly WorldData World = TestWorld.Shipping;
    private static readonly string Start = World.Config.StartCityId;

    private static Game NewGame() => Game.New(World, Seed);

    [Fact]
    public void TheBoardIsDeterministicAndNeverStored()
    {
        var city = World.City(Start);
        var a = Contracts.BoardFor(World, city, Seed, 1);
        var b = Contracts.BoardFor(World, city, Seed, 1);

        Assert.NotEmpty(a);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.NotEqual(JsonSerializer.Serialize(a), JsonSerializer.Serialize(Contracts.BoardFor(World, city, Seed + 1, 1)));

        var game = NewGame();
        var before = JsonSerializer.Serialize(game.State);
        _ = game.View().Contracts;
        Assert.Equal(before, JsonSerializer.Serialize(game.State));
        Assert.DoesNotContain("reward", before, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ACityOnlyAsksForWhatItDoesNotMake()
    {
        foreach (var city in World.Cities)
        {
            foreach (var offer in Contracts.BoardFor(World, city, Seed, 1))
            {
                foreach (var line in offer.Lines)
                    Assert.True(city.Market[line.GoodId].Production <= 1e-9, $"{city.Id} asks for {line.GoodId}, which it makes.");
                Assert.True(offer.Reward > 0);
            }
        }
    }

    [Fact]
    public void TheBoardRefreshesOnSchedule()
    {
        var city = World.City(Start);
        var cfg = World.Contracts;
        var first = Contracts.BoardFor(World, city, Seed, 1);
        var sameRound = Contracts.BoardFor(World, city, Seed, cfg.RefreshDays);
        var nextRound = Contracts.BoardFor(World, city, Seed, cfg.RefreshDays + 1);

        Assert.Equal(first.Select(o => o.Id), sameRound.Select(o => o.Id));
        Assert.NotEqual(first.Select(o => o.Id), nextRound.Select(o => o.Id));
    }

    [Fact]
    public void AcceptingStoresOnlyTheAcceptanceAndDeliveryPays()
    {
        var game = NewGame();
        var offer = game.View().Contracts.Board.First();
        var traders = World.Standing.SegmentOr("traders");

        var accepted = game.Apply(new AcceptContractCommand(offer.Id));
        Assert.True(accepted.Ok, accepted.Error);
        Assert.Single(game.State.Contracts);
        Assert.Equal(game.State.Day + offer.DeadlineDays, game.State.Contracts[0].Deadline);

        // Fill the hold exactly as asked, at a grade that satisfies the contract.
        foreach (var line in offer.Lines)
            game.State.Caravan.Cargo[line.GoodId] = new CargoLot { Units = line.Units, TotalCost = line.Units, Quality = Math.Max(90, offer.MinGrade) };

        var cashBefore = game.State.Cash;
        var delivered = game.Apply(new DeliverContractCommand(offer.Id));

        Assert.True(delivered.Ok, delivered.Error);
        Assert.Equal(cashBefore + offer.Reward, game.State.Cash);
        Assert.Empty(game.State.Contracts);
        Assert.Contains(offer.Id, game.State.ContractsClosed);
        Assert.Equal(offer.Standing, Standing.Segment(game.State, Start, traders), 6);
        Assert.All(offer.Lines, l => Assert.Equal(0, game.State.Caravan.Held(l.GoodId)));
        Assert.True(game.View().Contracts.Board.First(o => o.Id == offer.Id).Closed);
    }

    [Fact]
    public void DeliveryIsRefusedShortOrBelowGradeAndLeavesStateUntouched()
    {
        var game = NewGame();
        var board = game.View().Contracts.Board;
        var offer = board.FirstOrDefault(o => o.MinGrade > 0) ?? board.First();
        game.Apply(new AcceptContractCommand(offer.Id));

        var line = offer.Lines[0];
        game.State.Caravan.Cargo[line.GoodId] = new CargoLot { Units = line.Units - 1, TotalCost = 1, Quality = 95 };
        var before = JsonSerializer.Serialize(game.State);

        var shortBy = game.Apply(new DeliverContractCommand(offer.Id));
        Assert.False(shortBy.Ok);
        Assert.Equal(before, JsonSerializer.Serialize(game.State));

        if (offer.MinGrade > 0)
        {
            foreach (var l in offer.Lines)
                game.State.Caravan.Cargo[l.GoodId] = new CargoLot { Units = l.Units, TotalCost = 1, Quality = offer.MinGrade - 5 };
            before = JsonSerializer.Serialize(game.State);

            var poor = game.Apply(new DeliverContractCommand(offer.Id));
            Assert.False(poor.Ok);
            Assert.Contains("grade", poor.Error!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, JsonSerializer.Serialize(game.State));
        }
    }

    [Fact]
    public void AContractIsSettledOnlyInTheCityThatIssuedIt()
    {
        var game = NewGame();
        var offer = game.View().Contracts.Board.First();
        game.Apply(new AcceptContractCommand(offer.Id));
        foreach (var line in offer.Lines)
            game.State.Caravan.Cargo[line.GoodId] = new CargoLot { Units = line.Units, TotalCost = 1, Quality = 95 };

        var route = World.Routes.From(Start)[0];
        game.Apply(new DepartCommand(route.Other(Start)));
        game.Apply(new WaitCommand(CaravanMath.TravelDays(game.State.Caravan, World, route)));
        Assert.Equal(route.Other(Start), game.State.Caravan.LocationId);

        var elsewhere = game.Apply(new DeliverContractCommand(offer.Id));
        Assert.False(elsewhere.Ok);
        Assert.False(game.View().Contracts.Held.Single().Here);
    }

    [Fact]
    public void AnOfferOffTheBoardOrAlreadyHeldIsRefused()
    {
        var game = NewGame();
        var offer = game.View().Contracts.Board.First();

        Assert.False(game.Apply(new AcceptContractCommand("nowhere-c0-0")).Ok);
        Assert.True(game.Apply(new AcceptContractCommand(offer.Id)).Ok);

        var before = JsonSerializer.Serialize(game.State);
        Assert.False(game.Apply(new AcceptContractCommand(offer.Id)).Ok);
        Assert.Equal(before, JsonSerializer.Serialize(game.State));
    }

    [Fact]
    public void ALapsedContractIsTornUpAndCostsTradersStanding()
    {
        var game = NewGame();
        var offer = game.View().Contracts.Board.First();
        var traders = World.Standing.SegmentOr("traders");
        game.State.SetStanding(Start, traders, 20);
        game.State.Cash = 10_000_000;

        game.Apply(new AcceptContractCommand(offer.Id));
        game.Apply(new WaitCommand(offer.DeadlineDays + 1));

        Assert.Empty(game.State.Contracts);
        Assert.Contains(offer.Id, game.State.ContractsClosed);
        Assert.Equal(20 - World.Standing.ContractLapsePenalty, Standing.Segment(game.State, Start, traders), 6);
    }

    [Fact]
    public void ContractsSurviveASave()
    {
        var game = NewGame();
        var offer = game.View().Contracts.Board.First();
        game.Apply(new AcceptContractCommand(offer.Id));

        var restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(game.State))!;
        var resumed = Game.Resume(World, restored);

        Assert.Single(resumed.View().Contracts.Held);
        Assert.Equal(offer.Id, resumed.View().Contracts.Held[0].Id);
    }
}
