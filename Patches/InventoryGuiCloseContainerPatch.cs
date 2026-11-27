using BetterChests.Core;
using BetterChests.Search;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>Closing a chest (or the inventory, or opening another chest) empties the search.</summary>
[HarmonyPatch(typeof(InventoryGui), "CloseContainer")]
[Feature(ChestSearch.Feature)]
internal static class InventoryGui_CloseContainer_Patch
{
    private static void Postfix()
    {
        ChestSearch.Clear();
    }
}
