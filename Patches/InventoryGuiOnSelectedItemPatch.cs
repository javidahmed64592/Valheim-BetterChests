using BetterChests.Core;
using BetterChests.Search;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>
/// While searching, a click on the chest grid takes the item from the chest it is really in, and dropping or Ctrl +
/// clicking an item from the player's inventory puts it into the chest group.
/// </summary>
[HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
[Feature(ChestSearch.Feature)]
internal static class InventoryGui_OnSelectedItem_Patch
{
    private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, InventoryGrid.Modifier mod)
    {
        return !ChestSearch.TryHandleClick(__instance, grid, item, mod);
    }
}
