using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>
/// How the player relates to a city, as opposed to what the city is.
///
/// Relationship is a handful of segments per city (the office, the streets, the houses,
/// one held back), each stored as live state starting at zero. The total, the rank, the
/// reserved-shelf share, which permits are due and which product grades are open are
/// all derived from those numbers on demand, the same way a supply figure is derived
/// from the market: storing any of them would give the game two answers to the same
/// question. Permits, once granted, are the exception — they stick even if standing
/// later fell, so they are stored as ids.
///
/// Pure over (state, world) throughout, except <see cref="Grant"/>, which is the one
/// write and is only called from the command processor and the day tick.
/// </summary>
public static class Standing
{
    /// <summary>Total standing with a city: every segment summed.</summary>
    public static double Of(GameState state, string cityId)
        => state.StandingOf(cityId);

    public static double Segment(GameState state, string cityId, string segmentId)
        => state.StandingOf(cityId, segmentId);

    public static StatBandDef? Rank(StandingConfig config, double standing)
        => CityStats.Band(config.Ranks, standing);

    /// <summary>
    /// Fraction of the shelf held for the player. Other caravans only see what is left.
    /// The player can still buy the whole shelf — that is the point of the privilege.
    /// </summary>
    public static double ReservedRatio(StandingConfig config, double standing)
        => Math.Clamp(standing * config.ReservePerPoint, 0.0, config.ReserveMax);

    public static int ReservedUnits(int shelfUnits, double reservedRatio)
        => (int)Math.Floor(Math.Max(0, shelfUnits) * Math.Clamp(reservedRatio, 0.0, 1.0));

    public static int PublicUnits(int shelfUnits, double reservedRatio)
        => Math.Max(0, shelfUnits - ReservedUnits(shelfUnits, reservedRatio));

    /// <summary>Permits whose standing threshold this value has crossed.</summary>
    public static IEnumerable<PermitDef> Due(StandingConfig config, double standing)
        => config.Permits.Where(p => standing + 1e-9 >= p.Standing);

    /// <summary>True when the city will sell this grade to somebody with this total standing.</summary>
    public static bool TierOpen(TierDef tier, double standing)
        => standing + 1e-9 >= tier.MinStanding;

    /// <summary>How much of a grant a segment can still take before its ceiling.</summary>
    public static double Room(GameState state, StandingConfig config, string cityId, string segmentId)
        => Math.Max(0.0, config.SegmentMax - state.StandingOf(cityId, segmentId));

    /// <summary>
    /// The one write. Adds to a segment, clamped to the segment ceiling (and never below
    /// zero on a penalty). Returns what actually landed.
    /// </summary>
    public static double Grant(GameState state, StandingConfig config, string cityId, string segmentId, double amount)
    {
        var current = state.StandingOf(cityId, segmentId);
        var next = Math.Clamp(current + amount, 0.0, config.SegmentMax);
        state.SetStanding(cityId, segmentId, next);
        return next - current;
    }
}
