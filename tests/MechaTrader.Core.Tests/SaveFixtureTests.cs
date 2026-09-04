using System.Text.Json;
using MechaTrader.Core.Commands;
using MechaTrader.Core.State;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// Phase A step 6 (`MIGRATION_LEDGER.md`): pre-recorded save fixtures, one build's worth
/// of persisted state loaded back by a later build. Unlike <see cref="DeterminismTests"/>'
/// serialize-then-deserialize-in-the-same-run round trip, these prove the save *format*
/// stays load-compatible going forward — the invariant this ledger names as "existing
/// save/resume behavior remains compatible." Fixtures were captured by
/// <c>dotnet run --project tools/MechaTrader.Fingerprint -- fixtures</c>; regenerate the
/// same way, never hand-edit, when a deliberate save-format or content change lands.
/// </summary>
public class SaveFixtureTests
{
    private static string FixturePath(string file) => Path.Combine(
        TestWorld.RepositoryRoot(), "tests", "MechaTrader.Core.Tests", "Fixtures", "saves", file);

    private static GameState Load(string file)
    {
        var json = File.ReadAllText(FixturePath(file));
        return JsonSerializer.Deserialize<GameState>(json)
            ?? throw new InvalidDataException($"Fixture '{file}' deserialized to null.");
    }

    [Fact]
    public void Day1NewRunLoadsAndResumes()
    {
        var state = Load("day1-new-run.json");
        Assert.Equal(1, state.Day);
        Assert.Equal(20000, state.Cash);
        Assert.Equal("praha", state.Caravan.LocationId);

        var game = Game.Resume(TestWorld.Shipping, state);
        var result = game.Apply(new WaitCommand(1));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(2, game.State.Day);
    }

    [Fact]
    public void TradeCycleLoadsAndResumes()
    {
        var state = Load("trade-cycle.json");
        Assert.Equal(5, state.Day);
        Assert.Equal(19963, state.Cash);
        Assert.Equal("berlin", state.Caravan.LocationId);

        var game = Game.Resume(TestWorld.Shipping, state);
        var result = game.Apply(new WaitCommand(3));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(8, game.State.Day);
    }

    [Fact]
    public void LateRunMixedLoadsAndResumes()
    {
        var state = Load("late-run-mixed.json");
        Assert.Equal(60, state.Day);
        Assert.Equal(-1539, state.Cash);
        Assert.Equal("hamburg", state.Caravan.LocationId);

        // Touched crew (hired and paid off), a storeroom, and an expo pass — among the
        // systems Phase A step 6 asks this fixture to cross. The full-surface script
        // also accepts a contract, but its underfunded delivery attempt (see
        // `DeliverContractCommand` in the coverage matrix) leaves it to lapse well
        // before day 60, so its board acceptance is not observable in this final state.
        Assert.True(state.Warehouses.ContainsKey("praha"));
        Assert.NotEmpty(state.ExpoPasses);

        var game = Game.Resume(TestWorld.Shipping, state);
        var result = game.Apply(new WaitCommand(1));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(61, game.State.Day);
    }

    [Fact]
    public void ResumingNeverTouchesTheRngBeforeTheFirstCommand()
    {
        // Building a view or reading state must not itself advance the world, matching
        // the recruitment/contract/expo/standing invariants elsewhere in this suite.
        var state = Load("late-run-mixed.json");
        var rngBefore = state.RngState;

        var game = Game.Resume(TestWorld.Shipping, state);
        _ = game.View();

        Assert.Equal(rngBefore, game.State.RngState);
    }
}
