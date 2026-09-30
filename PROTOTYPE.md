# PROTOTYPE.md --- Mobile Japanese Village Builder

## 0. Mission

Build a **small, polished, playable Prototype 0.01** of a mobile
free-to-play city-building game.

The prototype is **not** intended to prove the full long-term game
design. It should prove that these four things work together:

1.  Building placement on a touch device feels satisfying.
2.  A small Japanese settlement already feels attractive and alive.
3.  The production → wait → collect → build loop feels satisfying.
4.  The architecture is clean enough that the prototype can grow into a
    real Android/iOS game instead of being discarded.

The prototype should be playable in roughly **5 minutes** and should
already communicate:

> "I am building and growing my own small Japanese settlement."

Do not overbuild systems that are not required for Prototype 0.01.

------------------------------------------------------------------------

# 1. High-Level Game Concept

## Genre

-   Mobile city builder / economic strategy game
-   Free-to-play oriented
-   No combat
-   Relaxed but progression-driven
-   Long-term inspiration:
    -   Forge of Empires: mobile production timers, collection loop,
        premium acceleration
    -   Age of Empires II: readable settlement composition and
        atmosphere
    -   Caesar III: living city feeling
-   Do **not** clone UI, assets, names, layouts, balancing, or
    copyrighted visual designs from these games.

## Setting

**Japan, 15th century**, broadly inspired by the Muromachi/Sengoku-era
visual world.

The prototype does not need strict museum-level historical accuracy, but
avoid obvious modern or European elements.

Visual motifs may include:

-   timber architecture
-   plaster walls
-   tiled and thatched roofs
-   rice fields
-   bamboo
-   Japanese pines
-   flowering trees
-   streams
-   stone
-   wooden bridges
-   small shrines
-   carts
-   baskets
-   stacked firewood
-   villagers in period-inspired clothing
-   dogs, cats, chickens, ducks

## Core Design Principle

The game has no combat.

Long-term motivation should come from:

-   settlement growth
-   economy
-   production
-   building upgrades
-   visual transformation
-   map expansion
-   optimization
-   collection
-   beautification
-   population/city life
-   future progression systems

Prototype 0.01 implements only the smallest subset needed to test the
core loop.

------------------------------------------------------------------------

# 2. Target Platforms

Primary prototype target:

-   Android

Architecture must be compatible with:

-   iOS

Use one Unity project/codebase.

Do not place Android-specific logic inside gameplay systems.

Any platform-dependent services should be hidden behind
interfaces/adapters so that Google Play and Apple services can be added
later.

------------------------------------------------------------------------

# 3. Technology

Use:

-   **Unity**
-   **C#**
-   **Universal Render Pipeline (URP)**
-   3D scene
-   Orthographic camera
-   Landscape orientation
-   Unity Input System
-   Unity UI using **uGUI or UI Toolkit**, whichever provides the most
    reliable mobile implementation for this prototype
-   ScriptableObjects for static game definitions
-   JSON or equivalent local serialization for save data

Prefer stable Unity LTS tooling available in the development
environment.

Do not introduce unnecessary external dependencies.

The project must remain easy to open and build by another developer.

------------------------------------------------------------------------

# 4. Rendering Strategy

Although the final visual impression should resemble polished
2D/isometric game art, implement the game as a **3D world viewed through
a fixed orthographic camera**.

Use:

-   low/mid-poly geometry
-   stylized proportions
-   hand-painted-looking materials/textures
-   soft lighting
-   restrained specular highlights
-   readable silhouettes
-   ambient shadows
-   subtle environmental animation

The camera should make the game read like an isometric 2D city builder.

Do not use one giant pre-rendered background image for the map.

Terrain, vegetation, buildings, props, characters and water should be
independent scene objects/assets.

------------------------------------------------------------------------

# 5. Art Direction

Internal art direction name:

> **Superseded:** the art direction was replaced by the decided 3D-Toon style, see
> [Docs/grafikstil-3d-toon.md](Docs/grafikstil-3d-toon.md). The text below is kept for history.

**Style A --- Cozy & Detailed**

Characteristics:

-   warm
-   inviting
-   detailed
-   hand-painted appearance
-   stylized rather than photorealistic
-   slightly simplified shapes
-   clear silhouettes
-   readable on a phone
-   natural colors
-   moderately saturated
-   visually rich without becoming noisy

Think of a miniature living Japanese settlement.

Important:

Mobile readability is more important than tiny detail.

Buildings must remain recognizable at normal gameplay zoom.

Use generated/reference concept art only as visual guidance. Build
placeholder 3D assets that approximate the art direction; all assets
must be replaceable later without rewriting gameplay logic.

------------------------------------------------------------------------

# 6. Camera

Use an orthographic camera.

Initial visual target:

-   classic isometric-like angle
-   approximately 30--40° downward pitch
-   approximately 45° yaw relative to grid axes
-   fixed rotation during gameplay

Do not allow free camera rotation in Prototype 0.01.

Support:

-   one-finger drag/pan
-   pinch-to-zoom
-   smooth movement
-   clamped zoom
-   map boundary constraints

Suggested zoom behavior:

-   minimum: close enough to inspect individual buildings/NPCs
-   maximum: enough to see most of the prototype settlement

Expose zoom limits and movement sensitivity in configuration.

Camera movement must not continue wildly after the finger is released.

Small optional inertia is acceptable if it feels controlled.

UI interaction must not accidentally move the camera.

------------------------------------------------------------------------

# 7. World / Map

Create a handcrafted prototype map.

Logical grid:

-   approximately **20 × 20 tiles**

Initially buildable/unlocked area:

-   approximately **12 × 12 tiles**

The remaining area may be visible but should clearly be unavailable for
building.

The grid should exist logically even when grid lines are hidden.

## Terrain

Create a compact Japanese valley environment containing:

-   central relatively flat building area
-   grass
-   patches of dirt
-   a small stream/river
-   a small wooden bridge if practical
-   clusters of trees
-   bamboo
-   rocks
-   shrubs
-   flowers/grass props
-   slight elevation variation around edges

The central construction area must remain easy to read.

Do not make terrain so visually dense that buildings disappear.

## Buildability

Every grid cell must know whether it is:

-   unlocked
-   locked
-   buildable
-   blocked by terrain
-   occupied by a building

Water, major rocks, cliffs and other permanent obstacles should be
non-buildable.

Small cosmetic vegetation may be automatically hidden/removed when a
building is placed.

Architect this cleanly so terrain rules can later become more
sophisticated.

------------------------------------------------------------------------

# 8. Grid System

Create a reusable grid system independent of rendering.

Each cell should have at least:

-   coordinates
-   world position
-   buildable flag
-   unlocked flag
-   occupancy reference

Buildings must occupy rectangular footprints.

Prototype footprints:

-   House: 2 × 2
-   Woodcutter: 2 × 2
-   Town Hall: choose 3 × 3 or 4 × 4 based on visual fit

The system must support future footprints such as:

-   1 × 1
-   2 × 3
-   3 × 3
-   4 × 4
-   etc.

Do not hardcode logic specifically for the three prototype buildings.

------------------------------------------------------------------------

# 9. Initial Game State

On a fresh game:

-   Town Hall already exists
-   Coins: **500**
-   Wood: **0**
-   Diamonds: **20**

The player should be able to immediately understand that the next action
is to construct a Woodcutter.

No tutorial system is required yet.

A small contextual hint is acceptable, e.g.:

> Build a Woodcutter

but keep it optional/simple.

------------------------------------------------------------------------

# 10. Resources

Prototype resources:

## Coins

Icon concept: gold coin.

Used to:

-   build Woodcutters

Produced by:

-   Houses

Starting amount:

**500**

## Wood

Icon concept: log/timber.

Used to:

-   build Houses

Produced by:

-   Woodcutters

Starting amount:

**0**

## Diamonds

Premium currency.

Starting amount:

**20**

Used in Prototype 0.01 only to:

-   instantly finish active production

There is **no real-money store yet**.

However, premium-currency logic must be implemented as a proper economy
system, not hardwired directly into UI buttons.

Future IAP integration should be possible without redesigning production
logic.

------------------------------------------------------------------------

# 11. Core Economy Loop

The prototype must form a closed economy loop:

**Coins → Woodcutter → Wood → House → Coins → more buildings**

The loop should be functional from a fresh save without developer
intervention.

Balance is temporary and should live in data/configuration.

------------------------------------------------------------------------

# 12. Buildings

Implement exactly three primary building types.

## 12.1 Town Hall

Initial building.

Functions in 0.01:

-   visual settlement centerpiece
-   exists from game start
-   selectable
-   displays name/status panel
-   does not need production

Future systems may attach progression to it, but do not implement that
now.

Suggested visual:

-   largest prototype building
-   dark tiled roof
-   timber structure
-   modest stone foundation
-   more prestigious than houses
-   banners or small decorative details
-   clearly Japanese

------------------------------------------------------------------------

## 12.2 Woodcutter

Build cost:

**100 Coins**

Footprint:

**2 × 2**

Purpose:

Produces Wood.

Production options:

  Option          Duration    Reward
  ------------- ---------- ---------
  Small Load        30 sec    5 Wood
  Normal Load        2 min   15 Wood
  Large Load         5 min   30 Wood

These values must be data-driven.

Visual ideas:

-   small timber workshop
-   thatched/wooden roof
-   stacked logs
-   chopping block
-   axe/tools
-   small smoke/chimney effect if suitable
-   surrounding wood props

Production state should have a visible world indicator.

------------------------------------------------------------------------

## 12.3 House

Build cost:

**20 Wood**

Footprint:

**2 × 2**

Purpose:

Produces Coins.

Prototype production:

  Duration       Reward
  ---------- ----------
  1 min        25 Coins

Make the architecture support multiple production options even if House
initially has only one.

Visual ideas:

-   modest Japanese timber home
-   thatched roof
-   small fenced garden
-   baskets
-   barrel/water vessel
-   firewood
-   cloth or small household props

------------------------------------------------------------------------

# 13. Building Data Architecture

Use ScriptableObjects or equivalent static data definitions.

Suggested `BuildingDefinition` fields:

-   stable ID
-   display name
-   description
-   prefab
-   icon
-   footprint width
-   footprint height
-   build costs
-   category
-   production definitions
-   movable
-   rotatable
-   visual level
-   optional future upgrade references

Do not store runtime state inside ScriptableObjects.

Runtime state belongs in scene/runtime/save models.

------------------------------------------------------------------------

# 14. Building Placement

Building placement is a critical prototype feature.

Flow:

1.  Tap Build button.
2.  Open build panel.
3.  Select building.
4.  Enter placement mode.
5.  Spawn transparent/ghost preview.
6.  Snap preview to grid.
7.  Drag preview around map.
8.  Show valid footprint in green.
9.  Show invalid footprint in red.
10. Show confirm and cancel controls.
11. On confirmation:
    -   validate again
    -   deduct resources
    -   place building
    -   mark cells occupied
    -   remove/hide small vegetation under footprint
    -   save state
12. Exit placement mode.

Do not deduct resources before successful placement.

If player cannot afford the building:

-   clearly show insufficient resource state
-   do not enter broken placement state

------------------------------------------------------------------------

# 15. Moving Buildings

Support moving already placed non-Town-Hall buildings.

Preferred mobile interaction:

-   long press building
-   enter move mode
-   show ghost/selected state
-   drag to new grid position
-   validate footprint
-   confirm/cancel

When moving:

-   original footprint should be treated correctly during validation
-   cancelling restores original position
-   no cost in Prototype 0.01

Architecture should support future move costs if needed.

------------------------------------------------------------------------

# 16. Rotation

If easy to support cleanly, add a rotate button during placement.

Rotation should change footprint orientation for non-square future
buildings.

All current prototype buildings may be square, so rotation is not
essential visually.

Do not spend significant prototype time creating four unique art
variants.

------------------------------------------------------------------------

# 17. Selection

Tap a building to select it.

Selected state may use:

-   subtle outline
-   ground highlight
-   small glow
-   selection ring

Do not use a visually aggressive effect.

Tapping empty terrain deselects.

Selection should open a contextual bottom panel.

------------------------------------------------------------------------

# 18. Production System

Production is the heart of the mobile loop.

A production building can be in one of these states:

-   Idle
-   Producing
-   ReadyToCollect

Suggested runtime model:

``` text
BuildingRuntimeState
- instanceId
- definitionId
- gridPosition
- rotation
- productionState
- activeProductionId
- productionStartUtc
- productionEndUtc
```

Use real timestamps.

Do **not** implement production by decrementing and saving a counter
every second.

When loading/resuming:

``` text
remaining = productionEndUtc - currentUtc
```

If remaining \<= 0:

-   state becomes ReadyToCollect

This allows offline completion.

------------------------------------------------------------------------

# 19. Starting Production

When an idle Woodcutter is selected:

Show production choices in the bottom sheet.

Example:

**Woodcutter --- Lv.1**

-   30 sec → +5 Wood
-   2 min → +15 Wood
-   5 min → +30 Wood

Tap an option:

-   production starts immediately
-   start/end UTC timestamps saved
-   bottom sheet changes to active production state
-   world indicator appears

Only one production may run per building in Prototype 0.01.

No queue.

------------------------------------------------------------------------

# 20. Active Production UI

While producing, show:

-   resource icon
-   countdown
-   progress indicator
-   diamond finish button

Example:

``` text
Woodcutter
Producing Wood

01:34 remaining
[==========------]

💎 Finish Now
```

The countdown may update once per second while visible.

Do not save once per second.

------------------------------------------------------------------------

# 21. Ready-to-Collect State

When production finishes:

-   building enters ReadyToCollect
-   display a clear floating collection icon above it
-   subtle bounce/pulse animation is encouraged
-   do not automatically grant resources

The player must collect manually.

On tap of the collection indicator/building:

-   grant reward
-   play satisfying feedback
-   update resource bar
-   return building to Idle
-   save

Possible feedback:

-   resource icon flies toward top resource bar
-   small particles
-   number popup
-   subtle sound hook

Implement animation hooks even if final effects are placeholders.

------------------------------------------------------------------------

# 22. Diamond Speed-Up

During active production, offer instant completion using Diamonds.

Prototype rule:

Use a simple data-driven formula.

For example:

``` text
diamondCost = max(1, ceil(remainingMinutes))
```

or another simple configurable formula.

Do not scatter the formula across UI code.

Create an economy/service method such as:

``` text
GetProductionSkipCost(...)
TrySpendPremiumCurrency(...)
```

Flow:

1.  player taps Finish Now
2.  calculate current price
3.  if enough Diamonds:
    -   deduct Diamonds
    -   mark production ReadyToCollect
    -   save
4.  if insufficient:
    -   show small message/modal
    -   do NOT open a real-money store yet

For very short prototype timers, the price should remain sensible.

------------------------------------------------------------------------

# 23. Future Monetization Readiness

Do not implement IAP in 0.01.

But code should anticipate:

-   diamond packs
-   production acceleration
-   construction acceleration
-   cosmetic purchases
-   event content
-   optional future production slots

Avoid architecture where Diamonds are just an integer modified directly
from random scripts.

Create a central wallet/economy service.

Example concepts:

``` text
CurrencyType
- Coins
- Wood
- Diamonds
```

``` text
Wallet
GetBalance()
CanAfford()
Spend()
Add()
```

All currency changes should pass through this layer.

------------------------------------------------------------------------

# 24. Build Menu

Bottom area should contain a prominent Build button.

Tap opens a compact mobile-friendly panel.

Prototype categories do not need to be complex.

Show:

-   House
-   Woodcutter

Each entry should display:

-   icon
-   name
-   cost
-   resource icon
-   affordable/unaffordable state

The Town Hall is not buildable.

UI must be comfortable for fingers.

Avoid tiny buttons.

------------------------------------------------------------------------

# 25. HUD

Top resource bar:

``` text
🪙 500     🪵 0     💎 20
```

Use proper icons/placeholders, not emoji in production UI.

Respect:

-   Android cutouts
-   iPhone safe areas
-   different aspect ratios

Create a reusable safe-area component.

Do not anchor important UI directly against physical screen edges.

------------------------------------------------------------------------

# 26. Bottom Sheet

Use a reusable contextual bottom sheet.

Possible states:

-   BuildingInfo
-   ProductionSelection
-   ProductionActive
-   ReadyToCollect
-   BuildMenu

It should:

-   animate in/out quickly
-   not cover too much of the settlement
-   remain usable on smaller phones
-   use large tap targets

Do not create a separate bespoke UI architecture for every building.

------------------------------------------------------------------------

# 27. Mobile Input

Use Unity Input System.

Required gestures:

## Tap

-   select building
-   press UI
-   collect production

## Drag

-   pan camera
-   drag building ghost during placement

## Pinch

-   zoom camera

## Long Press

-   move existing building

Important conflict rules:

-   UI gestures should not affect world camera
-   dragging a building should not pan camera unless implementing
    edge-pan deliberately
-   pinch should not select objects
-   small finger movement during tap should not accidentally become drag
-   long press should have a reasonable threshold

Centralize gesture thresholds/configuration.

------------------------------------------------------------------------

# 28. NPCs

Add **2--3 simple villagers**.

They are cosmetic in Prototype 0.01.

They do not need jobs or simulated schedules.

Behavior:

-   wander within allowed walkable region
-   idle occasionally
-   choose another destination
-   avoid water/blocked terrain
-   optionally stop near buildings

Simple states:

-   Idle
-   Walk

Possible future-compatible state machine:

``` text
NPCState
Idle
Walking
Working
Carrying
Visiting
```

Only Idle/Walking are required.

Animations:

-   idle
-   walk

If real character assets are unavailable, use clean placeholders that
can later be swapped.

Do not let NPC implementation delay core gameplay.

------------------------------------------------------------------------

# 29. Animals

Optional but strongly desirable if inexpensive:

-   one dog or cat
-   a few chickens/ducks

Purely cosmetic.

Use same principle as NPCs:

simple wandering/idle behavior.

No breeding, feeding or resource mechanics.

------------------------------------------------------------------------

# 30. Environmental Animation

Use subtle motion to make the settlement feel alive.

Possible effects:

-   chimney smoke
-   water movement
-   grass sway
-   tree/bamboo sway
-   butterflies/leaves
-   small water particles
-   idle animals

Keep mobile performance in mind.

Do not fill the scene with expensive particle systems.

------------------------------------------------------------------------

# 31. Water

The prototype stream should visually move.

Use a lightweight URP-compatible material/shader.

Requirements:

-   stylized
-   readable
-   not photorealistic
-   inexpensive on mobile

Do not implement complex fluid simulation.

------------------------------------------------------------------------

# 32. Lighting

Use a warm daylight scene.

Suggested:

-   one primary directional light
-   soft shadows
-   ambient/environment lighting
-   subtle color grading if inexpensive

Avoid:

-   heavy real-time GI
-   excessive dynamic lights
-   expensive post-processing

The visual target is a painted miniature, not realism.

------------------------------------------------------------------------

# 33. Placeholder Assets

All prototype art must be replaceable.

Separate gameplay root from visuals.

Suggested hierarchy:

``` text
BuildingInstance
 ├── GameplayRoot
 ├── VisualRoot
 │    └── CurrentModel
 ├── SelectionVisual
 ├── ProductionIndicatorAnchor
 └── VFXAnchor
```

Replacing `CurrentModel` later should not break:

-   footprint
-   selection
-   production
-   save data
-   building identity

This is extremely important.

------------------------------------------------------------------------

# 34. Building Upgrade Readiness

Do **not** implement full building upgrades in 0.01.

However, architecture must anticipate them.

Future concept:

-   House Lv.1 → Lv.2 → Lv.3
-   Woodcutter Lv.1 → Lv.2 → Lv.3
-   Town Hall Lv.1 → Lv.2 → Lv.3

A future upgrade may change:

-   model
-   production
-   capacity
-   footprint
-   cost
-   build/upgrade time

Include a level field in runtime/save data.

Prototype buildings start at Level 1.

Do not build upgrade UI yet.

------------------------------------------------------------------------

# 35. Roads

No road requirement in Prototype 0.01.

Do not block building production because of missing roads.

Architecture should not assume roads will never exist.

A future version may require:

-   road adjacency
-   connected settlement graph
-   walkers
-   transport

Do not implement this now.

------------------------------------------------------------------------

# 36. Construction Timers

Do not implement building construction timers in 0.01.

Buildings appear immediately after placement.

Future architecture should allow a construction state.

Production timers are enough to validate the waiting loop.

------------------------------------------------------------------------

# 37. Save System

Local persistence is required.

Save at minimum:

-   save version
-   currencies
-   placed buildings
-   each building's stable instance ID
-   building definition ID
-   level
-   grid coordinates
-   rotation
-   production state
-   production ID
-   production start UTC
-   production end UTC

Suggested structure:

``` text
GameSave
- version
- wallet
- buildings[]
```

Save after important state-changing actions:

-   building placed
-   building moved
-   production started
-   production collected
-   Diamonds spent
-   currency changed due to gameplay

Also save on:

-   application pause
-   application quit where supported

Use UTC timestamps.

------------------------------------------------------------------------

# 38. Save Versioning

Include a save schema version from day one.

Example:

``` text
saveVersion: 1
```

Create a small migration layer/interface even if no migration exists
yet.

Do not tie saved buildings to Unity scene instance IDs.

Use stable GUID/string IDs.

------------------------------------------------------------------------

# 39. Reset / Developer Tools

For prototype development, provide a hidden or development-only debug
menu.

Useful actions:

-   Add 1000 Coins
-   Add 1000 Wood
-   Add 100 Diamonds
-   Finish all productions
-   Reset save
-   Toggle grid
-   Toggle locked area visualization

Ensure debug controls do not appear in release builds unless explicitly
enabled.

------------------------------------------------------------------------

# 40. Audio

Audio is not a blocker for 0.01.

Prepare hooks for:

-   building placement
-   button tap
-   production start
-   collection
-   currency gain
-   ambient environment

If suitable royalty-free/internal placeholder audio is already
available, add subtle effects.

Do not spend significant time sourcing audio.

------------------------------------------------------------------------

# 41. Feedback / Juice

The prototype should not feel like a dry editor.

Add lightweight feedback to important actions.

## Building placement

-   small scale-in or settle animation
-   dust puff if inexpensive
-   soft feedback hook

## Start production

-   immediate visual state change

## Production complete

-   floating icon
-   subtle pulse

## Collect

-   resource number popup
-   resource icon animation toward HUD if practical
-   HUD amount smoothly updates

Keep effects short.

------------------------------------------------------------------------

# 42. Scene Structure

Suggested scenes:

``` text
Bootstrap
Game
```

Or one Game scene if that is simpler.

If using Bootstrap:

Responsibilities:

-   initialize services
-   load save
-   transition to Game

Game scene:

-   terrain
-   grid
-   camera
-   buildings
-   NPCs
-   UI

Avoid unnecessary scene complexity.

------------------------------------------------------------------------

# 43. Suggested Code Architecture

Prefer simple service-oriented composition over a large framework.

Possible structure:

``` text
Assets/
  Art/
  Audio/
  Materials/
  Prefabs/
    Buildings/
    Environment/
    Characters/
    UI/
  Scenes/
  ScriptableObjects/
    Buildings/
    Productions/
  Scripts/
    Core/
    Economy/
    Buildings/
    Grid/
    Production/
    Save/
    Input/
    Camera/
    UI/
    World/
    NPC/
    Debug/
```

Possible services:

``` text
GameManager
SaveService
WalletService
GridService
BuildingService
ProductionService
TimeService
InputService
```

Do not create singleton spaghetti.

Use dependency references/interfaces where they improve testability.

Do not create an elaborate dependency-injection framework just for this
prototype.

------------------------------------------------------------------------

# 44. Time Service

Create an abstraction around current time.

Example:

``` csharp
public interface ITimeProvider
{
    DateTime UtcNow { get; }
}
```

Production should depend on this abstraction.

Benefits:

-   easier tests
-   easier debug
-   future server-time migration

For 0.01 use device UTC.

Add a clear code comment:

> Device time is not secure against clock manipulation. Replace/validate
> with trusted server time before production monetization goes live.

Do not attempt anti-cheat/server infrastructure now.

------------------------------------------------------------------------

# 45. Production Definitions

Production recipes should be data-driven.

Suggested fields:

``` text
ProductionDefinition
- id
- displayName
- durationSeconds
- rewards[]
```

Example Woodcutter definitions:

``` text
wood_small
duration = 30
reward = Wood 5
```

``` text
wood_medium
duration = 120
reward = Wood 15
```

``` text
wood_large
duration = 300
reward = Wood 30
```

House:

``` text
coins_house_basic
duration = 60
reward = Coins 25
```

Do not encode these values in UI scripts.

------------------------------------------------------------------------

# 46. Economy Events

Create lightweight events for currency changes.

Example:

``` text
CurrencyChanged(currency, oldValue, newValue)
```

HUD subscribes and updates.

This prevents UI from polling wallet values every frame.

Likewise consider events for:

-   BuildingPlaced
-   BuildingSelected
-   ProductionStarted
-   ProductionCompleted
-   ProductionCollected

Avoid a huge global event bus.

------------------------------------------------------------------------

# 47. Performance Target

The prototype should be comfortable on a mid-range Android device.

Aim for:

-   stable 60 FPS where practical
-   acceptable fallback at 30 FPS on weaker devices
-   minimal garbage generation during normal gameplay
-   no expensive per-building `Update()` loops if avoidable

For production timers:

Do not require every building to execute heavy logic every frame.

A central production manager can update visible timer UI and check
completion efficiently.

Use:

-   GPU instancing where useful
-   shared materials
-   reasonable texture sizes
-   object pooling for repeated VFX if needed

Do not prematurely optimize tiny systems, but avoid obviously poor
patterns.

------------------------------------------------------------------------

# 48. Mobile Quality Settings

Create sensible quality defaults.

Avoid requiring:

-   high-end desktop shadows
-   very high anisotropic filtering
-   expensive anti-aliasing
-   heavy post-processing

Test multiple common landscape aspect ratios.

At minimum verify UI at approximately:

-   16:9
-   19.5:9 / 20:9
-   iPhone-like landscape safe areas

------------------------------------------------------------------------

# 49. UI Visual Language

UI should complement Style A.

Use:

-   warm beige/parchment surfaces
-   dark brown/charcoal framing
-   muted green accents
-   restrained gold accents
-   rounded but not overly cartoonish panels
-   clear resource icons
-   readable typography

Avoid excessive Japanese decorative clichés.

The settlement should remain the visual star.

Buttons should clearly communicate state:

-   normal
-   pressed
-   disabled
-   unaffordable
-   premium

------------------------------------------------------------------------

# 50. Accessibility / Usability

Prototype requirements:

-   large touch targets
-   readable text
-   do not communicate valid/invalid placement using color alone; also
    use icon/outline/state
-   avoid essential tiny world-space text
-   countdowns should be legible

Do not require precision tapping on tiny characters.

------------------------------------------------------------------------

# 51. Orientation

Landscape only for Prototype 0.01.

Lock orientation accordingly.

Code/UI should not contain assumptions that make a future portrait
experiment impossible, but do not support portrait now.

------------------------------------------------------------------------

# 52. First Launch Experience

Keep it minimal.

Fresh launch:

1.  Game loads directly into settlement.
2.  Camera frames Town Hall and buildable area.
3.  HUD shows resources.
4.  Build button is obvious.
5.  Optional small hint says: **"Build a Woodcutter."**

No account creation.

No splash sequence beyond what is technically necessary.

No story intro.

No tutorial character.

------------------------------------------------------------------------

# 53. Prototype Gameplay Example

A successful first session should look roughly like this:

1.  Player starts with Town Hall and 500 Coins.
2.  Player taps Build.
3.  Player selects Woodcutter.
4.  Ghost appears.
5.  Player places it.
6.  100 Coins are deducted.
7.  Player taps Woodcutter.
8.  Chooses 30-second production.
9.  Timer runs.
10. Player can inspect/move camera while waiting.
11. Production completes.
12. Wood icon appears.
13. Player taps it.
14. +5 Wood is granted.
15. Player repeats until enough Wood exists.
16. Player builds House for 20 Wood.
17. House produces Coins.
18. Player collects Coins.
19. Economy loop is now self-sustaining.
20. Player can build additional Houses/Woodcutters.

Diamonds allow active production to be completed immediately.

------------------------------------------------------------------------

# 54. What NOT to Implement

Do not add these unless required to fix architecture:

-   combat
-   armies
-   enemies
-   PvP
-   multiplayer
-   backend
-   accounts
-   cloud saves
-   real IAP
-   advertisements
-   quests
-   achievements
-   research tree
-   historical eras
-   diplomacy
-   road requirements
-   citizen needs
-   happiness
-   taxes beyond House production
-   warehouses
-   inventory limits
-   complex logistics
-   worker assignment
-   resource transport
-   building upgrade gameplay
-   map expansion purchase flow
-   seasons
-   day/night cycle
-   weather
-   events
-   leaderboards
-   guilds
-   notifications

The point is to validate the **core mobile city-builder interaction**,
not to build the whole game.

------------------------------------------------------------------------

# 55. Prototype Polish Priorities

If development time is limited, prioritize in this order:

1.  Stable project/build
2.  Touch camera
3.  Grid and placement
4.  Wallet/economy
5.  Production/offline timers
6.  Save/load
7.  Mobile UI
8.  Visual building placeholders
9.  Environment
10. NPC life
11. VFX/polish

Never sacrifice save integrity or gameplay correctness for decorative
features.

------------------------------------------------------------------------

# 56. Automated Tests

Add focused tests for systems that can break progression.

At minimum test:

## Wallet

-   Add currency
-   Spend currency
-   Cannot overspend
-   Correct balances

## Grid

-   valid placement
-   overlap rejected
-   locked cells rejected
-   blocked cells rejected
-   moving building correctly releases/reclaims cells

## Production

-   production start/end calculation
-   incomplete timer remains Producing
-   elapsed timer becomes ReadyToCollect
-   collecting grants reward once
-   cannot collect twice
-   Diamond skip deducts correct amount

## Save

-   save/load round trip
-   active production survives reload
-   completed offline production loads as ReadyToCollect

Tests should not require waiting in real time.

Use fake time provider.

------------------------------------------------------------------------

# 57. Logging

Use concise development logging.

Useful log categories:

-   SAVE
-   BUILDING
-   ECONOMY
-   PRODUCTION

Do not spam logs every frame.

Errors involving save corruption or invalid building definitions should
be obvious.

------------------------------------------------------------------------

# 58. Failure Handling

The game should fail gracefully.

Examples:

If save file is missing:

-   create fresh save

If save file is invalid:

-   log error
-   preserve corrupted file if practical
-   start/recover safely rather than crash

If building definition referenced by save is missing:

-   log clearly
-   avoid crashing entire load process

If production definition is missing:

-   safely return building to Idle or use a defined recovery behavior

Document recovery behavior.

------------------------------------------------------------------------

# 59. Editor Tooling

Create small editor conveniences if they materially speed iteration.

Examples:

-   visualize logical grid in Scene view
-   toggle buildability overlay
-   paint locked/buildable cells
-   validate duplicate definition IDs
-   quickly create building definitions

Do not build a giant custom editor.

------------------------------------------------------------------------

# 60. Definition Validation

At startup/editor validation, detect:

-   duplicate building IDs
-   duplicate production IDs
-   missing prefab
-   invalid footprint
-   negative costs
-   invalid production duration
-   missing reward
-   invalid currency values

Fail loudly in development.

------------------------------------------------------------------------

# 61. Asset Replacement Contract

Assume all prototype models/textures will eventually be replaced.

Gameplay must reference buildings by stable definition IDs, not prefab
names.

Do not infer gameplay footprint from mesh bounds.

Footprint comes from data.

Do not infer production from visual components.

Visual and gameplay layers must remain separated.

------------------------------------------------------------------------

# 62. Future iOS Compatibility

From the first commit:

-   use safe-area aware UI
-   use Unity Input System
-   avoid Android-only filesystem assumptions
-   use `Application.persistentDataPath`
-   keep platform store code out of gameplay
-   avoid hardcoded back-button navigation as primary UX
-   ensure UI works without Android system buttons

The Android back gesture/button may close panels where appropriate, but
it must not be required.

------------------------------------------------------------------------

# 63. Future IAP Compatibility

Do not install/configure store products yet unless required by project
tooling.

Prepare an interface such as:

``` text
IPurchaseService
```

No gameplay system should know whether Diamonds came from:

-   starting balance
-   debug tools
-   future IAP
-   rewards
-   events

Wallet receives currency through controlled transactions.

------------------------------------------------------------------------

# 64. Economy Transaction Reason

Prefer tracking a reason for currency transactions.

Example:

``` text
CurrencyTransactionReason
BuildingPurchase
ProductionReward
ProductionSkip
DebugGrant
InitialGrant
```

This will later help analytics and economy balancing.

No analytics backend is required yet.

------------------------------------------------------------------------

# 65. Analytics Readiness

Do not integrate a full analytics platform yet.

Create clean points where future events can be emitted:

-   building_placed
-   production_started
-   production_skipped
-   production_collected
-   currency_spent

If useful, define an `IAnalyticsService` with a no-op implementation.

Do not let analytics architecture slow development.

------------------------------------------------------------------------

# 66. Prototype Visual Assets to Create

Create temporary but coherent assets for:

### Buildings

-   Town Hall Lv.1
-   Woodcutter Lv.1
-   House Lv.1

### Environment

-   grass terrain
-   dirt variation
-   3--5 tree variants
-   1 cherry blossom tree
-   1--2 bamboo clusters
-   3--5 rock variants
-   shrubs/flowers
-   river/stream
-   simple wooden bridge
-   fences
-   logs
-   baskets/barrels
-   sign/lantern-like period-appropriate props where suitable

### Characters

-   2--3 villager variants
-   basic walk/idle

### Animals

Optional:

-   dog/cat
-   chicken/duck

These are prototypes, not final production art.

------------------------------------------------------------------------

# 67. Visual Scale

Establish one consistent world scale.

Buildings should feel slightly exaggerated compared with realistic human
proportions so they remain readable on mobile.

NPCs should be visible but not dominate.

Town Hall should visually anchor the settlement.

House and Woodcutter should be immediately distinguishable by
silhouette.

------------------------------------------------------------------------

# 68. Map Composition

Initial camera view should contain:

-   Town Hall near central/back area
-   enough open buildable terrain in foreground/side
-   stream visible but not cutting the usable area into frustrating
    fragments
-   trees framing map edges
-   a few decorative focal points

Do not make the initial settlement look empty like a development test
scene.

Even with only one functional building, surrounding nature should create
atmosphere.

------------------------------------------------------------------------

# 69. Locked Land

Prototype should visually distinguish locked cells/region without
implementing land purchasing.

Possible approaches:

-   subtle fog
-   darker overlay
-   boundary markers
-   natural obstacles

Do not put giant padlock icons across the landscape.

The system should already know which cells are unlocked.

Future expansion can modify that state.

------------------------------------------------------------------------

# 70. Grid Visualization

Normal gameplay:

-   grid hidden or extremely subtle

Placement mode:

-   reveal relevant grid
-   valid cells clearly indicated
-   occupied/blocked cells distinguishable

Do not permanently cover the beautiful terrain with strong grid lines.

------------------------------------------------------------------------

# 71. Building World Indicators

Use a reusable world-space indicator system anchored above buildings.

States needed:

-   production progress/remaining time if desired
-   ready-to-collect resource icon

Indicators should:

-   face camera
-   scale appropriately
-   not overlap excessively
-   remain tappable
-   disappear when irrelevant

Avoid rendering a countdown above every building permanently.

------------------------------------------------------------------------

# 72. Production Completion While App Is Closed

Required scenario:

1.  Start 5-minute Wood production.
2.  Close/pause app.
3.  Wait until end time has passed.
4.  Reopen.
5.  Woodcutter shows ReadyToCollect.
6.  Reward has **not** yet been granted.
7.  Player taps collect.
8.  Reward is granted exactly once.

This scenario must be tested before considering 0.01 complete.

------------------------------------------------------------------------

# 73. App Pause / Resume

On pause:

-   save important state

On resume:

-   refresh production states from UTC
-   update HUD
-   restore clean UI state

Do not assume the application process stayed alive.

------------------------------------------------------------------------

# 74. Premium Skip UX

Prototype premium action should be explicit.

Button example:

``` text
Finish now   💎 2
```

Do not make tapping the Diamond icon itself spend currency.

No accidental purchases/spending.

A confirmation dialog is optional for tiny prototype values;
architecture should permit adding one later.

------------------------------------------------------------------------

# 75. No Dark Patterns in Prototype

Monetization is part of the product direction, but Prototype 0.01 is for
validating gameplay.

Do not implement:

-   fake scarcity
-   misleading countdowns
-   forced purchase prompts
-   fake discounts
-   obstructive confirmation patterns
-   random paid rewards

Diamonds simply provide an optional timer skip.

------------------------------------------------------------------------

# 76. Build Deliverables

The coding agent should leave the project in a state where the developer
can:

1.  Open it in Unity.
2.  Press Play and experience the complete loop.
3.  Build an Android development APK/AAB.
4.  Later switch build target to iOS without rewriting gameplay.

Provide clear build instructions.

------------------------------------------------------------------------

# 77. Documentation

Create/update:

``` text
README.md
```

Include:

-   Unity version
-   required packages
-   project structure
-   how to run
-   how to build Android
-   where game balance is configured
-   how save data works
-   how to reset save
-   known limitations
-   future iOS notes

Also keep this `PROTOTYPE.md` in the repository.

------------------------------------------------------------------------

# 78. Completion Checklist

Prototype 0.01 is complete only when all critical items below work.

## Project

-   [ ] Unity URP project opens without errors
-   [ ] Landscape mobile orientation
-   [ ] Android build succeeds
-   [ ] iOS-compatible architecture

## Camera

-   [ ] one-finger pan
-   [ ] pinch zoom
-   [ ] camera bounds
-   [ ] UI does not accidentally move camera

## World

-   [ ] approximately 20×20 logical grid
-   [ ] approximately 12×12 unlocked area
-   [ ] attractive Japanese prototype environment
-   [ ] blocked terrain works

## Buildings

-   [ ] Town Hall starts on map
-   [ ] House can be built
-   [ ] Woodcutter can be built
-   [ ] overlap prevented
-   [ ] invalid placement prevented
-   [ ] buildings can be moved
-   [ ] placement is touch-friendly

## Economy

-   [ ] starts with 500 Coins
-   [ ] starts with 0 Wood
-   [ ] starts with 20 Diamonds
-   [ ] building costs work
-   [ ] insufficient funds handled

## Production

-   [ ] Woodcutter production options work
-   [ ] House production works
-   [ ] timer works
-   [ ] offline timer works
-   [ ] ready-to-collect state works
-   [ ] reward collected exactly once
-   [ ] Diamond skip works

## Save

-   [ ] currencies persist
-   [ ] buildings persist
-   [ ] moved positions persist
-   [ ] active production persists
-   [ ] completed production survives restart
-   [ ] save version exists

## UI

-   [ ] top resource bar
-   [ ] Build button
-   [ ] build menu
-   [ ] contextual building panel
-   [ ] production UI
-   [ ] premium skip UI
-   [ ] safe areas respected

## Life / Polish

-   [ ] at least 2 NPCs wander
-   [ ] subtle environmental animation
-   [ ] collection feedback
-   [ ] coherent Style A presentation

------------------------------------------------------------------------

# 79. Definition of Success

Do not judge Prototype 0.01 by amount of content.

Judge it by whether a player can:

-   understand the interface without explanation
-   comfortably navigate on a phone
-   place buildings without fighting the controls
-   enjoy seeing the small settlement
-   understand the economy
-   start production
-   leave and return
-   collect resources
-   use Diamonds to accelerate production
-   build another structure
-   feel motivated to continue growing the village

If those things work, the prototype has succeeded.

------------------------------------------------------------------------

# 80. Implementation Order for the Coding Agent

Follow approximately this order:

### Phase 1 --- Project foundation

1.  Create/configure Unity URP project.
2.  Configure landscape Android target.
3.  Install/configure Input System.
4.  Create folder structure.
5.  Establish bootstrap/game architecture.
6.  Create time, wallet and save interfaces.

### Phase 2 --- Grid and camera

7.  Implement logical grid.
8.  Implement buildability/occupancy.
9.  Add Scene-view debug grid.
10. Implement mobile camera pan/zoom/bounds.
11. Verify gestures in Device Simulator and touch input.

### Phase 3 --- Building system

12. Create `BuildingDefinition`.
13. Create generic building runtime model.
14. Implement Town Hall spawn.
15. Implement placement ghost.
16. Implement validation.
17. Implement purchasing/placement.
18. Implement selection.
19. Implement move mode.

### Phase 4 --- Economy and production

20. Implement wallet.
21. Create production definitions.
22. Implement production state machine.
23. Implement UTC timer logic.
24. Implement collection.
25. Implement Diamond skip.
26. Implement offline completion.

### Phase 5 --- Persistence

27. Serialize currencies.
28. Serialize buildings.
29. Serialize active production.
30. Add save version.
31. Test kill/restart/offline scenarios.

### Phase 6 --- UI

32. Resource HUD.
33. Build menu.
34. Building bottom sheet.
35. Production selection.
36. Active production panel.
37. Ready-to-collect world indicator.
38. Premium skip state.
39. Safe-area support.

### Phase 7 --- World

40. Build small Japanese valley map.
41. Add stream.
42. Add vegetation/rocks.
43. Add placeholder Style A buildings.
44. Add lighting/material polish.
45. Add environmental movement.

### Phase 8 --- Life

46. Add 2--3 wandering NPCs.
47. Add optional animal.
48. Add simple idle/walk animations.

### Phase 9 --- Polish/testing

49. Add placement feedback.
50. Add collection feedback.
51. Add debug menu.
52. Add automated tests.
53. Test multiple aspect ratios.
54. Profile Android build.
55. Fix touch conflicts.
56. Verify fresh-save progression.
57. Verify offline timer.
58. Write README/build instructions.

------------------------------------------------------------------------

# 81. Coding Standards

-   Prefer readable C# over clever abstractions.
-   Keep classes focused.
-   Avoid giant managers containing unrelated logic.
-   Use namespaces.
-   Use serialized private fields instead of public mutable fields where
    practical.
-   Validate inspector references.
-   Use stable IDs.
-   Avoid `FindObjectOfType`/scene searches as core dependency strategy.
-   Avoid per-frame polling where events suffice.
-   Comment architectural reasons, not obvious syntax.
-   Do not leave core systems as pseudocode.
-   Do not silently swallow exceptions.
-   Keep balance values out of code.
-   Keep UI logic separate from economy logic.
-   Keep visual prefabs separate from gameplay state.

------------------------------------------------------------------------

# 82. Important Agent Behavior

The agent should actively make reasonable implementation decisions
rather than stopping for minor questions.

When something is unspecified:

1.  choose the simplest robust solution consistent with this document;
2.  make it configurable;
3.  document the choice in `README.md`;
4.  continue.

Only stop for clarification if a decision would fundamentally change the
architecture or make the requested prototype impossible.

Do not expand scope merely because additional features seem interesting.

------------------------------------------------------------------------

# 83. Final Acceptance Scenario

Before declaring completion, perform this exact manual flow:

1.  Delete/reset save.
2.  Launch game.
3.  Confirm 500 Coins / 0 Wood / 20 Diamonds.
4.  Confirm Town Hall exists.
5.  Pan and zoom map.
6.  Open Build menu.
7.  Place Woodcutter on valid cells.
8.  Verify Coins decrease by 100.
9.  Try overlapping another building and verify rejection.
10. Start 30-second Wood production.
11. Verify timer.
12. Use normal completion once and collect Wood.
13. Verify Wood balance increases.
14. Start another production.
15. Use Diamond instant finish.
16. Verify Diamonds decrease.
17. Collect reward.
18. Produce enough Wood to place House.
19. Place House for 20 Wood.
20. Start House coin production.
21. Close application before completion.
22. Reopen after timer has elapsed.
23. Verify House is ReadyToCollect.
24. Collect Coins.
25. Move House.
26. Restart application.
27. Verify moved position persists.
28. Verify all balances persist.
29. Confirm NPCs/environment remain visually alive.
30. Build Android development package.

If any core step fails, Prototype 0.01 is not finished.

------------------------------------------------------------------------

# 84. Guiding Principle

**Build the smallest version that already feels like a real game, not a
technology demo.**

The code should be extensible, but the feature set should remain
deliberately tiny.

The player should see a beautiful little 15th-century Japanese
settlement, place a Woodcutter, wait for Wood, collect it, build a
House, generate Coins and immediately understand how this village could
eventually grow into a large living city.
