using System;
using System.Collections.Generic;
using System.Linq;

using BetterChests.Core;
using BetterChests.Sorting;
using BetterChests.UI;

using Jotunn.Managers;

using UnityEngine;
using UnityEngine.UI;

namespace BetterChests.Search;

/// <summary>
/// The search bar in the chest panel's title row. While something is typed in it, the chest's grid shows every item
/// in the open chest and the chests grouped with it whose name matches, as if they were one chest, sorted like a sort
/// would. Clicking an item takes that stack into the player's inventory from the chest it is really in (that chest is
/// asked for first, as when opening it). The view is rebuilt as you type, after each take, and every second.
/// </summary>
internal static class ChestSearch
{
    private const float RefreshInterval = 1f;

    /// <summary>Space between the search bar's edges and its text.</summary>
    private static readonly Vector2 TextPadding = new(8f, 2f);

    private static InputField _field;
    private static GameObject _clear;

    /// <summary>The clear button's width and height, at the right end of the search bar.</summary>
    private const float ClearButtonSize = 22f;
    private static readonly Color ClearColour = new(0.85f, 0.2f, 0.15f);
    private static Inventory _view;
    private static float _nextRefresh;

    /// <summary>Where each item shown in the view really is.</summary>
    private static readonly Dictionary<ItemDrop.ItemData, (Container Chest, ItemDrop.ItemData Item)> Sources = [];

    /// <summary>True while the search bar has the keyboard, so keys typed into it don't do anything else.</summary>
    internal static bool Focused => _field != null && _field.isFocused;

    /// <summary>True while the grid shows search results instead of the open chest.</summary>
    internal static bool Active => _field != null && _field.text.Trim().Length > 0;

    /// <summary>True if the item's name matches what is typed.</summary>
    internal static bool Matches(ItemDrop.ItemData item)
    {
        string text = _field != null ? _field.text.Trim() : "";
        return text.Length > 0
            && Localization.instance.Localize(item.m_shared.m_name).IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }

    /// <summary>Every stack the search shows, and the chest it is in.</summary>
    internal static List<(Container Chest, ItemDrop.ItemData Item)> Results()
    {
        return [.. Sources.Values];
    }

    /// <summary>Rebuilds the results on the next frame (after the chests change).</summary>
    internal static void Refresh()
    {
        _nextRefresh = 0f;
    }

    /// <summary>Adds the search bar to the title row, when the inventory screen is created.</summary>
    internal static void Create()
    {
        RectTransform row = ChestPanelLayout.TitleRow;
        if (row == null || GUIManager.Instance == null)
        {
            return;
        }

        GameObject field = GUIManager.Instance.CreateInputField(row, Vector2.zero, Vector2.one, Vector2.zero,
            InputField.ContentType.Standard, Localization.instance.Localize("$betterchests_search_placeholder"), 16);
        field.name = "BetterChests_Search";
        RectTransform rect = (RectTransform)field.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _field = field.GetComponent<InputField>();
        _field.characterLimit = 40;
        _clear = CreateClearButton(field.transform);
        _field.onValueChanged.AddListener(text => _clear.SetActive(text.Length > 0));
        _clear.SetActive(false);

        // Unity's input field insets its text by 6-7 pixels top and bottom, leaving less than a line of text in the
        // title row; legacy text that doesn't fit draws nothing, so neither the placeholder nor the typing showed.
        foreach (Text text in new[] { _field.textComponent, _field.placeholder as Text })
        {
            if (text == null)
            {
                continue;
            }
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = TextPadding;
            textRect.offsetMax = new Vector2(-TextPadding.x - ClearButtonSize, -TextPadding.y); // room for the cross
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.MiddleLeft;
        }
        _field.onValueChanged.AddListener(_ => _nextRefresh = 0f);
    }

    /// <summary>
    /// A red cross at the right end of the search bar that empties it. Shown only while something is typed. Drawn as an
    /// image: the font has no "×", and legacy text draws nothing for a missing character.
    /// </summary>
    private static GameObject CreateClearButton(Transform field)
    {
        GameObject clear = new("BetterChests_ClearSearch", typeof(RectTransform), typeof(Image), typeof(Button));
        clear.transform.SetParent(field, false);
        RectTransform rect = (RectTransform)clear.transform;
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(ClearButtonSize, ClearButtonSize);
        rect.anchoredPosition = new Vector2(-4f, 0f);

        Image cross = clear.GetComponent<Image>();
        cross.sprite = CrossSprite();
        cross.color = ClearColour;
        cross.preserveAspect = true;

        Button button = clear.GetComponent<Button>();
        button.targetGraphic = cross;
        button.onClick.AddListener(() =>
        {
            _field.text = "";
            _field.DeactivateInputField();
        });
        return clear;
    }

    private static Sprite _crossSprite;

    /// <summary>A white cross (two thick diagonal strokes, antialiased) on a transparent square, tinted by the image.</summary>
    private static Sprite CrossSprite()
    {
        if (_crossSprite != null)
        {
            return _crossSprite;
        }

        const int size = 32;
        const float halfWidth = 2.6f; // half the stroke width, in pixels
        const float inset = 6f; // how far the strokes stop from the edges
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                bool inside = px >= inset && px <= size - inset && py >= inset && py <= size - inset;
                // Distance to each diagonal, through the middle of the square.
                float toFirst = Mathf.Abs(px - py) / Mathf.Sqrt(2f);
                float toSecond = Mathf.Abs(px + py - size) / Mathf.Sqrt(2f);
                float distance = Mathf.Min(toFirst, toSecond);
                float alpha = inside ? Mathf.Clamp01(halfWidth + 0.5f - distance) : 0f;
                pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();

        _crossSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        _crossSprite.hideFlags = HideFlags.HideAndDontSave;
        return _crossSprite;
    }

    /// <summary>Empties the search when a chest is closed or another one is opened.</summary>
    internal static void Clear()
    {
        if (_field != null && _field.text.Length > 0)
        {
            _field.SetTextWithoutNotify("");
        }
        if (_clear != null)
        {
            _clear.SetActive(false);
        }
        _view = null;
        Sources.Clear();
    }

    /// <summary>
    /// What the chest grid shows (before <c>InventoryGrid.UpdateInventory</c>): while searching, the results in place
    /// of the open chest, so the grid is filled once a frame, with the results.
    /// </summary>
    internal static Inventory ShownIn(InventoryGui gui, Inventory incoming)
    {
        Container chest = gui.m_currentContainer;
        if (!Active || chest == null || incoming != chest.GetInventory())
        {
            return incoming;
        }

        if (_view == null || Time.time >= _nextRefresh)
        {
            Rebuild(chest);
        }
        return _view;
    }

    private static void Rebuild(Container chest)
    {
        _nextRefresh = Time.time + RefreshInterval;
        List<(Container Chest, ItemDrop.ItemData Item)> matches = [];
        foreach (Container member in ChestClusters.GroupOf(chest))
        {
            Inventory inventory = member != null ? member.GetInventory() : null;
            if (inventory == null)
            {
                continue;
            }
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (Matches(item))
                {
                    matches.Add((member, item));
                }
            }
        }

        List<(Container Chest, ItemDrop.ItemData Item)> sorted = [.. matches
            .OrderBy(m => ItemCategories.KeyOf(m.Item))
            .ThenBy(m => Localization.instance.Localize(m.Item.m_shared.m_name), StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(m => m.Item.m_quality)
            .ThenByDescending(m => m.Item.m_stack)];

        // As wide as the open chest, so the panel's layout (wardrobe columns and all) is the same.
        Inventory own = chest.GetInventory();
        int width = Mathf.Max(1, own.GetWidth());
        int height = Mathf.Max(own.GetHeight(), (sorted.Count + width - 1) / width);
        _view = new Inventory("$betterchests_search_results", null, width, height);
        Sources.Clear();
        for (int i = 0; i < sorted.Count; i++)
        {
            ItemDrop.ItemData shown = sorted[i].Item.Clone();
            shown.m_gridPos = new Vector2i(i % width, i / width);
            _view.m_inventory.Add(shown);
            Sources[shown] = sorted[i];
        }
    }

    /// <summary>
    /// A click on the chest grid (before <c>InventoryGui.OnSelectedItem</c>). While searching, a click on an item takes
    /// that stack, and an item dragged from the player's inventory goes into the group (see
    /// <see cref="SearchActions.Drop"/>), as does an item Ctrl + clicked in the player's inventory. True if handled here,
    /// so vanilla should skip it.
    /// </summary>
    internal static bool TryHandleClick(InventoryGui gui, InventoryGrid grid, ItemDrop.ItemData item, InventoryGrid.Modifier mod)
    {
        if (!Active)
        {
            return false;
        }

        // Ctrl + click on the player's inventory: into the group instead of straight into the open chest. Unequipped
        // first, as vanilla does.
        Player player = Player.m_localPlayer;
        if (grid == gui.m_playerGrid && mod == InventoryGrid.Modifier.Move && item != null && player != null
            && gui.m_currentContainer != null && !item.m_shared.m_questItem)
        {
            player.RemoveEquipAction(item);
            player.UnequipItem(item);
            SearchActions.Drop(gui.m_currentContainer, player.GetInventory(), item, item.m_stack);
            return true;
        }

        if (grid != gui.m_containerGrid)
        {
            return false;
        }

        if (gui.m_dragItem != null)
        {
            ItemDrop.ItemData dragged = gui.m_dragItem;
            Inventory from = gui.m_dragInventory;
            int amount = gui.m_dragAmount;
            gui.SetupDragItem(null, null, 1);
            SearchActions.Drop(gui.m_currentContainer, from, dragged, amount);
        }
        else if (item != null && Sources.TryGetValue(item, out (Container Chest, ItemDrop.ItemData Item) source))
        {
            if (mod == InventoryGrid.Modifier.Split && source.Item.m_stack > 1)
            {
                Split(gui, source.Chest, source.Item);
            }
            else
            {
                Take(source.Chest, source.Item);
            }
        }
        return true;
    }

    /// <summary>
    /// Shift + click on a result: vanilla's split dialog for the real stack in its real chest. The amount chosen is then
    /// dragged from that chest by vanilla, as if the chest were open, so the copy shown is never involved.
    /// </summary>
    private static void Split(InventoryGui gui, Container chest, ItemDrop.ItemData item)
    {
        ChestRequests.Request(chest, granted =>
        {
            Inventory from = chest.GetInventory();
            if (!granted)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$msg_inuse");
            }
            else if (from != null && from.ContainsItem(item) && gui != null)
            {
                gui.ShowSplitDialog(item, from);
            }
        });
    }

    /// <summary>
    /// A right-click on the chest grid (before <c>InventoryGui.OnRightClickItem</c>). While searching, it uses the real
    /// item in its real chest, never the copy shown (see <see cref="Use"/>). True if handled here, so vanilla should
    /// skip it.
    /// </summary>
    internal static bool TryHandleRightClick(InventoryGui gui, InventoryGrid grid, ItemDrop.ItemData item)
    {
        if (!Active || grid != gui.m_containerGrid)
        {
            return false;
        }
        if (item != null && Sources.TryGetValue(item, out (Container Chest, ItemDrop.ItemData Item) source))
        {
            Use(source.Chest, source.Item);
        }
        return true;
    }

    /// <summary>
    /// Eats or drinks a food or mead straight from its chest, as right-clicking it in that chest would. Gear can only be
    /// equipped from the player's inventory, so it comes into the inventory first and is then equipped (ammo that joins
    /// a stack already carried equips that stack).
    /// </summary>
    private static void Use(Container chest, ItemDrop.ItemData item)
    {
        Player player = Player.m_localPlayer;
        if (chest == null || player == null)
        {
            return;
        }

        ChestRequests.Request(chest, granted =>
        {
            Inventory from = chest.GetInventory();
            if (!granted)
            {
                player.Message(MessageHud.MessageType.Center, "$msg_inuse");
            }
            else if (from != null && from.ContainsItem(item))
            {
                if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable)
                {
                    player.UseItem(from, item, true);
                }
                else if (item.IsEquipable())
                {
                    Inventory carried = player.GetInventory();
                    carried.MoveItemToThis(from, item);
                    // A stackable item (ammo) may have joined a stack already carried; anything else must have come
                    // across itself (it doesn't if the inventory is full), so a different piece is never equipped.
                    ItemDrop.ItemData equip = null;
                    if (carried.ContainsItem(item))
                    {
                        equip = item;
                    }
                    else if (item.m_shared.m_maxStackSize > 1 && !from.ContainsItem(item))
                    {
                        equip = FindSameType(carried, item);
                    }
                    if (equip != null && !player.IsItemEquiped(equip))
                    {
                        player.UseItem(carried, equip, true);
                    }
                }
            }
            _nextRefresh = 0f;
        });
    }

    private static ItemDrop.ItemData FindSameType(Inventory inventory, ItemDrop.ItemData item)
    {
        foreach (ItemDrop.ItemData candidate in inventory.GetAllItems())
        {
            if (candidate.IsSameType(item))
            {
                return candidate;
            }
        }
        return null;
    }

    private static void Take(Container chest, ItemDrop.ItemData item)
    {
        Player player = Player.m_localPlayer;
        if (chest == null || player == null)
        {
            return;
        }

        ChestRequests.Request(chest, granted =>
        {
            Inventory from = chest.GetInventory();
            if (!granted)
            {
                player.Message(MessageHud.MessageType.Center, "$msg_inuse");
            }
            else if (from != null && from.ContainsItem(item))
            {
                player.GetInventory().MoveItemToThis(from, item);
                if (InventoryGui.instance != null)
                {
                    _ = InventoryGui.instance.m_moveItemEffects.Create(player.transform.position, Quaternion.identity);
                }
            }
            _nextRefresh = 0f;
        });
    }
}
