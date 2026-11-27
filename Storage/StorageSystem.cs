using Meitou.Data.Gameplay.Ai;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Items;
using Storage.Filters;
using Storage.Hauling;
using Storage.Network;
using Storage.Records;

namespace Storage;

/// <summary>
/// One storehouse's player settings: its storage filters and its hauling network.
/// </summary>
public sealed class StorehouseState(string site)
{
    /// <summary>
    /// The storage filters.
    /// </summary>
    public StorehouseFilter Filter { get; } = new();

    /// <summary>
    /// The hauling network.
    /// </summary>
    public HaulingNetwork Network { get; } = new(site);
}

/// <summary>
/// The mod's system (docs/storage.md): each storehouse's filters and network, the scans of its range, the haulers' errands, the game's storage
/// searches kept to the filters, the views the window reads, and the save.
/// </summary>
public sealed class StorageSystem(ScanData data) : ITickSystem, IStateHashed, IModSaveData, IChooserOwner, IStorageFilter, IStoragePriority
{
    #region State

    /// <summary>
    /// Ticks between scans and views (two game seconds at 30 ticks a second).
    /// </summary>
    public const int ScanEveryTicks = 60;

    /// <summary>
    /// Ticks between the haulers' steps.
    /// </summary>
    public const int StepEveryTicks = 10;

    /// <summary>
    /// Each storehouse's settings, by site id.
    /// </summary>
    readonly SortedDictionary<string, StorehouseState> _storehouses = new(StringComparer.Ordinal);

    /// <summary>
    /// The last scan of each storehouse, by site id.
    /// </summary>
    readonly Dictionary<string, ScanResult> _scans = new(StringComparer.Ordinal);

    /// <summary>
    /// The storehouses to scan at the next step (a Rescan press, a change of the network).
    /// </summary>
    readonly HashSet<string> _rescan = new(StringComparer.Ordinal);

    /// <summary>
    /// The views the window reads, replaced whole by each publish.
    /// </summary>
    volatile IReadOnlyDictionary<string, StorehouseView> _views = new Dictionary<string, StorehouseView>();

    /// <summary>
    /// The item catalog.
    /// </summary>
    public ItemCatalog Catalog => data.Catalog;

    /// <summary>
    /// The published views, by storehouse site id.
    /// </summary>
    public IReadOnlyDictionary<string, StorehouseView> Views => _views;

    /// <summary>
    /// A storehouse's settings, made the first time.
    /// </summary>
    public StorehouseState State(string site)
    {
        if (!_storehouses.TryGetValue(site, out var state)) _storehouses[site] = state = new StorehouseState(site);

        return state;
    }

    #endregion

    #region Ticking

    public void Register(ChooserRegistry chooser) => chooser.Jobs(HaulerJobs.Type, Haulers);

    /// <summary>
    /// The haulers' contributor, made with the system.
    /// </summary>
    Haulers Haulers => _haulersOnce ??= new Haulers(Plan, Turned);

    /// <summary>
    /// The haulers' contributor once made.
    /// </summary>
    Haulers? _haulersOnce;

    public void Inputs(World world, IReadOnlyList<SimCommand> commands)
    {
        foreach (var command in commands) Apply(world, command);
    }

    /// <summary>
    /// One of the window's or the panel's commands.
    /// </summary>
    void Apply(World world, SimCommand command)
    {
        switch (command)
        {
            case SetFilterCommand f when IsStorehouse(world, f.Storehouse):
                State(f.Storehouse).Filter.Put(f.Scope, f.Key, f.Level, data.Catalog);
                break;
            case SetNodeActiveCommand a when IsStorehouse(world, a.Storehouse):
                State(a.Storehouse).Network.SetActive(a.Node, a.Active);
                _rescan.Add(a.Storehouse);
                break;
            case SetSplitCommand s when IsStorehouse(world, s.Storehouse):
                State(s.Storehouse).Network.Node(s.Node).Split = s.Split;
                break;
            case PlaceNodeCommand p when IsStorehouse(world, p.Storehouse):
                State(p.Storehouse).Network.Place(p.Node, p.Column, p.Row);
                break;
            case LinkCommand l when IsStorehouse(world, l.Storehouse):
                ApplyLink(State(l.Storehouse).Network, l);
                _rescan.Add(l.Storehouse);
                break;
            case RescanCommand r: _rescan.Add(r.Storehouse); break;
            case AssignHaulersCommand h when IsStorehouse(world, h.Storehouse): HaulerJobs.Assign(world, h.Storehouse); break;
            case GoToStorageCommand g: GoTo(world, g); break;
        }
    }

    /// <summary>
    /// Adds, removes or moves a link; an added link puts both its ends in the network.
    /// </summary>
    static void ApplyLink(HaulingNetwork network, LinkCommand link)
    {
        if (link.Step != 0)
        {
            network.Move(link.From, link.To, link.Step);
            return;
        }

        if (!link.Add)
        {
            network.Unlink(link.From, link.To);
            return;
        }

        if (!network.Link(link.From, link.To)) return;

        network.SetActive(link.From, true);
        network.SetActive(link.To, true);
    }

    /// <summary>
    /// A move order for the character to the storage's door side (the game opens its window when the player clicks it there).
    /// </summary>
    static void GoTo(World world, GoToStorageCommand go)
    {
        if (world.Buildings.Find(go.Site) is not { } site) return;
        if (!world.Characters.TryResolveNext(go.Who, out int slot) || world.Characters.Cold(slot) is not { IsPlayer: true }) return;

        world.GiveOrder(go.Who, TaskType.MoveCusOrdered, point: site.Position);
    }

    public void SlowWorld(World world)
    {
        if (world.Tick % StepEveryTicks != 0) return;

        bool scan = world.Tick % ScanEveryTicks == 0;
        if (scan) Check(world);
        else ScanAsked(world);

        Haulers.Step(world);
        if (scan || _rescan.Count > 0) Publish(world);
        _rescan.Clear();
    }

    /// <summary>
    /// The full check: storehouses gone are forgotten, the hauler cap is kept, every storehouse is scanned.
    /// </summary>
    void Check(World world)
    {
        var present = Storehouses(world).ToDictionary(s => s.Id, StringComparer.Ordinal);
        foreach (string gone in _storehouses.Keys.Where(k => !present.ContainsKey(k)).ToList()) _storehouses.Remove(gone);
        foreach (string gone in _scans.Keys.Where(k => !present.ContainsKey(k)).ToList()) _scans.Remove(gone);

        HaulerJobs.Cap(world);
        foreach (var site in present.Values) ScanOne(world, site);
    }

    /// <summary>
    /// Scans the storehouses asked for since the last step.
    /// </summary>
    void ScanAsked(World world)
    {
        foreach (string id in _rescan)
            if (world.Buildings.Find(id) is { } site && IsStorehouse(world, id)) ScanOne(world, site);
    }

    /// <summary>
    /// Scans one storehouse and forgets the nodes of its network no longer in range.
    /// </summary>
    void ScanOne(World world, BuildSite site)
    {
        var state = State(site.Id);
        var scan = NetworkScan.Scan(world, site, state.Network, data, StorageSettings.Current.GatherGround);
        state.Network.Forget(new HashSet<string>(scan.Nodes.Keys, StringComparer.Ordinal));
        _scans[site.Id] = scan;
    }

    /// <summary>
    /// The player's finished storehouses, in site order.
    /// </summary>
    static IEnumerable<BuildSite> Storehouses(World world) =>
        world.Buildings.All.Where(s => s.Record == Ids.Storehouse && s.Complete && !s.Destroyed && s.Owner == world.Player.Faction);

    /// <summary>
    /// Whether a site is one of the player's finished storehouses.
    /// </summary>
    static bool IsStorehouse(World world, string site) =>
        world.Buildings.Find(site) is { Record: Ids.Storehouse, Complete: true, Destroyed: false } s && s.Owner == world.Player.Faction;

    #endregion

    #region Haulers

    /// <summary>
    /// The planner's input for a storehouse's haulers; null before its first scan.
    /// </summary>
    PlanInput? Plan(World world, string storehouse)
    {
        if (!_scans.TryGetValue(storehouse, out var scan) || !_storehouses.TryGetValue(storehouse, out var state)) return null;

        return new PlanInput(state.Network, scan.Nodes, state.Filter, data.Catalog, Haulers.Claimed(), scan.Gather);
    }

    /// <summary>
    /// An alternating source sent units: its turn moves on.
    /// </summary>
    void Turned(string storehouse, string source, long units)
    {
        if (_storehouses.TryGetValue(storehouse, out var state)) state.Network.Node(source).Turn += units;
    }

    #endregion

    #region Game searches

    /// <summary>
    /// Keeps the game's own put searches (its haulers, stashes, ditch trips) out of a storehouse for the items its filters forbid.
    /// </summary>
    public bool Allows(World world, CharacterId who, string storage, StorageUse use, string? record) =>
        use != StorageUse.Put || record is null || !_storehouses.TryGetValue(storage, out var state) || state.Filter.Allows(record, data.Catalog);

    /// <summary>
    /// Ranks a storehouse in the game's own put searches by its filter tier for the item.
    /// </summary>
    public StoragePriority Priority(World world, CharacterId who, string storage, StorageUse use, string? record) =>
        use == StorageUse.Put && record is not null && _storehouses.TryGetValue(storage, out var state)
            ? state.Filter.Level(record, data.Catalog)
            : StoragePriority.Normal;

    #endregion

    #region Views

    /// <summary>
    /// Publishes a view of every scanned storehouse.
    /// </summary>
    void Publish(World world)
    {
        var views = new Dictionary<string, StorehouseView>(StringComparer.Ordinal);
        foreach (var (id, scan) in _scans)
        {
            if (world.Buildings.Find(id) is not { } site || !_storehouses.TryGetValue(id, out var state)) continue;

            string name = NetworkScan.NameOf(site, data.BuildingName);
            views[id] = new StorehouseView(id, name, state.Filter.Copy(), state.Network.Copy(), [.. scan.Nodes.Values], scan.Inventory,
                HaulerLines(world, id), world.Tick);
        }

        _views = views;
    }

    /// <summary>
    /// The storehouse's haulers and what each is doing.
    /// </summary>
    List<HaulerLine> HaulerLines(World world, string storehouse)
    {
        var busy = Haulers.At(storehouse).ToDictionary(e => e.Who, e => e);
        var lines = new List<HaulerLine>();
        foreach (int slot in HaulerJobs.HoldersAt(world, storehouse))
        {
            var who = world.Characters.IdOf(slot);
            string name = world.Characters.Cold(slot)?.Name ?? "Hauler";
            string doing = busy.TryGetValue(who, out var e) ? Doing(e.Trip, e.Picked) : "Waiting for work";
            lines.Add(new HaulerLine(name, doing));
        }

        return lines;
    }

    /// <summary>
    /// A hauler's errand in words.
    /// </summary>
    string Doing(Trip trip, bool picked)
    {
        string item = data.Catalog.Find(trip.Record)?.Name ?? trip.Record;
        return picked ? $"Carrying {trip.Count} {item}" : $"Fetching {trip.Count} {item}";
    }

    #endregion

    #region Save and hash

    public void Hash(ref StateHasher hasher)
    {
        foreach (var (site, state) in _storehouses)
        {
            hasher.Add(Rng.StableHash(site));
            state.Filter.Hash(ref hasher);
            state.Network.Hash(ref hasher);
        }

        Haulers.Hash(ref hasher);
    }

    public void Write(Stream stream) => StorageSave.Write(stream, _storehouses.Values.Select(s => (s.Network.Storehouse, s)));

    public void Read(Stream stream)
    {
        _storehouses.Clear();
        foreach (var state in StorageSave.Read(stream)) _storehouses[state.Network.Storehouse] = state;
    }

    #endregion
}
