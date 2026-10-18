using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public sealed partial class CombatNetworkActorRuntime
    {
        public void PublishEnemyImpact(Transform attacker, SkillSO skill, int damage, EnemyAttackOutcome outcome)
        {
            if (!IsServer || !IsSpawned || attacker == null || skill == null ||
                outcome != EnemyAttackOutcome.Damaged && outcome != EnemyAttackOutcome.Guarded) return;
            var identity = attacker.GetComponentInParent<NetworkObject>();
            if (identity == null || !identity.IsSpawned || identity.NetworkManager != NetworkManager) return;
            int index = EnemySkills(identity).IndexOf(skill);
            if (index < 0) { Diagnose("Enemy impact feedback requires the skill in its authored loadout/profile."); return; }
            EnemyImpactFeedbackClientRpc(new NetworkObjectReference(identity), index, Mathf.Max(0, damage), (byte)outcome);
        }

        [ClientRpc]
        private void EnemyImpactFeedbackClientRpc(NetworkObjectReference attacker, int skillIndex, int damage, byte outcome)
        {
            if (IsServer || !attacker.TryGet(out var identity, NetworkManager)) return;
            var skills = EnemySkills(identity);
            if (skillIndex < 0 || skillIndex >= skills.Count || damage < 0 ||
                outcome != (byte)EnemyAttackOutcome.Damaged && outcome != (byte)EnemyAttackOutcome.Guarded) return;
            EnemyController.PlayOutcomeFeedback(skills[skillIndex], transform, (EnemyAttackOutcome)outcome);
            if (damage > 0) CombatDamageWorldFeedback.Show(transform, damage, new Color(1, .48f, .48f), 2.05f);
        }

        private static List<SkillSO> EnemySkills(NetworkObject identity)
        {
            var result = new List<SkillSO>();
            var data = identity != null ? identity.GetComponent<CharacterInfo>()?.SourceData : null;
            if (data == null || !data.isEnemy) return result;
            if (data.combatSkills != null)
                foreach (var skill in data.combatSkills) if (skill != null && !result.Contains(skill)) result.Add(skill);
            if (data.enemyCombatProfile?.patterns != null)
                foreach (var pattern in data.enemyCombatProfile.patterns)
                    if (pattern?.skills != null)
                        foreach (var skill in pattern.skills) if (skill != null && !result.Contains(skill)) result.Add(skill);
            return result;
        }
    }
}
