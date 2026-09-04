namespace MechaTrader.Core.View;
public sealed record SiteView(
    string Id,
    string Name,
    string GoodId,
    string GoodName,
    double Remaining,
    double ExpectedYield,
    bool CanMine,
    string Hint);

public sealed record FieldView(
    string CellId,
    string Biome,
    double X,
    double Y);

public sealed record MiningSiteView(
    string Id,
    string Name,
    double X,
    double Y,
    double Remaining,
    bool Depleted);

