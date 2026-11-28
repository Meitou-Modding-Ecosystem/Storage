using Meitou.Game.Host.Input;
using Meitou.Game.Ui;
using Meitou.Modding;
using Meitou.Simulation;
using Storage.Hauling;
using Storage.Records;

namespace Storage.Ui;

/// <summary>
/// The storehouse's interface (docs/storage.md "Manage storehouse"): its building panel buttons (Manage storehouse, and Assign selected for the
/// hauler job), the Manage storehouse window, and, where the game supports it, opening its storage from <see cref="StorageSettings.AccessMetres"/>
/// away.
/// </summary>
public sealed class StorageUi : IUiExtension
{
    /// <summary>
    /// The player's selected characters, as the last frame saw them (any thread reads the reference).
    /// </summary>
    volatile IReadOnlyList<CharacterId> _selected = [];

    /// <summary>
    /// The system of the running world.
    /// </summary>
    readonly Func<StorageSystem?> _system;

    /// <summary>
    /// The Manage storehouse window.
    /// </summary>
    readonly ManageWindow _window;

    /// <summary>
    /// Frames drawn so far, counted by the frame callback.
    /// </summary>
    long _frame;

    /// <summary>
    /// The storehouse whose panel was last laid out, and the frame it was; the key opens it while the panel is still up. The snapshot's inspected
    /// building cannot tell: the game publishes it only for buildings with doors or containers, which the storehouse has none of.
    /// </summary>
    (string Site, long Frame)? _panel;

    /// <summary>
    /// Frames a panel layout counts as still showing (the panel is laid out every frame while it is up).
    /// </summary>
    public const long PanelFrames = 5;

    /// <summary>
    /// The interface over <paramref name="system"/>; registers a frame callback on <paramref name="context"/> for the selection.
    /// </summary>
    public StorageUi(Func<StorageSystem?> system, ModContext context)
    {
        _system = system;
        _window = new ManageWindow(system, context, () => _selected);
        context.AddFrame(OnFrame);
        context.AddKeyAction(KeyName, "Manage storehouse", Pressed, Binding.Of(DefaultKey));
    }

    /// <summary>
    /// Keeps the selection and counts the frame.
    /// </summary>
    void OnFrame(ModFrame frame)
    {
        _selected = frame.Selected;
        _frame++;
    }

    /// <summary>
    /// The Manage storehouse key action's saved name.
    /// </summary>
    public const string KeyName = "manage-storehouse";

    /// <summary>
    /// The Manage storehouse key's default: N, which no game action holds (Ctrl is the camera's pivot and M the map); the player rebinds it on the
    /// Controls page.
    /// </summary>
    public const Key DefaultKey = Meitou.Game.Host.Input.Key.N;

    public string Id => StorageMod.Id;

    public void Register(UiRegistration registration)
    {
        registration.AddBuildingActions(Lines);
        registration.AddBuildingRows(Rows);
        registration.AddKeyInput(Key);
        _window.Register(registration);
        StorageWindows.AddReach(registration, Ids.Storehouse, () => StorageSettings.Current.AccessMetres * 10f);
    }

    /// <summary>
    /// The storehouse's one panel button, Manage storehouse: none for another building or one the player does not own. Other mods' buttons and
    /// the game's own lines share the panel, which clips what does not fit, so it is the only one (Assign selected is in the window and on the
    /// right-click menu's Work as).
    /// </summary>
    IEnumerable<UiBuildingAction> Lines(UiBuildingContext context)
    {
        if (StorehouseOf(context) is not { } site) return [];

        _panel = (site, _frame);
        string caption = _window.Shows(site) ? "Close storehouse settings" : "Manage storehouse";
        return [new UiBuildingAction(caption, "Storage filters and priorities, the hauling network and everything in range.", () => _window.Toggle(site))];
    }

    /// <summary>
    /// The window's key: Esc closes it.
    /// </summary>
    bool Key(UiKeyInput input)
    {
        if (!input.Cancel || !_window.IsOpen) return false;

        _window.Close();
        return true;
    }

    /// <summary>
    /// The Manage storehouse key (N by default): opens or closes the window of the storehouse whose panel is up, should the panel's button be crowded out.
    /// </summary>
    void Pressed(ModKeyContext context)
    {
        if (ShownStorehouse(_panel, _frame) is not { } site || _system()?.Views.ContainsKey(site) != true) return;

        _window.Toggle(site);
    }

    /// <summary>
    /// The storehouse of <paramref name="panel"/> when its panel was laid out within <see cref="PanelFrames"/> of <paramref name="frame"/>; null
    /// otherwise (no storehouse panel up).
    /// </summary>
    public static string? ShownStorehouse((string Site, long Frame)? panel, long frame) =>
        panel is { } p && frame - p.Frame <= PanelFrames ? p.Site : null;

    /// <summary>
    /// The storehouse's Haulers bar under Condition: its haulers out of three.
    /// </summary>
    IEnumerable<UiBuildingRow> Rows(UiBuildingContext context)
    {
        if (StorehouseOf(context) is not { } site) return [];

        _panel = (site, _frame);
        int haulers = _system()?.Views.GetValueOrDefault(site)?.Haulers.Count ?? 0;
        string value = $"{haulers}/{HaulerJobs.MaxHaulers}";
        return [new UiBuildingRow("Haulers", haulers / (float)HaulerJobs.MaxHaulers, value, "People with the Storehouse hauler job here (at most three).")];
    }

    /// <summary>
    /// The selected building's site when it is one of the player's storehouses; null otherwise.
    /// </summary>
    string? StorehouseOf(UiBuildingContext context)
    {
        var building = context.Building;
        if (!building.Owned || building.Record != Ids.Storehouse || _system() is null) return null;

        return building.SiteId;
    }
}
