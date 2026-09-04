namespace MechaTrader.Core.Model;

/// <summary>The catalogue of world events. Loaded from events.json.</summary>
public sealed class EventsConfig
{
    public int MaxConcurrent { get; init; } = 3;
    public double DailyChance { get; init; } = 0.2;
    public List<EventDef> Events { get; init; } = new();

    public EventDef? ById(string id)
    {
        foreach (var evt in Events)
        {
            if (evt.Id == id) return evt;
        }
        return null;
    }
}
