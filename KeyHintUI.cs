using System.Collections.Generic;
using System.Text.RegularExpressions;

using HarmonyLib;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace BetterChests;

/// <summary>
/// Adds this mod's keys to the vanilla inventory key hints (bottom of the screen while the inventory is
/// open). Each hint is a copy of the vanilla "Use" hint of the same row, and they follow the configured
/// keys. The split amount hint is a copy of the build mode "Rotate" hint, which shows the mouse wheel.
/// </summary>
internal static class KeyHintUI
{
    private const string ItemPrefix = "BetterChests_Hint_";

    private static readonly Regex WordBreak = new("(?<=[a-z0-9])(?=[A-Z])");

    private enum HintVisibility { ContainerOnly = 0, SplitDialogOnly = 1 }

    private sealed class HintDef
    {
        public string Label;
        /// <summary>
        /// Read when needed: the keybinds are created in the plugin's Awake. Null for the scroll hint.
        /// </summary>
        public System.Func<Keybind> Key;
        public HintVisibility Visibility;
        /// <summary>
        /// Shown with the mouse wheel icon instead of keys.
        /// </summary>
        public bool Scroll;
    }

    private static readonly HintDef[] Hints =
    [
        new HintDef { Label = "Take all", Key = () => BetterChestsPlugin._takeAllKey, Visibility = HintVisibility.ContainerOnly },
        new HintDef { Label = "Place stacks", Key = () => BetterChestsPlugin._placeStacksKey, Visibility = HintVisibility.ContainerOnly },
        new HintDef { Label = "Deposit all", Key = () => BetterChestsPlugin._depositAllKey, Visibility = HintVisibility.ContainerOnly },
        new HintDef { Label = "Sort", Key = () => BetterChestsPlugin._sortKey, Visibility = HintVisibility.ContainerOnly },
        new HintDef { Label = "Split amount", Scroll = true, Visibility = HintVisibility.SplitDialogOnly },
    ];

    /// <summary>
    /// The hints added to one vanilla row (the inventory row, or the inventory-with-chest row).
    /// </summary>
    private sealed class Row
    {
        public RectTransform Keyboard;
        public GameObject[] Items;
        /// <summary>
        /// Null for hints without a key.
        /// </summary>
        public TMP_Text[] KeyTexts;
        public bool Failed;
    }

    private static readonly Dictionary<GameObject, Row> Rows = [];

    private static void Update(KeyHints keyHints)
    {
        InventoryGui gui = InventoryGui.instance;
        if (gui == null || !InventoryGui.IsVisible())
        {
            return;
        }

        bool containerOpen = gui.IsContainerOpen();
        bool splitting = gui.m_splitDialog != null && gui.m_splitDialog.IsActive;

        UpdateRow(keyHints, keyHints.m_inventoryHints, false, splitting);
        UpdateRow(keyHints, keyHints.m_inventoryWithContainerHints, containerOpen, splitting);
    }

    private static void UpdateRow(KeyHints keyHints, GameObject root, bool containerOpen, bool splitting)
    {
        if (root == null || !root.activeInHierarchy)
        {
            return;
        }

        if (!Rows.TryGetValue(root, out Row row))
        {
            row = Create(keyHints, root);
            Rows[root] = row;
        }

        if (row.Failed || !row.Keyboard.gameObject.activeInHierarchy)
        {
            return;
        }

        bool dirty = false;
        for (int i = 0; i < Hints.Length; i++)
        {
            HintDef hint = Hints[i];
            bool visible = hint.Visibility == HintVisibility.SplitDialogOnly
                ? splitting
                : containerOpen && !splitting;

            GameObject item = row.Items[i];
            if (item.activeSelf != visible)
            {
                item.SetActive(visible);
                dirty = true;
            }

            if (!visible)
            {
                continue;
            }

            // The keys are configurable, so keep the key boxes up to date.
            TMP_Text text = row.KeyTexts[i];
            if (hint.Key != null && text != null)
            {
                string keyName = KeyName(hint.Key().Key);
                if (text.text != keyName)
                {
                    text.text = keyName;
                    dirty = true;
                }
            }
        }

        if (dirty)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(row.Keyboard);
        }
    }

    private static Row Create(KeyHints keyHints, GameObject root)
    {
        Row row = new();

        Transform keyboard = root.transform.Find("Keyboard");
        Transform template = keyboard != null ? FindTemplate(keyboard) : null;
        if (template == null || keyboard.GetComponent<HorizontalLayoutGroup>() == null)
        {
            row.Failed = true;
            BetterChestsPlugin._log.LogWarning("Vanilla inventory key hints layout not as expected, not adding key hints to " + root.name + ".");
            return row;
        }

        row.Keyboard = (RectTransform)keyboard;
        row.Items = new GameObject[Hints.Length];
        row.KeyTexts = new TMP_Text[Hints.Length];

        Transform scrollTemplate = FindScrollTemplate(keyHints);

        for (int i = 0; i < Hints.Length; i++)
        {
            HintDef hint = Hints[i];
            if (hint.Scroll)
            {
                row.Items[i] = CreateScrollHint(hint, template, scrollTemplate, keyboard);
            }
            else
            {
                row.Items[i] = CreateItem(hint, template, keyboard);
                row.KeyTexts[i] = row.Items[i].transform.Find("key_bkg").GetComponentInChildren<TMP_Text>(true);
            }
        }

        return row;
    }

    private static GameObject CreateItem(HintDef hint, Transform template, Transform keyboard)
    {
        GameObject item = Object.Instantiate(template.gameObject, keyboard, false);
        item.name = ItemPrefix + hint.Label;
        item.SetActive(false);

        Transform label = item.transform.Find("Text");
        // Vanilla labels can have a fixed width sized for their own text.
        LayoutElement fixedSize = label.GetComponent<LayoutElement>();
        if (fixedSize != null)
        {
            Object.DestroyImmediate(fixedSize);
        }

        label.GetComponent<TMP_Text>().text = hint.Label;
        return item;
    }

    /// <summary>
    /// A copy of the build mode "Rotate" hint (a label and a mouse wheel icon), or failing that a key hint
    /// reading "Mouse wheel".
    /// </summary>
    private static GameObject CreateScrollHint(HintDef hint, Transform keyTemplate, Transform scrollTemplate, Transform keyboard)
    {
        if (scrollTemplate != null)
        {
            return CreateItem(hint, scrollTemplate, keyboard);
        }

        GameObject item = CreateItem(hint, keyTemplate, keyboard);
        item.transform.Find("key_bkg").GetComponentInChildren<TMP_Text>(true).text = "Mouse wheel";
        return item;
    }

    private static Transform FindScrollTemplate(KeyHints keyHints)
    {
        if (keyHints.m_buildHints == null)
        {
            return null;
        }

        Transform rotate = keyHints.m_buildHints.transform.Find("Keyboard/rotate");
        if (rotate == null || rotate.Find("mousew_icon") == null)
        {
            return null;
        }

        Transform label = rotate.Find("Text");
        return label != null && label.GetComponent<TMP_Text>() != null ? rotate : null;
    }

    /// <summary>
    /// The vanilla "Use" hint (a label and one key box), or failing that any hint shaped like it.
    /// </summary>
    private static Transform FindTemplate(Transform keyboard)
    {
        Transform use = keyboard.Find("Use");
        if (IsSingleKeyHint(use))
        {
            return use;
        }

        foreach (Transform child in keyboard)
        {
            if (IsSingleKeyHint(child))
            {
                return child;
            }
        }

        return null;
    }

    private static bool IsSingleKeyHint(Transform item)
    {
        if (item == null || item.name.StartsWith(ItemPrefix, System.StringComparison.Ordinal))
        {
            return false;
        }

        Transform label = item.Find("Text");
        Transform key = item.Find("key_bkg");
        return label != null && label.GetComponent<TMP_Text>() != null
            && key != null && key.GetComponentInChildren<TMP_Text>(true) != null;
    }

    /// <summary>
    /// "LeftControl" -> "Left Ctrl", "Alpha1" -> "1", "F" -> "F".
    /// </summary>
    private static string KeyName(KeyCode key)
    {
        string name = key.ToString();
        if (name.StartsWith("Alpha", System.StringComparison.Ordinal))
        {
            return name.Substring(5);
        }

        if (name.StartsWith("Keypad", System.StringComparison.Ordinal))
        {
            return "Num " + name.Substring(6);
        }

        name = name.Replace("Control", "Ctrl");
        return WordBreak.Replace(name, " ");
    }

    [HarmonyPatch(typeof(KeyHints), "UpdateHints")]
    private static class KeyHints_UpdateHints_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(KeyHints __instance)
        {
            // Drop rows whose HUD was destroyed (it is rebuilt with each world load).
            RemoveDestroyed();
            Update(__instance);
        }
    }

    private static void RemoveDestroyed()
    {
        List<GameObject> stale = null;
        foreach (GameObject root in Rows.Keys)
        {
            if (root == null)
            {
                (stale ??= []).Add(root);
            }
        }

        if (stale == null)
        {
            return;
        }

        foreach (GameObject root in stale)
        {
            _ = Rows.Remove(root);
        }
    }
}
