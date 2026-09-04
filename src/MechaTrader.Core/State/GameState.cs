namespace MechaTrader.Core.State;

/// <summary>
/// A holding of one good. Cost is tracked in total rather than per-unit so that
/// weighted average cost stays exact across many partial buys and sells. Quality is
/// the same idea: a mixed lot is the unit-weighted average of what went into it.
/// </summary>
public sealed class CargoLot
{
    public int Units { get; set; }
    public long TotalCost { get; set; }

    /// <summary>Average grade of this lot, 0–100. S-tier is a read of this number.</summary>
    public double Quality { get; set; } = 70.0;

    public double AverageCost => Units > 0 ? (double)TotalCost / Units : 0.0;

    public void Add(int units, long cost, double quality)
    {
        if (units <= 0) return;
        var next = Units + units;
        Quality = next > 0 ? (Units * Quality + units * quality) / next : quality;
        Units = next;
        TotalCost += cost;
    }
}

/// <summary>One point of a journey's path polyline, in world km.</summary>
public sealed class TrackPoint
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>An in-progress journey along a path of map cells.</summary>
public sealed class TravelState
{
    public string FromId { get; set; } = "";
    public string ToId { get; set; } = "";
    public string FromKind { get; set; } = "city";
    public string ToKind { get; set; } = "city";
    public string FromName { get; set; } = "";
    public string ToName { get; set; } = "";
    public int TotalDays { get; set; }
    public int DaysRemaining { get; set; }
    public double KmPerDay { get; set; }
    public double FuelPerDay { get; set; }

    /// <summary>The cell the convoy parks on at arrival (a real "col,row" cell id).</summary>
    public string ToCellId { get; set; } = "";

    /// <summary>The smoothed, densified route polyline the front-end draws and interpolates along.</summary>
    public List<TrackPoint> Waypoints { get; set; } = new();
}

/// <summary>
/// One vehicle in the convoy. An instance rather than a type id, so a fitting can sit
/// on this truck and not the next one. Effects of the fittings are read by CaravanMath,
/// never stored here.
/// </summary>
public sealed class TruckState
{
    public string Id { get; set; } = "";
    public string TypeId { get; set; } = "";
    public List<string> UpgradeIds { get; set; } = new();
}

/// <summary>The player's convoy: what it hauls with, where it is, and what it carries.</summary>
public sealed class CaravanState
{
    /// <summary>Every vehicle aboard, in the order they were acquired.</summary>
    public List<TruckState> Trucks { get; set; } = new();

    /// <summary>Counter behind vehicle ids, so a sold truck's id is never reused.</summary>
    public int TruckSerial { get; set; }

    /// <summary>Asking prices set on the expo stall, keyed by good. Zero or absent means not listed.</summary>
    public Dictionary<string, long> ExpoAsks { get; set; } = new();

    /// <summary>Portable tools. Type ids; occupy hold volume.</summary>
    public List<string> GearIds { get; set; } = new();

    /// <summary>Everyone on the payroll. Travels with the convoy; hired and paid off in cities.</summary>
    public List<CrewMember> Crew { get; set; } = new();

    /// <summary>Current city, or null while on the road, at a claim, or in open country.</summary>
    public string? LocationId { get; set; }

    /// <summary>Current mining site, or null when not parked on a claim.</summary>
    public string? SiteId { get; set; }

    /// <summary>Cell id when parked in open country (neither a city nor a claim).</summary>
    public string? CellId { get; set; }

    public TravelState? Travel { get; set; }

    public Dictionary<string, CargoLot> Cargo { get; set; } = new();

    public bool IsTraveling => Travel is not null;

    public int Held(string goodId)
        => Cargo.TryGetValue(goodId, out var lot) ? lot.Units : 0;
}

/// <summary>
/// The complete mutable game state. Everything needed to resume a game lives here and
/// nowhere else, which is what makes save/load and deterministic replay straightforward.
/// </summary>
public sealed class GameState
{
    public int Day { get; set; }
    public ulong Seed { get; set; }
    public ulong RngState { get; set; }
    public long Cash { get; set; }
    public bool Bankrupt { get; set; }

    /// <summary>cityId to goodId to what the city holds, shelf and intake.</summary>
    public Dictionary<string, Dictionary<string, CityStock>> Stock { get; set; } = new();

    /// <summary>
    /// cityId to vitalId to that city's live value. Seeded from the city's founding
    /// vitals when a run starts; from then on this is the truth and content is only the
    /// starting point, which is what will let an event move a city without touching data.
    /// </summary>
    public Dictionary<string, Dictionary<string, double>> CityVitals { get; set; } = new();

    /// <summary>
    /// cityId to segmentId to standing. Missing means zero: a save written before a
    /// segment existed has never earned any of it, which is the honest answer. The
    /// total the rank reads is the sum, derived on demand and never stored.
    /// </summary>
    public Dictionary<string, Dictionary<string, double>> CityStanding { get; set; } = new();

    /// <summary>
    /// cityId to permit ids already granted. Sticky: a permit does not vanish if
    /// standing later fell. Derived eligibility is never stored; only the grant is.
    /// </summary>
    public Dictionary<string, HashSet<string>> CityPermits { get; set; } = new();

    public CaravanState Caravan { get; set; } = new();

    /// <summary>
    /// Candidate ids already taken out of the market. Recruitment pools are re-derived
    /// from the seed rather than stored, so this is the only record that someone was
    /// hired; it also stops a dismissed hand reappearing in the same pool.
    /// </summary>
    public HashSet<string> RecruitedIds { get; set; } = new();

    /// <summary>
    /// Currently running world events. Price multipliers and vital overlays are derived
    /// from this list, never stored beside it. A stock shock has already been written
    /// into the city's holding by the time the instance appears here.
    /// </summary>
    public List<ActiveEvent> ActiveEvents { get; set; } = new();

    /// <summary>
    /// Per-run mining deposits. Generated at <c>Game.New</c> from the seed and stored
    /// because they deplete. Positions are state, not content.
    /// </summary>
    public List<MiningSite> MiningSites { get; set; } = new();

    /// <summary>
    /// cityId to a rented storeroom. Missing means this run has never rented there.
    /// Auto prices and stock live on the room; they tick whether the convoy is home or not.
    /// </summary>
    public Dictionary<string, WarehouseState> Warehouses { get; set; } = new();

    /// <summary>
    /// Contracts the player has taken on. Offers are derived from the seed like a
    /// recruitment pool; only the acceptance is state. Removed on delivery or lapse.
    /// </summary>
    public List<ContractState> Contracts { get; set; } = new();

    /// <summary>Contract ids delivered or lapsed, so the board never offers them twice.</summary>
    public HashSet<string> ContractsClosed { get; set; } = new();

    /// <summary>Expo passes bought, as "cityId:round". A pass covers the whole expo.</summary>
    public HashSet<string> ExpoPasses { get; set; } = new();

    /// <summary>
    /// What happened at the stall on the most recent expo day. Stored because it is the
    /// product of that day's random draws; a view must be able to replay it without
    /// drawing again.
    /// </summary>
    public ExpoDayState? LastExpoDay { get; set; }

    public MiningSite? Site(string id)
    {
        foreach (var site in MiningSites)
        {
            if (site.Id == id) return site;
        }
        return null;
    }

    public CityStock StockOf(string cityId, string goodId)
        => Stock.TryGetValue(cityId, out var market) && market.TryGetValue(goodId, out var s)
            ? s
            : default;

    /// <summary>Everything the city owns of a good. This is what the sell price reads.</summary>
    public double TotalStockOf(string cityId, string goodId) => StockOf(cityId, goodId).Total;

    /// <summary>What is on the shelf, and so all a convoy can buy and all the buy price reads.</summary>
    public double ShelfOf(string cityId, string goodId) => StockOf(cityId, goodId).Out;

    /// <summary>
    /// A city's live vital, or null if this run has no value for it. Null is a real
    /// answer rather than a failure: a save written before a vital existed simply has
    /// not heard of it, and the caller falls back to the founding value.
    /// </summary>
    public double? VitalOf(string cityId, string vitalId)
        => CityVitals.TryGetValue(cityId, out var vitals) && vitals.TryGetValue(vitalId, out var value)
            ? value
            : null;

    public void SetVital(string cityId, string vitalId, double value)
    {
        if (!CityVitals.TryGetValue(cityId, out var vitals))
            CityVitals[cityId] = vitals = new Dictionary<string, double>();
        vitals[vitalId] = value;
    }

    /// <summary>Total standing with a city across every segment, or zero if never courted.</summary>
    public double StandingOf(string cityId)
    {
        if (!CityStanding.TryGetValue(cityId, out var segments)) return 0.0;
        var total = 0.0;
        foreach (var value in segments.Values) total += value;
        return total;
    }

    /// <summary>One segment of standing with a city, or zero.</summary>
    public double StandingOf(string cityId, string segmentId)
        => CityStanding.TryGetValue(cityId, out var segments) && segments.TryGetValue(segmentId, out var value)
            ? value
            : 0.0;

    public void SetStanding(string cityId, string segmentId, double value)
    {
        if (!CityStanding.TryGetValue(cityId, out var segments))
            CityStanding[cityId] = segments = new Dictionary<string, double>();
        segments[segmentId] = value;
    }

    /// <summary>The convoy's truck with this id, or null.</summary>
    public TruckState? Truck(string id)
    {
        foreach (var truck in Caravan.Trucks)
        {
            if (truck.Id == id) return truck;
        }
        return null;
    }

    public ContractState? Contract(string id)
    {
        foreach (var contract in Contracts)
        {
            if (contract.Id == id) return contract;
        }
        return null;
    }

    public bool HasPermit(string cityId, string permitId)
        => CityPermits.TryGetValue(cityId, out var held) && held.Contains(permitId);

    public void GrantPermit(string cityId, string permitId)
    {
        if (!CityPermits.TryGetValue(cityId, out var held))
            CityPermits[cityId] = held = new HashSet<string>();
        held.Add(permitId);
    }

    public void SetStock(string cityId, string goodId, CityStock value)
    {
        if (!Stock.TryGetValue(cityId, out var market))
            Stock[cityId] = market = new Dictionary<string, CityStock>();
        market[goodId] = value;
    }
}

/// <summary>
/// One live world event. The template is content; this is the instance: where, when,
/// and until when. Effects are not stored here — they are read off the template for
/// as long as the instance is on the list.
/// </summary>
public sealed class ActiveEvent
{
    public string DefId { get; set; } = "";

    /// <summary>Empty when the template is global.</summary>
    public string CityId { get; set; } = "";

    public int StartDay { get; set; }
    public int EndDay { get; set; }
}

/// <summary>
/// One contract the player holds. The offer it came from is re-derived from the id
/// (city, round, index) whenever the terms are needed, so the terms cannot drift.
/// </summary>
public sealed class ContractState
{
    public string Id { get; set; } = "";
    public string CityId { get; set; } = "";
    public int AcceptedDay { get; set; }
    public int Deadline { get; set; }
}

/// <summary>One buyer's visit to the stall: what they looked at and what they did.</summary>
public sealed class ExpoVisit
{
    public int Sequence { get; set; }
    public string Buyer { get; set; } = "";
    public string GoodId { get; set; } = "";

    /// <summary>browse, tooDear, close, bought or noStall.</summary>
    public string Outcome { get; set; } = "";

    public int Units { get; set; }
    public long Price { get; set; }
    public string Remark { get; set; } = "";
}

/// <summary>The stall's day: where, when, and every visit in order.</summary>
public sealed class ExpoDayState
{
    public int Day { get; set; }
    public string CityId { get; set; } = "";
    public long Revenue { get; set; }
    public int UnitsSold { get; set; }
    public List<ExpoVisit> Visits { get; set; } = new();
}

/// <summary>
/// One ore claim on the map. Generated per run; remaining reserve is the live figure.
/// </summary>
public sealed class MiningSite
{
    public string Id { get; set; } = "";
    public int Col { get; set; }
    public int Row { get; set; }
    public string GoodId { get; set; } = "";
    public double Remaining { get; set; }
}

/// <summary>
/// A rented storeroom in one city. Stock and the two auto prices are state; capacity
/// and rent are content. Auto prices of zero mean the order is off.
/// </summary>
public sealed class WarehouseState
{
    public string CityId { get; set; } = "";
    public Dictionary<string, CargoLot> Stock { get; set; } = new();

    /// <summary>goodId to the ask. Zero means do not auto-sell this good.</summary>
    public Dictionary<string, long> AutoSellPrice { get; set; } = new();

    /// <summary>goodId to the bid. Zero means do not auto-procure this good.</summary>
    public Dictionary<string, long> AutoProcurePrice { get; set; } = new();

    public int Held(string goodId)
        => Stock.TryGetValue(goodId, out var lot) ? lot.Units : 0;
}
