using Meitou.Simulation;
using Meitou.Simulation.Items;
using Storage.Filters;
using Storage.Network;

namespace Storage;

/// <summary>
/// Sets (or, with null, clears) a filter level of a storehouse's storage filters.
/// </summary>
public sealed record SetFilterCommand(string Storehouse, FilterScope Scope, string Key, StoragePriority? Level) : SimCommand;

/// <summary>
/// Puts a node in or out of a storehouse's hauling network.
/// </summary>
public sealed record SetNodeActiveCommand(string Storehouse, string Node, bool Active) : SimCommand;

/// <summary>
/// Sets how a node of a storehouse's network shares what it sends.
/// </summary>
public sealed record SetSplitCommand(string Storehouse, string Node, SplitMode Split) : SimCommand;

/// <summary>
/// Places a node on a storehouse's network grid.
/// </summary>
public sealed record PlaceNodeCommand(string Storehouse, string Node, int Column, int Row) : SimCommand;

/// <summary>
/// Adds a link (and puts both ends in the network) or removes it; <see cref="Step"/> other than 0 moves it earlier or later among its source's
/// links instead.
/// </summary>
public sealed record LinkCommand(string Storehouse, string From, string To, bool Add, int Step = 0) : SimCommand;

/// <summary>
/// Scans a storehouse's network now (the window's Rescan button).
/// </summary>
public sealed record RescanCommand(string Storehouse) : SimCommand;

/// <summary>
/// Gives the selected characters the hauler job at a storehouse.
/// </summary>
public sealed record AssignHaulersCommand(string Storehouse) : SimCommand;

/// <summary>
/// Sends a selected character to a storage of the network (the network inventory's Go there): a move order to it.
/// </summary>
public sealed record GoToStorageCommand(CharacterId Who, string Site) : SimCommand;
