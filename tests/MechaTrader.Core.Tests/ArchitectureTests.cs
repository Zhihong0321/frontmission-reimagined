using System.Text.RegularExpressions;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// Guards the one architectural rule the whole plan rests on: the simulation core is
/// pure. No filesystem, no console, no wall clock, no ambient randomness. Break that
/// and the core stops being portable to Godot, stops being deterministic, and stops
/// being testable, all at once. A grep is cheap insurance against it drifting.
/// </summary>
public class ArchitectureTests
{
    // Word-boundary anchored, so a type named IndustriesFile is not mistaken for File.
    private static readonly (string Pattern, string Label, string Reason)[] Banned =
    {
        (@"\bSystem\.IO\b", "System.IO",
            "the core must not touch the filesystem; load content through MechaTrader.Content"),
        (@"\bFile\.", "File.", "the core must not read or write files"),
        (@"\bDirectory\.", "Directory.", "the core must not walk the filesystem"),
        (@"\bConsole\.", "Console.", "the core must not assume a console; return events instead"),
        (@"\bDateTime\.(Now|UtcNow|Today)\b", "DateTime.Now",
            "wall-clock time breaks determinism; the day counter is the only clock"),
        (@"\bnew\s+Random\s*\(", "new Random(",
            "ambient randomness breaks determinism; use the seeded Rng in game state")
    };

    [Fact]
    public void CoreStaysFreeOfSideChannels()
    {
        var coreDirectory = Path.Combine(TestWorld.RepositoryRoot(), "src", "MechaTrader.Core");
        Assert.True(Directory.Exists(coreDirectory), $"Missing core sources at {coreDirectory}.");

        var sources = Directory.GetFiles(coreDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();

        Assert.NotEmpty(sources);

        var violations = new List<string>();

        foreach (var path in sources)
        {
            var lines = File.ReadAllLines(path);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var code = line.TrimStart();
                if (code.StartsWith("//") || code.StartsWith("///")) continue;

                foreach (var (pattern, label, reason) in Banned)
                {
                    if (Regex.IsMatch(line, pattern))
                        violations.Add($"{Path.GetFileName(path)}:{i + 1} uses '{label}' - {reason}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "The simulation core reached outside itself:" + Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void CoreDoesNotReferenceAnyFrontEnd()
    {
        var project = Path.Combine(TestWorld.RepositoryRoot(), "src", "MechaTrader.Core",
            "MechaTrader.Core.csproj");

        var text = File.ReadAllText(project);

        Assert.DoesNotContain("ProjectReference", text);
        Assert.DoesNotContain("PackageReference", text);
    }
}
