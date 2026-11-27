using Meitou.Data.Gameplay.Ai;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Production;
using Storage.Records;

namespace Storage.Hauling;

/// <summary>
/// The storehouse hauler job (docs/storage.md "Haulers"): its task type, its offer on the storehouse's right-click menu, giving it to the selected
/// characters from the panel, and the cap of <see cref="MaxHaulers"/> per storehouse. The job sits in the character's job list on the storehouse it
/// was given at; the jobs panel shows and removes it and the game saves it.
/// </summary>
public static class HaulerJobs
{
    /// <summary>
    /// The most haulers one storehouse takes.
    /// </summary>
    public const int MaxHaulers = 3;

    /// <summary>
    /// The job's task type.
    /// </summary>
    public static readonly TaskType Type = (TaskType)Ids.HaulerJob;

    /// <summary>
    /// The job's name in the jobs panel and on buttons.
    /// </summary>
    public const string Name = "Storehouse hauler";

    /// <summary>
    /// What a character the cap turns away says.
    /// </summary>
    public const string FullBark = "This storehouse already has three haulers.";

    /// <summary>
    /// The job's offer: the storehouse's right-click menu shows "Work as: Storehouse hauler" to selected people.
    /// </summary>
    public static ModJobOffer Offer() => new(Type, [Ids.Storehouse], "Work as: " + Name, ModJobTakers.People);

    /// <summary>
    /// The player's living characters holding the job at a storehouse, in slot order.
    /// </summary>
    public static List<int> HoldersAt(World world, string storehouse)
    {
        var holders = new List<int>();
        var table = world.Characters;
        for (int slot = 0; slot < table.HighWater; slot++)
        {
            if (table.Cold(slot) is not { IsPlayer: true, Jobs: { } jobs } || !table.Next[slot].Alive) continue;
            if (jobs.Any(j => j.Type == Type && j.Site == storehouse)) holders.Add(slot);
        }

        return holders;
    }

    /// <summary>
    /// Gives each of the player's selected characters the job at a storehouse, while it has fewer than <see cref="MaxHaulers"/>; one turned away
    /// says so.
    /// </summary>
    public static void Assign(World world, string storehouse)
    {
        int count = HoldersAt(world, storehouse).Count;
        foreach (var who in world.Player.Selection)
        {
            if (!world.Characters.TryResolveNext(who, out int slot) || world.Characters.Cold(slot) is not { IsPlayer: true } cold) continue;
            if (cold.Jobs.Any(j => j.Type == Type && j.Site == storehouse)) continue;

            if (count >= MaxHaulers)
            {
                world.Bark(who, FullBark);
                continue;
            }

            cold.Jobs.Add(new PermaJob(Type, storehouse));
            cold.Orders |= Meitou.Simulation.Player.CharacterOrders.Jobs;
            DecisionSystem.Request(cold);
            count++;
        }
    }

    /// <summary>
    /// Keeps every storehouse at <see cref="MaxHaulers"/>: past that (the right-click offer gives the job to the whole selection), the job is taken
    /// from the characters in the higher slots, who say why.
    /// </summary>
    public static void Cap(World world)
    {
        var bySite = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        var table = world.Characters;
        for (int slot = 0; slot < table.HighWater; slot++)
        {
            if (table.Cold(slot) is not { IsPlayer: true, Jobs: { Count: > 0 } jobs } || !table.Next[slot].Alive) continue;

            foreach (var job in jobs.Where(j => j.Type == Type && j.Site is not null))
            {
                if (!bySite.TryGetValue(job.Site!, out var list)) bySite[job.Site!] = list = [];
                if (!list.Contains(slot)) list.Add(slot);
            }
        }

        foreach (var (site, slots) in bySite)
            foreach (int slot in slots.Skip(MaxHaulers)) TakeAway(world, slot, site);
    }

    /// <summary>
    /// Takes the job at a storehouse from a character; the running job's index follows, as the jobs panel's X does.
    /// </summary>
    static void TakeAway(World world, int slot, string site)
    {
        if (world.Characters.Cold(slot) is not { } cold) return;

        for (int k = cold.Jobs.Count - 1; k >= 0; k--)
        {
            if (cold.Jobs[k].Type != Type || cold.Jobs[k].Site != site) continue;

            if (cold.Running is { Source: TaskSource.Job } run && run.Job == k) TaskStart.Stand(world.Characters, slot);
            cold.Jobs.RemoveAt(k);
            if (cold.Running is { Source: TaskSource.Job, Job: >= 0 } held && held.Job > k) cold.Running = held with { Job = held.Job - 1 };
        }

        world.Bark(world.Characters.IdOf(slot), FullBark);
        DecisionSystem.Request(cold);
    }
}
