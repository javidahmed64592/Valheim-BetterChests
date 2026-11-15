using System;
using System.Collections.Generic;

using UnityEngine;

namespace BetterChests.Core;

/// <summary>
/// Only a chest's owner may change it, so, like opening a chest yourself, its owner is asked to hand it over; they
/// refuse if someone has it open. Auto deposit and the chest keys share these requests, so a chest is only asked
/// once however many of them are waiting, and the answer never opens the chest's panel unless the player asked to
/// open it too.
/// </summary>
internal static class ChestRequests
{
    /// <summary>
    /// Seconds to wait for a chest's owner to answer before giving up (for example, they left).
    /// </summary>
    private const float Timeout = 5f;

    private sealed class Pending
    {
        public float AskedAt;
        public readonly List<Action<bool>> Callbacks = [];
    }

    private static readonly Dictionary<Container, Pending> Waiting = [];

    /// <summary>Chests the player has asked to open themselves while a request was waiting on them.</summary>
    private static readonly HashSet<Container> OpenedByPlayer = [];

    /// <summary>
    /// Asks for the chest and calls <paramref name="onAnswer"/> with true once this game owns it, or false if its owner
    /// refused or never answered. When this game already owns the chest, the answer arrives during the call.
    /// </summary>
    internal static void Request(Container container, Action<bool> onAnswer)
    {
        ForgetStale();
        if (Waiting.TryGetValue(container, out Pending pending))
        {
            pending.AskedAt = Time.time;
            pending.Callbacks.Add(onAnswer); // the chest's owner has already been asked
            return;
        }

        // Listed before asking: when this player owns the chest, the answer arrives during the call.
        pending = new Pending { AskedAt = Time.time };
        pending.Callbacks.Add(onAnswer);
        Waiting[container] = pending;
        container.m_nview.InvokeRPC("RPC_RequestOpen", Game.instance.GetPlayerProfile().GetPlayerID());
    }

    /// <summary>
    /// The chest's owner answered (before <c>Container.RPC_OpenResponse</c>). Returns true if the answer was for a
    /// request here and should not open the chest.
    /// </summary>
    internal static bool OnOpenResponse(Container container, bool granted)
    {
        if (!Waiting.TryGetValue(container, out Pending pending))
        {
            return false;
        }

        _ = Waiting.Remove(container);
        bool ours = granted && container.m_nview.IsValid() && container.IsOwner();
        foreach (Action<bool> callback in pending.Callbacks)
        {
            callback(ours);
        }

        // If the player also asked to open the chest, let the answer open it as usual.
        return !OpenedByPlayer.Remove(container);
    }

    /// <summary>
    /// Before <c>Container.Interact</c>: remembers that the player wants the chest open, so a request waiting on the
    /// same chest doesn't swallow it.
    /// </summary>
    internal static void BeforeInteract(Container container, bool hold)
    {
        if (!hold && Waiting.ContainsKey(container))
        {
            _ = OpenedByPlayer.Add(container);
        }
    }

    /// <summary>Gives up on chests whose owner never answered, telling the waiting callers.</summary>
    private static void ForgetStale()
    {
        List<Container> stale = null;
        foreach (KeyValuePair<Container, Pending> entry in Waiting)
        {
            if (entry.Key == null || Time.time - entry.Value.AskedAt > Timeout)
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
            Pending pending = Waiting[container];
            _ = Waiting.Remove(container);
            _ = OpenedByPlayer.Remove(container);
            foreach (Action<bool> callback in pending.Callbacks)
            {
                callback(false);
            }
        }
    }
}
