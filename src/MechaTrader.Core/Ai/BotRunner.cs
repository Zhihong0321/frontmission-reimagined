using MechaTrader.Core.Commands;
using MechaTrader.Core.Sim;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Ai;

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

        // Bound the loop independently of the day counter: a policy that only issues
        // non-time-advancing commands must not be able to spin forever.
        var guard = days * 64 + 1024;

        while (game.State.Day < targetDay && guard-- > 0)
        {
            var command = policy.Decide(game, rng) ?? new WaitCommand(1);

            var result = game.Apply(command);
            issued++;

            if (!result.Ok)
            {
                rejected++;
                // Always make progress, so a stuck policy still burns days and ends.
                game.Apply(new WaitCommand(1));
            }

            if (game.State.Bankrupt) bankrupt = true;
        }

        return new BotRunResult(
            policy.Name,
            days,
            start,
            game.NetWorth(),
            issued,
            rejected,
            bankrupt);
    }
}
