using Meitou.Data;
using Meitou.Data.Fcs;

namespace Storage.Records;

/// <summary>
/// The storehouse's BUILDING record (docs/storage.md "Storehouse"), made in the data phase: the imported granary model's look with the game's
/// General storage functionality, a large grid, and a place in the build menu from the start.
/// </summary>
public static class StorehouseRecords
{
    /// <summary>
    /// The game's General storage functionality: any item.
    /// </summary>
    public const string GeneralStorage = "2950-gamedata.base";

    /// <summary>
    /// The research every game starts with finished (<c>_Default Start</c>), whose <c>enable buildings</c> puts the storehouse in the build menu.
    /// </summary>
    public const string DefaultStart = "5359-gamedata.base";

    /// <summary>
    /// The storehouse's grid, in inventory cells: as wide and tall as a 1080-pixel screen shows beside a character's inventory.
    /// </summary>
    public const int GridWidth = 16, GridHeight = 12;

    /// <summary>
    /// How many times a stack in the storehouse is larger than in a character's inventory (<c>stackable bonus mult</c>).
    /// </summary>
    public const float StackMult = 4;

    /// <summary>
    /// The Building Materials the storehouse costs.
    /// </summary>
    public const int Materials = 40;

    /// <summary>
    /// The build menu category.
    /// </summary>
    public const string Category = "STORAGE";

    /// <summary>
    /// The build menu text.
    /// </summary>
    public const string Description = "An Edo-period granary: large general storage. Build it outdoors. Its haulers (up to three) gather the items its"
        + " filters allow from within 1500 m and run the hauling network you lay out in its Manage storehouse window (bottom-left panel button).";

    /// <summary>
    /// The storehouse record; null when the game has neither the imported look nor a General storage building to stand in for it.
    /// </summary>
    public static FcsRecord? Build(GameDatabase game) => Look(game) is { } look ? Storehouse(look) : null;

    /// <summary>
    /// Queues the storehouse and its unlock: <c>_Default Start</c> gains it in <c>enable buildings</c> (a reference set adds that entry).
    /// </summary>
    public static void Apply(Meitou.Modding.ModDataContext context)
    {
        if (Build(context.Data) is not { } storehouse) return;

        context.Apply(storehouse);
        if (context.Data.Find(DefaultStart) is not { Type: FcsRecordType.Research }) return;

        context.Edit(DefaultStart).References["enable buildings"] = [new FcsReference(Ids.Storehouse, 0, 0, 0)];
    }

    /// <summary>
    /// The storehouse record copied from <paramref name="look"/>.
    /// </summary>
    static FcsRecord Storehouse(GameRecord look)
    {
        var record = RecordKit.Copy(look, Ids.Storehouse, "Storehouse", "functionality", "upgrades to", "produces", "limit inventory");
        RecordKit.Links(record, "functionality", (GeneralStorage, 0, 0));
        record.Bools["has inventory"] = true;
        record.Bools["has lock"] = false;
        record.Bools["creates player town"] = true;
        record.Ints["storage size width"] = GridWidth;
        record.Ints["storage size height"] = GridHeight;
        record.Ints["itemtype limit"] = (int)FcsRecordType.Item;
        record.Floats["stackable bonus mult"] = StackMult;
        record.Strings["building category"] = Category;
        record.Strings["Description"] = Description;
        SetCost(record);
        return record;
    }

    /// <summary>
    /// Sets the build cost to <see cref="Materials"/> Building Materials, keeping the item the look's cost names.
    /// </summary>
    static void SetCost(FcsRecord record)
    {
        if (record.References.GetValueOrDefault("construction") is not { Count: > 0 } cost) return;

        record.References["construction"] = [.. cost.Take(1).Select(c => c with { Value0 = Materials })];
    }

    /// <summary>
    /// The imported look (docs/structures.md); else the first General storage BUILDING by string id, so tools and tests without the deployed model
    /// still get a storehouse; null for neither.
    /// </summary>
    internal static GameRecord? Look(GameDatabase game) =>
        game.Find(Ids.StorehouseLook) is { Type: FcsRecordType.Building } own
            ? own
            : game.OfType(FcsRecordType.Building)
                .Where(b => b.GetReferences("functionality").Any(f => f.TargetStringId == GeneralStorage))
                .OrderBy(b => b.StringId, StringComparer.Ordinal)
                .FirstOrDefault();
}
