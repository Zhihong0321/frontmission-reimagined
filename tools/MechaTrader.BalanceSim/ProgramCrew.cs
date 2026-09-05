using System.Diagnostics;
using MechaTrader.Content;
using MechaTrader.Core;
using MechaTrader.Core.Ai;
using MechaTrader.Core.Events;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.BalanceSim;

public static partial class Program
{
    /// <summary>
    /// What the recruitment centres are offering and what the best of them is worth.
    ///
    /// The hard check here is the one that keeps the price levers safe: however good a
    /// crew gets, buying and selling in the same city must never turn a profit. Crew
    /// erode the market spread rather than move the mid price, so this should hold by
    /// construction - which is exactly why it is worth asserting.
    /// </summary>
    private static void PrintCrew(WorldData world, List<string> failures)
    {
        var cfg = world.Crew;
        const ulong seed = 20260901UL;

        Console.WriteLine();
        Console.WriteLine($"{cfg.Skills.Count} skills, {cfg.Roles.Count} roles, " +
                          $"capacity {cfg.CrewCapacity}, pool refreshes every {cfg.RefreshDays} days");

        Console.WriteLine();
        Console.WriteLine($"{"skill",-14}{"lever",-9}{"at max",10}   effect at max skill");
        Console.WriteLine(new string('-', 78));
        foreach (var skill in cfg.Skills)
        {
            Console.WriteLine($"{skill.Name,-14}{skill.Lever,-9}{skill.MaxEffect,10:P0}   {skill.Blurb}");
        }

        var pools = world.Cities
            .Select(c => (City: c, Pool: Recruitment.PoolFor(world, c, seed, 1)))
            .ToList();

        var candidates = pools.SelectMany(p => p.Pool).ToList();
        if (candidates.Count == 0)
        {
            failures.Add("No city offers a single recruit; the recruitment centres are empty.");
            return;
        }

        var emptyCities = pools.Where(p => p.Pool.Count == 0).Select(p => p.City.Id).ToList();
        if (emptyCities.Count > 0)
            failures.Add($"Cities with no recruits at all: {string.Join(", ", emptyCities)}.");

        Console.WriteLine();
        Console.WriteLine($"day 1 pool: {candidates.Count} candidates across {world.Cities.Count} cities, " +
                          $"wages {candidates.Min(c => c.DailyWage):N0}-{candidates.Max(c => c.DailyWage):N0} cr/day, " +
                          $"signing fees {candidates.Min(c => c.SigningFee):N0}-{candidates.Max(c => c.SigningFee):N0} cr");

        Console.WriteLine();
        Console.WriteLine($"{"best hire per lever",-34}{"city",-12}{"level",7}{"wage",9}{"fee",9}   effect");
        Console.WriteLine(new string('-', 90));

        foreach (var skill in cfg.Skills)
        {
            var pick = pools
                .SelectMany(p => p.Pool.Select(c => (p.City, Candidate: c)))
                .OrderByDescending(x => x.Candidate.Skills.TryGetValue(skill.Id, out var v) ? v : 0)
                .ThenBy(x => x.Candidate.DailyWage)
                .First();

            var level = pick.Candidate.Skills[skill.Id];
            var effect = skill.MaxEffect * level / cfg.MaxSkill;

            Console.WriteLine($"{skill.Name + " (" + pick.Candidate.Name + ")",-34}{pick.City.Name,-12}" +
                              $"{level,7}{pick.Candidate.DailyWage,9:N0}{pick.Candidate.SigningFee,9:N0}" +
                              $"   {effect,6:P0} on {skill.Lever}");
        }

        // A maxed-out roster: the strongest terms the game can ever offer.
        // Each ceiling hand sits on the post that claims their lever, or the terms would
        // never see them.
        var maxed = cfg.Skills.Select(s => new CrewMember
        {
            Id = $"ceiling-{s.Id}",
            Name = s.Name,
            PostId = cfg.PostFor(s.Lever)?.Id ?? "",
            Skills = cfg.Skills.ToDictionary(x => x.Id, x => x.Id == s.Id ? cfg.MaxSkill : 1)
        }).ToList();

        var terms = CrewMath.Terms(maxed, cfg);
        var eco = world.Config.Economy;

        // Checked with a full intake as well as an empty one: the buy side reads the
        // shelf and the sell side reads the total, so a glutted city is the case where
        // the two are furthest apart.
        var worstCase = world.Cities
            .SelectMany(city => world.Goods.Select(good => (city, good)))
            .SelectMany(pair =>
            {
                var profile = pair.city.Market[pair.good.Id];
                var shelf = Economy.InitialStock(profile, eco);

                // Empty intake and a glutted one: the second is where the shelf and the
                // total are furthest apart, and so where an inversion would show first.
                return new[] { 0.0, profile.Equilibrium * 5 }.Select(intake =>
                {
                    var stock = new CityStock(shelf, intake);
                    var buy = Economy.BuyUnitPrice(pair.good, profile, stock, eco, terms);
                    var sell = Economy.SellUnitPrice(pair.good, profile, stock, eco, terms);
                    return sell - buy;
                });
            })
            .Max();

        Console.WriteLine();
        Console.WriteLine($"with a perfect crew: buy spread share {terms.BuySpreadShare:P0}, " +
                          $"sell spread share {terms.SellSpreadShare:P0}, " +
                          $"speed x{CrewMath.SpeedMultiplier(maxed, cfg):0.00}, " +
                          $"running costs x{CrewMath.RunningCostMultiplier(maxed, cfg):0.00}");
        Console.WriteLine($"best in-place round trip with that crew: {worstCase:0.000} cr/unit " +
                          "(must not be positive)");

        if (worstCase > 0)
        {
            failures.Add($"A perfect crew can buy and sell in the same city for {worstCase:0.00} cr/unit. " +
                         "Crew must erode the spread, never invert it.");
        }
    }

}
