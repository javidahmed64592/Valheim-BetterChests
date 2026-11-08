using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using UnityEngine;
using UnityEngine.UI;

namespace BetterChests;

/// <summary>
/// <para>
/// Items in the player's inventory can be marked to be kept by "Deposit all" and "Place stacks", by holding the keep key
/// (Alt) and right-clicking them. The mark is stored in the item's custom data, so it is saved with the
/// item and follows it around the inventory. Marked items get a frame around their slot.
/// </para>
/// <para>
/// Only items that Deposit all would otherwise take can be marked: not the hotbar, and not equipped items.
/// </para>
/// </summary>
internal static class KeepItems
{
    private const string DataKey = "BetterChests_Keep";

    /// <summary>
    /// The row of the player's inventory that the hotbar shows.
    /// </summary>
    internal const int HotbarRow = 0;

    private const string FrameName = "BetterChests_KeepFrame";
    private const float FrameThickness = 2f;
    private const float FrameInset = 1f;
    private static readonly Color FrameColour = new(1f, 0.76f, 0.3f, 0.95f);

    /// <summary>
    /// True for items that are never deposited anyway, so marking them means nothing.
    /// </summary>
    internal static bool AlwaysKept(ItemDrop.ItemData item)
    {
        return item.m_gridPos.y == HotbarRow || item.m_equipped;
    }

    internal static bool IsMarked(ItemDrop.ItemData item)
    {
        return item.m_customData != null && item.m_customData.ContainsKey(DataKey);
    }

    private static void Toggle(ItemDrop.ItemData item)
    {
        if (!item.m_customData.Remove(DataKey))
        {
            item.m_customData[DataKey] = "1";
        }
    }

    /// <summary>
    /// The frame around a slot: four thin bars, so the item stays visible inside it.
    /// </summary>
    private static GameObject CreateFrame(InventoryElement element)
    {
        GameObject frame = new(FrameName, typeof(RectTransform));
        RectTransform rect = (RectTransform)frame.transform;
        rect.SetParent(element.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(FrameInset, FrameInset);
        rect.offsetMax = new Vector2(-FrameInset, -FrameInset);

        AddBar(rect, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -FrameThickness), Vector2.zero);
        AddBar(rect, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, FrameThickness));
        AddBar(rect, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(FrameThickness, 0f));
        AddBar(rect, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-FrameThickness, 0f), Vector2.zero);
        return frame;
    }

    private static void AddBar(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject bar = new(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)bar.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Image image = bar.GetComponent<Image>();
        image.color = FrameColour;
        // Clicks go through to the slot.
        image.raycastTarget = false;
    }

    private static bool IsPlayerGrid(InventoryGrid grid)
    {
        InventoryGui gui = InventoryGui.instance;
        return gui != null && grid == gui.m_playerGrid
            && Player.m_localPlayer != null && grid.m_inventory == Player.m_localPlayer.GetInventory();
    }

    /// <summary>
    /// Keep key + right-click marks or unmarks the item instead of using it.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), "OnRightDown")]
    private static class InventoryGrid_OnRightDown_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(InventoryGrid __instance, UIInputHandler element)
        {
            if (!BetterChestsPlugin._keepItemKey.Held() || !IsPlayerGrid(__instance))
            {
                return true;
            }

            Vector2i pos = __instance.GetButtonPos(element.gameObject);
            ItemDrop.ItemData item = __instance.m_inventory.GetItemAt(pos.x, pos.y);
            if (item == null)
            {
                return false;
            }

            if (AlwaysKept(item))
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Hotbar and equipped items are never deposited");
                return false;
            }

            Toggle(item);
            return false;
        }
    }

    /// <summary>
    /// The player's items that "Place stacks" may move: all of them except marked ones. Equipped items are
    /// already skipped by vanilla, and hotbar items are placed as in vanilla.
    /// </summary>
    private static List<ItemDrop.ItemData> ItemsToStack(Inventory fromInventory)
    {
        List<ItemDrop.ItemData> items = fromInventory.GetAllItems();
        return Player.m_localPlayer == null || fromInventory != Player.m_localPlayer.GetInventory()
            ? items
            : [.. items.Where(item => !IsMarked(item) || AlwaysKept(item))];
    }

    /// <summary>
    /// "Place stacks" (the button, its key, and holding use on a chest) copies the player's item list and
    /// moves what matches; this swaps that list for one without the marked items.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
    private static class Inventory_StackAll_Patch
    {
        private static readonly MethodInfo GetAllItems = AccessTools.Method(typeof(Inventory), nameof(Inventory.GetAllItems));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(KeepItems), nameof(ItemsToStack));

        [HarmonyTranspiler]
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

    /// <summary>
    /// Shows or hides the frames each time vanilla redraws the player's inventory.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    private static class InventoryGrid_UpdateGui_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryGrid __instance)
        {
            if (!IsPlayerGrid(__instance))
            {
                return;
            }

            foreach (InventoryElement element in __instance.m_elements)
            {
                ItemDrop.ItemData item = __instance.m_inventory.GetItemAt(element.Position.x, element.Position.y);
                bool show = item != null && IsMarked(item) && !AlwaysKept(item);

                Transform frame = element.transform.Find(FrameName);
                if (frame != null)
                {
                    if (frame.gameObject.activeSelf != show)
                    {
                        frame.gameObject.SetActive(show);
                    }
                }
                else if (show)
                {
                    _ = CreateFrame(element);
                }
            }
        }
    }
}
