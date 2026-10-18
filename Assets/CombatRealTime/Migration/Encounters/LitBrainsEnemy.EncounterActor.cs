using UnityEngine;

public partial class LitBrainsEnemy : IEnemyEncounterActor
{
    public Transform EncounterRoot => transform;
    public ICombatTarget EncounterTarget => this;
    public bool IsEncounterAvailable => isActiveAndEnabled && !IsDead && Health != null &&
        EncounterAllowsDamage && !RequiresLegacyEncounterPresentation && HasSupportedEncounterBoss;
    public bool IsSuspended => IsCinematicSuspended || IsFlameDormant;
    CombatTimeDomain IEnemyEncounterActor.TimeDomain => GetComponent<CombatTimeDomain>();
    IBossEncounterBehaviour IEnemyEncounterActor.BossEncounter => GetComponent<BossEncounterBehaviour>();
    void IEnemyEncounterActor.SetSuspended(bool value) => SetCinematicSuspended(value);

    private bool HasSupportedEncounterBoss
    {
        get
        {
            var boss = GetComponent<BossEncounterBehaviour>();
            return boss == null || ReferenceEquals(boss.EncounterActor, this);
        }
    }

    // Introduction authority, knowledge gates and last words have not been ported to Brains.
    // Keep those authored actors on their original encounter instead of silently bypassing it.
    private bool RequiresLegacyEncounterPresentation => RuntimeCharacterData != null &&
        (RuntimeCharacterData.enemyEncounterOptions != null && RuntimeCharacterData.enemyEncounterOptions.startAsGhost ||
            RuntimeCharacterData.enemyDeathOptions != null && RuntimeCharacterData.enemyDeathOptions.enabled);
}
