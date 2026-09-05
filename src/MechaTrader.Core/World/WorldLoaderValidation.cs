using System.Text.Json;
using MechaTrader.Core.Model;

namespace MechaTrader.Core.World;

public static partial class WorldLoader
{
    private static List<CategoryDef> ResolveCategories(
        List<CategoryDef> authored, IReadOnlyList<GoodDef> goods)
    {
        if (authored.Count > 0) return authored;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var derived = new List<CategoryDef>();
        foreach (var good in goods)
        {
            if (string.IsNullOrWhiteSpace(good.Category)) continue;
            if (!seen.Add(good.Category)) continue;
            derived.Add(new CategoryDef { Id = good.Category, Name = good.Category });
        }
        return derived;
    }

    private static void ValidateGoods(
        IReadOnlyList<GoodDef> goods, IReadOnlyDictionary<string, CategoryDef> categoriesById)
    {
        if (categoriesById.Count == 0) return;

        foreach (var good in goods)
        {
            if (string.IsNullOrWhiteSpace(good.Category))
                throw new WorldLoadException($"Good '{good.Id}' has no category.");
            if (!categoriesById.ContainsKey(good.Category))
                throw new WorldLoadException($"Good '{good.Id}' names unknown category '{good.Category}'.");
        }
    }

    /// <summary>Authored tiers win; otherwise one plain tier per distinct number the goods use.</summary>
    private static List<TierDef> ResolveTiers(List<TierDef> authored, IReadOnlyList<GoodDef> goods)
    {
        if (authored.Count > 0) return authored.OrderBy(t => t.Tier).ToList();

        var derived = new List<TierDef>();
        foreach (var number in goods.Select(g => g.Tier).Distinct().OrderBy(t => t))
            derived.Add(new TierDef { Tier = number, Name = $"Tier {number}" });
        return derived;
    }

    /// <summary>
    /// Tiers must ascend with no duplicate, their price-per-volume floors must rise with
    /// them, and every good must sit inside its own tier's band. That last rule is the
    /// promise "a higher tier is denser value", enforced where content is read rather
    /// than trusted.
    /// </summary>
    private static Dictionary<int, TierDef> ValidateTiers(IReadOnlyList<TierDef> tiers, IReadOnlyList<GoodDef> goods)
    {
        var byId = new Dictionary<int, TierDef>();
        var previousFloor = double.NegativeInfinity;

        foreach (var tier in tiers)
        {
            if (tier.Tier < 1)
                throw new WorldLoadException($"Tier '{tier.Name}' has number {tier.Tier}; tiers start at 1.");
            if (!byId.TryAdd(tier.Tier, tier))
                throw new WorldLoadException($"Duplicate tier {tier.Tier}.");
            if (tier.MinPricePerVolume < previousFloor)
                throw new WorldLoadException($"Tier {tier.Tier} lowers the price-per-volume floor; tiers must rise.");
            if (tier.MinStanding < 0)
                throw new WorldLoadException($"Tier {tier.Tier} has a negative minStanding.");
            if (tier.EquilibriumScale <= 0)
                throw new WorldLoadException($"Tier {tier.Tier} equilibriumScale must be positive.");
            previousFloor = tier.MinPricePerVolume;
        }

        foreach (var good in goods)
        {
            if (good.BasePrice <= 0)
                throw new WorldLoadException($"Good '{good.Id}' must have a positive basePrice.");
            if (good.UnitVolume <= 0)
                throw new WorldLoadException($"Good '{good.Id}' must have a positive unitVolume.");
            if (!byId.TryGetValue(good.Tier, out var tier))
                throw new WorldLoadException($"Good '{good.Id}' names tier {good.Tier}, which goods.json does not declare.");

            var ratio = good.PricePerVolume;
            if (ratio + 1e-9 < tier.MinPricePerVolume)
            {
                throw new WorldLoadException(
                    $"Good '{good.Id}' is worth {ratio:0.#} per volume, below tier {tier.Tier}'s floor of {tier.MinPricePerVolume:0.#}.");
            }

            var next = tiers.FirstOrDefault(t => t.Tier > tier.Tier);
            if (next is not null && ratio >= next.MinPricePerVolume)
            {
                throw new WorldLoadException(
                    $"Good '{good.Id}' is worth {ratio:0.#} per volume, which is tier {next.Tier} territory (floor {next.MinPricePerVolume:0.#}).");
            }
        }

        return byId;
    }

    private static void ValidateQualityVital(QualityConfig quality, CityStatsConfig stats)
    {
        if (string.IsNullOrWhiteSpace(quality.CityVitalId)) return;
        if (stats.Vital(quality.CityVitalId) is null)
            throw new WorldLoadException($"goods.quality.cityVitalId '{quality.CityVitalId}' is not a declared vital.");
    }

    private static void ValidateQuality(QualityConfig quality)
    {
        if (quality.Base < 0 || quality.Base > 100)
            throw new WorldLoadException("goods.quality.base must be between 0 and 100.");
        if (quality.Random < 0)
            throw new WorldLoadException("goods.quality.random cannot be negative.");
        if (quality.CityVitalWeight < 0)
            throw new WorldLoadException("goods.quality.cityVitalWeight cannot be negative.");
        if (quality.Nominal < 0 || quality.Nominal > 100)
            throw new WorldLoadException("goods.quality.nominal must be between 0 and 100.");
        if (quality.Spread < 0)
            throw new WorldLoadException("goods.quality.spread cannot be negative.");
        if (quality.STierAt < 0 || quality.STierAt > 100)
            throw new WorldLoadException("goods.quality.sTierAt must be between 0 and 100.");
        if (quality.STierSellBonus < 0)
            throw new WorldLoadException("goods.quality.sTierSellBonus cannot be negative.");
    }

    private static void ValidateIndustryGoods(IndustriesFile file, IReadOnlyDictionary<string, GoodDef> goodsById)
    {
        foreach (var industry in file.Industries)
        {
            foreach (var id in industry.Production.Keys)
            {
                if (!goodsById.ContainsKey(id))
                    throw new WorldLoadException($"Industry '{industry.Id}' produces unknown good '{id}'.");
            }

            foreach (var id in industry.Consumption.Keys)
            {
                if (!goodsById.ContainsKey(id))
                    throw new WorldLoadException($"Industry '{industry.Id}' consumes unknown good '{id}'.");
            }
        }

        foreach (var id in file.BaseConsumptionPerPop.Keys)
        {
            if (!goodsById.ContainsKey(id))
                throw new WorldLoadException($"baseConsumptionPerPop references unknown good '{id}'.");
        }
    }

    /// <summary>
    /// Crew content is wired to the simulation by lever, not by skill id, so the ids
    /// themselves are free to change. What must hold is that every lever is claimed at
    /// most once and that nothing references a skill or industry that does not exist -
    /// a typo there would otherwise show up as a stat that silently does nothing.
    /// </summary>
    private static void ValidateCrew(
        CrewConfig crew,
        IReadOnlyDictionary<string, IndustryDef> industriesById,
        IReadOnlyDictionary<string, CategoryDef> categoriesById)
    {
        if (crew.MaxSkill <= 0)
            throw new WorldLoadException("crew.maxSkill must be positive.");
        if (crew.CrewCapacity < 0)
            throw new WorldLoadException("crew.crewCapacity cannot be negative.");
        if (crew.RefreshDays < 1)
            throw new WorldLoadException("crew.refreshDays must be at least 1.");
        if (crew.Skills.Count == 0)
            throw new WorldLoadException("crew.json defines no skills.");
        if (crew.Roles.Count == 0)
            throw new WorldLoadException("crew.json defines no roles.");

        var skillsById = ToLookup(crew.Skills, s => s.Id, "crew skill");
        var claimed = new Dictionary<string, string>();

        foreach (var skill in crew.Skills)
        {
            if (!CrewLever.All.Contains(skill.Lever))
            {
                throw new WorldLoadException(
                    $"Crew skill '{skill.Id}' declares unknown lever '{skill.Lever}'; " +
                    $"expected one of {string.Join(", ", CrewLever.All)}.");
            }

            if (skill.Lever == CrewLever.None) continue;

            if (claimed.TryGetValue(skill.Lever, out var owner))
            {
                throw new WorldLoadException(
                    $"Crew skills '{owner}' and '{skill.Id}' both claim the '{skill.Lever}' lever.");
            }

            claimed[skill.Lever] = skill.Id;
        }

        // Posts claim levers. A lever claimed twice would make "who pulls it" ambiguous,
        // and a post with no levers is a job title that does nothing.
        if (crew.Posts.Count > 0)
            ToLookup(crew.Posts, p => p.Id, "crew post");

        var postedLevers = new Dictionary<string, string>();
        foreach (var post in crew.Posts)
        {
            if (post.Levers.Count == 0)
                throw new WorldLoadException($"Crew post '{post.Id}' claims no lever.");

            foreach (var lever in post.Levers)
            {
                if (!CrewLever.All.Contains(lever) || lever == CrewLever.None)
                    throw new WorldLoadException($"Crew post '{post.Id}' claims unknown lever '{lever}'.");
                if (postedLevers.TryGetValue(lever, out var other))
                    throw new WorldLoadException($"Crew posts '{other}' and '{post.Id}' both claim the '{lever}' lever.");
                postedLevers[lever] = post.Id;
            }
        }

        if (crew.Intel.MinCities < 0 || crew.Intel.MaxCities < crew.Intel.MinCities)
            throw new WorldLoadException("crew.intel needs 0 <= minCities <= maxCities.");
        if (crew.Intel.MaxError < 0 || crew.Intel.MaxError > 1)
            throw new WorldLoadException("crew.intel.maxError must lie in [0, 1].");

        ToLookup(crew.Roles, r => r.Id, "crew role");

        foreach (var role in crew.Roles)
        {
            if (!string.IsNullOrWhiteSpace(role.Primary) && !skillsById.ContainsKey(role.Primary))
                throw new WorldLoadException($"Crew role '{role.Id}' specialises in unknown skill '{role.Primary}'.");
            if (!string.IsNullOrWhiteSpace(role.CategoryId) && !categoriesById.ContainsKey(role.CategoryId))
                throw new WorldLoadException($"Crew role '{role.Id}' specialises in unknown category '{role.CategoryId}'.");
            if (!string.IsNullOrWhiteSpace(role.Post) && crew.Post(role.Post) is null)
                throw new WorldLoadException($"Crew role '{role.Id}' signs on to unknown post '{role.Post}'.");
        }

        if (crew.Traits.Count > 0)
            ToLookup(crew.Traits, t => t.Id, "crew trait");

        foreach (var trait in crew.Traits)
        {
            if (!string.IsNullOrWhiteSpace(trait.Kind) && !TraitKind.All.Contains(trait.Kind))
            {
                throw new WorldLoadException(
                    $"Crew trait '{trait.Id}' declares unknown kind '{trait.Kind}'; " +
                    $"expected one of {string.Join(", ", TraitKind.All)}.");
            }

            if (!string.IsNullOrWhiteSpace(trait.CategoryId) && !categoriesById.ContainsKey(trait.CategoryId))
                throw new WorldLoadException($"Crew trait '{trait.Id}' names unknown category '{trait.CategoryId}'.");
        }

        foreach (var (industryId, bonuses) in crew.IndustryAffinity)
        {
            if (!industriesById.ContainsKey(industryId))
                throw new WorldLoadException($"crew.industryAffinity references unknown industry '{industryId}'.");

            foreach (var skillId in bonuses.Keys)
            {
                if (!skillsById.ContainsKey(skillId))
                {
                    throw new WorldLoadException(
                        $"crew.industryAffinity['{industryId}'] references unknown skill '{skillId}'.");
                }
            }
        }
    }

    /// <summary>
    /// The catalogue is the only place a stat is declared, so a typo here would otherwise
    /// ship as a stat that renders blank or a supply figure quietly summing nothing.
    /// Bands are checked for ascending order because the reader takes the first one a
    /// value falls under.
    /// </summary>
    private static void ValidateCityStats(CityStatsConfig stats, IReadOnlyDictionary<string, GoodDef> goodsById)
    {
        if (stats.Vitals.Count == 0)
            throw new WorldLoadException("citystats.json declares no vitals.");

        var vitalsById = ToLookup(stats.Vitals, v => v.Id, "city vital");

        foreach (var vital in stats.Vitals)
        {
            if (vital.Min >= vital.Max)
                throw new WorldLoadException($"City vital '{vital.Id}' has an empty range.");
            if (vital.Default < vital.Min || vital.Default > vital.Max)
                throw new WorldLoadException($"City vital '{vital.Id}' defaults outside its own range.");

            ValidateBands(vital.Bands, $"city vital '{vital.Id}'");
        }

        if (!vitalsById.ContainsKey(stats.PopulationVitalId))
        {
            throw new WorldLoadException(
                $"citystats.populationVitalId '{stats.PopulationVitalId}' is not a declared vital.");
        }

        ToLookup(stats.Supplies, s => s.Id, "city supply");

        foreach (var supply in stats.Supplies)
        {
            if (supply.Goods.Count == 0)
                throw new WorldLoadException($"City supply '{supply.Id}' reads no goods.");

            foreach (var goodId in supply.Goods)
            {
                if (!goodsById.ContainsKey(goodId))
                    throw new WorldLoadException($"City supply '{supply.Id}' reads unknown good '{goodId}'.");
            }
        }

        ValidateBands(stats.SupplyBands, "citystats.supplyBands");
    }

    private static void ValidateStanding(StandingConfig standing, CityStatsConfig cityStats)
    {
        if (standing.Max <= 0)
            throw new WorldLoadException("standing.max must be positive.");
        if (standing.SegmentMax <= 0)
            throw new WorldLoadException("standing.segmentMax must be positive.");
        if (standing.Segments.Count == 0)
            throw new WorldLoadException("standing.json declares no segments.");
        ToLookup(standing.Segments, s => s.Id, "standing segment");
        if (standing.TradersPerThousandCr < 0)
            throw new WorldLoadException("standing.tradersPerThousandCr cannot be negative.");
        if (standing.ContractLapsePenalty < 0)
            throw new WorldLoadException("standing.contractLapsePenalty cannot be negative.");
        if (standing.ReservePerPoint < 0)
            throw new WorldLoadException("standing.reservePerPoint cannot be negative.");
        if (standing.ReserveMax < 0 || standing.ReserveMax > 1)
            throw new WorldLoadException("standing.reserveMax must be between 0 and 1.");

        ValidateBands(standing.Ranks, "standing.ranks");
        ToLookup(standing.Permits, p => p.Id, "permit");
        ToLookup(standing.Actions, a => a.Id, "favor action");

        foreach (var permit in standing.Permits)
        {
            if (permit.Standing < 0 || permit.Standing > standing.Max)
            {
                throw new WorldLoadException(
                    $"Permit '{permit.Id}' requires standing {permit.Standing}, outside 0 to {standing.Max}.");
            }
        }

        if (standing.Actions.Count == 0)
            throw new WorldLoadException("standing.json declares no favor actions.");

        foreach (var action in standing.Actions)
        {
            if (action.Cost < 0)
                throw new WorldLoadException($"Favor action '{action.Id}' has a negative cost.");
            if (action.Standing < 0)
                throw new WorldLoadException($"Favor action '{action.Id}' grants negative standing.");
            if (action.StockPerGood < 0)
                throw new WorldLoadException($"Favor action '{action.Id}' cannot ship a negative quantity.");
            if (!string.IsNullOrWhiteSpace(action.SegmentId) && !standing.HasSegment(action.SegmentId))
                throw new WorldLoadException($"Favor action '{action.Id}' lands in unknown segment '{action.SegmentId}'.");

            if (string.IsNullOrWhiteSpace(action.VitalId)) continue;

            if (cityStats.Vital(action.VitalId) is null)
            {
                throw new WorldLoadException(
                    $"Favor action '{action.Id}' moves unknown vital '{action.VitalId}'.");
            }
        }
    }

    private static void ValidateEvents(
        EventsConfig events,
        IReadOnlyDictionary<string, GoodDef> goodsById,
        IReadOnlyDictionary<string, CategoryDef> categoriesById,
        IReadOnlyDictionary<string, IndustryDef> industriesById,
        IReadOnlyDictionary<string, City> citiesById,
        CityStatsConfig cityStats)
    {
        if (events.MaxConcurrent < 0)
            throw new WorldLoadException("events.maxConcurrent cannot be negative.");
        if (events.DailyChance < 0 || events.DailyChance > 1)
            throw new WorldLoadException("events.dailyChance must be between 0 and 1.");

        ToLookup(events.Events, e => e.Id, "event");

        foreach (var def in events.Events)
        {
            if (def.DurationDays < 1)
                throw new WorldLoadException($"Event '{def.Id}' durationDays must be at least 1.");
            if (def.Weight <= 0)
                throw new WorldLoadException($"Event '{def.Id}' weight must be positive.");
            if (string.IsNullOrWhiteSpace(def.Headline))
                throw new WorldLoadException($"Event '{def.Id}' is missing a headline.");
            if (def.PriceMult <= 0)
                throw new WorldLoadException($"Event '{def.Id}' priceMult must be positive.");
            if (def.StockMult <= 0)
                throw new WorldLoadException($"Event '{def.Id}' stockMult must be positive.");

            if (!def.TouchesPrice && !def.TouchesStock && !def.TouchesVitals)
                throw new WorldLoadException($"Event '{def.Id}' has no effect.");

            foreach (var goodId in def.Goods)
            {
                if (!goodsById.ContainsKey(goodId))
                    throw new WorldLoadException($"Event '{def.Id}' names unknown good '{goodId}'.");
            }

            foreach (var categoryId in def.Categories)
            {
                if (!categoriesById.ContainsKey(categoryId))
                    throw new WorldLoadException($"Event '{def.Id}' names unknown category '{categoryId}'.");
            }

            if (def.ReliefStanding < 0)
                throw new WorldLoadException($"Event '{def.Id}' reliefStanding cannot be negative.");
            if (def.ReliefStanding > 0 && def.ReliefUnits <= 0)
                throw new WorldLoadException($"Event '{def.Id}' reliefUnits must be positive.");
            if (def.ReliefStanding > 0 && !def.NamesGoods)
                throw new WorldLoadException($"Event '{def.Id}' is a shortage but names no good or category.");

            foreach (var industryId in def.Industries)
            {
                if (!industriesById.ContainsKey(industryId))
                    throw new WorldLoadException($"Event '{def.Id}' names unknown industry '{industryId}'.");
            }

            foreach (var cityId in def.Cities)
            {
                if (!citiesById.ContainsKey(cityId))
                    throw new WorldLoadException($"Event '{def.Id}' names unknown city '{cityId}'.");
            }

            foreach (var vitalId in def.VitalDeltas.Keys)
            {
                if (cityStats.Vital(vitalId) is null)
                    throw new WorldLoadException($"Event '{def.Id}' moves unknown vital '{vitalId}'.");
            }
        }
    }

    private static void ValidateBands(IReadOnlyList<StatBandDef> bands, string label)
    {
        if (bands.Count == 0) return;

        var previous = double.NegativeInfinity;

        for (var i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            var last = i == bands.Count - 1;

            if (band.UpTo is null)
            {
                if (!last)
                    throw new WorldLoadException($"{label}: only the last band may be open-ended.");
                continue;
            }

            if (last)
                throw new WorldLoadException($"{label}: the last band must be open-ended.");
            if (band.UpTo <= previous)
                throw new WorldLoadException($"{label}: bands must be in ascending order.");

            previous = band.UpTo.Value;
        }
    }

    private static Dictionary<string, GearDef> ValidateGear(IReadOnlyList<GearDef> gear)
    {
        var byId = ToLookup(gear, g => g.Id, "gear");
        foreach (var item in gear)
        {
            if (item.Price < 0)
                throw new WorldLoadException($"Gear '{item.Id}' has a negative price.");
            if (item.Volume < 0)
                throw new WorldLoadException($"Gear '{item.Id}' has a negative volume.");
            if (item.MineYield < 0)
                throw new WorldLoadException($"Gear '{item.Id}' has a negative mineYield.");
        }
        return byId;
    }

    private static void ValidateTrucks(IReadOnlyList<TruckDef> trucks)
    {
        foreach (var truck in trucks)
        {
            var kind = truck.EffectiveKind;
            if (kind != VehicleKind.Truck && kind != VehicleKind.Machine)
            {
                throw new WorldLoadException(
                    $"Truck '{truck.Id}' has unknown kind '{truck.Kind}'; expected truck or machine.");
            }

            if (truck.MineYield < 0)
                throw new WorldLoadException($"Truck '{truck.Id}' has a negative mineYield.");
        }
    }

    private static Dictionary<string, TruckUpgradeDef> ValidateUpgrades(TrucksFile file)
    {
        if (file.ResaleFraction < 0 || file.ResaleFraction > 1)
            throw new WorldLoadException("trucks.resaleFraction must be between 0 and 1.");

        var byId = ToLookup(file.Upgrades, u => u.Id, "truck upgrade");
        foreach (var upgrade in file.Upgrades)
        {
            if (upgrade.Price < 0)
                throw new WorldLoadException($"Truck upgrade '{upgrade.Id}' has a negative price.");
            if (upgrade.SpeedMult <= 0 || upgrade.FuelMult <= 0)
                throw new WorldLoadException($"Truck upgrade '{upgrade.Id}' multipliers must be positive.");
            foreach (var kind in upgrade.Kinds)
            {
                if (kind != VehicleKind.Truck && kind != VehicleKind.Machine)
                    throw new WorldLoadException($"Truck upgrade '{upgrade.Id}' fits unknown kind '{kind}'.");
            }
        }
        return byId;
    }

    private static void ValidateContracts(ContractsConfig contracts, IReadOnlyDictionary<int, TierDef> tiersById)
    {
        if (contracts.RefreshDays < 1)
            throw new WorldLoadException("contracts.refreshDays must be at least 1.");
        if (contracts.OffersPerCity < 0)
            throw new WorldLoadException("contracts.offersPerCity cannot be negative.");
        if (contracts.DeadlineDaysMin < 1 || contracts.DeadlineDaysMax < contracts.DeadlineDaysMin)
            throw new WorldLoadException("contracts deadline range is empty.");
        if (contracts.Kinds.Count == 0)
            throw new WorldLoadException("contracts.json declares no kinds.");

        ToLookup(contracts.Kinds, k => k.Id, "contract kind");
        foreach (var kind in contracts.Kinds)
        {
            if (kind.Weight <= 0)
                throw new WorldLoadException($"Contract kind '{kind.Id}' weight must be positive.");
            if (kind.Goods < 1)
                throw new WorldLoadException($"Contract kind '{kind.Id}' must ask for at least one good.");
            if (kind.UnitsMin < 1 || kind.UnitsMax < kind.UnitsMin)
                throw new WorldLoadException($"Contract kind '{kind.Id}' has an empty units range.");
            if (kind.MinGrade < 0 || kind.MinGrade > 100)
                throw new WorldLoadException($"Contract kind '{kind.Id}' minGrade must be between 0 and 100.");
            if (kind.RewardMult <= 0 && kind.PriceMult <= 0)
                throw new WorldLoadException($"Contract kind '{kind.Id}' must set rewardMult or priceMult.");
            if (kind.Standing < 0)
                throw new WorldLoadException($"Contract kind '{kind.Id}' cannot pay negative standing.");
        }

        foreach (var (key, weight) in contracts.TierWeights)
        {
            if (!int.TryParse(key, out var tier) || !tiersById.ContainsKey(tier))
                throw new WorldLoadException($"contracts.tierWeights names unknown tier '{key}'.");
            if (weight < 0)
                throw new WorldLoadException($"contracts.tierWeights['{key}'] cannot be negative.");
        }
    }

    private static void ValidateExpos(ExposConfig expos, IReadOnlyDictionary<string, CategoryDef> categoriesById)
    {
        if (expos.CycleDays < 1)
            throw new WorldLoadException("expos.cycleDays must be at least 1.");
        if (expos.FeeBase < 0 || expos.FeePerPop < 0)
            throw new WorldLoadException("expos fees cannot be negative.");
        if (expos.BuyersBase < 0 || expos.BuyersPerPop < 0)
            throw new WorldLoadException("expos buyer counts cannot be negative.");
        if (expos.BuffMin < 0 || expos.BuffMax < expos.BuffMin)
            throw new WorldLoadException("expos buff range is empty.");
        if (expos.PremiumMult < 0 || expos.Noise < 0 || expos.CloseBand < 0)
            throw new WorldLoadException("expos premiumMult, noise and closeBand cannot be negative.");
        if (expos.LotMax < 1)
            throw new WorldLoadException("expos.lotMax must be at least 1.");
        if (expos.Themes.Count == 0)
            throw new WorldLoadException("expos.json declares no themes.");

        ToLookup(expos.Themes, t => t.Id, "expo theme");
        foreach (var theme in expos.Themes)
        {
            if (theme.Categories.Count == 0)
                throw new WorldLoadException($"Expo theme '{theme.Id}' names no category.");
            if (theme.DurationDays < 1 || theme.DurationDays > expos.CycleDays)
                throw new WorldLoadException($"Expo theme '{theme.Id}' must run between 1 day and the cycle length.");
            if (theme.Weight <= 0)
                throw new WorldLoadException($"Expo theme '{theme.Id}' weight must be positive.");
            foreach (var categoryId in theme.Categories)
            {
                if (categoriesById.Count > 0 && !categoriesById.ContainsKey(categoryId))
                    throw new WorldLoadException($"Expo theme '{theme.Id}' names unknown category '{categoryId}'.");
            }
        }
    }

    private static void ValidateMap(WorldMap map, IReadOnlyDictionary<string, GoodDef> goodsById)
    {
        if (map.Mining.SpotCount < 0)
            throw new WorldLoadException("map.mining.spotCount cannot be negative.");
        if (string.IsNullOrWhiteSpace(map.Mining.GoodId))
            throw new WorldLoadException("map.mining.goodId is missing.");
        if (!goodsById.ContainsKey(map.Mining.GoodId))
            throw new WorldLoadException($"map.mining.goodId '{map.Mining.GoodId}' is not a known good.");
        if (map.Mining.ReserveMin <= 0 || map.Mining.ReserveMax < map.Mining.ReserveMin)
            throw new WorldLoadException("map.mining reserve range is empty.");
    }

    private static void ValidateWorld(
        GameConfig config,
        IReadOnlyList<City> cities,
        IReadOnlyDictionary<string, City> citiesById,
        IReadOnlyDictionary<string, TruckDef> trucksById,
        RouteGraph graph)
    {
        if (!citiesById.ContainsKey(config.StartCityId))
            throw new WorldLoadException($"config.startCityId '{config.StartCityId}' is not a known city.");

        if (config.StartTruckIds.Count == 0)
            throw new WorldLoadException("config.startTruckIds must list at least one truck.");

        if (config.Warehouse.RentCost < 0)
            throw new WorldLoadException("config.warehouse.rentCost cannot be negative.");
        if (config.Warehouse.DailyRent < 0)
            throw new WorldLoadException("config.warehouse.dailyRent cannot be negative.");
        if (config.Warehouse.Capacity <= 0)
            throw new WorldLoadException("config.warehouse.capacity must be positive.");

        foreach (var id in config.StartTruckIds)
        {
            if (!trucksById.ContainsKey(id))
                throw new WorldLoadException($"config.startTruckIds references unknown truck '{id}'.");
        }

        // Every city must be reachable, or the player can be stranded or locked out of content.
        var reachable = graph.Reachable(config.StartCityId);
        var orphans = cities.Where(c => !reachable.Contains(c.Id)).Select(c => c.Id).ToList();
        if (orphans.Count > 0)
        {
            throw new WorldLoadException(
                $"Cities unreachable from '{config.StartCityId}': {string.Join(", ", orphans)}.");
        }
    }

    private static Dictionary<string, T> ToLookup<T>(IEnumerable<T> items, Func<T, string> keySelector, string label)
    {
        var map = new Dictionary<string, T>();
        foreach (var item in items)
        {
            var key = keySelector(item);
            if (string.IsNullOrWhiteSpace(key))
                throw new WorldLoadException($"A {label} entry is missing its id.");
            if (!map.TryAdd(key, item))
                throw new WorldLoadException($"Duplicate {label} id '{key}'.");
        }
        return map;
    }

    private static T Parse<T>(string json, string label)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                   ?? throw new WorldLoadException($"Content file '{label}' parsed to null.");
        }
        catch (JsonException ex)
        {
            throw new WorldLoadException($"Content file '{label}' is not valid JSON: {ex.Message}");
        }
    }
}
