using Meitou.Simulation;

namespace Storage.Network;

/// <summary>
/// How a node shares what it sends between its links.
/// </summary>
public enum SplitMode : byte
{
    /// <summary>
    /// Each unit goes down the first link (in the player's order) whose target takes it and has room.
    /// </summary>
    InOrder,

    /// <summary>
    /// The units take the node's links in turn: with two links every odd unit goes down the first and every even unit down the second (with
    /// three, one in three each). A unit waits while the link whose turn it is cannot take it, so the split stays exact.
    /// </summary>
    Alternate,
}

/// <summary>
/// One link of a hauling network: units go from <see cref="From"/> to <see cref="To"/> (site ids), only units both ends can hold
/// (<see cref="ItemSet"/>).
/// </summary>
public sealed record NetworkLink(string From, string To);

/// <summary>
/// A node's settings: whether it is in the network, how it splits what it sends, its turn counter for <see cref="SplitMode.Alternate"/>, and its
/// cell on the network grid (the Hauling network tab's layout).
/// </summary>
public sealed class NodeSettings
{
    /// <summary>
    /// Whether the node is in the network: only active nodes' links are worked.
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// How the node shares what it sends.
    /// </summary>
    public SplitMode Split { get; set; }

    /// <summary>
    /// Units sent so far, for <see cref="SplitMode.Alternate"/>: the next unit takes link <c>Turn % links</c>.
    /// </summary>
    public long Turn { get; set; }

    /// <summary>
    /// The node's column on the grid; -1 until placed (the tab places it).
    /// </summary>
    public int Column { get; set; } = -1;

    /// <summary>
    /// The node's row on the grid; -1 until placed.
    /// </summary>
    public int Row { get; set; } = -1;

    /// <summary>
    /// A copy.
    /// </summary>
    public NodeSettings Copy() => new() { Active = Active, Split = Split, Turn = Turn, Column = Column, Row = Row };
}

/// <summary>
/// One storehouse's hauling network (docs/storage.md "Hauling network"): the storages and machines in its range the player activated, and the
/// links between them. The storehouse itself is always active. Links run in the player's order; a node may send down several links (to several
/// targets, in different directions) and receive from several. Changed only in the system's serial steps; the interface reads a copy.
/// </summary>
public sealed class HaulingNetwork(string storehouse)
{
    /// <summary>
    /// The most grid columns and rows.
    /// </summary>
    public const int GridColumns = 10, GridRows = 12;

    /// <summary>
    /// The most links one network holds.
    /// </summary>
    public const int MaxLinks = 200;

    /// <summary>
    /// The settings of each node the player touched, by site id.
    /// </summary>
    readonly SortedDictionary<string, NodeSettings> _nodes = new(StringComparer.Ordinal);

    /// <summary>
    /// The links, in the player's order.
    /// </summary>
    readonly List<NetworkLink> _links = [];

    /// <summary>
    /// The storehouse's site id.
    /// </summary>
    public string Storehouse { get; } = storehouse;

    /// <summary>
    /// The links, in order.
    /// </summary>
    public IReadOnlyList<NetworkLink> Links => _links;

    /// <summary>
    /// The settings of the nodes the player touched, by site id.
    /// </summary>
    public IReadOnlyDictionary<string, NodeSettings> Nodes => _nodes;

    /// <summary>
    /// A node's settings, made the first time.
    /// </summary>
    public NodeSettings Node(string site)
    {
        if (_nodes.TryGetValue(site, out var settings)) return settings;

        settings = new NodeSettings { Active = site == Storehouse };
        _nodes[site] = settings;
        return settings;
    }

    /// <summary>
    /// Whether a node is in the network.
    /// </summary>
    public bool IsActive(string site) => site == Storehouse || _nodes.TryGetValue(site, out var n) && n.Active;

    /// <summary>
    /// Puts a node in or out of the network; the storehouse stays in. Taking a node out keeps its links (they wait until it is back).
    /// </summary>
    public void SetActive(string site, bool active)
    {
        if (site == Storehouse) return;

        Node(site).Active = active;
    }

    /// <summary>
    /// Adds a link at the end; false (nothing added) for a link to itself, one already there, or a full network.
    /// </summary>
    public bool Link(string from, string to)
    {
        if (from == to || _links.Count >= MaxLinks || _links.Contains(new NetworkLink(from, to))) return false;

        _links.Add(new NetworkLink(from, to));
        return true;
    }

    /// <summary>
    /// Removes a link; false when it was not there.
    /// </summary>
    public bool Unlink(string from, string to) => _links.Remove(new NetworkLink(from, to));

    /// <summary>
    /// Moves a link one place earlier (<paramref name="step"/> -1) or later (+1) among its source's links.
    /// </summary>
    public void Move(string from, string to, int step)
    {
        int at = _links.IndexOf(new NetworkLink(from, to));
        if (at < 0) return;

        int other = step < 0
            ? (at == 0 ? -1 : _links.FindLastIndex(at - 1, l => l.From == from))
            : _links.FindIndex(at + 1, l => l.From == from);
        if (other < 0 || other == at) return;

        (_links[at], _links[other]) = (_links[other], _links[at]);
    }

    /// <summary>
    /// The active links a node sends down, in order: both ends active.
    /// </summary>
    public IEnumerable<NetworkLink> Out(string from) => _links.Where(l => l.From == from && IsActive(l.From) && IsActive(l.To));

    /// <summary>
    /// Every active link, in order.
    /// </summary>
    public IEnumerable<NetworkLink> ActiveLinks => _links.Where(l => IsActive(l.From) && IsActive(l.To));

    /// <summary>
    /// Places a node on the grid (clamped to it).
    /// </summary>
    public void Place(string site, int column, int row)
    {
        var node = Node(site);
        node.Column = Math.Clamp(column, 0, GridColumns - 1);
        node.Row = Math.Clamp(row, 0, GridRows - 1);
    }

    /// <summary>
    /// Forgets nodes gone from the world (dismantled, destroyed): their settings and their links.
    /// </summary>
    public void Forget(IReadOnlySet<string> present)
    {
        foreach (string site in _nodes.Keys.Where(s => s != Storehouse && !present.Contains(s)).ToList()) _nodes.Remove(site);
        _links.RemoveAll(l => !present.Contains(l.From) || !present.Contains(l.To));
    }

    /// <summary>
    /// Adds a read link as it is (a loaded save's).
    /// </summary>
    internal void Load(NetworkLink link) => _links.Add(link);

    /// <summary>
    /// Sets a read node as it is.
    /// </summary>
    internal void Load(string site, NodeSettings settings) => _nodes[site] = settings;

    /// <summary>
    /// A copy, for the interface.
    /// </summary>
    public HaulingNetwork Copy()
    {
        var copy = new HaulingNetwork(Storehouse);
        foreach (var (site, node) in _nodes) copy.Load(site, node.Copy());
        foreach (var link in _links) copy.Load(link);
        return copy;
    }

    /// <summary>
    /// Adds the network to the state hash.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(Rng.StableHash(Storehouse));
        foreach (var (site, node) in _nodes)
        {
            hasher.Add(Rng.StableHash(site));
            hasher.Add(node.Active);
            hasher.Add((int)node.Split);
            hasher.Add(node.Turn);
            hasher.Add(node.Column);
            hasher.Add(node.Row);
        }

        foreach (var link in _links)
        {
            hasher.Add(Rng.StableHash(link.From));
            hasher.Add(Rng.StableHash(link.To));
        }
    }
}
