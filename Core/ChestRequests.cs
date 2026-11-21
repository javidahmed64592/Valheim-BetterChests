using System;
using System.Collections.Generic;

using UnityEngine;

namespace BetterChests.Core;

/// <summary>
/// Only a chest's owner may change it, so, like opening a chest yourself, its owner is asked to hand it over; they
/// refuse if someone has it open. Auto deposit, restock, search and the chest keys share these requests, so a chest is
/// only asked once however many of them are waiting, and the answer never opens the chest's panel unless the player
/// asked to open it too.
/// </summary>
/// <remarks>
/// The owner's "yes" and the ownership itself travel separately: the answer is an RPC, sent straight away, while the
/// chest (its new owner and its contents) follows with the next object sync. The answer usually arrives first, so a
/// granted request waits until this game owns the chest, as vanilla's chest panel does, and then reads the contents
/// that came with it. Treating that gap as a refusal showed "someone else is using this" for nearly every chest
/// another player owned.
/// </remarks>
internal static class ChestRequests
{
    /// <summary>
    /// Seconds to wait for a chest's owner to answer, or for the chest to arrive after they agreed, before giving up
    /// (for example, they left).
    /// </summary>
    private const float Timeout = 5f;

    /// <summary>A chest that keeps changing hands is asked for this many times at most.</summary>
    private const int MaxAsks = 3;

    private sealed class Pending
    {
        /// <summary>When the owner was last asked, or agreed.</summary>
        public float Since;

        /// <summary>Who was asked. If the chest changes hands before they answer, they won't, so the new owner is asked.</summary>
        public long AskedOwner;

        /// <summary>The owner agreed; waiting for the chest to arrive.</summary>
        public bool Granted;

        /// <summary>How many times the chest has been asked for.</summary>
        public int Asks;

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
        Update();
        if (Waiting.TryGetValue(container, out Pending pending))
        {
            pending.Callbacks.Add(onAnswer); // the chest's owner has already been asked
            return;
        }

        // Listed before asking: when this player owns the chest, the answer arrives during the call.
        pending = new Pending();
        pending.Callbacks.Add(onAnswer);
        Waiting[container] = pending;
        Ask(container, pending);
    }

    private static void Ask(Container container, Pending pending)
    {
        pending.Since = Time.time;
        pending.Asks++;
        pending.AskedOwner = container.m_nview.GetZDO().GetOwner();
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

        // If the player also asked to open the chest, let the answer open it as usual (vanilla then waits for the
        // chest to arrive itself).
        bool swallow = !OpenedByPlayer.Remove(container);

        if (!granted || !container.m_nview.IsValid())
        {
            Finish(container, pending, false);
        }
        else if (container.IsOwner())
        {
            Finish(container, pending, true);
        }
        else
        {
            pending.Granted = true;
            pending.Since = Time.time;
        }
        return swallow;
    }

    /// <summary>
    /// Before <c>Container.Interact</c>: remembers that the player wants the chest open, so a request waiting on the
    /// same chest doesn't swallow it.
    /// </summary>
    internal static void BeforeInteract(Container container, bool hold)
    {
        if (!hold && Waiting.TryGetValue(container, out Pending pending) && !pending.Granted)
        {
            _ = OpenedByPlayer.Add(container);
        }
    }

    /// <summary>
    /// Called every frame: hands over chests that have arrived after their owner agreed, asks again when a chest
    /// changed hands before its owner answered, and gives up on chests whose owner never answered.
    /// </summary>
    internal static void Update()
    {
        if (Waiting.Count == 0)
        {
            return;
        }

        List<KeyValuePair<Container, Pending>> done = null;
        foreach (KeyValuePair<Container, Pending> entry in Waiting)
        {
            Container container = entry.Key;
            Pending pending = entry.Value;
            if (container == null || !container.m_nview.IsValid())
            {
                (done ??= []).Add(entry);
                continue;
            }

            if (pending.Granted)
            {
                if (container.IsOwner() || Time.time - pending.Since > Timeout)
                {
                    (done ??= []).Add(entry);
                }
                continue;
            }

            if (Time.time - pending.Since > Timeout)
            {
                (done ??= []).Add(entry);
                continue;
            }

            // The player we asked no longer owns the chest, so they ignore the request ("but im not the owner"): ask
            // whoever owns it now. When it changed hands to this game, the owner's answer is on its way.
            long owner = container.m_nview.GetZDO().GetOwner();
            if (owner != pending.AskedOwner && owner != 0L && owner != ZDOMan.GetSessionID() && pending.Asks < MaxAsks)
            {
                Ask(container, pending);
            }
        }

        if (done == null)
        {
            return;
        }

        foreach (KeyValuePair<Container, Pending> entry in done)
        {
            Container container = entry.Key;
            bool ours = container != null && container.m_nview.IsValid() && container.IsOwner();
            Finish(container, entry.Value, ours);
        }
    }

    private static void Finish(Container container, Pending pending, bool ours)
    {
        if (container is null || !Waiting.TryGetValue(container, out Pending current) || current != pending)
        {
            return;
        }

        _ = Waiting.Remove(container);
        _ = OpenedByPlayer.Remove(container);
        if (ours)
        {
            // The contents arrived with the chest, but the chest only reads them once a second. Read them now, so
            // changing it doesn't save over what the previous owner put in.
            _ = container.Load();
        }

        foreach (Action<bool> callback in pending.Callbacks)
        {
            callback(ours);
        }
    }
}
