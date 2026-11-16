using BetterChests.Search;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>While searching, the chest grid is filled with the search results instead of the open chest.</summary>
[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateInventory))]
internal static class InventoryGrid_UpdateInventory_Patch
{
    private static void Prefix(InventoryGrid __instance, ref Inventory inventory)
    {
        InventoryGui gui = InventoryGui.instance;
        if (gui != null && __instance == gui.m_containerGrid)
        {
            inventory = ChestSearch.ShownIn(gui, inventory);
        }
    }
}
