using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Opsive.UltimateCharacterController.Character;

/// <summary>Targeted rollout gate. Does not migrate or mutate any asset.</summary>
public sealed class ActorAnimationContractValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => 100;
    public void OnPreprocessBuild(BuildReport report) => Validate();
    [MenuItem("Lit/Animation Presentation/Validate Actor Contract")]
    public static void Validate()
        => ValidateContract(true);
    public static void ValidateTechnical()
        => ValidateContract(false);
    private static void ValidateContract(bool requireReleaseAcceptance)
    {
        foreach (bool enemy in new[] { false,true })
        {
            float offset = 0;
            var actor = ActorAnimationAudit.Actor(enemy);
            Require(actor != null && actor.avatar != null && actor.avatar.isValid, "Actor avatar unresolved.");
            Require(!actor.applyRootMotion, actor.name + ": automatic Root gameplay enabled.");
            var ucc = actor.GetComponentInParent<UltimateCharacterLocomotion>();
            if (ucc != null) Require(!ucc.UseRootMotionPosition && !ucc.UseRootMotionRotation, "UCC Root gameplay enabled.");
            var data = !enemy ? actor.GetComponentInParent<SquadCharacterController>()?.CharacterData ??
                actor.GetComponentInParent<CharacterInfo>()?.SourceData : null;
            var profile = enemy ? actor.GetComponent<LitBrainsEnemy>().presentationProfile :
                data?.playerSettings.locomotion.presentationProfile;
            if (requireReleaseAcceptance) Require(profile != null && profile.supportValidated,
                actor.name + ": locomotion presentation/support acceptance incomplete. See measured animation reports; do not approve this automatically.");
            if (profile != null)
            {
                Require(profile.IsUsable(actor.avatar), "Incomplete/unvalidated locomotion profile: " + profile.name);
                if (!enemy)
                {
                    var bridge = actor.GetComponentInParent<LitOpsiveLocomotionBridge>();
                    Require(bridge != null && bridge.GetComponentInChildren<LitPresentationAnimatorMonitor>(true) != null,
                        "Competing AnimatorMonitor direction writes remain active or the UCC bridge is missing.");
                }
                var controller = actor.runtimeAnimatorController as AnimatorController;
                Require(controller != null && controller.parameters.Any(p => p.name == LocomotionPresentationProfile.PlaybackParameter), "Missing cadence parameter.");
                foreach (var state in controller.layers.SelectMany(l => LocomotionPresentationCalibration.States(l.stateMachine)))
                    if (state.speedParameterActive && state.speedParameter == LocomotionPresentationProfile.PlaybackParameter)
                        Require(state.name == "Locomotion" || state.name == "CombatLocomotion", "Cadence applied to an action: " + state.name);
            }
            using (var sampling = new PlayerInPlaceSampling(actor))
            foreach (var clip in ActorAnimationAudit.Consumers(actor.runtimeAnimatorController).Keys)
            {
                var motion = sampling.Sample(clip);
                if (!enemy)
                {
                    Require(!PlayerInPlaceAudit.IsRootCandidate(clip), "Root provider reference in player gameplay: " + clip.name);
                    continue; // Protected landing/jump may retain body extraction; no consumer is allowed to apply it physically.
                }
                string sourcePath = ActorAnimationAudit.EnemySource(clip.name.Replace("Juggernaut_v2_",""));
                if (sourcePath == null) continue;
                Require(motion.MaxDisplacement <= .005f && motion.MaxYaw <= .1f, "Physical Root trajectory in reconstructed gameplay clip: " + clip.name);
                var source = ActorAnimationAudit.Source(sourcePath);
                var comparison = ActorClipReconstruction.Compare(actor,source,clip,offset);
                Require(comparison.positionError <= .005f && comparison.angleError <= .5f, "Source pose corrupted: " + clip.name);
            }
            if (enemy)
            {
                var combat = actor.GetComponent<LitBrainsEnemy>().combatProfile;
                foreach (var action in combat.attacks.Where(a => a.visualHandoff != null && a.visualHandoff.validated))
                {
                    Require(action.visualHandoff.sourceExitWindow.x * action.clip.length >= action.impactSeconds,
                        "Retiming would shift the impact: " + action.stateName);
                    Require(Mathf.Abs(action.visualHandoff.sourceExitWindow.x * action.clip.length - action.recoverySeconds) <= 1f / action.clip.frameRate &&
                        action.visualHandoff.sourceExitWindow.y > action.visualHandoff.sourceExitWindow.x && action.visualHandoff.sourceExitWindow.y <= 1,
                        "Recovery window differs from gameplay marker: " + action.stateName);
                    var controller = actor.runtimeAnimatorController as AnimatorController;
                    var state = controller.layers.SelectMany(l => LocomotionPresentationCalibration.States(l.stateMachine)).Single(s => s.name == action.stateName);
                    Require(state.speedParameterActive && state.speedParameter == "ActionRecoveryPlaybackRate", "Unbound recovery cadence: " + state.name);
                    Require(!state.transitions.Any(t => t.hasExitTime && t.conditions.Length == 0), "Concurrent automatic action exit: " + state.name);
                }
            }
        }
        Debug.Log("[ActorAnimationContract] PASS targeted physical Root and source-pose contract. Visual acceptance is a separate gate.");
    }
    private static void Require(bool condition,string reason) { if (!condition) throw new BuildFailedException(reason); }
}
