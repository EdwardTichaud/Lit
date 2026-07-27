# Combat Real Time

This is the active combat prototype used by `GameplaySessionRoot` and the
Juggernaut. The former turn-based combat remains archived in `Assets/Combat`.

## Scene setup

1. Add `RealTimeCombatManager`, `RealTimeCombatInput` and
   `CombatLockOnCameraController` to `BattleManager`. Assign
   `RealTimeCombat.inputactions` to the input component. Camera drivers are
   resolved automatically when their standard project components are present.
2. Create a `CombatAttackLibrary` asset containing the large attack pool. Add
   `RealTimeCombatLoadout` below Lucian and fill its eight slots with
   `CombatAttackDefinition` assets. Add the manager's `RealTimeCombatInput`
   reference in the Inspector. `RealTimeCombatLoadoutPanel.EquipLibraryAttack`
   is the button callback for equipping a library entry into the selected slot.
3. Add `RealTimeCombatEnemy` and `EnemySkills` to an enemy. Assign its
   `CombatHealth`, Animator and `SkillSO` list on `EnemySkills`. Enemy-only
   range, damage multiplier and reaction data are configured on each `SkillSO`.
   Add an optional `EnemyLockPoint` child; without it, the enemy root is used.
   Combat never starts from a trigger.
4. Add `RealTimeCombatAnimationEvents` to the animated enemy. Its attack clips
   must call, in order: `ShowReactionPrompt`, `ResolveEnemyAttackImpact`, then
   `EndEnemyAttack`. No timer fallback applies damage.
5. Put `RealTimeCombatReactionPrompt` on an existing world-space prompt UI and
   assign its TMP child. Put `RealTimeCombatHud` and
   `RealTimeCombatLoadoutPanel` on existing UI roots. Use
   `KnowledgeCombatPanel` for the permanent knowledge bonuses.

## Inputs

Left Shoulder locks the nearest valid enemy within the manager's `lockRange`;
pressing it again unlocks and returns to exploration. `Space`/south dodges,
`Q`/west counters and `E`/north jumps. Left Shift/right shoulder opens the
palette, arrows/D-pad pick one of eight slots and Enter/east confirms it. The
normal player map stays on for free movement.
