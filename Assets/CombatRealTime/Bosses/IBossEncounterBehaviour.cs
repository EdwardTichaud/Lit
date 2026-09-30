using UnityEngine;

public interface IBossEncounterBehaviour
{
    BossDefinitionSO Definition { get; }
    bool SuppressDefaultEnemyBrain { get; }
    bool IsBossEngaged { get; }
    bool IsBossResolved { get; }
    int CurrentSegments { get; }
    int MaximumSegments { get; }
    bool FilterIncomingDamage(int incomingDamage, SquadCharacterController source, out int permittedDamage);
}
