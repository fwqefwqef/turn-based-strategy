# Phase 12 Publication Audit Report

Last run: 2026-06-17 (private workspace, post Phase 11B/11C naming cleanup)

Auditor: automated + manual review of active `Assets/Game`, `Assets/Scenes`, asmdefs, and build output.

Private framework reference copy (not shipped): `TBS Framework - Copy/` at repo root (gitignored).

---

## Summary

| Area | Result |
|------|--------|
| Code contamination (`TbsFramework`, vendor asmdefs) | **PASS** |
| Scene/prefab script contamination | **PASS** |
| Active project compile without `Assets/TBS Framework` | **PASS** |
| Unified code root (`Assets/Game/Code`) | **PASS** |
| Bridge / migration debt (11C) | **OPEN** — not a publication contaminant, but readability debt |
| Public repo packaging (Phase 13) | **IN PROGRESS** |

**Publication blockers remaining:** finish Phase 13 packaging (license, clone smoke test, public/export branch confirmation). Phase 11C bridge cleanup is recommended before public release but does not reintroduce vendor code.

---

## 1. Code audit

Commands (re-run before shipping):

```powershell
rg -n "TbsFramework|CrookedHead|com\.crookedhead\.tbsf" Assets/Game Assets/Scenes -g "*.cs" -g "*.asmdef"
rg -n "SampleSquare|RuntimeSampleSquare|FrameworkSquareAnchor|FrameworkUnitAnchor|FrameworkCellGridAnchor" Assets/Game Assets/Scenes -g "*.cs" -g "*.prefab" -g "*.unity"
Test-Path "Assets/TBS Framework"   # must be FALSE in public repo
dotnet build com.windy.srpg.game.csproj
dotnet build com.windy.srpg.game.scenes.csproj
```

| Check | Result | Notes |
|-------|--------|-------|
| `using TbsFramework` in game/scenes C# | **0 matches** | |
| `com.crookedhead.tbsf` in active asmdefs | **0 references** | `com.windy.srpg.game.asmdef` references Unity packages only |
| Framework anchor components in code | **0 matches** | Removed during Phase 11 |
| `SampleSquare` / dual-cell hosts in active assets | **0 matches** | Tiles use single `Cell` component |
| Vendor strings in game/scenes `.cs` | **0 matches** | Historical mentions only in docs under `Assets/Docs` and `CLEAN_RUNTIME_REWRITE_PLAN.md` |
| `com.windy.srpg.game` build | **PASS** (0 errors) | 3 obsolete-API warnings |
| `com.windy.srpg.game.scenes` build | **PASS** (0 errors) | |

### Naming (project-owned)

| Concept | Current type | Namespace |
|---------|--------------|-----------|
| Grid host | `CellGrid` | `Windy.Srpg.Game.Grid` |
| Turn orchestrator | `RuntimeGrid` | `Windy.Srpg.Runtime.Grid` |
| Tile | `Cell` | `Windy.Srpg.Runtime.Grid` |
| Unit (runtime mirror) | `GridUnit` | `Windy.Srpg.Runtime.Units` |
| Unit (gameplay) | `Unit` | `Windy.Srpg.Game.Units` |
| Action base | `BattleAction` | `Windy.Srpg.Runtime.Actions` |

---

## 2. Asset audit

| Check | Result | Notes |
|-------|--------|-------|
| `Assets/TBS Framework/` in active tree | **REMOVED** (empty stub deleted this audit) | Vendor copy kept locally at `TBS Framework - Copy/` |
| Active scene `test.unity` | Present | Uses project-owned scripts |
| Active prefabs (`Square`, `Wall`, unit prefabs) | Present | `Cell` script GUID preserved from former tile host |
| Missing script GUIDs in active prefabs/scenes | **Not machine-verified** | Requires Unity Editor open + Console check |
| Framework example art/scenes in `Assets/` | **None found** in active paths | |

---

## 3. Provenance audit

| Check | Result | Notes |
|-------|--------|-------|
| Runtime/game code under `Windy.Srpg.*` | **Yes** | |
| Vendor `.meta` reuse in shipped runtime | **No evidence** in active code tree |
| Migration comparison CSV in repo | **Present at repo root** | `framework_compare_code*.csv` — **exclude from public repo** (already gitignored) |
| Stale generated `com.crookedhead.tbsf*.csproj` at repo root | **Present** | Unity regeneration artifacts — **exclude from public repo** (gitignored via `*.csproj`) |

---

## 4. Self-containment audit

| Check | Result | Notes |
|-------|--------|-------|
| Clone opens without vendor folder | **Expected PASS** | Builds succeed with `Assets/TBS Framework` absent |
| README requires vendor contact | **No** (but README text is stale — update in Phase 13) |
| Hidden DLL / missing asmdef | **None found** in active game/scenes assemblies |
| `Packages/manifest.json` vendor packages | **None** (standard Unity registry only) |

---

## 5. Known non-blockers (11C debt)

These are **project-owned** migration names/patterns, not vendor contamination:

- `CellGridStateRemotePlayerTurn`, `EnterRemotePlayerTurnState` — game-owned blocked-input state; rename optional
- `ApplyLegacyStateFromRuntime`, `SyncRuntimeMirrorNow`, `suppressLegacyToRuntimeStateMirror` — runtime↔scene sync bridge in `CellGrid.Runtime.cs`
- Large coordination files: `Unit.cs`, `MoveAbility*.cs`, `CellGrid*.cs`

Track under Phase 11C; do not block Phase 13 solely on these unless readability is a release goal.

---

## 6. Exclude from public repo (Phase 13 input)

| Path / pattern | Reason |
|----------------|--------|
| `TBS Framework - Copy/` | Private vendor source reference |
| `Assets/TBS Framework/` | Vendor tree (must not exist in public clone) |
| `framework_compare_code*.csv` | Private migration notes |
| `Assets/Docs/Rewrite/CURSOR_HANDOFF.md` | Private agent handoff (optional keep redacted) |
| `Tools/phase11*.py`, `Tools/collapse_cell.py`, etc. | One-off migration scripts (optional) |
| Generated `com.crookedhead.tbsf*.csproj` | Stale Unity project files |
| Private legal/vendor email archive | Outside repo per plan |

---

## 7. Recommended next steps (Phase 13)

1. Update root `README.md` for public clone (setup, license, no vendor dependency).
2. Unity smoke test on consolidated code: pre-battle → deploy → battle → move → attack → AI → win/lose.
3. Create public rewrite workspace or branch with exclusions above applied.
4. Editor pass: confirm zero missing scripts in `test.unity` and unit/tile prefabs.
5. Optional: finish 11C bridge shrink before tagging first public release.

---

## Audit command log (2026-06-17)

```
TbsFramework          : 0 files (Assets/Game, Assets/Scenes, *.cs, *.asmdef, *.prefab, *.unity)
CrookedHead             : 0 files
com.crookedhead.tbsf    : 0 files
SampleSquare            : 0 files
FrameworkSquareAnchor   : 0 files
BattleSquareCell        : 0 files (renamed → Cell)
dotnet build game       : SUCCESS
dotnet build scenes     : SUCCESS
Assets/Game/Code/*.cs   : 98 files
```
