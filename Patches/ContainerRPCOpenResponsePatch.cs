using BetterChests.Deposit;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>The owner's answer to a deposit's request: store the item instead of opening the chest.</summary>
[HarmonyPatch(typeof(Container), "RPC_OpenResponse")]
internal static class Container_RPC_OpenResponse_Patch
{
    private static bool Prefix(Container __instance, bool granted)
    {
        return !AutoDeposit.OnOpenResponse(__instance, granted);
    }
}
