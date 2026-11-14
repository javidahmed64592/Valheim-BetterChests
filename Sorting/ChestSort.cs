using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace BetterChests.Sorting;

/// <summary>
/// Sorting the open chest (from the "Sort" button or key, see <see cref="ChestButtons"/>). Partial
/// stacks are merged, then items are grouped (wood, stone, ores, metals, seeds, crops, food, gear and so
/// on, see <see cref="ItemCategories"/>), in game progression order within each group.
/// </summary>
internal static class ChestSort
{
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

        List<ItemDrop.ItemData> sorted = [.. items
            .Select(i => (Item: i, Key: ItemCategories.SortKey(i)))
            .OrderBy(e => e.Key.Group)
            .ThenBy(e => e.Key.Index)
            .ThenBy(e => Localization.instance.Localize(e.Item.m_shared.m_name), System.StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(e => e.Item.m_shared.m_name, System.StringComparer.Ordinal)
            .ThenByDescending(e => e.Item.m_quality)
            .ThenByDescending(e => e.Item.m_stack)
            .Select(e => e.Item)];

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
}
