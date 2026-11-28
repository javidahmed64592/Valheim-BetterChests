using BetterChests.Core;
using BetterChests.UI;

using HarmonyLib;

namespace BetterChests.Patches;

[HarmonyPatch(typeof(KeyHints), "UpdateHints")]
[Feature(Feature)]
internal static class KeyHints_UpdateHints_Patch
{
    /// <summary>Without it, the game's own key hints are shown with a chest open.</summary>
    public const string Feature = "Key hints";

    private static void Postfix(KeyHints __instance)
    {
        KeyHintUI.OnHintsUpdated(__instance);
    }
}
