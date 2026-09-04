namespace MechaTrader.Core.View;
/// <summary>
/// The contract board here (empty on the road) and every contract the house holds,
/// wherever it was signed. An offer arrives with the hold already checked against it.
/// </summary>
public sealed record ContractsView(
    string BoardCity,
    int RefreshInDays,
    IReadOnlyList<ContractOfferView> Board,
    IReadOnlyList<HeldContractView> Held);

public sealed record ContractLineView(
    string GoodId,
    string Name,
    string TierColor,
    int Units,
    int Held,
    double HeldQuality,
    bool Satisfied);

public sealed record ContractOfferView(
    string Id,
    string CityId,
    string CityName,
    string KindId,
    string KindName,
    string Blurb,
    IReadOnlyList<ContractLineView> Lines,
    double MinGrade,
    long Reward,
    double Standing,
    int DeadlineDays,
    bool Held,
    bool Closed);

public sealed record HeldContractView(
    string Id,
    string CityId,
    string CityName,
    string KindName,
    string Blurb,
    IReadOnlyList<ContractLineView> Lines,
    double MinGrade,
    long Reward,
    double Standing,
    int Deadline,
    int DaysLeft,
    bool Here,
    bool Deliverable,
    // Why it cannot be settled right now, already worded. Empty when it can.
    string Blocker);

