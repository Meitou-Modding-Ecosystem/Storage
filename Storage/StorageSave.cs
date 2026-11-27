using System.Text;
using Meitou.Simulation.Items;
using Storage.Filters;
using Storage.Network;

namespace Storage;

/// <summary>
/// The mod's save (docs/storage.md "Save"): per storehouse, its filter levels, its network's node settings and links, in the mod's sidecar file.
/// Version 1: a count of storehouses, then each storehouse's site id, its filter entries (scope, key, level), its nodes (site, active, split,
/// turn, column, row) and its links (from, to).
/// </summary>
public static class StorageSave
{
    /// <summary>
    /// The format version written.
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// Writes the storehouses' settings.
    /// </summary>
    public static void Write(Stream stream, IEnumerable<(string Site, StorehouseState State)> storehouses)
    {
        using var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var list = storehouses.ToList();
        w.Write(Version);
        w.Write(list.Count);
        foreach (var (site, state) in list)
        {
            w.Write(site);
            WriteFilter(w, state.Filter);
            WriteNetwork(w, state.Network);
        }
    }

    /// <summary>
    /// Reads the storehouses' settings; none for a newer version than this one reads.
    /// </summary>
    public static List<StorehouseState> Read(Stream stream)
    {
        using var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var states = new List<StorehouseState>();
        if (r.ReadInt32() > Version) return states;

        int count = r.ReadInt32();
        for (int k = 0; k < count; k++)
        {
            var state = new StorehouseState(r.ReadString());
            ReadFilter(r, state.Filter);
            ReadNetwork(r, state.Network);
            states.Add(state);
        }

        return states;
    }

    /// <summary>
    /// The filter's levels.
    /// </summary>
    static void WriteFilter(BinaryWriter w, StorehouseFilter filter)
    {
        var entries = filter.Entries().ToList();
        w.Write(entries.Count);
        foreach (var (scope, key, level) in entries)
        {
            w.Write((byte)scope);
            w.Write(key);
            w.Write((sbyte)level);
        }
    }

    /// <summary>
    /// Reads a filter's levels.
    /// </summary>
    static void ReadFilter(BinaryReader r, StorehouseFilter filter)
    {
        int count = r.ReadInt32();
        for (int k = 0; k < count; k++)
        {
            var scope = (FilterScope)r.ReadByte();
            string key = r.ReadString();
            filter.Load(scope, key, (StoragePriority)r.ReadSByte());
        }
    }

    /// <summary>
    /// The network's nodes and links.
    /// </summary>
    static void WriteNetwork(BinaryWriter w, HaulingNetwork network)
    {
        w.Write(network.Nodes.Count);
        foreach (var (site, node) in network.Nodes)
        {
            w.Write(site);
            w.Write(node.Active);
            w.Write((byte)node.Split);
            w.Write(node.Turn);
            w.Write(node.Column);
            w.Write(node.Row);
        }

        w.Write(network.Links.Count);
        foreach (var link in network.Links)
        {
            w.Write(link.From);
            w.Write(link.To);
        }
    }

    /// <summary>
    /// Reads a network's nodes and links.
    /// </summary>
    static void ReadNetwork(BinaryReader r, HaulingNetwork network)
    {
        int nodes = r.ReadInt32();
        for (int k = 0; k < nodes; k++)
        {
            string site = r.ReadString();
            var node = new NodeSettings { Active = r.ReadBoolean(), Split = (SplitMode)r.ReadByte(), Turn = r.ReadInt64(), Column = r.ReadInt32() };
            node.Row = r.ReadInt32();
            network.Load(site, node);
        }

        int links = r.ReadInt32();
        for (int k = 0; k < links; k++) network.Load(new NetworkLink(r.ReadString(), r.ReadString()));
    }
}
