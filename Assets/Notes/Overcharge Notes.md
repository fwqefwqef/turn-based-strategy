# Overcharge Notes

## Design Rules

- Overcharge is available from the Action Menu.
- Activating Overcharge does not consume the unit's action and does not commit or lock in pending movement.
- Overcharge activation remains pending until the unit commits an action or pending movement.
- Canceling from the Action Menu removes a pending Overcharge activation before normal movement cancellation occurs.
- An eligible unit may activate Overcharge only once per battle.
- Overcharge lasts for three of that unit's turns, including the activation turn.
- Each eligible character has a character-specific Overcharge passive and ultimate.
- The ultimate is available only while Overcharge is active.
- Successfully using the ultimate ends Overcharge immediately, regardless of the number of turns remaining.
- Only player-controlled units whose stable Unit ID has an Overcharge definition may activate it. Enemies cannot use Overcharge.

## Activation Flow

Selecting **Overcharge** creates a pending activation and:

1. Applies the character's Overcharge passive.
2. Sets the duration to three turns.
3. Temporarily inserts the character's ultimate into the available skill list.
4. Leaves the Action Menu open so the unit may still attack, use a skill or item, trade, or wait.
5. Leaves pending movement uncommitted, so the existing movement cancellation behavior remains available.

After activation, the Overcharge button is hidden. Canceling nested targeting returns to the Action Menu without removing Overcharge. Canceling from the Action Menu removes the pending activation and remains in the menu; a subsequent Cancel performs the normal pending-movement rollback. Once an action is committed, Overcharge becomes non-cancelable and consumes its once-per-battle use.

## Runtime State

Suggested states:

```csharp
public enum OverchargeState
{
    Unavailable,
    Ready,
    PendingActivation,
    Active,
    Spent
}
```

Suggested runtime values:

```csharp
OverchargeState State;
int TurnsRemaining;
```

`Spent` is not reset at turn start. It resets only when the unit is initialized for a new battle. Overcharge state is battle-local and should not permanently alter campaign progression or learned skills.

## Eligibility and Definitions

Use a central `OverchargeCatalog` or equivalent data asset keyed by stable Unit ID:

```csharp
[Serializable]
public class OverchargeDefinition
{
    public string UnitId;
    public string PassiveId;
    public string UltimateSkillId;
    public int DurationTurns = 3;
}
```

Activation requires both an eligible ID and player ownership:

```csharp
unit.PlayerNumber == 0
    && OverchargeCatalog.TryGet(unit.UnitID, out OverchargeDefinition definition)
```

If `PlayerNumber == 0` may later include allied NPCs, replace that check with an explicit player-roster or player-controlled flag. Do not use prefab names for eligibility because prefab names are not stable gameplay identities.

## Passive Behavior

The Overcharge passive is allowed to modify movement range as well as combat properties.

Because activation currently happens after movement selection, a movement bonus does not recalculate or reopen movement during the activation turn. It affects movement beginning on the unit's next turn. Reopening movement selection may be added later if desired.

Apply the passive with an Overcharge-specific runtime source and remove only effects from that source when Overcharge ends. This prevents removal of an identical passive supplied by equipment or another system.

If Overcharge needs to appear in the buff/debuff panel, it may also provide a display status containing its remaining duration. Mechanical behavior should still come from the character-specific passive.

## Ultimate Skill

The ultimate should be a temporary runtime-granted skill, not a learned skill:

```text
Learned skills
Equipment-granted skills
Overcharge-granted ultimate
        -> Combined available skill list
```

Insert the ultimate only while Overcharge is active. Remove the Overcharge-granted entry when Overcharge expires or the ultimate is used. Source-aware skill grants should prevent duplication and avoid changing saved character progression.

End Overcharge only after the ultimate has passed validation and its use has been successfully committed. Canceling target selection, failing target validation, or lacking MP must not consume or end Overcharge.

The ultimate consumes the unit's action by default. It may use the existing non-ending skill behavior only when deliberately configured to do so; such a skill remains subject to the existing once-per-turn limit.

## Duration

The activation turn counts as turn one. Initialize `TurnsRemaining` to 3 and decrement when that unit finishes a turn:

```text
Activation turn ends -> 2 turns remaining
Next turn ends       -> 1 turn remaining
Third turn ends      -> 0; Overcharge expires
```

Waiting and turns skipped by a debuff still count toward the duration. Successfully using the ultimate ends Overcharge immediately instead of waiting for the end-of-turn countdown.

## Ending Overcharge

Overcharge ends when either:

- `TurnsRemaining` reaches zero, or
- the unit successfully uses its ultimate.

Ending Overcharge must:

1. Remove the Overcharge-sourced passive.
2. Remove the Overcharge-granted ultimate.
3. Remove or update its display status.
4. Set the runtime state to `Spent`.
5. Recalculate derived statistics, including movement, if the passive modified them.

## Action Menu Presentation

- `Ready`: show **Overcharge**.
- `Active`: hide the button and show remaining duration through the unit status UI.
- `Spent` or `Unavailable`: hide the button.

Selecting the button applies the passive and grants the ultimate immediately so previews are accurate. The activation remains reversible until an action is committed. Pending and committed Overcharge scale the character sprite to 110% of its normal size. Ending or canceling Overcharge restores the exact original sprite scale without changing board occupancy or world-space UI.
