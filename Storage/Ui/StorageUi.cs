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
        if (StorehouseOf(context) is not { } site) return [];

        string caption = _window.Shows(site) ? "Close storehouse settings" : "Manage storehouse";
        return [new UiBuildingAction(caption, "Storage filters and priorities, the hauling network and everything in range.", () => _window.Toggle(site))];
    }

    /// <summary>
    /// The window's keys: Esc closes it; Ctrl+M opens (or closes) the selected storehouse's, should the panel's button be crowded out.
    /// </summary>
    bool Key(UiKeyInput input)
    {
        if (input.Cancel && _window.IsOpen)
        {
            _window.Close();
            return true;
        }

        if (input.Key != Meitou.Game.Host.Input.Key.M || !input.Ctrl || input.Shift || input.Alt) return false;
        if (input.Snapshot.InspectedBuilding?.Instance is not { } site || _system()?.Views.ContainsKey(site) != true) return false;

        _window.Toggle(site);
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
