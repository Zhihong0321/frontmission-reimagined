using MechaTrader.Content;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Tests;

/// <summary>
/// Shared access to the shipping content. Loaded once: the loader does real validation
/// work and every test wants the same immutable result.
/// </summary>
public static class TestWorld
{
    private static readonly Lazy<WorldData> Instance = new(() => ContentLoader.LoadWorld());

    public static WorldData Shipping => Instance.Value;

    /// <summary>Walks up from the test binaries to the repository root.</summary>
    public static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MechaTrader.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate MechaTrader.sln above the test output directory.");
    }
}
