# Age of Sakura - Prototype 0.01

A small mobile city-builder set in 15th-century Japan, with real-time production timers that also finish while the app is closed. Spec: [PROTOTYPE.md](PROTOTYPE.md).

## The playable loop (0.02)

```
Coins -> Woodcutter -> Wood -> House, Garden, Shrine, upgrades
Coins -> Rice Paddy  -> Rice -> House (eats rice, pays taxes) -> Coins
```

- **Supply chain:** a production can list `inputs`. The goods leave the store when the job *starts* (atomically; a failed start costs nothing). Rice Paddies produce Rice, Houses turn Rice into Coins. Rice has its own HUD counter and the goods visibly fly from the counter to the house.
- **House upgrades (Lv.1 -> 3):** a house has *wishes* that depend on its needs and surroundings. Level 2 wants: 2 tax runs collected (the supply chain works) and Beauty 2. Level 3 wants: 4 tax runs, Beauty 5, Faith 1 and Noise at most 2. Cost is Wood + Coins. Higher levels pay much more Coins per Rice.
- **Environment variables** (Beauty, Faith, Noise) are computed live from the map: Gardens add Beauty, Shrines Faith (and a little Beauty), Woodcutters add Noise, cherry trees, bamboo and water add Beauty (`environment.terrain`). Each source counts in full within its radius (centre to centre, in cells). So *where* you build and *what you move* changes what a house can become.
- **Guidance:** the hint bubble above the Build button names the next useful step (`GoalAdvisor`): build the chain, harvest rice, collect taxes, then whatever the next upgrade still lacks. A house that can be upgraded shows a green arrow bubble.
- **Upgrade UI:** tap a house, tap *Upgrade to Lv.N*, see every wish with its current value, the cost, and the missing wish's hint. Upgraded houses grow and get lanterns (Lv.2) and banners (Lv.3).
- **Pacing:** a scripted bot using only public rules reaches Lv.2 after about 12 minutes and Lv.3 after about 28 minutes (`Bot_CanPlayFromFreshSaveToLevelThree_WithoutDebugGrants`).

## Verification status (please read)

| What | Status |
|---|---|
| Game rules (wallet, grid, placement, production incl. inputs, offline timers, Diamond skip, save/load, recovery, environment values, house upgrades, goal advisor, a full bot playthrough) and gesture logic | **97 automated tests pass**, both with `dotnet test` and inside Unity 6000.0.84f1 (EditMode Test Runner). |
| Supply chain / upgrade UI, new buildings and icons, build-menu scrolling | **Rendered and screenshotted** in a Windows preview player (`-uishowcase ... -uistates game`, EN + DE, 1560x720). **Not tested:** touch input on a device (build-menu scrolling in particular), other aspect ratios, the Android build (`Builds/AgeOfSakura-dev.apk` is from before 0.02 and must be rebuilt). |
| Compiles in Unity 6 (runtime + editor scripts), project setup, definition validation | **Verified** via Unity batch mode. |
| 3D-Toon world (shader, outlines, shadows, buildings at 3 levels, trees, bridge, night variant, construction animation) | **Rendered and reviewed in screenshots** of the Windows preview player (Direct3D, 1560x720, `-uistates style`). **Not verified:** Android/OpenGL ES/Vulkan shader compilation and look, performance on a phone (the scene holds about 3,900 mesh renderers and 540,000 triangles before batching and culling, measured with the showcase), other aspect ratios. |
| Android development APK | **Builds successfully** (`Builds/AgeOfSakura-dev.apk`, ~39 MB). |
| Visuals, touch feel, layout on real screens, performance, runtime behaviour on a device | **Not verified.** Nobody has run the game yet (no editor Play session, no device). Expect small fixes. |

Do the manual acceptance run from PROTOTYPE.md section 83 before treating 0.01 as done. The checklist in section 78 is intentionally **not** ticked.

## Requirements

- Unity **6000.0 LTS** (pinned to `6000.0.58f2` in `ProjectSettings/ProjectVersion.txt`; any 6000.0.x should work - let the Hub upgrade/pick it).
- Android Build Support (with OpenJDK + SDK/NDK) for device builds; iOS Build Support optional.
- Packages (in `Packages/manifest.json`): Input System 1.11.2, Universal RP 17.0.3, uGUI 2.0.0, Test Framework 1.4.5. No other dependencies, no imported art or audio.
- .NET SDK 8 (only for running the core tests outside Unity).

## Run it

1. Open the folder in Unity. On first load `Age of Sakura > Run Project Setup` runs automatically (`Assets/Scripts/Editor/ProjectSetup.cs`): creates a mobile-tuned URP asset in `Assets/Settings`, sets landscape orientation, creates small template materials in `Assets/Resources/ShaderVariants` so builds keep exactly the shader variants the game uses (do **not** add the shaders to *Always Included Shaders*: that makes builds compile hundreds of thousands of variants), enables the Input System backend and creates `Assets/Scenes/Game.unity` (added to the build).
2. If the console says *"Restart the Unity Editor"* (Input System backend), restart once.
3. Open `Assets/Scenes/Game.unity` and press **Play**. The game also starts in any other scene (`GameBootstrap`).

Editor controls (touch is the real target): left-drag = pan, mouse wheel = zoom, click = tap, press-and-hold on a building = move. Use the Device Simulator for touch input and safe areas. `Esc` (Android back) closes panels; it is never required.

## Project structure

```
Assets/
  Resources/Definitions/game_definitions.json   all balance + map + camera/input tuning
  Scripts/
    Core/      engine-independent rules (no UnityEngine): Economy, Grid, Buildings, Production, Save, Data, Json, Time, Services
    Game/      Unity layer: Bootstrap (GameRoot), World (terrain, models, props, indicators), Camera, Input,
               Interaction (placement, selection), UI, NPC, Debug, Platform
    Editor/    project setup, build + validation menu items
  Tests/EditMode/   NUnit tests for Core (fake clock, no waiting)
Tools/CoreTests/    csproj that runs Core + tests with plain `dotnet test`
PROTOTYPE.md        the spec
```

Architecture in one paragraph: `GameSession` (Core) owns `Wallet`, `GridMap`, `BuildingService`, `ProductionService`, `EconomyService` and saving. It has no Unity dependency and reacts to an injected `ITimeProvider`. `GameRoot` (Unity) builds the session and all views, and only feeds it input and a heartbeat. UI and views react to events (`CurrencyChanged`, `BuildingPlaced`, `ProductionCompleted`, ...); nothing polls balances per frame. Visuals are replaceable: gameplay refers to buildings by definition id, footprints come from data, and `BuildingView` separates `GameplayRoot` from `VisualRoot/CurrentModel`. A replacement model only needs to implement `IBuildingModelProvider` (and carry a `BuildingModel` component).

## Balance and data

Everything tunable is in `Assets/Resources/Definitions/game_definitions.json`:

- `economy`: starting Coins/Wood/Diamonds and the Diamond skip formula `max(minSkipCost, ceil(remainingSeconds / secondsPerDiamond))` (default 60 s per Diamond, so 30 s = 1, 5 min = 5).
- `productions`: recipes (`durationSeconds`, optional `inputs`, `rewards`).
- `buildings`: cost, footprint, allowed productions, movable/rotatable/buildable, `visualId`, `emits` (environment sources: `variable` beauty/noise/faith, `value`, `radius`), `levels` (optional upgrade path, listed 1..N: `upgradeCost`, `needs` with `kind` served/beauty/faith/noise and `min`/`max`, and the `productions` offered at that level). `upgradeTo` is unused.
- `environment.terrain`: terrain types (Cherry, Bamboo, Water, ...) that act as environment sources.
- `map`: 20x20 ASCII terrain (`rows[z][x]`, legend in `MapDefinition.ParseCell`), unlocked 12x12 rectangle, Town Hall position.
- `camera` and `input`: yaw/pitch, zoom limits, inertia, drag threshold (dp), long-press time, ghost grab radius.

Invalid data fails loudly at startup (`DefinitionValidator`: duplicate ids, missing models, bad footprints/costs/durations/rewards, broken map). In the editor, `Age of Sakura > Validate Definitions` and re-import of the JSON run the same checks.

## Art style: 3D-Toon

The world follows the decided style in [Docs/grafikstil-3d-toon.md](Docs/grafikstil-3d-toon.md) (version 1.0.0): soft rounded low-poly shapes, 3-step toon shading, a constant dark-brown outline, warm sun and strong slightly pastel greens, fixed isometric camera (30 degrees elevation, 45 degrees azimuth, never rotated). The style document's YAML block is the single source of truth and is mirrored one-to-one in `ToonStyle.cs`; `ToonStyleTests` fail if code and document drift apart.

How it is implemented in Unity (the style's own pipeline is "bake to sprites"; that part is still *proposed* there and the engine question is open, so the game renders the same look live from the fixed camera, which is also what a later bake would render):

| Piece | Where |
|---|---|
| Toon shader: hemisphere light + sun quantised to 3 bands, PCF-soft sun shadow, inverted-hull outline in clip space (constant pixel width like three.js `OutlineEffect`), baked-vertex-colour mode for foliage, cutout leaf cards | `Assets/Resources/Shaders/AgeOfSakuraToon.shader` (Resources, so builds always contain it) |
| Style constants (palette, light day/night, outline, ramp, build-in timings) | `Game/World/ToonStyle.cs` |
| Rounded boxes/cylinders, smooth cones, icosphere foliage blobs with sun-baked vertex colours, leaf cards; every mesh carries a smoothed outline direction in uv3 | `Game/World/ToonMeshes.cs`, roofs in `ProceduralMeshes.cs` |
| Painted procedural textures: bark, birch bark, plaster, thatch, stone, earth, door, roof tiles (uv-mapped kawara for the pagoda roofs), planks, leaf and blossom cards | `Game/World/PaintedTextures.cs` |
| Buildings with three looks each (visual level 1..3): Town Hall as a castle-like tower with 2/3/4 storeys (sagging tile roofs with upswept corners, karahafu gables, golden shachihoko, red pillars, a stair on each visible face; about 29k/33k/37k triangles, guarded by a budget test; quick review: `-uistates hall`), thatched half-timber House, log Woodcutter, Rice Paddy, Garden, Shrine | `Game/World/BuildingModels.cs` |
| Trees (spruce with lobed drooping tiers, oak, cherry, birch, old; tapered trunks, roots and limbs, layered crowns of many clumps with baked mottling, 150-190 leaf/blossom cards per near crown, everything merged into ~15 renderers per tree; near about 1.6-5k triangles, far 1-2k; budgets guarded by `Vegetation_StaysWithinTriangleAndRendererBudgets`), bamboo, rocks, shrubs, flowers, mushrooms, base decals, well, signboard, barrel, crate, stone wall, banner, arched bridge | `Game/World/PropFactory.cs` |
| Villagers (6 looks: farmer with kasa and hoe, woman with basket, elder with staff, worker, woman, monk) with human proportions, jointed limbs, kimono/obi/trousers/sandals, faces and hair styles; dog and hens with jointed legs; walk and idle poses; merged per bone (about 4.3-5.5k triangles per villager, ~35 renderers) | `Game/NPC/Actors.cs` |
| Art review without the UI: `-uistates gallery` photographs all villagers, animals, trees and shrubs from the fixed direction with a second camera (`Game/Debug/GalleryShowcase.cs`) | `Builds/UiShots/<dir>/gallery_*.png` |
| Static model parts merged per material (a few draw calls per building instead of a few hundred) | `Game/World/ModelBatcher.cs` |
| Construction animation: Ground, Body, Roof, Trees, Props drop in with a bounce over 4.4 s (new and upgraded buildings) | `Game/World/BuildInAnimation.cs` |

Review it without a device: `Unity -batchmode -nographics -quit -buildTarget StandaloneWindows64 -executeMethod AgeOfSakura.EditorTools.PreviewBuild.BuildWindowsPreview`, then `Builds/Preview/AgeOfSakuraPreview.exe -uishowcase <dir> -uistates style -uires 1560x720` (overview, all three looks of each building, forest, bridge, night, construction sequence).

**No clipping between trees and houses (guarded by `BuildingClippingTests`):** buildings stay inside their footprint (roofs, props, everything), and every tree, bamboo grove or boulder next to buildable land is squeezed to a reach of half a cell (`PropFactory.MaxRadius`, applied by `WorldBuilder` for cells touching buildable ground), so a crown can never reach into a neighbouring cell however the player builds. Trees that belong to a model (shrubs, the garden's sakura) are checked against the model's walls, roofs and props with physics overlap queries. The Town Hall has no tree inside its footprint (a 3x3 building cannot hold one without growing through its roofs): it has blossom bushes on its terrace corners instead, and the garden holds a compact sakura.

**Values chosen here because the style document does not fix them** (change them in `ToonStyle.cs`): brightness of the three toon bands (0.42 / 0.72 / 1.0); all light intensities are divided by pi (three.js divides inside its Lambert BRDF, Unity does not); the outline colour is read as display colour; the four foliage ramp stops use the two palette greens or pinks (derived colours for birch, spruce and old trees); shadow distance 105 and a 2048 shadow map (the camera sits 80 units away and shadow distance is measured from the camera, the old 40-50 meant no shadow was ever drawn); night = different light values, bluish foliage tint and 1.9x window glow.

**Deviation that needs Fred's approval:** the YAML asks for icosahedron detail 9 (2000 triangles per foliage clump, a desktop setting). The game uses subdivision level 2 (320 triangles) near and 1 (80) far, always with smooth normals (`ToonStyle.FoliageSubdivisionsNear/Far`).

**Not covered by the style yet** (open questions in the document): UI style (the UI keeps its parchment look), figures and animals (they now use the toon shader and rounded shapes but were not redesigned), the sprite-bake pipeline, atlas format and budgets.

## Save data

- File: `Application.persistentDataPath/save.json` (pretty JSON, UTC ISO-8601 timestamps, `version: 1`, stable string ids).
- Saved after every state change (wallet, placement, move, production start/complete/collect), once per frame at most, plus on pause, focus loss and quit.
- Production stores `productionStartUtc` / `productionEndUtc`; on load/resume `remaining = end - now`. A finished timer becomes *ReadyToCollect*; the reward is only granted by an explicit collect, exactly once.
- Versioning: `SaveMigrator` applies ordered migrations (none needed yet).
- Recovery: missing save -> new game. Unreadable save -> kept as `save.json.corrupt-<timestamp>`, new game started, toast shown. Save from a *newer* version -> left untouched, temporary in-memory game. Unknown building definition in a save -> logged, that building skipped. Unknown production id -> building reset to Idle. Half-restored corrupt save never leaks partial state.
- Reset: debug menu > *Reset save*, or delete `save.json` (its folder is `Application.persistentDataPath`; on Windows in the Editor: `%USERPROFILE%/AppData/LocalLow/AgeOfSakura/Age of Sakura`).
- Device time is not secure. `SystemTimeProvider` documents that server time is needed before monetization.

## Debug menu

Gear button (top right) in the Editor and Development Builds only (`DebugCommands.Available`; define `AGE_OF_SAKURA_DEBUG_MENU` to force it in a release build): +1000 Coins/Wood, +100 Diamonds, finish all productions, toggle grid, toggle locked-area veil, reset save. The Scene view shows the logical grid while playing (`GameRoot.drawGridGizmos`).

## UI

- **Layout-driven:** every button, card, tile, chip and bar is built from layout groups (`UiKit.Row/Column/Button`), not hand-placed offsets, so icons and text stay centred and inside their container. Labels shrink to fit (down to ~55%) instead of overflowing. Shared sizes live in `UiTheme` (minimum touch target 120 units, button height 150, gaps, font sizes).
- **Buttons:** one `UiKit.Button` (Primary / Secondary / Premium / Danger; normal, pressed, unaffordable, disabled), drop shadow, press-down scale, one global click hook for audio.
- **Localisation:** all player-facing text is in `Loc.cs` (English + German, follows the device language). Building/production names use keys like `building.house.name` and fall back to the definition text. A missing key is logged as an error and shown as the key. Debug menu > *Language EN/DE* flips the language at runtime to check translations and text fitting.
- **Screens:** HUD chips, contextual bottom sheet (build menu, building info, production choice, running timer, collect), placement bar, toast, hint, debug menu, collect effects. Safe-area aware; sheet and bar shrink on narrow (4:3) screens.
- Uses uGUI with the built-in dynamic font. Switching to TextMeshPro (SDF text, outlines) is the next quality step; it needs the TMP essential resources imported and glyph coverage checked for the target languages.

## Tests

```bash
cd Tools/CoreTests
dotnet test
```

Covers wallet (incl. atomic multi-cost spend), grid (placement, overlap, locked/blocked/out-of-bounds, move release/reclaim, rotation), pathfinding, building purchase/moving, production (start/end, incomplete, elapsed, collect once, Diamond skip prices, clock moved backwards), save (round trip, active/completed-offline production, moved building, corrupt/unknown/too-new saves, migrations), definitions validation (incl. levels, effects, inputs), environment values, upgrades (refusals cost nothing), the supply chain, the goal advisor and a full fresh-save economy loop. In Unity: *Window > General > Test Runner > EditMode*.

In Unity the EditMode tests also run `ToonStyleTests` (mesh closure/winding/outline layout, foliage ramp, style constants against the style document, camera data) and `BuildingClippingTests` (models stay inside their footprint, trees never clip building parts, scenery stays inside its cell, models are merged); both files are compiled out of the plain `dotnet test` project.

Not covered by automated tests: gestures, rendering (only reviewed through showcase screenshots), UI layout, NPC behaviour.

## Build Android

Menu: `Age of Sakura > Build Android Development APK` (output `Builds/AgeOfSakura-dev.apk`) or `... Release AAB`. CLI:

```bash
Unity -batchmode -quit -projectPath . -executeMethod AgeOfSakura.EditorTools.BuildScripts.BuildAndroidDevelopment
```

For Google Play use IL2CPP + ARM64 (*Player Settings > Other Settings*) and configure your own keystore. Package id is `com.ageofsakura.prototype` (change it). Landscape only.

## Decisions where the spec left room

- **Definitions as JSON, not ScriptableObjects** ("or equivalent"): editable and testable without the editor, one file for balance + map + tuning. Runtime state is never stored in definitions.
- **All art is procedural** (generated meshes/textures/icons, particles), built behind replaceable interfaces (`IBuildingModelProvider`, `IconSet`, `ActorFactory`). The world uses one custom toon shader (see *Art style*); water stays a URP Unlit material with two scrolling ripple layers. The earlier "Cozy" test sprites for the three buildings were replaced by the 3D-Toon models; the old provider and atlas are kept in `Archive/cozy-sprites/` (outside `Assets`, not built).
- Town Hall is 3x3. Pinch/pan/zoom, long-press move and the ghost drag all go through one `GestureRecognizer`; a drag that starts near the ghost moves the ghost, otherwise it pans.
- uGUI with the built-in legacy font. Reference resolution 1920x1080 matched on height; safe area via `SafeAreaFitter`.
- Audio, IAP and analytics are interfaces with no-op implementations (`IAudioService`, `IPurchaseService`, `IAnalyticsService`).
- The hint bubble follows `GoalAdvisor` (chain order Woodcutter, Rice Paddy, House is hard-coded there; everything after is derived from the house's next upgrade).
- Levels are content of the *definition*, not new definitions: a level-2 house is the same building id with `Level = 2`, so saves, positions and views survive upgrades. The view is rebuilt on upgrade (the model provider gets a higher visual level).

## Known limitations

- Only partly verified on a device (see top): build menu, building/production/collect sheets and HUD were checked on a phone in English. Not yet checked visually: placement bar, toast, debug menu, German texts, 4:3 / 16:9 aspect ratios. No real textures, models, audio or animation clips; NPC animation is code-driven.
- Building/rotation art is not varied per rotation. No construction timers, roads, land purchase (by design). Only houses are upgradable so far; every building model has three looks (visual level 1..3) and houses additionally get lanterns (Lv.2) and banners (Lv.3) from `BuildingView`. The Woodcutter and Town Hall looks for levels 2 and 3 are only reachable through the showcase tool (their definitions have no upgrade path yet).
- Terrain is flat; hills are backdrop only. Camera bounds are rectangular in world space.
- Production timers use the device clock.
- Performance untested on devices. Mitigations built in: far scenery (forest ring, hills, landmarks) is built without sway and merged with `StaticBatchingUtility`, ambient motion runs from one central Update, shared materials, URP tuned conservatively (no HDR/MSAA, one shadow cascade). If frame rate is low, first check draw calls in the Frame Debugger.

## iOS notes

No Android-specific code in gameplay. Saves use `persistentDataPath`, input uses the Input System, UI respects safe areas and needs no system back button. To build: switch platform to iOS, set a bundle id / signing team, build from Xcode. Store integrations would implement `IPurchaseService` and credit Diamonds through `Wallet`.
