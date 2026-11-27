using System.Numerics;
using Meitou.Data.Gameplay.Construction;
using Meitou.Modding;
using Storage.Network;
using Storage.Records;
using Storage.Ui;

namespace Storage.Tests;

/// <summary>
/// The pure parts of the interface: the network inventory's search, the flow grid's layout and wires, the list windowing, and the outdoor rule.
/// </summary>
public sealed class InterfaceTests
{
    static readonly InventoryLine[] Lines =
    [
        Line("plate", "Iron Plates", "Items", "Other", "Shelf of plates"),
        Line("wheat", "Wheatstraw", "Items", "Raw food", "Barn"),
        Line("katana", "Katana", "Weapons", "Katanas", "Armoury"),
    ];

    [Fact]
    public void Plain_search_finds_any_part_of_the_item_name()
    {
        var (lines, error) = InventorySearch.Match(Lines, "plat", regex: false, byContainer: false);

        Assert.Null(error);
        Assert.Equal(["plate"], lines.Select(l => l.Record));
    }

    [Fact]
    public void Container_search_looks_at_container_names()
    {
        var (lines, _) = InventorySearch.Match(Lines, "barn", regex: false, byContainer: true);

        Assert.Equal(["wheat"], lines.Select(l => l.Record));
    }

    [Fact]
    public void Regex_search_matches_patterns_ignoring_case()
    {
        var (lines, _) = InventorySearch.Match(Lines, "^(katana|wheat)", regex: true, byContainer: false);

        Assert.Equal(["wheat", "katana"], lines.Select(l => l.Record));
    }

    [Fact]
    public void A_broken_regex_says_so_and_matches_nothing()
    {
        var (lines, error) = InventorySearch.Match(Lines, "(", regex: true, byContainer: false);

        Assert.Empty(lines);
        Assert.NotNull(error);
    }

    [Fact]
    public void The_list_is_in_category_order()
    {
        var (lines, _) = InventorySearch.Match(Lines.Reverse(), "", regex: false, byContainer: false);

        Assert.Equal(["plate", "wheat", "katana"], lines.Select(l => l.Record));
    }

    [Fact]
    public void The_grid_keeps_placed_nodes_and_fills_free_cells_for_the_rest()
    {
        var network = new HaulingNetwork("house");
        network.Place("a", 0, 0);
        network.Place("b", 0, 0);

        var cells = GridLayout.Cells(network, ["house", "a", "b"]);

        Assert.Equal((0, 0), cells["a"]);
        Assert.Equal(3, cells.Values.Distinct().Count());
    }

    [Theory]
    [InlineData(0, 0, 3, 2)]
    [InlineData(4, 3, 1, 0)]
    [InlineData(2, 2, 2, 5)]
    [InlineData(2, 2, 1, 2)]
    public void A_wire_runs_from_the_source_card_to_the_target_card(int c1, int r1, int c2, int r2)
    {
        const float w = 84, h = 52, inset = 5, thick = 2;
        var segments = GridLayout.Route((c1, r1), (c2, r2), w, h, inset, thick);

        var start = new Vector2(c1 * w + w - inset, r1 * h + h / 2);
        var end = new Vector2(c2 * w + inset, r2 * h + h / 2);
        Assert.True(Covers(segments[0], start), "The wire starts at the source's right side.");
        Assert.True(Covers(segments[^1], end), "The wire ends at the target's left side.");
        for (int i = 1; i < segments.Count; i++) Assert.True(Touch(segments[i - 1], segments[i]), $"Segment {i} joins the one before.");
    }

    [Fact]
    public void Only_the_rows_in_view_are_made()
    {
        Assert.Equal((0, 13), Kit.Shown(0, 260, 26, 1000));
        Assert.Equal((99, 13), Kit.Shown(2600, 260, 26, 1000));
        Assert.Equal((0, 4), Kit.Shown(0, 260, 26, 4));
    }

    [Fact]
    public void A_storehouse_must_stand_outside()
    {
        var storehouse = new BuildingBuildData { Id = Ids.Storehouse, Name = "Storehouse" };
        var other = new BuildingBuildData { Id = "other", Name = "Other" };

        Assert.Equal(Placement.OutsideOnly, Placement.Refuse(new PlacementAsk(storehouse, default, 0, "house-1", 0, false)));
        Assert.Null(Placement.Refuse(new PlacementAsk(storehouse, default, 0, null, 0, false)));
        Assert.Null(Placement.Refuse(new PlacementAsk(other, default, 0, "house-1", 0, false)));
    }

    /// <summary>
    /// An inventory line.
    /// </summary>
    static InventoryLine Line(string record, string name, string group, string sub, string site) =>
        new(record, name, group, sub, 1, site, site, default);

    /// <summary>
    /// Whether a segment covers a point.
    /// </summary>
    static bool Covers(WireSegment s, Vector2 p) =>
        p.X >= s.Left - 0.01f && p.X <= s.Left + s.Width + 0.01f && p.Y >= s.Top - 0.01f && p.Y <= s.Top + s.Height + 0.01f;

    /// <summary>
    /// Whether two segments overlap or touch.
    /// </summary>
    static bool Touch(WireSegment a, WireSegment b) =>
        a.Left <= b.Left + b.Width + 0.01f && b.Left <= a.Left + a.Width + 0.01f && a.Top <= b.Top + b.Height + 0.01f && b.Top <= a.Top + a.Height + 0.01f;
}
