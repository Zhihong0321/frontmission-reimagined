using MechaTrader.Core.World;

namespace MechaTrader.Content;

/// <summary>
/// Reads game content off disk and hands it to the core as plain strings.
///
/// This tiny project exists so that <c>MechaTrader.Core</c> can stay free of any
/// filesystem dependency. Godot will one day supply the same strings from res://,
/// and nothing in the simulation has to change.
/// </summary>
public static class ContentLoader
{
    private const string DataDirectoryName = "data";
    private const string SentinelFile = "config.json";

    /// <summary>
    /// Walks up from a starting directory looking for the repository's data folder, so
    /// tests, tools and the web host all find content regardless of their build output path.
    /// </summary>
    public static string FindDataDirectory(string? startDirectory = null)
    {
        var dir = new DirectoryInfo(startDirectory ?? AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, DataDirectoryName);
            if (File.Exists(Path.Combine(candidate, SentinelFile)))
                return candidate;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate a '{DataDirectoryName}' folder containing '{SentinelFile}' " +
            $"searching upward from '{startDirectory ?? AppContext.BaseDirectory}'.");
    }

    public static IReadOnlyDictionary<string, string> ReadAll(string dataDirectory)
    {
        var files = new Dictionary<string, string>(WorldLoader.RequiredKeys.Count);

        foreach (var key in WorldLoader.RequiredKeys)
        {
            var path = Path.Combine(dataDirectory, key + ".json");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Missing content file '{key}.json'.", path);

            files[key] = File.ReadAllText(path);
        }

        return files;
    }

    public static WorldData LoadWorld(string? dataDirectory = null)
    {
        var dir = dataDirectory ?? FindDataDirectory();
        return WorldLoader.Load(ReadAll(dir));
    }
}
