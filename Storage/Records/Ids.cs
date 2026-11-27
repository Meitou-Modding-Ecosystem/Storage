namespace Storage.Records;

/// <summary>
/// The mod's record ids and claimed numbers (README "Numbers"; GutenStructure docs/numbers.md, block 5100-5199).
/// </summary>
public static class Ids
{
    /// <summary>
    /// The mod's data file name, the suffix of its record ids.
    /// </summary>
    public const string FileName = "Storage.mod";

    /// <summary>
    /// The storehouse: the building the player places.
    /// </summary>
    public const string Storehouse = "1-" + FileName;

    /// <summary>
    /// The building the structure import made from <c>Structures/storehouse.glb</c> (block 0 of <c>Structures/ids.json</c>): the storehouse's look.
    /// </summary>
    public const string StorehouseLook = "900000-" + FileName;

    /// <summary>
    /// The storehouse hauler's job type (task type pool).
    /// </summary>
    public const int HaulerJob = 5100;

    /// <summary>
    /// The first and last number of the mod's block.
    /// </summary>
    public const int BlockFirst = 5100, BlockLast = 5199;
}
