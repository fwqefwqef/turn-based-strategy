# Turn Based Strategy

Unity SRPG project.

## Requirements

- Unity `6000.4.1f1`
- Open this folder as the Unity project root, the folder that contains `Assets/` and `ProjectSettings/`

## Project Layout

- Main gameplay code lives under `Assets/Game/Code`
- Game data lives under `Assets/StreamingAssets/Game`
- Publication and audit notes live under `Assets/Docs/Publication`

## Opening The Project

1. Open the project in Unity `6000.4.1f1`.
2. Load `Assets/Scenes/test.unity`.
3. Press Play to run the current test battle scene.

This project is intended to run without `Assets/TBS Framework` or any separately requested vendor package.

## Build / Verification

Quick compile check:

```powershell
dotnet build com.windy.srpg.game.csproj
```

## Publication Notes

- The active codebase builds without `Assets/TBS Framework`
- Publication audit report: `Assets/Docs/Publication/PHASE12_AUDIT_REPORT.md`
- Phase 13 packaging checklist: `Assets/Docs/Publication/PHASE13_PACKAGING_CHECKLIST.md`

## License

This project is released under [The Unlicense](LICENSE).
