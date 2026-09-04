using MechaTrader.Core.Commands;
using MechaTrader.Core.Sim;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Ai;

/// <summary>
/// What happened when a policy played a fresh game for a fixed number of days.
/// Profit and bankruptcy answer "did it work"; the rest answer "how did the game go".
/// </summary>
public sealed record BotRunResult(
    string PolicyName,
    int Days,
    long StartNetWorth,
    long EndNetWorth,
    int CommandsIssued,
    int CommandsRejected,
    bool WentBankrupt)
{
    public long Profit => EndNetWorth - StartNetWorth;
    public double ReturnPct => StartNetWorth > 0 ? 100.0 * Profit / StartNetWorth : 0.0;
    public double RejectionRate => CommandsIssued > 0 ? (double)CommandsRejected / CommandsIssued : 0.0;

    public IReadOnlyDictionary<string, int> CommandMix { get; init; } =
        new Dictionary<string, int>();

    public IReadOnlyList<string> RejectReasons { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CitiesVisited { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GoodsTraded { get; init; } = Array.Empty<string>();

    public int DaysTravelling { get; init; }
    public int DaysParked { get; init; }
    public long PeakNetWorth { get; init; }
    public long TroughNetWorth { get; init; }
    public int? FirstBankruptDay { get; init; }
    public int EndCrewCount { get; init; }
    public int EndTruckCount { get; init; }
    public double MaxStanding { get; init; }
    public bool SawWorldEvent { get; init; }
    public bool UsedCrew { get; init; }
    public bool UsedTrucks { get; init; }
    public bool UsedFavor { get; init; }
    public bool UsedStation { get; init; }
    public bool UsedContracts { get; init; }
    public bool UsedExpo { get; init; }
}

/// <summary>Plays a policy against a fresh game for a fixed number of days.</summary>
public static class BotRunner
{
    public static BotRunResult Run(WorldData world, ITraderPolicy policy, int days, ulong seed)
    {
        var game = Game.New(world, seed);
        var rng = new Rng(seed ^ 0xA5A5A5A5A5A5A5A5UL);

        var start = game.NetWorth();
        var targetDay = game.State.Day + days;

        var issued = 0;
        var rejected = 0;
        var bankrupt = false;
        int? firstBankruptDay = null;

        var mix = new Dictionary<string, int>(StringComparer.Ordinal);
        var rejectReasons = new SortedSet<string>(StringComparer.Ordinal);
        var cities = new SortedSet<string>(StringComparer.Ordinal);
        var goods = new SortedSet<string>(StringComparer.Ordinal);

        var travelDays = 0;
        var parkedDays = 0;
        var peak = start;
        var trough = start;
        var maxStanding = 0.0;
        var sawEvent = false;
        var usedCrew = false;
        var usedTrucks = false;
        var usedFavor = false;
        var usedStation = false;
        var usedContracts = false;
        var usedExpo = false;

        NoteCity(game, cities);
        NoteStanding(game, ref maxStanding);
        if (game.State.ActiveEvents.Count > 0) sawEvent = true;

        // Bound the loop independently of the day counter: a policy that only issues
        // non-time-advancing commands must not be able to spin forever.
        var guard = days * 64 + 1024;

        while (game.State.Day < targetDay && guard-- > 0)
        {
            var command = policy.Decide(game, rng) ?? new WaitCommand(1);
            var kind = Kind(command);
            mix[kind] = mix.TryGetValue(kind, out var n) ? n + 1 : 1;

            var traveling = game.State.Caravan.IsTraveling;
            var dayBefore = game.State.Day;

            var result = game.Apply(command);
            issued++;

            if (!result.Ok)
            {
                rejected++;
                if (!string.IsNullOrWhiteSpace(result.Error)) rejectReasons.Add(result.Error);
                // Always make progress, so a stuck policy still burns days and ends.
                game.Apply(new WaitCommand(1));
            }
            else
            {
                if (command is HireCrewCommand) usedCrew = true;
                else if (command is BuyTruckCommand) usedTrucks = true;
                else if (command is CityFavorCommand) usedFavor = true;
                else if (command is SellTruckCommand or UpgradeTruckCommand) usedStation = true;
                else if (command is AcceptContractCommand or DeliverContractCommand) usedContracts = true;
                else if (command is ExpoRegisterCommand or ExpoListCommand) usedExpo = true;

                if (command is BuyCommand buy) goods.Add(buy.GoodId);
                else if (command is SellCommand sell) goods.Add(sell.GoodId);
            }

            var elapsed = game.State.Day - dayBefore;
            if (elapsed > 0)
            {
                if (traveling) travelDays += elapsed;
                else parkedDays += elapsed;
            }

            var worth = game.NetWorth();
            if (worth > peak) peak = worth;
            if (worth < trough) trough = worth;

            NoteCity(game, cities);
            NoteStanding(game, ref maxStanding);
            if (game.State.ActiveEvents.Count > 0) sawEvent = true;

            if (game.State.Bankrupt)
            {
                bankrupt = true;
                firstBankruptDay ??= game.State.Day;
            }
        }

        return new BotRunResult(
            policy.Name,
            days,
            start,
            game.NetWorth(),
            issued,
            rejected,
            bankrupt)
        {
            CommandMix = mix,
            RejectReasons = rejectReasons.ToList(),
            CitiesVisited = cities.ToList(),
            GoodsTraded = goods.ToList(),
            DaysTravelling = travelDays,
            DaysParked = parkedDays,
            PeakNetWorth = peak,
            TroughNetWorth = trough,
            FirstBankruptDay = firstBankruptDay,
            EndCrewCount = game.State.Caravan.Crew.Count,
            EndTruckCount = game.State.Caravan.Trucks.Count,
            MaxStanding = maxStanding,
            SawWorldEvent = sawEvent,
            UsedCrew = usedCrew,
            UsedTrucks = usedTrucks,
            UsedFavor = usedFavor,
            UsedStation = usedStation,
            UsedContracts = usedContracts,
            UsedExpo = usedExpo
        };
    }

    private static void NoteCity(Game game, SortedSet<string> cities)
    {
        if (game.State.Caravan.LocationId is { } id) cities.Add(id);
    }

    private static void NoteStanding(Game game, ref double maxStanding)
    {
        foreach (var cityId in game.State.CityStanding.Keys)
        {
            var value = game.State.StandingOf(cityId);
            if (value > maxStanding) maxStanding = value;
        }
    }

    private static string Kind(Command command) => command switch
    {
        BuyCommand => "buy",
        SellCommand => "sell",
        DepartCommand => "depart",
        WaitCommand => "wait",
        BuyTruckCommand => "buytruck",
        BuyGearCommand => "buygear",
        HireCrewCommand => "hirecrew",
        DismissCrewCommand => "dismisscrew",
        AssignCrewCommand => "assigncrew",
        CityFavorCommand => "favor",
        RentWarehouseCommand => "rentwarehouse",
        WarehouseDepositCommand => "warehousedeposit",
        WarehouseWithdrawCommand => "warehousewithdraw",
        SetWarehouseSellCommand => "warehousesell",
        SetWarehouseProcureCommand => "warehouseprocure",
        SellTruckCommand => "selltruck",
        UpgradeTruckCommand => "upgradetruck",
        AcceptContractCommand => "acceptcontract",
        DeliverContractCommand => "delivercontract",
        ExpoRegisterCommand => "exporegister",
        ExpoListCommand => "expolist",
        _ => command.GetType().Name
    };
}
