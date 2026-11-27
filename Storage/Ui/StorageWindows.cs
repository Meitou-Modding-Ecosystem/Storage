using System.Reflection;
using Meitou.Game.Ui;
using Meitou.Modding;
using Meitou.Simulation;

namespace Storage.Ui;

/// <summary>
/// The engine's storage window calls the mod uses when the running game has them (docs/storage.md "Engine support"), found by reflection so the
/// mod still loads on a release without them: <c>ModInterface.OpenStorage(site, walker, reach)</c> opens a storage's window at once when a
/// character is within reach, else walks the walker there and opens it on arrival; <c>UiRegistration.AddStorageReach(record, reach)</c> lets a
/// click open a building's storage from that far.
/// </summary>
public static class StorageWindows
{
    /// <summary>
    /// <c>ModInterface.OpenStorage</c>; null on a game without it.
    /// </summary>
    static readonly MethodInfo? OpenMethod =
        typeof(ModInterface).GetMethod("OpenStorage", [typeof(string), typeof(CharacterId?), typeof(float)]);

    /// <summary>
    /// <c>UiRegistration.AddStorageReach</c>; null on a game without it.
    /// </summary>
    static readonly MethodInfo? ReachMethod = typeof(UiRegistration).GetMethod("AddStorageReach", [typeof(string), typeof(Func<float>)]);

    /// <summary>
    /// Whether the running game can open a storage's window for the mod.
    /// </summary>
    public static bool CanOpen => OpenMethod is not null;

    /// <summary>
    /// Opens <paramref name="site"/>'s window, walking <paramref name="walker"/> there first unless a selected character is within
    /// <paramref name="reach"/> (world units); false when the game cannot (then the caller walks the character itself).
    /// </summary>
    public static bool Open(ModInterface ui, string site, CharacterId? walker, float reach) =>
        OpenMethod?.Invoke(ui, [site, walker, reach]) is true;

    /// <summary>
    /// Lets a click on <paramref name="record"/>'s buildings open their storage from <paramref name="reach"/> (world units, asked each click);
    /// false on a game without it.
    /// </summary>
    public static bool AddReach(UiRegistration registration, string record, Func<float> reach)
    {
        if (ReachMethod is null) return false;

        ReachMethod.Invoke(registration, [record, reach]);
        return true;
    }
}
