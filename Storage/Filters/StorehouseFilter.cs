using Meitou.Simulation;
using Meitou.Simulation.Items;

namespace Storage.Filters;

/// <summary>
/// What a filter level is set on: one item, a sub-category of a category, or a whole category.
/// </summary>
public enum FilterScope : byte
{
    /// <summary>
    /// One item record (key: its string id).
    /// </summary>
    Item,

    /// <summary>
    /// A sub-category (key: <c>category/sub-category</c>, <see cref="StorehouseFilter.SubKey"/>).
    /// </summary>
    Sub,

    /// <summary>
    /// A category (key: its name).
    /// </summary>
    Group,
}

/// <summary>
/// One storehouse's storage filters (docs/storage.md "Storage filters"): each item is allowed at a priority tier or forbidden
/// (<see cref="StoragePriority.Forbidden"/>). A level set on an item beats its sub-category's, which beats its category's; nothing set is
/// <see cref="StoragePriority.Normal"/>, so a new storehouse takes everything. Its haulers gather only allowed items, the highest tier first, and the
/// game's own searches ask the same levels (<see cref="IStorageFilter"/>, <see cref="IStoragePriority"/>). Changed only in the system's serial
/// steps; the interface reads a copy.
/// </summary>
public sealed class StorehouseFilter
{
    /// <summary>
    /// The levels set, per scope, by key.
    /// </summary>
    readonly SortedDictionary<string, StoragePriority>[] _levels =
        [new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal)];

    /// <summary>
    /// The key of a sub-category.
    /// </summary>
    public static string SubKey(string group, string sub) => group + "/" + sub;

    /// <summary>
    /// The level set on a scope's key; null for none (it inherits).
    /// </summary>
    public StoragePriority? Set(FilterScope scope, string key) => _levels[(int)scope].TryGetValue(key, out var level) ? level : null;

    /// <summary>
    /// Sets a scope's level (null clears it, so it inherits). Setting a category or sub-category clears the levels set below it, so the whole
    /// category follows the new level.
    /// </summary>
    public void Put(FilterScope scope, string key, StoragePriority? level, ItemCatalog catalog)
    {
        if (level is { } set && !Enum.IsDefined(set)) return;

        if (scope != FilterScope.Item) ClearBelow(scope, key, catalog);
        if (level is { } value) _levels[(int)scope][key] = value;
        else _levels[(int)scope].Remove(key);
    }

    /// <summary>
    /// The level of an item record: its own, its sub-category's, its category's, else Normal. A record the catalog does not list follows only
    /// its own level.
    /// </summary>
    public StoragePriority Level(string record, ItemCatalog catalog)
    {
        if (Set(FilterScope.Item, record) is { } own) return own;
        if (catalog.Find(record) is not { } item) return StoragePriority.Normal;

        return Set(FilterScope.Sub, SubKey(item.Group, item.Sub)) ?? Set(FilterScope.Group, item.Group) ?? StoragePriority.Normal;
    }

    /// <summary>
    /// Whether the storehouse takes an item record.
    /// </summary>
    public bool Allows(string record, ItemCatalog catalog) => Level(record, catalog) != StoragePriority.Forbidden;

    /// <summary>
    /// Every level set, scope by scope in key order (the save and the hash).
    /// </summary>
    public IEnumerable<(FilterScope Scope, string Key, StoragePriority Level)> Entries()
    {
        for (int scope = 0; scope < _levels.Length; scope++)
            foreach (var (key, level) in _levels[scope]) yield return ((FilterScope)scope, key, level);
    }

    /// <summary>
    /// Sets a read level without clearing anything below it (a loaded save's entries).
    /// </summary>
    public void Load(FilterScope scope, string key, StoragePriority level)
    {
        if (Enum.IsDefined(scope) && Enum.IsDefined(level)) _levels[(int)scope][key] = level;
    }

    /// <summary>
    /// A copy, for the interface.
    /// </summary>
    public StorehouseFilter Copy()
    {
        var copy = new StorehouseFilter();
        foreach (var (scope, key, level) in Entries()) copy.Load(scope, key, level);
        return copy;
    }

    /// <summary>
    /// Adds the levels to the state hash.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        foreach (var (scope, key, level) in Entries())
        {
            hasher.Add((int)scope);
            hasher.Add(Rng.StableHash(key));
            hasher.Add((int)level);
        }
    }

    /// <summary>
    /// Clears the levels set on what lies under a category or sub-category.
    /// </summary>
    void ClearBelow(FilterScope scope, string key, ItemCatalog catalog)
    {
        foreach (var item in catalog.All)
        {
            bool under = scope == FilterScope.Group ? item.Group == key : SubKey(item.Group, item.Sub) == key;
            if (!under) continue;

            _levels[(int)FilterScope.Item].Remove(item.Id);
            if (scope == FilterScope.Group) _levels[(int)FilterScope.Sub].Remove(SubKey(item.Group, item.Sub));
        }
    }
}
