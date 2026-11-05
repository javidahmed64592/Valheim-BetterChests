using System.Collections.Generic;
using System.Linq;

using HarmonyLib;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace BetterChests;

/// <summary>
/// Sorting the open chest, from a "Sort" button under "Place stacks" or the sort key. Partial
/// stacks are merged, then items are grouped by type (materials, food, ammo, weapons, armour and so on),
/// with the item the chest holds most of first in each group.
/// </summary>
internal static class ChestSort
{
    private const string ButtonName = "BetterChests_Sort";
    private const string ButtonLabel = "Sort";

    /// <summary>
    /// Space between "Place stacks" and the sort button under it.
    /// </summary>
    private const float ButtonGap = 4f;

    /// <summary>
    /// Sort order of the item types; types not listed go last.
    /// </summary>
    private static readonly ItemDrop.ItemData.ItemType[] TypeOrder =
    [
        ItemDrop.ItemData.ItemType.Material,
        ItemDrop.ItemData.ItemType.Consumable,
        ItemDrop.ItemData.ItemType.Fish,
        ItemDrop.ItemData.ItemType.Ammo,
        ItemDrop.ItemData.ItemType.AmmoNonEquipable,
        ItemDrop.ItemData.ItemType.OneHandedWeapon,
        ItemDrop.ItemData.ItemType.TwoHandedWeapon,
        ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft,
        ItemDrop.ItemData.ItemType.Bow,
        ItemDrop.ItemData.ItemType.Attach_Atgeir,
        ItemDrop.ItemData.ItemType.Shield,
        ItemDrop.ItemData.ItemType.Torch,
        ItemDrop.ItemData.ItemType.Tool,
        ItemDrop.ItemData.ItemType.Helmet,
        ItemDrop.ItemData.ItemType.Chest,
        ItemDrop.ItemData.ItemType.Legs,
        ItemDrop.ItemData.ItemType.Shoulder,
        ItemDrop.ItemData.ItemType.Hands,
        ItemDrop.ItemData.ItemType.Utility,
        ItemDrop.ItemData.ItemType.Trinket,
        ItemDrop.ItemData.ItemType.Trophy,
        ItemDrop.ItemData.ItemType.Customization,
        ItemDrop.ItemData.ItemType.Misc,
    ];

    /// <summary>
    /// Sorts the chest open in the inventory screen, if any.
    /// </summary>
    internal static void SortOpenContainer()
    {
        InventoryGui gui = InventoryGui.instance;
        Container container = gui != null ? gui.m_currentContainer : null;
        if (container == null || !container.IsOwner() || Player.m_localPlayer == null || Player.m_localPlayer.IsTeleporting())
        {
            return;
        }

        // Drop whatever is being dragged back where it came from, like the vanilla chest buttons do.
        gui.SetupDragItem(null, null, 1);
        Sort(container.GetInventory());
        _ = gui.m_moveItemEffects.Create(gui.transform.position, Quaternion.identity);
    }

    private static void Sort(Inventory inventory)
    {
        List<ItemDrop.ItemData> items = inventory.GetAllItems();
        MergeStacks(items);

        Dictionary<string, int> totals = [];
        foreach (ItemDrop.ItemData item in items)
        {
            _ = totals.TryGetValue(item.m_shared.m_name, out int total);
            totals[item.m_shared.m_name] = total + item.m_stack;
        }

        List<ItemDrop.ItemData> sorted = [.. items
            .OrderBy(TypeRank)
            .ThenByDescending(i => totals[i.m_shared.m_name])
            .ThenBy(i => Localization.instance.Localize(i.m_shared.m_name), System.StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(i => i.m_quality)
            .ThenByDescending(i => i.m_stack)];

        int width = inventory.GetWidth();
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].m_gridPos = new Vector2i(i % width, i / width);
        }

        items.Clear();
        items.AddRange(sorted);
        inventory.Changed();
    }

    /// <summary>
    /// Tops up partial stacks from later stacks of the same item, removing any that end up empty.
    /// </summary>
    private static void MergeStacks(List<ItemDrop.ItemData> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            ItemDrop.ItemData target = items[i];
            int max = target.m_shared.m_maxStackSize;
            for (int j = i + 1; j < items.Count && target.m_stack < max; j++)
            {
                ItemDrop.ItemData source = items[j];
                if (source.m_stack <= 0 || !target.IsSameType(source))
                {
                    continue;
                }

                int move = Mathf.Min(max - target.m_stack, source.m_stack);
                target.m_stack += move;
                source.m_stack -= move;
                if (source.m_cheated)
                {
                    target.m_cheated = true;
                }
            }
        }

        _ = items.RemoveAll(i => i.m_stack <= 0);
    }

    private static int TypeRank(ItemDrop.ItemData item)
    {
        int rank = System.Array.IndexOf(TypeOrder, item.m_shared.m_itemType);
        return rank < 0 ? TypeOrder.Length : rank;
    }

    /// <summary>
    /// A copy of the "Place stacks" button, so it matches the vanilla style, placed under it.
    /// </summary>
    private static RectTransform CreateButton(InventoryGui gui)
    {
        Button template = gui.m_stackAllButton;
        if (template == null || template.transform.parent == null)
        {
            BetterChestsPlugin._log.LogWarning("Vanilla chest panel layout not as expected, not adding the sort button.");
            return null;
        }

        GameObject go = Object.Instantiate(template.gameObject, template.transform.parent, false);
        go.name = ButtonName;

        // The copy would otherwise also react to the gamepad button for "Place stacks".
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
            label.text = upper ? ButtonLabel.ToUpperInvariant() : ButtonLabel;
        }

        Button button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(SortOpenContainer);
        return rect;
    }

    [HarmonyPatch(typeof(InventoryGui), "Awake")]
    private static class InventoryGui_Awake_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryGui __instance)
        {
            RectTransform button = CreateButton(__instance);
            if (button != null)
            {
                ChestPanelLayout.Setup(__instance, button);
            }
        }
    }

    /// <summary>
    /// The take all, place stacks and sort keys work while a chest is open (and the split dialog isn't).
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
            else if (BetterChestsPlugin._placeStacksKey.Down())
            {
                __instance.OnStackAll();
            }
            else if (BetterChestsPlugin._sortKey.Down())
            {
                SortOpenContainer();
            }
        }
    }
}
