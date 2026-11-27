using System.Collections.Generic;
using System.Linq;

using BetterChests.Core;
using BetterChests.Search;

using UnityEngine;

namespace BetterChests.Sorting;

/// <summary>
/// <para>
/// Sorting a whole group of chests at once (Sort with the Sort Group Modifier held): each item is gathered into a
/// chest of its own as far as the chests allow, then every chest is sorted.
/// </para>
/// <para>
/// Stacks are merged across the group, items filling a chest or more get whole chests (the ones already holding the
/// most of them), and everything else fills the remaining chests in sort order, so similar items end up together
/// and an item is only split between chests when there isn't room otherwise. See <see cref="Plan"/>.
/// </para>
/// </summary>
internal static class GroupSort
{
    /// <summary>
    /// Asks for every chest in the chest's group, then sorts the ones this game gets. Chests another player has open
    /// are left out, and the message says how many.
    /// </summary>
    internal static void SortGroup(Container chest)
    {
        Player player = Player.m_localPlayer;
        if (chest == null || player == null || player.IsTeleporting())
        {
            return;
        }

        List<Container> group = ChestClusters.GroupOf(chest);
        SearchActions.WithChests(group, owned =>
        {
            Arrange(owned);
            foreach (Container member in owned)
            {
                ChestSort.SortContainer(member);
            }

            string count = owned.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int inUse = group.Count - owned.Count;
            string text = inUse > 0
                ? Localization.instance.Localize("$betterchests_sorted_group_in_use", count, inUse.ToString(System.Globalization.CultureInfo.InvariantCulture))
                : Localization.instance.Localize("$betterchests_sorted_group", count);
            player.Message(MessageHud.MessageType.Center, text);
            if (owned.Count > 0 && InventoryGui.instance != null)
            {
                _ = InventoryGui.instance.m_moveItemEffects.Create(player.transform.position, Quaternion.identity);
            }
        });
    }

    /// <summary>
    /// Plans where every stack in the group goes, then puts it there. Stacks are merged across the whole group first.
    /// </summary>
    private static void Arrange(List<Container> chests)
    {
        // How much of each item every chest holds, to keep items in the chests they're already in.
        Dictionary<string, Dictionary<Container, int>> amounts = [];
        List<(Container Chest, List<ItemDrop.ItemData> Items, List<int> Stacks)> before = [];
        List<ItemDrop.ItemData> all = [];
        foreach (Container chest in chests)
        {
            List<ItemDrop.ItemData> items = chest.GetInventory().GetAllItems();
            before.Add((chest, [.. items], [.. items.Select(i => i.m_stack)]));
            foreach (ItemDrop.ItemData item in items)
            {
                string kind = Kind(item);
                if (!amounts.TryGetValue(kind, out Dictionary<Container, int> byChest))
                {
                    byChest = [];
                    amounts[kind] = byChest;
                }
                byChest[chest] = (byChest.TryGetValue(chest, out int held) ? held : 0) + item.m_stack;
                all.Add(item);
            }
        }

        ChestSort.MergeStacks(all);
        Dictionary<Container, List<ItemDrop.ItemData>> plan = Plan(chests, all, amounts);
        if (plan == null || plan.Values.Sum(l => l.Count) != all.Count)
        {
            // Can't happen (merging never adds stacks), but never lose items over it: put everything back.
            BetterChestsPlugin._log.LogError("Sort group: couldn't place every stack, leaving the chests as they were.");
            foreach ((Container chest, List<ItemDrop.ItemData> items, List<int> stacks) in before)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    items[i].m_stack = stacks[i];
                }
                List<ItemDrop.ItemData> list = chest.GetInventory().GetAllItems();
                list.Clear();
                list.AddRange(items);
            }
            return;
        }

        foreach (Container chest in chests)
        {
            Inventory inventory = chest.GetInventory();
            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            items.Clear();
            items.AddRange(plan[chest]);
            int width = inventory.GetWidth();
            for (int i = 0; i < items.Count; i++)
            {
                items[i].m_gridPos = new Vector2i(i % width, i / width);
            }
            inventory.Changed();
        }
    }

    /// <summary>
    /// Which stacks go in which chest. Items needing a chest or more get whole chests, the ones already holding the
    /// most of them first. What's left (smaller items, and what's left over of the big ones) fills the other chests
    /// in sort order, an item moving on to the next chest rather than being split, unless there isn't room for that.
    /// </summary>
    private static Dictionary<Container, List<ItemDrop.ItemData>> Plan(
        List<Container> chests, List<ItemDrop.ItemData> all, Dictionary<string, Dictionary<Container, int>> amounts)
    {
        Dictionary<Container, List<ItemDrop.ItemData>> plan = chests.ToDictionary(c => c, _ => new List<ItemDrop.ItemData>());
        List<(string Kind, List<ItemDrop.ItemData> Stacks)> kinds = [.. all
            .GroupBy(Kind)
            .Select(g => (g.Key, g.OrderByDescending(i => i.m_stack).ToList()))
            .OrderByDescending(k => k.Item2.Count)
            .ThenByDescending(k => k.Item2.Sum(i => i.m_stack))
            .ThenBy(k => k.Key, System.StringComparer.Ordinal)];

        foreach ((string kind, List<ItemDrop.ItemData> stacks) in kinds)
        {
            while (true)
            {
                Container whole = chests
                    .Where(c => plan[c].Count == 0 && Capacity(c) <= stacks.Count)
                    .OrderByDescending(c => amounts[kind].TryGetValue(c, out int held) ? held : 0)
                    .ThenBy(chests.IndexOf)
                    .FirstOrDefault();
                if (whole == null)
                {
                    break;
                }
                int take = Capacity(whole);
                plan[whole].AddRange(stacks.Take(take));
                stacks.RemoveRange(0, take);
            }
        }

        List<List<ItemDrop.ItemData>> rest = [.. kinds
            .Where(k => k.Stacks.Count > 0)
            .Select(k => k.Stacks)
            .OrderBy(s => ItemCategories.KeyOf(s[0]))
            .ThenBy(s => Localization.instance.Localize(s[0].m_shared.m_name), System.StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(s => s[0].m_shared.m_name, System.StringComparer.Ordinal)
            .ThenByDescending(s => s[0].m_quality)];
        List<Container> open = [.. chests.Where(c => plan[c].Count < Capacity(c))];

        return Fill(open, rest, plan, Layout.OwnChest)
            ?? Fill(open, rest, plan, Layout.Pack)
            ?? Fill(open, rest, plan, Layout.Split);
    }

    private enum Layout
    {
        /// <summary>Every item starts a chest of its own.</summary>
        OwnChest = 0,

        /// <summary>Items share chests, one that doesn't fit in the rest of a chest starting the next.</summary>
        Pack = 1,

        /// <summary>Items fill every slot in order, splitting between chests where they must.</summary>
        Split = 2,
    }

    /// <summary>Lays the stacks into the chests in order; null if they run out of chests that way.</summary>
    private static Dictionary<Container, List<ItemDrop.ItemData>> Fill(
        List<Container> open, List<List<ItemDrop.ItemData>> rest, Dictionary<Container, List<ItemDrop.ItemData>> plan, Layout layout)
    {
        Dictionary<Container, List<ItemDrop.ItemData>> result = plan.ToDictionary(e => e.Key, e => new List<ItemDrop.ItemData>(e.Value));
        int at = 0;
        foreach (List<ItemDrop.ItemData> stacks in rest)
        {
            if (layout != Layout.Split)
            {
                while (at < open.Count
                    && ((layout == Layout.OwnChest && result[open[at]].Count > 0)
                        || Capacity(open[at]) - result[open[at]].Count < stacks.Count))
                {
                    at++;
                }
            }

            foreach (ItemDrop.ItemData stack in stacks)
            {
                while (at < open.Count && result[open[at]].Count >= Capacity(open[at]))
                {
                    at++;
                }
                if (at >= open.Count)
                {
                    return null;
                }
                result[open[at]].Add(stack);
            }
        }
        return result;
    }

    private static int Capacity(Container chest)
    {
        Inventory inventory = chest.GetInventory();
        return inventory.GetWidth() * inventory.GetHeight();
    }

    /// <summary>
    /// Stacks of the same item, quality and world level count as one item, as when gathering search results.
    /// </summary>
    private static string Kind(ItemDrop.ItemData item)
    {
        return item.m_shared.m_name + "|" + item.m_quality + "|" + item.m_worldLevel;
    }
}
