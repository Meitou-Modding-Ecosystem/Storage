using Storage.Filters;
using Storage.Hauling;
using Storage.Network;

namespace Storage;

/// <summary>
/// One hauler as the window shows it: its name, and what it is carrying where.
/// </summary>
public sealed record HaulerLine(string Name, string Doing);

/// <summary>
/// What the window shows of one storehouse (docs/storage.md "Manage storehouse"), published by the system's serial step and read on the host's
/// thread: copies of its filters and network, the nodes and items the last scan found, its haulers, and its storage's own Put and Take priorities
/// (the game's, set here instead of on the building panel). Never changes once published.
/// </summary>
public sealed record StorehouseView(string Site, string Name, StorehouseFilter Filter, HaulingNetwork Network, IReadOnlyList<NodeView> Nodes,
    IReadOnlyList<InventoryLine> Inventory, IReadOnlyList<HaulerLine> Haulers, long ScannedTick)
{
    /// <summary>
    /// The storage's Put priority (the game's haulers and workers putting things in).
    /// </summary>
    public Meitou.Simulation.Items.StoragePriority PutPriority { get; init; }

    /// <summary>
    /// The storage's Take priority (the game's workers and builders taking things out).
    /// </summary>
    public Meitou.Simulation.Items.StoragePriority TakePriority { get; init; }

    /// <summary>
    /// A node of the last scan by site id; null when it was not found.
    /// </summary>
    public NodeView? Node(string site) => Nodes.FirstOrDefault(n => n.Site == site);

    /// <summary>
    /// Whether <paramref name="from"/> could ever send <paramref name="to"/> something (the dropdown lists only these).
    /// </summary>
    public static bool Compatible(NodeView from, NodeView to, ItemCatalog catalog) =>
        from.Site != to.Site && from.Gives.Overlaps(to.Takes, r => catalog.Find(r)?.Type);
}
