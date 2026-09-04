namespace MechaTrader.Core.View;
/// <summary>
/// The payroll, what it buys, and who is available locally.
///
/// Every number here is already resolved into what it does to the convoy - the
/// front-end renders "+21% convoy speed", it does not know that a level is divided by
/// maxSkill and multiplied by a lever's maxEffect.
/// </summary>
public sealed record CrewView(
    int Size,
    int Capacity,
    long DailyWages,
    IReadOnlyList<CrewMemberView> Roster,
    IReadOnlyList<CrewSkillView> Skills,
    IReadOnlyList<CrewPostView> Posts,
    IntelView Intel,
    RecruitmentView? Recruitment);

/// <summary>A job aboard, who is on it, and who leads it. The shell offers these as choices.</summary>
public sealed record CrewPostView(
    string Id,
    string Name,
    string Blurb,
    // The skills this post gates, already named for print.
    string SkillNames,
    int Hands,
    string? LeaderName);

/// <summary>What the information post is delivering right now.</summary>
public sealed record IntelView(
    bool Active,
    string? InformantName,
    int Level,
    int MaxLevel,
    int Reach,
    int MaxReach,
    double ErrorPct,
    // Already formatted, e.g. "reads 5 markets within ±18%".
    string Summary);

public sealed record SkillLevelView(string Id, string Name, int Level);

public sealed record CrewMemberView(
    string Id,
    string Name,
    string RoleName,
    string PostId,
    string PostName,
    long DailyWage,
    long Severance,
    int HiredDay,
    string HiredAt,
    IReadOnlyList<SkillLevelView> Skills,
    IReadOnlyList<KnowledgeView> Knowledge,
    IReadOnlyList<TraitView> Traits);

public sealed record KnowledgeView(string Id, string Name, int Level, int MaxLevel);

public sealed record TraitView(string Id, string Name, string Kind, string Blurb);

/// <summary>One ability, at the level the best hand aboard has, and what that is worth.</summary>
public sealed record CrewSkillView(
    string Id,
    string Name,
    string Lever,
    string Blurb,
    int Level,
    int MaxLevel,
    string? LeaderName,
    string EffectText);

/// <summary>The local recruitment centre. Null while the convoy is on the road.</summary>
public sealed record RecruitmentView(
    string CityName,
    int RefreshInDays,
    IReadOnlyList<CandidateView> Candidates);

public sealed record CandidateView(
    string Id,
    string Name,
    string RoleName,
    // The post they would take on signing; empty means none.
    string PostName,
    long DailyWage,
    long SigningFee,
    bool Affordable,
    bool RoomAboard,
    IReadOnlyList<SkillLevelView> Skills,
    IReadOnlyList<KnowledgeView> Knowledge,
    IReadOnlyList<TraitView> Traits);

