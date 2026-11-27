using System.Globalization;
using Meitou.Game.Ui;
using Storage.Hauling;
using Storage.Network;

namespace Storage.Ui;

/// <summary>
/// The Hauling network tab (docs/storage.md "Hauling network"): every storage and machine in the storehouse's range, which of them are in its
/// network, and the links between them, as a list (with a searchable Send to dropdown per node) or as the drag and drop <see cref="FlowGrid"/>;
/// with the haulers and the Rescan button.
/// </summary>
public sealed class NetworkTab : IWindowTab
{
    #region Layout

    /// <summary>
    /// A list row's height, the toolbar's height and the dropdown's width, pixels.
    /// </summary>
    const float RowHeight = 28, BarHeight = 34, DropWidth = 360;

    /// <summary>
    /// The scroll views' ids.
    /// </summary>
    const string NodesId = "storage-network-nodes", DropId = "storage-network-drop";

    /// <summary>
    /// The window.
    /// </summary>
    readonly ManageWindow _window;

    /// <summary>
    /// The flow grid view.
    /// </summary>
    readonly FlowGrid _grid;

    /// <summary>
    /// Whether the flow grid is shown (else the list). Host thread, as is everything below.
    /// </summary>
    bool _showGrid;

    /// <summary>
    /// The node whose Send to dropdown is open (its site); null for none.
    /// </summary>
    string? _dropFor;

    /// <summary>
    /// The dropdown's search text.
    /// </summary>
    string _dropSearch = "";

    /// <summary>
    /// The tab of <paramref name="window"/>.
    /// </summary>
    public NetworkTab(ManageWindow window)
    {
        _window = window;
        _grid = new FlowGrid(window);
    }

    public void Build(UiNode body, Kit kit, StorehouseView view, float width, float height)
    {
        Toolbar(body, kit, view, width);
        float top = BarHeight + 6;
        if (_showGrid) _grid.Build(body, kit, view, top, width, height - top);
        else NodeList(body, kit, view, top, width - DropWidth - 12, height - top);

        if (!_showGrid) Side(body, kit, view, width - DropWidth, top, DropWidth, height - top);
    }

    /// <summary>
    /// The view switch, Rescan, the haulers' count and Assign selected.
    /// </summary>
    void Toolbar(UiNode body, Kit kit, StorehouseView view, float width)
    {
        body.Children.Add(kit.Button("List", Kit.Args("n-view", "0"), 0, 2, 80, 28, style: _showGrid ? kit.Keys.Tab : kit.Keys.TabOn));
        body.Children.Add(kit.Button("Flow grid", Kit.Args("n-view", "1"), 84, 2, 100, 28, style: _showGrid ? kit.Keys.TabOn : kit.Keys.Tab));
        body.Children.Add(kit.Button("Rescan", Kit.Args("n-rescan"), 196, 2, 90, 28));
        int active = view.Nodes.Count(n => view.Network.IsActive(n.Site));
        string summary = $"{view.Nodes.Count} storages and machines in range, {active} in the network, {view.Network.ActiveLinks.Count()} links.";
        body.Children.Add(kit.Label(summary, 296, 2, width - 296 - 330, 28, kit.Keys.Hint));
        body.Children.Add(kit.Label($"Haulers {view.Haulers.Count}/{HaulerJobs.MaxHaulers}", width - 320, 2, 110, 28, kit.Keys.Head));
        body.Children.Add(kit.Button("Assign selected", Kit.Args("n-assign"), width - 200, 2, 200, 28,
            view.Haulers.Count < HaulerJobs.MaxHaulers));
    }

    #endregion

    #region List

    /// <summary>
    /// Every node in range: the network's first, then the rest; each with its switch, its Send to button, its split and its links.
    /// </summary>
    void NodeList(UiNode body, Kit kit, StorehouseView view, float top, float width, float height)
    {
        var list = Kit.Scroll(NodesId, 0, top, width, height);
        float y = 0;
        foreach (var node in Ordered(view))
        {
            list.Children.Add(NodeRow(kit, view, node, y, width - 18));
            y += RowHeight;
            foreach (var (link, index) in view.Network.Links.Where(l => l.From == node.Site).Select((l, i) => (l, i)))
            {
                list.Children.Add(LinkRow(kit, view, link, index, y, width - 18));
                y += RowHeight - 4;
            }
        }

        body.Children.Add(list);
    }

    /// <summary>
    /// The nodes in the list's order: the storehouse, the network's other nodes, then the rest, each by name.
    /// </summary>
    static IEnumerable<NodeView> Ordered(StorehouseView view) => view.Nodes
        .OrderBy(n => n.Kind != NodeKind.Storehouse)
        .ThenBy(n => !view.Network.IsActive(n.Site))
        .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(n => n.Site, StringComparer.Ordinal);

    /// <summary>
    /// A node's row.
    /// </summary>
    UiNode NodeRow(Kit kit, StorehouseView view, NodeView node, float top, float width)
    {
        bool active = view.Network.IsActive(node.Site);
        var row = kit.Box(0, top, width, RowHeight - 2, active ? kit.Keys.RowOn : kit.Keys.Row);
        bool house = node.Kind == NodeKind.Storehouse;
        row.Children.Add(kit.Button(active ? "☑" : "☐", Kit.Args("n-active", node.Site, active ? "0" : "1"), 2, 2, 24, 22, !house));
        row.Children.Add(kit.Label(node.Name, 32, 0, width * 0.36f, RowHeight - 2));
        row.Children.Add(kit.Label(Describe(node), width * 0.36f + 36, 0, width * 0.3f, RowHeight - 2, kit.Keys.Hint));
        row.Children.Add(kit.Button(_dropFor == node.Site ? "Send to ▴" : "Send to ▾", Kit.Args("n-drop", node.Site), width - 250, 2, 110, 22,
            !node.Gives.Equals(ItemSet.Nothing)));
        var split = view.Network.Nodes.GetValueOrDefault(node.Site)?.Split ?? SplitMode.InOrder;
        string caption = split == SplitMode.Alternate ? "Odd / even" : "In order";
        string next = split == SplitMode.Alternate ? nameof(SplitMode.InOrder) : nameof(SplitMode.Alternate);
        row.Children.Add(kit.Button(caption, Kit.Args("n-split", node.Site, next), width - 134, 2, 130, 22,
            view.Network.Links.Count(l => l.From == node.Site) > 1));
        return row;
    }

    /// <summary>
    /// What a node is and the most it holds.
    /// </summary>
    static string Describe(NodeView node)
    {
        string kind = node.Kind switch { NodeKind.Storehouse => "this storehouse", NodeKind.Machine => "machine", _ => "storage" };
        int units = node.Holds.Sum(h => h.Count);
        return units > 0 ? $"{kind}, {units} items" : kind;
    }

    /// <summary>
    /// One link under its source: where it goes, its place among the source's links, and the buttons that move or remove it.
    /// </summary>
    static UiNode LinkRow(Kit kit, StorehouseView view, NetworkLink link, int index, float top, float width)
    {
        var row = kit.Box(36, top, width - 36, RowHeight - 6);
        string target = view.Node(link.To)?.Name ?? "(gone)";
        bool live = view.Network.IsActive(link.From) && view.Network.IsActive(link.To);
        string order = (index + 1).ToString(CultureInfo.InvariantCulture);
        row.Children.Add(kit.Label($"{order}. → {target}" + (live ? "" : "  (off: an end is out of the network)"), 0, 0, width - 160, RowHeight - 6,
            live ? kit.Keys.Text : kit.Keys.Hint));
        row.Children.Add(kit.Button("↑", Kit.Args("n-move", link.From, link.To, "-1"), width - 150, 1, 30, 20));
        row.Children.Add(kit.Button("↓", Kit.Args("n-move", link.From, link.To, "1"), width - 116, 1, 30, 20));
        row.Children.Add(kit.Button("×", Kit.Args("n-unlink", link.From, link.To), width - 82, 1, 30, 20));
        return row;
    }

    #endregion

    #region Side: dropdown and haulers

    /// <summary>
    /// The open Send to dropdown, else the haulers and how the network works.
    /// </summary>
    void Side(UiNode body, Kit kit, StorehouseView view, float left, float top, float width, float height)
    {
        var side = kit.Box(left, top, width, height, kit.Keys.Popup);
        if (_dropFor is { } site && view.Node(site) is { } from) Dropdown(side, kit, view, from, width, height);
        else Haulers(side, kit, view, width);

        body.Children.Add(side);
    }

    /// <summary>
    /// The dropdown: a search box and every other node of the range that could take something the source sends, not yet linked from it.
    /// </summary>
    void Dropdown(UiNode side, Kit kit, StorehouseView view, NodeView from, float width, float height)
    {
        side.Children.Add(kit.Label($"Send from {from.Name} to:", 8, 4, width - 50, 24, kit.Keys.Head));
        side.Children.Add(kit.Button("×", Kit.Args("n-drop", from.Site), width - 36, 4, 28, 22));
        side.Children.Add(kit.TextBox("storage-network-drop-search", _dropSearch, Kit.Args("n-dropsearch"), 8, 32, width - 16, 26));
        var targets = Targets(view, from).ToList();
        var list = Kit.Scroll(DropId, 8, 64, width - 16, height - 72);
        for (int i = 0; i < targets.Count; i++)
        {
            var to = targets[i];
            string caption = $"{to.Name}  ({Describe(to)})";
            list.Children.Add(kit.Button(caption, Kit.Args("n-link", from.Site, to.Site), 0, i * RowHeight, width - 40, RowHeight - 4,
                style: kit.Keys.Row));
        }

        if (targets.Count == 0) list.Children.Add(kit.Label("No storage or machine in range takes what this sends.", 0, 0, width - 24, 24, kit.Keys.Hint));
        side.Children.Add(list);
    }

    /// <summary>
    /// The nodes the dropdown offers: compatible, not linked yet, matching the search.
    /// </summary>
    IEnumerable<NodeView> Targets(StorehouseView view, NodeView from)
    {
        if (_window.Catalog is not { } catalog) return [];

        return view.Nodes
            .Where(to => StorehouseView.Compatible(from, to, catalog))
            .Where(to => !view.Network.Links.Contains(new NetworkLink(from.Site, to.Site)))
            .Where(to => _dropSearch.Length == 0 || to.Name.Contains(_dropSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(to => to.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(to => to.Site, StringComparer.Ordinal);
    }

    /// <summary>
    /// The haulers and what they do, and how the network works.
    /// </summary>
    static void Haulers(UiNode side, Kit kit, StorehouseView view, float width)
    {
        side.Children.Add(kit.Label("Haulers", 8, 4, width - 16, 24, kit.Keys.Head));
        float y = 30;
        if (view.Haulers.Count == 0)
        {
            side.Children.Add(kit.Label("None yet: select people and press Assign selected.", 8, y, width - 16, 22, kit.Keys.Hint));
            y += 24;
        }

        foreach (var hauler in view.Haulers)
        {
            side.Children.Add(kit.Label($"{hauler.Name}: {hauler.Doing}", 8, y, width - 16, 22));
            y += 24;
        }

        y += 12;
        foreach (string line in Help)
        {
            side.Children.Add(kit.Label(line, 8, y, width - 16, 22, kit.Keys.Hint));
            y += 22;
        }
    }

    /// <summary>
    /// How the network works, a line each.
    /// </summary>
    static readonly string[] Help =
    [
        "Only this storehouse is in the network at first.",
        "Send to links a node to another that takes what it sends.",
        "Haulers follow links first, a link's order breaking ties,",
        "then bring allowed items in range here, highest tier first.",
        "A link moves only what the source holds and the target takes;",
        "a machine or deposit also takes its own product back.",
        "Odd / even sends every other unit down each link in turn.",
        "The network is checked on load, on opening, and every so often.",
    ];

    #endregion

    #region Actions

    public bool Handle(string[] parts, UiActionContext action, StorehouseView view)
    {
        string house = view.Site;
        switch (parts[0])
        {
            case "n-view": _showGrid = parts[1] == "1"; return true;
            case "n-rescan": _window.Send(new RescanCommand(house) { Tick = 0 }); _window.Tell("Checking the network now."); return true;
            case "n-assign": _window.Send(new AssignHaulersCommand(house) { Tick = 0 }); return true;
            case "n-active": _window.Send(new SetNodeActiveCommand(house, parts[1], parts[2] == "1") { Tick = 0 }); return true;
            case "n-drop": ToggleDrop(parts[1]); return true;
            case "n-dropsearch": _dropSearch = action.Text.Trim(); _window.ScrollTop(DropId); return true;
            case "n-link": _window.Send(new LinkCommand(house, parts[1], parts[2], true) { Tick = 0 }); return true;
            case "n-unlink": _window.Send(new LinkCommand(house, parts[1], parts[2], false) { Tick = 0 }); return true;
            case "n-move" when int.TryParse(parts[3], CultureInfo.InvariantCulture, out int step):
                _window.Send(new LinkCommand(house, parts[1], parts[2], true, step) { Tick = 0 });
                return true;
            case "n-split" when Enum.TryParse<SplitMode>(parts[2], out var split):
                _window.Send(new SetSplitCommand(house, parts[1], split) { Tick = 0 });
                return true;
        }

        return _grid.Handle(parts, action, view);
    }

    /// <summary>
    /// Opens a node's dropdown, or closes it when it is open.
    /// </summary>
    void ToggleDrop(string site)
    {
        _dropFor = _dropFor == site ? null : site;
        _dropSearch = "";
        _window.ScrollTop(DropId);
    }

    #endregion
}
