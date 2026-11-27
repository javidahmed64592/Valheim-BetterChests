using BetterChests.Core;
using BetterChests.Deposit;

using HarmonyLib;

namespace BetterChests.Patches;

[HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.DropItem))]
[Feature(AutoDeposit.Feature)]
internal static class ItemDrop_DropItem_Patch
{
    private static void Postfix(ItemDrop __result)
    {
        AutoDeposit.OnItemDropped(__result);
    }
}
