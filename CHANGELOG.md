# Changelog

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
