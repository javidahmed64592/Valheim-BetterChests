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
/// Each item gets a home chest, the items with the most in the group choosing first: the chest that holds the most of
/// it and isn't already another item's home, or else an empty chest, or else (sharing) the chest that holds the most
/// of it. Every stack then moves to its home as far as it fits; what doesn't fit stays where it is. Moving twice lets
/// items use the space other items left behind in their home.
/// </para>
/// </summary>
internal static class GroupSort
{
    private const int Passes = 2;

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

    private static void Arrange(List<Container> chests)
    {
        // How much of each item every chest holds.
        Dictionary<string, Dictionary<Container, int>> amounts = [];
        foreach (Container chest in chests)
        {
            foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
            {
                string kind = Kind(item);
                if (!amounts.TryGetValue(kind, out Dictionary<Container, int> byChest))
                {
                    byChest = [];
                    amounts[kind] = byChest;
                }

                byChest[chest] = (byChest.TryGetValue(chest, out int held) ? held : 0) + item.m_stack;
            }
        }

        List<string> kinds = [.. amounts.Keys
            .OrderByDescending(k => amounts[k].Values.Sum())
            .ThenBy(k => k, System.StringComparer.Ordinal)];

        Dictionary<string, Container> homes = [];
        HashSet<Container> claimed = [];
        foreach (string kind in kinds)
        {
            List<Container> holders = [.. amounts[kind]
                .OrderByDescending(e => e.Value)
                .ThenBy(e => chests.IndexOf(e.Key))
                .Select(e => e.Key)];

            Container home = holders.FirstOrDefault(c => !claimed.Contains(c))
                ?? chests.FirstOrDefault(c => !claimed.Contains(c) && c.GetInventory().NrOfItems() == 0);
            if (home != null)
            {
                _ = claimed.Add(home);
            }
            else
            {
                home = holders[0];
            }

            homes[kind] = home;
        }

        for (int pass = 0; pass < Passes; pass++)
        {
            foreach (string kind in kinds)
            {
                Container home = homes[kind];
                Inventory to = home.GetInventory();
                foreach (Container chest in chests)
                {
                    if (chest == home)
                    {
                        continue;
                    }

                    Inventory from = chest.GetInventory();
                    foreach (ItemDrop.ItemData item in from.GetAllItems().Where(i => Kind(i) == kind).ToList())
                    {
                        _ = SearchActions.MoveInto(item, from, to, out _);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Stacks of the same item, quality and world level count as one item, as when gathering search results.
    /// </summary>
    private static string Kind(ItemDrop.ItemData item)
    {
        return item.m_shared.m_name + "|" + item.m_quality + "|" + item.m_worldLevel;
    }
}
