using BetterChests.Core;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>The owner's answer to a request for the chest (see <see cref="ChestRequests"/>), instead of opening it.</summary>
[HarmonyPatch(typeof(Container), "RPC_OpenResponse")]
internal static class Container_RPC_OpenResponse_Patch
{
    private static bool Prefix(Container __instance, bool granted)
    {
        return !ChestRequests.OnOpenResponse(__instance, granted);
    }
}
