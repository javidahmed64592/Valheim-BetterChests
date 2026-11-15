using BetterChests.UI;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>When the inventory screen is created: the chest buttons, and the split dialog's - and + buttons.</summary>
[HarmonyPatch(typeof(InventoryGui), "Awake")]
internal static class InventoryGui_Awake_Patch
{
    private static void Postfix(InventoryGui __instance)
    {
        ChestButtons.CreateButtons(__instance);
        SplitDialogControls.OnGuiAwake(__instance);
    }
}
