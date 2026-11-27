using System.Globalization;
using Meitou.Game.Ui;
using Storage.Network;

namespace Storage.Ui;

/// <summary>
/// The hauling network's flow grid (docs/storage.md "Flow grid"): the network's nodes as cards on a grid with a wire for each link, in any
/// direction. Drag a card to move it, drag from a card's ▸ port onto another card to link them, and drag a node from the tray onto the grid to
/// put it in the network; a click on a card shows it under the tray.
/// </summary>
public sealed class FlowGrid(ManageWindow window)
{
    #region Layout

    /// <summary>
    /// A cell's size, a card's inset in its cell, a wire's thickness, the port's width and the tray's width, pixels.
    /// </summary>
    const float CellWidth = 84, CellHeight = 52, Inset = 5, Thickness = 2, PortWidth = 14, TrayWidth = 210;

    /// <summary>
    /// The scroll views' ids.
    /// </summary>
    const string CanvasId = "storage-grid-canvas", TrayId = "storage-grid-tray";

    /// <summary>
    /// What is being dragged: a card to move, a port to link from, or a tray node to add. Host thread, as is everything below.
    /// </summary>
    enum DragKind
    {
        /// <summary>
        /// Nothing.
        /// </summary>
        None,

        /// <summary>
        /// A card, to move it.
        /// </summary>
        Move,

        /// <summary>
        /// A card's port, to link it to another.
        /// </summary>
        Link,

        /// <summary>
        /// A node of the tray, to put it in the network.
        /// </summary>
        Add,
    }

    /// <summary>
    /// The drag under way and its node.
    /// </summary>
    (DragKind Kind, string Site) _drag;

    /// <summary>
    /// The node under the pointer during the drag (a card's or a cell's id); empty for none.
    /// </summary>
    string _over = "";

    /// <summary>
    /// The node shown under the tray (its site); null for none.
    /// </summary>
    string? _picked;

    /// <summary>
    /// The last frame's drop targets: the cards' and ports' ids to their sites, the cells' ids to their cells.
    /// </summary>
    readonly Dictionary<string, string> _cardSites = new(StringComparer.Ordinal);

    /// <summary>
    /// The cells' ids to their cells.
    /// </summary>
    readonly Dictionary<string, (int Column, int Row)> _cellAt = new(StringComparer.Ordinal);

    /// <summary>
    /// The last frame's cards by site.
    /// </summary>
    Dictionary<string, (int Column, int Row)> _cells = [];

    /// <summary>
    /// The grid, its tray and the picked node's details.
    /// </summary>
    public void Build(UiNode body, Kit kit, StorehouseView view, float top, float width, float height)
    {
        var sites = view.Nodes.Where(n => view.Network.IsActive(n.Site)).Select(n => n.Site).ToList();
        _cells = GridLayout.Cells(view.Network, sites);
        _cardSites.Clear();
        _cellAt.Clear();
        Tray(body, kit, view, top, height);
        Canvas(body, kit, view, TrayWidth + 10, top, width - TrayWidth - 10, height);
    }

    #endregion

    #region Tray and details

    /// <summary>
    /// The nodes in range outside the network, to drag onto the grid, and the picked node's details under them.
    /// </summary>
    void Tray(UiNode body, Kit kit, StorehouseView view, float top, float height)
    {
        float trayHeight = height * 0.5f;
        body.Children.Add(kit.Label("Drag onto the grid to add:", 0, top, TrayWidth, 22, kit.Keys.Head));
        var tray = Kit.Scroll(TrayId, 0, top + 24, TrayWidth, trayHeight - 24);
        var outside = view.Nodes.Where(n => !view.Network.IsActive(n.Site)).OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();
        for (int i = 0; i < outside.Count; i++)
            tray.Children.Add(Draggable(kit, $"storage-grid-tray-{i}", outside[i].Name, Kit.Args("g-add", outside[i].Site), 0, i * 28, TrayWidth - 18, 24,
                kit.Keys.Row));

        if (outside.Count == 0) tray.Children.Add(kit.Label("Everything in range is in.", 0, 0, TrayWidth - 18, 22, kit.Keys.Hint));
        body.Children.Add(tray);
        Details(body, kit, view, top + trayHeight + 6, height - trayHeight - 6);
    }

    /// <summary>
    /// The picked node: what it is and holds, its split, its links and the button that takes it out of the network.
    /// </summary>
    void Details(UiNode body, Kit kit, StorehouseView view, float top, float height)
    {
        var box = kit.Box(0, top, TrayWidth, height, kit.Keys.Popup);
        body.Children.Add(box);
        if (_picked is not { } site || view.Node(site) is not { } node)
        {
            box.Children.Add(kit.Label("Click a card to see it here.", 6, 4, TrayWidth - 12, 22, kit.Keys.Hint));
            return;
        }

        box.Children.Add(kit.Label(node.Name, 6, 4, TrayWidth - 12, 22, kit.Keys.Head));
        float y = 28;
        foreach (var stock in node.Holds.OrderByDescending(s => s.Count).Take(3))
        {
            string name = window.Catalog?.Find(stock.Record)?.Name ?? stock.Record;
            box.Children.Add(kit.Label($"{name} × {stock.Count}", 6, y, TrayWidth - 12, 20, kit.Keys.Hint));
            y += 20;
        }

        y = Links(box, kit, view, site, y + 4);
        var split = view.Network.Nodes.GetValueOrDefault(site)?.Split ?? SplitMode.InOrder;
        string next = split == SplitMode.Alternate ? nameof(SplitMode.InOrder) : nameof(SplitMode.Alternate);
        box.Children.Add(kit.Button(split == SplitMode.Alternate ? "Split: odd / even" : "Split: in order", Kit.Args("n-split", site, next), 6, y,
            TrayWidth - 12, 24));
        if (node.Kind != NodeKind.Storehouse)
            box.Children.Add(kit.Button("Take out of the network", Kit.Args("n-active", site, "0"), 6, y + 28, TrayWidth - 12, 24));
    }

    /// <summary>
    /// The picked node's links, each with its remove button; the next free line's top.
    /// </summary>
    static float Links(UiNode box, Kit kit, StorehouseView view, string site, float y)
    {
        int index = 0;
        foreach (var link in view.Network.Links.Where(l => l.From == site).Take(6))
        {
            string order = (++index).ToString(CultureInfo.InvariantCulture);
            box.Children.Add(kit.Label($"{order}. → {view.Node(link.To)?.Name ?? "(gone)"}", 6, y, TrayWidth - 44, 22));
            box.Children.Add(kit.Button("×", Kit.Args("n-unlink", link.From, link.To), TrayWidth - 34, y + 1, 28, 20));
            y += 24;
        }

        return y + 4;
    }

    #endregion

    #region Canvas

    /// <summary>
    /// The grid: its cells (the drop targets), the wires under the cards, then the cards.
    /// </summary>
    void Canvas(UiNode body, Kit kit, StorehouseView view, float left, float top, float width, float height)
    {
        var canvas = Kit.Scroll(CanvasId, left, top, width, height);
        Cells(canvas, kit);
        Wires(canvas, kit, view);
        foreach (var (site, cell) in _cells.OrderBy(c => c.Key, StringComparer.Ordinal))
            if (view.Node(site) is { } node) canvas.Children.Add(Card(kit, view, node, cell));

        body.Children.Add(canvas);
    }

    /// <summary>
    /// The cells, each a drop target lit while a drag is over it.
    /// </summary>
    void Cells(UiNode canvas, Kit kit)
    {
        for (int row = 0; row < HaulingNetwork.GridRows; row++)
        {
            for (int column = 0; column < HaulingNetwork.GridColumns; column++)
            {
                string id = $"storage-grid-cell-{column}-{row}";
                _cellAt[id] = (column, row);
                bool lit = _drag.Kind is DragKind.Move or DragKind.Add && _over == id;
                canvas.Children.Add(new UiNode(id)
                {
                    Placement = new UiPlacement(CellWidth - 2, CellHeight - 2, Left: column * CellWidth + 1, Top: row * CellHeight + 1),
                    Style = lit ? kit.Keys.CellOn : kit.Keys.Cell,
                });
            }
        }
    }

    /// <summary>
    /// A wire for each link between two cards, the picked node's lit, with its order at its start and an arrow at its end.
    /// </summary>
    void Wires(UiNode canvas, Kit kit, StorehouseView view)
    {
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var link in view.Network.ActiveLinks)
        {
            if (!_cells.TryGetValue(link.From, out var from) || !_cells.TryGetValue(link.To, out var to)) continue;

            int index = order[link.From] = order.GetValueOrDefault(link.From) + 1;
            string style = link.From == _picked || link.To == _picked ? kit.Keys.CardOn : kit.Keys.Wire;
            foreach (var s in GridLayout.Route(from, to, CellWidth, CellHeight, Inset, Thickness))
                canvas.Children.Add(kit.Box(s.Left, s.Top, s.Width, s.Height, style));

            float endX = to.Column * CellWidth + Inset - 9, endY = to.Row * CellHeight + CellHeight / 2 - 9;
            canvas.Children.Add(kit.Label("▸", endX, endY, 10, 18, kit.Keys.Head));
            float startX = from.Column * CellWidth + CellWidth - Inset + 1, startY = from.Row * CellHeight + CellHeight / 2 - 16;
            canvas.Children.Add(kit.Label(index.ToString(CultureInfo.InvariantCulture), startX, startY - 2, 12, 14, kit.Keys.Hint));
        }
    }

    /// <summary>
    /// A node's card: its name and kind, lit while picked or while a link drag is over it, with its port on its right.
    /// </summary>
    UiNode Card(Kit kit, StorehouseView view, NodeView node, (int Column, int Row) cell)
    {
        string id = $"storage-grid-card-{_cardSites.Count}";
        _cardSites[id] = node.Site;
        bool lit = node.Site == _picked || _drag.Kind == DragKind.Link && _over == id;
        float width = CellWidth - Inset * 2, height = CellHeight - Inset * 2;
        var card = Draggable(kit, id, "", Kit.Args("g-card", node.Site), cell.Column * CellWidth + Inset, cell.Row * CellHeight + Inset, width,
            height, lit ? kit.Keys.CardOn : kit.Keys.Card);
        bool alternate = view.Network.Nodes.GetValueOrDefault(node.Site)?.Split == SplitMode.Alternate;
        card.Children.Add(kit.Label(node.Name, 3, 1, width - PortWidth - 4, 20));
        card.Children.Add(kit.Label(Kind(node) + (alternate ? " ⇅" : ""), 3, 20, width - PortWidth - 4, 18, kit.Keys.Hint));
        if (!node.Gives.Equals(ItemSet.Nothing))
        {
            string port = $"storage-grid-port-{_cardSites.Count}";
            _cardSites[port] = node.Site;
            card.Children.Add(Draggable(kit, port, "▸", Kit.Args("g-port", node.Site), width - PortWidth, 0, PortWidth, height, kit.Keys.Button));
        }

        return card;
    }

    /// <summary>
    /// A node's kind, short.
    /// </summary>
    static string Kind(NodeView node) => node.Kind switch { NodeKind.Storehouse => "storehouse", NodeKind.Machine => "machine", _ => "storage" };

    /// <summary>
    /// A node that takes presses, drags and the release.
    /// </summary>
    static UiNode Draggable(Kit kit, string id, string text, string argument, float left, float top, float width, float height, string style) =>
        new(id, UiNodeKind.Button)
        {
            Placement = new UiPlacement(width, height, Left: left, Top: top),
            Style = style,
            Text = text.Length > 0 ? Kit.Fit(text, width - 6, 0.9f) : "",
            Action = kit.Keys.Action,
            Argument = argument,
            Pointer = UiPointerEvents.Drag | UiPointerEvents.Release,
        };

    #endregion

    #region Actions

    /// <summary>
    /// The grid's verbs: a press starts a drag (and picks a card), a drag follows the pointer, a release drops.
    /// </summary>
    public bool Handle(string[] parts, UiActionContext action, StorehouseView view)
    {
        var kind = parts[0] switch { "g-card" => DragKind.Move, "g-port" => DragKind.Link, "g-add" => DragKind.Add, _ => DragKind.None };
        if (kind == DragKind.None) return false;

        switch (action.Kind)
        {
            case UiEventKind.Press:
                _drag = (kind, parts[1]);
                if (kind != DragKind.Add) _picked = parts[1];
                break;
            case UiEventKind.Drag:
                _over = action.Over;
                break;
            case UiEventKind.Release:
                Drop(view, action.Over);
                _drag = default;
                _over = "";
                break;
        }

        return true;
    }

    /// <summary>
    /// Drops the drag on <paramref name="over"/>: a card or tray node onto a free cell moves or adds it (onto another card, the two swap); a port
    /// onto another card links them when the card takes something the port's node sends.
    /// </summary>
    void Drop(StorehouseView view, string over)
    {
        var (kind, site) = _drag;
        string house = view.Site;
        if (kind == DragKind.Link)
        {
            if (_cardSites.TryGetValue(over, out string? target) && target != site) Link(view, site, target);
            return;
        }

        if (_cellAt.TryGetValue(over, out var cell)) Place(house, kind, site, cell);
        else if (kind == DragKind.Move && _cardSites.TryGetValue(over, out string? other) && other != site) Swap(house, site, other);
    }

    /// <summary>
    /// Moves or adds a node to a free cell (an occupied one: no change).
    /// </summary>
    void Place(string house, DragKind kind, string site, (int Column, int Row) cell)
    {
        if (_cells.Any(c => c.Value == cell && c.Key != site)) return;
        if (kind == DragKind.Add) window.Send(new SetNodeActiveCommand(house, site, true) { Tick = 0 });

        window.Send(new PlaceNodeCommand(house, site, cell.Column, cell.Row) { Tick = 0 });
    }

    /// <summary>
    /// Swaps two cards' cells.
    /// </summary>
    void Swap(string house, string site, string other)
    {
        if (!_cells.TryGetValue(site, out var a) || !_cells.TryGetValue(other, out var b)) return;

        window.Send(new PlaceNodeCommand(house, site, b.Column, b.Row) { Tick = 0 });
        window.Send(new PlaceNodeCommand(house, other, a.Column, a.Row) { Tick = 0 });
    }

    /// <summary>
    /// Links two nodes when the target takes something the source sends; else says why not.
    /// </summary>
    void Link(StorehouseView view, string from, string to)
    {
        if (view.Node(from) is not { } source || view.Node(to) is not { } target || window.Catalog is not { } catalog) return;

        if (!StorehouseView.Compatible(source, target, catalog))
        {
            window.Tell($"{target.Name} takes nothing {source.Name} sends.");
            return;
        }

        window.Send(new LinkCommand(view.Site, from, to, true) { Tick = 0 });
        window.Tell($"Linked {source.Name} → {target.Name}.");
    }

    #endregion
}
