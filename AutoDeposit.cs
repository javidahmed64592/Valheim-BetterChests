using System.Collections.Generic;

using BepInEx.Configuration;

using HarmonyLib;

using UnityEngine;

namespace BetterChests;

/// <summary>
/// Items the player drops near chests are stored in them. The item is dropped as normal first and lies on
/// the ground for a few seconds, so anyone can still pick it up. Then, if it landed near a chest the player
/// threw it towards, it is stored in that chest or the chests grouped with it. Chests that already hold the
/// item come first, nearest first, then any other chest with room. Whatever doesn't fit stays on the ground.
/// </summary>
internal static class AutoDeposit
{
    /// <summary>
    /// How long past the delay to wait for a still moving item to come to rest before storing it anyway.
    /// </summary>
    private const float MaxSettleTime = 3f;
    private const float SettledSpeedSqr = 0.25f;

    /// <summary>
    /// The size assumed for a chest without a collider.
    /// </summary>
    private static readonly Vector3 FallbackChestSize = Vector3.one;

    private static ConfigEntry<bool> _enabled;
    private static ConfigEntry<float> _radius;
    private static ConfigEntry<float> _clusterGap;
    private static ConfigEntry<float> _throwAngle;
    private static ConfigEntry<float> _delay;

    private static readonly List<Container> Containers = [];

    /// <summary>
    /// Set while the local player drops an item from their own inventory, so the dropped item can be watched.
    /// </summary>
    private static Player _dropper;

    public static void BindConfig(ConfigFile config)
    {
        _enabled = config.Bind("Auto Deposit", "Enabled", true,
            "Dropping an item near chests stores it in them instead of leaving it on the ground.");
        _radius = config.Bind("Auto Deposit", "Radius", 4f,
            new ConfigDescription(
                "How close in metres a dropped item must land to a chest for it to be stored. (Synced with server)",
                new AcceptableValueRange<float>(1f, 12f), Synced()));
        _clusterGap = config.Bind("Auto Deposit", "Cluster Gap", 1f,
            new ConfigDescription(
                "Chests at most this far apart in metres (edge to edge) count as one group: an item that lands " +
                "near one of them can go into any of them. 0 groups only chests that touch. (Synced with server)",
                new AcceptableValueRange<float>(0f, 4f), Synced()));
        _throwAngle = config.Bind("Auto Deposit", "Throw Angle", 60f,
            new ConfigDescription(
                "Only chests within this many degrees of the direction you face when dropping an item are used, " +
                "so items thrown away from a chest stay on the ground. 180 uses chests in every direction.",
                new AcceptableValueRange<float>(10f, 180f)));
        _delay = config.Bind("Auto Deposit", "Delay", 3f,
            new ConfigDescription(
                "Seconds a dropped item lies on the ground before it is stored, so other players can pick it up. (Synced with server)",
                new AcceptableValueRange<float>(0.5f, 10f), Synced()));
    }

    /// <summary>Settings that use the server's value when the server has the mod.</summary>
    private static ConfigurationManagerAttributes Synced()
    {
        return new ConfigurationManagerAttributes { IsAdminOnly = true };
    }

    /// <summary>
    /// How long to wait for a chest's owner to answer before giving up on that chest.
    /// </summary>
    private const float RequestTimeout = 5f;

    /// <summary>
    /// A dropped item on its way into a group of chests, one chest at a time.
    /// </summary>
    private sealed class Deposit
    {
        public ItemDrop Drop;
        public Queue<Container> Targets;
        public ItemDrop.ItemData Look;
        public Sprite Icon;
        public Vector3 Position;
        public Quaternion Rotation;
        public string ItemName;
        public int Stored;
        public float AskedAt;
    }

    /// <summary>
    /// Deposits waiting for a chest's owner to hand the chest over.
    /// </summary>
    private static readonly Dictionary<Container, List<Deposit>> Awaiting = [];

    /// <summary>
    /// Chests the player has asked to open themselves while a deposit was waiting on them.
    /// </summary>
    private static readonly HashSet<Container> OpenedByPlayer = [];

    /// <summary>
    /// Starts storing the dropped item in the chests it landed near.
    /// </summary>
    private static void TryDeposit(ItemDrop drop, Vector3 throwFrom, Vector3 throwDir)
    {
        ZNetView nview = drop.m_nview;
        if (!_enabled.Value || Player.m_localPlayer == null || nview == null || !nview.IsValid())
        {
            return;
        }

        ItemDrop.ItemData item = drop.m_itemData;
        string itemName = item.m_shared.m_name;

        // Someone else has asked for the item (to pick it up), so it is theirs now.
        if (!nview.IsOwner())
        {
            Log(itemName, "another player now owns it");
            return;
        }

        if (item.m_stack <= 0)
        {
            return;
        }

        List<Container> targets = GetTargets(drop.transform.position, throwFrom, throwDir, item);
        if (targets.Count == 0)
        {
            Log(itemName, "no usable chest within range in the direction it was thrown");
            return;
        }

        Next(new Deposit
        {
            Drop = drop,
            Targets = new Queue<Container>(targets),
            // The look is copied before the stack shrinks.
            Look = item.Clone(),
            Icon = item.GetIcon(),
            Position = drop.transform.position,
            Rotation = drop.transform.rotation,
            ItemName = itemName,
        });
    }

    /// <summary>
    /// Asks the next chest with room to take the item. Only a chest's owner may change it, so, like opening a
    /// chest yourself, its owner is asked to hand it over; they refuse if someone has it open.
    /// </summary>
    private static void Next(Deposit deposit)
    {
        if (!StillOurs(deposit))
        {
            return;
        }

        while (deposit.Targets.Count > 0)
        {
            Container container = deposit.Targets.Dequeue();
            if (container == null || !container.m_nview.IsValid() || !CanDepositInto(container)
                || SpaceFor(container.GetInventory(), deposit.Drop.m_itemData, deposit.Drop.m_itemData.m_stack) <= 0)
            {
                continue;
            }

            ForgetStaleRequests();
            deposit.AskedAt = Time.time;
            if (Awaiting.TryGetValue(container, out List<Deposit> waiting))
            {
                waiting.Add(deposit); // the chest's owner has already been asked
                return;
            }

            // Listed before asking: when this player owns the chest, the answer arrives during the call.
            Awaiting[container] = [deposit];
            container.m_nview.InvokeRPC("RPC_RequestOpen", Game.instance.GetPlayerProfile().GetPlayerID());
            return;
        }

        if (deposit.Stored <= 0)
        {
            Log(deposit.ItemName, "the chests near it are full or in use");
        }
    }

    /// <summary>
    /// The chest's owner answered. Returns true if the answer was for a deposit and should not open the chest.
    /// </summary>
    private static bool OnOpenResponse(Container container, bool granted)
    {
        if (!Awaiting.TryGetValue(container, out List<Deposit> waiting))
        {
            return false;
        }

        _ = Awaiting.Remove(container);
        foreach (Deposit deposit in waiting)
        {
            if (granted && container.m_nview.IsValid() && container.IsOwner())
            {
                DepositInto(container, deposit);
            }
            Next(deposit);
        }

        // If the player also asked to open the chest, let the answer open it as usual.
        return !OpenedByPlayer.Remove(container);
    }

    /// <summary>
    /// Gives up on chests whose owner never answered (for example, they left), moving on to the next chest.
    /// </summary>
    private static void ForgetStaleRequests()
    {
        List<Container> stale = null;
        foreach (KeyValuePair<Container, List<Deposit>> entry in Awaiting)
        {
            if (entry.Key == null || entry.Value.TrueForAll(d => Time.time - d.AskedAt > RequestTimeout))
            {
                (stale ??= []).Add(entry.Key);
            }
        }

        if (stale == null)
        {
            return;
        }

        foreach (Container container in stale)
        {
            List<Deposit> waiting = Awaiting[container];
            _ = Awaiting.Remove(container);
            _ = OpenedByPlayer.Remove(container);
            foreach (Deposit deposit in waiting)
            {
                Next(deposit);
            }
        }
    }

    /// <summary>
    /// True while the dropped item still lies there and belongs to this player's game.
    /// </summary>
    private static bool StillOurs(Deposit deposit)
    {
        ItemDrop drop = deposit.Drop;
        if (drop == null || drop.m_nview == null || !drop.m_nview.IsValid() || drop.m_itemData.m_stack <= 0)
        {
            return false;
        }

        if (!drop.m_nview.IsOwner())
        {
            Log(deposit.ItemName, "another player now owns it");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Moves as much of the dropped item as fits into a chest this player's game now owns.
    /// </summary>
    private static void DepositInto(Container container, Deposit deposit)
    {
        if (!StillOurs(deposit) || !CanDepositInto(container))
        {
            return;
        }

        ItemDrop drop = deposit.Drop;
        ItemDrop.ItemData item = drop.m_itemData;
        int amount = item.m_stack;
        Inventory inventory = container.GetInventory();
        int fits = SpaceFor(inventory, item, amount);
        if (fits <= 0)
        {
            return;
        }

        ItemDrop.ItemData moved = item.Clone();
        moved.m_stack = fits;
        // On failure AddItem leaves the part it couldn't place in moved.m_stack.
        int added = inventory.AddItem(moved) ? fits : fits - moved.m_stack;
        if (added <= 0)
        {
            return;
        }

        deposit.Stored += added;
        if (added >= amount)
        {
            drop.m_nview.Destroy();
        }
        else
        {
            drop.SetStack(amount - added);
        }

        // The item is stored already; this only shows where it went.
        string chestName = container.GetHoverName();
        string itemName = deposit.ItemName;
        Sprite icon = deposit.Icon;
        DepositEffect.Spawn(deposit.Look, deposit.Position, deposit.Rotation, container,
            () => OnArrive(container, itemName, chestName, added, icon));
    }

    private static void Log(string itemName, string reason)
    {
        BetterChestsPlugin._log.LogInfo("Auto deposit: left " + itemName + " on the ground: " + reason);
    }

    private static void OnArrive(Container container, string itemName, string chestName, int amount, Sprite icon)
    {
        if (container != null && InventoryGui.instance != null)
        {
            _ = InventoryGui.instance.m_moveItemEffects.Create(container.transform.position, Quaternion.identity);
        }

        if (Player.m_localPlayer != null)
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                Localization.instance.Localize("$betterchests_stored", Localization.instance.Localize(itemName),
                    Localization.instance.Localize(chestName)), amount, icon);
        }
    }

    private readonly struct Chest(Container container, Bounds bounds)
    {
        public Container Container { get; } = container;
        public Bounds Bounds { get; } = bounds;
    }

    /// <summary>
    /// The chests an item that landed at <paramref name="position"/> can go into, in the order to fill them:
    /// those that already hold it first, then the rest, each group nearest first.
    /// <para>
    /// The item must land within range of a chest the player threw it towards. That chest and every chest
    /// grouped with it (chains of chests close to each other) are used, however far they are from the item.
    /// </para>
    /// </summary>
    private static List<Container> GetTargets(Vector3 position, Vector3 throwFrom, Vector3 throwDir, ItemDrop.ItemData item)
    {
        _ = Containers.RemoveAll(c => c == null);
        List<Chest> chests = [];
        foreach (Container container in Containers)
        {
            if (IsChest(container))
            {
                chests.Add(new Chest(container, BoundsOf(container)));
            }
        }

        // The nearest chest in the direction of the throw that the item landed close enough to.
        float maxSqr = _radius.Value * _radius.Value;
        int anchor = -1;
        float anchorSqr = float.MaxValue;
        for (int i = 0; i < chests.Count; i++)
        {
            float sqr = chests[i].Bounds.SqrDistance(position);
            if (sqr <= maxSqr && sqr < anchorSqr && IsThrownTowards(chests[i].Bounds.center, throwFrom, throwDir))
            {
                anchor = i;
                anchorSqr = sqr;
            }
        }

        if (anchor < 0)
        {
            return [];
        }

        // Grow the group from that chest: any chest close enough to one already in it joins.
        List<Chest> cluster = [chests[anchor]];
        chests.RemoveAt(anchor);
        float gap = _clusterGap.Value;
        for (int i = 0; i < cluster.Count; i++)
        {
            Bounds reach = cluster[i].Bounds;
            reach.Expand(gap * 2f);
            for (int j = chests.Count - 1; j >= 0; j--)
            {
                if (reach.Intersects(chests[j].Bounds))
                {
                    cluster.Add(chests[j]);
                    chests.RemoveAt(j);
                }
            }
        }

        // Unavailable chests still link the group together, but nothing goes into them.
        _ = cluster.RemoveAll(c => !CanDepositInto(c.Container));
        cluster.Sort((a, b) => a.Bounds.SqrDistance(position).CompareTo(b.Bounds.SqrDistance(position)));

        List<Container> targets = [];
        List<Container> others = [];
        foreach (Chest chest in cluster)
        {
            (chest.Container.GetInventory().ContainsItemByName(item.m_shared.m_name) ? targets : others).Add(chest.Container);
        }

        targets.AddRange(others);
        return targets;
    }

    /// <summary>
    /// Whether the chest at <paramref name="target"/> is within the throw angle of the direction the
    /// player faced, ignoring height. A chest right under the player counts as either way.
    /// </summary>
    private static bool IsThrownTowards(Vector3 target, Vector3 throwFrom, Vector3 throwDir)
    {
        Vector3 toTarget = target - throwFrom;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude < 0.01f || Vector3.Angle(throwDir, toTarget) <= _throwAngle.Value;
    }

    private static Bounds BoundsOf(Container container)
    {
        bool found = false;
        Bounds bounds = default;
        foreach (Collider collider in container.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger)
            {
                continue;
            }

            if (found)
            {
                bounds.Encapsulate(collider.bounds);
            }
            else
            {
                bounds = collider.bounds;
                found = true;
            }
        }

        return found
            ? bounds
            : new Bounds(container.transform.position + (Vector3.up * (FallbackChestSize.y / 2f)), FallbackChestSize);
    }

    /// <summary>
    /// Player-built chests: not carts, tombstones or containers that vanish when emptied.
    /// </summary>
    private static bool IsChest(Container container)
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
        return nview != null && nview.IsValid();
    }

    /// <summary>
    /// Only chests that the player could open normally and that nobody else is using.
    /// </summary>
    private static bool CanDepositInto(Container container)
    {
        // The chest we have open ourselves is fine; one someone else has open is not.
        bool openHere = InventoryGui.instance != null && InventoryGui.instance.m_currentContainer == container;
        bool free = openHere || (!container.IsInUse() && container.m_nview.GetZDO().GetInt(ZDOVars.s_inUse) != 1);
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

    /// <summary>
    /// Watches an item the local player dropped, and tries to store it once it has lain on the ground for
    /// the delay. It goes away with the item, e.g. when someone picks it up.
    /// </summary>
    private sealed class PendingDeposit : MonoBehaviour
    {
        private ItemDrop _drop;
        private Rigidbody _body;
        private Vector3 _throwFrom;
        private Vector3 _throwDir;
        private float _depositAt;

        public static void Watch(ItemDrop drop, Player thrower)
        {
            PendingDeposit pending = drop.gameObject.AddComponent<PendingDeposit>();
            pending._drop = drop;
            pending._body = drop.GetComponent<Rigidbody>();
            pending._throwFrom = thrower.transform.position;
            Vector3 dir = thrower.transform.forward;
            dir.y = 0f;
            pending._throwDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            pending._depositAt = Time.time + _delay.Value;
        }

        private void Update()
        {
            float time = Time.time;
            if (time < _depositAt)
            {
                return;
            }

            // Wait for it to stop rolling, so it is judged by where it ends up.
            bool moving = _body != null && !_body.isKinematic && _body.linearVelocity.sqrMagnitude > SettledSpeedSqr;
            if (moving && time < _depositAt + MaxSettleTime)
            {
                return;
            }

            Destroy(this);
            if (_drop != null)
            {
                TryDeposit(_drop, _throwFrom, _throwDir);
            }
        }
    }

    /// <summary>
    /// The owner's answer to a deposit's request: store the item instead of opening the chest.
    /// </summary>
    [HarmonyPatch(typeof(Container), "RPC_OpenResponse")]
    private static class Container_RPC_OpenResponse_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(Container __instance, bool granted)
        {
            return !OnOpenResponse(__instance, granted);
        }
    }

    /// <summary>
    /// Remembers that the player wants a chest open, so a deposit waiting on the same chest doesn't swallow it.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    private static class Container_Interact_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(Container __instance, bool hold)
        {
            if (!hold && Awaiting.ContainsKey(__instance))
            {
                _ = OpenedByPlayer.Add(__instance);
            }
        }
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
    /// Every way of dropping an item from the inventory (dragging it out, Ctrl+click) ends up here. The item
    /// is dropped as normal; the drop is only marked so <see cref="ItemDrop_DropItem_Patch"/> watches it.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem))]
    private static class Humanoid_DropItem_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, int amount)
        {
            _dropper = null;
            if (_enabled == null || !_enabled.Value
                || __instance != Player.m_localPlayer
                || item == null || amount <= 0 || item.m_shared.m_questItem)
            {
                return;
            }

            Player player = Player.m_localPlayer;
            inventory ??= player.GetInventory();

            // Items dragged out of a chest are dropped as normal; storing them would put them straight back.
            if (inventory == player.GetInventory() && inventory.ContainsItem(item))
            {
                _dropper = player;
            }
        }

        [HarmonyFinalizer]
        private static void Finalizer()
        {
            _dropper = null;
        }
    }

    /// <summary>
    /// Creates the item that <see cref="Humanoid.DropItem"/> throws. (Not <c>ItemDrop.OnPlayerDrop</c>, which
    /// is so small that the game's JIT inlines it into the caller, so a patch on it never runs.)
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.DropItem))]
    private static class ItemDrop_DropItem_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDrop __result)
        {
            if (_dropper != null && __result != null)
            {
                PendingDeposit.Watch(__result, _dropper);
                _dropper = null;
            }
        }
    }
}
