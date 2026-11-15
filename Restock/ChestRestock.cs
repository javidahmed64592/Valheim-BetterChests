using System.Collections.Generic;
using System.Globalization;

using BetterChests.Core;

using UnityEngine;

namespace BetterChests.Restock;

/// <summary>
/// Tops up every partial stack the player carries (arrows, food, meads, materials, the hotbar included) from the chest
/// they are at and every chest grouped with it. Each chest is asked for first, as when opening it, so chests another
/// player has open are skipped.
/// </summary>
internal static class ChestRestock
{
    internal static void Restock(Container chest, Player player)
    {
        if (chest == null || player == null || player.IsTeleporting())
        {
            return;
        }

        if (!HasPartialStacks(player.GetInventory()))
        {
            player.Message(MessageHud.MessageType.Center, "$betterchests_restock_full");
            return;
        }

        List<Container> group = ChestClusters.GroupOf(chest);
        int waiting = group.Count;
        int moved = 0;
        int skipped = 0;
        foreach (Container member in group)
        {
            ChestRequests.Request(member, granted =>
            {
                if (granted)
                {
                    moved += TakeFrom(member.GetInventory(), player.GetInventory());
                }
                else
                {
                    skipped++;
                }

                waiting--;
                if (waiting == 0)
                {
                    Report(player, moved, skipped);
                }
            });
        }
    }

    private static bool HasPartialStacks(Inventory inventory)
    {
        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
        {
            if (IsPartial(item))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsPartial(ItemDrop.ItemData item)
    {
        return item.m_shared.m_maxStackSize > 1 && item.m_stack < item.m_shared.m_maxStackSize;
    }

    /// <summary>Moves what fits onto the player's partial stacks out of a chest this game owns. Returns how many items moved.</summary>
    private static int TakeFrom(Inventory chest, Inventory carried)
    {
        int moved = 0;
        foreach (ItemDrop.ItemData stack in carried.GetAllItems())
        {
            if (!IsPartial(stack))
            {
                continue;
            }

            foreach (ItemDrop.ItemData source in new List<ItemDrop.ItemData>(chest.GetAllItems()))
            {
                int room = stack.m_shared.m_maxStackSize - stack.m_stack;
                if (room <= 0)
                {
                    break;
                }
                if (source.m_stack <= 0 || !stack.IsSameType(source))
                {
                    continue;
                }

                int take = Mathf.Min(room, source.m_stack);
                stack.m_stack += take;
                source.m_stack -= take;
                moved += take;
                if (source.m_stack <= 0)
                {
                    _ = chest.RemoveItem(source);
                }
            }
        }

        if (moved > 0)
        {
            chest.Changed();
            carried.Changed();
        }
        return moved;
    }

    private static void Report(Player player, int moved, int skipped)
    {
        string text = moved > 0
            ? Localization.instance.Localize("$betterchests_restocked", moved.ToString(CultureInfo.InvariantCulture))
            : Localization.instance.Localize("$betterchests_restock_nothing");
        if (skipped > 0)
        {
            text += "\n" + Localization.instance.Localize("$betterchests_restock_skipped", skipped.ToString(CultureInfo.InvariantCulture));
        }
        player.Message(MessageHud.MessageType.Center, text);

        if (moved > 0 && InventoryGui.instance != null)
        {
            _ = InventoryGui.instance.m_moveItemEffects.Create(player.transform.position, Quaternion.identity);
        }
    }
}
