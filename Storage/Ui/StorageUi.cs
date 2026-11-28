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
    /// The interface over <paramref name="system"/>; registers a frame callback on <paramref name="context"/> for the selection.
    /// </summary>
    public StorageUi(Func<StorageSystem?> system, ModContext context)
    {
        _system = system;
        _window = new ManageWindow(system, context, () => _selected);
        context.AddFrame(frame => _selected = frame.Selected);
        context.AddKeyAction(KeyName, "Manage storehouse", Pressed, Binding.Of(DefaultKey));
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
        if (StorehouseOf(context) is not { } site)
        {
            _panel = null;
            return [];
        }

        _panel = new PanelShows(site, Environment.TickCount64);
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
    /// The Manage storehouse key (N by default): opens or closes the window of the storehouse whose panel shows, should the panel's button be
    /// crowded out; with no storehouse panel showing it closes an open window.
    /// </summary>
    /// <remarks>
    /// The snapshot's <c>InspectedBuilding</c> is published only for a building with doors, a lock or contents of its own fixtures, so it is null for
    /// a storehouse the player built; the panel's own source (<see cref="Lines"/>) says which storehouse it shows instead.
    /// </remarks>
    void Pressed(ModKeyContext context)
    {
        if (PanelShows.Current(_panel, Environment.TickCount64) is { } site && _system()?.Views.ContainsKey(site) == true) _window.Toggle(site);
        else if (_window.IsOpen) _window.Close();
    }

    /// <summary>
    /// The storehouse whose panel was last arranged and when (<see cref="Environment.TickCount64"/>); null while the panel shows another building.
    /// Host thread.
    /// </summary>
    PanelShows? _panel;

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

/// <summary>
/// The storehouse a building panel showed (<paramref name="Site"/>) at <paramref name="At"/> (milliseconds, <see cref="Environment.TickCount64"/>).
/// The panel is arranged at least 20 times a second while a building is selected, so a sighting older than <see cref="FreshMs"/> means it closed.
/// </summary>
public sealed record PanelShows(string Site, long At)
{
    /// <summary>
    /// How long a sighting of the panel holds, milliseconds.
    /// </summary>
    public const long FreshMs = 500;

    /// <summary>
    /// The storehouse <paramref name="last"/> saw if it is still fresh at <paramref name="now"/>; null otherwise.
    /// </summary>
    public static string? Current(PanelShows? last, long now) => last is { } seen && now - seen.At <= FreshMs ? seen.Site : null;
}
