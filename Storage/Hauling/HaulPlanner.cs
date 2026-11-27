using System.Numerics;
using Meitou.Data.Fcs;
using Meitou.Simulation.Items;
using Storage.Filters;
using Storage.Network;

namespace Storage.Hauling;

/// <summary>
/// Everything a plan reads: the storehouse's network and nodes, its filters, the units other haulers have claimed, and what lies around to gather.
/// </summary>
public sealed record PlanInput(HaulingNetwork Network, IReadOnlyDictionary<string, NodeView> Nodes, StorehouseFilter Filter, ItemCatalog Catalog,
    IReadOnlyDictionary<(string Place, string Record), int> Claimed, IReadOnlyList<GatherSource> Gather);

/// <summary>
/// Chooses a storehouse hauler's next trip (docs/storage.md "Haulers"). Network links come first, the link whose target ranks highest (a link
/// into the storehouse ranks by its filter tier, any other link as Normal; ties in the player's link order); then gathering what the filters allow
/// into the storehouse, the highest tier first, then the nearest to the hauler. A pure read: the same input gives the same trip.
/// </summary>
public static class HaulPlanner
{
    /// <summary>
    /// The most units one trip carries.
    /// </summary>
    public const int MaxUnits = 20;

    /// <summary>
    /// The next trip for a hauler standing at <paramref name="at"/> who can carry <paramref name="carry"/> units; null when there is no work.
    /// </summary>
    public static Trip? Plan(PlanInput input, Vector3 at, int carry)
    {
        int budget = Math.Min(carry, MaxUnits);
        if (budget <= 0 || !input.Nodes.ContainsKey(input.Network.Storehouse)) return null;

        return LinkTrip(input, budget) ?? GatherTrip(input, at, budget);
    }

    #region Links

    /// <summary>
    /// The best trip down the network's links; null for none.
    /// </summary>
    static Trip? LinkTrip(PlanInput input, int budget)
    {
        Trip? best = null;
        var bestRank = StoragePriority.Forbidden;
        foreach (string from in input.Network.ActiveLinks.Select(l => l.From).Distinct())
        {
            if (!input.Nodes.TryGetValue(from, out var source)) continue;

            foreach (var stock in source.Holds)
            {
                if (SourceTrip(input, source, stock, budget) is not { } trip) continue;

                var rank = Rank(input, trip);
                if (best is not null && rank <= bestRank) continue;

                best = trip;
                bestRank = rank;
            }
        }

        return best;
    }

    /// <summary>
    /// A trip of one stock down its source's links; null when none of them takes any now.
    /// </summary>
    static Trip? SourceTrip(PlanInput input, NodeView source, Stock stock, int budget)
    {
        int free = stock.Count - input.Claimed.GetValueOrDefault((source.Site, stock.Record));
        if (free <= 0) return null;

        var targets = Targets(input, source, stock).ToList();
        if (targets.Count == 0) return null;

        var settings = input.Network.Nodes.GetValueOrDefault(source.Site);
        var place = new PickPlace(PickKind.Node, source.Site, 0, source.Position);
        return settings is { Split: SplitMode.Alternate }
            ? Alternating(input, place, stock, targets, Math.Min(free, budget), settings.Turn)
            : InOrder(input, place, stock, targets, Math.Min(free, budget));
    }

    /// <summary>
    /// The source's link targets in link order that can ever take the stock (the storehouse only while its filters allow it).
    /// </summary>
    static IEnumerable<NodeView> Targets(PlanInput input, NodeView source, Stock stock)
    {
        foreach (var link in input.Network.Out(source.Site))
        {
            if (!input.Nodes.TryGetValue(link.To, out var target) || !target.Takes.Contains(stock.Record, stock.Type)) continue;
            if (target.Site == input.Network.Storehouse && !input.Filter.Allows(stock.Record, input.Catalog)) continue;

            yield return target;
        }
    }

    /// <summary>
    /// <see cref="SplitMode.InOrder"/>: the units fill the first target with room, then the next.
    /// </summary>
    static Trip? InOrder(PlanInput input, PickPlace place, Stock stock, List<NodeView> targets, int units)
    {
        var drops = new List<Drop>();
        int left = units;
        foreach (var target in targets)
        {
            int room = Room(input, target, stock);
            if (room <= 0) continue;

            int count = Math.Min(left, room);
            drops.Add(new Drop(target.Site, count));
            left -= count;
            if (left == 0) break;
        }

        return drops.Count == 0 ? null : new Trip(place, stock.Record, stock.Type, units - left, drops);
    }

    /// <summary>
    /// <see cref="SplitMode.Alternate"/>: unit <c>turn + i</c> goes to target <c>(turn + i) % targets</c>; the trip stops at the first unit whose
    /// target has no room, so the units keep their turns.
    /// </summary>
    static Trip? Alternating(PlanInput input, PickPlace place, Stock stock, List<NodeView> targets, int units, long turn)
    {
        var rooms = targets.Select(t => Room(input, t, stock)).ToArray();
        var counts = new int[targets.Count];
        int taken = 0;
        while (taken < units)
        {
            int k = (int)((turn + taken) % targets.Count);
            if (rooms[k] <= counts[k]) break;

            counts[k]++;
            taken++;
        }

        if (taken == 0) return null;

        var sequence = Enumerable.Range(0, taken).Select(i => targets[(int)((turn + i) % targets.Count)].Site).ToList();
        return new Trip(place, stock.Record, stock.Type, taken, Trip.Grouped(sequence), sequence);
    }

    /// <summary>
    /// How many units of the stock a target takes now.
    /// </summary>
    static int Room(PlanInput input, NodeView target, Stock stock) => target.RoomFor(stock.Record, stock.Type);

    /// <summary>
    /// A link trip's rank: its best target's (the storehouse's filter tier, Normal for any other).
    /// </summary>
    static StoragePriority Rank(PlanInput input, Trip trip) =>
        trip.Drops.Max(d => d.Target == input.Network.Storehouse ? input.Filter.Level(trip.Record, input.Catalog) : StoragePriority.Normal);

    #endregion

    #region Gathering

    /// <summary>
    /// The best gathering trip into the storehouse: the highest filter tier, then the nearest to the hauler, then the lower place key; null for none.
    /// </summary>
    static Trip? GatherTrip(PlanInput input, Vector3 at, int budget)
    {
        var storehouse = input.Nodes[input.Network.Storehouse];
        var best = input.Gather
            .Select(g => (Source: g, Level: input.Filter.Level(g.Record, input.Catalog), Free: Free(input, g)))
            .Where(c => c.Level != StoragePriority.Forbidden && c.Free > 0)
            .OrderByDescending(c => c.Level)
            .ThenBy(c => Vector3.DistanceSquared(Flat(c.Source.Place.Position), Flat(at)))
            .ThenBy(c => c.Source.Place.Key, StringComparer.Ordinal)
            .Select(c => (c.Source, Units: Math.Min(Math.Min(c.Free, budget), storehouse.RoomFor(c.Source.Record, c.Source.Type))))
            .FirstOrDefault(c => c.Units > 0);
        if (best.Source is null) return null;

        var drop = new Drop(storehouse.Site, best.Units);
        return new Trip(best.Source.Place, best.Source.Record, best.Source.Type, best.Units, [drop]);
    }

    /// <summary>
    /// The units of a gather source no other hauler has claimed.
    /// </summary>
    static int Free(PlanInput input, GatherSource source) => source.Count - input.Claimed.GetValueOrDefault((source.Place.Key, source.Record));

    /// <summary>
    /// A point on the ground plane.
    /// </summary>
    static Vector3 Flat(Vector3 p) => new(p.X, 0, p.Z);

    #endregion
}
