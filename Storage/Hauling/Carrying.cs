using Meitou.Simulation;
using Meitou.Simulation.Items;
using Meitou.Simulation.Production;

namespace Storage.Hauling;

/// <summary>
/// Moving a trip's units between the places haulers work and the hauler's own inventory (docs/storage.md "Haulers"). Storage items move as they
/// are (quality, wear, marks kept); a machine's output is made into items as the game's haulers make it. Serial steps only.
/// </summary>
public static class Carrying
{
    /// <summary>
    /// A new unit of a record; null when the world has no item factory or cannot make it.
    /// </summary>
    public static ItemInstance? Make(World world, string record, int quantity = 1) =>
        world.Systems.OfType<ProductionSystem>().FirstOrDefault()?.Items.Create(record, quantity);

    /// <summary>
    /// The units of a record the inventory holds, its worn backpack's included.
    /// </summary>
    public static int Count(Inventory inventory, string record) => Holders(inventory, record).Sum(h => h.Item.Quantity);

    #region Picking up

    /// <summary>
    /// Picks up to <paramref name="most"/> units of a record at a place into the carrier's inventory; returns the units picked.
    /// </summary>
    public static int Pick(World world, Inventory carrier, PickPlace place, string record, int most) => place.Kind switch
    {
        PickKind.Ground => FromGround(world, carrier, place.Ground, record),
        _ when world.Machines.Find(place.Site) is { } machine => FromMachine(world, carrier, machine, record, most),
        _ => 0,
    };

    /// <summary>
    /// Units from a storage's grid or a machine's output.
    /// </summary>
    static int FromMachine(World world, Inventory carrier, Machine machine, string record, int most)
    {
        int moved = machine.Storage is { } grid ? FromGrid(grid, carrier, record, most) : FromOutput(world, carrier, machine, record, most);
        if (moved > 0) world.Machines.Touch();

        return moved;
    }

    /// <summary>
    /// Units of a record out of a grid, stack by stack, as they are.
    /// </summary>
    static int FromGrid(Inventory grid, Inventory carrier, string record, int most)
    {
        int moved = 0;
        foreach (var item in grid.Items.Where(i => i.Record == record && i.Contents is null).ToList())
        {
            int got = InventoryCommands.MoveUnits(grid, item, Math.Min(most - moved, item.Quantity), carrier);
            moved += got;
            if (moved >= most || got == 0) break;
        }

        return moved;
    }

    /// <summary>
    /// Units of a machine's output, made into items.
    /// </summary>
    static int FromOutput(World world, Inventory carrier, Machine machine, string record, int most)
    {
        if (machine.Output is not { } output || output.Recipe.Item != record) return 0;

        int want = Math.Min(most, output.Count);
        if (want <= 0 || Make(world, record, want) is not { } stack) return 0;

        carrier.Add(stack);
        int moved = want - Math.Max(0, stack.Quantity);
        output.Count -= moved;
        return moved;
    }

    /// <summary>
    /// A loose item off the ground; what does not fit stays where it lay.
    /// </summary>
    static int FromGround(World world, Inventory carrier, int id, string record)
    {
        if (world.GroundItems.Find(id) is not { } lying || lying.Item.Record != record) return 0;
        if (world.GroundItems.Take(id) is not { } taken) return 0;

        int before = taken.Item.Quantity;
        if (!carrier.Add(taken.Item) && taken.Item.Quantity > 0)
            world.GroundItems.Load(taken.Item, taken.Position, taken.Rotation, taken.OwnedBy);

        return before - Math.Max(0, taken.Item.Quantity);
    }

    #endregion

    #region Dropping off

    /// <summary>
    /// Puts up to <paramref name="most"/> carried units of a record into a site: a storage's grid, or a machine's inputs and then its own output
    /// slot (a deposit taking back what it makes). Returns the units put.
    /// </summary>
    public static int Put(World world, Inventory carrier, string site, string record, int most)
    {
        if (world.Machines.Find(site) is not { } machine) return 0;

        int moved = machine.Storage is { } grid ? IntoGrid(carrier, grid, record, most) : IntoMachine(carrier, machine, record, most);
        if (moved > 0) world.Machines.Touch();

        return moved;
    }

    /// <summary>
    /// Carried units into a grid, as they are.
    /// </summary>
    static int IntoGrid(Inventory carrier, Inventory grid, string record, int most)
    {
        int moved = 0;
        foreach (var (owner, item) in Holders(carrier, record).ToList())
        {
            int got = InventoryCommands.MoveUnits(owner, item, Math.Min(most - moved, item.Quantity), grid);
            moved += got;
            if (moved >= most || got == 0) break;
        }

        return moved;
    }

    /// <summary>
    /// Carried units into a machine: its inputs first, then its output slot when it makes the record.
    /// </summary>
    static int IntoMachine(Inventory carrier, Machine machine, string record, int most)
    {
        int moved = IntoInputs(carrier, machine, record, most);
        return moved + IntoOutput(carrier, machine, record, most - moved);
    }

    /// <summary>
    /// Carried units into a machine's output slot of the same record, up to its capacity.
    /// </summary>
    static int IntoOutput(Inventory carrier, Machine machine, string record, int most)
    {
        if (machine.Output is not { } output || output.Recipe.Item != record) return 0;

        int moved = 0;
        while (moved < most && !output.Full && TakeOne(carrier, record))
        {
            output.Count++;
            moved++;
        }

        return moved;
    }

    /// <summary>
    /// Carried units into the machine's inputs that take the record, up to each one's capacity.
    /// </summary>
    static int IntoInputs(Inventory carrier, Machine machine, string record, int most)
    {
        int moved = 0;
        foreach (var slot in machine.Inputs.Where(i => i.Takes(record)))
        {
            while (moved < most && slot.Count < slot.Recipe.Capacity && TakeOne(carrier, record))
            {
                slot.Count++;
                moved++;
            }
        }

        return moved;
    }

    /// <summary>
    /// Takes one carried unit of a record away; false when none is carried.
    /// </summary>
    static bool TakeOne(Inventory carrier, string record) =>
        Holders(carrier, record).FirstOrDefault() is { Item: not null } held && held.Owner.RemoveOne(held.Item);

    /// <summary>
    /// The carried stacks of a record with the inventory holding each: the main grid's, then the worn backpack's.
    /// </summary>
    static IEnumerable<(Inventory Owner, ItemInstance Item)> Holders(Inventory carrier, string record)
    {
        foreach (var item in carrier.Items.Where(i => i.Record == record && i.Contents is null)) yield return (carrier, item);
        if (carrier.Backpack?.Contents is not { } bag) yield break;

        foreach (var item in bag.Items.Where(i => i.Record == record && i.Contents is null)) yield return (bag, item);
    }

    #endregion
}
