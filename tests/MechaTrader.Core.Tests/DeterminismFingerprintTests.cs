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
        ["cities.json"] = "aa25715d4757c1cc7763144e2b668fe728f5510e0347af26704d3fcdd1dc2062",
        ["citystats.json"] = "edbce70c8842c1eb2f9e902f6a68a32eebeccafc4b71f13277f3ec10105d5a8c",
        ["config.json"] = "248184f5b627ecbd9a8e1384296c25a3cc6c8cf396d3fe6d4855b900d81a6384",
        ["contracts.json"] = "dceed649eb539b30fa79b9e1e480a470dd17c7ed1d687892e4f71ab4587566f0",
        ["crew.json"] = "fd737aba149a83da3a0c1f9421aa6961f005a269bd37e8ede2b6a04ff2e71189",
        ["events.json"] = "d4145f078ef158697f6fc70e847631d995213f653c6ab3c377fc4c4e20065fbf",
        ["expos.json"] = "467532e5bf75c283a4b53cf5c122aa3c3d72d6089962512b6512825ee48531aa",
        ["gear.json"] = "3a8a4ccaffd560989a490805047817dad386be3ef21ae98e51fd3ae1d7104a0d",
        ["goods.json"] = "9ab9f7c3390db64f4366864c117e3cfecfeb07d944462e02e923ebd3f0cc1a9d",
        ["industries.json"] = "a2c55aca61bcd9cdc76336a69fe04d8247046c752f381a613e0df7bed104c244",
        ["map.json"] = "ede84684362ea08ad657d23bec805582ff8e18712dfb1b36890564d7a2426e76",
        ["routes.json"] = "b2f406cef500e191c8d6ee03c2c5dfcf045c1a552384263b277deb63641ecb0b",
        ["standing.json"] = "3593aa1d791db257f8f0c81a6312957548757023142d52cbe1a45c08132393a9",
        ["terrain.json"] = "b68401cae642ab99e41a32d03476b4a1de77ec82703ac5e4459f577ebd67659c",
        ["trucks.json"] = "4d35ece35c83cfccf22bf45a662a8c2fd58cd9258a3fc7f9b9d52b93f92c9c45",
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
