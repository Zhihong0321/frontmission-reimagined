using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MechaTrader.Core.State;
using MechaTrader.Core.View;

namespace MechaTrader.Fingerprint;

/// <summary>
/// SHA-256 fingerprints over the simulation's own serialization, pinned on disk so a
/// mechanical refactor (Phase C/D) that silently changes iteration order, property
/// order, or RNG consumption is caught by a byte-level diff rather than only by an
/// in-process comparison. See Phase A step 6 (`MIGRATION_PLAN.md`) and the accepted
/// `PA-KIMI-01` design (`D-015`).
/// </summary>
public static class Fingerprints
{
    /// <summary>
    /// `F_state`: byte-level fingerprint of the raw state serialization. Catches
    /// iteration-order, property-order, and RNG-consumption regressions directly —
    /// exactly what a mechanical split must preserve.
    /// </summary>
    public static string FState(GameState state) => Sha256Hex(JsonSerializer.Serialize(state));

    /// <summary>
    /// `F_view`: fingerprint of a canonicalized `GameView` — object keys sorted so the
    /// hash is stable across any future reordering of record properties or a runtime
    /// upgrade, while still catching any real change to what a front-end would receive.
    /// </summary>
    public static string FView(GameView view) => Sha256Hex(Canonicalize(JsonSerializer.Serialize(view)));

    /// <summary>
    /// `F_content`: a `(filename, sha256)` manifest over every required data file, so a
    /// content edit and a serialization-order regression can never be confused for each
    /// other in the same fingerprint. Line endings are normalized to `\n` first: git's
    /// checkout line-ending behavior (`core.autocrlf`) can differ between a clone and a
    /// worktree of the very same commit, and that is an environment detail, not a
    /// content change this fingerprint should ever fail on.
    /// </summary>
    public static IReadOnlyDictionary<string, string> FContent(IReadOnlyDictionary<string, string> files)
    {
        var manifest = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, text) in files) manifest[key + ".json"] = Sha256Hex(NormalizeLineEndings(text));
        return manifest;
    }

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n");

    public static string Sha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Re-serializes JSON with every object's keys sorted, recursively.</summary>
    public static string Canonicalize(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(document.RootElement, writer);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(item, writer);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
