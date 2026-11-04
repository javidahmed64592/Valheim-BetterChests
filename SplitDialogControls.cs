using HarmonyLib;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace BetterChests;

/// <summary>
/// Adds "-" and "+" buttons either side of the split stack slider, and keys that do the same (held
/// down, they repeat).
/// </summary>
internal static class SplitDialogControls
{
    private const float ButtonSize = 36f;
    private const float ButtonGap = 8f;
    private const float LabelFontSize = 26f;

    private const float RepeatDelay = 0.4f;
    private const float RepeatInterval = 0.05f;

    private static int _heldStep;
    private static float _nextRepeat;

    private static void Step(SplitDialog dialog, int delta)
    {
        Slider slider = dialog.m_splitSlider;
        float value = Mathf.Clamp(slider.value + delta, slider.minValue, slider.maxValue);
        if (!Mathf.Approximately(value, slider.value))
        {
            dialog.SliderValue = value;
        }
    }

    private static void HandleKeys(SplitDialog dialog)
    {
        if (BetterChestsPlugin.TextInputActive())
        {
            return;
        }

        float now = Time.unscaledTime;
        if (BetterChestsPlugin._splitDecreaseKey.Down())
        {
            StartHold(dialog, -1, now);
        }
        else if (BetterChestsPlugin._splitIncreaseKey.Down())
        {
            StartHold(dialog, 1, now);
        }
        else if (_heldStep != 0)
        {
            Keybind key = _heldStep < 0 ? BetterChestsPlugin._splitDecreaseKey : BetterChestsPlugin._splitIncreaseKey;
            if (!key.Held())
            {
                _heldStep = 0;
            }
            else if (now >= _nextRepeat)
            {
                Step(dialog, _heldStep);
                _nextRepeat = now + RepeatInterval;
            }
        }
    }

    private static void StartHold(SplitDialog dialog, int step, float now)
    {
        Step(dialog, step);
        _heldStep = step;
        _nextRepeat = now + RepeatDelay;
    }

    /// <summary>
    /// Narrows the slider to make room for a button at each end. The buttons are copies of the dialog's OK
    /// button, so they match the vanilla style.
    /// </summary>
    private static void CreateButtons(SplitDialog dialog)
    {
        Slider slider = dialog.m_splitSlider;
        Button template = dialog.m_splitOkButton;
        if (slider == null || template == null || slider.transform.parent == null)
        {
            BetterChestsPlugin._log.LogWarning("Vanilla split dialog layout not as expected, not adding the +/- buttons.");
            return;
        }

        RectTransform sliderRect = (RectTransform)slider.transform;
        const float shrink = 2f * (ButtonSize + ButtonGap);
        sliderRect.sizeDelta = new Vector2(sliderRect.sizeDelta.x - shrink, sliderRect.sizeDelta.y);

        float offset = (sliderRect.rect.width / 2f) + ButtonGap + (ButtonSize / 2f);
        AddButton(dialog, template, sliderRect, "BetterChests_SplitDecrease", "-", -offset, -1);
        AddButton(dialog, template, sliderRect, "BetterChests_SplitIncrease", "+", offset, 1);
    }

    private static void AddButton(SplitDialog dialog, Button template, RectTransform sliderRect, string name, string text, float offset, int delta)
    {
        GameObject go = Object.Instantiate(template.gameObject, sliderRect.parent, false);
        go.name = name;

        // The copy would otherwise also react to the OK button's keys.
        foreach (UIGamePad gamePad in go.GetComponentsInChildren<UIGamePad>(true))
        {
            if (gamePad.m_hint != null)
            {
                Object.DestroyImmediate(gamePad.m_hint);
            }

            Object.DestroyImmediate(gamePad);
        }

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = sliderRect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        // The slider's pivot may not be its centre.
        Vector2 sliderCentre = sliderRect.anchoredPosition
            + new Vector2((0.5f - sliderRect.pivot.x) * sliderRect.rect.width, (0.5f - sliderRect.pivot.y) * sliderRect.rect.height);
        rect.anchoredPosition = sliderCentre + new Vector2(offset, 0f);

        TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;
            label.enableAutoSizing = false;
            label.fontSize = LabelFontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.text = text;
        }

        Button button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() => Step(dialog, delta));
    }

    [HarmonyPatch(typeof(InventoryGui), "Awake")]
    private static class InventoryGui_Awake_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryGui __instance)
        {
            if (__instance.m_splitDialog != null)
            {
                CreateButtons(__instance.m_splitDialog);
            }
        }
    }

    /// <summary>
    /// Runs every frame while the inventory is open; the vanilla method does nothing unless the dialog is.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "UpdateSplitDialog")]
    private static class InventoryGui_UpdateSplitDialog_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryGui __instance)
        {
            if (__instance.m_splitDialog.m_splitSlider.gameObject.activeInHierarchy)
            {
                HandleKeys(__instance.m_splitDialog);
            }
            else
            {
                _heldStep = 0;
            }
        }
    }
}
