using System.Linq;

using BetterChests.Deposit;
using BetterChests.Restock;
using BetterChests.Sorting;

using TMPro;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BetterChests.UI;

/// <summary>
/// The chest panel's extra buttons ("Deposit all", "Sort" and "Restock", next to vanilla's "Take all" and "Place
/// stacks"; <see cref="ChestPanelLayout"/> puts all five in a row) and the chest keys.
/// </summary>
internal static class ChestButtons
{
    /// <summary>
    /// A copy of <paramref name="template"/>, so it matches the vanilla style. The layout places it.
    /// </summary>
    private static Button CreateButton(Button template, string name, string text, UnityAction onClick)
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

        TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            // Match the vanilla button's capitalisation.
            bool upper = !string.IsNullOrEmpty(label.text) && !label.text.Any(char.IsLower);
            string localized = Localization.instance.Localize(text);
            label.text = upper ? localized.ToUpperInvariant() : localized;
        }

        Button button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(onClick);
        return button;
    }

    /// <summary>
    /// Adds the Deposit all, Sort and Restock buttons when the inventory screen is created, and lays out the row:
    /// what comes into your inventory (Take all, Restock), what goes into the chest (Deposit all, Place stacks), then
    /// Sort.
    /// </summary>
    internal static void CreateButtons(InventoryGui gui)
    {
        Button depositAll = CreateButton(gui.m_takeAllButton, "BetterChests_DepositAll", "$betterchests_deposit_all",
            DepositAll.DepositIntoOpenContainer);
        Button sort = CreateButton(gui.m_stackAllButton, "BetterChests_Sort", "$betterchests_sort",
            ChestSort.SortOpenContainer);
        Button restock = CreateButton(gui.m_takeAllButton, "BetterChests_Restock", "$betterchests_hint_restock",
            () => ChestRestock.Restock(gui.m_currentContainer, Player.m_localPlayer));
        ChestPanelLayout.Setup(gui,
            [.. new[] { gui.m_takeAllButton, restock, depositAll, gui.m_stackAllButton, sort }.Where(b => b != null)]);
    }

    /// <summary>
    /// The keys work while a chest is open (and the split dialog isn't). Called every frame.
    /// </summary>
    internal static void HandleKeys(InventoryGui gui)
    {
        if (!InventoryGui.IsVisible()
            || !gui.IsContainerOpen()
            || gui.m_splitDialog.IsActive
            || BetterChestsPlugin.TextInputActive())
        {
            return;
        }

        if (BetterChestsPlugin._takeAllKey.Down())
        {
            gui.OnTakeAll();
        }
        else if (BetterChestsPlugin._depositAllKey.Down())
        {
            DepositAll.DepositIntoOpenContainer();
        }
        else if (BetterChestsPlugin._placeStacksKey.Down())
        {
            gui.OnStackAll();
        }
        else if (BetterChestsPlugin._sortKey.Down())
        {
            ChestSort.SortOpenContainer();
        }
        else if (BetterChestsPlugin._restockKey.Down())
        {
            ChestRestock.Restock(gui.m_currentContainer, Player.m_localPlayer);
        }
    }
}
