# Portia Grab Items

[Chinese documentation](README.zh-CN.md) · [Download the latest release](https://github.com/jeanmuss/portia-grab-items/releases/latest)

**Press Refresh to update the items required by your current quests — including their immediate crafting and assembly materials.**

A task-aware item browser and spawner for the Windows Steam edition of **My Time at Portia**. Open the panel with **F8**, select **Current tasks**, and click **Refresh** after accepting or progressing a quest.

## Refresh your current task requirements

1. Load your save and open the panel with **F8**.
2. Select **Current tasks** and press **Refresh**.
3. Browse the supported active item-collection objectives and commerce orders.
4. Obtain the required items or their immediate crafting/assembly materials from the same panel.

**[T]** marks items directly required by a task. **[M]** marks immediate ingredients. Materials are traced **exactly one level back**; ingredients of ingredients are not expanded. Alternative recipes contribute their combined ingredients, and each item appears once.

Refresh reads the current game state when clicked. This is an on-demand list, not continuous background tracking. It lists supported item-collection objectives, not every kind of quest objective, and does not complete quests automatically. Items without a reliable catalog name or that cannot be picked up are omitted.

## Other features

- Item icons with English and Chinese names.
- Search by English name, Chinese name, or item ID.
- Categories based on the game's items, plus a museum category with donation status.
- Left click to add **1** item; right click to add **10**, subject to bag capacity checks.
- The panel unlocks the cursor and suppresses gameplay input while open.

## Installation

**Version: 1.0.0.** Requires the Windows x64 Steam game and **BepInEx 5**. The development installation uses BepInEx **5.4.22**, Unity **2017.4**, and Mono CLR **2.0**.

1. Close the game.
2. Install the Windows x64 BepInEx 5 loader from its [official releases](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.22). The loader is a separate prerequisite.
3. Download **PortiaGrabItems-1.0.0-BepInEx.zip** from [Releases](https://github.com/jeanmuss/portia-grab-items/releases/latest). GitHub's automatic source archives are not installable plugin packages.
4. Extract the ZIP into the game directory. These three files must be directly inside `BepInEx/plugins/`:
   - `PortiaGrabItem.dll`
   - `PortiaGrabItem.names.tsv`
   - `PortiaGrabItem.recipes.tsv`
5. Launch the game normally through **Steam**, load a save, and press **F8**. No separate API launch is required.

Do not move the three files into a nested plugin folder. This is a **BepInEx plugin**, not a Unity Mod Manager package; compatibility with UMM and other mods has not been established. The plugin uses C# and Harmony runtime patches rather than replacing game JavaScript or game assemblies.

## Updating and uninstalling

Close the game and back up the existing three plugin files before replacing them. To uninstall, remove those three files. Items already added and saved remain in the save. Quest items can affect progression; keep a save backup before spawning them.

## Building

See [BUILDING.md](BUILDING.md). Build against locally supplied game and loader reference assemblies targeting CLR 2.0. Game assemblies and the loader are not redistributed in this repository.

## Release status and credits

The first public stable version, **1.0.0**, follows the private **0.1.4** development build. This release changes version metadata and packaging, not item-spawning logic. Local compilation and package checks passed. The user has tested the existing gameplay functionality; the newly packaged release has not received a separate in-game launch test or broad mod-compatibility testing.

Names and recipe relationships in the data tables come from the game. Icons are read from the game at runtime. Game content belongs to its respective owners. This is an unofficial mod.

See [CHANGELOG.md](CHANGELOG.md) for version history.
