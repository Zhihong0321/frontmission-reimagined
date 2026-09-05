using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Commands;

/// <summary>
/// The single place game state is allowed to change. Validates first and mutates only
/// once a command is known to be legal, so a rejected command leaves state untouched.
/// </summary>
public static partial class CommandProcessor
{
    public static CommandResult Execute(GameState state, WorldData world, Command command) => command switch
    {
        BuyCommand c => Buy(state, world, c),
        SellCommand c => Sell(state, world, c),
        DepartCommand c => Depart(state, world, c),
        WaitCommand c => Wait(state, world, c),
        BuyTruckCommand c => BuyTruck(state, world, c),
        SellTruckCommand c => SellTruck(state, world, c),
        UpgradeTruckCommand c => UpgradeTruck(state, world, c),
        BuyGearCommand c => BuyGear(state, world, c),
        HireCrewCommand c => HireCrew(state, world, c),
        DismissCrewCommand c => DismissCrew(state, world, c),
        AssignCrewCommand c => AssignCrew(state, world, c),
        CityFavorCommand c => Favor(state, world, c),
        RentWarehouseCommand => RentWarehouse(state, world),
        WarehouseDepositCommand c => WarehouseDeposit(state, world, c),
        WarehouseWithdrawCommand c => WarehouseWithdraw(state, world, c),
        SetWarehouseSellCommand c => SetWarehousePrice(state, world, c.GoodId, c.Price, sell: true),
        SetWarehouseProcureCommand c => SetWarehousePrice(state, world, c.GoodId, c.Price, sell: false),
        AcceptContractCommand c => AcceptContract(state, world, c),
        DeliverContractCommand c => DeliverContract(state, world, c),
        ExpoRegisterCommand => ExpoRegister(state, world),
        ExpoListCommand c => ExpoList(state, world, c),
        _ => CommandResult.Fail($"Unsupported command '{command.GetType().Name}'.")
    };
}
