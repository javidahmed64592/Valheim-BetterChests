using BetterChests.Search;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>While searching, Take all (the button and its key) takes every stack the search shows.</summary>
[HarmonyPatch(typeof(InventoryGui), "OnTakeAll")]
internal static class InventoryGui_OnTakeAll_Patch
{
    private static bool Prefix(InventoryGui __instance)
    {
        if (!ChestSearch.Active || Player.m_localPlayer == null || Player.m_localPlayer.IsTeleporting())
        {
            return true;
        }
        __instance.SetupDragItem(null, null, 1);
        SearchActions.TakeAll();
        return false;
    }
}
