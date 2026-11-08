using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using HarmonyLib;

using Jotunn.Configs;
using Jotunn.Managers;
using Jotunn.Utils;

using UnityEngine;

namespace BetterChests;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
public class BetterChestsPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "javidahmed64592.betterchests";
    public const string PluginName = "BetterChests";
    public const string PluginVersion = "0.4.0";

    internal static ManualLogSource _log;

    internal static Keybind _takeAllKey;
    internal static Keybind _placeStacksKey;
    internal static Keybind _depositAllKey;
    internal static Keybind _sortKey;
    internal static Keybind _keepItemKey;

    private Harmony _harmony;

    private void Awake()
    {
        _log = Logger;

        // Laid out like the chest panel's buttons: R and T for the top row, F and G for the row below.
        _takeAllKey = BindKey("Take All", "TakeAll", KeyCode.R,
            "While a chest is open, moves everything in it into your inventory " +
            "(the same as the \"Take all\" button).");
        _placeStacksKey = BindKey("Place Stacks", "PlaceStacks", KeyCode.T,
            "While a chest is open, moves items from your inventory onto matching stacks in the chest " +
            "(the same as the \"Place stacks\" button).");
        _depositAllKey = BindKey("Deposit All", "DepositAll", KeyCode.F,
            "While a chest is open, moves everything in your inventory into it, except your hotbar and " +
            "equipped items (the same as the \"Deposit all\" button).");
        _sortKey = BindKey("Sort", "Sort", KeyCode.G,
            "While a chest is open, sorts its contents (the same as the \"Sort\" button).");
        _keepItemKey = BindKey("Keep Item", "KeepItem", KeyCode.LeftAlt,
            "Hold and right-click an item in your inventory to mark it to be kept by \"Deposit all\" and " +
            "\"Place stacks\", or to unmark it. The left and right versions of Shift, Ctrl and Alt both work.");

        AutoDeposit.BindConfig(Config);
        Translations.Load();

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }

    private Keybind BindKey(string configName, string buttonName, KeyCode defaultKey, string description)
    {
        ConfigEntry<KeyCode> entry = Config.Bind("Keybinds", configName, defaultKey,
            description);

        // Registered so the key shows up as a key binding in Jotunn's Mod Settings menu.
        InputManager.Instance.AddButton(PluginGuid, new ButtonConfig
        {
            Name = "BetterChests_" + buttonName,
            Config = entry
        });

        return new Keybind(entry);
    }

    /// <summary>
    /// True while typing somewhere (chat, console, a text dialog), so letter keys must not trigger actions.
    /// </summary>
    internal static bool TextInputActive()
    {
        return (Chat.instance != null && Chat.instance.HasFocus())
            || Console.IsVisible()
            || TextInput.IsVisible();
    }
}

/// <summary>
/// A rebindable key. It is read straight from the keyboard, the way the inventory screen reads Escape:
/// the registered button did not respond while the inventory was open.
/// </summary>
internal sealed class Keybind(ConfigEntry<KeyCode> entry)
{
    public KeyCode Key => entry.Value;

    public bool Down()
    {
        return ZInput.GetKeyDown(entry.Value);
    }

    /// <summary>
    /// For modifier keys, either side counts: Left Alt or Right Alt.
    /// </summary>
    public bool Held()
    {
        KeyCode key = entry.Value;
        return ZInput.GetKey(key) || (OtherSide(key) is KeyCode other && ZInput.GetKey(other));
    }

    private static KeyCode? OtherSide(KeyCode key)
    {
        return key switch
        {
            KeyCode.LeftShift => KeyCode.RightShift,
            KeyCode.RightShift => KeyCode.LeftShift,
            KeyCode.LeftControl => KeyCode.RightControl,
            KeyCode.RightControl => KeyCode.LeftControl,
            KeyCode.LeftAlt => KeyCode.RightAlt,
            KeyCode.RightAlt => KeyCode.LeftAlt,
            _ => null,
        };
    }
}
