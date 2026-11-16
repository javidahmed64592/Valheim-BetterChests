using BetterChests.Search;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>Opening a different chest (vanilla swaps it in without closing the first) empties the search.</summary>
[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
internal static class InventoryGui_Show_Patch
{
    private static void Prefix(InventoryGui __instance, Container container)
    {
        if (container != __instance.m_currentContainer)
        {
            ChestSearch.Clear();
        }
    }
}
