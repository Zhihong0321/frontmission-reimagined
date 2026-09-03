using MechaTrader.Content;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// The build page exists to answer one question honestly: am I testing what is on disk?
///
/// It is the kind of feature that rots silently - git disappears, a path assumption
/// breaks, and the page keeps rendering something plausible. So these hold down both
/// halves: that it reports the truth here, and that it degrades to "unknown" rather than
/// throwing where there is no repository to read.
/// </summary>
public class BuildInfoTests
{
    private static string Root => TestWorld.RepositoryRoot();

    [Fact]
    public void ReportsTheVersionTheRepositoryDeclares()
    {
        var declared = File.ReadAllText(Path.Combine(Root, "VERSION")).Trim();
        var info = BuildInfoReader.Read(Root);

        Assert.Equal(declared, info.Version);
        Assert.NotEmpty(info.BuiltAtUtc);
        Assert.NotEmpty(info.BuiltAgo);
    }

    [Fact]
    public void ReadsHeadAndTheCommitLogFromTheWorkingCopy()
    {
        var info = BuildInfoReader.Read(Root);

        Assert.True(info.GitAvailable, "git should be readable from the repository root.");
        Assert.NotEmpty(info.Log);
        Assert.NotEmpty(info.Branch);

        var head = info.Log[0];

        Assert.True(head.IsHead, "the newest entry is the commit this build came from.");
        Assert.Equal(head.Hash, info.Commit);
        Assert.Equal(head.Subject, info.CommitSubject);
        Assert.DoesNotContain(info.Log.Skip(1), c => c.IsHead);
    }

    [Fact]
    public void EveryCommitInTheLogIsFullyPopulated()
    {
        // A subject split on the wrong separator would show up as blank rows rather
        // than as an error.
        foreach (var commit in BuildInfoReader.Read(Root).Log)
        {
            Assert.NotEmpty(commit.Hash);
            Assert.NotEmpty(commit.Subject);
            Assert.NotEmpty(commit.Author);
            Assert.NotEmpty(commit.When);
        }
    }

    [Fact]
    public void BuildOutputDoesNotCountAsAChange()
    {
        // A rebuild writes into obj/ and bin/ by definition. If those counted, every
        // build would report itself stale the instant it finished, and the warning
        // would be worth nothing.
        var (path, changed) = BuildInfoReader.NewestCode(Root);

        Assert.NotNull(path);
        Assert.DoesNotContain("/obj/", path);
        Assert.DoesNotContain("/bin/", path);
        Assert.True(changed > DateTime.MinValue);
    }

    [Fact]
    public void CodeIsMeasuredAgainstWhenTheBinaryWasCompiled()
    {
        // Point the reader at a binary older than the newest source and it must call
        // for a rebuild; point it at a newer one and it must not.
        var newest = BuildInfoReader.NewestCode(Root);
        var future = DateTime.UtcNow.AddYears(1);

        var ancient = TempMarker(newest.ChangedAtUtc.AddDays(-1));
        var current = TempMarker(newest.ChangedAtUtc.AddMinutes(1));

        try
        {
            var behind = BuildInfoReader.Read(Root, ancient, future);
            var abreast = BuildInfoReader.Read(Root, current, future);

            Assert.True(behind.NeedsRebuild);
            Assert.True(behind.Stale);
            Assert.Contains(newest.Path!, behind.StaleReason);
            Assert.Contains("rebuild", behind.StaleReason);

            Assert.False(abreast.Stale);
            Assert.Equal("", abreast.StaleReason);
        }
        finally
        {
            File.Delete(ancient);
            File.Delete(current);
        }
    }

    [Fact]
    public void TestsAndToolsChangesDoNotMarkTheGameStale()
    {
        // A change confined to tests/ or tools/ rebuilds neither the game nor the host
        // DLLs, so it must not flag the running game for a rebuild. The acceptance gate
        // caught this crying stale twice in one session: once on a test-file edit, once
        // on a harness edit, both with the game binary fully current. Uncommitted work
        // is already reported separately via the git Dirty flag.
        var root = Directory.CreateTempSubdirectory("mt-srcscan-");
        var srcDir = Directory.CreateDirectory(Path.Combine(root.FullName, "src"));
        var toolsDir = Directory.CreateDirectory(Path.Combine(root.FullName, "tools"));
        var testsDir = Directory.CreateDirectory(Path.Combine(root.FullName, "tests"));

        var srcFile = Path.Combine(srcDir.FullName, "Game.cs");
        var toolsFile = Path.Combine(toolsDir.FullName, "Program.cs");
        var testsFile = Path.Combine(testsDir.FullName, "GameTests.cs");
        File.WriteAllText(srcFile, "");
        File.WriteAllText(toolsFile, "");
        File.WriteAllText(testsFile, "");

        try
        {
            // The tools/ and tests/ files are newest, so a scanner that counted them
            // would pick one; the honest answer is the src/ file.
            File.SetLastWriteTimeUtc(toolsFile, DateTime.UtcNow.AddMinutes(1));
            File.SetLastWriteTimeUtc(testsFile, DateTime.UtcNow.AddMinutes(2));

            var (path, changed) = BuildInfoReader.NewestCode(root.FullName);

            Assert.Equal("src/Game.cs", path);
            Assert.Equal(File.GetLastWriteTimeUtc(srcFile), changed);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void ContentIsMeasuredAgainstWhenTheServerStarted()
    {
        // Editing data/ needs no rebuild - the world is simply read again at startup.
        // Judging it by build time would raise a false alarm on every content change,
        // which is the fastest way to teach someone to ignore the warning.
        var newest = BuildInfoReader.NewestContent(Root);
        var fresh = TempMarker(DateTime.UtcNow.AddYears(1));

        try
        {
            var startedBefore = BuildInfoReader.Read(Root, fresh, newest.ChangedAtUtc.AddMinutes(-1));
            var startedAfter = BuildInfoReader.Read(Root, fresh, newest.ChangedAtUtc.AddMinutes(1));

            Assert.True(startedBefore.NeedsRestart);
            Assert.False(startedBefore.NeedsRebuild);
            Assert.Contains(newest.Path!, startedBefore.StaleReason);
            Assert.Contains("restart", startedBefore.StaleReason);

            Assert.False(startedAfter.Stale);
        }
        finally
        {
            File.Delete(fresh);
        }
    }

    /// <summary>An empty file standing in for a compiled binary of a chosen age.</summary>
    private static string TempMarker(DateTime writtenAtUtc)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mt-build-{Guid.NewGuid():N}.dll");
        File.WriteAllText(path, "");
        File.SetLastWriteTimeUtc(path, writtenAtUtc);
        return path;
    }

    [Fact]
    public void BuildTimeSpansEveryAssemblyWeShip()
    {
        // A change confined to the simulation rewrites MechaTrader.Core.dll and leaves
        // MechaTrader.Host.dll alone. Reading only the entry assembly would report that
        // rebuild as never having happened and call a current build stale - which is
        // exactly what the acceptance gate caught the first time round.
        var built = DateTime.Parse(
            BuildInfoReader.Read(Root).BuiltAtUtc,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();

        var ours = Directory
            .EnumerateFiles(AppContext.BaseDirectory, "MechaTrader*.dll")
            .Select(File.GetLastWriteTimeUtc)
            .ToList();

        Assert.NotEmpty(ours);
        Assert.Equal(ours.Max(), built, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AFolderWithNoRepositoryReportsUnknownRatherThanThrowing()
    {
        // This is what a shipped build looks like: no sources, no .git, no VERSION file.
        var empty = Directory.CreateTempSubdirectory("mt-nogit-");

        try
        {
            var info = BuildInfoReader.Read(empty.FullName);

            Assert.False(info.GitAvailable);
            Assert.Empty(info.Log);
            Assert.Equal("", info.Commit);
            Assert.False(info.Stale);
            Assert.False(info.NeedsRebuild);
            Assert.False(info.NeedsRestart);
            Assert.NotEmpty(info.Version);      // falls back to the assembly version
            Assert.NotEmpty(info.BuiltAgo);
        }
        finally
        {
            empty.Delete(recursive: true);
        }
    }
}
