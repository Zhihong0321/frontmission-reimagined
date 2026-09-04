using MechaTrader.Core.Commands;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Ai;

/// <summary>An automated trader. Returns the next command it wants to issue.</summary>
public interface ITraderPolicy
{
    string Name { get; }
    Command? Decide(Game game, Rng rng);
}

/// <summary>
/// Buys the best margin it can reach in one hop, hauls it, sells it, repeats. When
/// nothing local pays, it repositions empty toward wherever the next run is.
///
/// This exists to answer the only question that matters before any art gets made:
/// does playing well beat playing badly? If this policy cannot out-earn
/// <see cref="RandomTrader"/>, the economy has no skill expression and no amount of
/// visual polish will produce a game. It is the un-crewed skill baseline;
/// <see cref="HouseTrader"/> is the play-tester that also spends on crew, trucks and
/// standing, and the seed of the rival houses planned for a later milestone.
/// </summary>
public sealed class GreedyTrader : ITraderPolicy
{
    public string Name => "greedy";

    private string? _pendingDestination;

    public Command? Decide(Game game, Rng rng)
    {
        var state = game.State;

        if (state.Caravan.Travel is { } travel)
            return new WaitCommand(Math.Max(1, travel.DaysRemaining));

        if (_pendingDestination is { } destination)
        {
            _pendingDestination = null;
            return new DepartCommand(destination);
        }

        // Clear the hold before evaluating anything new: this is a pure haulage loop.
        foreach (var (goodId, lot) in state.Caravan.Cargo)
        {
            if (lot.Units > 0) return new SellCommand(goodId, lot.Units);
        }

        var cityId = state.Caravan.LocationId;
        if (cityId is null) return new WaitCommand(1);

        var loaded = TradeScout.BestRunFrom(game, cityId);
        if (loaded is { } run && run.Net > 0)
        {
            _pendingDestination = run.DestinationId;
            return new BuyCommand(run.GoodId, run.Units);
        }

        // Nothing here pays. Sitting still still costs upkeep, so go find the trade
        // rather than waiting for one to arrive.
        var reposition = TradeScout.BestRepositioning(game, cityId);
        if (reposition is not null) return new DepartCommand(reposition);

        return new WaitCommand(1);
    }
}

/// <summary>
/// Same haulage loop as <see cref="GreedyTrader"/>, plus the extras a house actually
/// spends on: a hire that improves the next run, one extra mule when the hold is the
/// bottleneck, and a donate when the books can spare it.
///
/// Headless play-tester today; the seed of a rival trading house once the world holds
/// more than one convoy. Talks to the game only through commands. Never touches the
/// simulation RNG — recruitment pools are derived from the seed, like the city page.
/// </summary>
public sealed class HouseTrader : ITraderPolicy
{
    public string Name => "house";

    private string? _pendingDestination;

    public Command? Decide(Game game, Rng rng)
    {
        var state = game.State;

        if (state.Caravan.Travel is { } travel)
            return new WaitCommand(Math.Max(1, travel.DaysRemaining));

        if (_pendingDestination is { } destination)
        {
            _pendingDestination = null;
            return new DepartCommand(destination);
        }

        foreach (var (goodId, lot) in state.Caravan.Cargo)
        {
            if (lot.Units > 0) return new SellCommand(goodId, lot.Units);
        }

        var cityId = state.Caravan.LocationId;
        if (cityId is null) return new WaitCommand(1);

        // Order is priority: people, then hold, then the governor, then a fitting.
        var extra = TryHire(game, cityId)
                    ?? TryBuyMule(game, cityId)
                    ?? TryDonate(game, cityId)
                    ?? TryFitEconomy(game);
        if (extra is not null) return extra;

        var loaded = TradeScout.BestRunFrom(game, cityId);
        if (loaded is { } run && run.Net > 0)
        {
            _pendingDestination = run.DestinationId;
            return new BuyCommand(run.GoodId, run.Units);
        }

        var reposition = TradeScout.BestRepositioning(game, cityId);
        if (reposition is not null) return new DepartCommand(reposition);

        return new WaitCommand(1);
    }

    /// <summary>
    /// Sign the local candidate who most improves a lever the next run actually uses,
    /// if cash still holds a starting-capital reserve after the fee.
    /// </summary>
    private static Command? TryHire(Game game, string cityId)
    {
        var state = game.State;
        var world = game.World;
        var cfg = world.Crew;

        if (state.Caravan.Crew.Count >= cfg.CrewCapacity) return null;

        var next = TradeScout.BestRunFrom(game, cityId);
        if (next is not { } run || run.Net <= 0) return null;

        var reserve = world.Config.StartCash;
        var city = world.City(cityId);
        var pool = Recruitment.PoolFor(world, city, state.Seed, state.Day);

        CrewCandidate? best = null;
        var bestGain = 0;

        foreach (var candidate in pool)
        {
            if (state.RecruitedIds.Contains(candidate.Id)) continue;
            if (state.Cash < candidate.SigningFee + reserve) continue;

            var gain = LeverGain(state.Caravan.Crew, candidate, cfg);
            if (gain > bestGain)
            {
                bestGain = gain;
                best = candidate;
            }
        }

        return best is null ? null : new HireCrewCommand(best.Id);
    }

    /// <summary>
    /// How many skill points this candidate would raise on levers a loaded haul uses.
    /// Zero means they do not beat anyone already on the books for those jobs.
    /// </summary>
    private static int LeverGain(
        IReadOnlyList<CrewMember> roster,
        CrewCandidate candidate,
        CrewConfig cfg)
    {
        var gain = 0;

        // A candidate only pulls the levers of the post they will sign on to, so a
        // navigator's secondary sales score is worth nothing at a counter they never stand at.
        var post = cfg.DefaultPost(cfg.Role(candidate.RoleId));

        foreach (var lever in new[] { CrewLever.Speed, CrewLever.Buy, CrewLever.Sell, CrewLever.Upkeep })
        {
            var skill = cfg.SkillFor(lever);
            if (skill is null) continue;

            var claimant = cfg.PostFor(lever);
            if (claimant is not null && claimant.Id != post) continue;

            var theirs = candidate.Skills.TryGetValue(skill.Id, out var level) ? level : 0;
            var ours = CrewMath.Level(roster, cfg, skill.Id);
            var delta = theirs - ours;
            if (delta > gain) gain = delta;
        }

        return gain;
    }

    /// <summary>
    /// One extra mule, and only one, when cash still holds a reserve after the sticker
    /// and the best run is already hitting the hold rather than the wallet.
    /// </summary>
    private static Command? TryBuyMule(Game game, string cityId)
    {
        var state = game.State;
        var world = game.World;

        if (state.Caravan.Trucks.Count != world.Config.StartTruckIds.Count) return null;
        if (!TradeScout.BestRunIsVolumeCapped(game, cityId)) return null;

        if (!world.TrucksById.TryGetValue("mule", out var mule))
        {
            mule = world.Trucks
                .OrderBy(t => t.Price)
                .FirstOrDefault(t => t.Capacity > CaravanMath.Capacity(state.Caravan, world));
            if (mule is null) return null;
        }

        if (state.Cash - mule.Price < world.Config.StartCash) return null;

        return new BuyTruckCommand(mule.Id);
    }

    /// <summary>
    /// One fitting, on the first truck that lacks it: the cheapest upgrade that cuts
    /// running costs (fuel or upkeep) and does nothing else the scout would have to
    /// re-plan around, once cash after the sticker still holds twice starting capital.
    /// Content ids are not named; the pick is by effect.
    /// </summary>
    private static Command? TryFitEconomy(Game game)
    {
        var state = game.State;
        var world = game.World;

        var thrifty = world.TruckUpgrades
            .Where(u => (u.FuelMult < 1.0 || u.UpkeepDelta < 0) && u.SpeedMult >= 1.0 && u.CapacityBonus >= 0)
            .OrderBy(u => u.Price)
            .FirstOrDefault();
        if (thrifty is null) return null;
        if (state.Cash - thrifty.Price < world.Config.StartCash * 2) return null;

        foreach (var truck in state.Caravan.Trucks)
        {
            if (truck.UpgradeIds.Contains(thrifty.Id)) continue;
            if (!thrifty.Fits(world.Truck(truck.TypeId).EffectiveKind)) continue;
            return new UpgradeTruckCommand(truck.Id, thrifty.Id);
        }

        return null;
    }

    /// <summary>
    /// A gift, nothing else: only donate, only when standing is still zero and cash
    /// after the gift is at least twice starting capital. Invest and aid rewrite a
    /// city; the play-tester should not do that as a side effect of being clever.
    /// </summary>
    private static Command? TryDonate(Game game, string cityId)
    {
        var state = game.State;
        var world = game.World;

        if (Standing.Of(state, cityId) > 1e-9) return null;

        var donate = world.Standing.Action("donate");
        if (donate is null)
        {
            donate = world.Standing.Actions.FirstOrDefault(a =>
                string.IsNullOrWhiteSpace(a.VitalId) && a.StockPerGood <= 0);
        }

        if (donate is null) return null;
        if (state.Cash - donate.Cost < world.Config.StartCash * 2) return null;

        return new CityFavorCommand(donate.Id);
    }
}

/// <summary>
/// Trades at random. The control group: if this makes money, the economy is a
/// money printer and the trade loop has no tension.
/// </summary>
public sealed class RandomTrader : ITraderPolicy
{
    public string Name => "random";

    public Command? Decide(Game game, Rng rng)
    {
        var state = game.State;
        var world = game.World;

        if (state.Caravan.Travel is { } travel)
            return new WaitCommand(Math.Max(1, travel.DaysRemaining));

        var cityId = state.Caravan.LocationId;
        if (cityId is null) return new WaitCommand(1);

        var roll = rng.NextDouble();

        if (roll < 0.35)
        {
            foreach (var (goodId, lot) in state.Caravan.Cargo)
            {
                if (lot.Units <= 0) continue;
                var units = Math.Max(1, rng.NextInt(lot.Units) + 1);
                return new SellCommand(goodId, units);
            }
        }

        if (roll < 0.70)
        {
            var good = world.Goods[rng.NextInt(world.Goods.Count)];
            var profile = world.City(cityId).Market[good.Id];
            var stock = state.StockOf(cityId, good.Id);
            var free = CaravanMath.FreeVolume(state.Caravan, world);

            if (!Standing.TierOpen(world.TierOf(good), Standing.Of(state, cityId))) return new WaitCommand(1);
            var max = Economy.MaxAffordableUnits(
                good, profile, stock, state.Cash, free, world.Config.Economy,
                CrewMath.Terms(state.Caravan, world),
                WorldEvents.PriceMultiplier(state, world, cityId, good.Id),
                QualityMath.SellMultiplier(stock.OutQuality, world.Quality));
            if (max > 0) return new BuyCommand(good.Id, Math.Max(1, rng.NextInt(max) + 1));
        }

        var routes = world.Routes.From(cityId);
        if (routes.Count == 0) return new WaitCommand(1);

        var chosen = routes[rng.NextInt(routes.Count)];
        return new DepartCommand(chosen.Other(cityId));
    }
}
