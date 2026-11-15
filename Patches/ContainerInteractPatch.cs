using BetterChests.Deposit;

using HarmonyLib;

namespace BetterChests.Patches;

[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class Container_Interact_Patch
{
    private static void Prefix(Container __instance, bool hold)
    {
        AutoDeposit.BeforeInteract(__instance, hold);
    }
}
