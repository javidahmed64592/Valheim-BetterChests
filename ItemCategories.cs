using System;
using System.Collections.Generic;

namespace BetterChests;

/// <summary>
/// Groups items the way players think of them (wood, ores, seeds, food and so on), for sorting. Vanilla
/// items are listed by prefab name in rough progression order; other items (from mods or later game
/// updates) are placed by keywords in their prefab name, or failing that by their item type.
/// </summary>
internal static class ItemCategories
{
    /// <summary>
    /// The groups, in sort order: building materials, then farming and food, then gear.
    /// </summary>
    private enum Category
    {
        Wood = 0,
        Stone = 1,
        Ore = 2,
        Metal = 3,
        CreatureParts = 4,
        Valuables = 5,
        OtherMaterials = 6,
        Seeds = 7,
        Crops = 8,
        RawMeat = 9,
        Food = 10,
        Meads = 11,
        Ammo = 12,
        Weapons = 13,
        Shields = 14,
        Tools = 15,
        Armour = 16,
        Utility = 17,
        Trophies = 18,
        Misc = 19,
    }

    private static readonly (Category Category, string[] Prefabs)[] Known =
    [
        (Category.Wood, ["Wood", "FineWood", "RoundLog", "ElderBark", "YggdrasilWood", "Blackwood"]),
        (Category.Stone, ["Stone", "Flint", "Coal", "Obsidian", "Crystal", "Tar", "Sap", "BlackMarble", "Grausten", "SulfurStone", "ProustitePowder"]),
        (Category.Ore, ["CopperOre", "CopperScrap", "TinOre", "BronzeScrap", "IronScrap", "IronOre", "SilverOre", "BlackMetalScrap", "FlametalOre", "FlametalOreNew"]),
        (Category.Metal, ["Copper", "Tin", "Bronze", "BronzeNails", "Iron", "IronNails", "Chain", "Silver", "BlackMetal", "MechanicalSpring", "Eitr", "Flametal", "FlametalNew", "LinenThread", "JuteRed", "JuteBlue"]),
        (Category.CreatureParts,
        [
            "LeatherScraps", "DeerHide", "Feathers", "Resin", "GreydwarfEye", "BoneFragments", "TrollHide", "HardAntler",
            "Guck", "Entrails", "Ooze", "Bloodbag", "Chitin", "SerpentScale", "BjornHide", "WolfPelt", "WolfFang", "WolfClaw",
            "WolfHairBundle", "FreezeGland", "LoxPelt", "Needle", "ScaleHide", "Carapace", "Mandible", "Softtissue",
            "RoyalJelly", "GiantBloodSack", "Bilebag", "AskHide", "CharredBone", "CelestialFeather", "MorgenSinew",
            "MorgenHeart", "BoneMawSerpentScale", "AsksvinCarrionNeck", "AsksvinCarrionPelvic", "AsksvinCarrionSkull",
            "Ectoplasm",
        ]),
        (Category.Valuables,
        [
            "Coins", "Amber", "AmberPearl", "Ruby", "SilverNecklace", "SurtlingCore", "AncientSeed", "Wishbone",
            "Thunderstone", "DragonTear", "DragonEgg", "YmirRemains", "BlackCore", "QueenBee", "GemstoneBlue",
            "GemstoneGreen", "GemstoneRed", "MoltenCore",
        ]),
        (Category.Seeds, ["CarrotSeeds", "TurnipSeeds", "OnionSeeds", "VineberrySeeds", "Acorn", "BeechSeeds", "BirchSeeds", "FirCone", "PineCone"]),
        (Category.Crops,
        [
            "Barley", "BarleyFlour", "Flax", "Carrot", "Turnip", "Onion", "Vineberry", "Raspberry", "Blueberries",
            "Cloudberry", "Honey", "Mushroom", "MushroomYellow", "MushroomBlue", "MushroomJotunPuffs", "MushroomMagecap",
            "MushroomSmokePuff", "Thistle", "Dandelion", "Root", "Fiddleheadfern", "Pukeberries",
        ]),
        (Category.RawMeat,
        [
            "RawMeat", "NeckTail", "DeerMeat", "HareMeat", "WolfMeat", "BjornMeat", "SerpentMeat", "LoxMeat", "ChickenMeat",
            "ChickenEgg", "BugMeat", "AsksvinMeat", "VoltureMeat", "VoltureEgg", "BoneMawSerpentMeat", "FishRaw",
        ]),
    ];

    /// <summary>
    /// For items not listed above: keywords in the prefab name, checked in order.
    /// </summary>
    private static readonly (Category Category, string[] Keywords)[] Keywords =
    [
        (Category.Seeds, ["Seed", "Cone", "Acorn"]),
        (Category.Ore, ["Ore", "Scrap"]),
        (Category.Wood, ["Wood", "Log", "Bark"]),
        (Category.RawMeat, ["Meat", "Egg"]),
        (Category.CreatureParts, ["Hide", "Pelt", "Scale", "Bone", "Fang", "Claw", "Feather", "Leather", "Sinew", "Antler"]),
    ];

    private static readonly Dictionary<string, (Category Category, int Index)> KnownByPrefab = BuildKnown();

    private static Dictionary<string, (Category, int)> BuildKnown()
    {
        Dictionary<string, (Category, int)> known = new(StringComparer.Ordinal);
        foreach ((Category category, string[] prefabs) in Known)
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                known[prefabs[i]] = (category, i);
            }
        }

        return known;
    }

    /// <summary>
    /// Sort key for <paramref name="item"/>: its group, then its place within the group. Items with no
    /// listed place come last in their group (and are then ordered by name).
    /// </summary>
    public static (int Group, int Index) SortKey(ItemDrop.ItemData item)
    {
        string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
        if (prefab != null && KnownByPrefab.TryGetValue(prefab, out (Category Category, int Index) known))
        {
            return ((int)known.Category, known.Index);
        }

        return ((int)Classify(item, prefab), int.MaxValue);
    }

    private static Category Classify(ItemDrop.ItemData item, string prefab)
    {
        ItemDrop.ItemData.SharedData shared = item.m_shared;
        return shared.m_itemType switch
        {
            ItemDrop.ItemData.ItemType.Material => prefab != null ? ByKeyword(prefab) : Category.OtherMaterials,
            ItemDrop.ItemData.ItemType.Consumable =>
                shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f ? Category.Food : Category.Meads,
            ItemDrop.ItemData.ItemType.Fish => Category.RawMeat,
            ItemDrop.ItemData.ItemType.Ammo or ItemDrop.ItemData.ItemType.AmmoNonEquipable => Category.Ammo,
            ItemDrop.ItemData.ItemType.OneHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeapon
                or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft or ItemDrop.ItemData.ItemType.Bow
                or ItemDrop.ItemData.ItemType.Attach_Atgeir => Category.Weapons,
            ItemDrop.ItemData.ItemType.Shield => Category.Shields,
            ItemDrop.ItemData.ItemType.Torch or ItemDrop.ItemData.ItemType.Tool => Category.Tools,
            ItemDrop.ItemData.ItemType.Helmet or ItemDrop.ItemData.ItemType.Chest or ItemDrop.ItemData.ItemType.Legs
                or ItemDrop.ItemData.ItemType.Shoulder or ItemDrop.ItemData.ItemType.Hands => Category.Armour,
            ItemDrop.ItemData.ItemType.Utility or ItemDrop.ItemData.ItemType.Trinket => Category.Utility,
            ItemDrop.ItemData.ItemType.Trophy => Category.Trophies,
            _ => Category.Misc,
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
}
