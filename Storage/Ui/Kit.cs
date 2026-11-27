using Meitou.Game.Ui;

namespace Storage.Ui;

/// <summary>
/// Makes the window's nodes for one frame: labels, buttons, boxes and scroll views at pixel places, each with a fresh id, and each button running
/// the window's one action with its verb and arguments (<see cref="Args"/>).
/// </summary>
public sealed class Kit(WindowKeys keys)
{
    /// <summary>
    /// The separator of an action argument's parts (a unit separator: never in a name or id).
    /// </summary>
    public const char Separator = '\u001f';

    /// <summary>
    /// The width of one character of the window's text, pixels, for cutting text to fit.
    /// </summary>
    const float CharWidth = 7.2f;

    /// <summary>
    /// The nodes made so far, for their ids.
    /// </summary>
    int _made;

    /// <summary>
    /// The registered keys.
    /// </summary>
    public WindowKeys Keys => keys;

    /// <summary>
    /// An action argument: the verb, then its arguments.
    /// </summary>
    public static string Args(string verb, params string[] args) => string.Join(Separator, [verb, .. args]);

    /// <summary>
    /// An action argument's verb and arguments.
    /// </summary>
    public static string[] Parts(string argument) => argument.Split(Separator);

    /// <summary>
    /// <paramref name="text"/> cut with an ellipsis to fit <paramref name="width"/> pixels.
    /// </summary>
    public static string Fit(string text, float width, float scale = 1)
    {
        int most = Math.Max(1, (int)(width / (CharWidth * scale)));
        return text.Length <= most ? text : text[..(most - 1)] + "…";
    }

    /// <summary>
    /// A fresh node id.
    /// </summary>
    public string Id(string kind) => $"storage-{kind}-{_made++}";

    /// <summary>
    /// A label.
    /// </summary>
    public UiNode Label(string text, float left, float top, float width, float height, string? style = null) =>
        new(Id("label"), UiNodeKind.Label)
        {
            Placement = new UiPlacement(width, height, Left: left, Top: top),
            Style = style ?? keys.Text,
            Text = Fit(text, width),
        };

    /// <summary>
    /// A button running <paramref name="argument"/> (<see cref="Args"/>); a disabled one never runs it.
    /// </summary>
    public UiNode Button(string caption, string argument, float left, float top, float width, float height, bool enabled = true,
        string? style = null) =>
        new(Id("button"), UiNodeKind.Button)
        {
            Placement = new UiPlacement(width, height, Left: left, Top: top),
            Style = style ?? keys.Button,
            Text = Fit(caption, width - 6, 0.9f),
            Action = keys.Action,
            Argument = argument,
            Enabled = enabled,
        };

    /// <summary>
    /// A box: a panel in <paramref name="style"/> (none: invisible) that takes no pointer unless it has an action.
    /// </summary>
    public UiNode Box(float left, float top, float width, float height, string style = "", string id = "") =>
        new(id.Length > 0 ? id : Id("box"))
        {
            Placement = new UiPlacement(width, height, Left: left, Top: top),
            Style = style,
            Pickable = false,
        };

    /// <summary>
    /// A text box whose changes run <paramref name="argument"/> with the text.
    /// </summary>
    public UiNode TextBox(string id, string text, string argument, float left, float top, float width, float height) =>
        new(id, UiNodeKind.TextBox)
        {
            Placement = new UiPlacement(width, height, Left: left, Top: top),
            Text = text,
            MaxLength = 120,
            Action = keys.Action,
            Argument = argument,
        };

    /// <summary>
    /// A scroll view with a stable id (the host keeps its offset by it), its children placed absolutely.
    /// </summary>
    public static UiNode Scroll(string id, float left, float top, float width, float height) =>
        new(id, UiNodeKind.ScrollView) { Placement = new UiPlacement(width, height, Left: left, Top: top), ScrollStep = 48 };

    /// <summary>
    /// The rows of a list of <paramref name="total"/> rows of <paramref name="rowHeight"/> a scroll view <paramref name="viewHeight"/> high shows
    /// at <paramref name="offset"/>: the first and how many (one more each side, so the wheel never shows a gap).
    /// </summary>
    public static (int First, int Count) Shown(float offset, float viewHeight, float rowHeight, int total)
    {
        int first = Math.Clamp((int)(offset / rowHeight) - 1, 0, Math.Max(0, total));
        int count = Math.Min(total - first, (int)(viewHeight / rowHeight) + 3);
        return (first, Math.Max(0, count));
    }

    /// <summary>
    /// A box that makes a scroll view's content <paramref name="height"/> high, so its bar covers rows not made this frame.
    /// </summary>
    public UiNode Extent(float height) => Box(0, Math.Max(0, height - 1), 1, 1);
}
