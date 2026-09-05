using MechaTrader.Core.Model;

namespace MechaTrader.Core.World;

public static partial class WorldLoader
{
    private static City BuildCity(
        CityDto dto,
        IReadOnlyList<GoodDef> goods,
        IReadOnlyDictionary<int, TierDef> tiersById,
        IReadOnlyDictionary<string, IndustryDef> industriesById,
        IReadOnlyDictionary<string, double> baseConsumption,
        EconomyConfig eco,
        CityStatsConfig stats,
        CrewConfig crew)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
            throw new WorldLoadException("A city is missing its id.");

        var vitals = FoundingVitals(dto, stats);
        var population = vitals[stats.PopulationVitalId];

        if (population <= 0)
            throw new WorldLoadException($"City '{dto.Id}' must have a positive population.");

        var market = new Dictionary<string, CityGoodProfile>(goods.Count);

        foreach (var good in goods)
        {
            double production = 0;
            double consumption = 0;

            foreach (var industryId in dto.Industries)
            {
                if (!industriesById.TryGetValue(industryId, out var industry))
                    throw new WorldLoadException($"City '{dto.Id}' references unknown industry '{industryId}'.");

                if (industry.Production.TryGetValue(good.Id, out var p)) production += p * population;
                if (industry.Consumption.TryGetValue(good.Id, out var c)) consumption += c * population;
            }

            if (baseConsumption.TryGetValue(good.Id, out var basePer))
                consumption += basePer * population;

            // Rare grades do not rest in piles of 150: the tier scales the floor.
            var scale = tiersById.TryGetValue(good.Tier, out var tier) ? Math.Max(0.0, tier.EquilibriumScale) : 1.0;
            var equilibrium = Math.Max(eco.MinEquilibrium * scale, eco.EquilibriumDays * (production + consumption));

            market[good.Id] = new CityGoodProfile
            {
                GoodId = good.Id,
                Production = production,
                Consumption = consumption,
                Equilibrium = equilibrium
            };
        }

        return new City
        {
            Id = dto.Id,
            Name = string.IsNullOrWhiteSpace(dto.Name) ? dto.Id : dto.Name,
            Region = dto.Region,
            Lon = dto.Lon,
            Lat = dto.Lat,
            X = MapProjection.X(dto.Lon),
            Y = MapProjection.Y(dto.Lat),
            Vitals = vitals,
            Population = population,
            Industries = dto.Industries,
            Market = market,
            GovernorName = ResolveGovernorName(dto, crew),
            GovernorTitle = string.IsNullOrWhiteSpace(dto.GovernorTitle) ? "Governor" : dto.GovernorTitle.Trim()
        };
    }

    /// <summary>
    /// An authored name wins; otherwise a stable pick from the crew name pools so a city
    /// that has not been given a governor yet still has a person behind the desk.
    /// </summary>
    private static string ResolveGovernorName(CityDto dto, CrewConfig crew)
    {
        if (!string.IsNullOrWhiteSpace(dto.Governor)) return dto.Governor.Trim();
        if (crew.FirstNames.Count == 0 || crew.Surnames.Count == 0) return "the Governor";

        var hash = StableHash(dto.Id);
        var first = crew.FirstNames[hash % crew.FirstNames.Count];
        var last = crew.Surnames[(hash / 7) % crew.Surnames.Count];
        return $"{first} {last}";
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)(hash & 0x7fffffff);
        }
    }

    /// <summary>
    /// Reads one city's founding stats against the catalogue. Every declared vital gets a
    /// value - a city that authors nothing still has a full stat block, because a stat
    /// that is present only sometimes is one every reader has to null-check forever.
    /// </summary>
    private static Dictionary<string, double> FoundingVitals(CityDto dto, CityStatsConfig stats)
    {
        var vitals = new Dictionary<string, double>(stats.Vitals.Count);

        foreach (var vital in stats.Vitals)
        {
            var value = dto.Stats.TryGetValue(vital.Id, out var authored) ? authored : vital.Default;

            if (value < vital.Min || value > vital.Max)
            {
                throw new WorldLoadException(
                    $"City '{dto.Id}' sets '{vital.Id}' to {value}, outside its declared " +
                    $"range {vital.Min} to {vital.Max}.");
            }

            vitals[vital.Id] = value;
        }

        foreach (var id in dto.Stats.Keys)
        {
            if (!vitals.ContainsKey(id))
                throw new WorldLoadException($"City '{dto.Id}' sets unknown stat '{id}'.");
        }

        return vitals;
    }
}
