using System.Collections.Generic;

using UnityEngine;

namespace BetterChests.Deposit;

/// <summary>
/// The opposite of "Take all": moves everything from the player's inventory into the open chest, except
/// the hotbar (the inventory's top row), anything equipped, and items marked to be kept (see
/// <see cref="KeepItems"/>).
/// </summary>
internal static class DepositAll
{
    internal static void DepositIntoOpenContainer()
    {
        InventoryGui gui = InventoryGui.instance;
        Container container = gui != null ? gui.m_currentContainer : null;
        Player player = Player.m_localPlayer;
        if (container == null || !container.IsOwner() || player == null || player.IsTeleporting())
        {
            return;
        }

        // Drop whatever is being dragged back where it came from, like the vanilla chest buttons do.
        gui.SetupDragItem(null, null, 1);

        Inventory from = player.GetInventory();
        Inventory to = container.GetInventory();
        bool moved = false;
        foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(from.GetAllItems()))
        {
            if (KeepItems.AlwaysKept(item) || KeepItems.IsMarked(item) || item.m_shared.m_questItem)
            {
                continue;
            }

            // As vanilla "Take all" does: on success the item is now in the chest; on failure what didn't
            // fit is left in item.m_stack, still in the player's inventory.
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
        }

        if (!moved)
        {
            return;
        }

        from.Changed();
        to.Changed();
        _ = gui.m_moveItemEffects.Create(gui.transform.position, Quaternion.identity);
    }
}
