using System.Text.RegularExpressions;
using Storage.Filters;

namespace Storage.Network;

/// <summary>
/// The network inventory's search (docs/storage.md "Network inventory"): plain text (any part of the name, ignoring case) or a regular
/// expression, over the item names or the container names, and the matches in display order.
/// </summary>
public static class InventorySearch
{
    /// <summary>
    /// How long one regular expression may take on one name.
    /// </summary>
    static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// The lines that match, by category, sub-category, item name and container name; <c>Error</c> says why a regular expression could not run
    /// (then no line matches).
    /// </summary>
    public static (List<InventoryLine> Lines, string? Error) Match(IEnumerable<InventoryLine> lines, string text, bool regex, bool byContainer)
    {
        var ordered = Ordered(lines);
        if (text.Length == 0) return (ordered, null);
        if (!regex) return ([.. ordered.Where(l => Name(l, byContainer).Contains(text, StringComparison.OrdinalIgnoreCase))], null);

        try
        {
            var pattern = new Regex(text, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
            return ([.. ordered.Where(l => pattern.IsMatch(Name(l, byContainer)))], null);
        }
        catch (ArgumentException)
        {
            return ([], "Not a valid regular expression.");
        }
        catch (RegexMatchTimeoutException)
        {
            return ([], "The regular expression takes too long.");
        }
    }

    /// <summary>
    /// The name searched: the item's or its container's.
    /// </summary>
    static string Name(InventoryLine line, bool byContainer) => byContainer ? line.SiteName : line.Name;

    /// <summary>
    /// The lines in display order.
    /// </summary>
    static List<InventoryLine> Ordered(IEnumerable<InventoryLine> lines) =>
    [
        .. lines
            .OrderBy(l => ItemCatalog.GroupIndex(l.Group))
            .ThenBy(l => l.Sub, StringComparer.Ordinal)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.SiteName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.Site, StringComparer.Ordinal),
    ];
}
