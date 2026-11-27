using Meitou.Data.Fcs;
using Storage.Hauling;
using Storage.Network;
using static Storage.Tests.Fixtures;

namespace Storage.Tests;

/// <summary>
/// The hauling network's links and nodes (docs/storage.md "Hauling network"), compatibility, trips cut short, and the save.
/// </summary>
public sealed class NetworkTests
{
    [Fact]
    public void The_network_starts_with_only_the_storehouse()
    {
        var network = new HaulingNetwork(House);

        Assert.True(network.IsActive(House));
        Assert.False(network.IsActive("crate"));
        Assert.Empty(network.Links);
    }

    [Fact]
    public void The_storehouse_cannot_be_taken_out()
    {
        var network = new HaulingNetwork(House);
        network.SetActive(House, false);

        Assert.True(network.IsActive(House));
    }

    [Fact]
    public void A_link_is_added_once_and_never_to_itself()
    {
        var network = new HaulingNetwork(House);

        Assert.True(network.Link("a", "b"));
        Assert.False(network.Link("a", "b"));
        Assert.False(network.Link("a", "a"));
        Assert.Single(network.Links);
    }

    [Fact]
    public void Move_swaps_a_link_with_its_sources_previous_or_next()
    {
        var network = new HaulingNetwork(House);
        network.Link("a", "x");
        network.Link("b", "y");
        network.Link("a", "z");

        network.Move("a", "z", -1);
        Assert.Equal([new NetworkLink("a", "z"), new NetworkLink("b", "y"), new NetworkLink("a", "x")], network.Links);

        network.Move("a", "z", -1);
        Assert.Equal(new NetworkLink("a", "z"), network.Links[0]);
    }

    [Fact]
    public void Forget_drops_nodes_and_links_no_longer_in_range()
    {
        var network = new HaulingNetwork(House);
        network.Link("a", "b");
        network.Link("a", House);
        network.SetActive("b", true);

        network.Forget(new HashSet<string> { House, "a" });

        Assert.Equal([new NetworkLink("a", House)], network.Links);
        Assert.False(network.Nodes.ContainsKey("b"));
    }

    [Fact]
    public void Place_keeps_a_node_on_the_grid()
    {
        var network = new HaulingNetwork(House);
        network.Place("a", 99, -4);

        Assert.Equal(HaulingNetwork.GridColumns - 1, network.Node("a").Column);
        Assert.Equal(0, network.Node("a").Row);
    }

    [Fact]
    public void Compatible_needs_something_the_source_sends_and_the_target_takes()
    {
        var general = Node("general", NodeKind.Storage, ItemSet.Everything, ItemSet.Everything, 0);
        var plates = Node("plates", NodeKind.Storage, ItemSet.Of(["plate"]), ItemSet.Of(["plate"]), 0);
        var fruit = Node("fruit", NodeKind.Storage, ItemSet.Of(["wheat"]), ItemSet.Of(["wheat"]), 0);
        var weapons = Node("weapons", NodeKind.Storage, ItemSet.Everything, ItemSet.OfType(FcsRecordType.Weapon), 0);

        Assert.True(StorehouseView.Compatible(general, plates, Catalog));
        Assert.False(StorehouseView.Compatible(plates, fruit, Catalog));
        Assert.False(StorehouseView.Compatible(plates, weapons, Catalog));
        Assert.True(StorehouseView.Compatible(general, weapons, Catalog));
        Assert.False(StorehouseView.Compatible(general, general, Catalog));
    }

    [Fact]
    public void A_trip_cut_short_keeps_its_first_drops()
    {
        var place = new PickPlace(PickKind.Node, "crate", 0, default);
        var inOrder = new Trip(place, "plate", FcsRecordType.Item, 10, [new Drop("a", 4), new Drop("b", 6)]);
        Assert.Equal([new Drop("a", 4), new Drop("b", 2)], inOrder.Cut(6).Drops);

        var sequence = new List<string> { "a", "b", "a", "b" };
        var alternating = new Trip(place, "plate", FcsRecordType.Item, 4, Trip.Grouped(sequence), sequence);
        var cut = alternating.Cut(3);
        Assert.Equal(["a", "b", "a"], cut.Sequence);
        Assert.Equal([new Drop("a", 2), new Drop("b", 1)], cut.Drops);
    }

    [Fact]
    public void The_save_round_trips_filters_nodes_and_links()
    {
        var state = new StorehouseState(House);
        state.Filter.Put(Filters.FilterScope.Item, "plate", Meitou.Simulation.Items.StoragePriority.High, Catalog);
        state.Network.Link("a", "b");
        state.Network.SetActive("a", true);
        state.Network.Node("a").Split = SplitMode.Alternate;
        state.Network.Node("a").Turn = 7;
        state.Network.Place("b", 3, 4);

        using var stream = new MemoryStream();
        StorageSave.Write(stream, [(House, state)]);
        stream.Position = 0;
        var read = Assert.Single(StorageSave.Read(stream));

        Assert.Equal(state.Filter.Entries(), read.Filter.Entries());
        Assert.Equal(state.Network.Links, read.Network.Links);
        var a = read.Network.Node("a");
        Assert.True(a.Active);
        Assert.Equal(SplitMode.Alternate, a.Split);
        Assert.Equal(7, a.Turn);
        Assert.Equal((3, 4), (read.Network.Node("b").Column, read.Network.Node("b").Row));
    }
}
