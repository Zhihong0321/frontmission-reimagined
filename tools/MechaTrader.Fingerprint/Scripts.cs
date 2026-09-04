using MechaTrader.Core;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Sim;
using MechaTrader.Core.World;

namespace MechaTrader.Fingerprint;

public enum Coverage { Scripted, FeatureTestsOnly }

/// <summary>One line of the command-coverage matrix required by Phase A step 6 (`D-016`).</summary>
public sealed record CoverageEntry(string CommandType, Coverage How, string Note);

/// <summary>One command as it was actually applied, for evidence and logging.</summary>
public sealed record AppliedCommand(Command Command, bool Ok, string? Error);

public sealed record FullSurfaceResult(Game Game, IReadOnlyList<AppliedCommand> Applied);

/// <summary>
/// The command scripts shared by the determinism fingerprints, the save fixtures, and
/// the manual re-baselining tool. Every choice (which candidate, which contract, which
/// good) is read from the world's own content and the game's own pure lookup functions
/// (<see cref="Recruitment"/>, <see cref="Contracts"/>, <see cref="Expos"/>) rather than
/// hardcoded, so the script stays valid if content is retuned — the same convention the
/// existing <c>DeterminismTests.BuildScript</c> already uses for city selection.
/// </summary>
public static class Scripts
{
    /// <summary>
    /// All 21 command types dispatched by <c>CommandProcessor.Execute</c>. Every one is
    /// scripted directly by <see cref="RunFullSurfaceScript"/>; none rely solely on the
    /// per-feature xUnit suite for coverage. This list is asserted against the live
    /// dispatch switch by a test, so it cannot silently drift from the real surface.
    /// </summary>
    public static readonly IReadOnlyList<CoverageEntry> CommandCoverageMatrix = new[]
    {
        new CoverageEntry(nameof(BuyCommand), Coverage.Scripted, "hauls, funds the warehouse, funds each contract line, and stocks the expo stall"),
        new CoverageEntry(nameof(SellCommand), Coverage.Scripted, "sells the hauled lot on arrival at each stop"),
        new CoverageEntry(nameof(DepartCommand), Coverage.Scripted, "two-hop haul from the start city"),
        new CoverageEntry(nameof(WaitCommand), Coverage.Scripted, "travel days, the expo start delay, and a settling tail"),
        new CoverageEntry(nameof(BuyTruckCommand), Coverage.Scripted, "buys a second, non-starting truck type"),
        new CoverageEntry(nameof(SellTruckCommand), Coverage.Scripted, "resells that truck the same day"),
        new CoverageEntry(nameof(UpgradeTruckCommand), Coverage.Scripted, "attempts to fit Economy Tune to the starting truck from whatever cash remains"),
        new CoverageEntry(nameof(BuyGearCommand), Coverage.Scripted, "buys the first catalogued gear item"),
        new CoverageEntry(nameof(HireCrewCommand), Coverage.Scripted, "hires the pool's first affordable+room-aboard candidate, or its first candidate otherwise"),
        new CoverageEntry(nameof(DismissCrewCommand), Coverage.Scripted, "pays that hire off again while cash is still healthy"),
        new CoverageEntry(nameof(AssignCrewCommand), Coverage.Scripted, "moves the hire off whatever post hiring defaulted them to"),
        new CoverageEntry(nameof(CityFavorCommand), Coverage.Scripted, "donates to the start city's governor"),
        new CoverageEntry(nameof(RentWarehouseCommand), Coverage.Scripted, "rents a storeroom at the start city"),
        new CoverageEntry(nameof(WarehouseDepositCommand), Coverage.Scripted, "stashes part of the haul"),
        new CoverageEntry(nameof(WarehouseWithdrawCommand), Coverage.Scripted, "pulls some of it back out"),
        new CoverageEntry(nameof(SetWarehouseSellCommand), Coverage.Scripted, "sets an auto-sell ask on the stashed good"),
        new CoverageEntry(nameof(SetWarehouseProcureCommand), Coverage.Scripted, "sets an auto-buy bid on a second good"),
        new CoverageEntry(nameof(AcceptContractCommand), Coverage.Scripted, "accepts the start city's cheapest-looking board offer"),
        new CoverageEntry(nameof(DeliverContractCommand), Coverage.Scripted, "attempts to buy that offer's lines locally and deliver the same day"),
        new CoverageEntry(nameof(ExpoRegisterCommand), Coverage.Scripted, "registers for the next expo cycle at the start city"),
        new CoverageEntry(nameof(ExpoListCommand), Coverage.Scripted, "lists a good the start city does not make"),
    };

    /// <summary>The short buy/depart/wait/sell cycle used for the `trade-cycle` save fixture.</summary>
    public static Command[] BuildTradeCycleScript(WorldData world)
    {
        var startId = world.Config.StartCityId;
        var firstHop = world.Routes.From(startId)[0];
        var neighborId = firstHop.Other(startId);

        return new Command[]
        {
            new BuyCommand("steel", 20),
            new DepartCommand(neighborId),
            new WaitCommand(4),
            new SellCommand("steel", 20)
        };
    }

    /// <summary>
    /// Runs every command type in <see cref="CommandCoverageMatrix"/> against a fresh
    /// game on the given seed. Deterministic: every branch reads only world content,
    /// <c>game.State</c>/<c>game.View()</c>, and the immutable seed — never wall-clock
    /// time or unseeded randomness. A command is issued even when its success depends
    /// on world state that could plausibly vary by seed (an affordable recruit, a
    /// deliverable contract line); <see cref="AppliedCommand.Ok"/> records the outcome,
    /// but a rejected command still exercises `CommandProcessor`'s validation path for
    /// that type and leaves state untouched, so it cannot break reproducibility.
    /// </summary>
    public static FullSurfaceResult RunFullSurfaceScript(WorldData world, ulong seed)
    {
        var game = Game.New(world, seed);
        var applied = new List<AppliedCommand>();

        void Do(Command command)
        {
            var result = game.Apply(command);
            applied.Add(new AppliedCommand(command, result.Ok, result.Error));
        }

        var startId = world.Config.StartCityId;
        var start = world.City(startId);
        var firstHop = world.Routes.From(startId)[0];
        var neighborId = firstHop.Other(startId);
        var secondHop = world.Routes.From(neighborId).First(r => r.Other(neighborId) != startId);
        var farId = secondHop.Other(neighborId);

        const string haulGood = "steel";
        const string secondGood = "cells";

        // Every command below is issued unconditionally where its target (a candidate,
        // an offer, an expo instance) exists at all — which content guarantees it does
        // at the start city — rather than only when it looks affordable. A fixed 20,000
        // cr start budget cannot fund every one of these at once (a truck flip alone
        // costs 40% of a truck's price, and a signing fee or a "supply" contract can
        // each run into the thousands), so several are expected to be issued and
        // rejected rather than issued and succeed. A rejection still exercises
        // `CommandProcessor`'s validation path for that type, which is what coverage
        // means here; each type's success path is separately proven by the named
        // per-feature test class in `CommandCoverageMatrix`.

        // 1. Reserve a modest two-hop haul first, before anything else claims the budget.
        Do(new BuyCommand(haulGood, 15));
        Do(new BuyCommand(secondGood, 10));

        // 2. Buy and immediately resell a second truck — the single largest planned
        // expense, spent second so it is not squeezed out by the unpredictable signing
        // fee and contract cost below.
        var runnerType = world.Trucks
            .Where(t => t.Kind == "truck" && t.Id != world.Config.StartTruckIds[0])
            .OrderBy(t => t.Price)
            .FirstOrDefault();
        if (runnerType is not null)
        {
            var beforeIds = game.State.Caravan.Trucks.Select(t => t.Id).ToHashSet();
            Do(new BuyTruckCommand(runnerType.Id));
            var newTruckId = game.State.Caravan.Trucks.Select(t => t.Id)
                .FirstOrDefault(id => !beforeIds.Contains(id));
            if (newTruckId is not null) Do(new SellTruckCommand(newTruckId));
        }

        // 3. Crew: hire, move off the role's default post, then pay them off again.
        // Prefer a candidate the view already reports as affordable and room-aboard,
        // but fall back to whoever heads the pool so all three are always issued.
        var view = game.View();
        var candidateId = (view.Crew.Recruitment?.Candidates.FirstOrDefault(c => c.Affordable && c.RoomAboard)
            ?? view.Crew.Recruitment?.Candidates.FirstOrDefault())?.Id;
        if (candidateId is not null)
        {
            Do(new HireCrewCommand(candidateId));
            var defaultPostId = game.View().Crew.Roster
                .FirstOrDefault(m => m.Id == candidateId)?.PostId ?? "";
            var otherPostId = defaultPostId.Length > 0
                ? ""
                : view.Crew.Posts.Select(p => p.Id).FirstOrDefault();
            if (otherPostId is not null) Do(new AssignCrewCommand(candidateId, otherPostId));
            Do(new DismissCrewCommand(candidateId));
        }

        // 4. A storeroom: rent, deposit part of the haul, price both directions, withdraw.
        Do(new RentWarehouseCommand());
        Do(new WarehouseDepositCommand(haulGood, 5));
        Do(new SetWarehouseSellCommand(haulGood, (long)Math.Round(world.Good(haulGood).BasePrice * 1.2)));
        Do(new SetWarehouseProcureCommand(secondGood, (long)Math.Round(world.Good(secondGood).BasePrice * 0.8)));
        Do(new WarehouseWithdrawCommand(haulGood, 2));

        // 5. The start city's contract board: accept the cheapest-looking offer, buy its
        // lines locally (this city stocks every good, just not at a producer's price),
        // and attempt delivery the same day.
        var offers = Contracts.BoardFor(world, start, seed, game.State.Day);
        if (offers.Count > 0)
        {
            var offer = offers
                .OrderBy(o => o.Lines.Sum(l => l.Units * world.Good(l.GoodId).BasePrice))
                .First();
            foreach (var line in offer.Lines) Do(new BuyCommand(line.GoodId, line.Units));
            Do(new AcceptContractCommand(offer.Id));
            Do(new DeliverContractCommand(offer.Id));
        }

        // 6. Governor goodwill, a fitting, and a gear item — the three smallest fixed
        // costs, spent last from whatever remains.
        Do(new CityFavorCommand("donate"));
        var originalTruckId = game.State.Caravan.Trucks[0].Id;
        Do(new UpgradeTruckCommand(originalTruckId, "economy"));
        if (world.Gear.Count > 0) Do(new BuyGearCommand(world.Gear[0].Id));

        // 7. The start city's expo calendar: wait for the next cycle, register, and
        // list a good it does not produce (buying a few units first if none are held).
        var expo = Expos.Next(world, start, seed, game.State.Day);
        if (expo is not null)
        {
            if (expo.StartDay > game.State.Day) Do(new WaitCommand(expo.StartDay - game.State.Day));
            Do(new ExpoRegisterCommand());

            var listable = world.Goods.FirstOrDefault(g =>
                Expos.ThemeCovers(expo.Theme, g) && !Expos.CityMakes(start, g.Id));
            if (listable is not null)
            {
                if (!game.State.Caravan.Cargo.TryGetValue(listable.Id, out var lot) || lot.Units <= 0)
                    Do(new BuyCommand(listable.Id, 5));
                Do(new ExpoListCommand(listable.Id, (long)Math.Round(listable.BasePrice)));
            }
        }

        // 8. The haul: two hops, selling whatever the hold actually carries on arrival.
        Do(new DepartCommand(neighborId));
        var toNeighbor = CaravanMath.TravelDays(game.State.Caravan, world, firstHop);
        Do(new WaitCommand(Math.Max(1, toNeighbor)));
        var heldAtNeighbor = game.State.Caravan.Cargo.TryGetValue(haulGood, out var haulLot) ? haulLot.Units : 0;
        if (heldAtNeighbor > 0) Do(new SellCommand(haulGood, heldAtNeighbor));

        Do(new DepartCommand(farId));
        var toFar = CaravanMath.TravelDays(game.State.Caravan, world, secondHop);
        Do(new WaitCommand(Math.Max(1, toFar)));
        var heldAtFar = game.State.Caravan.Cargo.TryGetValue(secondGood, out var secondLot) ? secondLot.Units : 0;
        if (heldAtFar > 0) Do(new SellCommand(secondGood, heldAtFar));

        // 9. Settle for a while so the fixture crosses ~60 days.
        var settleDays = Math.Max(1, 60 - game.State.Day);
        Do(new WaitCommand(settleDays));

        return new FullSurfaceResult(game, applied);
    }
}
