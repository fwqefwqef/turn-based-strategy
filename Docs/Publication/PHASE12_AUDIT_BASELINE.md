# Phase 12 Publication Audit — Baseline (superseded)

**Superseded by:** [`PHASE12_AUDIT_REPORT.md`](PHASE12_AUDIT_REPORT.md) (2026-06-17, passing code/asset audit).

This file is kept as a historical snapshot from the pre–Phase 11 completion inventory. Do not use it for go/no-go decisions.

---

## Historical snapshot (2026-06-17 morning)

Private workspace snapshot after **Phase 10** (tile collapse, smoke-tested) and **Phase 11A** (asmdef filename cleanup + highlighter rename).

At that time the project was still intentionally framework-coupled. **All rows in "Still failing" below were resolved** in Phases 11, 11B, and subsequent 11C naming work before the Phase 12 audit pass.

### Was failing (now resolved)

| Check | Was | Now |
|-------|-----|-----|
| `using TbsFramework` in game code | ~40 files | **0** |
| Game asmdef → `com.crookedhead.tbsf` | required | **removed** |
| `Assets/TBS Framework/` | present | **removed from active tree** |
| Framework anchor components | on prefabs | **removed** |
| Dual code roots | Scripts + Runtime | **merged → `Assets/Game/Code`** |

Re-run commands and full results: see `PHASE12_AUDIT_REPORT.md`.
