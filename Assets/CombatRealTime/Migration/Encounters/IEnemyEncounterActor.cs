using System.Collections;
using UnityEngine;

/// <summary>Encounter policy and presentation, independent of the actor's combat brain.</summary>
public interface IEnemyEncounterActor
{
    Transform EncounterRoot { get; }
    ICombatTarget EncounterTarget { get; }
    CharacterInfo Health { get; }
    // Authored activation, introduction and flame gates; not temporary boss suspension.
    bool IsEncounterAvailable { get; }
    bool IsFlameDormant { get; }
    bool IsSuspended { get; }
    CombatTimeDomain TimeDomain { get; }
    IBossEncounterBehaviour BossEncounter { get; }
    void SetSuspended(bool value);
}

/// <summary>Optional authored last-word lifecycle; ordinary actors need not invent one.</summary>
public interface IEnemyEncounterDeathPresentation
{
    bool HasDeathPresentation { get; }
    bool IsDeathPresentationPlaying { get; }
    IEnumerator PlayDeathPresentation();
    void CancelDeathPresentation();
}
