using System;
using System.Collections.Generic;

using UnityEngine;

namespace BetterChests.Sorting;

/// <summary>
/// <para>
/// Where an item goes when a chest is sorted. Items are grouped the way players think of them (wood, ores, bars,
/// hides, food, weapons and so on), and ordered within each group:
/// </para>
/// <list type="bullet">
/// <item>Food (anything edible) by the stat it raises most, health then stamina then eitr, lowest first.</item>
/// <item>Armour by set (sets in tier order), then slot: helmet, chest, legs, hands, cape.</item>
/// <item>Weapons by class, one-handed before two-handed, then tier.</item>
/// <item>Moulds and casts next to the gear they make, in the same order as that gear.</item>
/// <item>Upgrade kits: armour kits by tier, then weapon kits by tier.</item>
/// <item>Vanilla materials in a listed order (by tier); other items by tier (see <see cref="ItemTiers"/>).</item>
/// </list>
/// </summary>
internal static class ItemCategories
{
    /// <summary>The groups, in sort order: building materials, then farming and food, then gear.</summary>
    private enum Category
    {
        Wood = 0,
        Stone = 1,
        Ore = 2,
        Bars = 3,
        MetalParts = 4,
        Hides = 5,
        CreatureParts = 6,
        Valuables = 7,
        OtherMaterials = 8,
        UpgradeKits = 9,
        GearParts = 10,
        Seeds = 11,
        Crops = 12,
        RawMeat = 13,
        Food = 14,
        Meads = 15,
        Ammo = 16,
        Weapons = 17,
        Shields = 18,
        Tools = 19,
        Armour = 20,
        Utility = 21,
        Trophies = 22,
        Misc = 23,
    }

    /// <summary>Vanilla materials in a fixed order (roughly by tier) within their group.</summary>
    private static readonly (Category Category, string[] Prefabs)[] Known =
    [
        (Category.Wood, ["Wood", "FineWood", "RoundLog", "ElderBark", "YggdrasilWood", "Blackwood", "Frostwood"]),
        (Category.Stone, ["Stone", "Flint", "Coal", "Obsidian", "Crystal", "Tar", "Sap", "BlackMarble", "Grausten", "SulfurStone", "ProustitePowder", "Ice"]),
        (Category.Ore, ["CopperOre", "CopperScrap", "TinOre", "BronzeScrap", "IronScrap", "IronOre", "SilverOre", "BlackMetalScrap", "FlametalOre", "FlametalOreNew", "GoldOre"]),
        (Category.Bars, ["Copper", "Tin", "Bronze", "Iron", "Silver", "BlackMetal", "Flametal", "FlametalNew", "Gold"]),
        (Category.MetalParts, ["BronzeNails", "IronNails", "Chain", "MechanicalSpring", "CharredCogwheel"]),
        (Category.Hides, ["LeatherScraps", "DeerHide", "TrollHide", "BjornHide", "WolfPelt", "LoxPelt", "ScaleHide", "AskHide", "SealHide", "MooseHide"]),
        (Category.CreatureParts,
        [
            "Feathers", "Resin", "GreydwarfEye", "BoneFragments", "HardAntler", "Guck", "Entrails", "Ooze", "Bloodbag",
            "Chitin", "SerpentScale", "WolfFang", "WolfClaw", "WolfHairBundle", "FreezeGland", "Needle", "Carapace",
            "Mandible", "Softtissue", "RoyalJelly", "GiantBloodSack", "Bilebag", "CharredBone", "CelestialFeather",
            "MorgenSinew", "MorgenHeart", "BoneMawSerpentScale", "AsksvinCarrionNeck", "AsksvinCarrionPelvic",
            "AsksvinCarrionSkull", "Ectoplasm",
        ]),
        (Category.Valuables,
        [
            "Coins", "Amber", "AmberPearl", "Ruby", "SilverNecklace", "SurtlingCore", "AncientSeed", "Wishbone",
            "Thunderstone", "DragonTear", "DragonEgg", "YmirRemains", "BlackCore", "QueenBee", "GemstoneBlue",
            "GemstoneGreen", "GemstoneRed", "MoltenCore",
        ]),
        (Category.OtherMaterials, ["Eitr", "LinenThread", "JuteRed", "JuteBlue", "NornThread"]),
        (Category.Seeds, ["CarrotSeeds", "TurnipSeeds", "OnionSeeds", "VineberrySeeds", "Acorn", "BeechSeeds", "BirchSeeds", "FirCone", "PineCone"]),
        (Category.Crops, ["Barley", "BarleyFlour", "Flax", "Carrot", "Turnip", "Onion", "Thistle", "Dandelion", "Root", "Fiddleheadfern"]),
        (Category.RawMeat,
        [
            "RawMeat", "NeckTail", "DeerMeat", "HareMeat", "WolfMeat", "BjornMeat", "SerpentMeat", "LoxMeat", "ChickenMeat",
            "ChickenEgg", "BugMeat", "AsksvinMeat", "VoltureMeat", "VoltureEgg", "BoneMawSerpentMeat", "FishRaw",
        ]),
    ];

    /// <summary>For materials not listed above: keywords in the prefab name, checked in order.</summary>
    private static readonly (Category Category, string[] Keywords)[] Keywords =
    [
        (Category.Seeds, ["Seed", "Cone", "Acorn"]),
        (Category.Ore, ["Ore", "Scrap"]),
        (Category.Wood, ["Wood", "Log", "Bark"]),
        (Category.RawMeat, ["Meat", "Egg"]),
        (Category.Hides, ["Hide", "Pelt", "Leather"]),
        (Category.CreatureParts, ["Scale", "Bone", "Fang", "Claw", "Feather", "Sinew", "Antler"]),
    ];

    /// <summary>Weapon classes in sort order, by the skill they use. Others come after.</summary>
    private static readonly Skills.SkillType[] WeaponClasses =
    [
        Skills.SkillType.Swords, Skills.SkillType.Axes, Skills.SkillType.Clubs, Skills.SkillType.Spears,
        Skills.SkillType.Polearms, Skills.SkillType.Knives, Skills.SkillType.Unarmed, Skills.SkillType.Bows,
        Skills.SkillType.Crossbows, Skills.SkillType.ElementalMagic, Skills.SkillType.BloodMagic,
    ];

    private const string UpgradeKitToken = "$item_upgrader_tier";
    private const string MouldToken = "_mold_";

    private static readonly Dictionary<string, (Category Category, int Index)> KnownByPrefab = BuildKnown();

    private static Dictionary<string, (Category, int)> BuildKnown()
    {
        Dictionary<string, (Category, int)> known = []; // string keys compare ordinally by default
        foreach ((Category category, string[] prefabs) in Known)
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                known[prefabs[i]] = (category, i);
            }
        }
        return known;
    }

    /// <summary>Where an item sorts. Compared field by field, in order; ties then go by name.</summary>
    internal readonly struct SortKey(int group, int sub, int tier, string family, int slot, float value) : IComparable<SortKey>
    {
        public readonly int Group = group;
        public readonly int Sub = sub;
        public readonly int Tier = tier;
        public readonly string Family = family ?? "";
        public readonly int Slot = slot;
        public readonly float Value = value;

        public int CompareTo(SortKey other)
        {
            int result = Group.CompareTo(other.Group);
            if (result == 0)
            {
                result = Sub.CompareTo(other.Sub);
            }
            if (result == 0)
            {
                result = Tier.CompareTo(other.Tier);
            }
            if (result == 0)
            {
                result = string.CompareOrdinal(Family, other.Family);
            }
            if (result == 0)
            {
                result = Slot.CompareTo(other.Slot);
            }
            if (result == 0)
            {
                result = Value.CompareTo(other.Value);
            }
            return result;
        }

        public override string ToString()
        {
            string tier = Tier == ItemTiers.Unknown ? "?" : Tier.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} sub {1} tier {2} {3} slot {4} value {5}", (Category)Group, Sub, tier, Family, Slot, Value);
        }
    }

    internal static bool IsGear(ItemDrop.ItemData.ItemType type)
    {
        return IsWeapon(type)
            || type is ItemDrop.ItemData.ItemType.Shield or ItemDrop.ItemData.ItemType.Helmet or ItemDrop.ItemData.ItemType.Chest
                or ItemDrop.ItemData.ItemType.Legs or ItemDrop.ItemData.ItemType.Hands or ItemDrop.ItemData.ItemType.Shoulder
                or ItemDrop.ItemData.ItemType.Tool;
    }

    private static bool IsWeapon(ItemDrop.ItemData.ItemType type)
    {
        return type is ItemDrop.ItemData.ItemType.OneHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeapon
            or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft or ItemDrop.ItemData.ItemType.Bow
            or ItemDrop.ItemData.ItemType.Attach_Atgeir;
    }

    internal static SortKey KeyOf(ItemDrop.ItemData item)
    {
        string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
        return KeyOf(item.m_shared, prefab);
    }

    private static SortKey KeyOf(ItemDrop.ItemData.SharedData shared, string prefab)
    {
        if (IsFood(shared))
        {
            (int stat, float value) = FoodStat(shared);
            return new SortKey((int)Category.Food, stat, 0, "", 0, value);
        }

        string name = shared.m_name ?? "";
        if (name.StartsWith(UpgradeKitToken, StringComparison.Ordinal))
        {
            return UpgradeKitKey(name);
        }

        if (IsGear(shared.m_itemType) || shared.m_itemType is ItemDrop.ItemData.ItemType.Ammo
            or ItemDrop.ItemData.ItemType.AmmoNonEquipable or ItemDrop.ItemData.ItemType.Torch
            or ItemDrop.ItemData.ItemType.Utility or ItemDrop.ItemData.ItemType.Trinket or ItemDrop.ItemData.ItemType.Trophy)
        {
            return GearKey(shared, prefab);
        }

        if (shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable)
        {
            return new SortKey((int)Category.Meads, 1, ItemTiers.Of(prefab), "", 0, 0f);
        }

        if (prefab != null && KnownByPrefab.TryGetValue(prefab, out (Category Category, int Index) known))
        {
            return new SortKey((int)known.Category, 0, 0, "", known.Index, 0f);
        }

        // A mould or cast: next to the gear it makes, the mould first.
        ItemDrop gear = ItemTiers.GearMadeFrom(prefab);
        if (gear != null)
        {
            SortKey of = GearKey(gear.m_itemData.m_shared, gear.name);
            return new SortKey((int)Category.GearParts, (of.Group * 100) + of.Sub, of.Tier, of.Family, of.Slot,
                name.Contains(MouldToken) ? 0f : 1f);
        }
        if (name.Contains(MouldToken))
        {
            return new SortKey((int)Category.GearParts, int.MaxValue, ItemTiers.Of(prefab), "", 0, 0f);
        }

        // Meat, fish and eggs from later biomes or other mods, with the raw meat.
        if (ItemTiers.IsRawFood(prefab) && shared.m_itemType is ItemDrop.ItemData.ItemType.Material or ItemDrop.ItemData.ItemType.Fish)
        {
            return new SortKey((int)Category.RawMeat, 1, ItemTiers.Of(prefab), "", 0, 0f);
        }

        // An ingredient made into one food or mead (an uncooked dish, a mead base): after that food or with the meads.
        ItemDrop consumable = ItemTiers.ConsumableMadeFrom(prefab);
        if (consumable != null)
        {
            ItemDrop.ItemData.SharedData made = consumable.m_itemData.m_shared;
            if (IsFood(made))
            {
                (int stat, float value) = FoodStat(made);
                return new SortKey((int)Category.Food, 3 + stat, 0, "", 0, value);
            }
            return new SortKey((int)Category.Meads, 0, ItemTiers.Of(prefab), "", 0, 0f);
        }

        Category category = shared.m_itemType switch
        {
            ItemDrop.ItemData.ItemType.Material => prefab != null ? ByKeyword(prefab) : Category.OtherMaterials,
            ItemDrop.ItemData.ItemType.Fish => Category.RawMeat,
            _ => Category.Misc,
        };
        return new SortKey((int)category, 1, ItemTiers.Of(prefab), "", 0, 0f);
    }

    private static bool IsFood(ItemDrop.ItemData.SharedData shared)
    {
        return shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable
            && (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f);
    }

    /// <summary>The stat a food raises most (0 health, 1 stamina, 2 eitr; ties go in that order) and how much.</summary>
    private static (int Stat, float Value) FoodStat(ItemDrop.ItemData.SharedData shared)
    {
        (int stat, float value) = (0, shared.m_food);
        if (shared.m_foodStamina > value)
        {
            (stat, value) = (1, shared.m_foodStamina);
        }
        if (shared.m_foodEitr > value)
        {
            (stat, value) = (2, shared.m_foodEitr);
        }
        return (stat, value);
    }

    /// <summary>"$item_upgrader_tier3 $item_upgrader_armor …": armour kits first, each kind by tier.</summary>
    private static SortKey UpgradeKitKey(string name)
    {
        int tier = 0;
        for (int index = UpgradeKitToken.Length; index < name.Length && char.IsDigit(name[index]); index++)
        {
            tier = (tier * 10) + (name[index] - '0');
        }
        int kind = name.Contains("$item_upgrader_weapon") ? 1 : 0;
        return new SortKey((int)Category.UpgradeKits, kind, tier, "", 0, 0f);
    }

    private static SortKey GearKey(ItemDrop.ItemData.SharedData shared, string prefab)
    {
        int tier = ItemTiers.Of(prefab);
        ItemDrop.ItemData.ItemType type = shared.m_itemType;

        if (IsWeapon(type))
        {
            if (shared.m_skillType is Skills.SkillType.Pickaxes or Skills.SkillType.WoodCutting)
            {
                return new SortKey((int)Category.Tools, 0, tier, "", 0, 0f);
            }
            int index = Array.IndexOf(WeaponClasses, shared.m_skillType);
            int weaponClass = index >= 0 ? index : WeaponClasses.Length;
            bool twoHanded = type is ItemDrop.ItemData.ItemType.TwoHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft;
            return new SortKey((int)Category.Weapons, (weaponClass * 2) + (twoHanded ? 1 : 0), tier, "", 0, 0f);
        }

        switch (type)
        {
            case ItemDrop.ItemData.ItemType.Shield:
                return new SortKey((int)Category.Shields, 0, tier, "", 0, shared.m_blockPower);
            case ItemDrop.ItemData.ItemType.Helmet:
            case ItemDrop.ItemData.ItemType.Chest:
            case ItemDrop.ItemData.ItemType.Legs:
            case ItemDrop.ItemData.ItemType.Hands:
            case ItemDrop.ItemData.ItemType.Shoulder:
                string set = shared.m_setName;
                int setTier = ItemTiers.OfSet(set);
                return new SortKey((int)Category.Armour, 0, setTier != ItemTiers.Unknown ? setTier : tier,
                    string.IsNullOrEmpty(set) ? "~" : set, SlotOrder(type), 0f);
            case ItemDrop.ItemData.ItemType.Ammo:
            case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                return new SortKey((int)Category.Ammo, 0, 0, shared.m_ammoType, 0, tier);
            case ItemDrop.ItemData.ItemType.Tool:
            case ItemDrop.ItemData.ItemType.Torch:
                return new SortKey((int)Category.Tools, 1, tier, "", 0, 0f);
            case ItemDrop.ItemData.ItemType.Utility:
            case ItemDrop.ItemData.ItemType.Trinket:
                return new SortKey((int)Category.Utility, 0, tier, "", 0, 0f);
            case ItemDrop.ItemData.ItemType.Trophy:
                return new SortKey((int)Category.Trophies, 0, tier, "", 0, 0f);
            default:
                return new SortKey((int)Category.Misc, 0, tier, "", 0, 0f);
        }
    }

    private static int SlotOrder(ItemDrop.ItemData.ItemType type)
    {
        return type switch
        {
            ItemDrop.ItemData.ItemType.Helmet => 0,
            ItemDrop.ItemData.ItemType.Chest => 1,
            ItemDrop.ItemData.ItemType.Legs => 2,
            ItemDrop.ItemData.ItemType.Hands => 3,
            _ => 4,
        };
    }

    private static Category ByKeyword(string prefab)
    {
        foreach ((Category category, string[] keywords) in Keywords)
        {
            foreach (string keyword in keywords)
            {
                if (prefab.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return category;
                }
            }
        }
        return Category.OtherMaterials;
    }

    /// <summary>Every item's sort key and tier, for checking the order (the betterchests_sortinfo command).</summary>
    internal static List<string> Describe()
    {
        ItemTiers.Rebuild();
        List<(SortKey Key, string Line)> lines = [];
        if (ObjectDB.instance == null)
        {
            return [];
        }
        foreach (GameObject itemPrefab in ObjectDB.instance.m_items)
        {
            ItemDrop item = itemPrefab != null ? itemPrefab.GetComponent<ItemDrop>() : null;
            if (item == null)
            {
                continue;
            }
            SortKey key = KeyOf(item.m_itemData.m_shared, item.name);
            string localized = Localization.instance.Localize(item.m_itemData.m_shared.m_name);
            lines.Add((key, $"{key} | {item.name} | {localized}"));
        }
        lines.Sort((a, b) => a.Key.CompareTo(b.Key));
        return lines.ConvertAll(l => l.Line);
    }
}
