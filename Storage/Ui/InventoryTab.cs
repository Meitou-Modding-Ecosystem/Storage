using System.Globalization;
using Meitou.Game.Host.Input;
using Meitou.Game.Ui;
using Meitou.Modding;
using Storage.Network;

namespace Storage.Ui;

/// <summary>
/// The Network inventory tab (docs/storage.md "Network inventory"): every item in the storehouse's range by category, each line naming its
/// container. Hovering or clicking a line shows where it is; a right click sends the lead selected character there and opens the container on
/// arrival. The search takes plain text or a regular expression, over item names or container names.
/// </summary>
public sealed class InventoryTab(ManageWindow window, ModContext context) : IWindowTab
{
    #region Layout

    /// <summary>
    /// A row's height, pixels.
    /// </summary>
    const float RowHeight = 24;

    /// <summary>
    /// The list's scroll view id.
    /// </summary>
    const string ListId = "storage-inventory-list";

    /// <summary>
    /// The search text, whether it is a regular expression and whether it searches container names. Host thread, as is everything below.
    /// </summary>
    string _search = "";

    /// <summary>
    /// Whether the search is a regular expression.
    /// </summary>
    bool _regex;

    /// <summary>
    /// Whether the search looks at container names (else item names).
    /// </summary>
    bool _byContainer;

    /// <summary>
    /// The rows of the last frame: a heading (no line) or an item line.
    /// </summary>
    List<(string Heading, InventoryLine? Line)> _rows = [];

    /// <summary>
    /// The line under the pointer, and the one last clicked.
    /// </summary>
    InventoryLine? _hovered, _picked;

    public void Build(UiNode body, Kit kit, StorehouseView view, float width, float height)
    {
        SearchBar(body, kit, width);
        var (lines, error) = InventorySearch.Match(view.Inventory, _search, _regex, _byContainer);
        _rows = Rows(lines);
        float listTop = 36, listHeight = height - listTop - 30;
        List(body, kit, error, listTop, width, listHeight);
        Status(body, kit, view, lines.Count, height - 26, width);
    }

    /// <summary>
    /// The search box, the regular expression switch and what it searches.
    /// </summary>
    void SearchBar(UiNode body, Kit kit, float width)
    {
        body.Children.Add(kit.Label("Search", 0, 0, 60, 26, kit.Keys.Head));
        body.Children.Add(kit.TextBox("storage-inventory-search", _search, Kit.Args("i-search"), 64, 0, 320, 26));
        body.Children.Add(kit.Button((_regex ? "☑" : "☐") + " Regex", Kit.Args("i-regex"), 394, 1, 90, 24));
        body.Children.Add(kit.Button("Item names", Kit.Args("i-by", "0"), 494, 1, 120, 24, style: _byContainer ? kit.Keys.Tab : kit.Keys.TabOn));
        body.Children.Add(kit.Button("Container names", Kit.Args("i-by", "1"), 618, 1, 140, 24, style: _byContainer ? kit.Keys.TabOn : kit.Keys.Tab));
        body.Children.Add(kit.Label("Right-click a line: go there and open it.", 770, 0, width - 770, 26, kit.Keys.Hint));
    }

    /// <summary>
    /// The lines under a heading for each category and sub-category.
    /// </summary>
    static List<(string, InventoryLine?)> Rows(List<InventoryLine> lines)
    {
        var rows = new List<(string, InventoryLine?)>();
        string last = "";
        foreach (var line in lines)
        {
            string heading = line.Sub.Length > 0 && line.Sub != line.Group ? $"{line.Group} / {line.Sub}" : line.Group;
            if (heading != last) rows.Add((heading, null));
            rows.Add(("", line));
            last = heading;
        }

        return rows;
    }

    /// <summary>
    /// The list: only the rows in view are made.
    /// </summary>
    void List(UiNode body, Kit kit, string? error, float top, float width, float height)
    {
        var list = Kit.Scroll(ListId, 0, top, width, height);
        list.Children.Add(kit.Extent(_rows.Count * RowHeight));
        var (first, count) = Kit.Shown(window.Offset(ListId), height, RowHeight, _rows.Count);
        for (int i = first; i < first + count; i++) list.Children.Add(Row(kit, i, width - 18));

        if (_rows.Count == 0) list.Children.Add(kit.Label(error ?? "Nothing in range matches.", 0, 0, width, RowHeight, error is null ? kit.Keys.Hint : kit.Keys.Bad));
        body.Children.Add(list);
    }

    /// <summary>
    /// One row: a heading, or an item line that takes hovers and clicks.
    /// </summary>
    UiNode Row(Kit kit, int index, float width)
    {
        var (heading, line) = _rows[index];
        float top = index * RowHeight;
        if (line is null) return kit.Label(heading, 4, top, width, RowHeight, kit.Keys.Head);

        bool on = line == _picked || line == _hovered;
        var row = new UiNode($"storage-inventory-row-{index}")
        {
            Placement = new UiPlacement(width, RowHeight - 2, Left: 0, Top: top),
            Style = on ? kit.Keys.RowOn : kit.Keys.Row,
            Action = kit.Keys.Action,
            Argument = Kit.Args("i-row", index.ToString(CultureInfo.InvariantCulture)),
            Pointer = UiPointerEvents.Hover | UiPointerEvents.Buttons,
        };
        row.Children.Add(kit.Label(line.Name, 24, 0, width * 0.42f, RowHeight - 2));
        row.Children.Add(kit.Label("× " + line.Count.ToString(CultureInfo.InvariantCulture), width * 0.42f + 28, 0, 80, RowHeight - 2));
        row.Children.Add(kit.Label(line.SiteName, width * 0.42f + 116, 0, width * 0.58f - 120, RowHeight - 2, kit.Keys.Hint));
        return row;
    }

    /// <summary>
    /// The foot line: where the hovered or picked line is, and how far from the storehouse.
    /// </summary>
    void Status(UiNode body, Kit kit, StorehouseView view, int count, float top, float width)
    {
        string text = (_hovered ?? _picked) is { } line
            ? $"{line.Name} × {line.Count} in {line.SiteName}, {Metres(view, line)} m from the storehouse."
            : $"{count} stacks in range. Hover or click a line to see its container.";
        body.Children.Add(kit.Label(text, 0, top, width, 24, kit.Keys.Hint));
    }

    /// <summary>
    /// A line's distance from the storehouse, metres.
    /// </summary>
    static string Metres(StorehouseView view, InventoryLine line) => view.Node(view.Site) is { } house
        ? Math.Round(NetworkScan.Flat(house.Position, line.Position) / 10).ToString(CultureInfo.InvariantCulture)
        : "?";

    #endregion

    #region Actions

    public bool Handle(string[] parts, UiActionContext action, StorehouseView view)
    {
        switch (parts[0])
        {
            case "i-search":
                _search = action.Text;
                window.ScrollTop(ListId);
                return true;
            case "i-regex":
                _regex = !_regex;
                return true;
            case "i-by":
                _byContainer = parts[1] == "1";
                return true;
            case "i-row":
                RowEvent(parts, action);
                return true;
        }

        return false;
    }

    /// <summary>
    /// A hover, click or right click on an item line.
    /// </summary>
    void RowEvent(string[] parts, UiActionContext action)
    {
        if (!int.TryParse(parts[1], CultureInfo.InvariantCulture, out int index) || index >= _rows.Count) return;
        if (_rows[index].Line is not { } line) return;

        switch (action.Kind)
        {
            case UiEventKind.Enter: _hovered = line; break;
            case UiEventKind.Leave when _hovered == line: _hovered = null; break;
            case UiEventKind.Press when action.Button == MouseButton.Right: GoThere(line); break;
            case UiEventKind.Press: _picked = line; break;
        }
    }

    /// <summary>
    /// Sends the lead selected character to the line's container and opens it on arrival.
    /// </summary>
    void GoThere(InventoryLine line)
    {
        _picked = line;
        if (window.Selected() is not [var who, ..])
        {
            window.Tell("Select one of your people first, then right-click the line.");
            return;
        }

        if (StorageWindows.Open(context.Interface, line.Site, who, 0))
        {
            window.Tell($"On the way to {line.SiteName}; it opens on arrival.");
            return;
        }

        window.Send(new GoToStorageCommand(who, line.Site) { Tick = 0 });
        window.Tell($"On the way to {line.SiteName}. Click it there to open it (this game cannot open it on arrival yet).");
    }

    #endregion
}
