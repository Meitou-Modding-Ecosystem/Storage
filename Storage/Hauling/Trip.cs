using System.Numerics;
using Meitou.Data.Fcs;

namespace Storage.Hauling;

/// <summary>
/// Where a trip picks its units up.
/// </summary>
public enum PickKind : byte
{
    /// <summary>
    /// A network node: a storage's grid or a machine's output (<see cref="PickPlace.Site"/>).
    /// </summary>
    Node,

    /// <summary>
    /// A machine's output gathered for the storehouse (<see cref="PickPlace.Site"/>), outside the network's links.
    /// </summary>
    Output,

    /// <summary>
    /// A loose item on the ground (<see cref="PickPlace.Ground"/>).
    /// </summary>
    Ground,
}

/// <summary>
/// The place a trip picks from: a site, or a ground item's id, at <paramref name="Position"/>.
/// </summary>
public readonly record struct PickPlace(PickKind Kind, string Site, int Ground, Vector3 Position)
{
    /// <summary>
    /// The claim key: units of one record claimed at one place.
    /// </summary>
    public string Key => Kind == PickKind.Ground ? "ground:" + Ground : Site;
}

/// <summary>
/// One delivery of a trip: <paramref name="Count"/> units into <paramref name="Target"/>'s site.
/// </summary>
public readonly record struct Drop(string Target, int Count);

/// <summary>
/// A hauler's trip (docs/storage.md "Haulers"): pick up <see cref="Count"/> units of one record at <see cref="From"/>, then drop them off in
/// order. An alternating split's trip lists each unit's target in turn order (<see cref="Sequence"/>), and its source's turn moves on by the units
/// picked.
/// </summary>
public sealed record Trip(PickPlace From, string Record, FcsRecordType Type, int Count, IReadOnlyList<Drop> Drops, IReadOnlyList<string>? Sequence = null)
{
    /// <summary>
    /// Whether the trip follows an alternating split.
    /// </summary>
    public bool Alternates => Sequence is not null;

    /// <summary>
    /// The trip once only <paramref name="picked"/> of its units were picked up: an alternating trip keeps its first units' turns, another drops
    /// off in its order until the units run out.
    /// </summary>
    public Trip Cut(int picked)
    {
        if (picked >= Count) return this;
        if (Sequence is { } sequence) return this with { Count = picked, Drops = Grouped(sequence.Take(picked)), Sequence = [.. sequence.Take(picked)] };

        var drops = new List<Drop>();
        int left = picked;
        foreach (var drop in Drops)
        {
            if (left <= 0) break;

            drops.Add(drop with { Count = Math.Min(left, drop.Count) });
            left -= drop.Count;
        }

        return this with { Count = picked, Drops = drops };
    }

    /// <summary>
    /// The units' targets as drops, one per target in order of first turn.
    /// </summary>
    public static IReadOnlyList<Drop> Grouped(IEnumerable<string> sequence) =>
        [.. sequence.GroupBy(t => t, StringComparer.Ordinal).Select(g => new Drop(g.Key, g.Count()))];
}

/// <summary>
/// An item a storehouse's haulers could gather into it outside the network's links: a loose ground item or a machine's finished output.
/// </summary>
public sealed record GatherSource(PickPlace Place, string Record, FcsRecordType Type, int Count);
