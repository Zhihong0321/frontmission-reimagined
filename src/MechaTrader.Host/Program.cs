using System.Text.Json.Serialization;
using MechaTrader.Host;

// The web root lives beside the sources rather than in the build output, so the UI can
// be edited and reloaded without rebuilding the server.
var webRoot = LocateWebRoot();

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

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/state", (GameSession session) => Results.Ok(session.Current()));

// The map never changes for a given world, so it is served once and cached by the client.
app.MapGet("/api/map", (GameSession session) =>
    Results.Ok(MechaTrader.Core.View.ViewBuilder.BuildMap(session.World)));

app.MapPost("/api/command", (GameSession session, CommandRequest request) =>
    Results.Ok(session.Execute(request)));

app.MapPost("/api/new", (GameSession session, NewGameRequest? request) =>
    Results.Ok(session.Restart(request?.Seed)));

// Warm the session before announcing the URL, so a content error surfaces here rather
// than as a 500 on the first request.
var session = app.Services.GetRequiredService<GameSession>();
var world = session.World;

const string url = "http://localhost:5080";

Console.WriteLine();
Console.WriteLine("  MECHA TRADER - Alpha 1");
Console.WriteLine($"  {world.Cities.Count} cities, {world.Goods.Count} goods, {world.Routes.All.Count} roads");
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

public sealed record NewGameRequest(ulong? Seed);
