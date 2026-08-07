# Chapter Data Notes

In this project, a chapter is a Unity scene that contains one battle map.

Add `ChapterData` to the scene to store chapter-level rules and metadata.

Recommended scene wiring:

1. Create a scene root GameObject named `Chapter Data`.
2. Add component: `TBS/Chapter/Chapter Data`.
3. Set `Average Enemy Level`.
4. Leave `Battle Conditions` at the default list unless the chapter needs custom behavior.

Keep `ChapterData` separate from `CellGrid`. The scene sync tool preserves the `CellGrid` map root, but copies non-preserved scene system roots from the source scene. A separate `Chapter Data` root lets chapter configuration copy cleanly into level scenes.

Default battle conditions:

1. `Victory / DefeatAllEnemies`
2. `Defeat / LoseAllAllies`

The condition list is ordered. The first condition that becomes true determines the battle result.

Battle conditions are always evaluated from the friendly player side. Friendly units are player `0`; all non-`0` players count as enemies for chapter win/loss checks.

`LastSideStandingCondition` now checks for `ChapterData` first. If no `ChapterData` exists in the scene, it falls back to the old last-side-standing behavior.

`Average Enemy Level` is also used by EXP logic when a chapter data component exists. If no chapter data exists, EXP falls back to calculating the average level from enemy units currently on the grid.
