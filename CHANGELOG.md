# Changelog

## 1.0.0

- First stable release.
- **Key combos:** every keybind is now a key plus an optional `Modifier` (`None`, `Shift`, `Ctrl` or `Alt`), both picked from a list in the config, so combos like Shift + B can be bound. Sort Group Modifier and Keep Item stay a single held key. Settings from older versions carry over.
- With `debugmode` on, the mod's key combos no longer also trigger the game's debug keys, such as Ctrl+Z toggling flying.
- **Safer after game updates:** if an update breaks part of the mod (such as search, nearby chests in tooltips, auto deposit or the split stack buttons), only that part turns off and the log says which, in one line; chest groups, the chest panel and its keys still work. If chest groups or the chest panel breaks, the whole mod turns off cleanly instead of throwing errors.
- **Debug setting:** the new `DisabledFeatures` setting under `[Debug]` turns parts of the mod off by name, for testing.
- Updated Jotunn to 2.30.3.

## 0.8.1

- **Sort group now places every stack.** Items with a chest's worth or more, such as stone or wood, get whole chests instead of the overflow staying scattered across the group. Partial stacks are merged across the group first; the rest get a chest each where there are enough, or share in sort order, and an item is only split between two chests when there's no other way to fit everything.

## 0.8.0

- **Sort group:** hold `Shift` with Sort (the button, or `H` on an open chest or one you're looking at) to sort a chest's whole group. Each item is gathered into a chest of its own as far as the chests allow, overflow stays where it is, and every chest is sorted. The modifier is the new `Sort Group Modifier` key setting.

## 0.7.1

- The mod's name is now shown as "Better Chests", with a space like the other mods. Its settings are unchanged.

## 0.7.0

- **Fixed** "Someone else is using this" appearing for chests nobody had open, stopping the chest keys, Restock, auto deposit and search from using them. It happened whenever another player owned the chest: their "yes" arrived a moment before the chest itself, and was taken as a "no". Requests now wait for the chest to arrive, as opening a chest does, and ask again if the chest changed hands before its owner answered.
- **New button order and default keys:** **Restock** (`R`), **Take all** (`T`), **Deposit all** (`F`), **Place stacks** (`G`) and **Sort** (`H`). The key hints and a chest's hover text follow the same order. Keys already saved in your config file are kept, so delete the `Keybinds` entries to get the new defaults.

## 0.6.0

- **Chest panel redesign.** The chest's name and a new search bar sit at the top, with all five buttons in one row underneath: **Take all**, **Restock**, **Deposit all**, **Place stacks** and **Sort**.
- **Search the chest group.** Typing in the search bar shows matching items from every chest in the group as one grid. Click to take a stack, Shift + click to split it, and right-click to eat, drink or equip it, straight from the chest it's in. A red cross clears the search.
- **Buttons act on the whole group while searching.** Take all takes every result. Place stacks and Deposit all put items into the chests that already hold them before the open chest, as do Ctrl + click and dragging onto the results. Sort gathers each result into the chest holding the most of it.
- **Restock** (**Y** or the button) tops up every partial stack you carry from the chest's group.
- **Keys work while looking at a chest**, without opening it, with a message and a sound saying what moved. While they act, they don't also trigger the game's or other mods' bindings for the same key (such as the Forsaken power on **F**). They can be turned off in the new `Chest Keys` config section.
- **Smarter sorting**, worked out from the game's own data so Deep North and other mods' items sort sensibly:
  - crops by tier;
  - cooked food by the stat it raises most;
  - armour by set, then slot;
  - weapons by class;
  - meads by effect;
  - moulds next to the gear they make;
  - upgrade kits by kind and tier;
  - separate groups for bars, metal parts and hides.

  The new `betterchests_sortinfo` console command logs where every item sorts.
- **Fixed** Place stacks moving items out of your hotbar. It now keeps the same items as Deposit all.
- The README has been rewritten around chest groups, and the Harmony patches now live in a `Patches` folder.

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
