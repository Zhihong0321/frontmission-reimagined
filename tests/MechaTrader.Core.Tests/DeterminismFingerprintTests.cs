using MechaTrader.Content;
using MechaTrader.Core.Commands;
using MechaTrader.Fingerprint;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// Phase A step 6 (`MIGRATION_LEDGER.md`): byte-level fingerprints pinned on disk, so a
/// mechanical refactor that silently changes iteration order, property order, or RNG
/// consumption is caught here rather than only by <see cref="DeterminismTests"/>'s
/// same-process comparison. Golden values were captured by
/// <c>dotnet run --project tools/MechaTrader.Fingerprint -- fingerprint</c> against the
/// real shipped content (<see cref="TestWorld.Shipping"/> already is
/// <c>ContentLoader.LoadWorld()</c>, not a synthetic minimal world) and must be
/// regenerated the same way — never hand-edited — whenever a change intentionally alters
/// serialization, content, or the full-surface script.
/// </summary>
public class DeterminismFingerprintTests
{
    private const ulong FingerprintSeed = 424242;

    // Captured 2026-09-04 against the shipped `data/` content at this commit.
    private const string GoldenFState = "a96681c178a462fee913c495428eb5432720edfaad6fb2593663ef5b842bbe99";
    private const string GoldenFView = "93a94b5cca687a89c9408b3c84c8599eb23b6c901c888a37497c376206af6626";

    private static readonly IReadOnlyDictionary<string, string> GoldenFContent = new Dictionary<string, string>
    {
        ["cities.json"] = "9afbc5307c79f5c9263a1022182027a1ef417cbd0bd1cdef388776f5adc5b63a",
        ["citystats.json"] = "b46d47786ffa84af443be95c9977128f4855a99076eea14012aa046633321821",
        ["config.json"] = "e97ce83c7101089c0f1328907c967a1a315d06f57b9fcc9fd91285f0879318b5",
        ["contracts.json"] = "1f375fa3830a8aefe0627618c8347603a870464f53aca1d63f7d78c65d75125d",
        ["crew.json"] = "429b2458c4eefa6263a03a3d9355292d6b5426099376af2bad147a531eae80bf",
        ["events.json"] = "c041377b66bb622245a2172c811841ead0c7c6e88d0f01597429ecc12843e51b",
        ["expos.json"] = "60b02edade6cb3de46c69ad4866863d8f1353d49b0859801021b5169ecca530e",
        ["gear.json"] = "a88cfc7961240d1fbb15cad5486f3c43e4c6cffb6877f83b7a113ed4635c503e",
        ["goods.json"] = "7e9d8b8d7295eac5279003755ea63138a1d876f4a6d05394fa42c9a0612862e7",
        ["industries.json"] = "2dab0932c9191ff07663d4553a95d4131f8c17899e597efe97730e103f128c26",
        ["map.json"] = "36d9e6e234681a115a3b522b095e0b3be202dc6a08daf0651978bc014bca88db",
        ["routes.json"] = "c130b9f39b2c9c33e018518b8c66e159882f1fa38ce9b0aad8549b9703f5c0aa",
        ["standing.json"] = "ac51740fefdf9e775d1c9d0f2c207d279d8d0d0bab8315ccdcf9478f32ccb78b",
        ["terrain.json"] = "9856925c7bc903dc8e56c7d37fa9e965d81f754e80decf18f358918b131b6698",
        ["trucks.json"] = "817e292566d22b4ab6876ac53d039c38ccc7ecc21a252cd50d9220ca0543d112",
    };

    [Fact]
    public void CoverageMatrixNamesEveryLiveCommandTypeExactlyOnce()
    {
        // The matrix must track `Command`'s real subtypes, not the other way round, so a
        // new command added to Commands.cs fails this test until it is dispositioned
        // here — exactly the "recorded, accepted risk" requirement from `D-016` item 7.
        var liveTypes = typeof(Command).Assembly.GetTypes()
            .Where(t => typeof(Command).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var matrixTypes = Scripts.CommandCoverageMatrix
            .Select(e => e.CommandType)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(liveTypes, matrixTypes);
    }

    [Fact]
    public void EveryScriptedCommandTypeIsActuallyIssued()
    {
        var result = Scripts.RunFullSurfaceScript(TestWorld.Shipping, FingerprintSeed);
        var issued = result.Applied.Select(a => a.Command.GetType().Name).ToHashSet();

        var scripted = Scripts.CommandCoverageMatrix
            .Where(e => e.How == Coverage.Scripted)
            .Select(e => e.CommandType);

        foreach (var commandType in scripted)
            Assert.Contains(commandType, issued);
    }

    [Fact]
    public void FStateMatchesTheGoldenFingerprint()
    {
        var result = Scripts.RunFullSurfaceScript(TestWorld.Shipping, FingerprintSeed);
        Assert.Equal(GoldenFState, Fingerprints.FState(result.Game.State));
    }

    [Fact]
    public void FViewMatchesTheGoldenFingerprint()
    {
        var result = Scripts.RunFullSurfaceScript(TestWorld.Shipping, FingerprintSeed);
        Assert.Equal(GoldenFView, Fingerprints.FView(result.Game.View()));
    }

    [Fact]
    public void FullSurfaceScriptIsDeterministicIndependentOfTheGoldenValue()
    {
        // Belt and suspenders alongside the golden-value facts above: even if content
        // changes and the golden hashes are re-baselined, two runs of the same seed must
        // still agree with each other.
        var first = Scripts.RunFullSurfaceScript(TestWorld.Shipping, FingerprintSeed);
        var second = Scripts.RunFullSurfaceScript(TestWorld.Shipping, FingerprintSeed);

        Assert.Equal(Fingerprints.FState(first.Game.State), Fingerprints.FState(second.Game.State));
        Assert.Equal(Fingerprints.FView(first.Game.View()), Fingerprints.FView(second.Game.View()));
    }

    [Fact]
    public void FContentMatchesTheGoldenManifest()
    {
        var dataDir = ContentLoader.FindDataDirectory(TestWorld.RepositoryRoot());
        var files = ContentLoader.ReadAll(dataDir);
        var manifest = Fingerprints.FContent(files);

        Assert.Equal(GoldenFContent.Count, manifest.Count);
        foreach (var (file, hash) in GoldenFContent)
        {
            Assert.True(manifest.TryGetValue(file, out var actual), $"Manifest is missing '{file}'.");
            Assert.Equal(hash, actual);
        }
    }
}
