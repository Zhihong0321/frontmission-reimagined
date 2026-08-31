namespace MechaTrader.Core.Events;

public enum GameEventKind
{
    Info,
    Trade,
    Travel,
    Arrival,
    Expense,
    Warning
}

/// <summary>
/// Something worth telling the player about. Commands return these rather than writing
/// to a log directly, so the core stays free of any notion of a display.
/// </summary>
public sealed record GameEvent(int Day, GameEventKind Kind, string Message);
