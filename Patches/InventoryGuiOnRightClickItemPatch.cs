using BetterChests.Search;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>While searching, right-clicking a result uses the real item in its real chest, never the copy shown.</summary>
[HarmonyPatch(typeof(InventoryGui), "OnRightClickItem")]
internal static class InventoryGui_OnRightClickItem_Patch
{
    private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item)
    {
        return !ChestSearch.TryHandleRightClick(__instance, grid, item);
    }
}
