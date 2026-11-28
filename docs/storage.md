# Storage

The Storage mod adds the **Storehouse**: an Edo-period granary (`Storage/Structures/storehouse.glb`, 6.5 m, no interior) with a large general
inventory, storage filters, a hauler job for up to three people and a hauling network over every storage and machine within 1500 m.

## Storehouse

- Record `1-Storage.mod`, made in the data phase from the imported look `900000-Storage.mod` plus General storage functionality
  (`2950-gamedata.base`): a 16 × 12 grid of any item type, stacks ×4, 40 building materials, build category `STORAGE`, `max operators` 0 (the
  panel's Workers line reads "-"; General storage's own is 5). It is unlocked from the start (`_Default Start`'s `enable buildings`).
- It can only be built outside: a ghost standing in or on another building is refused ("A storehouse must be built outside.").
- Its range is 1500 m (15000 world units) on the ground plane.

## Manage storehouse

Clicking the storehouse opens its inventory as any storage's. Its building panel adds one button, **Manage storehouse**, which opens the window
with three tabs, and a **Haulers** bar (n/3) under Condition. The panel clips lines past its lower edge and other mods add lines too, so the
mod adds no other button: Assign selected is on the Hauling network tab and the right-click menu's Work as. The **Manage storehouse** key (N by default, rebound on the
settings' Controls page) opens or closes the selected storehouse's window as well (the one whose panel shows; with none, it closes the window), and **Esc** closes it. The window reads the system's published views and changes the world only through
commands, so it never races the simulation.

Setting **Inventory reach (m)** (100 to 500, default 100) is how far a selected character may stand and still open the inventory with a click,
once the engine supports it (Engine support).

## Storage filters

The item categories as the F10 item menu groups them (Items, Weapons, Crossbows, Armour, Containers, Limb replacements, Maps), each with its
sub-categories (an item's function, a weapon's skill, an armour's slot, a limb's slot). Each item is Forbidden or allowed at a tier: Low,
Deferred, Normal, Preferred, Important, High, Critical (the engine's `StoragePriority`).

- An item's own setting wins over its sub-category's, which wins over its category's; nothing set is Normal. Setting a category clears the
  settings under it; Reset returns to inheriting.
- The storehouse's storage takes only allowed items (`IStorageFilter`), and the game's own haulers rank it by tier (`IStoragePriority`).
- The search box lists matching items from every category.
- The storage's own **Put priority** and **Take priority** (the game's, which the panel shows for every storage) are set here, from Forbidden to
  Critical, through the game's `SetStoragePriority`. The engine still shows them on the panel too (no engine hook hides them yet).

## Hauling network

Every finished storage and machine of the player's within range, rescanned on load, when the window opens, every 60 ticks and with Rescan. Only
the storehouse is in the network at first; a node joins when its box is ticked, when it is dropped on the flow grid, or when a link names it.

- **Send to** on a node opens a searchable list of the other nodes in range that are compatible with it and not yet linked from it.
- A link moves what the source holds now and the target takes now. Links from a source run in their order (↑ ↓), the first with room first.
- **Odd / even** on a source with two or more links sends unit 1 down its first link, unit 2 down its second, and so on, carrying on from the
  last unit sent across trips.
- Links ignore the game's hauling rules: they pull from any storage, and a machine or deposit (a well, a mine, a farm) also takes its own
  product back into its output slot, up to its capacity. A line of wells can pass water from well to well to a water tower.
- A link into the storehouse follows its filters.

### Compatible storages

A node sends what it holds (a storage limited to some items: only those; a machine: its output) and takes what its limits allow (the
storehouse: anything; a storage: its listed items or its item type; a machine: its inputs and its own output). Two nodes are compatible when
some item is in both: general storage → iron plate storage is offered, iron plate storage → greenfruit storage is not.

### Flow grid

The network as cards on a 10 × 12 grid, with a wire per link in any direction (out of the source's right side, into the target's left), its
order at the start. Drag a card to move it (onto another card: they swap), drag from a card's ▸ port onto another card to link them, and drag
a node from the tray onto a cell to add it. Clicking a card shows what it holds, its links, its split and Take out of the network.

## Haulers

The Storehouse hauler job (task type 5100) is offered at the storehouse; at most three people hold it per storehouse (the fourth is refused
with a bark). A hauler first puts away anything it still carries, then takes the best trip:

1. Link trips, ranked by the storehouse filter tier of what goes into the storehouse (other targets count as Normal), link order breaking ties.
2. Otherwise gathering into the storehouse: allowed machine outputs no link takes, and (setting **Haulers gather loose items**, on by default)
   loose items in range with no owner or the player's; highest tier first, then nearest.

A trip carries at most 20 units and what the hauler has room for. Storage items move as they are (quality and wear kept); a machine output is
made into items as the game's haulers make it.

## Network inventory

Every item stack in range by category and sub-category, each line naming its container. Hovering or clicking a line shows its container and
distance; right-clicking sends the lead selected character there and opens the container on arrival (Engine support). The search takes plain
text (any part, ignoring case) or, with Regex ticked, a regular expression, over item names or container names.

## Engine support

`Storage/Ui/StorageWindows.cs` looks up `ModInterface.OpenStorage(site, walker, reach)` and `UiRegistration.AddStorageReach(record, reach)`
by reflection. Without them (Meitou 0.24.0) a click opens the inventory at the game's own reach and Go there gives a move order only.

## Save

Each storehouse's filters, nodes (in or out, split, turn, grid cell) and links are saved in the mod's save data (`StorageSave`, version 1).
