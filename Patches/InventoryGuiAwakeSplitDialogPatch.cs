using BetterChests.Core;
using BetterChests.UI;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>When the inventory screen is created: the split dialog's - and + buttons.</summary>
[HarmonyPatch(typeof(InventoryGui), "Awake")]
[Feature(SplitDialogControls.Feature)]
internal static class InventoryGui_Awake_SplitDialog_Patch
{
    private static void Postfix(InventoryGui __instance)
    {
        SplitDialogControls.OnGuiAwake(__instance);
    }
}
