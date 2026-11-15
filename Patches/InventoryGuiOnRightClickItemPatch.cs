using BetterChests.Search;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>While searching, right-clicking a result would use the copy shown (eat it, equip it), so it does nothing.</summary>
[HarmonyPatch(typeof(InventoryGui), "OnRightClickItem")]
internal static class InventoryGui_OnRightClickItem_Patch
{
    private static bool Prefix(InventoryGui __instance, InventoryGrid grid)
    {
        return !ChestSearch.IsResultsGrid(__instance, grid);
    }
}
