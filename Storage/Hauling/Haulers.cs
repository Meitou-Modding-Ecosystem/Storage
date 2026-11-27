using System.Numerics;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Movement;
using Meitou.Simulation.Production;
using Storage.Network;

namespace Storage.Hauling;

/// <summary>
/// One stop of a hauler's errand: where to go, and what is done on arrival (false: the errand is called off).
/// </summary>
public sealed record Stop(Vector3 Point, Func<World, int, bool> Done);

/// <summary>
/// A hauler's errand: the job it serves (its index in the job list), its storehouse, its trip, its stops left, and when the work at the current stop
/// ends (NaN while walking). Not saved: tasks are not, so a loaded hauler plans again; what it carries stays in its inventory and its next errand
/// puts it away first.
/// </summary>
public sealed class Errand(int job, string storehouse, Trip trip)
{
    /// <summary>
    /// The job's index in the character's job list.
    /// </summary>
    public int Job { get; } = job;

    /// <summary>
    /// The storehouse the job is at.
    /// </summary>
    public string Storehouse { get; } = storehouse;

    /// <summary>
    /// The trip, cut to what was picked once picked.
    /// </summary>
    public Trip Trip { get; set; } = trip;

    /// <summary>
    /// Whether the units are picked up (the claim on them ends).
    /// </summary>
    public bool Picked { get; set; }

    /// <summary>
    /// The stops left, the current one first.
    /// </summary>
    public Queue<Stop> Stops { get; } = [];

    /// <summary>
    /// The game hour the work at the current stop ends; NaN while walking.
    /// </summary>
    public double WorkUntil { get; set; } = double.NaN;
}

/// <summary>
/// The storehouse hauler job's contributor (docs/storage.md "Haulers"): for each listed hauler job the chooser asks whether there is a trip
/// (<see cref="HaulPlanner"/>), starts it on the job's row and the system's serial step walks it on: pick up, then each drop-off, then anything
/// left over back into the storehouse.
/// </summary>
public sealed class Haulers(Func<World, string, PlanInput?> plans, Action<string, string, long> turned) : IJobSource
{
    #region Errands

    /// <summary>
    /// How near (ground plane) to a stop counts as there, in world units.
    /// </summary>
    public const float Reach = 30;

    /// <summary>
    /// Game hours the hauler works at each stop (picking up or putting down).
    /// </summary>
    public const double Handling = 0.03;

    /// <summary>
    /// Each hauler's errand.
    /// </summary>
    readonly SortedDictionary<CharacterId, Errand> _errands = new(Comparer<CharacterId>.Create(Compare));

    /// <summary>
    /// The records each hauler carries for its job (picked up and not yet all put down), with their record types: what a later errand puts away
    /// first, so its own things stay with it.
    /// </summary>
    readonly SortedDictionary<CharacterId, SortedDictionary<string, FcsRecordType>> _carried = new(Comparer<CharacterId>.Create(Compare));

    /// <summary>
    /// The errands under way, for the debug row and the window.
    /// </summary>
    public int Busy => _errands.Count;

    /// <summary>
    /// The units each errand not yet picked up holds claimed, by (place key, record), so two haulers do not go for the same units.
    /// </summary>
    public IReadOnlyDictionary<(string Place, string Record), int> Claimed()
    {
        var claimed = new Dictionary<(string, string), int>();
        foreach (var errand in _errands.Values.Where(e => !e.Picked))
        {
            var key = (errand.Trip.From.Key, errand.Trip.Record);
            claimed[key] = claimed.GetValueOrDefault(key) + errand.Trip.Count;
        }

        return claimed;
    }

    /// <summary>
    /// What each hauler of a storehouse is doing, for the window: (character, record, units, picked).
    /// </summary>
    public IEnumerable<(CharacterId Who, Trip Trip, bool Picked)> At(string storehouse) =>
        _errands.Where(e => e.Value.Storehouse == storehouse).Select(e => (e.Key, e.Value.Trip, e.Value.Picked));

    /// <summary>
    /// A stable order of character ids.
    /// </summary>
    static int Compare(CharacterId a, CharacterId b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : a.Generation.CompareTo(b.Generation);

    #endregion

    #region Chooser

    /// <summary>
    /// Runs while the character works this job's errand; else whether there is something to put away or a trip to make.
    /// </summary>
    public DecisionCheck CanRun(World world, int slot, int k)
    {
        if (JobAt(world, slot, k) is not { Site: { } storehouse } job || job.Type != HaulerJobs.Type) return DecisionCheck.No(null);
        if (_errands.TryGetValue(world.Characters.IdOf(slot), out var errand) && errand.Job == k) return DecisionCheck.Runs;

        return Next(world, slot, storehouse) is not null ? DecisionCheck.Run : DecisionCheck.No(null);
    }

    /// <summary>
    /// Plans the job's errand and walks to its first stop; an errand under way for it goes on.
    /// </summary>
    public void Start(World world, int slot, int k)
    {
        var who = world.Characters.IdOf(slot);
        if (_errands.TryGetValue(who, out var current) && current.Job == k)
        {
            Resume(world, slot, current);
            return;
        }

        if (JobAt(world, slot, k) is not { Site: { } storehouse } || Next(world, slot, storehouse) is not { } trip) return;

        var errand = new Errand(k, storehouse, trip);
        Lay(world, errand);
        _errands[who] = errand;
        Walk(world, slot, errand);
    }

    /// <summary>
    /// The job can no longer run: its errand ends.
    /// </summary>
    public void Stop(World world, int slot, int k)
    {
        var who = world.Characters.IdOf(slot);
        if (!_errands.TryGetValue(who, out var errand) || errand.Job != k) return;

        _errands.Remove(who);
        TaskStart.Stand(world.Characters, slot);
    }

    /// <summary>
    /// The hauler's next trip: units it carries put away first, else the planner's trip.
    /// </summary>
    Trip? Next(World world, int slot, string storehouse)
    {
        if (plans(world, storehouse) is not { } input || world.Characters.Cold(slot)?.Inventory is not { } carried) return null;

        return PutAway(input, carried, Carried(world.Characters.IdOf(slot))) ?? HaulPlanner.Plan(input, world.Characters.Next[slot].Position, HaulPlanner.MaxUnits);
    }

    /// <summary>
    /// A trip that puts the units of the storehouse's filter-allowed records the hauler carries (left over from an earlier trip) into the
    /// storehouse; null when it carries none or the storehouse has no room.
    /// </summary>
    static Trip? PutAway(PlanInput input, Meitou.Simulation.Items.Inventory carried, IEnumerable<KeyValuePair<string, FcsRecordType>> records)
    {
        if (!input.Nodes.TryGetValue(input.Network.Storehouse, out var storehouse)) return null;

        foreach (var (record, type) in records)
        {
            int count = Carrying.Count(carried, record);
            int room = storehouse.RoomFor(record, type);
            if (count <= 0 || room <= 0) continue;

            var here = new PickPlace(PickKind.Node, "", 0, storehouse.Position);
            return new Trip(here, record, type, Math.Min(count, room), [new Drop(storehouse.Site, Math.Min(count, room))]);
        }

        return null;
    }

    #endregion

    #region Stepping

    /// <summary>
    /// The serial step: each errand whose character left its row (an order, a fight) is dropped; an arrived one works; finished work moves on.
    /// </summary>
    public void Step(World world)
    {
        Tidy(world);
        foreach (var (who, errand) in _errands.ToList())
        {
            if (!world.Characters.TryResolveNext(who, out int slot) || !OnRow(world, slot))
            {
                _errands.Remove(who);
                continue;
            }

            StepOne(world, slot, who, errand);
        }
    }

    /// <summary>
    /// One errand: work on arrival, move on when the work is done.
    /// </summary>
    void StepOne(World world, int slot, CharacterId who, Errand errand)
    {
        var stop = errand.Stops.Peek();
        if (double.IsNaN(errand.WorkUntil))
        {
            if (!Arrived(world, slot, stop.Point)) return;

            errand.WorkUntil = world.GameHours + Handling;
            Work(world, slot, errand);
        }

        if (world.GameHours < errand.WorkUntil) return;

        errand.Stops.Dequeue();
        errand.WorkUntil = double.NaN;
        if (!stop.Done(world, slot) || errand.Stops.Count == 0) Finish(world, slot, who);
        else Walk(world, slot, errand);
    }

    /// <summary>
    /// Lays out an errand's stops: the pick-up (none for units already carried), each drop-off in order, and the storehouse for what is left.
    /// </summary>
    void Lay(World world, Errand errand)
    {
        var trip = errand.Trip;
        if (trip.From.Site.Length == 0 && trip.From.Kind == PickKind.Node) errand.Picked = true;
        else errand.Stops.Enqueue(new Stop(trip.From.Position, (w, s) => PickUp(w, s, errand)));

        foreach (var drop in trip.Drops)
        {
            var point = world.Buildings.Find(drop.Target)?.Position ?? trip.From.Position;
            errand.Stops.Enqueue(new Stop(point, (w, s) => DropOff(w, s, errand, drop.Target)));
        }
    }

    /// <summary>
    /// Picks the trip's units up and cuts the trip to what was picked; an alternating source's turn moves on by them. False when nothing was picked.
    /// </summary>
    bool PickUp(World world, int slot, Errand errand)
    {
        if (world.Characters.Cold(slot)?.Inventory is not { } carried) return false;

        var trip = errand.Trip;
        int picked = Carrying.Pick(world, carried, trip.From, trip.Record, trip.Count);
        errand.Picked = true;
        if (picked <= 0) return false;

        errand.Trip = trip.Cut(picked);
        Note(world.Characters.IdOf(slot), trip.Record, trip.Type);
        if (trip.Alternates) turned(errand.Storehouse, trip.From.Site, picked);

        return true;
    }

    /// <summary>
    /// Puts the units planned for a target into it. Carries on to the next stop even when the target took fewer (the rest goes on to the
    /// storehouse at the end, <see cref="PutAway"/>).
    /// </summary>
    bool DropOff(World world, int slot, Errand errand, string target)
    {
        if (world.Characters.Cold(slot)?.Inventory is not { } carried) return false;

        int planned = errand.Trip.Drops.Where(d => d.Target == target).Sum(d => d.Count);
        Carrying.Put(world, carried, target, errand.Trip.Record, planned);
        if (Carrying.Count(carried, errand.Trip.Record) == 0) Forget(world.Characters.IdOf(slot), errand.Trip.Record);

        return true;
    }

    /// <summary>
    /// Goes on with an errand: back to work at its stop, or on the walk there.
    /// </summary>
    static void Resume(World world, int slot, Errand errand)
    {
        if (double.IsNaN(errand.WorkUntil)) Walk(world, slot, errand);
        else Work(world, slot, errand);
    }

    /// <summary>
    /// Walks the hauler to its current stop on the job's row.
    /// </summary>
    static void Walk(World world, int slot, Errand errand)
    {
        var request = new TaskRequest(CharacterTask.GoTo, HaulerJobs.Type, TaskSource.Job) { Point = errand.Stops.Peek().Point, Job = errand.Job };
        TaskStart.Start(world, slot, request);
    }

    /// <summary>
    /// Stands the hauler working at its stop on the job's row.
    /// </summary>
    static void Work(World world, int slot, Errand errand) =>
        TaskStart.Start(world, slot, new TaskRequest(CharacterTask.Idle, HaulerJobs.Type, TaskSource.Job) { Job = errand.Job });

    /// <summary>
    /// Ends an errand: the hauler stands, and the chooser picks its next job (this one again when there is more work).
    /// </summary>
    void Finish(World world, int slot, CharacterId who)
    {
        _errands.Remove(who);
        TaskStart.Stand(world.Characters, slot);
        if (world.Characters.Cold(slot) is { } cold) DecisionSystem.Request(cold);
    }

    #endregion

    #region Carried

    /// <summary>
    /// The records a hauler carries for its job.
    /// </summary>
    IEnumerable<KeyValuePair<string, FcsRecordType>> Carried(CharacterId who) =>
        _carried.TryGetValue(who, out var records) ? records.ToList() : [];

    /// <summary>
    /// Notes that a hauler carries units of a record for its job.
    /// </summary>
    void Note(CharacterId who, string record, FcsRecordType type)
    {
        if (!_carried.TryGetValue(who, out var records)) _carried[who] = records = new(StringComparer.Ordinal);

        records[record] = type;
    }

    /// <summary>
    /// A record the hauler no longer carries for its job.
    /// </summary>
    void Forget(CharacterId who, string record)
    {
        if (!_carried.TryGetValue(who, out var records)) return;

        records.Remove(record);
        if (records.Count == 0) _carried.Remove(who);
    }

    /// <summary>
    /// Forgets the carried records of characters gone from the world, and those they no longer carry.
    /// </summary>
    void Tidy(World world)
    {
        foreach (var (who, records) in _carried.ToList())
        {
            if (!world.Characters.TryResolveNext(who, out int slot) || world.Characters.Cold(slot)?.Inventory is not { } inventory)
            {
                _carried.Remove(who);
                continue;
            }

            foreach (string record in records.Keys.Where(r => Carrying.Count(inventory, r) == 0).ToList()) Forget(who, record);
        }
    }

    /// <summary>
    /// Adds the errands and carried records to the state hash.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        foreach (var (who, errand) in _errands)
        {
            hasher.Add(who);
            hasher.Add(errand.Job);
            hasher.Add(Rng.StableHash(errand.Trip.Record));
            hasher.Add(errand.Trip.Count);
            hasher.Add(errand.Picked);
            hasher.Add(errand.Stops.Count);
            hasher.Add(errand.WorkUntil);
        }

        foreach (var (who, records) in _carried)
        {
            hasher.Add(who);
            foreach (string record in records.Keys) hasher.Add(Rng.StableHash(record));
        }
    }

    #endregion

    #region Reading

    /// <summary>
    /// The character's job <paramref name="k"/>; null past the list.
    /// </summary>
    static PermaJob? JobAt(World world, int slot, int k) =>
        world.Characters.Cold(slot)?.Jobs is { } jobs && (uint)k < (uint)jobs.Count ? jobs[k] : null;

    /// <summary>
    /// Whether the character's last start was the hauler row's, from a job.
    /// </summary>
    static bool OnRow(World world, int slot) =>
        world.Characters.Cold(slot)?.Running is { Source: TaskSource.Job } run && run.Row == HaulerJobs.Type;

    /// <summary>
    /// Whether the character stands at the point and has stopped walking.
    /// </summary>
    static bool Arrived(World world, int slot, Vector3 point)
    {
        ref readonly var hot = ref world.Characters.Next[slot];
        return NetworkScan.Flat(hot.Position, point) <= Reach && hot.Task != (byte)CharacterTask.GoTo;
    }

    #endregion
}
