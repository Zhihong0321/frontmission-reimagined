using System.Text.Json;
using System.Text.Json.Serialization;
using MechaTrader.Host;
using Microsoft.Extensions.FileProviders;

// The web root lives beside the sources rather than in the build output, so the UI can
// be edited and reloaded without rebuilding the server.
var webRoot = LocateWebRoot();
var chartRoot = Path.Combine(webRoot, "chart");
var requiredChartFiles = new[]
{
    "chart.html",
    "world.js",
    "game-bridge.js",
    "ops.js",
    "ops.css",
    "chart-tiles-worker.js",
    Path.Combine("art", "manifest.js")
};
var missingChartFiles = requiredChartFiles
    .Where(relative => !File.Exists(Path.Combine(chartRoot, relative)))
    .ToArray();
if (missingChartFiles.Length > 0)
{
    throw new FileNotFoundException(
        $"Consolidated chart root '{chartRoot}' is incomplete; missing: {string.Join(", ", missingChartFiles)}");
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = webRoot
});

builder.Logging.ClearProviders();
builder.Services.AddSingleton<GameSession>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

builder.Services.AddHttpClient("artlab", client =>
{
    client.BaseAddress = new Uri("https://asiasouth.up.railway.app/v1/");
    client.Timeout = TimeSpan.FromMinutes(6);
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Live player view: the consolidated Keeper's Chart below this repository's web root.
var chartFiles = new PhysicalFileProvider(chartRoot);
var chartDefaults = new DefaultFilesOptions
{
    FileProvider = chartFiles,
    RequestPath = "/chart"
};
chartDefaults.DefaultFileNames.Clear();
chartDefaults.DefaultFileNames.Add("chart.html");
app.UseDefaultFiles(chartDefaults);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = chartFiles,
    RequestPath = "/chart"
});

app.MapGet("/api/state", (GameSession session) => Results.Ok(session.Current()));

app.MapGet("/chart/demo-map.html", () => Results.Redirect("/chart/"));

// The map never changes for a given world, so it is served once and cached by the client.
app.MapGet("/api/map", (GameSession session) =>
    Results.Ok(MechaTrader.Core.View.ViewBuilder.BuildMap(session.World)));

app.MapPost("/api/command", (GameSession session, CommandRequest request) =>
    Results.Ok(session.Execute(request)));

app.MapPost("/api/new", (GameSession session, NewGameRequest? request) =>
    Results.Ok(session.Restart(request?.Seed)));

// Read per request rather than cached at startup: the whole point is to answer "is this
// still the newest build", and a cached answer would go stale exactly when it matters.
app.MapGet("/api/build", () => Results.Ok(MechaTrader.Content.BuildInfoReader.Read()));

app.MapGet("/api/artlab/status", (IWebHostEnvironment env) =>
    Results.Ok(new { ok = true, hasKey = !string.IsNullOrWhiteSpace(ReadArtlabKey(env.WebRootPath)) }));

app.MapGet("/api/artlab/library", (IWebHostEnvironment env) =>
{
    var dir = Path.Combine(env.WebRootPath, "artlab", "out");
    if (!Directory.Exists(dir)) return Results.Ok(Array.Empty<object>());
    var files = Directory.GetFiles(dir, "*.png")
        .OrderByDescending(File.GetLastWriteTimeUtc)
        .Take(48)
        .Select(f => new { file = "artlab/out/" + Path.GetFileName(f), slug = Path.GetFileNameWithoutExtension(f) });
    return Results.Ok(files);
});

app.MapPost("/api/artlab/generate", async (IHttpClientFactory httpFactory, IWebHostEnvironment env, ArtlabGenRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Prompt))
        return Results.BadRequest(new { error = "prompt is required" });

    var key = ReadArtlabKey(env.WebRootPath);
    if (string.IsNullOrWhiteSpace(key))
        return Results.Json(new { error = "No API key. Put it in .artlab-secret at the repo root." }, statusCode: 500);

    var client = httpFactory.CreateClient("artlab");
    var model = string.IsNullOrWhiteSpace(req.Model) ? "gpt-image-2" : req.Model.Trim();
    var size = string.IsNullOrWhiteSpace(req.Size) ? "1024x1024" : req.Size;
    var quality = string.IsNullOrWhiteSpace(req.Quality) ? "low" : req.Quality;

    async Task<(int Status, string Raw)> PostAsync(string useModel, string useQuality)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = useModel,
            ["prompt"] = req.Prompt,
            ["n"] = 1,
            ["size"] = size,
            ["output_format"] = "png",
            ["quality"] = useQuality,
            ["background"] = "transparent"
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, "images/generations");
        msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        msg.Content = JsonContent.Create(payload);
        using var res = await client.SendAsync(msg);
        return ((int)res.StatusCode, await res.Content.ReadAsStringAsync());
    }

    var used = model;
    var usedQuality = quality;
    var (status, raw) = await PostAsync(used, usedQuality);

    for (var attempt = 2; attempt <= 3 && ArtlabIsTimeout(status, raw); attempt++)
    {
        await Task.Delay(1500);
        if (attempt == 3 && !string.Equals(usedQuality, "low", StringComparison.OrdinalIgnoreCase))
            usedQuality = "low";
        (status, raw) = await PostAsync(used, usedQuality);
    }

    if (status >= 400 &&
        !string.Equals(used, "gpt-image-1", StringComparison.OrdinalIgnoreCase) &&
        ArtlabShouldFallback(raw))
    {
        used = "gpt-image-1";
        usedQuality = "low";
        (status, raw) = await PostAsync(used, usedQuality);
    }

    if (status >= 400)
        return Results.Json(new { error = FriendlyArtlabError(raw), status, model = used }, statusCode: 502);

    byte[]? bytes = null;
    try
    {
        using var doc = JsonDocument.Parse(raw);
        bytes = await ExtractArtlabPng(client, doc.RootElement);
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = "Unexpected API payload: " + TrimErr(ex.Message + " " + raw) }, statusCode: 502);
    }

    if (bytes is null || bytes.Length == 0)
        return Results.Json(new { error = "API returned no image bytes." }, statusCode: 502);

    var outDir = Path.Combine(env.WebRootPath, "artlab", "out");
    Directory.CreateDirectory(outDir);
    var slug = SanitizeSlug(req.Slug);
    var name = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{slug}.png";
    await File.WriteAllBytesAsync(Path.Combine(outDir, name), bytes);
    await File.WriteAllTextAsync(Path.Combine(outDir, Path.ChangeExtension(name, ".txt")), req.Prompt);

    return Results.Ok(new { b64 = Convert.ToBase64String(bytes), saved = "artlab/out/" + name, model = used });
});

// Warm the session before announcing the URL, so a content error surfaces here rather
// than as a 500 on the first request.
var session = app.Services.GetRequiredService<GameSession>();
var world = session.World;

const string url = "http://localhost:5080";

Console.WriteLine();
var build = MechaTrader.Content.BuildInfoReader.Read();

Console.WriteLine($"  MECHA TRADER {build.Version}");
Console.WriteLine($"  built {build.BuiltAgo}" +
                  (build.Commit.Length > 0 ? $" from {build.Commit} on {build.Branch}" : "") +
                  (build.Dirty ? $" (+{build.DirtyFiles} uncommitted)" : "") +
                  (build.Stale ? "  ** STALE: " + build.StaleReason + " **" : ""));
  Console.WriteLine($"  {world.Cities.Count} cities, {world.Map.Width}×{world.Map.Height} map, {world.Goods.Count} goods, {world.Routes.All.Count} roads");
Console.WriteLine($"  {url}");
Console.WriteLine();

app.Run(url);
return;

static string LocateWebRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);

    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "web");
        if (File.Exists(Path.Combine(candidate, "index.html"))) return candidate;

        dir = dir.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate the 'web' folder containing index.html.");
}

static string? ReadArtlabKey(string webRoot)
{
    var env = Environment.GetEnvironmentVariable("ARTLAB_API_KEY");
    if (!string.IsNullOrWhiteSpace(env)) return env.Trim();

    var dir = new DirectoryInfo(webRoot);
    while (dir is not null)
    {
        var path = Path.Combine(dir.FullName, ".artlab-secret");
        if (File.Exists(path)) return File.ReadAllText(path).Trim();
        dir = dir.Parent;
    }

    return null;
}

static string SanitizeSlug(string? slug)
{
    if (string.IsNullOrWhiteSpace(slug)) return "asset";
    var chars = slug.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
    var s = new string(chars).Trim('-');
    return s.Length == 0 ? "asset" : s;
}

static string TrimErr(string raw)
{
    if (string.IsNullOrEmpty(raw)) return "(empty)";
    raw = raw.Replace('\n', ' ');
    return raw.Length <= 800 ? raw : raw[..800];
}

static bool ArtlabIsTimeout(int status, string raw)
{
    if (status is 408 or 504 or 524 or 598 or 599) return true;
    if (string.IsNullOrEmpty(raw)) return false;
    if (raw.Contains("超时")) return true;
    var s = raw.ToLowerInvariant();
    return s.Contains("timeout") || s.Contains("timed out") || s.Contains("\"code\":504") || s.Contains("\"code\": 504");
}

static string? TryArtlabMessage(string raw)
{
    try
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var err))
        {
            if (err.ValueKind == JsonValueKind.String) return err.GetString();
            if (err.TryGetProperty("message", out var nested) && nested.ValueKind == JsonValueKind.String)
                return nested.GetString();
        }
        if (root.TryGetProperty("message", out var top) && top.ValueKind == JsonValueKind.String)
            return top.GetString();
    }
    catch
    {
        // not JSON — fall through
    }
    return null;
}

static string FriendlyArtlabError(string raw)
{
    var msg = TryArtlabMessage(raw);
    if (string.IsNullOrEmpty(msg)) return TrimErr(raw);
    if (msg.Contains("超时") || msg.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
        msg.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        return "Image gateway timed out. Hit Generate again — retries are automatic, and quality low usually finishes.";
    return msg;
}

static bool ArtlabShouldFallback(string raw)
{
    if (string.IsNullOrEmpty(raw)) return true;
    var s = raw.ToLowerInvariant();
    return s.Contains("background")
        || s.Contains("transparent")
        || s.Contains("not support")
        || s.Contains("unsupported")
        || s.Contains("unknown model")
        || s.Contains("does not exist")
        || s.Contains("invalid model")
        || s.Contains("model_not_found")
        || s.Contains("response_format");
}

static string? ReadArtlabString(JsonElement obj, params string[] names)
{
    foreach (var name in names)
    {
        if (obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
    }
    return null;
}

static async Task<byte[]?> ExtractArtlabPng(HttpClient client, JsonElement root)
{
    JsonElement payload = root;
    if (root.TryGetProperty("data", out var data))
        payload = data.ValueKind == JsonValueKind.Array ? data[0] : data;

    var b64 = ReadArtlabString(payload, "b64_json", "b64")
              ?? ReadArtlabString(root, "b64_json", "b64");
    if (!string.IsNullOrEmpty(b64))
    {
        if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = b64.IndexOf(',');
            if (comma >= 0) b64 = b64[(comma + 1)..];
        }
        return Convert.FromBase64String(b64);
    }

    var url = ReadArtlabString(payload, "url") ?? ReadArtlabString(root, "url");
    if (string.IsNullOrEmpty(url)) return null;
    return await client.GetByteArrayAsync(url);
}

public sealed record ArtlabGenRequest(string Prompt, string? Size, string? Quality, string? Slug, string? Model);

public sealed record NewGameRequest(ulong? Seed);
