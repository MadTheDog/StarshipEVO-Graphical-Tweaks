# Sal's Graphical Tweaks v 1.0.0

---

## Short introduction

Client-side graphics controls, glass fixes and rendering optimisations for Starship EVO. Adjust shadows and decal visibility, remove internal glass seams and reduce unnecessary rendering work.

---

## What it does

All settings are under **Options → Graphics**. Changes are saved between sessions.

| Feature | What changes | Performance |
|---|---|---|
| Shadow distance | Slider and numeric input from 1–1500 units, with reset buttons. | Longer distances can reduce FPS and nearby shadow detail. |
| Sun shadow resolution | Game default, 512, 1024, 2048 or 4096 px. | Lower resolutions reduce shadow-map cost. |
| Sharp shadow range | Game default or 15, 30, 60 and 100 units. Smaller ranges concentrate detail near the camera while distant shadows continue to the total shadow distance. | Uses the existing shadow maps. |
| Glass sunlight occlusion | Blocks direct solar glare where ship geometry casts a shadow, retaining environment reflections. Enabled by default. | Adds rendering work; cost varies with the view. |
| Seamless glass | Removes covered internal joins across eleven borderless glass definitions, including supported mirrored and partial tetra/hepta joins. Enabled by default. | Large window collections have not been benchmarked. |
| Decal visibility | Slider and numeric multiplier from 1–10×, with reset buttons. Decal artwork keeps its size. | Higher ranges can significantly reduce FPS in decal-heavy views. |
| Renderer optimisation | Skips culling dispatches for empty render passes. Enabled by default. | Savings depend on the scene. |
| Ambient occlusion optimisation | Removes duplicate AO passes and stale AO work after disabling AO. Enabled by default. | Reduces redundant work while preserving the selected AO quality. |
| Decal CPU optimisation — experimental | Reduces some offscreen decal calculations during camera turns. Disabled by default. | About **28–31% higher FPS** in one 10× decal camera-turning test; idle CPU cost increased. |

The percentage above comes from one test scene: approximately 30 FPS with the experimental option off and 39 FPS with it on. Other features have no reliable FPS percentage measurement. See [performance measurements](docs/PERFORMANCE.md).

---

## Installation requirements

- Starship EVO on **64-bit Windows**.
- **BepInEx 5, Windows x64**. Tested with [5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5).
- Direct3D 11. Glass sunlight occlusion also needs the game's HDR/Linear rendering and enabled sunlight shadows.

Tested with SEVO Steam build **26122752**, Unity **2019.4.30f1**.

---

## How to install

1. Close Starship EVO and back up any existing world you plan to use.
2. Open the game folder through **Steam → Starship EVO → Manage → Browse local files**.
3. Extract the BepInEx 5 Windows x64 package into that folder, beside `Starship EVO.exe`.
4. Launch the game once, then close it so BepInEx creates its configuration files.
5. Open `BepInEx/config/BepInEx.cfg` and set:

   ```ini
   [Chainloader]
   HideManagerGameObject = true
   ```

6. Download `Sals-Graphical-Tweaks-1.0.0.zip` from [Releases](https://github.com/MadTheDog/StarshipEVO-Graphical-Tweaks/releases/latest).
7. Extract its `BepInEx` folder into the game folder. The result should be:

   ```text
   Starship EVO/
   ├── Starship EVO.exe
   └── BepInEx/
       └── plugins/
           └── GraphicalTweaks/
               ├── GraphicalTweaks.dll
               └── graphicaltweaks-glass.bundle
   ```

8. Launch the game and open **Options → Graphics**. The mod's settings are at the bottom.

When updating, close the game and replace both files in `BepInEx/plugins/GraphicalTweaks/`. Keep a single copy of the mod installed. Existing settings remain in `BepInEx/config/local.sevo.graphicaltweaks.cfg`.

## How to uninstall

1. Close the game.
2. Delete `BepInEx/plugins/GraphicalTweaks/`.
3. Optionally delete `BepInEx/config/local.sevo.graphicaltweaks.cfg` to remove the saved settings.

---

## Notes

- Back up an existing world before trying the mod.
- Rendering and settings change locally. Saved blocks, physical models, colliders and character formats stay unchanged.
- **Multiplayer, large-grid/window stress tests, sustained remote building, display resizing and same-process world reloads remain untested.**
- Seam removal requires compatible borderless clear glass touching within the same grid. Shadow coverage and quality limit the glass glare correction.
- Decal range depends on pixel size, receiver depth and tile limits.
- Some native building/resource changes trigger a global glass-cache refresh. Large-world rebuild cost remains unmeasured.
- Tree/rock LOD smoothing is not included.
- Unofficial mod; unaffiliated with Moonfire Entertainment.
- [Building from source](docs/DEVELOPMENT.md).

---

## License

Copyright © 2026 Sal. Released under [MIT-0](LICENSE): free to use, modify, redistribute and sell. Credit to **Sal** is appreciated, but optional. [License details](https://opensource.org/license/mit-0).
