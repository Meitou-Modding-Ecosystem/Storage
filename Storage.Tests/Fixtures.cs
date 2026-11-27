using System.Numerics;
using Meitou.Data.Fcs;
using Storage.Filters;
using Storage.Hauling;
using Storage.Network;

namespace Storage.Tests;

/// <summary>
/// Small hand-made catalogs, nodes and plans for the pure parts of the mod.
/// </summary>
static class Fixtures
{
    /// <summary>
    /// Iron plates and wheat (Items / Other and Raw food), water (Items / Raw food), a katana (Weapons / Katanas).
    /// </summary>
    public static readonly ItemCatalog Catalog = new(
    [
        new CatalogItem("plate", "Iron Plates", FcsRecordType.Item, "Items", "Other"),
        new CatalogItem("wheat", "Wheatstraw", FcsRecordType.Item, "Items", "Raw food"),
        new CatalogItem("water", "Water", FcsRecordType.Item, "Items", "Raw food"),
        new CatalogItem("katana", "Katana", FcsRecordType.Weapon, "Weapons", "Katanas"),
    ]);

    /// <summary>
    /// The storehouse's site id.
    /// </summary>
    public const string House = "house";

    /// <summary>
    /// A node holding <paramref name="holds"/> with room for <paramref name="room"/> units of anything it takes.
    /// </summary>
    public static NodeView Node(string site, NodeKind kind, ItemSet gives, ItemSet takes, int room, params (string Record, int Count)[] holds) => new()
    {
        Site = site,
        Name = site,
        Record = "building",
        Position = new Vector3(site.Length * 100, 0, 0),
        Kind = kind,
        Gives = gives,
        Takes = takes,
        Holds = [.. holds.Select(h => new Stock(h.Record, Catalog.Find(h.Record)?.Type ?? FcsRecordType.Item, h.Count))],
        Room = (_, _) => room,
    };

    /// <summary>
    /// The storehouse: takes anything, with <paramref name="room"/>.
    /// </summary>
    public static NodeView Storehouse(int room = 100) => Node(House, NodeKind.Storehouse, ItemSet.Everything, ItemSet.Everything, room);

    /// <summary>
    /// A plan's input over <paramref name="nodes"/>.
    /// </summary>
    public static PlanInput Input(HaulingNetwork network, StorehouseFilter? filter = null, IReadOnlyList<GatherSource>? gather = null,
        params NodeView[] nodes) =>
        new(network, nodes.ToDictionary(n => n.Site), filter ?? new StorehouseFilter(), Catalog, new Dictionary<(string, string), int>(),
            gather ?? []);
}
