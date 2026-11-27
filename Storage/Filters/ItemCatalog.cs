using Meitou.Data;
using Meitou.Data.Characters;
using Meitou.Data.Fcs;

namespace Storage.Filters;

/// <summary>
/// One item record the filters list: its string id, name, record type, category (by record type) and sub-category (by the record's own data).
/// </summary>
public sealed record CatalogItem(string Id, string Name, FcsRecordType Type, string Group, string Sub);

/// <summary>
/// The item records of the merged data, grouped as the F10 debug menu's item picker groups them (engine <c>DebugItemCatalog</c>): by record
/// type, then by the record's own category field (a weapon's skill category, an armour's slot, an item's function, a limb's slot).
/// Built once at load; read on any thread.
/// </summary>
public sealed class ItemCatalog
{
    #region Categories

    /// <summary>
    /// The categories in the order shown, by the record types they hold.
    /// </summary>
    public static readonly (string Group, FcsRecordType Type)[] Groups =
    [
        ("Items", FcsRecordType.Item), ("Weapons", FcsRecordType.Weapon), ("Crossbows", FcsRecordType.Crossbow), ("Armour", FcsRecordType.Armour),
        ("Containers", FcsRecordType.Container), ("Limb replacements", FcsRecordType.LimbReplacement), ("Maps", FcsRecordType.MapItem),
    ];

    /// <summary>
    /// The item function names (<c>ItemFunction</c> of the FCS editor), by value; one that is not listed is "Other".
    /// </summary>
    static readonly string[] FunctionNames =
    [
        "Other", "First aid", "Splint kits", "Food", "Containers", "Weapons", "Clothing", "Other", "Narcotics", "Tools", "Other", "Blueprints",
        "Robot repair", "Books", "Money", "Raw food", "Ammunition", "Severed limbs",
    ];

    /// <summary>
    /// The weapon skill category names (<c>WeaponCategory</c> of the FCS editor), by value 0 to 8.
    /// </summary>
    static readonly string[] WeaponCategoryNames =
        ["Katanas", "Sabres", "Blunt", "Heavy weapons", "Hackers", "Unarmed", "Bows", "Turrets", "Polearms"];

    /// <summary>
    /// The limb replacement slot names, from slot 50.
    /// </summary>
    static readonly string[] LimbNames = ["Left arm", "Right arm", "Left leg", "Right leg"];

    /// <summary>
    /// The <c>item function</c> of blank blueprint look-template ITEMs, left out because they carry no research.
    /// </summary>
    const int BlueprintItemFunction = 11;

    #endregion

    #region Catalog

    /// <summary>
    /// Every listed item by string id.
    /// </summary>
    readonly Dictionary<string, CatalogItem> _byId;

    /// <summary>
    /// The items in display order: category, sub-category, name, id.
    /// </summary>
    public IReadOnlyList<CatalogItem> All { get; }

    /// <summary>
    /// A catalog of <paramref name="items"/> (tests make one by hand).
    /// </summary>
    public ItemCatalog(IEnumerable<CatalogItem> items)
    {
        All = [.. items.OrderBy(i => GroupIndex(i.Group)).ThenBy(i => i.Sub, StringComparer.Ordinal).ThenBy(i => i.Name, StringComparer.Ordinal)
            .ThenBy(i => i.Id, StringComparer.Ordinal)];
        _byId = All.GroupBy(i => i.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    /// <summary>
    /// The catalog of the merged data's named item records.
    /// </summary>
    public static ItemCatalog From(GameDatabase data)
    {
        var types = Groups.ToDictionary(g => g.Type, g => g.Group);
        var items = data.Records.Values
            .Where(r => r.Name.Length > 0 && types.ContainsKey(r.Type))
            .Where(r => r.Type != FcsRecordType.Item || r.GetInt("item function") != BlueprintItemFunction)
            .Select(r => new CatalogItem(r.StringId, r.Name, r.Type, types[r.Type], SubOf(r)));
        return new ItemCatalog(items);
    }

    /// <summary>
    /// The item of a string id; null for a record the catalog does not list.
    /// </summary>
    public CatalogItem? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// The category names in display order, those with items only.
    /// </summary>
    public IEnumerable<string> GroupNames => All.Select(i => i.Group).Distinct();

    /// <summary>
    /// The sub-category names of a category, in display order.
    /// </summary>
    public IEnumerable<string> SubsOf(string group) => All.Where(i => i.Group == group).Select(i => i.Sub).Distinct();

    /// <summary>
    /// A category's place in <see cref="Groups"/>; past the end for another name.
    /// </summary>
    public static int GroupIndex(string group)
    {
        int at = Array.FindIndex(Groups, g => g.Group == group);
        return at < 0 ? Groups.Length : at;
    }

    #endregion

    #region Sub-categories

    /// <summary>
    /// The sub-category of a record: "Other" for the types that have none.
    /// </summary>
    static string SubOf(GameRecord r) => r.Type switch
    {
        FcsRecordType.Weapon => WeaponCategory(r.GetInt("skill category")),
        FcsRecordType.Armour => ArmourSlot(r.GetInt("slot", (int)AttachSlot.Body)),
        FcsRecordType.Item => Function(r.GetInt("item function")),
        FcsRecordType.LimbReplacement => Limb(r.GetInt("slot")),
        _ => "Other",
    };

    /// <summary>
    /// A weapon skill category's name.
    /// </summary>
    static string WeaponCategory(int category) => category switch
    {
        >= 0 and < 9 => WeaponCategoryNames[category],
        >= 9 and < 21 => "Creature attacks",
        _ => "Other",
    };

    /// <summary>
    /// An armour slot's name.
    /// </summary>
    static string ArmourSlot(int slot) => Enum.IsDefined((AttachSlot)slot) && (AttachSlot)slot != AttachSlot.None ? ((AttachSlot)slot).ToString() : "Other";

    /// <summary>
    /// An item function's name.
    /// </summary>
    public static string Function(int function) => function >= 0 && function < FunctionNames.Length ? FunctionNames[function] : "Other";

    /// <summary>
    /// A limb replacement slot's name.
    /// </summary>
    static string Limb(int slot) => slot is >= 50 and < 54 ? LimbNames[slot - 50] : "Other";

    #endregion
}
