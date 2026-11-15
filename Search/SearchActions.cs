using System;
using System.Collections.Generic;

using BetterChests.Core;
using BetterChests.Deposit;

using UnityEngine;

namespace BetterChests.Search;

/// <summary>
/// What the chest panel's buttons do while searching: they work on the whole chest group, not just the open chest.
/// Items going into the chests first top up matching stacks anywhere in the group (25 carrots in another chest are
/// filled up before any go in the open one); whatever is left goes into the open chest. Each chest is asked for
/// first, as when opening it, so a chest another player has open is left out.
/// </summary>
internal static class SearchActions
{
    /// <summary>Take all: every stack the search shows, from whichever chests they are in.</summary>
    internal static void TakeAll()
    {
        Player player = Player.m_localPlayer;
        if (player == null)
        {
            return;
        }

        Dictionary<Container, List<ItemDrop.ItemData>> byChest = [];
        foreach ((Container chest, ItemDrop.ItemData item) in ChestSearch.Results())
        {
            if (!byChest.TryGetValue(chest, out List<ItemDrop.ItemData> items))
            {
                items = [];
                byChest[chest] = items;
            }
            items.Add(item);
        }

        WithChests([.. byChest.Keys], owned =>
        {
            bool moved = false;
            foreach (Container chest in owned)
            {
                Inventory from = chest.GetInventory();
                foreach (ItemDrop.ItemData item in byChest[chest])
                {
                    if (from.ContainsItem(item))
                    {
                        player.GetInventory().MoveItemToThis(from, item);
                        moved = true;
                    }
                }
            }
            Done(player, moved);
        });
    }

    /// <summary>Place stacks: tops up stacks anywhere in the group with the carried items the search matches.</summary>
    internal static void PlaceStacks(Container open)
    {
        Player player = Player.m_localPlayer;
        if (player == null || open == null)
        {
            return;
        }

        WithChests(ChestClusters.GroupOf(open), owned =>
        {
            bool moved = false;
            foreach (ItemDrop.ItemData item in Carried(player, onlyMatching: true))
            {
                moved |= TopUp(item, player.GetInventory(), owned) > 0;
            }
            Done(player, moved);
        });
    }

    /// <summary>
    /// Deposit all: each item tops up matching stacks anywhere in the group, and what is left goes into the open
    /// chest. The hotbar, equipped and marked items stay, as always.
    /// </summary>
    internal static void DepositAll(Container open)
    {
        Player player = Player.m_localPlayer;
        if (player == null || open == null)
        {
            return;
        }

        WithChests(ChestClusters.GroupOf(open), owned =>
        {
            bool moved = false;
            foreach (ItemDrop.ItemData item in Carried(player, onlyMatching: false))
            {
                moved |= Store(item, player.GetInventory(), owned, open);
            }
            Done(player, moved);
        });
    }

    /// <summary>
    /// An item dragged onto the search results: into the group, the same way as Deposit all puts it. A split stack
    /// (Shift + drag) moves only the amount dragged; what doesn't fit anywhere stays in the player's inventory.
    /// </summary>
    internal static void Drop(Container open, Inventory from, ItemDrop.ItemData item, int amount)
    {
        Player player = Player.m_localPlayer;
        if (player == null || open == null || from == null || item == null || from != player.GetInventory())
        {
            return;
        }

        WithChests(ChestClusters.GroupOf(open), owned =>
        {
            if (!from.ContainsItem(item))
            {
                return;
            }
            if (amount <= 0 || amount >= item.m_stack)
            {
                Done(player, Store(item, from, owned, open));
                return;
            }

            // Only part of the stack: move a copy of that much, and put back whatever doesn't fit.
            ItemDrop.ItemData part = item.Clone();
            part.m_stack = amount;
            item.m_stack -= amount;
            bool moved = Store(part, from, owned, open);
            if (part.m_stack > 0)
            {
                item.m_stack += part.m_stack;
            }
            from.Changed();
            Done(player, moved);
        });
    }

    /// <summary>The player's items that Deposit all and Place stacks may move, as without searching.</summary>
    private static List<ItemDrop.ItemData> Carried(Player player, bool onlyMatching)
    {
        List<ItemDrop.ItemData> items = [];
        foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
        {
            if (KeepItems.AlwaysKept(item) || KeepItems.IsMarked(item) || item.m_shared.m_questItem)
            {
                continue;
            }
            if (!onlyMatching || ChestSearch.Matches(item))
            {
                items.Add(item);
            }
        }
        return items;
    }

    /// <summary>Tops up the group's partial stacks of this item, then puts what is left in the open chest.</summary>
    private static bool Store(ItemDrop.ItemData item, Inventory from, List<Container> owned, Container open)
    {
        bool moved = TopUp(item, from, owned) > 0;
        if (item.m_stack <= 0 || !owned.Contains(open))
        {
            return moved;
        }

        // As vanilla "Take all" does: on success the item is now in the chest; on failure what didn't fit is left in
        // item.m_stack, still in the player's inventory.
        Inventory to = open.GetInventory();
        int before = item.m_stack;
        if (to.AddItem(item))
        {
            _ = from.RemoveItem(item);
            moved = true;
        }
        else if (item.m_stack != before)
        {
            moved = true;
        }
        from.Changed();
        to.Changed();
        return moved;
    }

    /// <summary>Moves the item onto partial stacks of the same item in the owned chests. Returns how many moved.</summary>
    private static int TopUp(ItemDrop.ItemData item, Inventory from, List<Container> owned)
    {
        int moved = 0;
        foreach (Container chest in owned)
        {
            Inventory inventory = chest.GetInventory();
            bool changed = false;
            foreach (ItemDrop.ItemData stack in inventory.GetAllItems())
            {
                if (item.m_stack <= 0)
                {
                    break;
                }
                int room = stack.m_shared.m_maxStackSize - stack.m_stack;
                if (room <= 0 || !stack.IsSameType(item))
                {
                    continue;
                }

                int put = Mathf.Min(room, item.m_stack);
                stack.m_stack += put;
                item.m_stack -= put;
                moved += put;
                changed = true;
            }
            if (changed)
            {
                inventory.Changed();
            }
        }

        if (moved > 0)
        {
            if (item.m_stack <= 0)
            {
                _ = from.RemoveItem(item);
            }
            from.Changed();
        }
        return moved;
    }

    /// <summary>Asks for every chest, then acts on the ones this game now owns (the open one first).</summary>
    private static void WithChests(List<Container> chests, Action<List<Container>> act)
    {
        List<Container> owned = [];
        int waiting = chests.Count;
        if (waiting == 0)
        {
            return;
        }

        Container open = InventoryGui.instance != null ? InventoryGui.instance.m_currentContainer : null;
        foreach (Container chest in chests)
        {
            ChestRequests.Request(chest, granted =>
            {
                if (granted)
                {
                    owned.Add(chest);
                }
                if (--waiting == 0)
                {
                    // Other chests' stacks first, so the open chest only takes what didn't fit elsewhere.
                    owned.Sort((a, b) => (a == open).CompareTo(b == open));
                    act(owned);
                    ChestSearch.Refresh();
                }
            });
        }
    }

    private static void Done(Player player, bool moved)
    {
        if (moved && InventoryGui.instance != null)
        {
            _ = InventoryGui.instance.m_moveItemEffects.Create(player.transform.position, Quaternion.identity);
        }
    }
}
