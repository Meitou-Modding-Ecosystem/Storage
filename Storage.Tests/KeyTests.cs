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

    /// <summary>
    /// The key opens the storehouse whose panel is up, and none once the panel has not been laid out for a few frames (another building picked,
    /// or nothing).
    /// </summary>
    [Fact]
    public void The_key_finds_the_storehouse_whose_panel_is_up()
    {
        Assert.Equal("site-1", StorageUi.ShownStorehouse(("site-1", 100), 100));
        Assert.Equal("site-1", StorageUi.ShownStorehouse(("site-1", 100), 100 + StorageUi.PanelFrames));
        Assert.Null(StorageUi.ShownStorehouse(("site-1", 100), 101 + StorageUi.PanelFrames));
        Assert.Null(StorageUi.ShownStorehouse(null, 100));
    }
}
