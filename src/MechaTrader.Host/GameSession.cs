using MechaTrader.Content;
using MechaTrader.Core;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Events;
using MechaTrader.Core.View;
using MechaTrader.Core.World;

namespace MechaTrader.Host;

public sealed record CommandRequest(
    string Type,
    string? GoodId,
    int? Units,
    string? ToCityId,
    int? Days,
    string? TruckTypeId,
    string? CandidateId,
    string? CrewId);

public sealed record LogEntry(int Day, string Kind, string Message);

public sealed record Snapshot(GameView View, IReadOnlyList<LogEntry> Log, string? Error);

/// <summary>
/// Holds the one in-progress game for this process and translates web requests into
/// core commands.
///
/// Everything here is adapter work: parsing a JSON body into a <see cref="Command"/>
/// and keeping a display log. No rules live at this layer, which is what makes the
/// browser front-end disposable when the Godot one arrives.
/// </summary>
public sealed class GameSession
{
    private const int MaxLogEntries = 200;

    private readonly object _gate = new();
    private readonly WorldData _world;
    private readonly List<LogEntry> _log = new();

    private Game _game;

    public GameSession()
    {
        _world = ContentLoader.LoadWorld();
        _game = Game.New(_world, DefaultSeed);
        Announce();
    }

    private const ulong DefaultSeed = 20260901;

    public WorldData World => _world;

    public Snapshot Current()
    {
        lock (_gate) return new Snapshot(_game.View(), Recent(), null);
    }

    public Snapshot Restart(ulong? seed)
    {
        lock (_gate)
        {
            _game = Game.New(_world, seed ?? DefaultSeed);
            _log.Clear();
            Announce();
            return new Snapshot(_game.View(), Recent(), null);
        }
    }

    public Snapshot Execute(CommandRequest request)
    {
        lock (_gate)
        {
            if (!TryParse(request, out var command, out var parseError))
                return new Snapshot(_game.View(), Recent(), parseError);

            var result = _game.Apply(command!);

            if (!result.Ok)
                return new Snapshot(_game.View(), Recent(), result.Error);

            foreach (var e in result.Events) Append(e);

            return new Snapshot(_game.View(), Recent(), null);
        }
    }

    private static bool TryParse(CommandRequest request, out Command? command, out string? error)
    {
        command = null;
        error = null;

        switch (request.Type?.ToLowerInvariant())
        {
            case "buy":
                if (request.GoodId is null || request.Units is null)
                    return Reject("buy needs a goodId and units.", out error);
                command = new BuyCommand(request.GoodId, request.Units.Value);
                return true;

            case "sell":
                if (request.GoodId is null || request.Units is null)
                    return Reject("sell needs a goodId and units.", out error);
                command = new SellCommand(request.GoodId, request.Units.Value);
                return true;

            case "depart":
                if (request.ToCityId is null)
                    return Reject("depart needs a toCityId.", out error);
                command = new DepartCommand(request.ToCityId);
                return true;

            case "wait":
                command = new WaitCommand(request.Days ?? 1);
                return true;

            case "buytruck":
                if (request.TruckTypeId is null)
                    return Reject("buyTruck needs a truckTypeId.", out error);
                command = new BuyTruckCommand(request.TruckTypeId);
                return true;

            case "hirecrew":
                if (request.CandidateId is null)
                    return Reject("hireCrew needs a candidateId.", out error);
                command = new HireCrewCommand(request.CandidateId);
                return true;

            case "dismisscrew":
                if (request.CrewId is null)
                    return Reject("dismissCrew needs a crewId.", out error);
                command = new DismissCrewCommand(request.CrewId);
                return true;

            default:
                return Reject($"Unknown command type '{request.Type}'.", out error);
        }
    }

    private static bool Reject(string message, out string? error)
    {
        error = message;
        return false;
    }

    private void Announce()
    {
        var start = _game.View();
        var where = start.Location is null ? "the road" : $"{start.Location.Name}, {start.Location.Region}";

        _log.Add(new LogEntry(start.Day, nameof(GameEventKind.Info),
            $"Convoy registered at {where} with {start.Cash:N0} cr."));
    }

    private void Append(GameEvent e)
    {
        _log.Add(new LogEntry(e.Day, e.Kind.ToString(), e.Message));

        if (_log.Count > MaxLogEntries)
            _log.RemoveRange(0, _log.Count - MaxLogEntries);
    }

    /// <summary>Newest first, so the browser can render without reversing.</summary>
    private List<LogEntry> Recent()
    {
        var slice = _log.Count <= 60 ? _log : _log.GetRange(_log.Count - 60, 60);
        var copy = new List<LogEntry>(slice);
        copy.Reverse();
        return copy;
    }
}
