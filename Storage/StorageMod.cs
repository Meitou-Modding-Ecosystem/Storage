using Meitou.Data.Fcs;
using Meitou.Modding;
using Meitou.Simulation;
using Storage.Filters;
using Storage.Hauling;
using Storage.Network;
using Storage.Records;
using Storage.Ui;

namespace Storage;

/// <summary>
/// The mod's entry, named by "entry" in meitou.json (docs/storage.md): adds the storehouse in the data phase, then registers the settings, the
/// hauler job, the outdoor rule, the system, the panel button and the Manage storehouse window.
/// </summary>
public sealed class StorageMod : IMod, IModDataPhase
{
    /// <summary>
    /// The mod's manifest id.
    /// </summary>
    public const string Id = "storage";

    /// <summary>
    /// The system of the latest world, for the interface and the debug row.
    /// </summary>
    StorageSystem? _latest;

    /// <summary>
    /// Adds the storehouse and its unlock to the merged game data.
    /// </summary>
    public void EditData(ModDataContext context) => StorehouseRecords.Apply(context);

    /// <summary>
    /// Registers everything the mod runs.
    /// </summary>
    public void Initialize(ModContext context)
    {
        context.AddSettings(StorageSettings.Current);
        context.AddJobType(HaulerJobs.Type, HaulerJobs.Name);
        if (context.Data.Find(Ids.Storehouse) is not null) context.AddJobOffer(HaulerJobs.Offer());

        context.AddPlacementRule(Placement.Rule);

        var names = context.Data.OfType(FcsRecordType.Building).ToDictionary(b => b.StringId, b => b.Name, StringComparer.Ordinal);
        var data = new ScanData(ItemCatalog.From(context.Data), record => names.GetValueOrDefault(record));
        context.AddSystem("storage", _ => _latest = new StorageSystem(data), SystemAnchor.End);
        context.AddUi(new StorageUi(() => _latest, context));
        context.AddDebugCommand("Storage: storehouses", _ => Told(context, Summary(_latest)));
    }

    /// <summary>
    /// The debug row's answer: each storehouse's nodes, links, items and haulers.
    /// </summary>
    static string Summary(StorageSystem? system)
    {
        if (system is null) return "no world yet";
        if (system.Views.Count == 0) return "no storehouse yet";

        return string.Join("; ", system.Views.Values.Select(v =>
            $"{v.Name}: {v.Nodes.Count} nodes, {v.Network.Links.Count} links, {v.Inventory.Sum(l => l.Count)} items, {v.Haulers.Count} haulers"));
    }

    /// <summary>
    /// Logs a debug answer too (the status line shows only its start).
    /// </summary>
    static string Told(ModContext context, string answer)
    {
        context.Log(answer);
        return answer;
    }
}
