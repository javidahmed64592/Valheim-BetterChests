using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using BetterChests.Core;
using BetterChests.Deposit;
using BetterChests.Duplicates;
using BetterChests.Keys;
using BetterChests.Search;
using BetterChests.Sorting;

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
    public const string PluginName = "Better Chests";
    public const string PluginVersion = "0.8.1";

    internal static ManualLogSource _log;

    internal static Keybind _restockKey;
    internal static Keybind _takeAllKey;
    internal static Keybind _depositAllKey;
    internal static Keybind _placeStacksKey;
    internal static Keybind _sortKey;
    internal static Keybind _sortGroupKey;
    internal static Keybind _keepItemKey;

    private Harmony _harmony;

    private void Awake()
    {
        _log = Logger;

        // In the chest panel's order, left to right along the keyboard's home row and the row above.
        _restockKey = BindKey("Restock", "Restock", KeyCode.R,
            "While a chest is open, or while looking at one, tops up every partial stack you carry (the hotbar included) " +
            "from that chest and every chest grouped with it.");
        _takeAllKey = BindKey("Take All", "TakeAll", KeyCode.T,
            "While a chest is open, or while looking at one, moves everything in it into your inventory " +
            "(the same as the \"Take all\" button).");
        _depositAllKey = BindKey("Deposit All", "DepositAll", KeyCode.F,
            "While a chest is open, or while looking at one, moves everything in your inventory into it, except your hotbar and " +
            "equipped items (the same as the \"Deposit all\" button).");
        _placeStacksKey = BindKey("Place Stacks", "PlaceStacks", KeyCode.G,
            "While a chest is open, or while looking at one, moves items from your inventory (except your hotbar, equipped and marked items) onto matching stacks in the chest " +
            "(the same as the \"Place stacks\" button).");
        _sortKey = BindKey("Sort", "Sort", KeyCode.H,
            "While a chest is open, or while looking at one, sorts its contents (the same as the \"Sort\" button).");
        _sortGroupKey = BindKey("Sort Group Modifier", "SortGroupModifier", KeyCode.LeftShift,
            "Hold with the Sort key or button to sort the chest's whole group: each item is gathered into a chest of its own " +
            "as far as the chests allow, and every chest is sorted. The left and right versions of Shift, Ctrl and Alt both work.");
        _keepItemKey = BindKey("Keep Item", "KeepItem", KeyCode.LeftAlt,
            "Hold and right-click an item in your inventory to mark it to be kept by \"Deposit all\" and " +
            "\"Place stacks\", or to unmark it. The left and right versions of Shift, Ctrl and Alt both work.");

        AutoDeposit.BindConfig(Config);
        ChestDuplicates.BindConfig(Config);
        ChestKeys.BindConfig(Config);
        Translations.Load();
        CommandManager.Instance.AddConsoleCommand(new SortInfoCommand());

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
        return ChestSearch.Focused
            || (Chat.instance != null && Chat.instance.HasFocus())
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
