# Changelog

## Unreleased

- **Search a chest group.** Type in the new search bar at the top right of the chest panel: the grid shows every item whose name matches from the open chest and every chest grouped with it, as if they were one chest, in sort order. Click an item to take that stack into your inventory from the chest it is really in (a chest another player has open says it's in use). While searching, the buttons work on the whole group: **Take all** takes every stack the search shows; **Place stacks** tops up stacks anywhere in the group with the carried items that match; **Deposit all**, and dragging an item onto the results, first top up matching stacks anywhere in the group and put the rest in the open chest (so 25 carrots in another chest are filled up before any go in the open one). While you type, your keys don't close the inventory or use hotbar items. Closing the chest, or opening another, empties the search.
- The chest panel has a new layout: the chest's name at the top left, and all five buttons in one row under it, grouped by what they do: **Take all** and **Restock** (into your inventory), **Deposit all** and **Place stacks** (into the chest), then **Sort**. Restock now has a button too. Button labels shrink to fit on narrow chests.
- Press **Y** at a chest (open, or just looking at it) to **restock**: every partial stack you carry, the hotbar included, is topped up from that chest and every chest grouped with it. Chests another player has open are skipped, and a message says how many items came in. The key can be changed with the new `Restock` keybind, and the chest's hover text and key hints show it.
- **Sorting is smarter**, especially for food and Deep North items:
  - Crops picked from plants, wild or grown (berries, mushrooms, carrots, turnips, barley…), are ordered by tier, from the earliest biome to the latest, whether you can eat them or not.
  - Cooked food is ordered by the stat it raises most, in four sections (health, balanced health and stamina, stamina, eitr), lowest first.
  - Armour is grouped by set, then helmet, chest and legs, also for sets the game doesn't name (such as the mage sets). Weapons are grouped by class, one-handed before two-handed.
  - Moulds and casts get their own group, ordered like the gear they make: armour sets first, then weapons (staffs included), then shields.
  - Upgrade kits get their own group: armour kits by tier, then weapon kits by tier.
  - Meads (anything a fermenter makes) are grouped by what they do: health, stamina, eitr, each resistance (fire, frost, poison…), then the rest. Within each group they go by tier, so minor comes before medium, major and lingering. Mead bases come first, in the same order. Other consumables (boss hearts, toadstools, rotten meat…) have their own group after the meads.
  - Metal bars and metal parts (nails, chains, springs) are separate groups, so all bars sit together and all nails sit together.
  - Hides get their own group, by tier. Jute and linen thread moved out of the metals.
  - Items the mod doesn't list (Deep North items and other mods' items) are placed by tier, worked out from the game's own data: the earliest biome a creature, tree, rock or plant gives it in, carried through recipes, smelting and cooking. A crafted item is never earlier than the station that makes it (so anything made at a Black Forge is Mistlands or later). Before, they all went to the end of their group by name.
  - New console command `betterchests_sortinfo` writes every item's place in the sort order to the BepInEx log.
- Fixed **Place stacks** (the button, its key and holding use on a chest) moving items from your hotbar into the chest, such as a mead in the hotbar when the chest holds meads, even though marking a hotbar item says hotbar items are never deposited. Place stacks now keeps the same items as Deposit all: your hotbar, anything equipped, and marked items.
- The chest keys (**R** take all, **T** place stacks, **F** deposit all, **G** sort) also work while looking at a chest, without opening it, and the chest's hover text shows them. They work on the same chests as opening them: not other players' private chests, chests in someone else's ward, or a chest another player has open. While they act on a chest, the key doesn't also do what the game binds it to (**F** is the Forsaken power, **R** hides your weapons) or what other mods bound to it through the game's controls. They can be turned off in the new `Chest Keys` config section, which also hides the hover text hints.
- Moved the Harmony patches out of the feature classes into a `Patches` folder, one file per patched game method, like the other mods. No behaviour change.

## 0.5.0

- Items in an open chest that another chest in its group also holds get a blue frame, so you can spot items worth gathering into one chest.
- Hovering over an item in the open chest makes the other chests in its group that hold it glow, and its tooltip says how many of them there are and how much they hold.
- Hovering over an item in your inventory does the same for every chest in the groups within auto deposit range of you, whether or not a chest is open.
- Other players' private chests and chests in someone else's ward are never counted. Nothing is moved for you.
- Added the `Show Duplicates` setting to turn this off.
- Moved the source files into feature folders (`Core`, `Deposit`, `Duplicates`, `Sorting`, `UI`). No change in behaviour.
## 0.4.1

- The wardrobe is now shown as 10 columns by 5 rows instead of 5 by 10, so all of it fits without a scroll bar. The chest panel widens to fit it.

## 0.4.0

- Auto deposit now asks a chest's owner for the chest the same way opening a chest does, instead of taking it over. A chest another player has open is skipped, and two players can no longer change a chest's contents at the same time. This also works when the other player doesn't have the mod.
- Opening a chest just as an item is being stored in it still opens the chest.
- The auto deposit `Radius`, `Cluster Gap` and `Delay` are now synced from the server when it has the mod. `Enabled` and `Throw Angle` stay your own choice.
- Buttons, key hints and messages can now be translated (`Translations/English.json`).
- Added a CI check that the version matches in every file.
- Moved `package.sh` into `.github/scripts` and made it the same for every mod: the name, version and DLL now come from `manifest.json` and the `.csproj`.

## 0.3.2

- Items marked to keep (Alt + right-click) now also stay in your inventory when using Place stacks, not just Deposit all.

## 0.3.1

- Fixed auto deposit never storing dropped items: the hook that starts watching a dropped item never ran.
- When a dropped item is left on the ground, the reason is written to the BepInEx log.

## 0.3.0

- Dropped items now land on the ground and lie there for 3 seconds before being stored, so other players can pick them up first. Previously they went straight into the chest.
- The distance to a chest is now measured from where the item lands, not from the player.
- Only chests in the direction you throw an item are used (within 60 degrees of the way you face), so items dropped with your back to a chest stay on the ground.
- Chests close together count as one group: an item that lands near one of them can go into any of them, filling chests that already hold it first.
- Lowered the default auto deposit radius from 6m to 4m.
- Added the Cluster Gap, Throw Angle and Delay settings.

## 0.2.1

- Lowered the default auto deposit radius from 10m to 6m, and the maximum from 50m to 12m.

## 0.2.0

- Items in your inventory can be marked to stay behind when using Deposit all: hold Alt and right-click (Mouse 1) them. Marked items get a gold frame.
- The keep key can be changed in the config file.

## 0.1.1

- Declared the mod as client side to Jotunn, so servers and other players are never required to have it installed.

## 0.1.0

- Initial release.
- Dropped items are stored in nearby chests, preferring chests that already hold the item. They are shown flying into the chest.
- Added a Sort button to the chest panel, which merges stacks and groups related items together (wood, stone, ores, metals, seeds, crops, food, gear and so on).
- Added a Deposit all button to the chest panel, which moves everything except the hotbar and equipped items into the chest.
- The chest panel grows to fit the new buttons and every row of big chests.
- Added keys laid out like the chest buttons while a chest is open: take all (`R`), place stacks (`T`), deposit all (`F`) and sort (`G`).
- Added -/+ buttons to the split stack dialog, and the mouse wheel changes the amount too.
- The keys are shown in the inventory key hints and can be changed in the config file.
