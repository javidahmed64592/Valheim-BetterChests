using System.Collections.Generic;
using System.Text.RegularExpressions;

using HarmonyLib;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace BetterChests;

/// <summary>
/// Adds this mod's keys to the vanilla inventory key hints (bottom of the screen while the inventory is
/// open). Each hint is a copy of the vanilla "Use" hint of the same row, with a "/" and a second key box
/// added for the split stack keys. They follow the configured keys.
/// </summary>
internal static class KeyHintUI
{
    private const string ItemPrefix = "BetterChests_Hint_";
    private const string Separator = "/";

    private static readonly Regex WordBreak = new("(?<=[a-z0-9])(?=[A-Z])");

    private enum HintVisibility { ContainerOnly = 0, SplitDialogOnly = 1 }

    private sealed class HintDef
    {
        public string Label;
        /// <summary>
        /// read when needed: the keybinds are created in the plugin's Awake
        /// </summary>
        public System.Func<Keybind>[] Keys;
        public HintVisibility Visibility;
    }

    private static readonly HintDef[] Hints =
    [
        new HintDef { Label = "Take all", Keys = [() => BetterChestsPlugin._takeAllKey], Visibility = HintVisibility.ContainerOnly },
        new HintDef { Label = "Place stacks", Keys = [() => BetterChestsPlugin._placeStacksKey], Visibility = HintVisibility.ContainerOnly },
        new HintDef { Label = "Sort", Keys = [() => BetterChestsPlugin._sortKey], Visibility = HintVisibility.ContainerOnly },
        new HintDef
        {
            Label = "Split amount",
            Keys = [() => BetterChestsPlugin._splitDecreaseKey, () => BetterChestsPlugin._splitIncreaseKey],
            Visibility = HintVisibility.SplitDialogOnly
        },
    ];

    /// <summary>
    /// The hints added to one vanilla row (the inventory row, or the inventory-with-chest row).
    /// </summary>
    private sealed class Row
    {
        public RectTransform Keyboard;
        public GameObject[] Items;
        public TMP_Text[][] KeyTexts;
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

        UpdateRow(keyHints.m_inventoryHints, false, splitting);
        UpdateRow(keyHints.m_inventoryWithContainerHints, containerOpen, splitting);
    }

    private static void UpdateRow(GameObject root, bool containerOpen, bool splitting)
    {
        if (root == null || !root.activeInHierarchy)
        {
            return;
        }

        if (!Rows.TryGetValue(root, out Row row))
        {
            row = Create(root);
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
            for (int k = 0; k < hint.Keys.Length; k++)
            {
                string keyName = KeyName(hint.Keys[k]().Key);
                TMP_Text text = row.KeyTexts[i][k];
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

    private static Row Create(GameObject root)
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

        Transform separatorTemplate = FindSeparator(keyboard);

        row.Keyboard = (RectTransform)keyboard;
        row.Items = new GameObject[Hints.Length];
        row.KeyTexts = new TMP_Text[Hints.Length][];

        for (int i = 0; i < Hints.Length; i++)
        {
            HintDef hint = Hints[i];
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

            Transform key = item.transform.Find("key_bkg");
            row.KeyTexts[i] = new TMP_Text[hint.Keys.Length];
            row.KeyTexts[i][0] = key.GetComponentInChildren<TMP_Text>(true);

            // Further keys go after the first, each preceded by a "/".
            Transform previous = key;
            for (int k = 1; k < hint.Keys.Length; k++)
            {
                // Unity objects need an explicit null check rather than ??.
                Transform separatorSource = label;
                if (separatorTemplate != null)
                {
                    separatorSource = separatorTemplate;
                }

                GameObject separator = Object.Instantiate(separatorSource.gameObject, item.transform, false);
                separator.name = "separator";
                separator.SetActive(true);
                separator.GetComponent<TMP_Text>().text = Separator;
                separator.transform.SetSiblingIndex(previous.GetSiblingIndex() + 1);

                GameObject extraKey = Object.Instantiate(key.gameObject, item.transform, false);
                extraKey.name = "key_bkg (" + k + ")";
                extraKey.transform.SetSiblingIndex(separator.transform.GetSiblingIndex() + 1);
                row.KeyTexts[i][k] = extraKey.GetComponentInChildren<TMP_Text>(true);
                previous = extraKey.transform;
            }

            row.Items[i] = item;
        }

        return row;
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
    /// The "+" between the keys of a vanilla multi-key hint, styled for sitting between key boxes.
    /// </summary>
    private static Transform FindSeparator(Transform keyboard)
    {
        foreach (Transform child in keyboard)
        {
            Transform plus = child.Find("plus");
            if (plus != null && plus.GetComponent<TMP_Text>() != null)
            {
                return plus;
            }
        }

        return null;
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
