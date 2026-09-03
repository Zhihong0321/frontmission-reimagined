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
/// is the counterweight. Category knowledge and special traits follow the same rule:
/// the best eye for metals is the one that grades the crate.
///
/// Posts narrow who counts. A lever a post claims (content: crew.json <c>posts</c>) is
/// pulled only by the hands on that post, so the broker has to be at the counter to
/// haggle and the scout has to be on information to read a market. A lever nobody
/// claims is convoy-wide: everyone aboard reads the road and runs the books.
///
/// Every function is a pure read over the roster, so nothing can go stale and no cache
/// has to be invalidated when someone is hired, posted, or paid off.
/// </summary>
public static class CrewMath
{
    /// <summary>Highest level of one skill anywhere in the roster, posts ignored.</summary>
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

    /// <summary>
    /// Highest level of one skill among the hands allowed to use it: if a post claims
    /// the skill's lever, only hands on that post count.
    /// </summary>
    public static int Level(IReadOnlyList<CrewMember> roster, CrewConfig cfg, string skillId)
    {
        var lever = CrewLever.None;
        foreach (var skill in cfg.Skills)
        {
            if (skill.Id == skillId) { lever = skill.Lever; break; }
        }
        return Level(OnPost(roster, cfg, lever), skillId);
    }

    /// <summary>The hand who leads a skill, or null if nobody eligible has it above zero.</summary>
    public static CrewMember? Leader(IReadOnlyList<CrewMember> roster, CrewConfig cfg, string skillId)
    {
        var level = Level(roster, cfg, skillId);
        if (level <= 0) return null;

        var lever = CrewLever.None;
        foreach (var skill in cfg.Skills)
        {
            if (skill.Id == skillId) { lever = skill.Lever; break; }
        }
        foreach (var member in OnPost(roster, cfg, lever))
        {
            if (member.Skill(skillId) == level) return member;
        }
        return null;
    }

    /// <summary>
    /// The hands who pull a lever: everyone if no post claims it, otherwise only those
    /// on the claiming post. Order is roster order, so "first with the top level" is
    /// stable.
    /// </summary>
    public static IReadOnlyList<CrewMember> OnPost(IReadOnlyList<CrewMember> roster, CrewConfig cfg, string lever)
    {
        var post = cfg.PostFor(lever);
        if (post is null) return roster;

        var hands = new List<CrewMember>();
        foreach (var member in roster)
        {
            if (string.Equals(member.PostId, post.Id, StringComparison.Ordinal)) hands.Add(member);
        }
        return hands;
    }

    /// <summary>Whether a hand is on the post that claims a lever (true if none does).</summary>
    public static bool PullsLever(CrewMember member, CrewConfig cfg, string lever)
    {
        var post = cfg.PostFor(lever);
        return post is null || string.Equals(member.PostId, post.Id, StringComparison.Ordinal);
    }

    /// <summary>Effective level of the skill wired to a lever, as a fraction of maxSkill.</summary>
    public static double Factor(IReadOnlyList<CrewMember> roster, CrewConfig cfg, string lever)
    {
        var skill = cfg.SkillFor(lever);
        if (skill is null || cfg.MaxSkill <= 0) return 0.0;

        return Math.Clamp((double)Level(OnPost(roster, cfg, lever), skill.Id) / cfg.MaxSkill, 0.0, 1.0);
    }

    /// <summary>Effect delivered by a lever right now: maxEffect scaled by the crew's level.</summary>
    public static double Effect(IReadOnlyList<CrewMember> roster, CrewConfig cfg, string lever)
    {
        var skill = cfg.SkillFor(lever);
        if (skill is null) return 0.0;

        return skill.MaxEffect * Factor(roster, cfg, lever);
    }

    public static double SpeedMultiplier(IReadOnlyList<CrewMember> roster, CrewConfig cfg)
        => 1.0 + Effect(roster, cfg, CrewLever.Speed)
               + BestTrait(OnPost(roster, cfg, CrewLever.Speed), cfg, t => t.SpeedBonus);

    /// <summary>Multiplier on truck upkeep and fuel. Wages themselves are never discounted.</summary>
    public static double RunningCostMultiplier(IReadOnlyList<CrewMember> roster, CrewConfig cfg)
        => Math.Clamp(
            1.0 - Effect(roster, cfg, CrewLever.Upkeep)
                - BestTrait(OnPost(roster, cfg, CrewLever.Upkeep), cfg, t => t.UpkeepCut),
            0.0, 1.0);

    public static TradeTerms Terms(IReadOnlyList<CrewMember> roster, CrewConfig cfg, string? categoryId = null)
    {
        var buyers = OnPost(roster, cfg, CrewLever.Buy);
        var sellers = OnPost(roster, cfg, CrewLever.Sell);

        var buy = Effect(roster, cfg, CrewLever.Buy)
                  + BestTrait(buyers, cfg, t => t.BuyBargain)
                  + KnowledgeBargainOf(buyers, cfg, categoryId);
        var sell = Effect(roster, cfg, CrewLever.Sell)
                   + BestTrait(sellers, cfg, t => t.SellBargain)
                   + KnowledgeBargainOf(sellers, cfg, categoryId);
        return new TradeTerms(1.0 - buy, 1.0 - sell);
    }

    /// <summary>
    /// Best category knowledge among the hands at the counter, including product-trait
    /// bonuses, as a fraction of maxKnowledge. This is the eye that grades a crate.
    /// </summary>
    public static double KnowledgeFactor(IReadOnlyList<CrewMember> roster, CrewConfig cfg, string? categoryId)
    {
        if (cfg.MaxKnowledge <= 0) return 0.0;
        return Math.Clamp(BestKnowledge(roster, cfg, categoryId) / cfg.MaxKnowledge, 0.0, 1.0);
    }

    /// <summary>Best knowledge of a category among the hands on the trading post.</summary>
    public static double BestKnowledge(IReadOnlyList<CrewMember> roster, CrewConfig cfg, string? categoryId)
    {
        var best = 0.0;
        foreach (var member in OnPost(roster, cfg, CrewLever.Buy))
        {
            var value = KnowledgeOf(member, cfg, categoryId);
            if (value > best) best = value;
        }
        return best;
    }

    /// <summary>One hand's knowledge of a category, plus any product trait that applies.</summary>
    public static double KnowledgeOf(CrewMember member, CrewConfig cfg, string? categoryId)
    {
        var value = string.IsNullOrWhiteSpace(categoryId) ? 0.0 : member.KnowledgeOf(categoryId);
        foreach (var traitId in member.TraitIds)
        {
            var trait = cfg.Trait(traitId);
            if (trait is null || trait.Kind != TraitKind.Product) continue;
            if (string.IsNullOrWhiteSpace(trait.CategoryId) ||
                string.Equals(trait.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase))
            {
                value += trait.KnowledgeBonus;
            }
        }
        return Math.Clamp(value, 0.0, Math.Max(0, cfg.MaxKnowledge));
    }

    /// <summary>
    /// How far knowledge pushes selection from a random crate toward the best crates
    /// still on the pile. Product quality-bonus traits add on top, still clamped to 1.
    /// Only the hands at the counter pick.
    /// </summary>
    public static double SelectionFactor(IReadOnlyList<CrewMember> roster, CrewConfig cfg, string? categoryId)
    {
        var k = KnowledgeFactor(roster, cfg, categoryId);
        k += BestProductQualityBonus(OnPost(roster, cfg, CrewLever.Buy), cfg, categoryId);
        return Math.Clamp(k, 0.0, 1.0);
    }

    public static long DailyWages(IReadOnlyList<CrewMember> roster)
    {
        long total = 0;
        foreach (var member in roster) total += member.DailyWage;
        return total;
    }

    /// <summary>Wage implied by skills alone. Used by tests that do not carry knowledge.</summary>
    public static long WageFor(IReadOnlyDictionary<string, int> skills, CrewConfig cfg)
        => WageFor(skills, null, null, cfg);

    /// <summary>Wage implied by a set of skills, knowledge and traits. Frozen at hire.</summary>
    public static long WageFor(
        IReadOnlyDictionary<string, int> skills,
        IReadOnlyDictionary<string, double>? knowledge,
        IReadOnlyList<string>? traitIds,
        CrewConfig cfg)
    {
        long points = 0;
        foreach (var level in skills.Values) points += level;

        if (traitIds is not null)
        {
            foreach (var id in traitIds)
            {
                var trait = cfg.Trait(id);
                if (trait is not null) points += trait.WagePoints;
            }
        }

        long knowledgePay = 0;
        if (knowledge is not null)
        {
            foreach (var value in knowledge.Values)
                knowledgePay += (long)(value / 10.0) * cfg.Wage.PerKnowledgeTen;
        }

        return cfg.Wage.Base + cfg.Wage.PerSkillPoint * points + knowledgePay;
    }

    public static TradeTerms Terms(CaravanState caravan, WorldData world, string? categoryId = null)
        => Terms(caravan.Crew, world.Crew, categoryId);

    public static double SpeedMultiplier(CaravanState caravan, WorldData world)
        => SpeedMultiplier(caravan.Crew, world.Crew);

    public static double RunningCostMultiplier(CaravanState caravan, WorldData world)
        => RunningCostMultiplier(caravan.Crew, world.Crew);

    /// <summary>Share of the spread the trading post's best eye for a category still erodes.</summary>
    public static double KnowledgeBargain(IReadOnlyList<CrewMember> roster, CrewConfig cfg, string? categoryId)
        => KnowledgeBargainOf(OnPost(roster, cfg, CrewLever.Buy), cfg, categoryId);

    private static double KnowledgeBargainOf(IReadOnlyList<CrewMember> hands, CrewConfig cfg, string? categoryId)
    {
        if (cfg.MaxKnowledge <= 0) return 0.0;

        var best = 0.0;
        foreach (var member in hands)
        {
            var value = KnowledgeOf(member, cfg, categoryId);
            if (value > best) best = value;
        }
        return Math.Clamp(best / cfg.MaxKnowledge, 0.0, 1.0) * Math.Max(0.0, cfg.KnowledgeBargain);
    }

    private static double BestTrait(IReadOnlyList<CrewMember> hands, CrewConfig cfg, Func<CrewTraitDef, double> read)
    {
        var best = 0.0;
        foreach (var member in hands)
        {
            foreach (var traitId in member.TraitIds)
            {
                var trait = cfg.Trait(traitId);
                if (trait is null) continue;
                var value = read(trait);
                if (value > best) best = value;
            }
        }
        return best;
    }

    private static double BestProductQualityBonus(
        IReadOnlyList<CrewMember> hands, CrewConfig cfg, string? categoryId)
    {
        var best = 0.0;
        foreach (var member in hands)
        {
            foreach (var traitId in member.TraitIds)
            {
                var trait = cfg.Trait(traitId);
                if (trait is null || trait.Kind != TraitKind.Product) continue;
                if (!string.IsNullOrWhiteSpace(trait.CategoryId) &&
                    !string.Equals(trait.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (trait.QualityBonus > best) best = trait.QualityBonus;
            }
        }
        return best;
    }
}
