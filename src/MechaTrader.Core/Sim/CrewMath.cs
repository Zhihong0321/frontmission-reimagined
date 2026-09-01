using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>
/// What the people on the payroll are worth, in the units the simulation already uses.
///
/// A task is led by the best hand available for it: the effective level of a skill is
/// the highest any single crew member has, not the sum. Ten mediocre drivers do not add
/// up to one great one, so the decision is who to hire rather than how many, and payroll
/// is the counterweight.
///
/// Every function is a pure read over the roster, so nothing can go stale and no cache
/// has to be invalidated when someone is hired or paid off.
/// </summary>
public static class CrewMath
{
    /// <summary>Highest level of one skill anywhere in the roster.</summary>
    public static int Level(IReadOnlyList<CrewMember> roster, string skillId)
    {
        var best = 0;
        foreach (var member in roster)
        {
            var level = member.Skill(skillId);
            if (level > best) best = level;
        }
        return best;
    }

    /// <summary>Effective level of the skill wired to a lever, as a fraction of maxSkill.</summary>
    public static double Factor(IReadOnlyList<CrewMember> roster, CrewConfig cfg, string lever)
    {
        var skill = cfg.SkillFor(lever);
        if (skill is null || cfg.MaxSkill <= 0) return 0.0;

        return Math.Clamp((double)Level(roster, skill.Id) / cfg.MaxSkill, 0.0, 1.0);
    }

    /// <summary>Effect delivered by a lever right now: maxEffect scaled by the crew's level.</summary>
    public static double Effect(IReadOnlyList<CrewMember> roster, CrewConfig cfg, string lever)
    {
        var skill = cfg.SkillFor(lever);
        if (skill is null) return 0.0;

        return skill.MaxEffect * Factor(roster, cfg, lever);
    }

    public static double SpeedMultiplier(IReadOnlyList<CrewMember> roster, CrewConfig cfg)
        => 1.0 + Effect(roster, cfg, CrewLever.Speed);

    /// <summary>Multiplier on truck upkeep and fuel. Wages themselves are never discounted.</summary>
    public static double RunningCostMultiplier(IReadOnlyList<CrewMember> roster, CrewConfig cfg)
        => Math.Clamp(1.0 - Effect(roster, cfg, CrewLever.Upkeep), 0.0, 1.0);

    public static TradeTerms Terms(IReadOnlyList<CrewMember> roster, CrewConfig cfg)
        => new(1.0 - Effect(roster, cfg, CrewLever.Buy), 1.0 - Effect(roster, cfg, CrewLever.Sell));

    public static long DailyWages(IReadOnlyList<CrewMember> roster)
    {
        long total = 0;
        foreach (var member in roster) total += member.DailyWage;
        return total;
    }

    /// <summary>Wage implied by a set of skills. The whole roster is priced by this one rule.</summary>
    public static long WageFor(IReadOnlyDictionary<string, int> skills, CrewConfig cfg)
    {
        long points = 0;
        foreach (var level in skills.Values) points += level;

        return cfg.Wage.Base + cfg.Wage.PerSkillPoint * points;
    }

    public static TradeTerms Terms(CaravanState caravan, WorldData world)
        => Terms(caravan.Crew, world.Crew);

    public static double SpeedMultiplier(CaravanState caravan, WorldData world)
        => SpeedMultiplier(caravan.Crew, world.Crew);

    public static double RunningCostMultiplier(CaravanState caravan, WorldData world)
        => RunningCostMultiplier(caravan.Crew, world.Crew);
}
