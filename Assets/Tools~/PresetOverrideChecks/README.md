# Unit preset overrides

Select a scene unit or unit prefab in Unity. Assign **Inherited Preset**, then edit
**Preset Overrides**:

- **Stat Bonuses:** added to the preset's HP, MP, movement and five combat stats.
  Zero leaves a stat unchanged; negative bonuses are supported. HP is clamped to
  at least 1, MP/movement to at least 0. The preset's NotMove AI mode keeps movement 0.
- **Extra Inventory:** catalog dropdowns append items after the preset inventory.
  Duplicate items are allowed. Consumables expose charges (-1 uses their default).
  Each item can be marked droppable. Combined inventory still has the normal eight
  slots; the Inspector warns about overflow and weapons the unit cannot equip.
- **Extra Skills / Extra Passives:** catalog dropdowns append entries, ignoring
  duplicate IDs. Passives use the existing class-passive list.
- **Enable Overrides:** temporarily disables additions without deleting them.
- **Resolved Starting Values:** shows combined stats and loadout. Stats shown are
  before equipment and passive effects.

The preset asset stays unchanged, and later changes to it remain inherited.
Repeated validation or initialization does not add the same bonuses repeatedly.
Fields overwritten by the preset are hidden on preset-based units. Without a
preset, the direct starting fields remain editable. Save IDs are read-only.
Starting configuration is read-only in Play Mode.

Campaign units restored from a save use their saved progression/loadout rather
than adding scene/prefab overrides again. This avoids repeated bonuses across loads.

## Verification

```powershell
dotnet build com.windy.srpg.game.editor.csproj
dotnet run --project 'Assets/Tools~/PresetOverrideChecks/PresetOverrideChecks.csproj'
```

The console checks use the compiled game's resolver, with no mock implementation.
They do not launch Unity or verify Inspector rendering. Set `UnityManagedPath` with
`-p:UnityManagedPath=...` if the Unity install uses a different directory.
