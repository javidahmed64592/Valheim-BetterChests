using System.Collections.Generic;
using System.Linq;

using BetterChests.UI;

using UnityEngine;

namespace BetterChests.Deposit;

/// <summary>
/// <para>
/// Items in the player's inventory can be marked to be kept by "Deposit all" and "Place stacks", by holding the keep key
/// (Alt) and right-clicking them. The mark is stored in the item's custom data, so it is saved with the
/// item and follows it around the inventory. Marked items get a frame around their slot.
/// </para>
/// <para>
/// Only items that Deposit all would otherwise take can be marked: not the hotbar, and not equipped items.
/// </para>
/// </summary>
internal static class KeepItems
{
    private const string DataKey = "BetterChests_Keep";

    /// <summary>
    /// The row of the player's inventory that the hotbar shows.
    /// </summary>
    internal const int HotbarRow = 0;

    private const string FrameName = "BetterChests_KeepFrame";
    private static readonly Color FrameColour = new(1f, 0.76f, 0.3f, 0.95f);

    /// <summary>
    /// True for items that are never deposited anyway, so marking them means nothing.
    /// </summary>
    internal static bool AlwaysKept(ItemDrop.ItemData item)
    {
        return item.m_gridPos.y == HotbarRow || item.m_equipped;
    }

    internal static bool IsMarked(ItemDrop.ItemData item)
    {
        return item.m_customData != null && item.m_customData.ContainsKey(DataKey);
    }

    private static void Toggle(ItemDrop.ItemData item)
    {
        if (!item.m_customData.Remove(DataKey))
        {
            item.m_customData[DataKey] = "1";
        }
    }

    private static bool IsPlayerGrid(InventoryGrid grid)
    {
        InventoryGui gui = InventoryGui.instance;
        return gui != null && grid == gui.m_playerGrid
            && Player.m_localPlayer != null && grid.m_inventory == Player.m_localPlayer.GetInventory();
    }

    /// <summary>
    /// Keep key + right-click marks or unmarks the item instead of using it. Called before
    /// <c>InventoryGrid.OnRightDown</c>; true if the click was used here, so vanilla should skip it.
    /// </summary>
    internal static bool TryHandleRightClick(InventoryGrid grid, UIInputHandler element)
    {
        if (!BetterChestsPlugin._keepItemKey.Held() || !IsPlayerGrid(grid))
        {
            return false;
        }

        Vector2i pos = grid.GetButtonPos(element.gameObject);
        ItemDrop.ItemData item = grid.m_inventory.GetItemAt(pos.x, pos.y);
        if (item == null)
        {
            return true;
        }

        if (AlwaysKept(item))
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$betterchests_always_kept");
            return true;
        }

        Toggle(item);
        return true;
    }

    /// <summary>
    /// The player's items that "Place stacks" may move: everything except the hotbar, equipped items and marked
    /// items, the same as "Deposit all" keeps. Vanilla placed hotbar items too, so a mead in the hotbar went into a
    /// chest holding meads, although marking it says hotbar items are never deposited.
    /// </summary>
    internal static List<ItemDrop.ItemData> ItemsToStack(Inventory fromInventory)
    {
        List<ItemDrop.ItemData> items = fromInventory.GetAllItems();
        return Player.m_localPlayer == null || fromInventory != Player.m_localPlayer.GetInventory()
            ? items
            : [.. items.Where(item => !IsMarked(item) && !AlwaysKept(item))];
    }



    /// <summary>
    /// Shows or hides the frames each time vanilla redraws the player's inventory.
    /// </summary>
    internal static void OnGridUpdated(InventoryGrid grid)
    {
        if (!IsPlayerGrid(grid))
        {
            return;
        }

        foreach (InventoryElement element in grid.m_elements)
        {
            ItemDrop.ItemData item = grid.m_inventory.GetItemAt(element.Position.x, element.Position.y);
            bool show = item != null && IsMarked(item) && !AlwaysKept(item);
            SlotFrame.Show(element, FrameName, FrameColour, show);
        }
    }
}
