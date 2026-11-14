using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;

using BepInEx.Configuration;

using HarmonyLib;

using UnityEngine;

namespace BetterChests;

/// <summary>
/// <para>
/// Shows where else items are stored, so players can tidy their chests themselves. Nothing is moved.
/// </para>
/// <list type="bullet">
/// <item>Items in the open chest that another chest in its group (see <see cref="ChestClusters"/>) also holds
/// get a blue frame.</item>
/// <item>Hovering over an item in the open chest makes the other chests in its group that hold it glow, and
/// its tooltip says how many of them there are and how much they hold.</item>
/// <item>Hovering over an item in the player's inventory does the same for every chest in the groups within
/// auto deposit range of the player.</item>
/// </list>
/// <para>
/// Only chests the player may open count: not other players' private chests or chests in someone else's ward.
/// </para>
/// </summary>
internal static class ChestDuplicates
{
    private const string FrameName = "BetterChests_DuplicateFrame";
    private static readonly Color FrameColour = new(0.45f, 0.75f, 1f, 0.95f);
    private const string TooltipColour = "#73BFFF";

    private static readonly Color GlowColour = new(0.45f, 0.75f, 1f);
    private const float GlowMin = 0.2f;
    private const float GlowMax = 0.6f;
    private const float GlowPulseSpeed = 4f;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>
    /// How often the groups are worked out again. Chests only see each other's changes once a second anyway.
    /// </summary>
    private const float RefreshInterval = 0.25f;

    private static ConfigEntry<bool> _enabled;

    private static Container _openChest;
    private static float _refreshAt;

    /// <summary>
    /// The other chests in the open chest's group.
    /// </summary>
    private static readonly List<Container> GroupOthers = [];

    /// <summary>
    /// Items held by <see cref="GroupOthers"/>.
    /// </summary>
    private static readonly HashSet<string> GroupItems = [];

    /// <summary>
    /// Every chest in the groups within auto deposit range of the player, and in the open chest's group.
    /// </summary>
    private static readonly List<Container> Nearby = [];

    private static InventoryGrid _hoverGrid;
    private static ItemDrop.ItemData _hoverItem;
    private static int _hoverFrame = -1;

    /// <summary>
    /// The chests glowing now, by the object their look is set on.
    /// </summary>
    private static readonly HashSet<GameObject> Glowing = [];
    private static readonly HashSet<GameObject> GlowTargets = [];
    private static readonly List<Container> Holders = [];

    public static void BindConfig(ConfigFile config)
    {
        _enabled = config.Bind("Chest Panel", "Show Duplicates", true,
            "Frames items in an open chest that other chests in its group also hold. Hovering over an item shows " +
            "how many nearby chests hold it in its tooltip, and makes those chests glow.");
    }

    private static void Refresh(InventoryGui gui)
    {
        Container open = gui.m_currentContainer;
        if (open == _openChest && Time.time < _refreshAt)
        {
            return;
        }

        _openChest = open;
        _refreshAt = Time.time + RefreshInterval;
        GroupOthers.Clear();
        GroupItems.Clear();
        Nearby.Clear();

        Player player = Player.m_localPlayer;
        if (player == null)
        {
            return;
        }

        List<Chest> pool = ChestClusters.AllChests();
        int openIndex = open != null ? pool.FindIndex(c => c.Container == open) : -1;
        if (openIndex >= 0)
        {
            foreach (Chest chest in ChestClusters.TakeCluster(pool, openIndex))
            {
                if (!ChestClusters.CanAccess(chest.Container))
                {
                    continue;
                }

                Nearby.Add(chest.Container);
                if (chest.Container != open)
                {
                    GroupOthers.Add(chest.Container);
                    foreach (ItemDrop.ItemData item in chest.Container.GetInventory().GetAllItems())
                    {
                        _ = GroupItems.Add(item.m_shared.m_name);
                    }
                }
            }
        }

        Vector3 position = player.transform.position;
        float maxSqr = AutoDeposit.Radius * AutoDeposit.Radius;
        int anchor;
        while ((anchor = pool.FindIndex(c => c.Bounds.SqrDistance(position) <= maxSqr)) >= 0)
        {
            foreach (Chest chest in ChestClusters.TakeCluster(pool, anchor))
            {
                if (ChestClusters.CanAccess(chest.Container))
                {
                    Nearby.Add(chest.Container);
                }
            }
        }
    }

    /// <summary>
    /// The chests to look in for an item hovered in <paramref name="grid"/>, or null for other grids.
    /// </summary>
    private static List<Container> ChestsFor(InventoryGui gui, InventoryGrid grid)
    {
        List<Container> forPlayer = grid == gui.m_playerGrid ? Nearby : null;
        return grid == gui.m_containerGrid ? GroupOthers : forPlayer;
    }

    /// <summary>
    /// Fills <paramref name="holders"/> with the chests holding the item, and returns how many they hold.
    /// </summary>
    private static int FindHolders(List<Container> chests, string itemName, List<Container> holders)
    {
        holders.Clear();
        int total = 0;
        foreach (Container chest in chests)
        {
            if (chest == null)
            {
                continue;
            }

            int amount = 0;
            foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
            {
                if (item.m_shared.m_name == itemName)
                {
                    amount += item.m_stack;
                }
            }

            if (amount > 0)
            {
                holders.Add(chest);
                total += amount;
            }
        }

        return total;
    }

    /// <summary>
    /// Stands in for <c>InventoryGrid.CreateItemTooltip</c>, which vanilla calls every frame for the hovered
    /// item: the tooltip also says which nearby chests hold the item, and the item is remembered for the glow.
    /// </summary>
    private static void CreateItemTooltip(InventoryGrid grid, ItemDrop.ItemData item, UITooltip tooltip)
    {
        string text = item.GetTooltip();
        InventoryGui gui = InventoryGui.instance;
        if (_enabled.Value && gui != null)
        {
            Refresh(gui);
            List<Container> chests = ChestsFor(gui, grid);
            if (chests != null)
            {
                _hoverGrid = grid;
                _hoverItem = item;
                _hoverFrame = Time.frameCount;

                int amount = FindHolders(chests, item.m_shared.m_name, Holders);
                if (Holders.Count > 0)
                {
                    text += "\n<color=" + TooltipColour + ">" + HoldersText(grid == gui.m_containerGrid, Holders.Count, amount) + "</color>";
                }
            }
        }

        tooltip.Set(item.m_shared.m_name, text, grid.m_tooltipAnchor);
    }

    private static string HoldersText(bool inChest, int chests, int amount)
    {
        string count = amount.ToString(CultureInfo.InvariantCulture);
        return (inChest, chests) switch
        {
            (true, 1) => Localization.instance.Localize("$betterchests_also_in_group_one", count),
            (true, _) => Localization.instance.Localize("$betterchests_also_in_group", chests.ToString(CultureInfo.InvariantCulture), count),
            (false, 1) => Localization.instance.Localize("$betterchests_in_nearby_one", count),
            _ => Localization.instance.Localize("$betterchests_in_nearby", chests.ToString(CultureInfo.InvariantCulture), count),
        };
    }

    /// <summary>
    /// Makes the chests holding the hovered item glow, and stops the rest.
    /// </summary>
    private static void UpdateGlow(InventoryGui gui)
    {
        GlowTargets.Clear();
        List<Container> chests = _hoverFrame == Time.frameCount && _hoverItem != null && InventoryGui.IsVisible()
            ? ChestsFor(gui, _hoverGrid)
            : null;
        if (chests != null && _enabled.Value)
        {
            _ = FindHolders(chests, _hoverItem.m_shared.m_name, Holders);
            foreach (Container chest in Holders)
            {
                _ = GlowTargets.Add(chest.m_nview.gameObject);
            }
        }

        MaterialMan materials = MaterialMan.instance;
        if (materials == null)
        {
            return;
        }

        foreach (GameObject go in Glowing)
        {
            if (go != null && !GlowTargets.Contains(go))
            {
                materials.ResetValue(go, EmissionColorId);
                materials.ResetValue(go, ColorId);
            }
        }

        Glowing.Clear();
        if (GlowTargets.Count == 0)
        {
            return;
        }

        float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * GlowPulseSpeed));
        Color emission = GlowColour * Mathf.Lerp(GlowMin, GlowMax, pulse);
        foreach (GameObject go in GlowTargets)
        {
            materials.SetValue(go, EmissionColorId, emission);
            materials.SetValue(go, ColorId, GlowColour);
            _ = Glowing.Add(go);
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    private static class InventoryGrid_UpdateGui_Patch
    {
        private static readonly MethodInfo Original = AccessTools.Method(typeof(InventoryGrid), "CreateItemTooltip");
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(ChestDuplicates), nameof(CreateItemTooltip));

        /// <summary>
        /// Calls <see cref="CreateItemTooltip"/> instead of the vanilla method, which takes the
        /// same arguments (the grid, then the item and tooltip).
        /// </summary>
        [HarmonyTranspiler]
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

        /// <summary>
        /// Frames the open chest's items that other chests in its group also hold.
        /// </summary>
        [HarmonyPostfix]
        private static void Postfix(InventoryGrid __instance)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || __instance != gui.m_containerGrid)
            {
                return;
            }

            bool enabled = _enabled.Value;
            if (enabled)
            {
                Refresh(gui);
            }

            foreach (InventoryElement element in __instance.m_elements)
            {
                ItemDrop.ItemData item = __instance.m_inventory.GetItemAt(element.Position.x, element.Position.y);
                bool show = enabled && item != null && GroupItems.Contains(item.m_shared.m_name);
                SlotFrame.Show(element, FrameName, FrameColour, show);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "Update")]
    private static class InventoryGui_Update_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryGui __instance)
        {
            UpdateGlow(__instance);
        }
    }
}
