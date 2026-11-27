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
        _window.Register(registration);
        StorageWindows.AddReach(registration, Ids.Storehouse, () => StorageSettings.Current.AccessMetres * 10f);
    }

    /// <summary>
    /// The storehouse's panel buttons: none for another building or one the player does not own.
    /// </summary>
    IEnumerable<UiBuildingAction> Lines(UiBuildingContext context)
    {
        var building = context.Building;
        if (!building.Owned || building.Record != Ids.Storehouse || building.SiteId is not { } site || _system() is null) return [];

        string caption = _window.Shows(site) ? "Close storehouse settings" : "Manage storehouse";
        var manage = new UiBuildingAction(caption, "Storage filters, the hauling network and everything in range.", () => _window.Toggle(site));
        var assign = new UiBuildingAction($"Assign selected: {HaulerJobs.Name}",
            $"Up to {HaulerJobs.MaxHaulers} people haul for this storehouse (remove the job in their jobs panel).",
            () => context.Enqueue(new AssignHaulersCommand(site) { Tick = context.Tick }));
        return [assign, manage];
    }
}
