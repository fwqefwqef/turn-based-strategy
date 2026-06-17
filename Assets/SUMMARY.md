# Turn Based Strategy - Summary

Last reviewed: 2026-06-08

## Project Snapshot

- Unity SRPG with heavily customized gameplay/runtime code, but still structurally dependent on `TBS Framework`.
- Current playable loop:
  - load game data + text tables
  - load or seed a readable campaign save
  - pre-battle unit selection / deployment ordering
  - battle with movement preview, action menus, attacks, skills, items, trade, EXP, and inspect UI
- Main decoupling note:
  - the deepest framework coupling is still at:
    - `Assets/Game/Scripts/Grid/CellGrid.cs`
    - `Assets/Game/Scripts/Units/Unit.cs`
    - `Assets/Game/Scripts/Abilities/Ability.cs`
    - `Assets/Game/Scripts/Grid/States/`
    - `Assets/Game/Scripts/AI/`

## Important Data Files

- Unified gameplay catalog:
  - `Assets/StreamingAssets/gdata.json`
- Text / localization table:
  - `Assets/StreamingAssets/game_text.csv`
- Persistent campaign save:
  - `Application.persistentDataPath/campaign_save.json`
- Friendly unit preset assets:
  - `Assets/Scenes/friendly preset/`
- Enemy unit preset assets:
  - `Assets/Scenes/enemy preset/`

## Main Runtime Entry Points

1. Battle/grid flow:
   - `Assets/Game/Scripts/Grid/CellGrid.cs`
2. Unit runtime:
   - `Assets/Game/Scripts/Units/Unit.cs`
3. Ability base + move/action flow:
   - `Assets/Game/Scripts/Abilities/Ability.cs`
   - `Assets/Game/Scripts/Abilities/MoveAbility.cs`
4. Pre-battle UI flow:
   - `Assets/Game/Scripts/UI/PreBattleUIController.cs`
5. Save/load:
   - `Assets/Game/Scripts/Campaign/CampaignSaveData.cs`
   - `Assets/Game/Scripts/Campaign/CampaignSaveFactory.cs`
   - `Assets/Game/Scripts/Campaign/CampaignSaveManager.cs`
6. Catalog loading:
   - `Assets/Game/Scripts/Catalogs/JsonCatalogLoader.cs`
7. Text/localization loading:
   - `Assets/Game/Scripts/Localization/GameTextCatalog.cs`

## Implemented Systems

### 1. Catalog-driven gameplay data

- Items, skills, passives, and buffs now load from one JSON file:
  - `Assets/StreamingAssets/gdata.json`
- Runtime loading goes through `CatalogResourceLoader`.
- This replaced the old idea of scattering built-in entries across several hardcoded catalog files.

### 2. Text / localization layer

- `GameTextCatalog` loads `Assets/StreamingAssets/game_text.csv`.
- Intended use:
  - code can request text by key
  - scene-authored TMP text can still win if already filled in
  - explicit string overrides can also win when desired
- This means UI text is no longer forced to live only in code.

### 3. Unit presets and sprite layout

- `UnitPreset` is now a general standalone unit definition asset, not just an enemy-only helper.
- Each preset can define:
  - identity
  - sprite
  - sprite layout
  - base stats including movement
  - growth rates
  - starting inventory
  - starting skills
  - starting passives
- `UnitPresetOverride` still exists and is mainly useful for enemy/variant layering.
- Sprite layout has been simplified to:
  - target size
  - offset X
  - offset Y
- Current default sprite target size:
  - `1.2 x 1.2`

### 4. Save identity and owned-unit model

- Friendly units are now saved as flattened owned-unit data, not as `preset + override`.
- Each saved unit stores both:
  - `UnitId`
  - `VisualId`
- `UnitId` is the gameplay/save identity.
- `VisualId` preserves sprite/preset identity for lookups without making the save depend on a preset layer for stats/loadout.
- Current owned-unit save payload includes:
  - level
  - experience
  - weapon proficiencies
  - base stats including movement
  - growth rates
  - current HP / MP
  - inventory with remaining charges
  - learned skills
  - unique passives
  - equipped passives

### 5. Readable campaign save

- Save file is JSON written to:
  - `Application.persistentDataPath/campaign_save.json`
- Current top-level save structure includes:
  - `OwnedUnits`
  - `DeploymentRosterUnitIds`
  - `StorageItems`
  - `Gold`
- `CampaignSaveFactory` handles:
  - creating starter owned units from presets
  - merging captured units into an existing save
  - normalizing deployment rosters
  - resolving shortened or oversized rosters against deployment slot count

### 6. Pre-battle deployment flow

- There is now a pre-battle phase driven by `CellGrid` + `PreBattleUIController`.
- Root pre-battle actions:
  - `Battle Start`
  - `Select Units`
  - `Switch Deployment`
- Current roster rules:
  - roster cannot go below 1 unit
  - roster cannot exceed deployment slot count
  - if the saved roster is shorter than the available deployment slots, it is auto-filled in owned-unit save order
  - if the saved roster is longer than slot count, it is trimmed
- `Select Units` currently behaves like:
  - selected stack/list
  - benched stack/list
  - toggle between them within slot rules
- `Switch Deployment` currently behaves like:
  - select one deployed unit
  - select a second deployed unit
  - swap their roster positions
- Roster order is the deployment order and persists back to the save.
- `CellGrid.startBattleImmediatelyWithCurrentRoster` can bypass the menu and start immediately.

### 7. Scene deployment slots

- Deployment slots are now explicit scene objects via:
  - `Assets/Game/Scripts/Grid/DeploymentSlot.cs`
- They are highlighted in-scene and can show selected state.
- They are used only for friendly deployment.
- They are hidden when battle actually starts.

### 8. Friendly unit deployment placeholders

- Friendly units in the scene are treated as deployment placeholders during pre-battle.
- On scene start, save data is loaded into those placeholder units in deployment-slot order.
- Units not currently in the deployment roster are excluded from battle.
- `starterOwnedUnitPresets` in `CellGrid` still matter today for:
  - first-run save seeding
  - resolving a saved unit's `VisualId` back to a visual preset

### 9. Combat / progression / UI systems already in place

- Pending move preview before committing movement
- Action menu:
  - `Attack`
  - `Heal`
  - `Skill`
  - `Item`
  - `Trade`
  - `Wait`
  - `Cancel`
- Single-target attacks and skills
- Healing skills
- Area spells
- MP-based skill costs
- Inventory, equipment, consumables, and trade menus
- Buffs and passives
- Level / EXP progression
- World HP / MP bars
- Unit hover / inspect UI
- Combat sequence HUD
- Camera assistance and UI clamping helpers

## Current Preset/Save Notes

- Friendly presets currently exist as standalone assets in:
  - `Assets/Scenes/friendly preset/`
- Enemy presets currently exist as standalone assets in:
  - `Assets/Scenes/enemy preset/`
- Starting consumable charge override defaults are initialized to `-1`, meaning:
  - use the item's default charge count unless explicitly overridden
- If a consumable would initialize with `0` charges, that entry is skipped instead of being kept as dead inventory.

## Architecture Notes For Framework Decoupling

- The game is not using `TBS Framework` only as a loose helper library.
- Core runtime types still inherit from framework classes:
  - `CellGrid : CellGrid`
  - `Unit : Unit`
  - `Ability : Ability`
  - custom grid states derive from framework grid-state types
  - AI actions derive from framework AI types
- Because of that, a full decoupling should be treated as a staged migration, not a one-shot rewrite.

## Good Starting Files For The Next Phase

- `Assets/Game/Scripts/Grid/CellGrid.cs`
- `Assets/Game/Scripts/Units/Unit.cs`
- `Assets/Game/Scripts/Abilities/Ability.cs`
- `Assets/Game/Scripts/Grid/States/CellGridState.cs`
- `Assets/Game/Scripts/AI/AttackAIAction.cs`
- `Assets/Game/Scripts/AI/MoveToPositionAIAction.cs`
- `Assets/TBS Framework/Scripts/`

## Major Things Still Not Finished

- Pre-battle inventory/convoy management
- Passive slot/loadout progression rules tied to level
- Full controller / keyboard-first control revision
- Better AI evaluation and targeting previews
- Framework decoupling
