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

    [Fact]
    public void The_key_finds_the_storehouse_while_its_panel_is_fresh()
    {
        var seen = new PanelShows("site-7", 10_000);

        Assert.Equal("site-7", PanelShows.Current(seen, 10_000 + PanelShows.FreshMs));
        Assert.Null(PanelShows.Current(seen, 10_001 + PanelShows.FreshMs));
        Assert.Null(PanelShows.Current(null, 10_000));
    }
}
