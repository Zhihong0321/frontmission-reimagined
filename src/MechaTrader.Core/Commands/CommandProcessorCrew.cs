using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Commands;

public static partial class CommandProcessor
{
    /// <summary>
    /// Sign on a candidate from the city's current recruitment pool.
    ///
    /// The pool is re-derived here from the seed rather than read from anywhere the
    /// front-end could have touched, so a hire can only ever be for somebody the
    /// simulation itself is offering today.
    /// </summary>
    private static CommandResult HireCrew(GameState state, WorldData world, HireCrewCommand cmd)
    {
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail("Nobody signs on mid-road; hire in a city.");

        var cityId = state.Caravan.LocationId;
        if (cityId is null) return CommandResult.Fail("The convoy has no location.");

        var crewConfig = world.Crew;

        if (state.Caravan.Crew.Count >= crewConfig.CrewCapacity)
        {
            return CommandResult.Fail(
                $"The convoy already carries {crewConfig.CrewCapacity} crew; pay somebody off first.");
        }

        if (state.RecruitedIds.Contains(cmd.CandidateId))
            return CommandResult.Fail("That hand has already taken a contract.");

        var city = world.City(cityId);
        var pool = Recruitment.PoolFor(world, city, state.Seed, state.Day);

        var candidate = pool.FirstOrDefault(c => c.Id == cmd.CandidateId);
        if (candidate is null)
            return CommandResult.Fail($"Nobody by that reference is at the {city.Name} recruitment centre.");

        if (state.Cash < candidate.SigningFee)
        {
            return CommandResult.Fail(
                $"Not enough credits: {candidate.SigningFee:N0} signing fee, {state.Cash:N0} held.");
        }

        state.Cash -= candidate.SigningFee;
        state.RecruitedIds.Add(candidate.Id);

        // A hand signs on to the post their trade implies: a broker goes to the counter,
        // a scout to information. The player can move them afterwards.
        var post = crewConfig.DefaultPost(crewConfig.Role(candidate.RoleId));

        state.Caravan.Crew.Add(new CrewMember
        {
            Id = candidate.Id,
            Name = candidate.Name,
            RoleId = candidate.RoleId,
            PostId = post,
            DailyWage = candidate.DailyWage,
            HiredDay = state.Day,
            HiredAtCityId = cityId,
            Skills = new Dictionary<string, int>(candidate.Skills),
            Knowledge = new Dictionary<string, double>(candidate.Knowledge),
            TraitIds = new List<string>(candidate.TraitIds)
        });

        var postDef = crewConfig.Post(post);
        var posted = postDef is null ? "" : $" Posted to {postDef.Name.ToLowerInvariant()}.";

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Crew,
                $"{candidate.Name} signed on at {city.Name} as {candidate.RoleName}: " +
                $"{candidate.SigningFee:N0} cr down, {candidate.DailyWage:N0} cr/day.{posted}")
        });
    }

    /// <summary>
    /// Move a hand between posts. Costs nothing and works on the road, because it is a
    /// matter of who sits where, not of what the city will do for you.
    /// </summary>
    private static CommandResult AssignCrew(GameState state, WorldData world, AssignCrewCommand cmd)
    {
        var member = state.Caravan.Crew.FirstOrDefault(c => c.Id == cmd.CrewId);
        if (member is null) return CommandResult.Fail("Nobody by that reference is on the payroll.");

        var postId = cmd.PostId?.Trim() ?? "";
        var post = world.Crew.Post(postId);
        if (postId.Length > 0 && post is null)
            return CommandResult.Fail($"No such post '{cmd.PostId}'.");

        if (string.Equals(member.PostId, postId, StringComparison.Ordinal))
        {
            return CommandResult.Fail(post is null
                ? $"{member.Name} already holds no post."
                : $"{member.Name} is already on {post.Name.ToLowerInvariant()}.");
        }

        member.PostId = postId;

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Crew, post is null
                ? $"{member.Name} stood down from every post."
                : $"{member.Name} posted to {post.Name.ToLowerInvariant()}.")
        });
    }

    private static CommandResult DismissCrew(GameState state, WorldData world, DismissCrewCommand cmd)
    {
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail("Nobody is put off the convoy mid-road; pay them off in a city.");

        var member = state.Caravan.Crew.FirstOrDefault(c => c.Id == cmd.CrewId);
        if (member is null) return CommandResult.Fail("Nobody by that reference is on the payroll.");

        var severance = member.DailyWage * Math.Max(0, world.Crew.SeveranceDays);
        if (state.Cash < severance)
            return CommandResult.Fail($"Not enough credits: {severance:N0} severance, {state.Cash:N0} held.");

        state.Cash -= severance;
        state.Caravan.Crew.Remove(member);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Crew,
                $"{member.Name} paid off for {severance:N0} cr. " +
                $"Payroll is now {CrewMath.DailyWages(state.Caravan.Crew):N0} cr/day.")
        });
    }
}
