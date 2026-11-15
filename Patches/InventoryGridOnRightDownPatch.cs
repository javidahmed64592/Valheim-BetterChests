using BetterChests.Deposit;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>Keep key + right-click marks or unmarks the item instead of using it.</summary>
[HarmonyPatch(typeof(InventoryGrid), "OnRightDown")]
internal static class InventoryGrid_OnRightDown_Patch
{
    private static bool Prefix(InventoryGrid __instance, UIInputHandler element)
    {
        return !KeepItems.TryHandleRightClick(__instance, element);
    }
}
