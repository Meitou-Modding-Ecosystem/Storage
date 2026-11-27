using Meitou.Game.Ui;
using Meitou.Modding;
using Meitou.Simulation;

namespace Storage.Ui;

/// <summary>
/// A tab of the Manage storehouse window.
/// </summary>
public enum WindowTab
{
    /// <summary>
    /// Storage filters: what the storehouse takes, and at what tier.
    /// </summary>
    Filters,

    /// <summary>
    /// Hauling network: the storages and machines in range and the links between them.
    /// </summary>
    Network,

    /// <summary>
    /// Network inventory: every item in range.
    /// </summary>
    Inventory,
}

/// <summary>
/// One tab's part of the window: it lays its nodes into the body and handles the verbs of its own buttons.
/// </summary>
public interface IWindowTab
{
    /// <summary>
    /// The tab's nodes for this frame, in the body's <paramref name="width"/> by <paramref name="height"/> pixels.
    /// </summary>
    void Build(UiNode body, Kit kit, StorehouseView view, float width, float height);

    /// <summary>
    /// Handles one of its verbs (<see cref="Kit.Parts"/>); false for a verb it does not know.
    /// </summary>
    bool Handle(string[] parts, UiActionContext action, StorehouseView view);
}

/// <summary>
/// The Manage storehouse window (docs/storage.md "Manage storehouse"): the panel's button opens it for one storehouse, with a tab per
/// <see cref="WindowTab"/>. It reads only the system's published views, on the host's thread, and changes the world only through commands.
/// </summary>
public sealed class ManageWindow
{
    #region Window

    /// <summary>
    /// The window's size, pixels.
    /// </summary>
    public const float Width = 1100, Height = 680;

    /// <summary>
    /// Where the tabs' body starts below the window's top, and the margin around it.
    /// </summary>
    const float BodyTop = 50, Margin = 12, FooterHeight = 24;

    /// <summary>
    /// The system of the running world.
    /// </summary>
    readonly Func<StorageSystem?> _system;

    /// <summary>
    /// The mod's context: the command queue and the tick.
    /// </summary>
    readonly ModContext _context;

    /// <summary>
    /// The tabs, by <see cref="WindowTab"/>.
    /// </summary>
    readonly Dictionary<WindowTab, IWindowTab> _tabs;

    /// <summary>
    /// The registration, for the scroll views' offsets.
    /// </summary>
    UiRegistration? _registration;

    /// <summary>
    /// The registered keys.
    /// </summary>
    WindowKeys _keys = null!;

    /// <summary>
    /// Whether the window is open, the storehouse it shows (its site id) and the tab shown. Host thread, as is everything below.
    /// </summary>
    bool _open;

    /// <summary>
    /// The storehouse shown.
    /// </summary>
    string _storehouse = "";

    /// <summary>
    /// The tab shown.
    /// </summary>
    WindowTab _tab;

    /// <summary>
    /// The last line the window told the player; empty for none.
    /// </summary>
    string _notice = "";

    /// <summary>
    /// The window over <paramref name="system"/>'s views; <paramref name="selected"/> gives the player's selected characters.
    /// </summary>
    public ManageWindow(Func<StorageSystem?> system, ModContext context, Func<IReadOnlyList<CharacterId>> selected)
    {
        _system = system;
        _context = context;
        Selected = selected;
        _tabs = new()
        {
            [WindowTab.Filters] = new FiltersTab(this),
            [WindowTab.Network] = new NetworkTab(this),
            [WindowTab.Inventory] = new InventoryTab(this, context),
        };
    }

    /// <summary>
    /// The player's selected characters now.
    /// </summary>
    public Func<IReadOnlyList<CharacterId>> Selected { get; }

    /// <summary>
    /// The item catalog of the running world; null before it.
    /// </summary>
    public Filters.ItemCatalog? Catalog => _system()?.Catalog;

    /// <summary>
    /// Whether the window is open.
    /// </summary>
    public bool IsOpen => _open;

    /// <summary>
    /// Closes the window.
    /// </summary>
    public void Close() => _open = false;

    /// <summary>
    /// Whether the window shows <paramref name="storehouse"/>.
    /// </summary>
    public bool Shows(string storehouse) => _open && _storehouse == storehouse;

    /// <summary>
    /// Opens the window on <paramref name="storehouse"/>, or closes it when it already shows it.
    /// </summary>
    public void Toggle(string storehouse)
    {
        if (Shows(storehouse))
        {
            _open = false;
            return;
        }

        _open = true;
        _storehouse = storehouse;
        _notice = "";
        Send(new RescanCommand(storehouse) { Tick = 0 });
    }

    /// <summary>
    /// Registers the window's styles, action and control, and puts it in the HUD's overlay.
    /// </summary>
    public void Register(UiRegistration registration)
    {
        _registration = registration;
        _keys = WindowKeys.Register(registration, Dispatch);
        string control = registration.RegisterControl("manage", Window);
        registration.Contribute(UiSlots.Overlay, frame => frame.CreateControl(control), order: 10);
    }

    /// <summary>
    /// Queues a command for the world, at the tick now.
    /// </summary>
    public void Send(SimCommand command) => _context.Enqueue(command with { Tick = _context.Tick });

    /// <summary>
    /// Shows a line at the window's foot.
    /// </summary>
    public void Tell(string text) => _notice = text;

    /// <summary>
    /// The offset of one of the window's scroll views, pixels.
    /// </summary>
    public float Offset(string node) => _registration?.ScrollOffset(node) ?? 0;

    /// <summary>
    /// Scrolls one of the window's scroll views to its top.
    /// </summary>
    public void ScrollTop(string node) => _registration?.ScrollTo(node, 0);

    #endregion

    #region Frame

    /// <summary>
    /// The window's node this frame: hidden while closed or before the first world.
    /// </summary>
    UiNode Window(UiFrameContext frame)
    {
        if (!_open || _system() is not { } system) return new UiNode("storage-window") { Visible = false };

        var kit = new Kit(_keys);
        var placement = new UiPlacement(Width, Height, Left: new UiLength(-Width / 2, 0.5f), Top: new UiLength(-Height / 2, 0.5f));
        var window = new UiNode("storage-window") { Placement = placement, Style = _keys.Window };
        var view = system.Views.GetValueOrDefault(_storehouse);
        Header(window, kit, view);
        Body(window, kit, view);
        Footer(window, kit);
        return window;
    }

    /// <summary>
    /// The title, the tabs and the close button.
    /// </summary>
    void Header(UiNode window, Kit kit, StorehouseView? view)
    {
        window.Children.Add(kit.Label("Storehouse: " + (view?.Name ?? "…"), Margin, 10, 360, 28, _keys.Title));
        float left = 390;
        foreach (var tab in Enum.GetValues<WindowTab>())
        {
            string style = tab == _tab ? _keys.TabOn : _keys.Tab;
            window.Children.Add(kit.Button(Caption(tab), Kit.Args("tab", tab.ToString()), left, 10, 190, 28, style: style));
            left += 196;
        }

        window.Children.Add(kit.Button("×", Kit.Args("close"), Width - Margin - 32, 10, 32, 28));
    }

    /// <summary>
    /// A tab's caption.
    /// </summary>
    public static string Caption(WindowTab tab) => tab switch
    {
        WindowTab.Filters => "Storage filters",
        WindowTab.Network => "Hauling network",
        _ => "Network inventory",
    };

    /// <summary>
    /// The shown tab's body, or why there is none yet.
    /// </summary>
    void Body(UiNode window, Kit kit, StorehouseView? view)
    {
        float width = Width - Margin * 2, height = Height - BodyTop - Margin - FooterHeight;
        var body = kit.Box(Margin, BodyTop, width, height);
        if (view is null) body.Children.Add(kit.Label("Looking over the network… (the storehouse is checked once it is built)", 0, 0, width, 24));
        else _tabs[_tab].Build(body, kit, view, width, height);

        window.Children.Add(body);
    }

    /// <summary>
    /// The notice line.
    /// </summary>
    void Footer(UiNode window, Kit kit)
    {
        if (_notice.Length == 0) return;

        window.Children.Add(kit.Label(_notice, Margin, Height - Margin - FooterHeight + 2, Width - Margin * 2, FooterHeight, _keys.Hint));
    }

    #endregion

    #region Actions

    /// <summary>
    /// The window's one action: the close and tab buttons here, every other verb to the shown tab.
    /// </summary>
    void Dispatch(UiActionContext action)
    {
        string[] parts = Kit.Parts(action.Argument);
        switch (parts[0])
        {
            case "close":
                _open = false;
                return;
            case "tab" when Enum.TryParse<WindowTab>(parts[1], out var tab):
                _tab = tab;
                _notice = "";
                return;
        }

        if (_system()?.Views.GetValueOrDefault(_storehouse) is not { } view) return;

        _tabs[_tab].Handle(parts, action, view);
    }

    #endregion
}
