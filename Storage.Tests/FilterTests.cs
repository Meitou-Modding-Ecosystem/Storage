using Meitou.Simulation.Items;
using Storage.Filters;

namespace Storage.Tests;

/// <summary>
/// The storage filters (docs/storage.md "Storage filters"): an item's own level wins over its sub-category's, which wins over its category's, and
/// a category's setting clears the settings under it.
/// </summary>
public sealed class FilterTests
{
    static readonly ItemCatalog Catalog = Fixtures.Catalog;

    [Fact]
    public void Everything_is_allowed_at_normal_by_default()
    {
        var filter = new StorehouseFilter();

        Assert.Equal(StoragePriority.Normal, filter.Level("plate", Catalog));
        Assert.True(filter.Allows("katana", Catalog));
    }

    [Fact]
    public void An_item_inherits_its_sub_category_then_its_category()
    {
        var filter = new StorehouseFilter();
        filter.Put(FilterScope.Group, "Items", StoragePriority.Forbidden, Catalog);
        filter.Put(FilterScope.Sub, StorehouseFilter.SubKey("Items", "Raw food"), StoragePriority.High, Catalog);

        Assert.False(filter.Allows("plate", Catalog));
        Assert.Equal(StoragePriority.High, filter.Level("wheat", Catalog));
        Assert.True(filter.Allows("katana", Catalog));
    }

    [Fact]
    public void An_items_own_level_wins_and_reset_returns_to_inheriting()
    {
        var filter = new StorehouseFilter();
        filter.Put(FilterScope.Group, "Items", StoragePriority.Low, Catalog);
        filter.Put(FilterScope.Item, "water", StoragePriority.Critical, Catalog);
        Assert.Equal(StoragePriority.Critical, filter.Level("water", Catalog));

        filter.Put(FilterScope.Item, "water", null, Catalog);
        Assert.Equal(StoragePriority.Low, filter.Level("water", Catalog));
    }

    [Fact]
    public void Setting_a_category_clears_the_settings_under_it()
    {
        var filter = new StorehouseFilter();
        filter.Put(FilterScope.Item, "plate", StoragePriority.High, Catalog);
        filter.Put(FilterScope.Sub, StorehouseFilter.SubKey("Items", "Raw food"), StoragePriority.Low, Catalog);
        filter.Put(FilterScope.Item, "katana", StoragePriority.High, Catalog);

        filter.Put(FilterScope.Group, "Items", StoragePriority.Preferred, Catalog);

        Assert.Null(filter.Set(FilterScope.Item, "plate"));
        Assert.Null(filter.Set(FilterScope.Sub, StorehouseFilter.SubKey("Items", "Raw food")));
        Assert.Equal(StoragePriority.High, filter.Set(FilterScope.Item, "katana"));
    }

    [Fact]
    public void A_copy_holds_the_same_settings_and_hash()
    {
        var filter = new StorehouseFilter();
        filter.Put(FilterScope.Item, "plate", StoragePriority.High, Catalog);
        var copy = filter.Copy();

        Assert.Equal(filter.Entries(), copy.Entries());
    }

    [Theory]
    [InlineData(StoragePriority.Forbidden, StoragePriority.Low)]
    [InlineData(StoragePriority.Normal, StoragePriority.Preferred)]
    [InlineData(StoragePriority.Critical, StoragePriority.Critical)]
    public void Up_steps_one_tier_and_stops_at_critical(StoragePriority from, StoragePriority to) => Assert.Equal(to, FilterLevels.Up(from));

    [Theory]
    [InlineData(StoragePriority.Normal, StoragePriority.Deferred)]
    [InlineData(StoragePriority.Low, StoragePriority.Low)]
    public void Down_steps_one_tier_and_never_forbids(StoragePriority from, StoragePriority to) => Assert.Equal(to, FilterLevels.Down(from));

    [Fact]
    public void Toggle_forbids_and_allows_at_normal()
    {
        Assert.Equal(StoragePriority.Forbidden, FilterLevels.Toggle(StoragePriority.High));
        Assert.Equal(StoragePriority.Normal, FilterLevels.Toggle(StoragePriority.Forbidden));
    }
}
