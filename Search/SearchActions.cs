using System;
using System.Collections.Generic;
using System.Linq;

using BetterChests.Core;
using BetterChests.Deposit;
using BetterChests.Sorting;

using UnityEngine;

namespace BetterChests.Search;

/// <summary>
/// What the chest panel's buttons do while searching: they work on the whole chest group, not just the open chest.
/// Items going into the chests first top up matching stacks anywhere in the group (25 carrots in another chest are
/// filled up before any go in the open one); whatever is left goes into the open chest. Each chest is asked for
/// first, as when opening it, so a chest another player has open is left out. Sort gathers each item the search
/// shows into the chest that holds the most of it.
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
                moved |= Store(item, player.GetInventory(), owned, open, out _);
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
                Done(player, Store(item, from, owned, open, out _));
                return;
            }

            // Only part of the stack: move a copy of that much, and put back only what didn't fit anywhere.
            ItemDrop.ItemData part = item.Clone();
            part.m_stack = amount;
            item.m_stack -= amount;
            bool moved = Store(part, from, owned, open, out int left);
            item.m_stack += left;
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

    /// <summary>
    /// Puts an item into the group: first onto partial stacks of it anywhere, then into free slots in the other chests
    /// that already hold it (so it joins the rest of its kind), and only then into the open chest.
    /// <paramref name="left"/> is how many of the item didn't fit anywhere.
    /// </summary>
    private static bool Store(ItemDrop.ItemData item, Inventory from, List<Container> owned, Container open, out int left)
    {
        bool moved = TopUp(item, from, owned) > 0;
        left = Mathf.Max(0, item.m_stack);
        foreach (Container chest in owned)
        {
            if (left <= 0)
            {
                return moved;
            }
            if (chest != open && Holds(chest.GetInventory(), item))
            {
                moved |= MoveInto(item, from, chest.GetInventory(), out left);
            }
        }

        if (left > 0 && owned.Contains(open))
        {
            moved |= MoveInto(item, from, open.GetInventory(), out left);
        }
        return moved;
    }

    private static bool Holds(Inventory inventory, ItemDrop.ItemData item)
    {
        foreach (ItemDrop.ItemData stack in inventory.GetAllItems())
        {
            if (stack != item && stack.IsSameType(item))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Vanilla <c>Inventory.AddItem</c>, used safely. True from it means all of the item was placed: either the item
    /// itself went into <paramref name="to"/>, or all of it was merged onto stacks there, which leaves its m_stack as it
    /// was. So on true nothing is left over, whatever m_stack says, and the item comes out of <paramref name="from"/>.
    /// False means what didn't fit is left in m_stack. (Reading m_stack after a true is what duplicated split stacks.)
    /// </summary>
    internal static bool MoveInto(ItemDrop.ItemData item, Inventory from, Inventory to, out int left)
    {
        int before = item.m_stack;
        bool moved;
        if (to.AddItem(item))
        {
            _ = from.RemoveItem(item);
            left = 0;
            moved = true;
        }
        else
        {
            left = item.m_stack;
            moved = left != before;
        }
        from.Changed();
        to.Changed();
        return moved;
    }

    /// <summary>
    /// Sort: for each item the search shows, every stack of it in the group moves into the chest holding the most of
    /// it (as far as it fits), and then each chest that took items is sorted.
    /// </summary>
    internal static void Gather()
    {
        Player player = Player.m_localPlayer;
        List<(Container Chest, ItemDrop.ItemData Item)> results = ChestSearch.Results();
        if (player == null || results.Count == 0)
        {
            return;
        }

        List<Container> chests = [.. results.Select(r => r.Chest).Distinct()];
        WithChests(chests, owned =>
        {
            HashSet<Container> sorted = [];
            bool moved = false;
            foreach (IGrouping<string, (Container Chest, ItemDrop.ItemData Item)> kind in results
                .Where(r => owned.Contains(r.Chest))
                .GroupBy(r => r.Item.m_shared.m_name + "|" + r.Item.m_quality + "|" + r.Item.m_worldLevel))
            {
                Container target = kind
                    .GroupBy(r => r.Chest)
                    .OrderByDescending(g => g.Sum(r => r.Item.m_stack))
                    .First().Key;
                Inventory to = target.GetInventory();
                foreach ((Container chest, ItemDrop.ItemData item) in kind)
                {
                    Inventory from = chest.GetInventory();
                    if (chest != target && from.ContainsItem(item))
                    {
                        moved |= MoveInto(item, from, to, out _);
                    }
                }
                _ = sorted.Add(target);
            }

            foreach (Container chest in sorted)
            {
                ChestSort.SortContainer(chest);
            }
            Done(player, moved || sorted.Count > 0);
        });
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
    internal static void WithChests(List<Container> chests, Action<List<Container>> act)
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
