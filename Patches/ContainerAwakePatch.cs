using BetterChests.Core;

using HarmonyLib;

namespace BetterChests.Patches;

[HarmonyPatch(typeof(Container), "Awake")]
internal static class Container_Awake_Patch
{
    private static void Postfix(Container __instance)
    {
        ChestClusters.Register(__instance);
    }
}
