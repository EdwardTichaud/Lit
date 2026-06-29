using System.Collections.Generic;
using UnityEngine;

public enum CombatStateEffect
{
    None,
    Sleep,
    DamageRedirect,
    SharedTargetFocus,
    ForcedGrounded,
    ForcedAirborne,
    ForcedAutoAttack,
    InterceptionImmunity
}

public enum CombatStateOverrideTrigger
{
    None,
    TakeDamage,
    ReceiveHeal,
    TurnCompleted,
    DurationExpired,
    Cleanse,
    ProtectorDefeated,
    CasterTurnBegan,
    CasterDefeated,
    TargetDefeated,
    ManualRemoval,
    CustomScript
}

/// <summary>
/// Décrit un état de combat générique (stun, silence, etc.) et les conditions
/// permettant d'en sortir. Les designers peuvent créer un asset par état
/// afin de documenter facilement ses effets et ses triggers de sortie.
/// </summary>
[CreateAssetMenu(fileName = "CombatState", menuName = "Symphonie/Combat State")]
public class CombatStateSO : ScriptableObject
{
[Header("Informations générales")]
[Tooltip("Nom de l'état tel qu'il apparaîtra dans les outils de design.")]
public string stateName;

[Header("Effets appliqués à la cible")]
[Tooltip("Liste des effets intrinsèques conférés par cet état.")]
public List<CombatStateEffect> stateEffects = new();

    [Header("Conditions de sortie")]
    [Tooltip("Evénements qui annulent cet état (réception de soin, tour écoulé, purge...).")]
    public List<CombatStateOverrideTrigger> overriddenEffects = new();
}
