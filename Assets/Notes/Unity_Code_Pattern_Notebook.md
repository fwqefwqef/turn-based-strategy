# Unity Code Pattern Notebook

This is a small starter notebook for reading and writing Unity code in this project. It is meant to be practical: common patterns, what they mean, when to use them, and what to watch for.

## GameObject Vs Component

A `GameObject` is the object in the Unity Hierarchy.

A `Component` is something attached to a `GameObject`.

Examples of components:

- `Transform`
- `RectTransform`
- `Image`
- `Button`
- `CellGrid`
- `GUIController`
- `PreBattleUIController`

Your scripts become components when they inherit from `MonoBehaviour`.

```csharp
public class GUIController : MonoBehaviour
{
}
```

Mental model:

```text
GameObject = container
Component = behavior or data attached to the container
MonoBehaviour = your custom script component
```

## Inspector References

Use `[SerializeField] private` when Unity should assign the value in the Inspector.

```csharp
[SerializeField] private Button saveButton;
[SerializeField] private TMP_Text titleText;
[SerializeField] private GameObject panelRoot;
```

This means:

- The field is private to code.
- Unity can still show it in the Inspector.
- A designer or programmer can drag the scene object into the slot.

Use this for visible UI, authored scene objects, templates, buttons, and panel roots.

## Public Fields

Public fields also appear in the Inspector.

```csharp
public CellGrid CellGrid;
```

They are simple, but they also allow other code to change them freely. Prefer `[SerializeField] private` unless another script really needs direct access.

Better default:

```csharp
[SerializeField] private CellGrid cellGrid;
```

## Awake

`Awake` runs when Unity creates the component instance.

Use it for local setup:

- Cache components.
- Hide UI panels.
- Subscribe button clicks.
- Subscribe to scene events if the required object is already available.

Example:

```csharp
private void Awake()
{
    closeButton.onClick.AddListener(Hide);
    panelRoot.SetActive(false);
}
```

Do not assume every other object has finished its own setup in `Awake`.

## Start

`Start` runs after all active objects have had `Awake` called.

Use it when your script can wait until the scene is more fully initialized.

```csharp
private void Start()
{
    RefreshDisplay();
}
```

## Initialize

Use `Initialize(...)` when another script should provide this script's dependencies.

```csharp
public void Initialize(CellGrid grid)
{
    cellGrid = grid;
    RefreshDisplay();
}
```

Then another controller can call:

```csharp
preBattleUiController.Initialize(CellGrid);
```

This is useful when a script needs a specific object, such as the scene's `CellGrid`, and you do not want it searching for that object repeatedly.

## GetComponent

`GetComponent<T>()` searches the same `GameObject` for a component.

```csharp
private Button button;

private void Awake()
{
    button = GetComponent<Button>();
}
```

Use this when the component should be on the same object.

Example hierarchy:

```text
Save Button
- RectTransform
- Image
- Button
- SaveButtonView
```

Inside `SaveButtonView`, this makes sense:

```csharp
Button button = GetComponent<Button>();
```

## AddComponent

`AddComponent<T>()` adds a component to a `GameObject` at runtime.

```csharp
gameObject.AddComponent<GameplayInputController>();
```

Use this mostly for invisible helper components.

Good:

- Input coordinator
- Text overflow fitter
- Debug helper

Avoid it for authored UI panels, because those usually need manual scene layout.

## FindAnyObjectByType

`FindAnyObjectByType<T>()` searches the active scene for an active component.

```csharp
CellGrid grid = FindAnyObjectByType<CellGrid>();
```

Use it as a fallback, not as your main plan.

Common pattern:

```csharp
if (cellGrid == null)
{
    cellGrid = FindAnyObjectByType<CellGrid>();
}
```

Important: this does not find inactive objects.

## Active And Inactive Objects

If a `GameObject` is inactive, many scene searches will not find it.

This matters for UI panels.

Safer UI pattern:

```text
Battle Result UI Controller   active
- Panel Root                  inactive at start
  - Result Text
  - Exit Button
```

The controller script stays active, but the visible panel can be hidden.

## Show And Hide Panels

Use one root object for a UI panel.

```csharp
[SerializeField] private GameObject panelRoot;

public void Show()
{
    panelRoot.SetActive(true);
}

public void Hide()
{
    panelRoot.SetActive(false);
}
```

Do not hide every child one by one unless there is a specific reason.

## Buttons

Buttons use `onClick` listeners.

```csharp
private void Awake()
{
    saveButton.onClick.AddListener(OnSaveClicked);
}

private void OnSaveClicked()
{
    Save();
}
```

If the button reference is optional, null-check it:

```csharp
if (saveButton != null)
{
    saveButton.onClick.AddListener(OnSaveClicked);
}
```

If you add listeners in `OnEnable`, remove them in `OnDisable`.

```csharp
private void OnEnable()
{
    saveButton.onClick.AddListener(OnSaveClicked);
}

private void OnDisable()
{
    saveButton.onClick.RemoveListener(OnSaveClicked);
}
```

## Events

An event is a list of methods to call when something happens.

The owner publishes the event:

```csharp
public event EventHandler BattleStarted;
```

Other scripts subscribe:

```csharp
cellGrid.BattleStarted += OnBattleStarted;
```

The owner broadcasts:

```csharp
BattleStarted?.Invoke(this, EventArgs.Empty);
```

Other scripts unsubscribe:

```csharp
cellGrid.BattleStarted -= OnBattleStarted;
```

The callback must match the event shape:

```csharp
private void OnBattleStarted(object sender, EventArgs e)
{
}
```

Use events when one system announces something, and several other systems may want to react.

## Event Arguments

Use event arguments when the listener needs extra information.

```csharp
public event EventHandler<BattleEndedEventArgs> BattleEnded;
```

Then the callback receives the data:

```csharp
private void OnGameEnded(object sender, BattleEndedEventArgs e)
{
    if (e.WinningPlayerNumbers.Contains(0))
    {
        SaveVictoryProgress();
    }
}
```

## Null Checks

Null means "this reference does not point to an object."

Common guard:

```csharp
if (cellGrid == null)
{
    enabled = false;
    return;
}
```

This prevents later code from crashing.

Null propagation:

```csharp
battleResultUi?.Show(args, ExitScene);
```

This means:

```text
If battleResultUi is not null, call Show.
If it is null, do nothing.
```

This is useful for optional features, but dangerous if the UI is required. It can hide a missing reference.

## Lists

Lists are common for units, items, skills, passives, and generated UI rows.

```csharp
private readonly List<Button> generatedButtons = new List<Button>();
```

Add:

```csharp
generatedButtons.Add(button);
```

Loop:

```csharp
foreach (Button button in generatedButtons)
{
    button.interactable = false;
}
```

Clear:

```csharp
generatedButtons.Clear();
```

## Rebuilding A UI List

Common template pattern:

```csharp
[SerializeField] private Transform contentRoot;
[SerializeField] private Button buttonTemplate;

private readonly List<Button> generatedButtons = new List<Button>();

private void RebuildList(IReadOnlyList<string> names)
{
    foreach (Button button in generatedButtons)
    {
        Destroy(button.gameObject);
    }

    generatedButtons.Clear();

    foreach (string name in names)
    {
        Button button = Instantiate(buttonTemplate, contentRoot);
        button.gameObject.SetActive(true);
        button.GetComponentInChildren<TMP_Text>().text = name;
        generatedButtons.Add(button);
    }
}
```

Scene setup:

```text
Scroll View
- Viewport
  - Content
    - Button Template
```

Usually the template starts inactive.

## Lambdas In Buttons

When creating generated buttons, you often need the button to remember which item it represents.

```csharp
foreach (InventoryItem item in items)
{
    InventoryItem capturedItem = item;
    button.onClick.AddListener(() => SelectItem(capturedItem));
}
```

The local `capturedItem` makes the intent clear.

## TextMeshPro

This project uses TextMeshPro for most UI text.

```csharp
using TMPro;

[SerializeField] private TMP_Text nameText;
```

Set text:

```csharp
nameText.text = unit.DisplayName;
```

Prefer `TMP_Text` over old Unity `Text`.

## Namespaces

Folders do not decide whether a script needs `using`.

Namespaces do.

Same namespace:

```csharp
namespace Windy.Srpg.Game.UI
{
    public class GUIController : MonoBehaviour
    {
        private GameplayInputController inputController;
    }
}
```

No extra `using` needed.

Different namespace:

```csharp
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.UI
{
    public class GUIController : MonoBehaviour
    {
        private CellGrid cellGrid;
    }
}
```

Use namespaces to keep systems understandable:

- `Windy.Srpg.Game.UI`
- `Windy.Srpg.Game.Grid`
- `Windy.Srpg.Game.Units`
- `Windy.Srpg.Game.Inventory`
- `Windy.Srpg.Game.Passives`

## Serialized Field Renaming

Renaming a serialized field can break Inspector wiring.

Risky:

```csharp
[SerializeField] private Button saveButton;
```

renamed to:

```csharp
[SerializeField] private Button confirmButton;
```

Unity may lose the assigned reference.

Safer during refactor: migrate the scene, prefab, and asset references to the new serialized field name in the same pass, then open the scene and check that the Inspector wiring survived.

## Runtime State Vs Save Data

Runtime state is temporary.

Examples:

- Current HP during battle
- Selected unit
- Highlighted cell
- Button currently selected

Save data is what should persist after closing and loading.

Examples:

- Unit level
- Unit EXP
- Base stats
- Inventory item IDs
- Equipped passive IDs
- Storage contents

Avoid saving values that can be derived from other values.

Example: max MP should usually come from stats, passives, equipment, and level. Do not store it separately unless the player can directly change that exact value.

## ScriptableObjects

ScriptableObjects are asset-based data.

Use them for definitions:

- Unit presets
- Item definitions
- Skill definitions
- Passive definitions

Runtime unit state should usually live on the runtime `Unit`, not on the shared preset asset.

Important mental model:

```text
Preset asset = template data
Runtime object = current battle instance
Save data = persistent player progress
```

## Common UI Controller Shape

A simple UI controller often looks like this:

```csharp
public class ExamplePanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text titleText;

    private void Awake()
    {
        closeButton.onClick.AddListener(Hide);
        Hide();
    }

    public void Show(string title)
    {
        titleText.text = title;
        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }
}
```

This is a good default shape for authored UI panels.

## Common Manager Shape

A manager-style script often looks like this:

```csharp
public class ExampleManager : MonoBehaviour
{
    [SerializeField] private CellGrid cellGrid;

    private void Awake()
    {
        if (cellGrid == null)
        {
            cellGrid = FindAnyObjectByType<CellGrid>();
        }

        if (cellGrid == null)
        {
            enabled = false;
            return;
        }

        cellGrid.BattleStarted += OnBattleStarted;
    }

    private void OnDestroy()
    {
        if (cellGrid != null)
        {
            cellGrid.BattleStarted -= OnBattleStarted;
        }
    }

    private void OnBattleStarted(object sender, EventArgs e)
    {
    }
}
```

## Debugging Checklist

When something does not work, ask:

1. Is the GameObject active?
2. Is the component enabled?
3. Is the serialized field assigned in the Inspector?
4. Is the script on the object I think it is on?
5. Is `Awake`, `Start`, or `Initialize` actually being called?
6. Is the code looking for active objects only?
7. Is an event subscribed before it is broadcast?
8. Is an event unsubscribed too early?
9. Is the button listener attached?
10. Is the UI object hidden behind another object?

## Tiny Practice Exercises

Read one script and write a five-line map:

```text
Script name:
- What object owns it?
- What fields are manually wired?
- What objects does it find or create?
- What events does it subscribe to?
- What public methods can other scripts call?
```

Good scripts to practice with:

- `GUIController`
- `BattleResultUI`
- `PreBattleUIController`
- `GameplayInputController`
- `UnitInspectPanelUI`

## Words To Recognize

`SerializeField`: show private field in Inspector.

`MonoBehaviour`: Unity component script base class.

`GameObject`: scene object container.

`Component`: attached behavior or data.

`GetComponent`: find component on the same GameObject.

`FindAnyObjectByType`: search the active scene.

`Instantiate`: clone a prefab or template.

`Destroy`: remove a runtime object.

`SetActive`: show, hide, enable, or disable a GameObject.

`Awake`: early setup.

`Start`: first-frame setup after Awake.

`OnEnable`: runs when component becomes enabled.

`OnDisable`: runs when component becomes disabled.

`OnDestroy`: cleanup before object is destroyed.

`event`: list of callbacks to run when something happens.

`+=`: subscribe/add.

`-=`: unsubscribe/remove.

`?.`: only call/access if not null.

`namespace`: code grouping, not folder location.
