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
    public long DailyWage { get; set; }
    public int HiredDay { get; set; }
    public string HiredAtCityId { get; set; } = "";

    public Dictionary<string, int> Skills { get; set; } = new();

    public int Skill(string skillId) => Skills.TryGetValue(skillId, out var v) ? v : 0;
}
