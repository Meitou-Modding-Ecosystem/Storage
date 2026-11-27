using Meitou.Modding;

namespace Storage;

/// <summary>
/// The player's settings (docs/storage.md "Settings"), shown under the mod's row on the Settings screen. Systems and the interface read
/// <see cref="Current"/>.
/// </summary>
public sealed class StorageSettings
{
    /// <summary>
    /// The settings in force: the registered object once the mod has loaded, defaults before (and in tests).
    /// </summary>
    public static StorageSettings Current { get; internal set; } = new();

    /// <summary>
    /// The default of <see cref="AccessMetres"/>.
    /// </summary>
    public const int DefaultAccessMetres = 100;

    /// <summary>
    /// The longest <see cref="AccessMetres"/>.
    /// </summary>
    public const int MaxAccessMetres = 500;

    /// <summary>
    /// How far from a storehouse a selected character may be and still open its inventory with a click, in metres.
    /// </summary>
    [Setting("Inventory reach (m)", Hint = "How far from a storehouse one of your selected people may stand and still open its inventory with a click.",
        Min = DefaultAccessMetres, Max = MaxAccessMetres, Step = 10)]
    public int AccessMetres { get; set; } = DefaultAccessMetres;

    /// <summary>
    /// Whether a storehouse's haulers pick up the loose items its filters allow lying within its range, as well as finished goods waiting in
    /// machines' outputs.
    /// </summary>
    [Setting("Haulers gather loose items", Hint = "Storehouse haulers also pick up allowed items lying on the ground within range.")]
    public bool GatherGround { get; set; } = true;
}
