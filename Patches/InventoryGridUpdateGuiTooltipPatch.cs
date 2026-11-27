using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using BetterChests.Core;
using BetterChests.Duplicates;

using HarmonyLib;

namespace BetterChests.Patches;

/// <summary>
/// Calls <see cref="ChestDuplicates.CreateItemTooltip"/> instead of the vanilla method, which takes the same arguments
/// (the grid, then the item and tooltip), so tooltips list the nearby chests holding the item.
/// </summary>
[HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
[Feature(Feature)]
internal static class InventoryGrid_UpdateGui_Tooltip_Patch
{
    /// <summary>
    /// Without it, item tooltips are vanilla's, without the nearby chests holding the item, and hovering over an
    /// item no longer makes those chests glow (the glow uses the item this tooltip remembers).
    /// </summary>
    public const string Feature = "Nearby chests in tooltips";

    private static readonly MethodInfo Original = AccessTools.Method(typeof(InventoryGrid), "CreateItemTooltip");
    private static readonly MethodInfo Replacement = AccessTools.Method(typeof(ChestDuplicates), nameof(ChestDuplicates.CreateItemTooltip));

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
            throw new InvalidOperationException("InventoryGrid.UpdateGui has no CreateItemTooltip call to replace");
        }
    }
}
