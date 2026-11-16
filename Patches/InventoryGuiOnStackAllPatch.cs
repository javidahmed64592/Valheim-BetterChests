using BetterChests.Search;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>While searching, Place stacks (the button and its key) tops up stacks anywhere in the chest group.</summary>
[HarmonyPatch(typeof(InventoryGui), "OnStackAll")]
internal static class InventoryGui_OnStackAll_Patch
{
    private static bool Prefix(InventoryGui __instance)
    {
        if (!ChestSearch.Active || Player.m_localPlayer == null || Player.m_localPlayer.IsTeleporting())
        {
            return true;
        }
        __instance.SetupDragItem(null, null, 1);
        SearchActions.PlaceStacks(__instance.m_currentContainer);
        return false;
    }
}
