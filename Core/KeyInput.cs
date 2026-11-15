using System.Collections.Generic;

using HarmonyLib;

namespace BetterChests.Core;

/// <summary>Keeping a key the mod has used from also doing what other mods bound it to.</summary>
internal static class KeyInput
{
    private static readonly AccessTools.FieldRef<ZInput, Dictionary<string, ZInput.ButtonDef>> Buttons =
        AccessTools.FieldRefAccess<ZInput, Dictionary<string, ZInput.ButtonDef>>("m_buttons");

    /// <summary>
    /// Cancels, for the rest of this frame, every game button that went down this frame. The game and mods built on
    /// Jotunn read their keys through these buttons, so a chest key used while looking at a chest (say F to deposit
    /// all) doesn't also do what the game or another mod bound it to (F is the Forsaken power, R hides your weapons).
    /// Run straight after the game updates its buttons each frame, before anything reads them. Mods that read the
    /// keyboard directly can't be stopped this way.
    /// </summary>
    internal static void CancelButtonsPressedThisFrame()
    {
        ZInput input = ZInput.instance;
        if (input == null)
        {
            return;
        }

        foreach (ZInput.ButtonDef button in Buttons(input).Values)
        {
            if (button.Pressed)
            {
                button.ResetState();
            }
        }
    }
}
