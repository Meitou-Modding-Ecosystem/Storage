using System.Numerics;
using Meitou.Game.Ui;

namespace Storage.Ui;

/// <summary>
/// The Manage storehouse window's registered styles and its one action, whose argument names what to do (<see cref="Kit.Args"/>).
/// </summary>
public sealed record WindowKeys(string Window, string Title, string Head, string Text, string Hint, string Bad, string Button, string Tab,
    string TabOn, string Row, string RowOn, string Field, string Card, string CardOn, string Cell, string CellOn, string Wire, string Popup,
    string Action)
{
    /// <summary>
    /// Registers the styles and the action <paramref name="dispatch"/>.
    /// </summary>
    public static WindowKeys Register(UiRegistration r, Action<UiActionContext> dispatch) => new(
        r.RegisterStyle("window", new UiStyle { Background = new Vector4(0.07f, 0.07f, 0.06f, 0.95f) }),
        r.RegisterStyle("title", LabelStyle(new Vector4(0.95f, 0.85f, 0.55f, 1), 1.15f)),
        r.RegisterStyle("head", LabelStyle(new Vector4(0.85f, 0.75f, 0.5f, 1), 1)),
        r.RegisterStyle("text", LabelStyle(new Vector4(0.92f, 0.92f, 0.88f, 1), 0.95f)),
        r.RegisterStyle("hint", LabelStyle(new Vector4(0.68f, 0.68f, 0.64f, 1), 0.85f)),
        r.RegisterStyle("bad", LabelStyle(new Vector4(0.95f, 0.45f, 0.4f, 1), 0.9f)),
        r.RegisterStyle("button", ButtonStyle(new Vector4(0.24f, 0.21f, 0.16f, 1))),
        r.RegisterStyle("tab", ButtonStyle(new Vector4(0.18f, 0.16f, 0.12f, 1))),
        r.RegisterStyle("tab-on", ButtonStyle(new Vector4(0.45f, 0.36f, 0.2f, 1))),
        r.RegisterStyle("row", Fill(new Vector4(0.11f, 0.105f, 0.09f, 1), new Vector4(0.17f, 0.15f, 0.12f, 1))),
        r.RegisterStyle("row-on", Fill(new Vector4(0.26f, 0.22f, 0.14f, 1), new Vector4(0.3f, 0.26f, 0.17f, 1))),
        r.RegisterStyle("field", new UiStyle { Text = new Vector4(0.95f, 0.95f, 0.9f, 1) }),
        r.RegisterStyle("card", Fill(new Vector4(0.2f, 0.25f, 0.3f, 1), new Vector4(0.26f, 0.32f, 0.38f, 1)) with { TextAlign = "Center" }),
        r.RegisterStyle("card-on", Fill(new Vector4(0.45f, 0.36f, 0.2f, 1), new Vector4(0.5f, 0.4f, 0.24f, 1)) with { TextAlign = "Center" }),
        r.RegisterStyle("cell", Fill(new Vector4(0.1f, 0.1f, 0.09f, 1), new Vector4(0.14f, 0.14f, 0.12f, 1))),
        r.RegisterStyle("cell-on", Fill(new Vector4(0.2f, 0.3f, 0.2f, 1), new Vector4(0.24f, 0.36f, 0.24f, 1))),
        r.RegisterStyle("wire", new UiStyle { Background = new Vector4(0.9f, 0.75f, 0.35f, 0.9f) }),
        r.RegisterStyle("popup", new UiStyle { Background = new Vector4(0.13f, 0.12f, 0.1f, 0.99f) }),
        r.RegisterAction("act", dispatch));

    /// <summary>
    /// A label style in <paramref name="colour"/> at <paramref name="scale"/>.
    /// </summary>
    static UiStyle LabelStyle(Vector4 colour, float scale) => new() { Text = colour, TextScale = scale, TextAlign = "Left VCenter" };

    /// <summary>
    /// A button style on <paramref name="background"/>.
    /// </summary>
    static UiStyle ButtonStyle(Vector4 background) => new()
    {
        Background = background,
        Hover = new Vector4(0.36f, 0.31f, 0.22f, 1),
        Disabled = new Vector4(0.14f, 0.13f, 0.12f, 1),
        Text = new Vector4(0.95f, 0.92f, 0.85f, 1),
        TextAlign = "Center",
        TextScale = 0.9f,
    };

    /// <summary>
    /// A filled panel style with a hover colour.
    /// </summary>
    static UiStyle Fill(Vector4 background, Vector4 hover) => new()
    {
        Background = background,
        Hover = hover,
        Text = new Vector4(0.92f, 0.92f, 0.88f, 1),
        TextScale = 0.9f,
    };
}
