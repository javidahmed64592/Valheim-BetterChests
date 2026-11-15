using BetterChests.Core;

using HarmonyLib;

namespace BetterChests.Patches;

[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
internal static class Container_Interact_Patch
{
    private static void Prefix(Container __instance, bool hold)
    {
        ChestRequests.BeforeInteract(__instance, hold);
    }
}
