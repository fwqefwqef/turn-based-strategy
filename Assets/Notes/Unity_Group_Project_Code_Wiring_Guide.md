# Unity Group Project Code Wiring Guide

This note is a practical checklist for working on this project in a Unity team setting. The goal is to make code changes without accidentally breaking scene wiring, prefab references, save data, or another person's work.

## The Big Rule

Unity scenes and prefabs store references by serialized field name, script GUID, and object reference. Code is not the only source of truth.

Before changing a MonoBehaviour field, ask:

- Is this assigned in the Inspector?
- Is this object expected to exist in the scene?
- Is this object allowed to be created by code?
- Will renaming or deleting this field break scene wiring?

## Scene Wiring Vs Code Creation

There are two different patterns:

### Manual scene wiring

Use this for visible UI, authored panels, templates, buttons, scroll views, and designer-controlled layout.

Example:

```csharp
[SerializeField] private BattleResultUI battleResultUi;
[SerializeField] private TMP_Text resultText;
[SerializeField] private Button exitButton;
```

The scene should contain the objects, and code should only show, hide, populate, or subscribe to them.

Good for:

- Pre-battle UI panels
- Inventory/passive management lists
- Result UI
- Level-up UI
- Action/skill/item/trade menus

Avoid creating these from code unless the feature is intentionally runtime-generated.

### Code-created infrastructure

Use this for invisible helpers that do not need authored layout.

Example:

```csharp
gameplayInputController = GetComponent<GameplayInputController>() ?? gameObject.AddComponent<GameplayInputController>();
```

Good for:

- Input routing components
- Button text fitting helpers
- Non-visual services attached to a known object

Do not use this for UI panels that need manually arranged children.

## Active And Inactive Objects

`FindAnyObjectByType<T>()` only finds active objects.

If the component itself is on an active GameObject, this is fine:

```csharp
battleResultUi = FindAnyObjectByType<BattleResultUI>();
```

If the component is on an inactive panel, this will not find it. In that case either:

- Keep the component on an active controller object and assign its inactive `panelRoot`, or
- Use an inactive-object search intentionally:

```csharp
FindObjectsByType<BattleResultUI>(FindObjectsInactive.Include)
```

Preferred pattern for UI panels in this project: keep the controller object active, and assign an inactive panel root.

## Awake, Start, Initialize

Use each for a different job.

### Awake

Use `Awake` for local setup:

- Cache local components.
- Hide panels.
- Subscribe button `onClick` handlers.
- Configure modal roots.

Do not assume every other object has finished its own `Awake`.

### Start

Use `Start` when the object can wait one frame for scene setup.

Good for:

- Kicking off scene flow after all `Awake` calls.
- Starting battle initialization.

### Initialize(...)

Use explicit `Initialize(...)` when one system owns another system's setup.

Example:

```csharp
preBattleUiController.Initialize(CellGrid);
gameplayInputController.Initialize(CellGrid);
```

This is useful when a controller needs a specific dependency and should not hunt for it repeatedly.

## Inspector Fields

Use `[SerializeField] private` for fields assigned in Unity:

```csharp
[SerializeField] private Button saveButton;
```

Prefer this over public fields unless another class really needs access.

Good:

```csharp
[SerializeField] private TMP_Text statusText;
```

Avoid:

```csharp
public TMP_Text StatusText;
```

Public fields make it unclear whether outside code should mutate the value.

## Renaming Serialized Fields

Renaming a `[SerializeField]` field can break scene and prefab references.

If you rename a serialized field and need existing Unity wiring to survive, use:

```csharp
[FormerlySerializedAs("oldFieldName")]
[SerializeField] private Button newFieldName;
```

After the scene has been opened and re-saved, the marker can usually be removed later, but do not remove it casually during the same refactor.

High-risk changes:

- Renaming serialized fields.
- Changing field type, such as `GameObject` to `RectTransform`.
- Moving a script to another assembly.
- Deleting a MonoBehaviour script.

## Button Wiring

For UI buttons, subscribe in code if the behavior belongs to the script:

```csharp
private void Awake()
{
    exitButton.onClick.AddListener(Exit);
}
```

If a button is optional, null-check it:

```csharp
if (exitButton != null)
{
    exitButton.onClick.AddListener(Exit);
}
```

If you add listeners in `OnEnable`, remove them in `OnDisable`. If you add them once in `Awake`, normally you do not need to remove them unless the object lives across scene reloads.

## Panel Visibility

For manual UI panels, use one root object:

```csharp
[SerializeField] private GameObject panelRoot;
```

Show/hide only the root:

```csharp
panelRoot.SetActive(true);
panelRoot.SetActive(false);
```

If the script is attached to the panel itself, disabling the panel disables the script too. That can prevent `Awake`, `Start`, or `Show` from being called by another object.

Safer pattern:

- Active object: `Battle Result UI Controller`
- Inactive child: `Panel Root`
- Controller has fields for `Panel Root`, `Result Text`, and `Exit Button`

## Scroll Lists And Templates

For scrollable lists in this project, prefer a button template under the ScrollView content object.

Scene setup:

- `Scroll View`
- `Viewport`
- `Content`
- `Button Template`

Code pattern:

```csharp
Button row = Instantiate(rowTemplate, content);
row.gameObject.SetActive(true);
```

Keep the template disabled or hidden at runtime. When rebuilding the list, delete generated children but preserve the template.

Do not manually position every row if the content has a `VerticalLayoutGroup`. Let layout components do layout.

If buttons are tiny, check:

- Template preferred height.
- `LayoutElement`.
- `VerticalLayoutGroup` child force expand settings.
- `ContentSizeFitter`.
- ScrollRect `Content` reference.

## Scene Lookup Helpers

Use scene lookup sparingly. It is convenient, but it hides dependencies.

Acceptable:

```csharp
cellGrid = FindAnyObjectByType<CellGrid>();
```

Better when possible:

```csharp
[SerializeField] private CellGrid cellGrid;
```

Best for important UI:

- Assign it manually in the Inspector.
- Use scene lookup only as a fallback.

Avoid doing scene lookups every frame. Cache the result.

## Events

Subscribe and unsubscribe symmetrically.

```csharp
private void Awake()
{
    CellGrid.BattleEnded += OnBattleEnded;
}

private void OnDestroy()
{
    CellGrid.BattleEnded -= OnBattleEnded;
}
```

Always unsubscribe from long-lived objects, especially scene managers, static events, and global UI events.

### Event ownership in plain English

An event is not a bool. It is closer to a list of methods that should be called when something happens.

The object that owns the event is the publisher:

```csharp
public event EventHandler<BattleEndedEventArgs> BattleEnded;
```

In this project, `CellGrid` owns `BattleEnded`. Other scripts can subscribe to it or unsubscribe from it, but outside code cannot directly broadcast it.

The method being added is the subscriber callback:

```csharp
CellGrid.BattleEnded += OnGameEnded;
```

This does not mean `BattleEnded` subscribes to `OnGameEnded`. It means `OnGameEnded` is added to `BattleEnded`'s call list. When `CellGrid` later broadcasts `BattleEnded`, Unity/C# calls every subscribed method.

The matching unsubscribe removes that method from the call list:

```csharp
CellGrid.BattleEnded -= OnGameEnded;
```

Without unsubscribing, a destroyed object can leave behind a stale callback. That can cause duplicate behavior, null reference errors, or callbacks firing on objects that should be gone.

### BattleEnded flow in this project

The battle result chain currently works like this:

1. `CellGrid` detects that the battle has an outcome.
2. `CellGrid.Scene.cs` calls `TryApplyBattleOutcome(...)`.
3. `TryApplyBattleOutcome(...)` broadcasts the internal scene event:

```csharp
SceneGameEnded?.Invoke(this, new BattleEndedEventArgs(winningPlayers, losingPlayers));
```

4. `CellGrid` has already wired that internal event to `OnSceneGameEnded(...)`.
5. `OnSceneGameEnded(...)` broadcasts the public event:

```csharp
private void OnSceneGameEnded(object sender, BattleEndedEventArgs e)
{
    BattleEnded?.Invoke(this, e);
}
```

6. Any listener subscribed to `CellGrid.BattleEnded` runs its callback.

For the result UI, `GUIController` subscribes in `Awake`:

```csharp
CellGrid.BattleEnded += OnGameEnded;
```

Then `GUIController.OnGameEnded(...)` runs when the event is broadcast:

```csharp
private void OnGameEnded(object sender, BattleEndedEventArgs e)
{
    if (EndTurnButton != null)
    {
        EndTurnButton.interactable = false;
    }

    if (e?.WinningPlayerNumbers?.Contains(0) == true)
    {
        CellGrid.SaveVictoryProgress();
    }

    battleResultUi?.Show(e, ExitScene);
}
```

The `sender` is the object broadcasting the event, usually `this`. The `e` argument is the event data. For `BattleEnded`, the data says which player numbers won and lost.

The `?.Invoke(...)` syntax means "if anyone is subscribed, call them." If nobody is listening, nothing happens.

## Save Data Boundaries

Do not store derived values in the save file unless the player can intentionally change them.

Good save data:

- Unit ID
- Level
- EXP
- Base stats
- Inventory item IDs
- Skill IDs
- Passive IDs
- Storage contents

Avoid save data like:

- Current max HP
- Current max MP
- Stats after equipment/passive modifiers

Those should be derived when loading from:

- Base stats
- Equipment
- Accessories
- Passives
- Level

In this project, victory saves unit progress through `CellGrid.SaveVictoryProgress()`, which captures current friendly units into the campaign save.

## Runtime State Vs Save State

Runtime state can include temporary values:

- Current HP during battle
- Current MP during battle
- Movement left this turn
- Buff duration
- Pending move
- Current selected unit

Campaign save state should be stable between scenes:

- Unit progression
- Inventory/passive loadout
- Deployment roster
- Shared storage

If a value resets when entering a battle, it probably does not belong in campaign save data.

## Prefabs And Scene Objects

When adding code that expects components, decide where those components live:

- Unit behavior belongs on unit prefab or unit children.
- Grid flow belongs on `CellGrid`.
- Scene UI belongs under Canvas.
- Input bridge belongs near `GUIController`.

Do not silently add gameplay-critical visible components in code unless the team agrees.

## Safe Refactor Checklist

Before changing a script used in a scene:

1. Search for the field or class name.
2. Check whether it is `[SerializeField]`.
3. If renaming, add `[FormerlySerializedAs]`.
4. If changing a type, expect manual scene rewiring.
5. Run a compile check.
6. Open the scene and look for missing script/reference warnings.
7. Test the UI path that uses the object.

## Common Unity Gotchas

### Disabled object scripts do not run

If a script is on an inactive GameObject, Unity will not call `Awake` until it becomes active.

### Missing references can fail silently

If code uses null propagation:

```csharp
battleResultUi?.Show(...);
```

Nothing happens if the reference is missing. Useful for optional features, dangerous for required UI.

### Generated project files can be noisy

Unity can rewrite `.csproj`, package lock, or project settings files during compile. Treat those carefully in source control.

### Scene changes are easy to forget

If you add a serialized field, code may compile but the scene still needs to be wired.

## Team Habits That Help

- Keep visual UI manually wired unless there is a strong reason not to.
- Keep helper components auto-added only when they have no scene layout.
- Name scene objects similarly to serialized fields.
- Prefer one controller script per UI panel.
- Keep save files free of derived values.
- Commit code and scene changes together when wiring changes are required.
- Mention required scene wiring in PR notes or commit messages.

## Project-Specific Examples

### Battle result UI

Manual scene wiring:

- Active controller object with `BattleResultUI`.
- Assign `Panel Root`.
- Assign `Result Text`.
- Assign `Exit Button`.
- Assign `GUIController > Battle Result Ui`, or leave it discoverable if the controller object is active.

### Pre-battle inventory/passive UI

Manual scene wiring:

- Assign panel roots.
- Assign back/action/filter buttons.
- Assign ScrollView content containers.
- Assign row templates.

The code should populate template rows, not create whole layouts from scratch.

### Victory save

Victory should happen after combat, EXP, and manual level-up stat selection finish.

The result UI should appear after the save has captured updated unit progression.
