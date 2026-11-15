using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using BetterChests.Deposit;
using BetterChests.Duplicates;
using BetterChests.UI;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>
/// Vanilla redraws an inventory grid's slots here. Three features act on it: the chest panel's layout, the duplicate
/// frames and tooltips on the open chest, and the frames on items marked to keep in the player's inventory. They work
/// on different grids or parts of a slot, so the order doesn't matter, but the layout goes first.
/// </summary>
[HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
internal static class InventoryGrid_UpdateGui_Patch
{
    private static readonly MethodInfo Original = AccessTools.Method(typeof(InventoryGrid), "CreateItemTooltip");
    private static readonly MethodInfo Replacement = AccessTools.Method(typeof(ChestDuplicates), nameof(ChestDuplicates.CreateItemTooltip));

    /// <summary>
    /// Calls <see cref="ChestDuplicates.CreateItemTooltip"/> instead of the vanilla method, which takes the same
    /// arguments (the grid, then the item and tooltip), so tooltips list the nearby chests holding the item.
    /// </summary>
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int replaced = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(Original))
            {
                replaced++;
                yield return new CodeInstruction(OpCodes.Call, Replacement).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                continue;
            }

            yield return instruction;
        }

        if (replaced == 0)
        {
            BetterChestsPlugin._log.LogWarning("InventoryGrid.UpdateGui not as expected: tooltips will not list nearby chests.");
        }
    }

    private static void Postfix(InventoryGrid __instance)
    {
        ChestPanelLayout.OnGridUpdated(__instance);
        ChestDuplicates.OnGridUpdated(__instance);
        KeepItems.OnGridUpdated(__instance);
    }
}
