using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>
/// A rented storeroom: volume, auto-sell, auto-procure. Pure over state plus content
/// except <see cref="Tick"/>, which is the day-tick write and is only called from
/// <see cref="DayTick"/>.
/// </summary>
public static class WarehouseMath
{
    public static double UsedVolume(WarehouseState warehouse, WorldData world)
    {
        double total = 0;
        foreach (var (goodId, lot) in warehouse.Stock)
        {
            if (lot.Units <= 0) continue;
            if (!world.GoodsById.TryGetValue(goodId, out var good)) continue;
            total += lot.Units * good.UnitVolume;
        }
        return total;
    }

    public static double FreeVolume(WarehouseState warehouse, WorldData world)
        => Math.Max(0, world.Config.Warehouse.Capacity - UsedVolume(warehouse, world));

    public static long DailyRent(GameState state, WorldData world)
        => world.Config.Warehouse.DailyRent * state.Warehouses.Count;

    /// <summary>
    /// Unattended buy and sell against the local market, at market terms — nobody is
    /// standing at the counter, so crew knowledge does not cherry-pick and does not
    /// bargain. Iterates cities and goods in content order so the sequence is stable.
    /// </summary>
    public static void Tick(GameState state, WorldData world, List<GameEvent> events)
    {
        var eco = world.Config.Economy;
        var quality = world.Quality;

        foreach (var city in world.Cities)
        {
            if (!state.Warehouses.TryGetValue(city.Id, out var warehouse)) continue;

            foreach (var good in world.Goods)
            {
                AutoSell(state, world, city, warehouse, good, eco, quality, events);
                AutoProcure(state, world, city, warehouse, good, eco, quality, events);
            }
        }
    }

    private static void AutoSell(
        GameState state, WorldData world, City city, WarehouseState warehouse, GoodDef good,
        EconomyConfig eco, QualityConfig quality, List<GameEvent> events)
    {
        if (!warehouse.AutoSellPrice.TryGetValue(good.Id, out var ask) || ask <= 0) return;
        if (!warehouse.Stock.TryGetValue(good.Id, out var lot) || lot.Units <= 0) return;

        var profile = city.Market[good.Id];
        var eventMult = WorldEvents.PriceMultiplier(state, world, city.Id, good.Id);
        var stock = state.StockOf(city.Id, good.Id);
        var terms = TradeTerms.Market;

        // One price for the day: either the whole lot clears or none of it does.
        var price = Economy.SellUnitPrice(good, profile, stock, eco, terms, eventMult)
                    * QualityMath.SellMultiplier(lot.Quality, quality);
        if (price + 1e-9 < ask) return;
        var units = lot.Units;

        var quote = Economy.QuoteSell(good, profile, stock, units, eco, terms, eventMult);
        var total = (long)Math.Round(quote.Total * QualityMath.SellMultiplier(lot.Quality, quality));
        var costBasis = (long)Math.Round(lot.AverageCost * units);

        state.Cash += total;
        state.SetStock(city.Id, good.Id, quote.ResultingStock);

        lot.Units -= units;
        lot.TotalCost = Math.Max(0, lot.TotalCost - costBasis);
        if (lot.Units == 0) warehouse.Stock.Remove(good.Id);

        events.Add(new GameEvent(state.Day, GameEventKind.Trade,
            $"Storeroom in {city.Name} auto-sold {units:N0} {good.Name} for {total:N0} cr."));
    }

    private static void AutoProcure(
        GameState state, WorldData world, City city, WarehouseState warehouse, GoodDef good,
        EconomyConfig eco, QualityConfig quality, List<GameEvent> events)
    {
        if (!warehouse.AutoProcurePrice.TryGetValue(good.Id, out var bid) || bid <= 0) return;

        var profile = city.Market[good.Id];
        var eventMult = WorldEvents.PriceMultiplier(state, world, city.Id, good.Id);
        var stock = state.StockOf(city.Id, good.Id);
        var terms = TradeTerms.Market;
        var saleable = Economy.UnitsOnTheShelf(stock, eco);
        if (saleable <= 0) return;

        var free = FreeVolume(warehouse, world);
        var volumeCap = good.UnitVolume > 0 ? (int)Math.Floor(free / good.UnitVolume) : 0;
        var cap = Math.Min(saleable, volumeCap);
        if (cap <= 0 || state.Cash <= 0) return;

        var gradeMult = QualityMath.SellMultiplier(stock.OutQuality, quality);
        var price = Economy.BuyUnitPrice(good, profile, stock, eco, terms, eventMult) * gradeMult;
        if (price - 1e-9 > bid || price <= 0) return;
        var units = Math.Min(cap, (int)Math.Floor(state.Cash / price));
        if (units <= 0) return;

        var quote = Economy.QuoteBuy(good, profile, stock, units, eco, terms, eventMult);
        // Unattended: buy the slice as-is, knowledge 0, so selected grade is the average.
        var (selected, resulting) = QualityMath.Take(stock, saleable, units, 0.0, quality, quote.ResultingStock);
        var total = (long)Math.Round(quote.Total * QualityMath.SellMultiplier(selected, quality));

        state.Cash -= total;
        state.SetStock(city.Id, good.Id, resulting);

        if (!warehouse.Stock.TryGetValue(good.Id, out var lot))
            warehouse.Stock[good.Id] = lot = new CargoLot();
        lot.Add(units, total, selected);

        events.Add(new GameEvent(state.Day, GameEventKind.Trade,
            $"Storeroom in {city.Name} auto-bought {units:N0} {good.Name} for {total:N0} cr."));
    }
}
