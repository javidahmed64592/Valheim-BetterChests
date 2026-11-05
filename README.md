[![CI](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-betterchests/ci.yml?branch=main&style=flat-square&label=CI&logo=github)](https://github.com/javidahmed64592/valheim-betterchests/actions/workflows/ci.yml)
[![Package](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-betterchests/package.yml?branch=main&style=flat-square&label=Package&logo=github)](https://github.com/javidahmed64592/valheim-betterchests/actions/workflows/package.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# BetterChests

Enhances chests with additional features like sorting and auto-depositing!

![Chest Interface](https://github.com/javidahmed64592/Valheim-BetterChests/raw/main/images/game_screenshot_1.png)
![Split Stack Window](https://github.com/javidahmed64592/Valheim-BetterChests/raw/main/images/game_screenshot_2.png)

| Key             | Action                                                                                          |
| --------------- | ----------------------------------------------------------------------------------------------- |
| `R`             | Take all: move everything in the open chest into your inventory.                                |
| `T`             | Place stacks: move items onto matching stacks in the open chest.                                |
| `F`             | Deposit all: move everything except your hotbar, equipped and marked items into the open chest. |
| `G`             | Sort the open chest.                                                                            |
| `Alt` + Mouse 1 | Mark or unmark an item in your inventory to keep it when depositing all.                        |
| Mouse wheel     | Change the amount by one when splitting a stack (Shift+click).                                  |

The keys match the layout of the chest panel's buttons.
The keys are listed in the inventory's key hints.
All of them can be changed, see [Configuration](#configuration).

## Auto Deposit

Dropping an item near chests stores it in them instead of on the ground.
Chests that already hold the item are filled first, nearest first, then any other chest with room.

Only chests you could open yourself are used: not private chests of other players, chests inside someone else's ward, or chests another player has open.

## Deposit All

The open chest gets a **Deposit all** button under **Take all**.
It moves everything in your inventory into the chest, except your hotbar (the top row of your inventory) and anything you have equipped, so you can empty your bags after an adventure without losing your gear.
To keep other items too, such as your food, hold **Alt** and right-click (Mouse 1) them in your inventory.

## Sort

The open chest gets a **Sort** button under **Place stacks**.
The chest panel is made taller to fit the new buttons, and to show every row of big chests without scrolling, as far as the screen allows.
Sorting merges partial stacks, then groups related items together, in this order: wood, stone and minerals, ores, metals, creature parts, valuables, seeds, crops, raw meat and fish, food, meads, then ammo, weapons, shields, tools, armour, utility items and trophies.
Within each group, items follow the game's progression, so copper comes before tin, bronze and iron.
Items from other mods are placed by their name and type.

## Split Stack

The split stack dialog gets **-** and **+** buttons either side of the slider to change the amount by one, and so does the mouse wheel.
The number keys, arrow keys and Enter still work as before.

## Configuration

Settings are in the BepInEx config file `javidahmed64592.betterchests.cfg`.
You can change the keys, turn auto deposit off, and change how far away chests are used for dropped items (10m by default).

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/javidahmed64592/Valheim-BetterChests/blob/main/LICENSE) file for details.
