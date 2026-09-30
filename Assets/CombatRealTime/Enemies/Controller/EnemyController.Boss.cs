using UnityEngine;

public sealed partial class EnemyController
{
    private BossEncounterBehaviour bossEncounter;
    public IBossEncounterBehaviour BossEncounter => bossEncounter != null ? bossEncounter : GetComponent<BossEncounterBehaviour>();
    public bool IsBossBrainSuppressed => BossEncounter != null && BossEncounter.SuppressDefaultEnemyBrain;

    private void BossAwake()
    {
        bossEncounter = GetComponent<BossEncounterBehaviour>();
        if (bossEncounter != null) SetSuspended(true);
    }

    private bool BossFilterIncomingDamage(int amount, SquadCharacterController source, out int permittedDamage)
    {
        IBossEncounterBehaviour boss = BossEncounter;
        if (boss == null) { permittedDamage = amount; return true; }
        return boss.FilterIncomingDamage(amount, source, out permittedDamage);
    }
}
