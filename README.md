[![CI](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-betterchests/ci.yml?branch=main&style=flat-square&label=CI&logo=github)](https://github.com/javidahmed64592/valheim-betterchests/actions/workflows/ci.yml)
[![Package](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-betterchests/package.yml?branch=main&style=flat-square&label=Package&logo=github)](https://github.com/javidahmed64592/valheim-betterchests/actions/workflows/package.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# BetterChests

Enhances chests with additional features like sorting and auto-depositing!

![Chest Interface](https://github.com/javidahmed64592/Valheim-BetterChests/raw/main/images/game_screenshot_1.png)
![Split Stack Window](https://github.com/javidahmed64592/Valheim-BetterChests/raw/main/images/game_screenshot_2.png)

| Key                 | Action                                                                                          |
| ------------------- | ----------------------------------------------------------------------------------------------- |
| `R`                 | Take all: move everything in the open chest into your inventory.                                |
| `T`                 | Place stacks: move items onto matching stacks in the open chest.                                |
| `F`                 | Deposit all: move everything except your hotbar, equipped and marked items into the open chest. |
| `G`                 | Sort the open chest.                                                                            |
| `Alt` + Right-click | Mark or unmark an item in your inventory to keep it when depositing all.                        |
| Mouse wheel         | Change the amount by one when splitting a stack (Shift+click).                                  |

The keys match the layout of the chest panel's buttons.
The keys are listed in the inventory's key hints.
All of them can be changed, see [Configuration](#configuration).

## Auto Deposit

Dropping an item near chests stores it in them.
The item lands on the ground as normal and lies there for a few seconds, so other players can still pick it up.
Then, if it landed within 4m of a chest you threw it towards, it is pulled into that chest.
Chests you had your back to are left alone, so you can still drop items on the ground next to a chest.

Chests standing close together count as one group, such as a row or a stack of chests.
An item that lands near any of them can go into any chest in the group, even one at the far end.
Chests in the group that already hold the item are filled first, nearest first, then any other chest with room.
Whatever doesn't fit stays on the ground.

Only chests you could open yourself are used: not private chests of other players, chests inside someone else's ward, or chests another player has open.

## Deposit All

The open chest gets a **Deposit all** button under **Take all**.
It moves everything in your inventory into the chest, except your hotbar (the top row of your inventory) and anything you have equipped, so you can empty your bags after an adventure without losing your gear.
To keep other items too, such as your food, hold **Alt** and right-click them in your inventory.

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
You can change the keys and turn auto deposit off.
For auto deposit you can also change:

- **Radius**: how close a dropped item must land to a chest (4m by default, up to 12m).
- **Cluster Gap**: how far apart chests can be and still count as one group (1m by default, edge to edge).
- **Throw Angle**: how far to either side of the way you face a chest can be (60 degrees by default; 180 uses chests in every direction).
- **Delay**: how long a dropped item lies on the ground before it is stored (3 seconds by default).

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/javidahmed64592/Valheim-BetterChests/blob/main/LICENSE) file for details.
