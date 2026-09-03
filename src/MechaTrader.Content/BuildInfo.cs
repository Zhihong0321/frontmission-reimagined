using System.Diagnostics;
using System.Reflection;

namespace MechaTrader.Content;

/// <summary>One commit, as the build page shows it.</summary>
public sealed record CommitEntry(
    string Hash,
    string Subject,
    string Author,
    string When,
    // True for the commit this build was made from.
    bool IsHead);

/// <summary>
/// Which build is actually running, and whether it is still the newest one.
///
/// The question this exists to answer is "am I testing what is on disk?", and the honest
/// answer needs three separate facts: when the binary was compiled, what the repository
/// says HEAD is, and whether any source has been touched since. A version string alone
/// cannot tell you that you forgot to rebuild.
/// </summary>
public sealed record BuildInfo(
    string Version,
    string BuiltAtUtc,
    // "4 minutes ago" - the figure a human actually reads.
    string BuiltAgo,
    // Something on disk is newer than what is running: you are testing yesterday.
    bool Stale,
    // Code changed after this binary was compiled.
    bool NeedsRebuild,
    // Content changed after this server started; the world is loaded once, at startup.
    bool NeedsRestart,
    string StaleReason,
    bool GitAvailable,
    string Branch,
    string Commit,
    string CommitSubject,
    string CommitAuthor,
    string CommitWhen,
    // Uncommitted work in the tree, so HEAD does not fully describe this build.
    bool Dirty,
    int DirtyFiles,
    IReadOnlyList<CommitEntry> Log);

/// <summary>
/// Reads build metadata off the working copy.
///
/// Lives in <c>MechaTrader.Content</c> for the same reason <see cref="ContentLoader"/>
/// does: this is the one project allowed to touch a filesystem, and none of it is a game
/// rule. The simulation neither knows nor cares which build is running.
///
/// Every lookup degrades rather than throws. A copy with no git, no repository and no
/// VERSION file still reports its assembly version and when it was compiled, which is
/// what a shipped build will look like.
/// </summary>
public static class BuildInfoReader
{
    private const int GitTimeoutMs = 4000;
    private const int LogEntries = 20;

    /// <summary>Written by the build itself, so nothing here is a real change.</summary>
    private static readonly string[] IgnoredDirectories = { "obj", "bin", ".git", ".vs", "node_modules" };

    /// <summary>
    /// Code. A change here is only running once the solution has been rebuilt, so it is
    /// measured against when the binary was compiled. Only <c>src/</c> feeds the game
    /// binary: a change confined to <c>tests/</c> or <c>tools/</c> rebuilds neither the
    /// game nor the host DLLs, so counting them would make the page cry stale on a
    /// perfectly current game every time a test or the harness was touched.
    /// </summary>
    private static readonly string[] CodeDirectories = { "src" };

    private static readonly string[] CodeExtensions = { ".cs", ".csproj" };

    /// <summary>
    /// Content. A change here needs no rebuild at all - but the world is loaded once, at
    /// startup, so it is measured against when this server process began. Measuring it
    /// against the build time instead would report a false alarm every time content was
    /// edited, and a warning that cries wolf is worse than no warning.
    /// </summary>
    private static readonly string[] ContentDirectories = { "data" };

    private static readonly string[] ContentExtensions = { ".json" };

    /// <param name="startedAtUtc">
    /// When this process began. Overridable so the restart check can be tested; in
    /// normal use it is read from the running process.
    /// </param>
    public static BuildInfo Read(
        string? repoRoot = null, string? assemblyPath = null, DateTime? startedAtUtc = null)
    {
        var root = repoRoot ?? FindRepositoryRoot();
        var built = BuildTimeUtc(assemblyPath);
        var started = startedAtUtc ?? ProcessStartUtc();
        var now = DateTime.UtcNow;

        var (codeFile, codeAt) = root is null ? (null, default(DateTime)) : NewestCode(root);
        var (dataFile, dataAt) = root is null ? (null, default(DateTime)) : NewestContent(root);

        var needsRebuild = codeFile is not null && codeAt > built;
        var needsRestart = dataFile is not null && dataAt > started;

        var log = root is null ? new List<CommitEntry>() : GitLog(root);
        var head = log.FirstOrDefault();

        var branch = root is null ? "" : Git(root, "rev-parse --abbrev-ref HEAD") ?? "";
        var status = root is null ? null : Git(root, "status --porcelain");

        var dirtyFiles = status is null || status.Length == 0
            ? 0
            : status.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        return new BuildInfo(
            Version: VersionString(root),
            BuiltAtUtc: built.ToString("O"),
            BuiltAgo: Ago(now - built),
            Stale: needsRebuild || needsRestart,
            NeedsRebuild: needsRebuild,
            NeedsRestart: needsRestart,
            StaleReason: needsRebuild
                ? $"{codeFile} changed {Ago(now - codeAt)}, after this build was compiled - rebuild to pick it up"
                : needsRestart
                    ? $"{dataFile} changed {Ago(now - dataAt)}, after this server started - restart to load it"
                    : "",
            GitAvailable: head is not null,
            Branch: branch,
            Commit: head?.Hash ?? "",
            CommitSubject: head?.Subject ?? "",
            CommitAuthor: head?.Author ?? "",
            CommitWhen: head?.When ?? "",
            Dirty: dirtyFiles > 0,
            DirtyFiles: dirtyFiles,
            Log: log);
    }

    /// <summary>The newest source file anyone has edited, and when.</summary>
    public static (string? Path, DateTime ChangedAtUtc) NewestCode(string repoRoot)
        => NewestChange(repoRoot, CodeDirectories, CodeExtensions);

    /// <summary>The newest content file anyone has edited, and when.</summary>
    public static (string? Path, DateTime ChangedAtUtc) NewestContent(string repoRoot)
        => NewestChange(repoRoot, ContentDirectories, ContentExtensions);

    /// <summary>
    /// The newest thing anyone has edited under these directories, and when. Build output
    /// is skipped: a rebuild writes into obj/ and bin/ by definition, so counting those
    /// would make every build look stale the instant it finished.
    /// </summary>
    public static (string? Path, DateTime ChangedAtUtc) NewestChange(
        string repoRoot, IReadOnlyList<string> directories, IReadOnlyList<string> extensions)
    {
        string? newestPath = null;
        var newest = DateTime.MinValue;

        foreach (var directory in directories)
        {
            var full = Path.Combine(repoRoot, directory);
            if (!Directory.Exists(full)) continue;

            foreach (var file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
            {
                if (!extensions.Contains(Path.GetExtension(file))) continue;
                if (IsBuildOutput(file, repoRoot)) continue;

                var changed = File.GetLastWriteTimeUtc(file);
                if (changed <= newest) continue;

                newest = changed;
                newestPath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            }
        }

        return (newestPath, newest);
    }

    private static bool IsBuildOutput(string file, string repoRoot)
    {
        var relative = Path.GetRelativePath(repoRoot, file);

        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (IgnoredDirectories.Contains(segment)) return true;
        }

        return false;
    }

    /// <summary>The repository root, or null when running from a copy that has no sources.</summary>
    public static string? FindRepositoryRoot(string? startDirectory = null)
    {
        var dir = new DirectoryInfo(startDirectory ?? AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MechaTrader.sln"))) return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// The VERSION file is the one place a human sets this. The assembly version is the
    /// fallback so a build with no sources beside it still names itself.
    /// </summary>
    private static string VersionString(string? repoRoot)
    {
        if (repoRoot is not null)
        {
            var path = Path.Combine(repoRoot, "VERSION");

            if (File.Exists(path))
            {
                var text = File.ReadAllText(path).Trim();
                if (text.Length > 0) return text;
            }
        }

        var assembly = Assembly.GetEntryAssembly() ?? typeof(BuildInfoReader).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        var version = informational ?? assembly.GetName().Version?.ToString() ?? "unknown";

        // Strip the "+<sourcerevision>" the SDK appends; the commit is reported separately.
        var plus = version.IndexOf('+');
        return plus > 0 ? version[..plus] : version;
    }

    /// <summary>
    /// When this application was last compiled - the newest of our own assemblies beside
    /// it, not the entry assembly alone.
    ///
    /// That distinction is load-bearing. A change confined to the simulation rewrites
    /// MechaTrader.Core.dll and leaves MechaTrader.Host.dll untouched, because the host's
    /// own sources did not change. Reading the entry assembly would report that rebuild
    /// as never having happened, and the page would call a current build stale.
    /// </summary>
    private static DateTime BuildTimeUtc(string? assemblyPath)
    {
        if (assemblyPath is not null)
        {
            return File.Exists(assemblyPath) ? File.GetLastWriteTimeUtc(assemblyPath) : DateTime.UtcNow;
        }

        var newest = DateTime.MinValue;

        try
        {
            foreach (var dll in Directory.EnumerateFiles(AppContext.BaseDirectory, "MechaTrader*.dll"))
            {
                var written = File.GetLastWriteTimeUtc(dll);
                if (written > newest) newest = written;
            }
        }
        catch
        {
            // Fall through to the entry assembly below.
        }

        if (newest > DateTime.MinValue) return newest;

        var fallback = (Assembly.GetEntryAssembly() ?? typeof(BuildInfoReader).Assembly).Location;

        return !string.IsNullOrEmpty(fallback) && File.Exists(fallback)
            ? File.GetLastWriteTimeUtc(fallback)
            : DateTime.UtcNow;
    }

    /// <summary>
    /// When this server started, which is the honest yardstick for content: the world is
    /// read once at startup and never re-read.
    /// </summary>
    private static DateTime ProcessStartUtc()
    {
        try
        {
            return Process.GetCurrentProcess().StartTime.ToUniversalTime();
        }
        catch
        {
            // Some hosts refuse to report it; assume the process is as old as the build.
            return DateTime.UtcNow;
        }
    }

    private static List<CommitEntry> GitLog(string repoRoot)
    {
        // A unit separator, because commit subjects contain every printable character.
        var output = Git(repoRoot, $"log -n {LogEntries} --pretty=format:%h%x1f%s%x1f%an%x1f%aI");
        var entries = new List<CommitEntry>();

        if (output is null) return entries;

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.TrimEnd('\r').Split('\u001f');
            if (parts.Length < 4) continue;

            entries.Add(new CommitEntry(parts[0], parts[1], parts[2], parts[3], entries.Count == 0));
        }

        return entries;
    }

    /// <summary>
    /// Runs one git command. Anything that goes wrong - no git on PATH, not a repository,
    /// a hung process - is reported as "no answer" rather than as a failure, because a
    /// build page that cannot start is worse than one that says the log is unavailable.
    /// </summary>
    private static string? Git(string repoRoot, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null) return null;

            var output = process.StandardOutput.ReadToEnd();

            if (!process.WaitForExit(GitTimeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                return null;
            }

            return process.ExitCode == 0 ? output.TrimEnd('\r', '\n') : null;
        }
        catch
        {
            return null;
        }
    }

    private static string Ago(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;

        if (span.TotalSeconds < 45) return "just now";
        if (span.TotalMinutes < 2) return "a minute ago";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} minutes ago";
        if (span.TotalHours < 2) return "an hour ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} hours ago";
        if (span.TotalDays < 2) return "yesterday";

        return $"{(int)span.TotalDays} days ago";
    }
}
