using BetterChests.UI;

using HarmonyLib;

namespace BetterChests.Patches;

[HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
internal static class InventoryGui_UpdateContainer_Patch
{
    private static void Prefix(InventoryGui __instance)
    {
        ChestPanelLayout.BeforeContainerUpdate(__instance);
    }
}
