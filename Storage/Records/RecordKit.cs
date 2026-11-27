using Meitou.Data;
using Meitou.Data.Fcs;

namespace Storage.Records;

/// <summary>
/// Copying a merged record into a new one and linking records, for the data phase.
/// </summary>
static class RecordKit
{
    /// <summary>
    /// A new record, as a mod file adds one.
    /// </summary>
    public static FcsRecord New(string id, FcsRecordType type, string name) => new() { StringId = id, Flags = 0x10, Name = name, RecordType = type };

    /// <summary>
    /// A copy of <paramref name="model"/> under a new id and name: every field, and every link but those named in <paramref name="skip"/>.
    /// </summary>
    public static FcsRecord Copy(GameRecord model, string id, string name, params string[] skip)
    {
        var record = New(id, model.Type, name);
        foreach (var (key, value) in model.Bools) record.Bools[key] = value;
        foreach (var (key, value) in model.Floats) record.Floats[key] = value;
        foreach (var (key, value) in model.Ints) record.Ints[key] = value;
        foreach (var (key, value) in model.Vector3S) record.Vector3S[key] = value;
        foreach (var (key, value) in model.Vector4S) record.Vector4S[key] = value;
        foreach (var (key, value) in model.Strings) record.Strings[key] = value;
        foreach (var (key, value) in model.Filenames) record.Filenames[key] = value;
        foreach (string list in model.ReferenceLists)
        {
            if (skip.Contains(list)) continue;

            record.References[list] = [.. model.GetReferences(list).Select(Link)];
        }

        return record;
    }

    /// <summary>
    /// Sets <paramref name="list"/> to links to <paramref name="targets"/>, each with the values (v0, v1).
    /// </summary>
    public static void Links(FcsRecord record, string list, params (string Target, int V0, int V1)[] targets) =>
        record.References[list] = [.. targets.Select(t => new FcsReference(t.Target, t.V0, t.V1, 0))];

    /// <summary>
    /// A merged link as a file's.
    /// </summary>
    public static FcsReference Link(GameReference link) => new(link.TargetStringId, link.Values.Value0, link.Values.Value1, link.Values.Value2);
}
