using System.Numerics;
using Meitou.Data.Fcs;

namespace Storage.Network;

/// <summary>
/// What a hauling network node is.
/// </summary>
public enum NodeKind : byte
{
    /// <summary>
    /// The network's own storehouse.
    /// </summary>
    Storehouse,

    /// <summary>
    /// A storage (a box, a shelf, a resource store, another storehouse).
    /// </summary>
    Storage,

    /// <summary>
    /// A machine or farm with inputs it takes or an output it gives (a thresher, an oven, a well, a wheat farm).
    /// </summary>
    Machine,
}

/// <summary>
/// Units of one item a node holds and could send: <paramref name="Count"/> of <paramref name="Record"/> (<paramref name="Type"/>).
/// </summary>
public sealed record Stock(string Record, FcsRecordType Type, int Count);

/// <summary>
/// A node of a hauling network as a check of the world finds it (docs/storage.md "Hauling network"): one of the player's finished storages or
/// machines in the storehouse's range. Built in the system's serial step and read by the planner and the interface; never changes once built.
/// </summary>
public sealed class NodeView
{
    /// <summary>
    /// The node's site id.
    /// </summary>
    public required string Site { get; init; }

    /// <summary>
    /// The name shown: the player's custom name when it has one, else its building's.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The building record's string id.
    /// </summary>
    public required string Record { get; init; }

    /// <summary>
    /// Where it stands.
    /// </summary>
    public Vector3 Position { get; init; }

    /// <summary>
    /// What it is.
    /// </summary>
    public NodeKind Kind { get; init; }

    /// <summary>
    /// What it can ever send.
    /// </summary>
    public ItemSet Gives { get; init; } = ItemSet.Nothing;

    /// <summary>
    /// What it can ever take.
    /// </summary>
    public ItemSet Takes { get; init; } = ItemSet.Nothing;

    /// <summary>
    /// What it holds now and could send, in its own order.
    /// </summary>
    public IReadOnlyList<Stock> Holds { get; init; } = [];

    /// <summary>
    /// How many units of a record (of a type) it could take now; 0 when it is full or does not take it.
    /// </summary>
    public Func<string, FcsRecordType, int> Room { get; init; } = (_, _) => 0;

    /// <summary>
    /// The units of a record it holds.
    /// </summary>
    public int Count(string record) => Holds.Where(s => s.Record == record).Sum(s => s.Count);

    /// <summary>
    /// How many units of the record it takes now: none past <see cref="Takes"/>, else its <see cref="Room"/>.
    /// </summary>
    public int RoomFor(string record, FcsRecordType type) => Takes.Contains(record, type) ? Math.Max(0, Room(record, type)) : 0;
}
