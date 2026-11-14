using System.Collections.Generic;

using HarmonyLib;

using UnityEngine;
using UnityEngine.UI;

namespace BetterChests.UI;

/// <summary>
/// Makes room in the chest panel for the buttons added under "Take all" and "Place stacks", and makes the panel taller so
/// that every row of a big chest fits without scrolling. The panel hangs below the player's inventory and
/// grows downwards; it stops at the bottom of the screen, beyond which the chest scrolls as normal.
/// Tall, narrow chests listed in <see cref="WideLayouts"/> are shown with more columns and fewer rows, and the
/// panel grows to the right to fit them. Only the display changes: the chest's slots and saved items stay as
/// vanilla has them, so players without the mod see the same chest.
/// </summary>
internal static class ChestPanelLayout
{
    private const float Gap = 4f;

    /// <summary>
    /// Space between the grid and the panel's edges, the same as vanilla leaves around a four row chest.
    /// </summary>
    private const float GridPadding = 14f;

    /// <summary>
    /// Room left at the bottom of the screen for the key hints.
    /// </summary>
    private const float ScreenBottomMargin = 70f;

    /// <summary>
    /// Chests, by prefab name, shown with this many columns instead of their own width.
    /// </summary>
    private static readonly Dictionary<string, int> WideLayouts = new()
    {
        // 5 x 10 in vanilla, which needs a scroll bar.
        ["piece_chest_warderobe"] = 10,
    };

    private static RectTransform _panel;
    private static InventoryGrid _grid;
    private static float _baseWidth;
    private static float _baseHeight;
    private static float _gridTop;
    private static Vector2 _lastSize = -Vector2.one;

    /// <summary>
    /// The number of columns the open chest is shown with.
    /// </summary>
    private static int _columns;

    /// <summary>
    /// What the grid's slots were last laid out for: slot count, chest width, columns shown and grid width.
    /// </summary>
    private static (int Count, int Width, int Columns, float GridWidth) _laidOut;

    /// <summary>
    /// Moves the panel's contents so they stay put when the panel grows downwards, and the grid down to
    /// below <paramref name="buttons"/>.
    /// </summary>
    public static void Setup(InventoryGui gui, RectTransform[] buttons)
    {
        _panel = gui.m_container;
        _grid = gui.m_containerGrid;
        RectTransform gridRect = _grid != null ? (RectTransform)_grid.transform : null;
        if (_panel == null || gridRect == null || gridRect.parent != _panel)
        {
            BetterChestsPlugin._log.LogWarning("Vanilla chest panel layout not as expected, not resizing it.");
            _panel = null;
            return;
        }

        _baseWidth = _panel.rect.width;
        _baseHeight = _panel.rect.height;
        _lastSize = -Vector2.one;

        // Pin the header buttons and the scroll bar to the top, and to their side, so the panel can grow under
        // them and to the right.
        PinToTop((RectTransform)gui.m_takeAllButton.transform);
        PinToTop((RectTransform)gui.m_stackAllButton.transform);
        PinToSide((RectTransform)gui.m_takeAllButton.transform);
        PinToSide((RectTransform)gui.m_stackAllButton.transform);
        float vanillaGridTop = -gridRect.offsetMax.y;
        _gridTop = vanillaGridTop;
        foreach (RectTransform button in buttons)
        {
            PinToTop(button);
            PinToSide(button);
            float bottom = -(button.anchoredPosition.y - (button.pivot.y * button.rect.height));
            _gridTop = Mathf.Max(_gridTop, bottom + Gap);
        }

        gridRect.offsetMax = new Vector2(gridRect.offsetMax.x, -_gridTop);

        Scrollbar scrollbar = _grid.m_scrollbar;
        if (scrollbar != null && scrollbar.transform.parent == _panel)
        {
            // It stretches from just above the grid's top to its usual distance from the bottom.
            RectTransform scrollRect = (RectTransform)scrollbar.transform;
            PinToTop(scrollRect);
            PinToSide(scrollRect);
            float top = -(scrollRect.anchoredPosition.y + ((1f - scrollRect.pivot.y) * scrollRect.rect.height));
            float bottom = _baseHeight - top - scrollRect.rect.height;
            float width = scrollRect.rect.width;
            float x = scrollRect.anchoredPosition.x;
            scrollRect.anchorMin = new Vector2(scrollRect.anchorMin.x, 0f);
            scrollRect.anchorMax = new Vector2(scrollRect.anchorMax.x, 1f);
            scrollRect.pivot = new Vector2(scrollRect.pivot.x, 0.5f);
            scrollRect.offsetMin = new Vector2(x - (width * scrollRect.pivot.x), bottom);
            scrollRect.offsetMax = new Vector2(x + (width * (1f - scrollRect.pivot.x)), -(top + _gridTop - vanillaGridTop));
        }
    }

    /// <summary>
    /// Re-anchors a child of the panel to the panel's top edge without moving it.
    /// </summary>
    private static void PinToTop(RectTransform rect)
    {
        float parentHeight = ((RectTransform)rect.parent).rect.height;
        float fromTop = (1f - rect.anchorMin.y) * parentHeight;
        rect.anchorMin = new Vector2(rect.anchorMin.x, 1f);
        rect.anchorMax = new Vector2(rect.anchorMax.x, 1f);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, rect.anchoredPosition.y - fromTop);
    }

    /// <summary>
    /// Re-anchors a child of the panel to the panel's left or right edge, whichever it is nearer, without moving it.
    /// </summary>
    private static void PinToSide(RectTransform rect)
    {
        if (!Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x))
        {
            return;
        }

        float parentWidth = ((RectTransform)rect.parent).rect.width;
        float fromLeft = (rect.anchorMin.x * parentWidth) + rect.anchoredPosition.x;
        float side = fromLeft < parentWidth / 2f ? 0f : 1f;
        rect.anchorMin = new Vector2(side, rect.anchorMin.y);
        rect.anchorMax = new Vector2(side, rect.anchorMax.y);
        rect.anchoredPosition = new Vector2(fromLeft - (side * parentWidth), rect.anchoredPosition.y);
    }

    private static void Resize(Container container, Inventory inventory)
    {
        int slots = inventory.GetWidth() * inventory.GetHeight();
        _columns = WideLayouts.TryGetValue(Utils.GetPrefabName(container.gameObject), out int columns) && columns > inventory.GetWidth()
            ? columns
            : inventory.GetWidth();
        int rows = _columns > 0 ? (slots + _columns - 1) / _columns : 0;
        float space = _grid.m_elementSpace;

        // Keep the room vanilla leaves beside the widest grid that fits.
        float width = _baseWidth;
        if (_columns > inventory.GetWidth() && space > 0f)
        {
            float sidePadding = _baseWidth - (Mathf.Floor(_baseWidth / space) * space);
            width = Mathf.Max(_baseWidth, (_columns * space) + sidePadding);
        }

        float wanted = _gridTop + (rows * space) + GridPadding;
        float height = Mathf.Min(Mathf.Max(_baseHeight, wanted), MaxHeight());
        Vector2 size = new(width, height);
        if (size == _lastSize)
        {
            return;
        }

        _lastSize = size;
        _panel.sizeDelta = size;
    }

    /// <summary>
    /// Places the grid's slots in <see cref="_columns"/> columns, in the same reading order as the chest's own
    /// grid. Each slot keeps its place in the chest, so clicking and dragging work as before. Vanilla only lays
    /// the slots out again when the chest size changes, so they are moved only when the layout changes.
    /// </summary>
    private static void LayOutSlots(InventoryGrid grid)
    {
        int width = grid.m_inventory.GetWidth();
        int columns = Mathf.Max(_columns, width);
        float space = grid.m_elementSpace;
        float gridWidth = ((RectTransform)grid.transform).rect.width;
        (int, int, int, float) layout = (grid.m_elements.Count, width, columns, gridWidth);
        if (layout != _laidOut)
        {
            _laidOut = layout;
            float left = (gridWidth / 2f) - (columns * space / 2f);
            foreach (InventoryElement element in grid.m_elements)
            {
                int index = (element.Position.y * width) + element.Position.x;
                ((RectTransform)element.transform).anchoredPosition =
                    new Vector2(left + (index % columns * space), -(index / columns) * space);
            }
        }

        // Vanilla sets the scroll area to the chest's own height on every update.
        if (columns != width)
        {
            int rows = (grid.m_elements.Count + columns - 1) / columns;
            grid.m_gridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rows * space);
        }
    }

    /// <summary>
    /// The tallest the panel can be while staying on screen above the key hints.
    /// </summary>
    private static float MaxHeight()
    {
        Canvas canvas = _panel.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return _baseHeight;
        }

        Vector3[] corners = new Vector3[4];
        _panel.GetWorldCorners(corners);
        float panelTop = corners[1].y;
        ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(corners);
        float screenBottom = corners[0].y;

        float scale = _panel.lossyScale.y;
        return scale <= 0f
            ? _baseHeight
            : Mathf.Max(_baseHeight, ((panelTop - screenBottom) / scale) - ScreenBottomMargin);
    }

    /// <summary>
    /// Before vanilla fills the grid, so its first update lays the chest out in the resized panel.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
    private static class InventoryGui_UpdateContainer_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(InventoryGui __instance)
        {
            if (_panel == null || __instance.m_container != _panel)
            {
                return;
            }

            Container container = __instance.m_currentContainer;
            if (container != null && container.GetInventory() != null)
            {
                Resize(container, container.GetInventory());
            }
        }
    }

    /// <summary>
    /// After vanilla updates the chest's slots, which it lays out in the chest's own width.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    private static class InventoryGrid_UpdateGui_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryGrid __instance)
        {
            if (_panel == null || __instance != _grid || __instance.m_inventory == null)
            {
                return;
            }

            LayOutSlots(__instance);
        }
    }
}
