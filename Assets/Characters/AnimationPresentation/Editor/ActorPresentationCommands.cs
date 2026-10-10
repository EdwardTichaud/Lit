using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit commands for an open Editor. No import-time migration or scene changes.</summary>
[InitializeOnLoad]
public static class ActorPresentationCommands
{
    static ActorPresentationCommands() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        const string request = "Library/ActorPresentation.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        string command = File.ReadAllText(request).Trim(); File.Delete(request);
        try
        {
            switch (command)
            {
                case "audit": ActorAnimationAudit.Audit(); break;
                case "after": ActorAnimationAudit.AuditAfter(); break;
                case "prevalidate": ActorClipReconstruction.Prevalidate(); break;
                case "apply": ActorClipReconstruction.Apply(); break;
                case "reconstruct": Reconstruct(); break;
                case "validate": ActorAnimationContractValidator.Validate(); break;
                case "technical": ActorAnimationContractValidator.ValidateTechnical(); break;
                default: throw new InvalidOperationException("Unknown animation command: " + command);
            }
            File.WriteAllText("Library/ActorPresentation.result",command + " OK");
        }
        catch (Exception error) { Debug.LogException(error); File.WriteAllText("Library/ActorPresentation.result",error.ToString()); }
    }
    public static void Reconstruct()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before reconstruction.");
        ActorAnimationAudit.Audit();
        ActorClipReconstruction.Prevalidate(); ActorClipReconstruction.Apply();
        // This command deliberately does not enable unaccepted presentation profiles.
    }
    public static void MeasureFinal()
    {
        ActorAnimationAudit.AuditAfter(); ActorActionExitAudit.Audit(); ActorCyclePhaseAlignment.Audit();
        ActorAnimationContractValidator.ValidateTechnical();
    }
}

/// <summary>Read-only recovery feasibility report. Never changes combat markers or silently approves a retimed tail.</summary>
public static class ActorActionExitAudit
{
    [Serializable] public sealed class Exit
    {
        public string actor, state, clip, status;
        public float impactSeconds, recoverySeconds, gameplayReleaseSeconds, sourceLength, recoveryRate;
        public float originalExitFootPoseDistance, recoveredExitFootPoseDistance, recoveredExitPelvisHeightDifference, suggestedDestinationPhase;
        public float recoveredExitSoleDistance;
    }
    [Serializable] public sealed class Report { public List<Exit> exits = new List<Exit>(); }
    [MenuItem("Lit/Animation Presentation/Audit Action Exits")]
    public static void Audit()
        => Audit(false);
    public static void AuditWithNativeIK()
        => Audit(true);
    private static void Audit(bool nativeFootIK)
    {
        var actor = ActorAnimationAudit.Actor(true);
        var profile = actor.GetComponent<LitBrainsEnemy>().combatProfile;
        var idle = ActorAnimationAudit.Source(ActorAnimationAudit.EnemySource("Idle"));
        var report = new Report();
        using (var sampler = new ActorPoseSampler(actor))
        {
            var destination = sampler.Sample(idle,measureSoles:true,applyFootIK:nativeFootIK);
            foreach (var attack in profile.attacks)
            {
                var frames = sampler.Sample(attack.clip,measureSoles:true,applyFootIK:nativeFootIK);
                float release = attack.recoverySeconds + attack.recoveryDurationSeconds;
                int originalIndex = Mathf.Clamp(Mathf.RoundToInt(release/attack.clip.length*(frames.Length-1)),0,frames.Length-1);
                var last = frames[frames.Length-1];
                int best = 0; float cost = float.PositiveInfinity;
                for (int i = 0; i < destination.Length-1; i++)
                {
                    float distance = Mathf.Max(Vector3.Distance(last.leftSole,destination[i].leftSole),Vector3.Distance(last.rightSole,destination[i].rightSole));
                    if (distance < cost) { best = i; cost = distance; }
                }
                var handoff = new VisualActionHandoff {sourceExitWindow=new Vector2(attack.recoverySeconds/attack.clip.length,1)};
                report.exits.Add(new Exit {actor="Juggernaut_v2",state=attack.stateName,clip=AssetDatabase.GetAssetPath(attack.clip),
                    impactSeconds=attack.impactSeconds,recoverySeconds=attack.recoverySeconds,gameplayReleaseSeconds=release,sourceLength=attack.clip.length,
                    recoveryRate=handoff.RecoveryRate(attack.clip,attack.recoveryDurationSeconds),
                    originalExitFootPoseDistance=Mathf.Max(Vector3.Distance(frames[originalIndex].leftFoot,destination[best].leftFoot),Vector3.Distance(frames[originalIndex].rightFoot,destination[best].rightFoot)),
                    recoveredExitFootPoseDistance=Mathf.Max(Vector3.Distance(last.leftFoot,destination[best].leftFoot),Vector3.Distance(last.rightFoot,destination[best].rightFoot)),
                    recoveredExitSoleDistance=cost,recoveredExitPelvisHeightDifference=Mathf.Abs(last.body.y-destination[best].body.y),
                    suggestedDestinationPhase=(float)best/(destination.Length-1),
                    status=cost > .03f || Mathf.Abs(last.body.y-destination[best].body.y) > .02f
                        ? "Blocked: source tail does not match Idle within support tolerances; dedicated recovery/animation retouch required"
                        : "Pose candidate; validate retimed recovery and interruptions visually before enabling"});
            }
        }
        Directory.CreateDirectory(ActorAnimationAudit.ReportFolder);
        File.WriteAllText(ActorAnimationAudit.ReportFolder + (nativeFootIK ? "/action-exits-native-ik.json" : "/action-exits.json"),JsonUtility.ToJson(report,true));
    }
}
