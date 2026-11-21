using BetterChests.Core;
using BetterChests.Keys;
using BetterChests.Search;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>
/// The game updates its buttons once a frame, before anything reads them. A chest key used on the chest the player
/// is looking at is acted on here, and the buttons that went down with it are cancelled, so the key doesn't also do
/// what the game or another mod bound it to. Chest requests waiting on another player are also followed up here.
/// </summary>
[HarmonyPatch(typeof(ZInput), nameof(ZInput.Update))]
internal static class ZInput_Update_Patch
{
    private static void Postfix()
    {
        ChestRequests.Update();

        // Typing in the chest search bar: E would otherwise close the inventory, the number keys use hotbar items…
        if (ChestSearch.Focused || ChestKeys.TryUse())
        {
            KeyInput.CancelButtonsPressedThisFrame();
        }
    }
}
