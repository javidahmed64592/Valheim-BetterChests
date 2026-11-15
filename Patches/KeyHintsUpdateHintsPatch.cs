using BetterChests.UI;

using HarmonyLib;

namespace BetterChests.Patches;

[HarmonyPatch(typeof(KeyHints), "UpdateHints")]
internal static class KeyHints_UpdateHints_Patch
{
    private static void Postfix(KeyHints __instance)
    {
        KeyHintUI.OnHintsUpdated(__instance);
    }
}
