using Meitou.Game.Host.Input;
using Storage.Ui;
using Xunit;

namespace Storage.Tests;

/// <summary>
/// The Manage storehouse key action's default.
/// </summary>
public sealed class KeyTests
{
    /// <summary>
    /// No game action holds the default key, so the key action keeps it on a fresh install (a held key is left out at start).
    /// </summary>
    [Fact]
    public void The_default_key_is_free_of_the_game_s_bindings()
    {
        var bindings = new InputBindings();
        var key = Binding.Of(StorageUi.DefaultKey);

        foreach (var action in Enum.GetValues<InputAction>()) Assert.DoesNotContain(key, bindings.Get(action));
    }
}
