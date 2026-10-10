using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Measures candidates only. Support detection is reported for author review, not certified automatically.</summary>
public static class LocomotionPresentationCalibration
{
    [Serializable] public sealed class Measurement
    {
        public string actor, clip;
        public bool nativeFootIK;
        public float nativeSpeed, rootReferenceSpeed, leftPhase, rightPhase, estimatedStanceResidual;
        public float inplaceDuration, rootDuration;
        public string rootSource, status;
    }
    [Serializable] public sealed class Report { public List<Measurement> measurements = new List<Measurement>(); }
    public static Measurement Measure(Animator actor, AnimationClip clip, Vector2 direction,bool applyFootIK = false)
    {
        using (var sampler = new ActorPoseSampler(actor))
        {
            var frames = sampler.Sample(clip,applyFootIK:applyFootIK);
            var velocities = new List<float>();
            float leftMinimum = frames.Min(f => f.leftFoot.y), rightMinimum = frames.Min(f => f.rightFoot.y);
            Vector3 axis = new Vector3(direction.x,0,direction.y).normalized;
            float dt = clip.length / (frames.Length - 1);
            float leftPhase = 0, rightPhase = 0;
            float leftLowest = float.MaxValue, rightLowest = float.MaxValue;
            for (int i = 1; i < frames.Length - 1; i++)
            {
                var frame = frames[i];
                if (frame.leftFoot.y < leftLowest) { leftLowest = frame.leftFoot.y; leftPhase = frame.time / clip.length; }
                if (frame.rightFoot.y < rightLowest) { rightLowest = frame.rightFoot.y; rightPhase = frame.time / clip.length; }
                foreach (bool left in new[] { true, false })
                {
                    Vector3 foot = left ? frame.leftFoot : frame.rightFoot;
                    Vector3 before = left ? frames[i-1].leftFoot : frames[i-1].rightFoot;
                    float minimum = left ? leftMinimum : rightMinimum;
                    float speed = -Vector3.Dot((foot - before) / dt, axis);
                    if (foot.y <= minimum + .025f && speed > .1f) velocities.Add(speed);
                }
            }
            if (velocities.Count < 5) throw new InvalidOperationException("No reliable support samples: " + clip.name);
            velocities.Sort(); float nativeSpeed = velocities[velocities.Count / 2];
            var measurement = new Measurement { actor = actor.name, clip = AssetDatabase.GetAssetPath(clip), nativeSpeed = nativeSpeed, nativeFootIK=applyFootIK,
                inplaceDuration = clip.length,
                leftPhase = leftPhase, rightPhase = rightPhase,
                estimatedStanceResidual = velocities.Max(v => Mathf.Abs(v - nativeSpeed)) * dt,
                status = "Estimated support candidates; author contact validation required" };
            string originalName = clip.name.Replace("Juggernaut_v2_", "");
            string inplace = ActorAnimationAudit.EnemySource(originalName);
            if (inplace == null && originalName.EndsWith("_Inplace",StringComparison.OrdinalIgnoreCase))
                inplace = AssetDatabase.GetAssetPath(clip);
            if (inplace != null)
            {
                string rootName = Path.GetFileNameWithoutExtension(inplace).Replace("_Inplace","_Root");
                var paths = AssetDatabase.GetAllAssetPaths().Where(p => p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileNameWithoutExtension(p).Equals(rootName,StringComparison.OrdinalIgnoreCase)).ToArray();
                if (paths.Length == 1)
                {
                    var original = ActorAnimationAudit.Source(paths[0]);
                    measurement.rootDuration = original.length;
                    using (var sampling = new PlayerInPlaceSampling(actor))
                        measurement.rootReferenceSpeed = sampling.Sample(original).Distance / clip.length;
                    measurement.rootSource = paths[0];
                }
            }
            return measurement;
        }
    }
    [MenuItem("Lit/Animation Presentation/Measure Locomotion Candidates")]
    public static void MeasureCandidates()
    {
        var report = new Report();
        foreach (bool enemy in new[] { false, true })
        {
            var actor = ActorAnimationAudit.Actor(enemy);
            var clips = ActorAnimationAudit.Consumers(actor.runtimeAnimatorController).Keys;
            foreach (var clip in clips.Where(c => c.name.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (clip.name.Contains("Start") || clip.name.Contains("Stop")) continue;
                string[] directions = { "F", "FR", "R", "BR", "B", "BL", "L", "FL" };
                int index = Array.FindIndex(directions, d => clip.name.Contains("_" + d + "_") || clip.name.EndsWith("_" + d));
                float angle = Mathf.Max(0,index) * Mathf.PI / 4;
                try
                {
                    var controller = actor.runtimeAnimatorController as AnimatorController;
                    var ikOptions = controller.layers.SelectMany(l => States(l.stateMachine)).Where(s => UsesClip(s.motion,clip))
                        .Select(s => s.iKOnFeet).Distinct();
                    foreach (bool nativeIK in ikOptions)
                        report.measurements.Add(Measure(actor,clip,new Vector2(Mathf.Sin(angle),Mathf.Cos(angle)),nativeIK));
                }
                catch (Exception e) { Debug.LogWarning(e.Message); }
            }
        }
        Directory.CreateDirectory(ActorAnimationAudit.ReportFolder);
        File.WriteAllText(ActorAnimationAudit.ReportFolder + "/locomotion-candidates.json", JsonUtility.ToJson(report,true));
    }
    public static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
    {
        foreach (var state in machine.states) yield return state.state;
        foreach (var child in machine.stateMachines) foreach (var state in States(child.stateMachine)) yield return state;
    }
    private static bool UsesClip(Motion motion, AnimationClip clip)
        => motion == clip || motion is BlendTree tree && tree.children.Any(child => UsesClip(child.motion,clip));
    /// <summary>Only named cycle states get the multiplier; actions/starts/stops/Timeline remain at their authored cadence.</summary>
    public static void BindCadence(AnimatorController controller, params string[] stateNames)
    {
        if (!controller.parameters.Any(p => p.name == LocomotionPresentationProfile.PlaybackParameter))
        {
            controller.AddParameter(new AnimatorControllerParameter { name = LocomotionPresentationProfile.PlaybackParameter,
                type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        }
        foreach (var state in controller.layers.SelectMany(l => States(l.stateMachine)).Where(s => stateNames.Contains(s.name)))
        {
            if (state.speedParameterActive && state.speedParameter != LocomotionPresentationProfile.PlaybackParameter)
                throw new InvalidOperationException("Cadence already owned: " + state.name);
            if (state.speedParameterActive && state.speedParameter == LocomotionPresentationProfile.PlaybackParameter) continue;
            state.speedParameter = LocomotionPresentationProfile.PlaybackParameter;
            state.speedParameterActive = true; EditorUtility.SetDirty(state);
        }
    }
    public static void StageReconstruction()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').Contains("/Library/ActorAnimationSandbox/"))
            throw new InvalidOperationException("Run staging in the isolated validation project.");
        ActorClipReconstruction.Prevalidate(); ActorClipReconstruction.Apply();
        var paths = AssetDatabase.FindAssets("t:AnimationClip",new[] { ActorAnimationAudit.EnemyFolder + "Animations" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var hashes = paths.Select(p => Hash128.Compute(File.ReadAllText(p))).ToArray();
        ActorClipReconstruction.Prevalidate(); ActorClipReconstruction.Apply();
        for (int i = 0; i < paths.Length; i++) if (hashes[i] != Hash128.Compute(File.ReadAllText(paths[i])))
            throw new InvalidOperationException("Reconstruction not idempotent: " + paths[i]);
        ActorAnimationAudit.AuditAfter(); MeasureCandidates();
        File.WriteAllText(ActorAnimationAudit.ReportFolder + "/stage-result.txt", "PASS source pose fidelity, root trajectory, event identity, GUID preservation, idempotence\nVisual support validation pending.");
    }
    public static void PrepareLaboratoryCandidates()
        => PrepareLaboratoryCandidates(false);
    public static void PrepareLaboratoryCandidates(bool includeExperimentalHandoffs)
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').Contains("/Library/ActorAnimationSandbox/"))
            throw new InvalidOperationException("Experimental profiles are restricted to ActorAnimationSandbox.");
        Directory.CreateDirectory("Assets/AnimationPresentationCandidates"); AssetDatabase.Refresh();
        UpgradeLaboratoryMonitor();
        var phaseReport = new ActorCyclePhaseAlignment.Report();
        foreach (bool enemy in new[] {false,true})
        {
            var actor = ActorAnimationAudit.Actor(enemy);
            var controller = (AnimatorController)actor.runtimeAnimatorController;
            var profile = AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>("Assets/AnimationPresentationCandidates/" + (enemy ? "Juggernaut" : "Lucian") + ".asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<LocomotionPresentationProfile>();
                AssetDatabase.CreateAsset(profile,"Assets/AnimationPresentationCandidates/" + (enemy ? "Juggernaut" : "Lucian") + ".asset");
            }
            var states = controller.layers.SelectMany(l => States(l.stateMachine)).ToArray();
            var locomotion = (BlendTree)states.Single(s => s.name == (enemy ? "Locomotion" : "CombatLocomotion")).motion;
            var tiers = locomotion.children.Where(c => c.motion is BlendTree).ToArray();
            if (tiers.Length != 2) throw new InvalidOperationException("Two directional gait trees required.");
            foreach (var tier in tiers) phaseReport.items.AddRange(ActorCyclePhaseAlignment.ApplyLaboratory(actor,(BlendTree)tier.motion).items);
            profile.measuredAvatar = actor.avatar;
            profile.alignNavMeshGround = enemy;
            profile.walk = Cycles(actor,(BlendTree)tiers[0].motion);
            profile.run = Cycles(actor,(BlendTree)tiers[1].motion);
            profile.blendThresholds = enemy ? new Vector2(.5f,1) : new Vector2(1.1f,3.25f);
            if (!enemy)
            {
                var forward = ((BlendTree)states.Single(s => s.name == "Locomotion").motion).children.Where(c => c.threshold > 0).ToArray();
                bool nativeIK = states.Single(s => s.name == "Locomotion").iKOnFeet;
                profile.explorationWalk = Cycle(actor,(AnimationClip)forward[0].motion,Vector2.up,nativeIK);
                profile.explorationRun = Cycle(actor,(AnimationClip)forward[1].motion,Vector2.up,nativeIK);
            }
            profile.validated = true; // Experimental activation in the isolated lab only.
            EditorUtility.SetDirty(profile);
            BindCadence(controller,enemy ? new[] {"Locomotion"} : new[] {"Locomotion","CombatLocomotion"});
            if (enemy)
            {
                var prefab = PrefabUtility.LoadPrefabContents(JuggernautV2Setup.PrefabPath);
                try { prefab.GetComponent<LitBrainsEnemy>().presentationProfile = profile; PrefabUtility.SaveAsPrefabAsset(prefab,JuggernautV2Setup.PrefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
                var combat = AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(LucianBrainsCombatSetup.FluidProfilePath);
                if (!includeExperimentalHandoffs)
                {
                    // Disarm previous experimental runs. A measured blocked exit is never a validated default.
                    foreach (var action in combat.attacks)
                    {
                        if (action.visualHandoff != null) action.visualHandoff.validated = false;
                        var state = states.Single(s => s.name == action.stateName);
                        if (state.speedParameter == "ActionRecoveryPlaybackRate")
                        { state.speedParameterActive = false; EditorUtility.SetDirty(state); }
                    }
                    EditorUtility.SetDirty(combat);
                }
                else
                {
                if (!controller.parameters.Any(p => p.name == "ActionRecoveryPlaybackRate"))
                    controller.AddParameter(new AnimatorControllerParameter {name="ActionRecoveryPlaybackRate",type=AnimatorControllerParameterType.Float,defaultFloat=1});
                foreach (var action in combat.attacks)
                {
                    action.visualHandoff = new VisualActionHandoff { validated=true,
                        sourceExitWindow = new Vector2(action.recoverySeconds / action.clip.length,1),
                        destinationCycleSeconds=profile.walk[0].clip.length };
                    var state = states.Single(s => s.name == action.stateName);
                    state.speedParameter = "ActionRecoveryPlaybackRate"; state.speedParameterActive=true;
                    foreach (var transition in state.transitions.Where(t => t.hasExitTime && t.conditions.Length == 0).ToArray()) state.RemoveTransition(transition);
                    EditorUtility.SetDirty(state);
                }
                EditorUtility.SetDirty(combat);
                }
            }
            else
            {
                var data = AssetDatabase.LoadAssetAtPath<GameObject>(ActorAnimationAudit.PlayerPrefab).GetComponent<CharacterInfo>().CharacterData;
                data.playerSettings.locomotion.presentationProfile = profile;
                EditorUtility.SetDirty(data);
            }
            EditorUtility.SetDirty(controller);
        }
        AssetDatabase.SaveAssets(); MeasureCandidates();
        File.WriteAllText(ActorAnimationAudit.ReportFolder + "/directional-phases.json",JsonUtility.ToJson(phaseReport,true));
    }
    private static LocomotionPresentationProfile.Cycle[] Cycles(Animator actor,BlendTree tree) => tree.children
        .Where(c => c.motion is AnimationClip && c.position.sqrMagnitude > .001f).Select(c =>
        {
            var cycle = Cycle(actor,(AnimationClip)c.motion,c.position);
            cycle.nativeSpeed *= c.timeScale;
            cycle.leftContactPhase = Mathf.Repeat(cycle.leftContactPhase-c.cycleOffset,1);
            cycle.rightContactPhase = Mathf.Repeat(cycle.rightContactPhase-c.cycleOffset,1);
            return cycle;
        }).ToArray();
    private static LocomotionPresentationProfile.Cycle Cycle(Animator actor,AnimationClip clip,Vector2 direction,bool applyFootIK = false)
    {
        var measurement = Measure(actor,clip,direction,applyFootIK);
        return new LocomotionPresentationProfile.Cycle {clip=clip,direction=direction,
            nativeSpeed=measurement.nativeSpeed,
            leftContactPhase=measurement.leftPhase,rightContactPhase=measurement.rightPhase};
    }
    private static void UpgradeLaboratoryMonitor()
    {
        var prefab = PrefabUtility.LoadPrefabContents(ActorAnimationAudit.PlayerPrefab);
        try
        {
            var monitor = prefab.GetComponentInChildren<Opsive.UltimateCharacterController.Character.AnimatorMonitor>(true);
            if (monitor == null) throw new InvalidOperationException("Player AnimatorMonitor unresolved.");
            if (monitor is LitPresentationAnimatorMonitor) return;
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Characters/AnimationPresentation/LitPresentationAnimatorMonitor.cs");
            var serialized = new SerializedObject(monitor);
            serialized.FindProperty("m_Script").objectReferenceValue = script;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab,ActorAnimationAudit.PlayerPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ActorAnimationAudit.PlayerPrefab).GetComponentInChildren<LitPresentationAnimatorMonitor>(true) == null)
            throw new InvalidOperationException("Monitor conversion failed; measured presentation cannot compete with raw input writes.");
    }
}
