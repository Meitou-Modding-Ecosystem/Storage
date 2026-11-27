using Meitou.Simulation.Items;

namespace Storage.Filters;

/// <summary>
/// The tiers the storage filters step through (docs/storage.md "Storage filters"): Forbidden, or allowed at a tier from Low to Critical; the
/// haulers bring the higher tiers first.
/// </summary>
public static class FilterLevels
{
    /// <summary>
    /// The lowest allowed tier.
    /// </summary>
    public const StoragePriority Lowest = StoragePriority.Low;

    /// <summary>
    /// The highest tier.
    /// </summary>
    public const StoragePriority Highest = StoragePriority.Critical;

    /// <summary>
    /// The tier above <paramref name="level"/> (Forbidden goes to Low; Critical stays).
    /// </summary>
    public static StoragePriority Up(StoragePriority level) => level < Lowest ? Lowest : level >= Highest ? Highest : level + 1;

    /// <summary>
    /// The tier below <paramref name="level"/>, never below Low (forbidding is its own switch).
    /// </summary>
    public static StoragePriority Down(StoragePriority level) => level <= Lowest ? Lowest : level - 1;

    /// <summary>
    /// Allow or forbid: an allowed level becomes Forbidden, Forbidden becomes Normal.
    /// </summary>
    public static StoragePriority Toggle(StoragePriority level) =>
        level == StoragePriority.Forbidden ? StoragePriority.Normal : StoragePriority.Forbidden;

    /// <summary>
    /// A tier's name as the window shows it.
    /// </summary>
    public static string Name(StoragePriority level) => level.ToString();
}
