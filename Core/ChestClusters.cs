using System.Collections.Generic;

using BetterChests.Deposit;

using HarmonyLib;

using UnityEngine;

namespace BetterChests.Core;

/// <summary>
/// A chest and the space it takes up in the world.
/// </summary>
internal readonly struct Chest(Container container, Bounds bounds)
{
    public Container Container { get; } = container;
    public Bounds Bounds { get; } = bounds;
}

/// <summary>
/// Groups of chests standing close together (chains of chests at most the auto deposit's Cluster Gap apart),
/// shared by auto deposit and the duplicate highlights.
/// </summary>
internal static class ChestClusters
{
    /// <summary>
    /// The size assumed for a chest without a collider.
    /// </summary>
    private static readonly Vector3 FallbackChestSize = Vector3.one;

    private static readonly List<Container> Containers = [];

    /// <summary>
    /// Each chest's bounds and where the chest was when they were measured, so a chest that moves is measured again.
    /// </summary>
    private static readonly Dictionary<Container, (Vector3 Position, Quaternion Rotation, Bounds Bounds)> BoundsCache = [];

    /// <summary>
    /// Every loaded player-built chest.
    /// </summary>
    internal static List<Chest> AllChests()
    {
        if (Containers.RemoveAll(c => c == null) > 0)
        {
            List<Container> gone = null;
            foreach (Container container in BoundsCache.Keys)
            {
                if (container == null)
                {
                    (gone ??= []).Add(container);
                }
            }

            gone?.ForEach(c => BoundsCache.Remove(c));
        }

        List<Chest> chests = [];
        foreach (Container container in Containers)
        {
            if (IsChest(container))
            {
                chests.Add(new Chest(container, BoundsOf(container)));
            }
        }

        return chests;
    }

    /// <summary>
    /// Takes the chest at <paramref name="anchor"/> out of <paramref name="pool"/>, along with every chest
    /// grouped with it: any chest close enough to one already in the group joins it.
    /// </summary>
    internal static List<Chest> TakeCluster(List<Chest> pool, int anchor)
    {
        List<Chest> cluster = [pool[anchor]];
        pool.RemoveAt(anchor);
        float gap = AutoDeposit.ClusterGap;
        for (int i = 0; i < cluster.Count; i++)
        {
            Bounds reach = cluster[i].Bounds;
            reach.Expand(gap * 2f);
            for (int j = pool.Count - 1; j >= 0; j--)
            {
                if (reach.Intersects(pool[j].Bounds))
                {
                    cluster.Add(pool[j]);
                    pool.RemoveAt(j);
                }
            }
        }

        return cluster;
    }

    /// <summary>
    /// Whether the player may look in the chest: not another player's private chest, nor one inside
    /// someone else's ward. Someone else having it open doesn't matter.
    /// </summary>
    internal static bool CanAccess(Container container)
    {
        bool allowed = container.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID());
        bool warded = container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position, 0f, false);
        return allowed && !warded;
    }

    /// <summary>
    /// Player-built chests: not carts, tombstones or containers that vanish when emptied.
    /// </summary>
    internal static bool IsChest(Container container)
    {
        if (container == null
            || container.GetInventory() == null
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

    private static Bounds BoundsOf(Container container)
    {
        Transform transform = container.transform;
        if (BoundsCache.TryGetValue(container, out (Vector3 Position, Quaternion Rotation, Bounds Bounds) cached)
            && cached.Position == transform.position && cached.Rotation == transform.rotation)
        {
            return cached.Bounds;
        }

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

        if (!found)
        {
            // Not remembered, in case the colliders are only switched off for now.
            return new Bounds(transform.position + (Vector3.up * (FallbackChestSize.y / 2f)), FallbackChestSize);
        }

        BoundsCache[container] = (transform.position, transform.rotation, bounds);
        return bounds;
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
}
