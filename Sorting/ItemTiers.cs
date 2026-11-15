using System.Collections.Generic;

using UnityEngine;

namespace BetterChests.Sorting;

/// <summary>
/// <para>
/// How far into the game each item is, worked out from the game's own data so that items from later updates and
/// other mods sort sensibly too. An item's tier is the earliest biome it can be found in: dropped by a creature
/// that spawns there, or by a tree, rock or plant that grows there. Items made from others (crafted, smelted,
/// cooked or fermented) take the highest tier of what they are made from. Items found neither way have no tier.
/// </para>
/// <para>
/// Also records which piece of gear a part is for: a mould is used only to make one cast, which is fired into one
/// weapon or armour piece, so the mould and cast can sort next to that gear's set or weapon class.
/// </para>
/// <para>Worked out once per world, the first time it is needed.</para>
/// </summary>
internal static class ItemTiers
{
    internal const int Unknown = int.MaxValue;

    /// <summary>How many steps of "used only to make" to follow from a part to the gear it makes.</summary>
    private const int MaxChain = 4;

    private static ObjectDB _builtFor;
    private static readonly Dictionary<string, int> Tiers = [];
    private static readonly Dictionary<string, ItemDrop> GearFor = [];
    private static readonly Dictionary<string, ItemDrop> ConsumableFor = [];
    private static readonly HashSet<string> RawFoods = [];
    private static readonly HashSet<string> Harvested = [];
    private static readonly Dictionary<string, int> ArmourFamilyTiers = [];

    /// <summary>The item's tier (1 = Meadows … 8 = Deep North), or <see cref="Unknown"/>.</summary>
    internal static int Of(string prefab)
    {
        Build();
        return prefab != null && Tiers.TryGetValue(prefab, out int tier) ? tier : Unknown;
    }

    /// <summary>The weapon, shield or armour piece this part (a mould or a cast) ends up as, if it is used for one.</summary>
    internal static ItemDrop GearMadeFrom(string prefab)
    {
        Build();
        return prefab != null && GearFor.TryGetValue(prefab, out ItemDrop gear) ? gear : null;
    }

    /// <summary>The food or mead this ingredient (such as an uncooked dish) ends up as, if it is used for only one.</summary>
    internal static ItemDrop ConsumableMadeFrom(string prefab)
    {
        Build();
        return prefab != null && ConsumableFor.TryGetValue(prefab, out ItemDrop consumable) ? consumable : null;
    }

    /// <summary>
    /// True for something picked from a plant, wild or grown from a seed (berries, mushrooms, carrots, barley…).
    /// </summary>
    internal static bool IsHarvested(string prefab)
    {
        Build();
        return prefab != null && Harvested.Contains(prefab);
    }

    /// <summary>The lowest tier among an armour set's pieces (see <see cref="ItemCategories.ArmourFamily"/>).</summary>
    internal static int OfArmourFamily(string family)
    {
        Build();
        return family != null && ArmourFamilyTiers.TryGetValue(family, out int tier) ? tier : Unknown;
    }

    /// <summary>
    /// True for something cooked as it was found (meat, fish, eggs): a cooking station's input that no recipe makes.
    /// Uncooked dishes are made by a recipe, so they don't count.
    /// </summary>
    internal static bool IsRawFood(string prefab)
    {
        Build();
        return prefab != null && RawFoods.Contains(prefab);
    }

    /// <summary>Biomes in the order the game unlocks them. An area in several biomes counts as the earliest.</summary>
    private static int BiomeTier(Heightmap.Biome biome)
    {
        int best = Unknown;
        foreach ((Heightmap.Biome flag, int tier) in new[]
        {
            (Heightmap.Biome.Meadows, 1), (Heightmap.Biome.BlackForest, 2), (Heightmap.Biome.Swamp, 3),
            (Heightmap.Biome.Ocean, 3), (Heightmap.Biome.Mountain, 4), (Heightmap.Biome.Plains, 5),
            (Heightmap.Biome.Mistlands, 6), (Heightmap.Biome.AshLands, 7), (Heightmap.Biome.DeepNorth, 8),
        })
        {
            if ((biome & flag) != 0 && tier < best)
            {
                best = tier;
            }
        }
        return best;
    }

    private static void Build()
    {
        ObjectDB db = ObjectDB.instance;
        if (db == null || db == _builtFor || ZNetScene.instance == null || ZoneSystem.instance == null)
        {
            return;
        }

        _builtFor = db;
        Tiers.Clear();
        GearFor.Clear();
        ConsumableFor.Clear();
        RawFoods.Clear();
        Harvested.Clear();
        ArmourFamilyTiers.Clear();

        AddCreatureSources();
        AddVegetationSources();

        // What each item is made into, by recipe or by a station's conversion.
        Dictionary<string, HashSet<ItemDrop>> madeInto = [];
        List<(List<string> Inputs, ItemDrop Output)> makes = [];
        HashSet<string> crafted = [];
        HashSet<string> cooked = [];
        foreach (Recipe recipe in db.m_recipes)
        {
            if (recipe == null || recipe.m_item == null || !recipe.m_enabled || recipe.m_resources == null)
            {
                continue;
            }

            List<string> inputs = [];
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (requirement?.m_resItem != null)
                {
                    inputs.Add(requirement.m_resItem.name);
                }
            }
            if (inputs.Count > 0)
            {
                makes.Add((inputs, recipe.m_item));
                _ = crafted.Add(recipe.m_item.name);
            }
        }

        foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
        {
            if (prefab == null)
            {
                continue;
            }
            if (prefab.TryGetComponent(out Smelter smelter))
            {
                AddConversions(makes, smelter.m_conversion, c => (c.m_from, c.m_to));
            }
            if (prefab.TryGetComponent(out CookingStation cooking))
            {
                AddConversions(makes, cooking.m_conversion, c => (c.m_from, c.m_to));
                foreach (CookingStation.ItemConversion conversion in cooking.m_conversion)
                {
                    if (conversion?.m_from != null)
                    {
                        _ = cooked.Add(conversion.m_from.name);
                    }
                }
            }
            if (prefab.TryGetComponent(out Fermenter fermenter))
            {
                AddConversions(makes, fermenter.m_conversion, c => (c.m_from, c.m_to));
            }
            if (prefab.TryGetComponent(out Plant plant) && prefab.TryGetComponent(out Piece planted))
            {
                AddCrops(makes, plant, planted);
            }
        }

        foreach (string input in cooked)
        {
            if (!crafted.Contains(input))
            {
                _ = RawFoods.Add(input);
            }
        }

        foreach ((List<string> inputs, ItemDrop output) in makes)
        {
            foreach (string input in inputs)
            {
                if (!madeInto.TryGetValue(input, out HashSet<ItemDrop> outputs))
                {
                    outputs = [];
                    madeInto[input] = outputs;
                }
                _ = outputs.Add(output);
            }
        }

        // An item made from others is as far into the game as the furthest of them. Repeated until nothing changes,
        // since a bar comes from an ore, a nail from a bar, and so on.
        for (int pass = 0; pass < 10; pass++)
        {
            bool changed = false;
            foreach ((List<string> inputs, ItemDrop output) in makes)
            {
                int tier = 0;
                foreach (string input in inputs)
                {
                    if (Tiers.TryGetValue(input, out int inputTier))
                    {
                        tier = Mathf.Max(tier, inputTier);
                    }
                }
                if (tier > 0)
                {
                    changed |= Lower(output.name, tier);
                }
            }
            if (!changed)
            {
                break;
            }
        }

        foreach (GameObject itemPrefab in db.m_items)
        {
            ItemDrop item = itemPrefab != null ? itemPrefab.GetComponent<ItemDrop>() : null;
            if (item == null)
            {
                continue;
            }

            string family = ItemCategories.ArmourFamily(item.m_itemData.m_shared, item.name);
            if (family != null && Tiers.TryGetValue(item.name, out int pieceTier)
                && (!ArmourFamilyTiers.TryGetValue(family, out int familyTier) || pieceTier < familyTier))
            {
                ArmourFamilyTiers[family] = pieceTier;
            }

            FollowChain(item.name, madeInto);
        }

        BetterChestsPlugin._log.LogInfo($"Sorting: tiers for {Tiers.Count} items, {GearFor.Count} gear parts, {ConsumableFor.Count} ingredients");
    }

    private static void AddConversions<T>(List<(List<string>, ItemDrop)> makes, List<T> conversions, System.Func<T, (ItemDrop From, ItemDrop To)> fields)
    {
        if (conversions == null)
        {
            return;
        }
        foreach (T conversion in conversions)
        {
            (ItemDrop from, ItemDrop to) = fields(conversion);
            if (from != null && to != null)
            {
                makes.Add(([from.name], to));
            }
        }
    }

    /// <summary>
    /// Records the one piece of gear (or else the one food or mead) that everything this item is made into ends up as.
    /// A mould makes a cast, which is fired into a sword, and the same mould also makes the sword directly with an
    /// upgrade kit: both lead to the same sword, so the mould is for that sword. A bar of gold makes many things, so it
    /// is for none.
    /// </summary>
    private static void FollowChain(string prefab, Dictionary<string, HashSet<ItemDrop>> madeInto)
    {
        ItemDrop gear = EndsAs(prefab, madeInto, 0, wantGear: true);
        if (gear != null)
        {
            GearFor[prefab] = gear;
            return;
        }

        ItemDrop consumable = EndsAs(prefab, madeInto, 0, wantGear: false);
        if (consumable != null)
        {
            ConsumableFor[prefab] = consumable;
        }
    }

    /// <summary>The single gear piece (or consumable) all of this item's uses end up as, or null if none or several.</summary>
    private static ItemDrop EndsAs(string prefab, Dictionary<string, HashSet<ItemDrop>> madeInto, int depth, bool wantGear)
    {
        if (depth >= MaxChain || !madeInto.TryGetValue(prefab, out HashSet<ItemDrop> outputs))
        {
            return null;
        }

        ItemDrop found = null;
        foreach (ItemDrop output in outputs)
        {
            ItemDrop.ItemData.ItemType type = output.m_itemData.m_shared.m_itemType;
            bool isEnd = wantGear ? ItemCategories.IsGear(type) : type == ItemDrop.ItemData.ItemType.Consumable;
            ItemDrop end = isEnd ? output : EndsAs(output.name, madeInto, depth + 1, wantGear);
            if (end == null)
            {
                continue; // a use that leads nowhere in particular (say, a by-product) doesn't decide it
            }
            if (found != null && found.name != end.name)
            {
                return null;
            }
            found = end;
        }
        return found;
    }

    /// <summary>
    /// A planted crop gives what it grows into; its tier is its seed's, since a crop like carrots only grows from
    /// seeds and is never found wild.
    /// </summary>
    private static void AddCrops(List<(List<string>, ItemDrop)> makes, Plant plant, Piece planted)
    {
        List<string> seeds = [];
        foreach (Piece.Requirement requirement in planted.m_resources)
        {
            if (requirement?.m_resItem != null)
            {
                seeds.Add(requirement.m_resItem.name);
            }
        }
        if (seeds.Count == 0 || plant.m_grownPrefabs == null)
        {
            return;
        }

        foreach (GameObject grown in plant.m_grownPrefabs)
        {
            Pickable pickable = grown != null ? grown.GetComponent<Pickable>() : null;
            ItemDrop crop = pickable != null && pickable.m_itemPrefab != null ? pickable.m_itemPrefab.GetComponent<ItemDrop>() : null;
            if (crop != null)
            {
                makes.Add((seeds, crop));
                _ = Harvested.Add(crop.name);
            }
        }
    }

    /// <summary>Creatures' drops, at the tier of the earliest biome they spawn in (also in raids).</summary>
    private static void AddCreatureSources()
    {
        GameObject zoneCtrl = ZoneSystem.instance.m_zoneCtrlPrefab;
        SpawnSystem spawnSystem = zoneCtrl != null ? zoneCtrl.GetComponent<SpawnSystem>() : null;
        if (spawnSystem != null)
        {
            foreach (SpawnSystemList list in spawnSystem.m_spawnLists)
            {
                if (list == null)
                {
                    continue;
                }
                foreach (SpawnSystem.SpawnData spawn in list.m_spawners)
                {
                    if (spawn != null && spawn.m_enabled)
                    {
                        AddCreature(spawn.m_prefab, BiomeTier(spawn.m_biome));
                    }
                }
            }
        }

        if (RandEventSystem.instance != null)
        {
            foreach (RandomEvent raid in RandEventSystem.instance.m_events)
            {
                if (raid?.m_spawn == null)
                {
                    continue;
                }
                foreach (SpawnSystem.SpawnData spawn in raid.m_spawn)
                {
                    AddCreature(spawn?.m_prefab, BiomeTier(raid.m_biome));
                }
            }
        }
    }

    private static void AddCreature(GameObject creature, int tier)
    {
        if (creature == null || tier == Unknown || !creature.TryGetComponent(out CharacterDrop drops))
        {
            return;
        }
        foreach (CharacterDrop.Drop drop in drops.m_drops)
        {
            if (drop?.m_prefab != null)
            {
                _ = Lower(drop.m_prefab.name, tier);
            }
        }
    }

    /// <summary>What trees, rocks, ore deposits and plants growing in each biome give.</summary>
    private static void AddVegetationSources()
    {
        foreach (ZoneSystem.ZoneVegetation vegetation in ZoneSystem.instance.m_vegetation)
        {
            if (vegetation != null && vegetation.m_enable && vegetation.m_prefab != null)
            {
                AddDropsOf(vegetation.m_prefab, BiomeTier(vegetation.m_biome), 0);
            }
        }
    }

    private static void AddDropsOf(GameObject prefab, int tier, int depth)
    {
        if (prefab == null || tier == Unknown || depth > 3)
        {
            return;
        }

        foreach (Pickable pickable in prefab.GetComponentsInChildren<Pickable>(true))
        {
            AddItem(pickable.m_itemPrefab, tier);
            AddTable(pickable.m_extraDrops, tier);
            if (pickable.m_itemPrefab != null)
            {
                _ = Harvested.Add(pickable.m_itemPrefab.name);
            }
        }
        foreach (PickableItem pickable in prefab.GetComponentsInChildren<PickableItem>(true))
        {
            AddItem(pickable.m_itemPrefab != null ? pickable.m_itemPrefab.gameObject : null, tier);
            foreach (PickableItem.RandomItem random in pickable.m_randomItemPrefabs)
            {
                AddItem(random.m_itemPrefab != null ? random.m_itemPrefab.gameObject : null, tier);
            }
        }
        foreach (TreeBase tree in prefab.GetComponentsInChildren<TreeBase>(true))
        {
            AddTable(tree.m_dropWhenDestroyed, tier);
            AddDropsOf(tree.m_logPrefab, tier, depth + 1);
        }
        foreach (TreeLog log in prefab.GetComponentsInChildren<TreeLog>(true))
        {
            AddTable(log.m_dropWhenDestroyed, tier);
            AddDropsOf(log.m_subLogPrefab, tier, depth + 1);
        }
        foreach (MineRock rock in prefab.GetComponentsInChildren<MineRock>(true))
        {
            AddTable(rock.m_dropItems, tier);
        }
        foreach (MineRock5 rock in prefab.GetComponentsInChildren<MineRock5>(true))
        {
            AddTable(rock.m_dropItems, tier);
        }
        foreach (DropOnDestroyed drop in prefab.GetComponentsInChildren<DropOnDestroyed>(true))
        {
            AddTable(drop.m_dropWhenDestroyed, tier);
        }
        foreach (Destructible destructible in prefab.GetComponentsInChildren<Destructible>(true))
        {
            AddDropsOf(destructible.m_spawnWhenDestroyed, tier, depth + 1);
        }
    }

    private static void AddTable(DropTable table, int tier)
    {
        if (table?.m_drops == null)
        {
            return;
        }
        foreach (DropTable.DropData drop in table.m_drops)
        {
            AddItem(drop.m_item, tier);
        }
    }

    private static void AddItem(GameObject item, int tier)
    {
        if (item != null && item.GetComponent<ItemDrop>() != null)
        {
            _ = Lower(item.name, tier);
        }
    }

    /// <summary>Records the tier if it is earlier than one already known. True if it changed.</summary>
    private static bool Lower(string prefab, int tier)
    {
        if (Tiers.TryGetValue(prefab, out int known) && known <= tier)
        {
            return false;
        }
        Tiers[prefab] = tier;
        return true;
    }

    /// <summary>For the sort info command.</summary>
    internal static void Rebuild()
    {
        _builtFor = null;
        Build();
    }
}
