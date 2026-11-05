using System.Collections.Generic;

using BepInEx.Configuration;

using HarmonyLib;

using UnityEngine;

namespace BetterChests;

/// <summary>
/// Dropping an item near chests stores it in them instead of on the ground. Chests that already hold
/// the item come first, nearest first, then any other chest with room. Whatever doesn't fit is dropped
/// on the ground as normal.
/// </summary>
internal static class AutoDeposit
{
    private static ConfigEntry<bool> _enabled;
    private static ConfigEntry<float> _radius;

    private static readonly List<Container> Containers = [];

    public static void BindConfig(ConfigFile config)
    {
        _enabled = config.Bind("Auto Deposit", "Enabled", true,
            "Dropping an item near chests stores it in them instead of dropping it on the ground.");
        _radius = config.Bind("Auto Deposit", "Radius", 10f,
            new ConfigDescription("Distance in metres from the player within which chests are used for dropped items.",
                new AcceptableValueRange<float>(1f, 50f)));
    }

    /// <summary>
    /// Stores up to <paramref name="amount"/> of <paramref name="item"/> from <paramref name="from"/>
    /// in nearby chests, and returns how many were stored.
    /// </summary>
    private static int Deposit(Player player, Inventory from, ItemDrop.ItemData item, int amount)
    {
        List<Container> targets = GetTargets(player, item);

        int capacity = 0;
        foreach (Container container in targets)
        {
            capacity += SpaceFor(container.GetInventory(), item, amount - capacity);
            if (capacity >= amount)
            {
                break;
            }
        }

        if (capacity <= 0)
        {
            return 0;
        }

        // Same as dropping: the item leaves the player's hands first.
        player.RemoveEquipAction(item);
        player.UnequipItem(item, triggerEquipEffects: false);
        if (player.m_hiddenLeftItem == item || player.m_hiddenRightItem == item)
        {
            if (player.m_hiddenLeftItem == item)
            {
                player.m_hiddenLeftItem = null;
            }

            if (player.m_hiddenRightItem == item)
            {
                player.m_hiddenRightItem = null;
            }

            player.SetupVisEquipment(player.m_visEquipment, isRagdoll: false);
        }

        // The look is copied before the stack shrinks below, as it may be removed entirely.
        ItemDrop.ItemData look = item.Clone();
        string itemName = item.m_shared.m_name;
        Sprite icon = item.GetIcon();
        int stored = 0;
        foreach (Container container in targets)
        {
            int remaining = amount - stored;
            if (remaining <= 0)
            {
                break;
            }

            Inventory inventory = container.GetInventory();
            int fits = SpaceFor(inventory, item, remaining);
            if (fits <= 0)
            {
                continue;
            }

            // Only the owner's changes are saved, so take ownership of chests held by others.
            if (!container.IsOwner())
            {
                container.m_nview.ClaimOwnership();
            }

            ItemDrop.ItemData moved = item.Clone();
            moved.m_stack = fits;
            // On failure AddItem leaves the part it couldn't place in moved.m_stack.
            int added = inventory.AddItem(moved) ? fits : fits - moved.m_stack;
            if (added <= 0)
            {
                continue;
            }

            _ = from.RemoveItem(item, added);
            stored += added;

            // The item is stored already; this only shows where it went.
            string chestName = container.GetHoverName();
            DepositEffect.Spawn(player, look, container, () => OnArrive(container, itemName, chestName, added, icon));
        }

        if (stored > 0)
        {
            // Same animation and sound as a vanilla drop.
            player.m_zanim.SetTrigger("interact");
            _ = player.m_dropEffects.Create(player.transform.position, Quaternion.identity, null, 1f, -1, player.GetZDOID());
        }

        return stored;
    }

    private static void OnArrive(Container container, string itemName, string chestName, int amount, Sprite icon)
    {
        if (container != null && InventoryGui.instance != null)
        {
            _ = InventoryGui.instance.m_moveItemEffects.Create(container.transform.position, Quaternion.identity);
        }

        if (Player.m_localPlayer != null)
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Stored " + itemName + " in " + chestName, amount, icon);
        }
    }

    /// <summary>
    /// Chests within range that can take some of the item: those that already hold it first, then the
    /// rest, each group nearest first.
    /// </summary>
    private static List<Container> GetTargets(Player player, ItemDrop.ItemData item)
    {
        Vector3 pos = player.transform.position;
        float maxSqr = _radius.Value * _radius.Value;

        List<Container> nearby = [];
        _ = Containers.RemoveAll(c => c == null);
        foreach (Container container in Containers)
        {
            if ((container.transform.position - pos).sqrMagnitude <= maxSqr && CanDepositInto(container))
            {
                nearby.Add(container);
            }
        }

        nearby.Sort((a, b) =>
            (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude));

        List<Container> targets = [];
        List<Container> others = [];
        foreach (Container container in nearby)
        {
            (container.GetInventory().ContainsItemByName(item.m_shared.m_name) ? targets : others).Add(container);
        }

        targets.AddRange(others);
        return targets;
    }

    /// <summary>
    /// Only player-built chests that the player could open normally and that nobody else is using.
    /// </summary>
    private static bool CanDepositInto(Container container)
    {
        if (container.GetInventory() == null
            || container.m_wagon != null
            || container.m_autoDestroyEmpty
            || container.GetComponent<TombStone>() != null
            || container.GetComponent<Piece>() == null)
        {
            return false;
        }

        ZNetView nview = container.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return false;
        }

        // The chest we have open ourselves is fine; one someone else has open is not.
        bool openHere = InventoryGui.instance != null && InventoryGui.instance.m_currentContainer == container;
        bool free = openHere || (!container.IsInUse() && nview.GetZDO().GetInt(ZDOVars.s_inUse) != 1);
        bool allowed = container.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID());
        bool warded = container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position, 0f, false);
        return free && allowed && !warded;
    }

    /// <summary>
    /// How many of the item (up to <paramref name="amount"/>) the inventory can take, matching
    /// <see cref="Inventory.AddItem(ItemDrop.ItemData)"/>: it tops up matching stacks, then puts the rest in
    /// one empty slot.
    /// </summary>
    private static int SpaceFor(Inventory inventory, ItemDrop.ItemData item, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        if (inventory.HaveEmptySlot())
        {
            return amount;
        }

        if (item.m_shared.m_maxStackSize <= 1)
        {
            return 0;
        }

        int free = 0;
        foreach (ItemDrop.ItemData other in inventory.GetAllItems())
        {
            if (other.m_shared.m_name == item.m_shared.m_name
                && other.m_quality == item.m_quality
                && other.m_worldLevel == item.m_worldLevel)
            {
                free += Mathf.Max(0, other.m_shared.m_maxStackSize - other.m_stack);
            }
        }

        return Mathf.Min(free, amount);
    }

    [HarmonyPatch(typeof(Container), "Awake")]
    private static class Container_Awake_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Container __instance)
        {
            if (__instance.GetInventory() != null && !Containers.Contains(__instance))
            {
                Containers.Add(__instance);
            }
        }
    }

    /// <summary>
    /// Every way of dropping an item from the inventory (dragging it out, Ctrl+click) ends up here.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem))]
    private static class Humanoid_DropItem_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, ref int amount, ref bool __result)
        {
            if (_enabled == null || !_enabled.Value
                || __instance != Player.m_localPlayer
                || item == null || amount <= 0 || item.m_shared.m_questItem)
            {
                return true;
            }

            Player player = Player.m_localPlayer;
            inventory ??= player.GetInventory();

            // Items dragged out of a chest are dropped as normal; storing them would put them straight back.
            if (inventory != player.GetInventory() || !inventory.ContainsItem(item))
            {
                return true;
            }

            int stored = Deposit(player, inventory, item, Mathf.Min(amount, item.m_stack));
            if (stored <= 0)
            {
                return true;
            }

            // Let the game drop whatever didn't fit.
            amount -= stored;
            if (amount > 0 && inventory.ContainsItem(item))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }
}
