# Phase 13 Packaging Checklist

Last updated: 2026-06-18

## Goal

Prepare a public repo/package that:

- opens in Unity without any vendor framework files
- contains all code needed to compile and run
- excludes private rewrite artifacts and local-only reference material

## Current Status

- Publication audit: complete
- Public packaging: started
- Export boundary file: added via `.gitattributes`
- Public-facing README: updated
- License: added (`LICENSE`, The Unlicense)
- Unity clone smoke test: still required

## Public Export Boundary

These paths are currently marked `export-ignore` in `.gitattributes` and should stay out of a public release archive:

- `TBS Framework - Copy/`
- `framework_compare_code*.csv`
- `Tools/`
- `Assets/Docs/Rewrite/CURSOR_HANDOFF.md`
- `Assets/Docs/Rewrite/CODEX_HANDOVER.md`
- `Assets/Docs/Publication/PHASE12_AUDIT_BASELINE.md`
- `Assets/CLEAN_RUNTIME_REWRITE_PLAN.md`

## Still Required Before Publishing

1. Run a Unity editor smoke test from the current project state:
   - open `Assets/Scenes/test.unity`
   - verify no missing scripts
   - verify pre-battle flow
   - verify deployment flow
   - verify battle flow
   - verify save-backed owned units load correctly
2. Create the actual public workspace or release branch with the export boundary applied.
3. Re-run the publication audit commands from `PHASE12_AUDIT_REPORT.md`.
4. Confirm the packaged/public tree does not contain any vendor files or private migration artifacts.

## Suggested Release Prep Flow

1. Commit the private workspace changes.
2. Create a release branch or clean public copy.
3. Use the export boundary as the default exclusion list.
4. Open the result in Unity and run the smoke test.
5. Tag the first public release only after that smoke test passes.

## Notes

- Phase 11C cleanup can continue in parallel, but it is no longer a publication blocker by itself.
- `PHASE12_AUDIT_REPORT.md` can remain in the public repo if you want the provenance/audit trail visible.
- The handoff docs and rewrite plan are intentionally treated as private working notes.
