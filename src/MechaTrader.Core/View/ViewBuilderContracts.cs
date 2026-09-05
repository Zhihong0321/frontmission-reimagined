using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.View;

public static partial class ViewBuilder
{
    /// <summary>
    /// The board here and every contract held. Offers are derived from the seed, so
    /// this is a pure read; the hold is checked against each line so the page can say
    /// "deliverable" without knowing what a lot is.
    /// </summary>
    private static ContractsView BuildContracts(GameState state, WorldData world, City? location)
    {
        var cfg = world.Contracts;
        var board = new List<ContractOfferView>();

        if (location is not null)
        {
            foreach (var offer in Contracts.BoardFor(world, location, state.Seed, state.Day))
            {
                board.Add(new ContractOfferView(
                    Id: offer.Id,
                    CityId: offer.CityId,
                    CityName: location.Name,
                    KindId: offer.KindId,
                    KindName: offer.KindName,
                    Blurb: offer.Blurb,
                    Lines: ContractLines(state, world, offer),
                    MinGrade: offer.MinGrade,
                    Reward: offer.Reward,
                    Standing: offer.Standing,
                    DeadlineDays: offer.DeadlineDays,
                    Held: state.Contract(offer.Id) is not null,
                    Closed: state.ContractsClosed.Contains(offer.Id)));
            }
        }

        var held = new List<HeldContractView>();
        foreach (var contract in state.Contracts)
        {
            var offer = Contracts.Resolve(world, state.Seed, contract.Id);
            if (offer is null) continue;
            var city = world.CitiesById.TryGetValue(contract.CityId, out var c) ? c : null;
            var here = location is not null && location.Id == contract.CityId;
            var blocker = Contracts.DeliveryBlocker(state, world, offer);
            var reason = !here ? $"Settled in {city?.Name ?? contract.CityId}." : blocker ?? "";

            held.Add(new HeldContractView(
                Id: contract.Id,
                CityId: contract.CityId,
                CityName: city?.Name ?? contract.CityId,
                KindName: offer.KindName,
                Blurb: offer.Blurb,
                Lines: ContractLines(state, world, offer),
                MinGrade: offer.MinGrade,
                Reward: offer.Reward,
                Standing: offer.Standing,
                Deadline: contract.Deadline,
                DaysLeft: Math.Max(0, contract.Deadline - state.Day),
                Here: here,
                Deliverable: here && blocker is null,
                Blocker: reason));
        }

        return new ContractsView(
            BoardCity: location?.Name ?? "",
            RefreshInDays: Contracts.DaysUntilRefresh(state.Day, cfg),
            Board: board,
            Held: held);
    }

    private static List<ContractLineView> ContractLines(GameState state, WorldData world, ContractOffer offer)
    {
        var lines = new List<ContractLineView>(offer.Lines.Count);
        foreach (var line in offer.Lines)
        {
            var good = world.Good(line.GoodId);
            state.Caravan.Cargo.TryGetValue(line.GoodId, out var lot);
            var heldUnits = lot?.Units ?? 0;
            var q = lot?.Quality ?? 0;
            lines.Add(new ContractLineView(
                GoodId: line.GoodId,
                Name: good.Name,
                TierColor: world.TierOf(good).Color,
                Units: line.Units,
                Held: heldUnits,
                HeldQuality: Math.Round(q, 1),
                Satisfied: heldUnits >= line.Units && (offer.MinGrade <= 0 || q + 1e-9 >= offer.MinGrade)));
        }
        return lines;
    }

    /// <summary>
    /// The expo here. Schedule and theme are derived from the seed; the stall and the
    /// report are state. Suggested asks are what a typical buyer would just pay, so the
    /// player has a number to argue up from.
    /// </summary>
    private static ExpoView BuildExpo(GameState state, WorldData world, City city)
    {
        var cfg = world.Expos;
        var running = Expos.Running(world, city, state.Seed, state.Day);
        var next = running ?? Expos.Next(world, city, state.Seed, state.Day);
        var theme = next?.Theme;
        var buff = theme is null ? 0.0 : Expos.Buff(cfg, theme);
        var passHeld = next is not null && state.ExpoPasses.Contains(next.PassId);
        var eco = world.Config.Economy;

        var listings = new List<ExpoListingView>();
        foreach (var good in world.Goods)
        {
            if (!state.Caravan.Cargo.TryGetValue(good.Id, out var lot) || lot.Units <= 0) continue;
            var makes = Expos.CityMakes(city, good.Id);
            var covered = theme is not null && Expos.ThemeCovers(theme, good);
            var reason = makes ? $"{city.Name} makes this; not allowed on a stall here."
                : theme is null ? "No expo scheduled."
                : !covered ? $"Not in this expo's theme."
                : running is null ? "The expo has not opened yet."
                : !passHeld ? "Buy a pass to list it."
                : "";
            state.Caravan.ExpoAsks.TryGetValue(good.Id, out var ask);
            // A shade under the typical buyer, so "try N" clears most of the hall rather than half of it.
            var suggested = theme is null
                ? 0
                : (long)Math.Round(Expos.TypicalWillingness(cfg, good, buff, lot.Quality, world.Quality) * (1.0 - cfg.Noise * 0.5));
            var profile = city.Market[good.Id];
            var stock = state.StockOf(city.Id, good.Id);
            var terms = CrewMath.Terms(state.Caravan, world, good.Category);
            var mult = WorldEvents.PriceMultiplier(state, world, city.Id, good.Id);
            var localSell = Economy.SellUnitPrice(good, profile, stock, eco, terms, mult) * QualityMath.SellMultiplier(lot.Quality, world.Quality);

            listings.Add(new ExpoListingView(
                GoodId: good.Id,
                Name: good.Name,
                Category: world.CategoryName(good.Category),
                TierColor: world.TierOf(good).Color,
                Held: lot.Units,
                Quality: Math.Round(lot.Quality, 1),
                Ask: ask,
                Suggested: suggested,
                LocalSell: Math.Round(localSell, 1),
                CityMakes: makes,
                Covered: covered,
                Eligible: reason.Length == 0,
                Reason: reason));
        }

        ExpoReportView? report = null;
        if (state.LastExpoDay is { } day && day.CityId == city.Id)
        {
            report = new ExpoReportView(
                Day: day.Day,
                Revenue: day.Revenue,
                UnitsSold: day.UnitsSold,
                Buyers: day.Visits.Count,
                Visits: day.Visits.Select(v => new ExpoVisitView(
                    v.Sequence,
                    v.Buyer,
                    v.GoodId,
                    world.GoodsById.TryGetValue(v.GoodId, out var g) ? g.Name : "",
                    v.Outcome,
                    v.Units,
                    v.Price,
                    v.Remark)).ToList());
        }

        return new ExpoView(
            CityName: city.Name,
            Running: running is not null,
            ThemeId: theme?.Id ?? "",
            Title: theme?.Title ?? "",
            Categories: theme is null ? Array.Empty<string>() : theme.Categories.Select(world.CategoryName).ToList(),
            StartsIn: next is null ? 0 : Math.Max(0, next.StartDay - state.Day),
            DaysLeft: running is null ? 0 : Math.Max(0, running.EndDay - state.Day),
            DurationDays: theme?.DurationDays ?? 0,
            Fee: Expos.Fee(cfg, city),
            PassHeld: passHeld,
            Buff: Math.Round(buff, 3),
            BuyersPerDay: theme is null ? 0 : Expos.BuyersPerDay(cfg, city, buff),
            Listings: listings,
            Report: report);
    }
}
