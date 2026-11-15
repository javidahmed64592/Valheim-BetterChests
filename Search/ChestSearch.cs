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
            textRect.offsetMax = -TextPadding;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.MiddleLeft;
        }
        _field.onValueChanged.AddListener(_ => _nextRefresh = 0f);
    }

    /// <summary>Empties the search when a chest is closed or another one is opened.</summary>
    internal static void Clear()
    {
        if (_field != null && _field.text.Length > 0)
        {
            _field.SetTextWithoutNotify("");
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
    /// <see cref="SearchActions.Drop"/>). True if handled here, so vanilla should skip it.
    /// </summary>
    internal static bool TryHandleClick(InventoryGui gui, InventoryGrid grid, ItemDrop.ItemData item)
    {
        if (!Active || grid != gui.m_containerGrid)
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
            Take(source.Chest, source.Item);
        }
        return true;
    }

    /// <summary>A right-click on the view would use the copy (eat it, equip it); it does nothing instead.</summary>
    internal static bool IsResultsGrid(InventoryGui gui, InventoryGrid grid)
    {
        return Active && grid == gui.m_containerGrid;
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
