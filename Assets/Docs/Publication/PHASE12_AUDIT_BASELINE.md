# Phase 12 Publication Audit — Baseline (2026-06-17)

Private workspace snapshot after **Phase 10** (tile collapse, smoke-tested) and **Phase 11A** (asmdef filename cleanup + highlighter rename).

This is a **baseline inventory**, not a passing audit. The project is still intentionally framework-coupled in the private workspace.

---

## Passing (project-owned naming)

| Check | Status |
|-------|--------|
| Game code namespaces | `Windy.Srpg.Game.*` and `Windy.Srpg.Runtime.*` — no `TbsFramework` namespaces in game/runtime sources |
| Runtime assembly | `com.windy.srpg.runtime` — **zero** `using TbsFramework` in `Assets/Game/Runtime/**` |
| Scenes assembly scripts | `Assets/Scenes/*.cs` — **zero** `using TbsFramework` (SampleSquare/RuntimeSampleSquareCell removed) |
| Game asmdef **filename** | `com.windy.srpg.game.asmdef` (renamed from crookedhead filename; GUID preserved) |
| Scenes asmdef **filename** | `com.windy.srpg.game.scenes.asmdef` |
| Active tile prefabs | `Square.prefab`, `Wall.prefab` use `BattleSquareCell` + `FrameworkSquareAnchor` only |

---

## Still failing (expected until framework removal)

| Check | Count / notes |
|-------|----------------|
| `using TbsFramework` in `Assets/Game/Scripts/**` | ~40 files — bridge/anchor layer + gameplay still references framework `Cell`, `Unit`, `CellGrid` types |
| Game asmdef **reference** to framework | `com.windy.srpg.game` → `com.crookedhead.tbsf` (required until anchors replaced) |
| Framework folder present | `Assets/TBS Framework/` — entire vendor tree (private workspace only) |
| Framework anchor components | `FrameworkCellGridAnchor`, `FrameworkSquareAnchor`, `FrameworkUnitAnchor` on scene prefabs |
| Dual code roots | `Assets/Game/Scripts` + `Assets/Game/Runtime` (Phase 11B consolidation pending) |

---

## Active scene asset contamination scan

Searched `Assets/Scenes/**` for `TbsFramework`, `SampleSquare`, `crookedhead`:

- `test.unity` — may contain historical YAML strings; no live SampleSquare scripts
- Unit preset `.asset` files — may reference framework types in serialized fields (audit before publication)

---

## Recommended next slices (private workspace)

1. **Phase 11B** — consolidate `Assets/Game/Scripts` + `Assets/Game/Runtime` under one publishable root (`Assets/Game/Code` or similar)
2. **Phase 11C** — isolate or shrink bridge files (`*Legacy*`, `*Framework*`, `RuntimeMirror`)
3. **Lifecycle bootstrap (remaining)** — remove `StartLegacyBattle` fallback once runtime board is mandatory
4. **Phase 12** — re-run this audit after each slice; block publication until all rows pass

---

## Audit commands (re-run before publication)

```powershell
rg -n "TbsFramework|CrookedHead|com\.crookedhead\.tbsf" Assets/Game Assets/Scenes -g "*.cs" -g "*.asmdef"
rg -n "SampleSquare|RuntimeSampleSquare" Assets/Scenes Assets/Game -g "*.cs" -g "*.prefab" -g "*.unity"
Test-Path "Assets/TBS Framework"   # must be FALSE in public repo
```
