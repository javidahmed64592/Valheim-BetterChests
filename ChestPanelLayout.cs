using HarmonyLib;

using UnityEngine;
using UnityEngine.UI;

namespace BetterChests;

/// <summary>
/// Makes room in the chest panel for the buttons added under "Take all" and "Place stacks", and makes the panel taller so
/// that every row of a big chest fits without scrolling. The panel hangs below the player's inventory and
/// grows downwards; it stops at the bottom of the screen, beyond which the chest scrolls as normal.
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

    private static RectTransform _panel;
    private static InventoryGrid _grid;
    private static float _baseHeight;
    private static float _gridTop;
    private static float _lastHeight = -1f;

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

        _baseHeight = _panel.rect.height;
        _lastHeight = -1f;

        // Pin the header buttons and the scroll bar to the top, so the panel can grow under them.
        PinToTop((RectTransform)gui.m_takeAllButton.transform);
        PinToTop((RectTransform)gui.m_stackAllButton.transform);
        float vanillaGridTop = -gridRect.offsetMax.y;
        _gridTop = vanillaGridTop;
        foreach (RectTransform button in buttons)
        {
            PinToTop(button);
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

    private static void Resize(Inventory inventory)
    {
        float wanted = _gridTop + (inventory.GetHeight() * _grid.m_elementSpace) + GridPadding;
        float height = Mathf.Min(Mathf.Max(_baseHeight, wanted), MaxHeight());
        if (Mathf.Approximately(height, _lastHeight))
        {
            return;
        }

        _lastHeight = height;
        _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, height);
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
                Resize(container.GetInventory());
            }
        }
    }
}
