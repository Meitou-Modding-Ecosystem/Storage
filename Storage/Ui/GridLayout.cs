using Storage.Network;

namespace Storage.Ui;

/// <summary>
/// A straight piece of a wire on the flow grid, pixels from the grid's top left.
/// </summary>
public readonly record struct WireSegment(float Left, float Top, float Width, float Height);

/// <summary>
/// The flow grid's layout (docs/storage.md "Flow grid"): where each node of the network stands and how a link's wire runs between two cards.
/// </summary>
public static class GridLayout
{
    /// <summary>
    /// The cells of <paramref name="sites"/> (the network's nodes in order): each placed node in its own cell (the first of two in one cell keeps
    /// it), then every other node in the free cells by rows.
    /// </summary>
    public static Dictionary<string, (int Column, int Row)> Cells(HaulingNetwork network, IReadOnlyList<string> sites)
    {
        var cells = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        var taken = new HashSet<(int, int)>();
        var unplaced = new List<string>();
        foreach (string site in sites)
        {
            var node = network.Nodes.GetValueOrDefault(site);
            if (node is { Column: >= 0, Row: >= 0 } && taken.Add((node.Column, node.Row))) cells[site] = (node.Column, node.Row);
            else unplaced.Add(site);
        }

        var free = FreeCells(taken).GetEnumerator();
        foreach (string site in unplaced)
            if (free.MoveNext()) cells[site] = free.Current;

        return cells;
    }

    /// <summary>
    /// The cells not taken, by rows.
    /// </summary>
    static IEnumerable<(int, int)> FreeCells(HashSet<(int, int)> taken)
    {
        for (int row = 0; row < HaulingNetwork.GridRows; row++)
            for (int column = 0; column < HaulingNetwork.GridColumns; column++)
                if (!taken.Contains((column, row))) yield return (column, row);
    }

    /// <summary>
    /// The wire from the card in <paramref name="from"/> to the card in <paramref name="to"/>, on cells <paramref name="cellWidth"/> by
    /// <paramref name="cellHeight"/> whose cards sit <paramref name="inset"/> inside them: out of the source's right side and into the target's
    /// left side, along the gaps between the cards when the target is not further right; <paramref name="thickness"/> pixels wide.
    /// </summary>
    public static List<WireSegment> Route((int Column, int Row) from, (int Column, int Row) to, float cellWidth, float cellHeight, float inset,
        float thickness)
    {
        float x1 = from.Column * cellWidth + cellWidth - inset, y1 = from.Row * cellHeight + cellHeight / 2;
        float x2 = to.Column * cellWidth + inset, y2 = to.Row * cellHeight + cellHeight / 2;
        if (to.Column > from.Column)
        {
            float middle = (to.Column * cellWidth + inset + from.Column * cellWidth + cellWidth - inset) / 2;
            return Path(thickness, (x1, y1), (middle, y1), (middle, y2), (x2, y2));
        }

        // Back or in the same column: out right into the gap, along the row gap above the target (below the source when it is in the same row),
        // then left of the target and in.
        float outX = x1 + inset / 2, inX = x2 - inset / 2;
        float gapY = to.Row != from.Row ? to.Row * cellHeight + inset / 2 : from.Row * cellHeight + cellHeight - inset / 2;
        return Path(thickness, (x1, y1), (outX, y1), (outX, gapY), (inX, gapY), (inX, y2), (x2, y2));
    }

    /// <summary>
    /// The segments joining <paramref name="points"/> in order, each axis-aligned.
    /// </summary>
    static List<WireSegment> Path(float thickness, params (float X, float Y)[] points)
    {
        var segments = new List<WireSegment>();
        for (int i = 1; i < points.Length; i++)
        {
            var (a, b) = (points[i - 1], points[i]);
            float left = Math.Min(a.X, b.X) - thickness / 2, top = Math.Min(a.Y, b.Y) - thickness / 2;
            segments.Add(new WireSegment(left, top, Math.Abs(a.X - b.X) + thickness, Math.Abs(a.Y - b.Y) + thickness));
        }

        return segments;
    }
}
