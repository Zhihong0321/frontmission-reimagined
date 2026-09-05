using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.View;

public static partial class ViewBuilder
{
    /// <summary>
    /// The crew board: who is aboard, what the roster is actually worth, and who is
    /// waiting at the local recruitment centre.
    /// </summary>
    private static CrewView BuildCrew(GameState state, WorldData world, City? location)
    {
        var cfg = world.Crew;
        var roster = state.Caravan.Crew;

        var members = roster.Select(m => new CrewMemberView(
            Id: m.Id,
            Name: m.Name,
            RoleName: RoleName(cfg, m.RoleId),
            PostId: m.PostId,
            PostName: cfg.Post(m.PostId)?.Name ?? "",
            DailyWage: m.DailyWage,
            Severance: m.DailyWage * Math.Max(0, cfg.SeveranceDays),
            HiredDay: m.HiredDay,
            HiredAt: world.CitiesById.TryGetValue(m.HiredAtCityId, out var hiredAt) ? hiredAt.Name : "",
            Skills: LevelsOf(cfg, m.Skills),
            Knowledge: KnowledgeOf(world, cfg, m),
            Traits: TraitsOf(cfg, m.TraitIds))).ToList();

        var skills = cfg.Skills.Select(skill =>
        {
            // Posts decide who counts: a broker riding in the back leads nothing.
            var level = CrewMath.Level(roster, cfg, skill.Id);
            var leader = CrewMath.Leader(roster, cfg, skill.Id)?.Name;

            return new CrewSkillView(
                Id: skill.Id,
                Name: skill.Name,
                Lever: skill.Lever,
                Blurb: skill.Blurb,
                Level: level,
                MaxLevel: cfg.MaxSkill,
                LeaderName: leader,
                EffectText: EffectText(state, world, skill, level));
        }).ToList();

        var posts = cfg.Posts.Select(post =>
        {
            var hands = roster.Where(m => m.PostId == post.Id).ToList();
            var gated = cfg.Skills.Where(s => post.Levers.Contains(s.Lever)).Select(s => s.Name);

            // The post's leader is whoever leads its first gated skill, so the roster
            // card can say "Trading · led by X" without the shell ranking anyone.
            var leadSkill = cfg.Skills.FirstOrDefault(s => post.Levers.Contains(s.Lever));
            var leader = leadSkill is null ? null : CrewMath.Leader(roster, cfg, leadSkill.Id)?.Name;

            return new CrewPostView(
                Id: post.Id,
                Name: post.Name,
                Blurb: post.Blurb,
                SkillNames: string.Join(", ", gated),
                Hands: hands.Count,
                LeaderName: leader);
        }).ToList();

        return new CrewView(
            Size: roster.Count,
            Capacity: cfg.CrewCapacity,
            DailyWages: CrewMath.DailyWages(roster),
            Roster: members,
            Skills: skills,
            Posts: posts,
            Intel: BuildIntel(state, world),
            Recruitment: location is null ? null : BuildRecruitment(state, world, location));
    }

    private static IntelView BuildIntel(GameState state, WorldData world)
    {
        var cfg = world.Crew;
        var roster = state.Caravan.Crew;
        var level = Intel.Level(roster, cfg);
        var reach = Intel.Reach(roster, cfg);
        var error = Intel.Error(roster, cfg);
        var informant = Intel.Informant(roster, cfg);

        return new IntelView(
            Active: reach > 0,
            InformantName: informant?.Name,
            Level: level,
            MaxLevel: cfg.MaxSkill,
            Reach: reach,
            MaxReach: Math.Max(cfg.Intel.MinCities, cfg.Intel.MaxCities),
            ErrorPct: Math.Round(error * 100, 1),
            Summary: IntelSummary(reach, error));
    }

    private static string IntelSummary(int reach, double error)
        => reach <= 0
            ? "nobody gathering: no word from other markets"
            : $"reads the {reach} nearest markets within ±{error:P0}";

    /// <summary>
    /// The local hiring board, minus anyone who has already taken a contract this run.
    /// The pool itself is derived from the seed, not stored, so this is a pure read.
    /// </summary>
    private static RecruitmentView BuildRecruitment(GameState state, WorldData world, City city)
    {
        var cfg = world.Crew;
        var roomAboard = state.Caravan.Crew.Count < cfg.CrewCapacity;

        var candidates = Recruitment.PoolFor(world, city, state.Seed, state.Day)
            .Where(c => !state.RecruitedIds.Contains(c.Id))
            .Select(c => new CandidateView(
                Id: c.Id,
                Name: c.Name,
                RoleName: c.RoleName,
                PostName: cfg.Post(cfg.DefaultPost(cfg.Role(c.RoleId)))?.Name ?? "",
                DailyWage: c.DailyWage,
                SigningFee: c.SigningFee,
                Affordable: state.Cash >= c.SigningFee,
                RoomAboard: roomAboard,
                Skills: LevelsOf(cfg, c.Skills),
                Knowledge: KnowledgeOf(world, cfg, c.Knowledge, c.TraitIds),
                Traits: TraitsOf(cfg, c.TraitIds)))
            .ToList();

        return new RecruitmentView(
            CityName: city.Name,
            RefreshInDays: Recruitment.DaysUntilRefresh(state.Day, cfg),
            Candidates: candidates);
    }

    private static List<SkillLevelView> LevelsOf(CrewConfig cfg, IReadOnlyDictionary<string, int> skills)
        => cfg.Skills
            .Select(s => new SkillLevelView(s.Id, s.Name, skills.TryGetValue(s.Id, out var level) ? level : 0))
            .ToList();

    private static List<KnowledgeView> KnowledgeOf(WorldData world, CrewConfig cfg, CrewMember member)
        => KnowledgeOf(world, cfg, member.Knowledge, member.TraitIds);

    private static List<KnowledgeView> KnowledgeOf(
        WorldData world, CrewConfig cfg,
        IReadOnlyDictionary<string, double> knowledge,
        IReadOnlyList<string> traitIds)
    {
        var rows = new List<KnowledgeView>();
        foreach (var category in world.Categories)
        {
            var dummy = new CrewMember
            {
                Knowledge = new Dictionary<string, double>(knowledge),
                TraitIds = traitIds.ToList()
            };
            var level = (int)Math.Round(CrewMath.KnowledgeOf(dummy, cfg, category.Id));
            if (level <= 0) continue;
            rows.Add(new KnowledgeView(category.Id, category.Name, level, cfg.MaxKnowledge));
        }
        return rows;
    }

    private static List<TraitView> TraitsOf(CrewConfig cfg, IReadOnlyList<string> ids)
    {
        var rows = new List<TraitView>();
        foreach (var id in ids)
        {
            var trait = cfg.Trait(id);
            rows.Add(trait is null
                ? new TraitView(id, id, "", "")
                : new TraitView(trait.Id, trait.Name, trait.Kind, trait.Blurb));
        }
        return rows;
    }

    private static WarehouseView BuildWarehouse(GameState state, WorldData world, City? location)
    {
        var cfg = world.Config.Warehouse;
        if (location is null)
        {
            return new WarehouseView(false, cfg.RentCost, cfg.DailyRent, cfg.Capacity, 0,
                Array.Empty<WarehouseLotView>());
        }

        if (!state.Warehouses.TryGetValue(location.Id, out var warehouse))
        {
            return new WarehouseView(false, cfg.RentCost, cfg.DailyRent, cfg.Capacity, 0,
                Array.Empty<WarehouseLotView>());
        }

        var lots = new List<WarehouseLotView>();
        foreach (var good in world.Goods)
        {
            warehouse.Stock.TryGetValue(good.Id, out var lot);
            var units = lot?.Units ?? 0;
            warehouse.AutoSellPrice.TryGetValue(good.Id, out var ask);
            warehouse.AutoProcurePrice.TryGetValue(good.Id, out var bid);
            if (units <= 0 && ask <= 0 && bid <= 0) continue;

            var q = lot?.Quality ?? world.Quality.Nominal;
            lots.Add(new WarehouseLotView(
                good.Id,
                good.Name,
                units,
                Math.Round(q, 1),
                lot is not null && units > 0 && QualityMath.IsSTier(q, world.Quality),
                ask,
                bid));
        }

        return new WarehouseView(
            true,
            cfg.RentCost,
            cfg.DailyRent,
            cfg.Capacity,
            Math.Round(WarehouseMath.UsedVolume(warehouse, world), 1),
            lots);
    }

    private static string RoleName(CrewConfig cfg, string roleId)
    {
        foreach (var role in cfg.Roles)
        {
            if (role.Id == roleId) return string.IsNullOrWhiteSpace(role.Name) ? role.Id : role.Name;
        }
        return roleId;
    }

    /// <summary>
    /// What a skill is worth, stated in the terms the player is already reading
    /// elsewhere on the screen. The price levers erode the market's spread rather than
    /// moving the mid price, so the honest figure to show is the change to the quote.
    /// </summary>
    private static string EffectText(GameState state, WorldData world, CrewSkillDef skill, int level)
    {
        var cfg = world.Crew;
        var spread = world.Config.Economy.Spread;
        var effect = cfg.MaxSkill > 0
            ? skill.MaxEffect * Math.Clamp((double)level / cfg.MaxSkill, 0.0, 1.0)
            : 0.0;

        switch (skill.Lever)
        {
            case CrewLever.Speed:
                var speed = CaravanMath.SpeedKmPerDay(state.Caravan, world);
                return level == 0
                    ? $"nobody reading the road: {speed:N0} km/day"
                    : $"+{effect:P0} convoy speed, now {speed:N0} km/day";

            case CrewLever.Buy:
                return level == 0
                    ? "nobody haggling: the market keeps its full cut"
                    : $"-{spread * effect / (1.0 + spread):P1} off every buy quote";

            case CrewLever.Sell:
                return level == 0
                    ? "nobody closing: the market keeps its full cut"
                    : $"+{spread * effect / (1.0 - spread):P1} on every sell quote";

            case CrewLever.Upkeep:
                var upkeep = CaravanMath.TruckUpkeep(state.Caravan, world);
                return level == 0
                    ? $"nobody on the books: {upkeep:N0} cr/day of truck upkeep"
                    : $"-{effect:P0} truck upkeep and fuel, {upkeep * effect:N0} cr/day";

            case CrewLever.Intel:
                return IntelSummary(
                    Intel.Reach(state.Caravan.Crew, cfg),
                    Intel.Error(state.Caravan.Crew, cfg));

            default:
                return "carried, but nothing in the simulation reads it yet";
        }
    }

}
