using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>
/// Small XP grants when a convoy trades. Category knowledge of the good, and the
/// related lever (negotiation on a buy, sales on a sell). Slight on purpose: a career
/// is many hauls, not one lucky crate.
/// </summary>
public static class TradeXp
{
    public static void Grant(GameState state, WorldData world, string categoryId, string lever, int units)
    {
        if (state.Caravan.Crew.Count == 0 || units <= 0) return;

        var cfg = world.Crew;
        var scale = 0.6 + 0.4 * Math.Log10(1 + units);
        var knowledgeXp = cfg.TradeKnowledgeXp * scale;
        var skillXp = cfg.TradeSkillXp * scale;
        var skill = cfg.SkillFor(lever);

        foreach (var member in state.Caravan.Crew)
        {
            var kScale = 1.0;
            foreach (var traitId in member.TraitIds)
            {
                var trait = cfg.Trait(traitId);
                if (trait is null || trait.Kind != TraitKind.Product) continue;
                if (string.IsNullOrWhiteSpace(trait.CategoryId) ||
                    string.Equals(trait.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    kScale = 1.5;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(categoryId) && cfg.MaxKnowledge > 0)
                AddKnowledge(member, categoryId, knowledgeXp * kScale, cfg.MaxKnowledge);

            if (skill is not null)
                AddSkillXp(member, skill.Id, skillXp, cfg.MaxSkill);
        }
    }

    public static void AddKnowledge(CrewMember member, string categoryId, double amount, int cap)
    {
        if (amount <= 0 || cap <= 0) return;
        var current = member.KnowledgeOf(categoryId);
        member.Knowledge[categoryId] = Math.Clamp(current + amount, 0.0, cap);
    }

    public static void AddSkillXp(CrewMember member, string skillId, double amount, int maxSkill)
    {
        if (amount <= 0 || maxSkill <= 0) return;

        var xp = (member.SkillXp.TryGetValue(skillId, out var running) ? running : 0.0) + amount;
        var level = member.Skill(skillId);

        while (xp >= 1.0 && level < maxSkill)
        {
            xp -= 1.0;
            level++;
        }

        if (level >= maxSkill) xp = 0.0;

        member.Skills[skillId] = level;
        member.SkillXp[skillId] = xp;
    }
}
