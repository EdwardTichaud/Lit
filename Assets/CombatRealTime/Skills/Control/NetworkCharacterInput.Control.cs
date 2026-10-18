using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class NetworkCharacterInput
{
    private readonly Dictionary<SkillSO, float> controlReadyAt = new Dictionary<SkillSO, float>();
    private float controlPendingUntil;
    private bool controlPending;
    private static SkillSO LoadControl(string asset) => string.IsNullOrEmpty(asset) || asset.Length > 128 || asset.Contains("/") || asset.Contains("\\") ?
        null : Resources.Load<SkillSO>("CombatControlSkills/" + asset);
    public bool TryRequestControlSkill(SkillSO skill, ICombatTarget enemy, Vector3 direction)
    {
        var runtime = GetComponent<Lit.Combat.Migration.Network.CombatNetworkActorRuntime>();
        if (runtime != null) return runtime.TryRequestSkill(skill, enemy?.Root != null ? enemy.Root.GetComponentInParent<NetworkObject>() : null, direction);
        if (!IsSpawned || !IsOwner || controlPending && Time.unscaledTime < controlPendingUntil || skill == null || !skill.IsControlSkill || LoadControl(skill.name) != skill) return false;
        NetworkObjectReference target = default;
        if (skill.control.kind != SkillControlKind.PasDeRecul)
        {
            var identity = enemy?.Root?.GetComponentInParent<NetworkObject>();
            if (identity == null || !identity.IsSpawned) return false;
            target = identity;
        }
        controlPending = true; controlPendingUntil = Time.unscaledTime + 5;
        UseControlSkillServerRpc(skill.name, target, direction);
        return true;
    }
    [ServerRpc]
    private void UseControlSkillServerRpc(string asset, NetworkObjectReference enemy, Vector3 direction)
    {
        var skill = LoadControl(asset);
        bool accepted = false;
        if (GetComponent<Lit.Combat.Migration.Network.CombatNetworkActorRuntime>() == null &&
            skill != null && skill.IsControlSkill && controller != null && controller.CharacterData != null &&
            controller.CharacterData.combatSkills.Contains(skill) && SkillHealing.CanCast(controller) &&
            (!controlReadyAt.TryGetValue(skill, out float ready) || Time.time >= ready) &&
            !float.IsNaN(direction.x) && !float.IsNaN(direction.y) && !float.IsNaN(direction.z) &&
            !float.IsInfinity(direction.x) && !float.IsInfinity(direction.y) && !float.IsInfinity(direction.z))
        {
            var effects = PlayerControlSkills.Get(controller);
            var target = enemy.TryGet(out var identity) ? ResolveProtectionEnemy(identity) : null;
            accepted = effects.TryActivate(skill, target, Vector3.ProjectOnPlane(direction, Vector3.up).normalized);
            if (accepted)
            {
                controlReadyAt[skill] = Time.time + skill.cooldownSeconds;
                if (skill.control.kind == SkillControlKind.Revelation)
                    ControlEffectClientRpc(asset, enemy, Vector3.zero, target.Root.GetComponent<EnemyRevelation>().Generation);
                else if (skill.control.kind == SkillControlKind.Entrave)
                {
                    EnemyEntraveZone newest = null;
                    foreach (var zone in FindObjectsByType<EnemyEntraveZone>(FindObjectsInactive.Exclude))
                        if (zone.Owner == controller && (newest == null || zone.Generation > newest.Generation)) newest = zone;
                    ControlEffectClientRpc(asset, enemy, newest.transform.position, newest.Generation);
                }
                else ControlEffectClientRpc(asset, default, Vector3.zero, 0);
            }
        }
        ControlResultClientRpc(asset, accepted, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } });
    }
    [ClientRpc]
    private void ControlEffectClientRpc(string asset, NetworkObjectReference enemy, Vector3 point, ulong generation)
    {
        if (IsServer || controller == null) return;
        var skill = LoadControl(asset); if (skill == null) return;
        if (skill.control.kind == SkillControlKind.PasDeRecul) { PlayerControlSkills.Get(controller).PlayRemoteMove(skill); return; }
        if (skill.control.kind == SkillControlKind.Entrave) { EnemyEntraveZone.Create(controller, point, skill, generation); return; }
        if (!enemy.TryGet(out var identity)) return;
        var target = ResolveProtectionEnemy(identity); if (target?.Root == null) return;
        var mark = target.Root.GetComponent<EnemyRevelation>();
        if (mark == null) mark = target.Root.gameObject.AddComponent<EnemyRevelation>();
        mark.Apply(controller, skill.control.durationSeconds, skill.control.strength, generation);
    }
    [ClientRpc]
    private void ControlResultClientRpc(string asset, bool accepted, ClientRpcParams parameters = default)
    {
        controlPending = false;
        var skill = LoadControl(asset);
        if (accepted && skill != null && controller != null) RealTimeCombatManager.Instance?.ConfirmHealingSkill(skill, controller.transform, skill.cooldownSeconds);
    }
    public void ClearControlEffectsForCombatEnd()
    {
        if (IsSpawned && IsOwner) ClearControlEffectsServerRpc();
    }
    [ServerRpc]
    private void ClearControlEffectsServerRpc()
    {
        controller?.GetComponent<PlayerControlSkills>()?.ClearCombatEffects();
        ClearControlEffectsClientRpc();
    }
    [ClientRpc]
    private void ClearControlEffectsClientRpc() => controller?.GetComponent<PlayerControlSkills>()?.ClearCombatEffects();
    private void ClearControlResources()
    {
        controlPending = false; controlReadyAt.Clear();
        controller?.GetComponent<PlayerControlSkills>()?.AbortOwnedMovement();
        controller?.GetComponent<PlayerControlSkills>()?.ClearCombatEffects();
    }
}
