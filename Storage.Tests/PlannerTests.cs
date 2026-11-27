using System.Numerics;
using Meitou.Data.Fcs;
using Meitou.Simulation.Items;
using Storage.Filters;
using Storage.Hauling;
using Storage.Network;
using static Storage.Tests.Fixtures;

namespace Storage.Tests;

/// <summary>
/// The haulers' plans (docs/storage.md "Haulers"): links first, in their order or alternating, then gathering by tier and distance.
/// </summary>
public sealed class PlannerTests
{
    [Fact]
    public void In_order_fills_the_first_target_before_the_next()
    {
        var network = new HaulingNetwork(House);
        Link(network, "crate", "a", "b");
        var crate = Node("crate", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 0, ("plate", 10));
        var a = Node("a", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 4);
        var b = Node("b", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 20);

        var trip = HaulPlanner.Plan(Input(network, null, null, Storehouse(), crate, a, b), Vector3.Zero, 20)!;

        Assert.Equal(10, trip.Count);
        Assert.Equal([new Drop("a", 4), new Drop("b", 6)], trip.Drops);
        Assert.False(trip.Alternates);
    }

    [Fact]
    public void Odd_even_sends_every_other_unit_down_each_link()
    {
        var network = new HaulingNetwork(House);
        Link(network, "crate", "odd", "even");
        network.Node("crate").Split = SplitMode.Alternate;
        var crate = Node("crate", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 0, ("plate", 5));
        var odd = Node("odd", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 20);
        var even = Node("even", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 20);

        var trip = HaulPlanner.Plan(Input(network, null, null, Storehouse(), crate, odd, even), Vector3.Zero, 20)!;

        Assert.Equal(["odd", "even", "odd", "even", "odd"], trip.Sequence);
        Assert.Equal([new Drop("odd", 3), new Drop("even", 2)], trip.Drops);
    }

    [Fact]
    public void Odd_even_continues_from_the_sources_turn()
    {
        var network = new HaulingNetwork(House);
        Link(network, "crate", "odd", "even");
        network.Node("crate").Split = SplitMode.Alternate;
        network.Node("crate").Turn = 1;
        var crate = Node("crate", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 0, ("plate", 2));
        var odd = Node("odd", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 20);
        var even = Node("even", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 20);

        var trip = HaulPlanner.Plan(Input(network, null, null, Storehouse(), crate, odd, even), Vector3.Zero, 20)!;

        Assert.Equal(["even", "odd"], trip.Sequence);
    }

    [Fact]
    public void Odd_even_stops_at_the_first_target_without_room()
    {
        var network = new HaulingNetwork(House);
        Link(network, "crate", "odd", "even");
        network.Node("crate").Split = SplitMode.Alternate;
        var crate = Node("crate", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 0, ("plate", 6));
        var odd = Node("odd", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 1);
        var even = Node("even", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 20);

        var trip = HaulPlanner.Plan(Input(network, null, null, Storehouse(), crate, odd, even), Vector3.Zero, 20)!;

        Assert.Equal(["odd", "even"], trip.Sequence);
    }

    [Fact]
    public void A_link_moves_only_what_the_target_takes()
    {
        var network = new HaulingNetwork(House);
        Link(network, "crate", "plates");
        var crate = Node("crate", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 0, ("wheat", 8), ("plate", 3));
        var plates = Node("plates", NodeKind.Storage, ItemSet.Of(["plate"]), ItemSet.Of(["plate"]), 20);

        var trip = HaulPlanner.Plan(Input(network, null, null, Storehouse(), crate, plates), Vector3.Zero, 20)!;

        Assert.Equal("plate", trip.Record);
        Assert.Equal(3, trip.Count);
    }

    [Fact]
    public void A_chain_of_wells_passes_water_into_the_next_wells_output()
    {
        // A well takes its own product back (its output slot's room), so wells can relay water to a tower.
        var network = new HaulingNetwork(House);
        Link(network, "well1", "well2");
        var water = ItemSet.Of(["water"]);
        var well1 = Node("well1", NodeKind.Machine, water, water, 0, ("water", 7));
        var well2 = Node("well2", NodeKind.Machine, water, water, 5, ("water", 15));

        var trip = HaulPlanner.Plan(Input(network, null, null, Storehouse(), well1, well2), Vector3.Zero, 20)!;

        Assert.Equal(new PickPlace(PickKind.Node, "well1", 0, well1.Position), trip.From);
        Assert.Equal([new Drop("well2", 5)], trip.Drops);
    }

    [Fact]
    public void A_link_into_the_storehouse_respects_its_filters()
    {
        var network = new HaulingNetwork(House);
        Link(network, "crate", House);
        var filter = new StorehouseFilter();
        filter.Put(FilterScope.Item, "plate", StoragePriority.Forbidden, Catalog);
        var crate = Node("crate", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 0, ("plate", 4));

        Assert.Null(HaulPlanner.Plan(Input(network, filter, null, Storehouse(), crate), Vector3.Zero, 20));
    }

    [Fact]
    public void Links_go_to_the_storehouse_in_tier_order()
    {
        var network = new HaulingNetwork(House);
        Link(network, "crate", House);
        var filter = new StorehouseFilter();
        filter.Put(FilterScope.Item, "katana", StoragePriority.Critical, Catalog);
        var crate = Node("crate", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 0, ("plate", 4), ("katana", 1));

        var trip = HaulPlanner.Plan(Input(network, filter, null, Storehouse(), crate), Vector3.Zero, 20)!;

        Assert.Equal("katana", trip.Record);
    }

    [Fact]
    public void An_inactive_end_turns_the_link_off()
    {
        var network = new HaulingNetwork(House);
        Link(network, "crate", "a");
        network.SetActive("a", false);
        var crate = Node("crate", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 0, ("plate", 4));
        var a = Node("a", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 20);

        Assert.Null(HaulPlanner.Plan(Input(network, null, null, Storehouse(), crate, a), Vector3.Zero, 20));
    }

    [Fact]
    public void Gathering_takes_the_highest_tier_then_the_nearest()
    {
        var network = new HaulingNetwork(House);
        var filter = new StorehouseFilter();
        filter.Put(FilterScope.Item, "katana", StoragePriority.High, Catalog);
        var gather = new List<GatherSource>
        {
            Ground(1, "plate", new Vector3(10, 0, 0)),
            Ground(2, "plate", new Vector3(5, 0, 0)),
            Ground(3, "katana", new Vector3(500, 0, 0)),
        };

        var first = HaulPlanner.Plan(Input(network, filter, gather, Storehouse()), Vector3.Zero, 20)!;
        Assert.Equal(3, first.From.Ground);

        gather.RemoveAt(2);
        var second = HaulPlanner.Plan(Input(network, filter, gather, Storehouse()), Vector3.Zero, 20)!;
        Assert.Equal(2, second.From.Ground);
    }

    [Fact]
    public void Gathering_leaves_forbidden_items()
    {
        var network = new HaulingNetwork(House);
        var filter = new StorehouseFilter();
        filter.Put(FilterScope.Group, "Items", StoragePriority.Forbidden, Catalog);

        Assert.Null(HaulPlanner.Plan(Input(network, filter, [Ground(1, "plate", Vector3.Zero)], Storehouse()), Vector3.Zero, 20));
    }

    [Fact]
    public void A_trip_carries_at_most_what_the_hauler_can()
    {
        var network = new HaulingNetwork(House);
        Link(network, "crate", "a");
        var crate = Node("crate", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 0, ("plate", 50));
        var a = Node("a", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 100);

        Assert.Equal(7, HaulPlanner.Plan(Input(network, null, null, Storehouse(), crate, a), Vector3.Zero, 7)!.Count);
        Assert.Equal(HaulPlanner.MaxUnits, HaulPlanner.Plan(Input(network, null, null, Storehouse(), crate, a), Vector3.Zero, 99)!.Count);
    }

    /// <summary>
    /// Links <paramref name="from"/> to each target, all in the network.
    /// </summary>
    static void Link(HaulingNetwork network, string from, params string[] targets)
    {
        network.SetActive(from, true);
        foreach (string to in targets)
        {
            network.Link(from, to);
            network.SetActive(to, true);
        }
    }

    /// <summary>
    /// A loose item to gather.
    /// </summary>
    static GatherSource Ground(int id, string record, Vector3 at) =>
        new(new PickPlace(PickKind.Ground, "", id, at), record, Catalog.Find(record)!.Type, 1);
}
