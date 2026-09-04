namespace MechaTrader.Core.View;
/// <summary>One product grade, for legends and colouring. Content passed through.</summary>
public sealed record TierView(int Tier, string Name, string Color, double MinStanding);

