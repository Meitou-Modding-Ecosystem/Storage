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
    /// The storehouse whose panel was last arranged: its instance (as <c>InspectedBuilding</c> names it) and its site; null while the panel shows
    /// another building. Host thread.
    /// </summary>
    (string Instance, string Site)? _panel;

    /// <summary>
    /// The Manage storehouse key action's name and default binding (Alt+M: Ctrl turns the camera and M alone opens the map). The player rebinds it
    /// on the Controls page.
    /// </summary>
    public const string KeyName = "manage-storehouse", KeyLabel = "Manage storehouse";

    public static readonly Meitou.Game.Host.Input.Binding KeyDefault =
        Meitou.Game.Host.Input.Binding.Of(Meitou.Game.Host.Input.Key.M).With(Meitou.Game.Host.Input.KeyModifiers.Alt);

    /// <summary>
    /// The interface over <paramref name="system"/>; registers a frame callback on <paramref name="context"/> for the selection.
    /// </summary>
    public StorageUi(Func<StorageSystem?> system, ModContext context)
    {
        _system = system;
        _window = new ManageWindow(system, context, () => _selected);
        context.AddFrame(frame => _selected = frame.Selected);
        context.AddKeyAction(KeyName, KeyLabel, Pressed, KeyDefault);
    }

    /// <summary>
    /// The Manage storehouse key: opens (or closes) the window of the selected storehouse, the one whose panel shows.
    /// </summary>
    void Pressed(Meitou.Game.Host.Input.ModKeyContext context)
    {
        if (_panel is not { } panel || context.Snapshot.InspectedBuilding?.Instance != panel.Instance) return;
        if (_system()?.Views.ContainsKey(panel.Site) != true) return;

        _window.Toggle(panel.Site);
    }

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
        if (StorehouseOf(context) is not { } site)
        {
            _panel = null;
            return [];
        }

        _panel = (context.Building.Instance, site);
        string caption = _window.Shows(site) ? "Close storehouse settings" : "Manage storehouse";
        return [new UiBuildingAction(caption, "Storage filters and priorities, the hauling network and everything in range.", () => _window.Toggle(site))];
    }

    /// <summary>
    /// The window's key: Esc closes it. (Opening it from the keyboard is the Manage storehouse key action, <see cref="Pressed"/>.)
    /// </summary>
    bool Key(UiKeyInput input)
    {
        if (!input.Cancel || !_window.IsOpen) return false;

        _window.Close();
        return true;
    }

    /// <summary>
    /// The storehouse's Haulers bar under Condition: its haulers out of three.
    /// </summary>
    IEnumerable<UiBuildingRow> Rows(UiBuildingContext context)
    {
        if (StorehouseOf(context) is not { } site) return [];

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
