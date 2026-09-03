using MechaTrader.Core;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using Xunit;

namespace MechaTrader.Core.Tests;

public class QualityTests
{
    private static readonly World.WorldData World = TestWorld.Shipping;
    private static readonly QualityConfig Q = World.Quality;

    [Fact]
    public void BuyingTheWholeShelfKeepsTheAverageEvenAtFullKnowledge()
    {
        var selected = QualityMath.SelectedQuality(72, 200, 200, 1.0, Q);
        Assert.Equal(72, selected, 6);
    }

    [Fact]
    public void KnowledgeDoesNotChangeARandomDraw()
    {
        var selected = QualityMath.SelectedQuality(72, 200, 10, 0.0, Q);
        Assert.Equal(72, selected, 6);
    }

    [Fact]
    public void KnowledgeRaisesASmallOrderAboveTheAverage()
    {
        var cherry = QualityMath.SelectedQuality(72, 200, 1, 1.0, Q);
        var sweep = QualityMath.SelectedQuality(72, 200, 200, 1.0, Q);
        Assert.True(cherry > sweep, $"cherry {cherry:0.0} was not above sweep {sweep:0.0}.");
        Assert.Equal(72, sweep, 6);
    }

    [Fact]
    public void CherryPickingLowersWhatIsLeftOnTheShelf()
    {
        var stock = new CityStock(205, 0, 72);
        var saleable = Economy.UnitsOnTheShelf(stock, World.Config.Economy);
        var quoted = stock with { Out = stock.Out - 1 };
        var (selected, remaining) = QualityMath.Take(stock, saleable, 1, 1.0, Q, quoted);

        Assert.True(selected > 72);
        Assert.True(remaining.OutQuality < 72,
            $"remaining grade {remaining.OutQuality:0.00} did not fall after a perfect pick.");
    }

    [Fact]
    public void STierSellIsThirtyPercentUp()
    {
        Assert.Equal(1.0, QualityMath.SellMultiplier(Q.Nominal, Q), 6);
        Assert.Equal(1.30, QualityMath.SellMultiplier(Q.STierAt, Q), 6);
        Assert.True(QualityMath.IsSTier(Q.STierAt, Q));
        Assert.False(QualityMath.IsSTier(Q.Nominal, Q));
    }

    [Fact]
    public void CategoryKnowledgeImprovesTheBuyBargain()
    {
        // Knowledge only bargains from the counter: the eye has to be on the trading post.
        var specialist = new CrewMember
        {
            Id = "eye",
            Name = "Eye",
            PostId = World.Crew.PostFor(Model.CrewLever.Buy)?.Id ?? "",
            Knowledge = new Dictionary<string, double> { ["metals"] = World.Crew.MaxKnowledge }
        };

        var none = CrewMath.Terms(Array.Empty<CrewMember>(), World.Crew, "metals");
        var with = CrewMath.Terms(new[] { specialist }, World.Crew, "metals");

        Assert.True(with.BuySpreadShare < none.BuySpreadShare,
            "A metals specialist should concede less of the buy spread.");
        Assert.True(with.BuySpreadShare >= 0);
        Assert.True(with.SellSpreadShare >= 0);
    }

    [Fact]
    public void ABuyGrantsCategoryKnowledgeToTheRoster()
    {
        var world = World;
        var game = Game.New(world, 7);
        var member = new CrewMember
        {
            Id = "hand",
            Name = "Hand",
            RoleId = "hand",
            DailyWage = 5,
            Skills = world.Crew.Skills.ToDictionary(s => s.Id, s => 1)
        };
        game.State.Caravan.Crew.Add(member);

        var good = world.Goods[0];
        var before = member.KnowledgeOf(good.Category);
        var result = game.Apply(new BuyCommand(good.Id, 1));
        Assert.True(result.Ok, result.Error);
        Assert.True(member.KnowledgeOf(good.Category) > before,
            "Trading should teach the category that was handled.");
    }
}
