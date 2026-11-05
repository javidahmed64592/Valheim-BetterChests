using System.Linq;

using HarmonyLib;

using TMPro;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BetterChests;

/// <summary>
/// The chest panel's extra buttons, "Deposit all" under "Take all" and "Sort" under "Place stacks", and
/// keys for all four laid out like them: R and T for the top row, F and G for the bottom.
/// </summary>
internal static class ChestButtons
{
    /// <summary>
    /// Space between a vanilla button and the new one under it.
    /// </summary>
    private const float ButtonGap = 4f;

    /// <summary>
    /// A copy of <paramref name="template"/>, so it matches the vanilla style, placed under it.
    /// </summary>
    private static RectTransform CreateButtonBelow(Button template, string name, string text, UnityAction onClick)
    {
        if (template == null || template.transform.parent == null)
        {
            BetterChestsPlugin._log.LogWarning("Vanilla chest panel layout not as expected, not adding the " + text + " button.");
            return null;
        }

        GameObject go = Object.Instantiate(template.gameObject, template.transform.parent, false);
        go.name = name;

        // The copy would otherwise also react to the vanilla button's gamepad button.
        foreach (UIGamePad gamePad in go.GetComponentsInChildren<UIGamePad>(true))
        {
            if (gamePad.m_hint != null)
            {
                Object.DestroyImmediate(gamePad.m_hint);
            }

            Object.DestroyImmediate(gamePad);
        }

        RectTransform rect = (RectTransform)go.transform;
        RectTransform templateRect = (RectTransform)template.transform;
        rect.anchoredPosition = templateRect.anchoredPosition - new Vector2(0f, templateRect.rect.height + ButtonGap);

        TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            // Match the vanilla button's capitalisation.
            bool upper = !string.IsNullOrEmpty(label.text) && !label.text.Any(char.IsLower);
            label.text = upper ? text.ToUpperInvariant() : text;
        }

        Button button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(onClick);
        return rect;
    }

    [HarmonyPatch(typeof(InventoryGui), "Awake")]
    private static class InventoryGui_Awake_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryGui __instance)
        {
            RectTransform depositAll = CreateButtonBelow(__instance.m_takeAllButton, "BetterChests_DepositAll", "Deposit all",
                DepositAll.DepositIntoOpenContainer);
            RectTransform sort = CreateButtonBelow(__instance.m_stackAllButton, "BetterChests_Sort", "Sort",
                ChestSort.SortOpenContainer);
            ChestPanelLayout.Setup(__instance, [.. new[] { depositAll, sort }.Where(b => b != null)]);
        }
    }

    /// <summary>
    /// The keys work while a chest is open (and the split dialog isn't).
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "Update")]
    private static class InventoryGui_Update_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryGui __instance)
        {
            if (!InventoryGui.IsVisible()
                || !__instance.IsContainerOpen()
                || __instance.m_splitDialog.IsActive
                || BetterChestsPlugin.TextInputActive())
            {
                return;
            }

            if (BetterChestsPlugin._takeAllKey.Down())
            {
                __instance.OnTakeAll();
            }
            else if (BetterChestsPlugin._depositAllKey.Down())
            {
                DepositAll.DepositIntoOpenContainer();
            }
            else if (BetterChestsPlugin._placeStacksKey.Down())
            {
                __instance.OnStackAll();
            }
            else if (BetterChestsPlugin._sortKey.Down())
            {
                ChestSort.SortOpenContainer();
            }
        }
    }
}
