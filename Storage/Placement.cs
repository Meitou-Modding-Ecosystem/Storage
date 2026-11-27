using Meitou.Modding;
using Storage.Records;

namespace Storage;

/// <summary>
/// Keeps the storehouse outdoors (docs/storage.md "Storehouse"): a ghost standing in or on another building is refused.
/// </summary>
public static class Placement
{
    /// <summary>
    /// Build mode's text for a storehouse ghost inside or on a building.
    /// </summary>
    public const string OutsideOnly = "A storehouse must be built outside.";

    /// <summary>
    /// The rule.
    /// </summary>
    public static readonly PlacementRule Rule = new() { Refuse = Refuse };

    /// <summary>
    /// <see cref="OutsideOnly"/> for a storehouse ghost standing in or on a building; null for any other ghost.
    /// </summary>
    public static string? Refuse(PlacementAsk ask) => ask.Building.Id == Ids.Storehouse && ask.Parent is not null ? OutsideOnly : null;
}
