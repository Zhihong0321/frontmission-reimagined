using MechaTrader.Core.Events;

namespace MechaTrader.Core.Commands;

/// <summary>
/// Every way the player can affect the world. State only ever changes by applying one
/// of these through <see cref="CommandProcessor"/>, which is what makes the game
/// replayable from a seed plus a command list.
/// </summary>
public abstract record Command;

public sealed record BuyCommand(string GoodId, int Units) : Command;

public sealed record SellCommand(string GoodId, int Units) : Command;

/// <summary>Set out for an adjacent city. Time still has to be spent to arrive.</summary>
public sealed record DepartCommand(string ToCityId) : Command;

/// <summary>Advance time, whether parked or on the road. The single clock in the game.</summary>
public sealed record WaitCommand(int Days) : Command;

public sealed record BuyTruckCommand(string TruckTypeId) : Command;

/// <summary>Sign on somebody from the local recruitment centre. Costs a signing fee.</summary>
public sealed record HireCrewCommand(string CandidateId) : Command;

/// <summary>Pay somebody off. Costs severance; the wage stops the same day.</summary>
public sealed record DismissCrewCommand(string CrewId) : Command;

public sealed class CommandResult
{
    public bool Ok { get; private init; }
    public string? Error { get; private init; }
    public IReadOnlyList<GameEvent> Events { get; private init; } = Array.Empty<GameEvent>();

    public static CommandResult Fail(string error) => new() { Ok = false, Error = error };

    public static CommandResult Success(IReadOnlyList<GameEvent> events) => new() { Ok = true, Events = events };
}
