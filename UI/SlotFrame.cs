using UnityEngine;
using UnityEngine.UI;

namespace BetterChests.UI;

/// <summary>
/// A coloured frame around an inventory slot: four thin bars, so the item stays visible inside it.
/// </summary>
internal static class SlotFrame
{
    private const float Thickness = 2f;
    private const float Inset = 1f;

    /// <summary>
    /// Shows or hides the frame called <paramref name="name"/> on the slot, creating it the first time.
    /// </summary>
    internal static void Show(InventoryElement element, string name, Color colour, bool show)
    {
        Transform frame = element.transform.Find(name);
        if (frame != null)
        {
            if (frame.gameObject.activeSelf != show)
            {
                frame.gameObject.SetActive(show);
            }
        }
        else if (show)
        {
            Create(element, name, colour);
        }
    }

    private static void Create(InventoryElement element, string name, Color colour)
    {
        GameObject frame = new(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)frame.transform;
        rect.SetParent(element.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(Inset, Inset);
        rect.offsetMax = new Vector2(-Inset, -Inset);

        AddBar(rect, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -Thickness), Vector2.zero, colour);
        AddBar(rect, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, Thickness), colour);
        AddBar(rect, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(Thickness, 0f), colour);
        AddBar(rect, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-Thickness, 0f), Vector2.zero, colour);
    }

    private static void AddBar(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color colour)
    {
        GameObject bar = new(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)bar.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Image image = bar.GetComponent<Image>();
        image.color = colour;
        // Clicks go through to the slot.
        image.raycastTarget = false;
    }
}
