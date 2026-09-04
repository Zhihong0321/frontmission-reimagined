using System.Text.Json;
using MechaTrader.Content;
using MechaTrader.Core;

namespace MechaTrader.Fingerprint;

/// <summary>
/// Regenerates the Phase A determinism fingerprints and save fixtures on demand, so a
/// future intentional content or engine change can re-baseline the golden values
/// checked into `DeterminismFingerprintTests.cs` and `SaveFixtureTests.cs` without
/// hand-editing hex strings. Never run automatically by `check.ps1` — this is a
/// developer tool, not a gate.
/// </summary>
public static class Program
{
    private const ulong FingerprintSeed = 424242;

    public static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
        var world = ContentLoader.LoadWorld();

        switch (command)
        {
            case "fingerprint":
                return RunFingerprint(world);
            case "content":
                return RunContent(world);
            case "fixtures":
                var outDir = args.Length > 1 ? args[1] : "tests/MechaTrader.Core.Tests/Fixtures/saves";
                return RunFixtures(world, outDir);
            case "all":
                var a = RunFingerprint(world);
                var b = RunContent(world);
                var c = RunFixtures(world, args.Length > 1 ? args[1] : "tests/MechaTrader.Core.Tests/Fixtures/saves");
                return a == 0 && b == 0 && c == 0 ? 0 : 1;
            default:
                Console.Error.WriteLine($"Unknown command '{command}'. Use: fingerprint | content | fixtures [dir] | all [dir]");
                return 2;
        }
    }

    private static int RunFingerprint(MechaTrader.Core.World.WorldData world)
    {
        var result = Scripts.RunFullSurfaceScript(world, FingerprintSeed);
        var fState = Fingerprints.FState(result.Game.State);
        var fView = Fingerprints.FView(result.Game.View());

        Console.WriteLine($"seed: {FingerprintSeed}");
        Console.WriteLine($"F_state: {fState}");
        Console.WriteLine($"F_view: {fView}");
        Console.WriteLine($"day: {result.Game.State.Day}  cash: {result.Game.State.Cash}");
        Console.WriteLine();
        Console.WriteLine("command coverage:");
        foreach (var group in result.Applied.GroupBy(a => a.Command.GetType().Name))
        {
            var ok = group.Count(a => a.Ok);
            Console.WriteLine($"  {group.Key}: {group.Count()} issued, {ok} ok, {group.Count() - ok} rejected");
        }
        Console.WriteLine();
        Console.WriteLine("rejections:");
        foreach (var a in result.Applied.Where(a => !a.Ok))
            Console.WriteLine($"  [{a.Command.GetType().Name}] {a.Command} -> {a.Error}");

        var scripted = new HashSet<string>(Scripts.CommandCoverageMatrix
            .Where(e => e.How == Coverage.Scripted).Select(e => e.CommandType));
        var issued = new HashSet<string>(result.Applied.Select(a => a.Command.GetType().Name));
        var missing = scripted.Except(issued).ToList();
        if (missing.Count > 0)
        {
            Console.Error.WriteLine($"Coverage matrix claims {string.Join(", ", missing)} as Scripted but the script never issued them.");
            return 1;
        }

        return 0;
    }

    private static int RunContent(MechaTrader.Core.World.WorldData world)
    {
        var dataDir = ContentLoader.FindDataDirectory();
        var files = ContentLoader.ReadAll(dataDir);
        var manifest = Fingerprints.FContent(files);

        Console.WriteLine($"data directory: {dataDir}");
        foreach (var (file, hash) in manifest) Console.WriteLine($"  {file}  {hash}");
        return 0;
    }

    private static int RunFixtures(MechaTrader.Core.World.WorldData world, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var manifest = new List<object>();

        // day1-new-run.json: Game.New only, zero commands.
        var day1 = Game.New(world, 111);
        var day1Path = Path.Combine(outDir, "day1-new-run.json");
        File.WriteAllText(day1Path, JsonSerializer.Serialize(day1.State, options));
        manifest.Add(new { file = "day1-new-run.json", seed = 111, script = "none", sha256 = Fingerprints.Sha256Hex(JsonSerializer.Serialize(day1.State)) });
        Console.WriteLine($"wrote {day1Path}");

        // trade-cycle.json: one buy/depart/wait/sell cycle.
        var tradeCycle = Game.New(world, 222);
        foreach (var cmd in Scripts.BuildTradeCycleScript(world)) tradeCycle.Apply(cmd);
        var tradeCyclePath = Path.Combine(outDir, "trade-cycle.json");
        File.WriteAllText(tradeCyclePath, JsonSerializer.Serialize(tradeCycle.State, options));
        manifest.Add(new { file = "trade-cycle.json", seed = 222, script = "BuildTradeCycleScript", sha256 = Fingerprints.Sha256Hex(JsonSerializer.Serialize(tradeCycle.State)) });
        Console.WriteLine($"wrote {tradeCyclePath}");

        // late-run-mixed.json: the full-surface script, ~60+ days across crew, warehouse,
        // contract, expo, and standing.
        var mixed = Scripts.RunFullSurfaceScript(world, FingerprintSeed);
        var mixedPath = Path.Combine(outDir, "late-run-mixed.json");
        File.WriteAllText(mixedPath, JsonSerializer.Serialize(mixed.Game.State, options));
        manifest.Add(new { file = "late-run-mixed.json", seed = FingerprintSeed, script = "RunFullSurfaceScript", sha256 = Fingerprints.Sha256Hex(JsonSerializer.Serialize(mixed.Game.State)) });
        Console.WriteLine($"wrote {mixedPath}");

        var manifestPath = Path.Combine(outDir, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, options));
        Console.WriteLine($"wrote {manifestPath}");

        return 0;
    }
}
