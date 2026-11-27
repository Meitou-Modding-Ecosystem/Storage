using Meitou.Game.Ui;
using Meitou.Simulation.Items;
using Storage.Filters;

namespace Storage.Ui;

/// <summary>
/// The Storage filters tab (docs/storage.md "Storage filters"): the item categories as the F10 item menu groups them on the left, and on the right
/// the items of the chosen category, each allowed or forbidden at a tier. A setting on a category or sub-category applies to every item in it
/// that has none of its own.
/// </summary>
public sealed class FiltersTab(ManageWindow window) : IWindowTab
{
    #region Layout

    /// <summary>
    /// The category list's width and a row's height, pixels.
    /// </summary>
    const float CategoryWidth = 240, RowHeight = 26;

    /// <summary>
    /// The scroll views' ids.
    /// </summary>
    const string CategoriesId = "storage-filter-categories", ItemsId = "storage-filter-items";

    /// <summary>
    /// The chosen category (empty: every item) and sub-category (empty: the whole category). Host thread, as is the search text.
    /// </summary>
    string _group = "", _sub = "";

    /// <summary>
    /// The search text: items whose name holds it, in any category.
    /// </summary>
    string _search = "";

    public void Build(UiNode body, Kit kit, StorehouseView view, float width, float height)
    {
        if (window.Catalog is not { } catalog) return;

        Categories(body, kit, view, catalog, height);
        float left = CategoryWidth + 12, right = width - left;
        body.Children.Add(kit.Label("Search", left, 0, 60, 26, kit.Keys.Head));
        body.Children.Add(kit.TextBox("storage-filter-search", _search, Kit.Args("f-search"), left + 64, 0, 260, 26));
        body.Children.Add(kit.Label("Haulers bring allowed items, highest tier first.", left + 340, 0, right - 340, 26, kit.Keys.Hint));
        ScopeBar(body, kit, view, catalog, left, 34, right);
        Items(body, kit, view, catalog, left, 72, right, height - 72);
    }

    /// <summary>
    /// The category list: every item, then each category with its sub-categories under it.
    /// </summary>
    void Categories(UiNode body, Kit kit, StorehouseView view, ItemCatalog catalog, float height)
    {
        var list = Kit.Scroll(CategoriesId, 0, 0, CategoryWidth, height);
        float top = 0;
        list.Children.Add(CategoryRow(kit, "All items", "", "", null, top));
        foreach (string group in catalog.GroupNames)
        {
            top += RowHeight;
            list.Children.Add(CategoryRow(kit, group, group, "", view.Filter.Set(FilterScope.Group, group), top));
            foreach (string sub in catalog.SubsOf(group).Where(s => s.Length > 0))
            {
                top += RowHeight;
                var level = view.Filter.Set(FilterScope.Sub, StorehouseFilter.SubKey(group, sub));
                list.Children.Add(CategoryRow(kit, "    " + sub, group, sub, level, top));
            }
        }

        body.Children.Add(list);
    }

    /// <summary>
    /// One category row, with the level set on it.
    /// </summary>
    UiNode CategoryRow(Kit kit, string caption, string group, string sub, StoragePriority? level, float top)
    {
        bool chosen = group == _group && sub == _sub && _search.Length == 0;
        string text = level is { } set ? $"{caption}  ({FilterLevels.Name(set)})" : caption;
        return kit.Button(text, Kit.Args("f-cat", group, sub), 0, top, CategoryWidth - 16, RowHeight - 2,
            style: chosen ? kit.Keys.RowOn : kit.Keys.Row);
    }

    #endregion

    #region Scope bar and items

    /// <summary>
    /// The chosen category's own setting: allow or forbid all of it, its tier, and the reset to inherit.
    /// </summary>
    void ScopeBar(UiNode body, Kit kit, StorehouseView view, ItemCatalog catalog, float left, float top, float width)
    {
        if (_group.Length == 0 || _search.Length > 0)
        {
            body.Children.Add(kit.Label("Pick a category to set all of it at once, or set items one by one.", left, top, width, 28, kit.Keys.Hint));
            return;
        }

        var (scope, key) = _sub.Length > 0 ? (FilterScope.Sub, StorehouseFilter.SubKey(_group, _sub)) : (FilterScope.Group, _group);
        var own = view.Filter.Set(scope, key);
        var shown = own ?? (scope == FilterScope.Sub ? view.Filter.Set(FilterScope.Group, _group) : null) ?? StoragePriority.Normal;
        string name = _sub.Length > 0 ? $"{_group} / {_sub}" : _group;
        body.Children.Add(kit.Label($"All {name}: {FilterLevels.Name(shown)}{(own is null ? " (inherited)" : "")}", left, top, 360, 28, kit.Keys.Head));
        LevelButtons(body, kit, scope, key, shown, own is not null, left + 370, top);
    }

    /// <summary>
    /// The allow/forbid switch, the tier steps and the reset of one setting.
    /// </summary>
    static void LevelButtons(UiNode into, Kit kit, FilterScope scope, string key, StoragePriority level, bool own, float left, float top)
    {
        string s = ((int)scope).ToString(System.Globalization.CultureInfo.InvariantCulture);
        bool allowed = level != StoragePriority.Forbidden;
        into.Children.Add(kit.Button(allowed ? "Forbid" : "Allow", Kit.Args("f-set", s, key, Code(FilterLevels.Toggle(level))), left, top + 2, 72, 22));
        into.Children.Add(kit.Button("−", Kit.Args("f-set", s, key, Code(FilterLevels.Down(level))), left + 78, top + 2, 26, 22,
            allowed && level > FilterLevels.Lowest));
        into.Children.Add(kit.Button("+", Kit.Args("f-set", s, key, Code(FilterLevels.Up(level))), left + 108, top + 2, 26, 22,
            level < FilterLevels.Highest));
        into.Children.Add(kit.Button("Reset", Kit.Args("f-set", s, key, ""), left + 140, top + 2, 60, 22, own));
    }

    /// <summary>
    /// A level as an action argument.
    /// </summary>
    static string Code(StoragePriority level) => ((int)level).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The items shown: the search's across every category, else the chosen category's; only the rows in view are made.
    /// </summary>
    void Items(UiNode body, Kit kit, StorehouseView view, ItemCatalog catalog, float left, float top, float width, float height)
    {
        var items = Shown(catalog).ToList();
        var list = Kit.Scroll(ItemsId, left, top, width, height);
        list.Children.Add(kit.Extent(items.Count * RowHeight));
        var (first, count) = Kit.Shown(window.Offset(ItemsId), height, RowHeight, items.Count);
        for (int i = first; i < first + count; i++) list.Children.Add(ItemRow(kit, view, catalog, items[i], i * RowHeight, width - 18));

        if (items.Count == 0) list.Children.Add(kit.Label("No items match.", 0, 0, width, RowHeight, kit.Keys.Hint));
        body.Children.Add(list);
    }

    /// <summary>
    /// The items of the search or the chosen category.
    /// </summary>
    IEnumerable<CatalogItem> Shown(ItemCatalog catalog)
    {
        if (_search.Length > 0) return catalog.All.Where(i => i.Name.Contains(_search, StringComparison.OrdinalIgnoreCase));
        if (_group.Length == 0) return catalog.All;

        return catalog.All.Where(i => i.Group == _group && (_sub.Length == 0 || i.Sub == _sub));
    }

    /// <summary>
    /// One item's row: its name, its level (and whether it is its own) and the buttons that change it.
    /// </summary>
    static UiNode ItemRow(Kit kit, StorehouseView view, ItemCatalog catalog, CatalogItem item, float top, float width)
    {
        var level = view.Filter.Level(item.Id, catalog);
        bool own = view.Filter.Set(FilterScope.Item, item.Id) is not null;
        var row = kit.Box(0, top, width, RowHeight - 2, kit.Keys.Row);
        string where = item.Sub.Length > 0 ? $"{item.Group} / {item.Sub}" : item.Group;
        row.Children.Add(kit.Label(item.Name, 8, 0, width * 0.38f, RowHeight - 2));
        row.Children.Add(kit.Label(where, width * 0.38f + 8, 0, width * 0.22f, RowHeight - 2, kit.Keys.Hint));
        string style = level == StoragePriority.Forbidden ? kit.Keys.Bad : own ? kit.Keys.Head : kit.Keys.Hint;
        row.Children.Add(kit.Label(FilterLevels.Name(level) + (own ? "" : " (inherited)"), width * 0.6f + 8, 0, 140, RowHeight - 2, style));
        LevelButtons(row, kit, FilterScope.Item, item.Id, level, own, width - 210, -2);
        return row;
    }

    #endregion

    #region Actions

    public bool Handle(string[] parts, UiActionContext action, StorehouseView view)
    {
        switch (parts[0])
        {
            case "f-cat":
                (_group, _sub, _search) = (parts[1], parts[2], "");
                window.ScrollTop(ItemsId);
                return true;
            case "f-search":
                _search = action.Text.Trim();
                window.ScrollTop(ItemsId);
                return true;
            case "f-set":
                Set(parts, view);
                return true;
        }

        return false;
    }

    /// <summary>
    /// Sets a level (an empty level clears it, to inherit).
    /// </summary>
    void Set(string[] parts, StorehouseView view)
    {
        if (!int.TryParse(parts[1], out int scope)) return;

        StoragePriority? level = int.TryParse(parts[3], out int value) ? (StoragePriority)value : null;
        window.Send(new SetFilterCommand(view.Site, (FilterScope)scope, parts[2], level) { Tick = 0 });
    }

    #endregion
}
