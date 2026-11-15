using BetterChests.Keys;

using HarmonyLib;

namespace BetterChests.Patches;

[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
internal static class Container_GetHoverText_Patch
{
    private static void Postfix(Container __instance, ref string __result)
    {
        string hints = ChestKeys.HoverHints(__instance);
        if (hints.Length > 0)
        {
            __result += Localization.instance.Localize(hints);
        }
    }
}
