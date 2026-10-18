using Lit.Combat.Migration.Network;
using Unity.Netcode;
using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private bool TryRequestNetworkSkill(SkillSO skill, PlayerAttackContext context)
    {
        ResolvePlayerReferences();
        if (skill == null || playerController == null || IsPlayerDead()) return false;
        var runtime = playerController.GetComponent<CombatNetworkActorRuntime>();
        if (runtime == null || !runtime.IsSpawned || !runtime.IsOwner)
            return RefuseSkillTargeting("Acteur de combat reseau non enregistre.");
        Transform target = context?.Ally != null ? context.Ally.transform : context?.TargetRoot;
        if (context == null) target = skill.targetingMode == SkillTargetingMode.Self ? playerRoot : LockedCombatTarget?.Root;
        if (skill.targetingMode == SkillTargetingMode.FreeAttack || skill.targetingMode == SkillTargetingMode.CasterArea ||
            skill.targetingMode == SkillTargetingMode.AllyArea) target = null;
        var identity = target != null ? target.GetComponentInParent<NetworkObject>() : null;
        if (target != null && (identity == null || !identity.IsSpawned))
            return RefuseSkillTargeting("Cible absente du monde reseau courant.");
        Vector3 direction = context != null ? context.Direction : playerRoot.forward;
        if (!runtime.TryRequestSkill(skill, identity, direction))
            return RefuseSkillTargeting(runtime.LastDiagnostic ?? "Commande de combat reseau indisponible.");
        return true;
    }
}
