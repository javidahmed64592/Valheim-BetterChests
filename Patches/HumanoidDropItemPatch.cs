using BetterChests.Core;
using BetterChests.Deposit;

using HarmonyLib;

namespace BetterChests.Patches;

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem))]
[Feature(AutoDeposit.Feature)]
internal static class Humanoid_DropItem_Patch
{
    private static void Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, int amount)
    {
        AutoDeposit.BeforeDrop(__instance, inventory, item, amount);
    }

    private static void Finalizer()
    {
        AutoDeposit.AfterDrop();
    }
}
