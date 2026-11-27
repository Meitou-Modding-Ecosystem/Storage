using Meitou.Data.Fcs;

namespace Storage.Network;

/// <summary>
/// The items a node can ever send or take: any item, any item of one record type, or listed records (docs/storage.md "Compatible storages").
/// </summary>
public sealed class ItemSet
{
    /// <summary>
    /// Every item.
    /// </summary>
    public static readonly ItemSet Everything = new(null, null);

    /// <summary>
    /// No item.
    /// </summary>
    public static readonly ItemSet Nothing = new(null, new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// A set of the record type <paramref name="type"/> (null: any type) holding <paramref name="records"/> only (null: any of the type).
    /// </summary>
    ItemSet(FcsRecordType? type, IReadOnlySet<string>? records)
    {
        Type = type;
        Records = records;
    }

    /// <summary>
    /// The one record type it holds; null for any.
    /// </summary>
    public FcsRecordType? Type { get; }

    /// <summary>
    /// The only records it holds; null for any (of <see cref="Type"/>).
    /// </summary>
    public IReadOnlySet<string>? Records { get; }

    /// <summary>
    /// Any item of one record type.
    /// </summary>
    public static ItemSet OfType(FcsRecordType type) => new(type, null);

    /// <summary>
    /// The listed records only (none: <see cref="Nothing"/>).
    /// </summary>
    public static ItemSet Of(IEnumerable<string> records) => new(null, new HashSet<string>(records, StringComparer.Ordinal));

    /// <summary>
    /// Whether it holds an item of this record and type.
    /// </summary>
    public bool Contains(string record, FcsRecordType type) =>
        (Type is null || Type == type) && (Records is null || Records.Contains(record));

    /// <summary>
    /// Whether some item is in both sets; <paramref name="typeOf"/> gives a listed record's type (null: unknown, which matches any type).
    /// </summary>
    public bool Overlaps(ItemSet other, Func<string, FcsRecordType?> typeOf)
    {
        if (Type is { } a && other.Type is { } b && a != b) return false;
        if (Records is null && other.Records is null) return true;
        if (Records is null) return other.Records!.Any(r => FitsType(r, Type, typeOf));
        if (other.Records is null) return Records.Any(r => FitsType(r, other.Type, typeOf));

        return Records.Any(other.Records.Contains);
    }

    /// <summary>
    /// Whether a listed record is of the wanted type (null: any).
    /// </summary>
    static bool FitsType(string record, FcsRecordType? wanted, Func<string, FcsRecordType?> typeOf) =>
        wanted is null || typeOf(record) is not { } type || type == wanted;
}
