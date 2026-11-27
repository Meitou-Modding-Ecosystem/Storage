using System.Numerics;
using Meitou.Data.Fcs;
using Meitou.Simulation;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Items;
using Meitou.Simulation.Production;
using Storage.Filters;
using Storage.Hauling;
using Storage.Records;

namespace Storage.Network;

/// <summary>
/// One item line of a storehouse's network inventory: units of a record in one node.
/// </summary>
public sealed record InventoryLine(string Record, string Name, string Group, string Sub, int Count, string Site, string SiteName, Vector3 Position);

/// <summary>
/// What a check of the world finds around one storehouse: its nodes, what lies around to gather, and every item in range.
/// </summary>
public sealed record ScanResult(IReadOnlyDictionary<string, NodeView> Nodes, IReadOnlyList<GatherSource> Gather, IReadOnlyList<InventoryLine> Inventory);

/// <summary>
/// The data a scan reads besides the world: the item catalog and the building names of the merged data.
/// </summary>
public sealed record ScanData(ItemCatalog Catalog, Func<string, string?> BuildingName);

/// <summary>
/// Finds a storehouse's network in the world (docs/storage.md "Hauling network"): the player's finished storages and machines within its range,
/// the loose items and finished outputs its haulers could gather, and the network inventory. Runs in the system's serial step.
/// </summary>
public static class NetworkScan
{
    /// <summary>
    /// A storehouse's range, in world units (0.1 m): 1500 m.
    /// </summary>
    public const float Range = 15000;

    /// <summary>
    /// The units a storage is taken to have room for while it has room for one (the trip learns the rest when it unloads).
    /// </summary>
    const int StorageRoom = HaulPlanner.MaxUnits;

    /// <summary>
    /// The scan around the storehouse <paramref name="storehouse"/>; <paramref name="gatherGround"/> also lists loose items to gather.
    /// </summary>
    public static ScanResult Scan(World world, BuildSite storehouse, HaulingNetwork network, ScanData data, bool gatherGround)
    {
        var probes = new Probes(world);
        var nodes = new SortedDictionary<string, NodeView>(StringComparer.Ordinal);
        var gather = new List<GatherSource>();
        var inventory = new List<InventoryLine>();
        foreach (var site in InRange(world, storehouse.Position))
        {
            if (world.Machines.Find(site.Id) is not { } machine || Node(site, storehouse, machine, data, probes) is not { } node) continue;

            nodes[site.Id] = node;
            AddLines(inventory, node, data.Catalog);
            if (node.Kind == NodeKind.Machine && !network.Out(site.Id).Any()) AddOutput(gather, node);
        }

        if (gatherGround) AddGround(world, storehouse.Position, gather);
        return new ScanResult(nodes, gather, inventory);
    }

    /// <summary>
    /// The player's finished buildings within range of a point, in site order.
    /// </summary>
    public static IEnumerable<BuildSite> InRange(World world, Vector3 at) =>
        world.Buildings.All.Where(s => s.Complete && !s.Destroyed && s.Owner == world.Player.Faction && Flat(s.Position, at) <= Range);

    /// <summary>
    /// The distance between two points on the ground plane.
    /// </summary>
    public static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));

    /// <summary>
    /// The name a site shows: the player's custom name, else its building's.
    /// </summary>
    public static string NameOf(BuildSite site, Func<string, string?> buildingName) =>
        site.CustomName is { Length: > 0 } custom ? custom : site.Name.Length > 0 ? site.Name : buildingName(site.Record) ?? site.Record;

    #region Nodes

    /// <summary>
    /// A site's node: a storage, a machine with inputs or an output (crafting benches aside); null for any other building.
    /// </summary>
    static NodeView? Node(BuildSite site, BuildSite storehouse, Machine machine, ScanData data, Probes probes)
    {
        string name = NameOf(site, data.BuildingName);
        if (machine.Data.IsStorage && machine.Storage is { } grid)
        {
            var kind = site.Id == storehouse.Id ? NodeKind.Storehouse : NodeKind.Storage;
            return Storage(site, name, kind, machine, grid, probes);
        }

        if (machine.Craft is not null || machine.Inputs.Length == 0 && machine.Output is null) return null;

        return MachineNode(site, name, machine, data.Catalog);
    }

    /// <summary>
    /// A storage's node: it sends what it holds and takes what its limits allow (the storehouse: anything; its filters decide).
    /// </summary>
    static NodeView Storage(BuildSite site, string name, NodeKind kind, Machine machine, Inventory grid, Probes probes)
    {
        var data = machine.Data;
        var takes = kind == NodeKind.Storehouse ? ItemSet.Everything
            : data.StorageLimit.Count > 0 ? ItemSet.Of(data.StorageLimit) : ItemSet.OfType(data.ItemTypeLimit);
        var holds = grid.Items
            .Where(i => i.Contents is null)
            .GroupBy(i => (i.Record, i.Type))
            .Select(g => new Stock(g.Key.Record, g.Key.Type, g.Sum(i => i.Quantity)))
            .ToList();
        return new NodeView
        {
            Site = site.Id, Name = name, Record = site.Record, Position = site.Position, Kind = kind,
            Gives = data.StorageLimit.Count > 0 ? ItemSet.Of(data.StorageLimit) : ItemSet.Everything, Takes = takes, Holds = holds,
            Room = (record, _) => probes.Fits(grid, record) ? StorageRoom : 0,
        };
    }

    /// <summary>
    /// A machine's node: it sends its output and takes its inputs, up to each input's capacity. A link may also bring a machine its own output (a
    /// well's water to the next well, ore back into a mine): it goes into the output slot up to its capacity, which the game's haulers never do.
    /// </summary>
    static NodeView MachineNode(BuildSite site, string name, Machine machine, ItemCatalog catalog)
    {
        var inputs = machine.Inputs;
        var output = machine.Output;
        var holds = output is { Count: > 0 } o ? [new Stock(o.Recipe.Item, TypeOf(catalog, o.Recipe.Item), o.Count)] : new List<Stock>();
        return new NodeView
        {
            Site = site.Id, Name = name, Record = site.Record, Position = site.Position, Kind = NodeKind.Machine,
            Gives = output is null ? ItemSet.Nothing : ItemSet.Of([output.Recipe.Item]),
            Takes = ItemSet.Of(TakenBy(machine)),
            Holds = holds,
            Room = (record, _) => InputRoom(machine, record) + OutputRoom(machine, record),
        };
    }

    /// <summary>
    /// The records a machine takes: its inputs' and its own output's.
    /// </summary>
    static IEnumerable<string> TakenBy(Machine machine)
    {
        var inputs = machine.Inputs.Select(i => i.Item);
        return machine.Output is { } output ? inputs.Append(output.Recipe.Item) : inputs;
    }

    /// <summary>
    /// The units of a record a machine's inputs have room for.
    /// </summary>
    public static int InputRoom(Machine machine, string record) =>
        machine.Inputs.Where(i => i.Takes(record)).Sum(i => Math.Max(0, i.Recipe.Capacity - i.Count));

    /// <summary>
    /// The units of a record a machine's output slot has room for: none unless it makes that record.
    /// </summary>
    public static int OutputRoom(Machine machine, string record) =>
        machine.Output is { } output && output.Recipe.Item == record ? Math.Max(0, output.Recipe.Capacity - output.Count) : 0;

    /// <summary>
    /// The record type of an item record (ITEM when the catalog does not list it).
    /// </summary>
    public static FcsRecordType TypeOf(ItemCatalog catalog, string record) => catalog.Find(record)?.Type ?? FcsRecordType.Item;

    #endregion

    #region Gathering and inventory

    /// <summary>
    /// A machine's finished output, to gather when no link of the network sends it.
    /// </summary>
    static void AddOutput(List<GatherSource> gather, NodeView node)
    {
        foreach (var stock in node.Holds)
            gather.Add(new GatherSource(new PickPlace(PickKind.Output, node.Site, 0, node.Position), stock.Record, stock.Type, stock.Count));
    }

    /// <summary>
    /// The loose items in range the player's people may take: no owner or the player's faction's, not a bag with things inside.
    /// </summary>
    static void AddGround(World world, Vector3 at, List<GatherSource> gather)
    {
        foreach (var g in world.GroundItems.All)
        {
            if (g.Item.Contents is not null || Flat(g.Position, at) > Range) continue;
            if (g.OwnedBy >= 0 && g.OwnedBy != world.Player.Faction) continue;

            gather.Add(new GatherSource(new PickPlace(PickKind.Ground, "", g.Id, g.Position), g.Item.Record, g.Item.Type, g.Item.Quantity));
        }
    }

    /// <summary>
    /// A node's items as inventory lines.
    /// </summary>
    static void AddLines(List<InventoryLine> lines, NodeView node, ItemCatalog catalog)
    {
        foreach (var stock in node.Holds)
        {
            var item = catalog.Find(stock.Record);
            lines.Add(new InventoryLine(stock.Record, item?.Name ?? stock.Record, item?.Group ?? "Other", item?.Sub ?? "Other", stock.Count, node.Site,
                node.Name, node.Position));
        }
    }

    #endregion

    /// <summary>
    /// One probe item per record for a check, to ask a grid whether a unit fits.
    /// </summary>
    sealed class Probes(World world)
    {
        /// <summary>
        /// The probes made so far; null for a record the world cannot make.
        /// </summary>
        readonly Dictionary<string, ItemInstance?> _made = new(StringComparer.Ordinal);

        /// <summary>
        /// Whether one unit of the record fits the grid.
        /// </summary>
        public bool Fits(Inventory grid, string record)
        {
            if (!_made.TryGetValue(record, out var probe))
            {
                probe = Carrying.Make(world, record);
                _made[record] = probe;
            }

            return probe is not null && grid.HasRoomFor(probe);
        }
    }
}
