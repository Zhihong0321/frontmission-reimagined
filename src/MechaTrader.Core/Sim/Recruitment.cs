using MechaTrader.Core.Model;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>Someone offering to sign on, generated rather than authored.</summary>
public sealed record CrewCandidate(
    string Id,
    string Name,
    string RoleId,
    string RoleName,
    IReadOnlyDictionary<string, int> Skills,
    long DailyWage,
    long SigningFee);

/// <summary>
/// The recruitment centre every city keeps.
///
/// A pool is a pure function of (world seed, city, hiring round) and is never stored:
/// the view derives it to draw the board and the command processor derives the same
/// list again to validate a hire, so the two can never disagree. That is also why this
/// does not touch <see cref="State.GameState.RngState"/> - building a view must not
/// advance the world's random sequence, or looking at a screen would change the game.
///
/// Who walks in is shaped by the city: population sets how many, and its industries
/// bias what they are good at, so a trade hub grows brokers and a plant town grows
/// bookkeepers.
/// </summary>
public static class Recruitment
{
    /// <summary>Which pool a day falls in. Day 1 is the first round.</summary>
    public static int RoundFor(int day, CrewConfig cfg)
    {
        var refresh = Math.Max(1, cfg.RefreshDays);
        return Math.Max(0, day - 1) / refresh;
    }

    /// <summary>Days left before the current pool is replaced.</summary>
    public static int DaysUntilRefresh(int day, CrewConfig cfg)
    {
        var refresh = Math.Max(1, cfg.RefreshDays);
        return refresh - Math.Max(0, day - 1) % refresh;
    }

    public static IReadOnlyList<CrewCandidate> PoolFor(WorldData world, City city, ulong seed, int day)
        => PoolForRound(world, city, seed, RoundFor(day, world.Crew));

    public static IReadOnlyList<CrewCandidate> PoolForRound(WorldData world, City city, ulong seed, int round)
    {
        var cfg = world.Crew;
        if (cfg.Roles.Count == 0 || cfg.Skills.Count == 0) return Array.Empty<CrewCandidate>();

        var gen = cfg.Candidates;
        var count = Math.Clamp(
            gen.BasePerCity + (int)Math.Round(city.Population * gen.PerPopulation),
            1,
            Math.Max(1, gen.MaxPerCity));

        var affinity = AffinityFor(city, cfg);
        var pool = new List<CrewCandidate>(count);

        for (var index = 0; index < count; index++)
        {
            pool.Add(Generate(cfg, city, affinity, seed, round, index));
        }

        return pool;
    }

    private static CrewCandidate Generate(
        CrewConfig cfg,
        City city,
        IReadOnlyDictionary<string, int> affinity,
        ulong seed,
        int round,
        int index)
    {
        // One independent stream per candidate, so pool size changes do not reshuffle
        // the people who were already in it.
        var rng = new Rng(Hash(seed, city.Id, round, index));

        var role = cfg.Roles[rng.NextInt(cfg.Roles.Count)];

        // Iterated in content order: the random sequence must not depend on dictionary
        // enumeration order, or two runs of the same seed could differ.
        var skills = new Dictionary<string, int>(cfg.Skills.Count);
        foreach (var skill in cfg.Skills)
        {
            var (min, max) = Band(cfg, role, skill.Id);
            var level = min + rng.NextInt(Math.Max(1, max - min + 1));

            if (affinity.TryGetValue(skill.Id, out var bonus)) level += bonus;

            skills[skill.Id] = Math.Clamp(level, 1, cfg.MaxSkill);
        }

        var wage = CrewMath.WageFor(skills, cfg);

        var name = cfg.FirstNames.Count > 0 && cfg.Surnames.Count > 0
            ? $"{cfg.FirstNames[rng.NextInt(cfg.FirstNames.Count)]} {cfg.Surnames[rng.NextInt(cfg.Surnames.Count)]}"
            : $"Hand {index + 1}";

        return new CrewCandidate(
            Id: $"{city.Id}-r{round}-{index}",
            Name: name,
            RoleId: role.Id,
            RoleName: string.IsNullOrWhiteSpace(role.Name) ? role.Id : role.Name,
            Skills: skills,
            DailyWage: wage,
            SigningFee: wage * Math.Max(0, cfg.SigningFeeDays));
    }

    /// <summary>
    /// The range a skill rolls in. Specialists spike their own skill and are ordinary
    /// elsewhere; a generalist splits the difference on everything and peaks at nothing.
    /// </summary>
    private static (int Min, int Max) Band(CrewConfig cfg, CrewRoleDef role, string skillId)
    {
        var gen = cfg.Candidates;

        if (string.IsNullOrWhiteSpace(role.Primary))
            return ((gen.PrimaryMin + gen.SecondaryMin) / 2, (gen.PrimaryMax + gen.SecondaryMax) / 2);

        return role.Primary == skillId
            ? (gen.PrimaryMin, gen.PrimaryMax)
            : (gen.SecondaryMin, gen.SecondaryMax);
    }

    private static IReadOnlyDictionary<string, int> AffinityFor(City city, CrewConfig cfg)
    {
        var totals = new Dictionary<string, int>();

        foreach (var industryId in city.Industries)
        {
            if (!cfg.IndustryAffinity.TryGetValue(industryId, out var bonuses)) continue;

            foreach (var (skillId, bonus) in bonuses)
            {
                totals[skillId] = totals.TryGetValue(skillId, out var running) ? running + bonus : bonus;
            }
        }

        return totals;
    }

    /// <summary>
    /// FNV-1a over the city id, folded with the seed and round.
    /// <c>string.GetHashCode</c> is randomised per process in .NET, so using it here
    /// would make pools differ between runs of the same save.
    /// </summary>
    private static ulong Hash(ulong seed, string cityId, int round, int index)
    {
        var hash = 0xCBF29CE484222325UL;

        foreach (var c in cityId)
        {
            hash ^= c;
            hash *= 0x100000001B3UL;
        }

        hash ^= seed + 0x9E3779B97F4A7C15UL;
        hash *= 0x100000001B3UL;
        hash ^= (ulong)round * 0xD1B54A32D192ED03UL;
        hash *= 0x100000001B3UL;
        hash ^= (ulong)index * 0xA24BAED4963EE407UL;

        return hash == 0 ? 0x9E3779B97F4A7C15UL : hash;
    }
}
