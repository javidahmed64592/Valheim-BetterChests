using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using BetterChests.Deposit;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>
/// "Place stacks" (the button, its key, and holding use on a chest) copies the player's item list and moves what
/// matches; this swaps that list for one without the items marked to keep.
/// </summary>
[HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
internal static class Inventory_StackAll_Patch
{
    private static readonly MethodInfo GetAllItems = AccessTools.Method(typeof(Inventory), nameof(Inventory.GetAllItems));
    private static readonly MethodInfo Replacement = AccessTools.Method(typeof(KeepItems), nameof(KeepItems.ItemsToStack));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        bool replaced = false;
        foreach (CodeInstruction instruction in instructions)
        {
            if (!replaced && instruction.Calls(GetAllItems))
            {
                replaced = true;
                yield return new CodeInstruction(OpCodes.Call, Replacement).MoveLabelsFrom(instruction);
                continue;
            }

            yield return instruction;
        }

        if (!replaced)
        {
            BetterChestsPlugin._log.LogWarning("Inventory.StackAll not as expected: Place stacks will not skip kept items.");
        }
    }
}
