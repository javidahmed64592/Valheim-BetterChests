using BetterChests.Core;
using BetterChests.Keys;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>
/// The game updates its buttons once a frame, before anything reads them. A chest key used on the chest the player
/// is looking at is acted on here, and the buttons that went down with it are cancelled, so the key doesn't also do
/// what the game or another mod bound it to.
/// </summary>
[HarmonyPatch(typeof(ZInput), nameof(ZInput.Update))]
internal static class ZInput_Update_Patch
{
    private static void Postfix()
    {
        if (ChestKeys.TryUse())
        {
            KeyInput.CancelButtonsPressedThisFrame();
        }
    }
}
