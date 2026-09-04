namespace MechaTrader.Core.State;

/// <summary>
/// Somebody on the payroll.
///
/// Skills are a dictionary keyed by skill id rather than fixed properties, so a new
/// ability is an entry in crew.json and nothing here changes. The wage is frozen at
/// hire time and stored rather than recomputed: the terms you agreed to are part of
/// the save, not something later content retuning can silently rewrite.
/// </summary>
public sealed class CrewMember
{
    /// <summary>Stable candidate id, e.g. "praha-r3-1". Unique for the whole run.</summary>
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";
    public string RoleId { get; set; } = "";

    /// <summary>
    /// The job this hand is on: a post id from crew.json, or empty for none. Levers a
    /// post claims are pulled only by the hands on it; the rest are convoy-wide.
    /// </summary>
    public string PostId { get; set; } = "";

    public long DailyWage { get; set; }
    public int HiredDay { get; set; }
    public string HiredAtCityId { get; set; } = "";

    public Dictionary<string, int> Skills { get; set; } = new();

    /// <summary>Fractional progress toward the next skill point, keyed by skill id.</summary>
    public Dictionary<string, double> SkillXp { get; set; } = new();

    /// <summary>Category id to knowledge 0–100. Missing means none.</summary>
    public Dictionary<string, double> Knowledge { get; set; } = new();

    /// <summary>Special trait ids this hand carries. Looked up in crew content.</summary>
    public List<string> TraitIds { get; set; } = new();

    public int Skill(string skillId) => Skills.TryGetValue(skillId, out var v) ? v : 0;

    public double KnowledgeOf(string categoryId)
        => Knowledge.TryGetValue(categoryId, out var v) ? v : 0.0;
}
