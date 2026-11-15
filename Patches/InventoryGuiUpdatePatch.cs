using BetterChests.Duplicates;
using BetterChests.UI;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>Every frame while the inventory screen exists: the chest keys, and the glow on chests holding the hovered item.</summary>
[HarmonyPatch(typeof(InventoryGui), "Update")]
internal static class InventoryGui_Update_Patch
{
    private static void Postfix(InventoryGui __instance)
    {
        ChestButtons.HandleKeys(__instance);
        ChestDuplicates.UpdateGlow(__instance);
    }
}
