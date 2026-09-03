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

/// <summary>
/// Set out for a city, a mining claim, or any map cell. Issuing this while already
/// travelling reroutes from the convoy's current cell. Issuing it for the cell the
/// convoy is already on parks there (a halt). Time still has to be spent to arrive.
/// </summary>
public sealed record DepartCommand(string ToCityId) : Command;

/// <summary>Advance time, whether parked or on the road. The single clock in the game.</summary>
public sealed record WaitCommand(int Days) : Command;

public sealed record BuyTruckCommand(string TruckTypeId) : Command;

/// <summary>Sell one vehicle back to the station, at the resale fraction of it and its fittings.</summary>
public sealed record SellTruckCommand(string TruckId) : Command;

/// <summary>Fit an upgrade to one vehicle. One of each per vehicle; must suit its kind.</summary>
public sealed record UpgradeTruckCommand(string TruckId, string UpgradeId) : Command;

/// <summary>Buy a portable tool in a city. Occupies hold volume.</summary>
public sealed record BuyGearCommand(string GearId) : Command;

/// <summary>Sign on somebody from the local recruitment centre. Costs a signing fee.</summary>
public sealed record HireCrewCommand(string CandidateId) : Command;

/// <summary>Pay somebody off. Costs severance; the wage stops the same day.</summary>
public sealed record DismissCrewCommand(string CrewId) : Command;

/// <summary>
/// Put a hand on a post (trading, information, ...) or take them off one with an empty
/// id. Free, allowed on the road, and the only way a post changes after signing.
/// </summary>
public sealed record AssignCrewCommand(string CrewId, string PostId) : Command;

/// <summary>
/// Court the local governor. The action id names a content entry (donate, invest, aid);
/// adding a fourth gesture is a JSON line, not a new command type.
/// </summary>
public sealed record CityFavorCommand(string ActionId) : Command;

/// <summary>Rent a storeroom in the city the convoy is parked in.</summary>
public sealed record RentWarehouseCommand : Command;

/// <summary>Move cargo from the hold into the local storeroom.</summary>
public sealed record WarehouseDepositCommand(string GoodId, int Units) : Command;

/// <summary>Move cargo from the local storeroom into the hold.</summary>
public sealed record WarehouseWithdrawCommand(string GoodId, int Units) : Command;

/// <summary>Ask the storeroom to auto-sell this good at or above this price. Zero clears the order.</summary>
public sealed record SetWarehouseSellCommand(string GoodId, long Price) : Command;

/// <summary>Ask the storeroom to auto-buy this good at or below this price. Zero clears the order.</summary>
public sealed record SetWarehouseProcureCommand(string GoodId, long Price) : Command;

/// <summary>Take a contract off the local board. The offer is re-derived from the seed to validate it.</summary>
public sealed record AcceptContractCommand(string ContractId) : Command;

/// <summary>Hand over every line of a held contract in the city that issued it, and collect.</summary>
public sealed record DeliverContractCommand(string ContractId) : Command;

/// <summary>Buy a pass for the expo running in this city. Covers every day it is open.</summary>
public sealed record ExpoRegisterCommand : Command;

/// <summary>Put a good on the stall at this asking price per unit. Zero takes it down.</summary>
public sealed record ExpoListCommand(string GoodId, long Price) : Command;

public sealed class CommandResult
{
    public bool Ok { get; private init; }
    public string? Error { get; private init; }
    public IReadOnlyList<GameEvent> Events { get; private init; } = Array.Empty<GameEvent>();

    public static CommandResult Fail(string error) => new() { Ok = false, Error = error };

    public static CommandResult Success(IReadOnlyList<GameEvent> events) => new() { Ok = true, Events = events };
}
