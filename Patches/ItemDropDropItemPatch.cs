using BetterChests.Deposit;

using HarmonyLib;

namespace BetterChests.Patches;

[HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.DropItem))]
internal static class ItemDrop_DropItem_Patch
{
    private static void Postfix(ItemDrop __result)
    {
        AutoDeposit.OnItemDropped(__result);
    }
}
