using BetterChests.Deposit;
using BetterChests.Duplicates;
using BetterChests.UI;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>
/// Vanilla redraws an inventory grid's slots here. Three features act on it: the chest panel's layout, the duplicate
/// frames on the open chest, and the frames on items marked to keep in the player's inventory. They work on
/// different grids or parts of a slot, so the order doesn't matter, but the layout goes first. The tooltips are in
/// <see cref="InventoryGrid_UpdateGui_Tooltip_Patch"/>, so they can turn off on their own.
/// </summary>
[HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
internal static class InventoryGrid_UpdateGui_Patch
{
    private static void Postfix(InventoryGrid __instance)
    {
        ChestPanelLayout.OnGridUpdated(__instance);
        ChestDuplicates.OnGridUpdated(__instance);
        KeepItems.OnGridUpdated(__instance);
    }
}
