using BetterChests.UI;

using HarmonyLib;

namespace BetterChests.Patches;

[HarmonyPatch(typeof(InventoryGui), "UpdateSplitDialog")]
internal static class InventoryGui_UpdateSplitDialog_Patch
{
    private static void Postfix(InventoryGui __instance)
    {
        SplitDialogControls.OnSplitDialogUpdate(__instance);
    }
}
