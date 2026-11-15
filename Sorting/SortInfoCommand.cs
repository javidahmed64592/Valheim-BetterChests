using Jotunn.Entities;

namespace BetterChests.Sorting;

/// <summary>
/// Writes every item's place in the sort order (group, tier, family) to the BepInEx log, in that order, so the
/// sorting can be checked against real chests.
/// </summary>
internal sealed class SortInfoCommand : ConsoleCommand
{
    public override string Name => "betterchests_sortinfo";

    public override string Help => "Writes the sort order of every item (group, tier, set or class) to the BepInEx log.";

    public override void Run(string[] args)
    {
        int count = 0;
        foreach (string line in ItemCategories.Describe())
        {
            BetterChestsPlugin._log.LogInfo("Sort: " + line);
            count++;
        }
        Console.instance?.Print($"betterchests_sortinfo: wrote {count} items to the BepInEx log.");
    }
}
