using System.Text.Json;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// Crew are the first thing in the game that changes the terms of trade rather than
/// the goods being traded, so these tests guard two separate things: that hiring is
/// an honest transaction, and that no roster can ever break the price model.
/// </summary>
public class CrewTests
{
    private const ulong Seed = 4242;

    private static readonly World.WorldData World = TestWorld.Shipping;
    private static readonly string Start = World.Config.StartCityId;

    private static Game NewGame() => Game.New(World, Seed);

    /// <summary>A hand who is perfect at one skill and hopeless at the rest.</summary>
    private static CrewMember Specialist(string skillId, int level)
    {
        var skills = World.Crew.Skills.ToDictionary(s => s.Id, s => s.Id == skillId ? level : 1);

        return new CrewMember
        {
            Id = $"test-{skillId}",
            Name = $"Test {skillId}",
            RoleId = "hand",
            DailyWage = CrewMath.WageFor(skills, World.Crew),
            Skills = skills
        };
    }

    private static List<CrewMember> PerfectCrew()
        => World.Crew.Skills.Select(s => Specialist(s.Id, World.Crew.MaxSkill)).ToList();

    private static CrewCandidate FirstCandidate(GameState state)
        => Recruitment.PoolFor(World, World.City(state.Caravan.LocationId!), state.Seed, state.Day)[0];

    /* ---------- the recruitment centres ---------- */

    [Fact]
    public void EveryCityRunsARecruitmentCentre()
    {
        var cfg = World.Crew;

        foreach (var city in World.Cities)
        {
            var pool = Recruitment.PoolFor(World, city, Seed, 1);
            Assert.NotEmpty(pool);

            foreach (var candidate in pool)
            {
                Assert.False(string.IsNullOrWhiteSpace(candidate.Name), $"{city.Id} offered a nameless hand.");
                Assert.True(candidate.DailyWage > 0, $"{candidate.Name} would work for nothing.");
                Assert.Equal(candidate.DailyWage * cfg.SigningFeeDays, candidate.SigningFee);

                foreach (var skill in cfg.Skills)
                {
                    var level = candidate.Skills[skill.Id];
                    Assert.InRange(level, 1, cfg.MaxSkill);
                }
            }
        }
    }

    [Fact]
    public void PoolsAreDeterministicAndCityLocal()
    {
        // Pools are re-derived rather than stored, so the same inputs must give the same
        // people every time - the command processor validates a hire against its own
        // second derivation of exactly this list.
        var city = World.City(Start);

        var first = Recruitment.PoolFor(World, city, Seed, 1);
        var second = Recruitment.PoolFor(World, city, Seed, 1);

        Assert.Equal(
            first.Select(c => $"{c.Id}|{c.Name}|{string.Join(",", c.Skills.Values)}"),
            second.Select(c => $"{c.Id}|{c.Name}|{string.Join(",", c.Skills.Values)}"));

        var elsewhere = Recruitment.PoolFor(World, World.Cities.First(c => c.Id != Start), Seed, 1);
        Assert.NotEqual(first.Select(c => c.Name), elsewhere.Select(c => c.Name));

        var otherSeed = Recruitment.PoolFor(World, city, Seed + 1, 1);
        Assert.NotEqual(first.Select(c => c.Name), otherSeed.Select(c => c.Name));
    }

    [Fact]
    public void PoolsRefreshOnSchedule()
    {
        var cfg = World.Crew;
        var city = World.City(Start);

        var day1 = Recruitment.PoolFor(World, city, Seed, 1);
        var lastDayOfRound = Recruitment.PoolFor(World, city, Seed, cfg.RefreshDays);
        var nextRound = Recruitment.PoolFor(World, city, Seed, cfg.RefreshDays + 1);

        Assert.Equal(day1.Select(c => c.Id), lastDayOfRound.Select(c => c.Id));
        Assert.NotEqual(day1.Select(c => c.Id), nextRound.Select(c => c.Id));

        Assert.Equal(cfg.RefreshDays, Recruitment.DaysUntilRefresh(1, cfg));
        Assert.Equal(1, Recruitment.DaysUntilRefresh(cfg.RefreshDays, cfg));
    }

    [Fact]
    public void ReadingTheHiringBoardDoesNotAdvanceTheWorld()
    {
        // The pool is generated from a pure hash rather than the game's RNG. If it ever
        // drew from GameState.RngState, opening a screen would change the world and
        // determinism would be gone.
        var game = NewGame();

        var rngBefore = game.State.RngState;
        var dayBefore = game.State.Day;

        for (var i = 0; i < 5; i++) Assert.NotEmpty(game.View().Crew.Recruitment!.Candidates);

        Assert.Equal(rngBefore, game.State.RngState);
        Assert.Equal(dayBefore, game.State.Day);
    }

    /* ---------- hiring and paying off ---------- */

    [Fact]
    public void HiringCostsTheSigningFeeAndFillsASeat()
    {
        var game = NewGame();
        var candidate = FirstCandidate(game.State);
        var cashBefore = game.State.Cash;

        var result = game.Apply(new HireCrewCommand(candidate.Id));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(cashBefore - candidate.SigningFee, game.State.Cash);

        var hired = Assert.Single(game.State.Caravan.Crew);
        Assert.Equal(candidate.Name, hired.Name);
        Assert.Equal(candidate.DailyWage, hired.DailyWage);
        Assert.Equal(game.State.Day, hired.HiredDay);
        Assert.Equal(Start, hired.HiredAtCityId);
    }

    [Fact]
    public void AHandOnlySignsOnce()
    {
        var game = NewGame();
        var candidate = FirstCandidate(game.State);

        Assert.True(game.Apply(new HireCrewCommand(candidate.Id)).Ok);
        Assert.False(game.Apply(new HireCrewCommand(candidate.Id)).Ok);

        Assert.Single(game.State.Caravan.Crew);
        Assert.DoesNotContain(game.View().Crew.Recruitment!.Candidates, c => c.Id == candidate.Id);
    }

    [Fact]
    public void HiringStopsAtTheConvoyCapacity()
    {
        var game = NewGame();

        // Filled directly rather than through a run of hires: the point under test is the
        // capacity gate, not how long it takes to reach it.
        foreach (var skill in World.Crew.Skills.Take(World.Crew.CrewCapacity))
            game.State.Caravan.Crew.Add(Specialist(skill.Id, 5));

        var candidate = FirstCandidate(game.State);
        var result = game.Apply(new HireCrewCommand(candidate.Id));

        Assert.False(result.Ok);
        Assert.Contains("crew", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(World.Crew.CrewCapacity, game.State.Caravan.Crew.Count);
    }

    [Fact]
    public void RejectedHiresLeaveStateUntouched()
    {
        var game = NewGame();
        var cash = game.State.Cash;

        Assert.False(game.Apply(new HireCrewCommand("nobody-r0-0")).Ok);
        Assert.False(game.Apply(new DismissCrewCommand("nobody-r0-0")).Ok);

        Assert.Equal(cash, game.State.Cash);
        Assert.Empty(game.State.Caravan.Crew);
        Assert.Empty(game.State.RecruitedIds);
    }

    [Fact]
    public void NobodyIsHiredOrPaidOffOnTheRoad()
    {
        var game = NewGame();
        var candidate = FirstCandidate(game.State);

        var neighbour = World.Routes.From(Start)[0].Other(Start);
        Assert.True(game.Apply(new DepartCommand(neighbour)).Ok);

        Assert.False(game.Apply(new HireCrewCommand(candidate.Id)).Ok);
        Assert.False(game.Apply(new DismissCrewCommand(candidate.Id)).Ok);
        Assert.Null(game.View().Crew.Recruitment);
    }

    [Fact]
    public void WagesAreChargedEveryDay()
    {
        var game = NewGame();
        var truckUpkeep = CaravanMath.TruckUpkeep(game.State.Caravan, World);

        var candidate = FirstCandidate(game.State);
        Assert.True(game.Apply(new HireCrewCommand(candidate.Id)).Ok);

        var expectedDaily = (long)Math.Round(
            truckUpkeep * CrewMath.RunningCostMultiplier(game.State.Caravan, World) + candidate.DailyWage);

        var before = game.State.Cash;
        game.Apply(new WaitCommand(10));

        Assert.Equal(before - expectedDaily * 10, game.State.Cash);
    }

    [Fact]
    public void PayingSomebodyOffCostsSeveranceAndStopsTheWage()
    {
        var game = NewGame();
        var candidate = FirstCandidate(game.State);
        Assert.True(game.Apply(new HireCrewCommand(candidate.Id)).Ok);

        var severance = candidate.DailyWage * World.Crew.SeveranceDays;
        var before = game.State.Cash;

        var result = game.Apply(new DismissCrewCommand(candidate.Id));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(before - severance, game.State.Cash);
        Assert.Empty(game.State.Caravan.Crew);
        Assert.Equal(0, CrewMath.DailyWages(game.State.Caravan.Crew));

        // Still spent: a hand who has taken a contract does not reappear on the board.
        Assert.Contains(candidate.Id, game.State.RecruitedIds);
    }

    [Fact]
    public void CrewSurviveASaveLoadRoundTrip()
    {
        var game = NewGame();
        var candidate = FirstCandidate(game.State);
        Assert.True(game.Apply(new HireCrewCommand(candidate.Id)).Ok);

        var restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(game.State))!;
        var resumed = Game.Resume(World, restored);

        game.Apply(new WaitCommand(15));
        resumed.Apply(new WaitCommand(15));

        Assert.Equal(JsonSerializer.Serialize(game.State), JsonSerializer.Serialize(resumed.State));
        Assert.Equal(candidate.Name, resumed.State.Caravan.Crew[0].Name);
    }

    /* ---------- what crew are actually worth ---------- */

    [Fact]
    public void AnEmptyRosterTradesOnTheMarketsOwnTerms()
    {
        var terms = CrewMath.Terms(new List<CrewMember>(), World.Crew);

        Assert.Equal(TradeTerms.Market.BuySpreadShare, terms.BuySpreadShare);
        Assert.Equal(TradeTerms.Market.SellSpreadShare, terms.SellSpreadShare);
    }

    [Fact]
    public void ATaskIsLedByTheBestHandNotTheHeadcount()
    {
        var cfg = World.Crew;
        var skillId = cfg.Skills[0].Id;

        var crowd = new List<CrewMember> { Specialist(skillId, 3), Specialist(skillId, 3), Specialist(skillId, 3) };
        var oneGoodHand = new List<CrewMember> { Specialist(skillId, 7) };

        Assert.Equal(3, CrewMath.Level(crowd, skillId));
        Assert.Equal(7, CrewMath.Level(oneGoodHand, skillId));
    }

    [Fact]
    public void NegotiationCutsTheBuyPriceAndSalesRaisesTheSellPrice()
    {
        var cfg = World.Crew;
        var eco = World.Config.Economy;
        var good = World.Good("steel");
        var profile = World.City("munchen").Market[good.Id];
        var stock = CityStock.Shelved(profile.Equilibrium);

        var buyLever = cfg.SkillFor(Model.CrewLever.Buy)!;
        var sellLever = cfg.SkillFor(Model.CrewLever.Sell)!;

        var haggler = new List<CrewMember> { Specialist(buyLever.Id, cfg.MaxSkill) };
        var closer = new List<CrewMember> { Specialist(sellLever.Id, cfg.MaxSkill) };

        var plainBuy = Economy.QuoteBuy(good, profile, stock, 50, eco, TradeTerms.Market).Total;
        var haggledBuy = Economy.QuoteBuy(good, profile, stock, 50, eco, CrewMath.Terms(haggler, cfg)).Total;

        var plainSell = Economy.QuoteSell(good, profile, stock, 50, eco, TradeTerms.Market).Total;
        var closedSell = Economy.QuoteSell(good, profile, stock, 50, eco, CrewMath.Terms(closer, cfg)).Total;

        Assert.True(haggledBuy < plainBuy, $"A negotiator paid {haggledBuy} where the market asked {plainBuy}.");
        Assert.True(closedSell > plainSell, $"A closer took {closedSell} where the market offered {plainSell}.");
    }

    [Fact]
    public void NoCrewCanMakeAnInPlaceRoundTripProfitable()
    {
        // Two independent things stop this, and both are checked here. The buy price
        // reads only the shelf while the sell price reads everything the city owns, so
        // the sell quote is structurally the lower of the two whatever the intake holds;
        // and crew bonuses are a share of the spread, clamped so they can close the gap
        // but never invert it.
        var eco = World.Config.Economy;
        var terms = CrewMath.Terms(PerfectCrew(), World.Crew);

        foreach (var city in World.Cities)
        {
            foreach (var good in World.Goods)
            {
                var profile = city.Market[good.Id];

                foreach (var shelf in new[] { eco.MinStock, profile.Equilibrium, profile.Equilibrium * 50 })
                {
                    foreach (var intake in new[] { 0.0, profile.Equilibrium })
                    {
                        var stock = new CityStock(shelf, intake);

                        var buy = Economy.BuyUnitPrice(good, profile, stock, eco, terms);
                        var sell = Economy.SellUnitPrice(good, profile, stock, eco, terms);

                        Assert.True(sell <= buy + 1e-9,
                            $"{city.Id}/{good.Id} at shelf {shelf:0} intake {intake:0}: a perfect crew " +
                            $"could buy at {buy:0.000} and sell at {sell:0.000} in the same city.");
                    }
                }
            }
        }
    }

    [Fact]
    public void NoCrewCanTurnASaleIntoACheaperBuyBack()
    {
        // The loop the two-store split exists to rule out: unload a hold, then buy the
        // same goods back off a shelf your own sale supposedly depressed.
        var game = NewGame();
        foreach (var member in PerfectCrew()) game.State.Caravan.Crew.Add(member);

        var world = World;
        var good = world.Good("steel");
        var profile = world.City(Start).Market[good.Id];
        var eco = world.Config.Economy;
        var terms = CrewMath.Terms(game.State.Caravan, world);

        Assert.True(game.Apply(new BuyCommand(good.Id, 40)).Ok);

        var shelfBefore = game.State.ShelfOf(Start, good.Id);
        var buyBefore = Economy.BuyUnitPrice(good, profile, game.State.StockOf(Start, good.Id), eco, terms);

        var cashBefore = game.State.Cash;
        Assert.True(game.Apply(new SellCommand(good.Id, 40)).Ok);

        var afterSale = game.State.StockOf(Start, good.Id);
        Assert.Equal(shelfBefore, afterSale.Out);
        Assert.Equal(buyBefore, Economy.BuyUnitPrice(good, profile, afterSale, eco, terms));

        Assert.True(game.Apply(new BuyCommand(good.Id, 40)).Ok);
        Assert.True(game.State.Cash < cashBefore,
            "Selling a hold and buying it straight back turned a profit.");
    }

    [Fact]
    public void ANavigatorShortensJourneysAndNeverLengthensThem()
    {
        var game = NewGame();
        var caravan = game.State.Caravan;

        var plainDays = World.Routes.All.ToDictionary(r => r, r => CaravanMath.TravelDays(caravan, World, r));

        var navigation = World.Crew.SkillFor(Model.CrewLever.Speed)!;
        caravan.Crew.Add(Specialist(navigation.Id, World.Crew.MaxSkill));

        var shortened = 0;

        foreach (var (route, before) in plainDays)
        {
            var after = CaravanMath.TravelDays(caravan, World, route);
            Assert.True(after <= before, $"A navigator made {route.FromId}->{route.ToId} slower.");
            if (after < before) shortened++;
        }

        Assert.True(shortened > 0,
            "A maxed navigator does not save a single day anywhere on the map; the speed lever is inert.");
    }

    [Fact]
    public void AccountingCutsRunningCostsButNeverTheWageBill()
    {
        var game = NewGame();
        var caravan = game.State.Caravan;

        var accounting = World.Crew.SkillFor(Model.CrewLever.Upkeep)!;
        var truckUpkeep = CaravanMath.TruckUpkeep(caravan, World);
        var route = World.Routes.From(Start)[0];
        var fuelBefore = CaravanMath.TravelFuel(caravan, World, route);

        var purser = Specialist(accounting.Id, World.Crew.MaxSkill);
        caravan.Crew.Add(purser);

        Assert.True(CaravanMath.TravelFuel(caravan, World, route) < fuelBefore, "Fuel was not trimmed.");
        Assert.Equal(truckUpkeep, CaravanMath.TruckUpkeep(caravan, World));

        // Truck upkeep is discounted; the wage is not. Nobody discounts their own pay.
        var expected = truckUpkeep * CrewMath.RunningCostMultiplier(caravan, World) + purser.DailyWage;
        Assert.Equal(expected, CaravanMath.DailyUpkeep(caravan, World), 6);
    }

    [Fact]
    public void EveryDeclaredSkillIsWiredToSomething()
    {
        // A skill with lever "none" is legal - it is how a future stat ships before the
        // system behind it does - but it must be a deliberate choice, so this records
        // which levers the shipping content actually uses.
        var levers = World.Crew.Skills.Select(s => s.Lever).ToList();

        Assert.Contains(Model.CrewLever.Speed, levers);
        Assert.Contains(Model.CrewLever.Buy, levers);
        Assert.Contains(Model.CrewLever.Sell, levers);
        Assert.Contains(Model.CrewLever.Upkeep, levers);
    }
}
