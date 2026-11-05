using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using HarmonyLib;

using Jotunn.Configs;
using Jotunn.Managers;

using UnityEngine;

namespace BetterChests;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(Jotunn.Main.ModGuid)]
public class BetterChestsPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "javidahmed64592.betterchests";
    public const string PluginName = "BetterChests";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource _log;

    internal static Keybind _takeAllKey;
    internal static Keybind _placeStacksKey;
    internal static Keybind _sortKey;
    internal static Keybind _splitDecreaseKey;
    internal static Keybind _splitIncreaseKey;

    private Harmony _harmony;

    private void Awake()
    {
        _log = Logger;

        _takeAllKey = BindKey("Take All", "TakeAll", KeyCode.H,
            "While a chest is open, moves everything in it into your inventory " +
            "(the same as the \"Take all\" button).");
        _placeStacksKey = BindKey("Place Stacks", "PlaceStacks", KeyCode.F,
            "While a chest is open, moves items from your inventory onto matching stacks in the chest " +
            "(the same as the \"Place stacks\" button).");
        _sortKey = BindKey("Sort", "Sort", KeyCode.G,
            "While a chest is open, sorts its contents (the same as the \"Sort\" button).");
        _splitDecreaseKey = BindKey("Split Stack Decrease", "SplitDecrease", KeyCode.R,
            "While splitting a stack, takes one fewer item. Hold to keep decreasing.");
        _splitIncreaseKey = BindKey("Split Stack Increase", "SplitIncrease", KeyCode.T,
            "While splitting a stack, takes one more item. Hold to keep increasing.");

        AutoDeposit.BindConfig(Config);

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
            description + " Also rebindable in game from Settings > Mod Settings.");

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

    public bool Held()
    {
        return ZInput.GetKey(entry.Value);
    }
}
