using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace BetterChests;

/// <summary>
/// Sorting the open chest (from the "Sort" button or key, see <see cref="ChestButtons"/>). Partial
/// stacks are merged, then items are grouped by type (materials, food, ammo, weapons, armour and so on),
/// with the item the chest holds most of first in each group.
/// </summary>
internal static class ChestSort
{
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
}
