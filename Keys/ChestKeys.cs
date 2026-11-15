using BepInEx.Configuration;

using BetterChests.Core;
using BetterChests.Deposit;
using BetterChests.Restock;
using BetterChests.Sorting;
using BetterChests.UI;

using UnityEngine;

namespace BetterChests.Keys;

/// <summary>
/// The chest keys (take all, place stacks, deposit all and sort) also work while looking at a chest, without opening
/// it. The same chests can be used as by opening them: not other players' private chests, chests in someone else's
/// ward, or a chest another player has open. The chest's owner hands it over first, as when opening it, so the
/// change is saved however far its owner is.
/// </summary>
internal static class ChestKeys
{
    private enum Action
    {
        None = 0,
        TakeAll = 1,
        PlaceStacks = 2,
        DepositAll = 3,
        Sort = 4,
        Restock = 5,
    }

    private static ConfigEntry<bool> _enabled;
    private static ConfigEntry<bool> _hoverHints;

    internal static void BindConfig(ConfigFile config)
    {
        _enabled = config.Bind("Chest Keys", "Enabled", true,
            "The chest keys (take all, place stacks, deposit all, sort) also work while looking at a chest, without opening it.");
        _hoverHints = config.Bind("Chest Keys", "Hover Hints", true,
            "Show the chest keys in a chest's hover text.");
    }

    /// <summary>
    /// Called every frame, straight after the game updates its buttons. If a chest key was pressed while looking at a
    /// chest, asks for the chest and does the action, and returns true so the key is kept from other uses this frame.
    /// </summary>
    internal static bool TryUse()
    {
        Player player = Player.m_localPlayer;
        Action action = PressedAction();
        if (action == Action.None)
        {
            return false;
        }

        Container chest = LookedAtChest(player);
        if (chest == null)
        {
            return false;
        }

        if (action == Action.Restock)
        {
            ChestRestock.Restock(chest, player); // asks for each chest in the group itself
            return true;
        }

        ChestRequests.Request(chest, granted =>
        {
            if (!granted)
            {
                player.Message(MessageHud.MessageType.Center, "$msg_inuse");
                return;
            }
            Do(action, chest, player);
        });
        return true;
    }

    /// <summary>The chest the player is looking at, if the keys may use it; null otherwise.</summary>
    private static Container LookedAtChest(Player player)
    {
        // TakeInput is false while a menu, the inventory (with or without a chest open), chat or a text box is open,
        // and while dead or teleporting.
        if (_enabled == null || !_enabled.Value || player == null || !player.TakeInput())
        {
            return null;
        }

        GameObject hovering = player.GetHoverObject();
        Container chest = hovering != null ? hovering.GetComponentInParent<Container>() : null;
        return chest != null && ChestClusters.IsChest(chest) && ChestClusters.CanAccess(chest) ? chest : null;
    }

    private static Action PressedAction()
    {
        if (BetterChestsPlugin._takeAllKey.Down())
        {
            return Action.TakeAll;
        }
        else if (BetterChestsPlugin._placeStacksKey.Down())
        {
            return Action.PlaceStacks;
        }
        else if (BetterChestsPlugin._depositAllKey.Down())
        {
            return Action.DepositAll;
        }
        else if (BetterChestsPlugin._sortKey.Down())
        {
            return Action.Sort;
        }
        else if (BetterChestsPlugin._restockKey.Down())
        {
            return Action.Restock;
        }
        return Action.None;
    }

    /// <summary>Does what the chest panel's button does, on a chest this game now owns.</summary>
    private static void Do(Action action, Container chest, Player player)
    {
        if (chest == null || !chest.m_nview.IsValid() || player == null || player.IsTeleporting())
        {
            return;
        }

        Inventory inventory = chest.GetInventory();
        bool changed;
        switch (action)
        {
            case Action.TakeAll:
                int before = inventory.NrOfItems();
                player.GetInventory().MoveAll(inventory);
                changed = inventory.NrOfItems() != before;
                break;
            case Action.PlaceStacks:
                changed = inventory.StackAll(player.GetInventory(), message: true) > 0;
                break;
            case Action.DepositAll:
                changed = DepositAll.DepositInto(chest, player);
                break;
            case Action.Sort:
                ChestSort.SortContainer(chest);
                changed = true;
                break;
            default:
                return;
        }

        if (changed && InventoryGui.instance != null)
        {
            _ = InventoryGui.instance.m_moveItemEffects.Create(chest.transform.position, Quaternion.identity);
        }
    }

    /// <summary>
    /// The keys in a chest's hover text, laid out like the chest panel's buttons: take all and place stacks, then
    /// deposit all and sort. Only for chests the keys may use.
    /// </summary>
    internal static string HoverHints(Container chest)
    {
        return _enabled == null || !_enabled.Value || !_hoverHints.Value
            || !ChestClusters.IsChest(chest) || !ChestClusters.CanAccess(chest)
            ? ""
            : "\n" + Hint(BetterChestsPlugin._takeAllKey, "$betterchests_hint_take_all")
            + "  " + Hint(BetterChestsPlugin._placeStacksKey, "$betterchests_hint_place_stacks")
            + "\n" + Hint(BetterChestsPlugin._depositAllKey, "$betterchests_deposit_all")
            + "  " + Hint(BetterChestsPlugin._sortKey, "$betterchests_sort")
            + "\n" + Hint(BetterChestsPlugin._restockKey, "$betterchests_hint_restock");
    }

    private static string Hint(Keybind key, string label)
    {
        return "[<color=yellow><b>" + KeyHintUI.KeyName(key.Key) + "</b></color>] " + label;
    }
}
