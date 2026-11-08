using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using Object = UnityEngine.Object;

/// <summary>Tests continuous exploration support from the current Idle bone pose, in memory.</summary>
public static class LucianIdleSupportAudit
{
    [Serializable] sealed class Case
    {
        public float walkWeight, maxSoleError, maxIdleFootHorizontalError, maxIdleFootRotation, maxHipsError;
        public float minLowerSole, maxLowerSole;
        public int samples;
    }

    [Serializable] sealed class Report
    {
        public string idleClip, status;
        public string scope = "Lucian exploration tree and target avatar, in memory. No UCC, movement, transitions between states or runtime activation.";
        public List<Case> blends = new List<Case>();
    }

    [Serializable] sealed class StopFrame
    {
        public float stopPhase, idlePhase, stopWeight, idleWeight, leftStep, rightStep, footAngle, lowerSole;
    }

    [Serializable] sealed class StopReport
    {
        public string scope = "Actual Lucian avatar, Walk_Stop and Locomotion motions, authored unconditional transition. Isolated Animator without UCC or production writes.";
        public List<StopFrame> authored = new List<StopFrame>();
        public List<StopFrame> blendedGoals = new List<StopFrame>();
        public List<StopFrame> groundedGoals = new List<StopFrame>();
        public float maxHipsDifference;
    }

    [Serializable] sealed class BrakeMatch
    {
        public string pair;
        public float sourcePhase, targetPhase, leftDistance, rightDistance, pelvisDistance, pelvisAngle;
    }

    [Serializable] sealed class BrakeDonorReport
    {
        public string scope = "Isolated Humanoid clips sampled on Lucian. Match minimizes the larger horizontal foot displacement; no changes to clip timing, poses or physics.";
        public string stopClip, combatIdleClip, explorationIdleClip, approachClip;
        public float stopLength, leftFootTravel, rightFootTravel, leftSoleExcursion, rightSoleExcursion;
        public List<BrakeMatch> matches = new List<BrakeMatch>();
    }

    public static void RunBrakeDonorAudit()
    {
        var profile = AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        NativeFootSupportPreparation.Validate(profile, profile.footSupports, true);
        var source = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder + "/LucianCadence.overrideController");
        var controller = (AnimatorController)source.runtimeAnimatorController;
        var states = LocomotionPresentationCalibration.States(controller.layers[0].stateMachine).ToArray();
        AnimationClip Clip(Motion motion)
        {
            var key = (AnimationClip)motion;
            return source[key] ?? key;
        }

        var stop = Clip(((BlendTree)states.Single(s => s.name == "Walk_Stop").motion).children.Single().motion);
        var combat = Clip(states.Single(s => s.name == "CombatIdle").motion);
        var exploration = Clip(((BlendTree)states.Single(s => s.name == "Locomotion").motion).children.Single(c => Mathf.Abs(c.threshold) < .0001f).motion);
        var approach = profile.walk.Single(c => c.clip.name.Contains("Walk_FL_")).clip;
        using var sampler = new ActorPoseSampler(ActorAnimationAudit.Actor(false));
        const int samples = 240;
        var walkFrames = sampler.Sample(approach, sampleCount: samples, measureSoles: true, applyFootIK: true);
        var stopFrames = sampler.Sample(stop, sampleCount: samples, measureSoles: true, applyFootIK: true);
        var combatFrames = sampler.Sample(combat, sampleCount: samples, measureSoles: true);
        var explorationFrames = sampler.Sample(exploration, sampleCount: samples, measureSoles: true);
        var report = new BrakeDonorReport
        {
            stopClip = AssetDatabase.GetAssetPath(stop),
            combatIdleClip = AssetDatabase.GetAssetPath(combat),
            explorationIdleClip = AssetDatabase.GetAssetPath(exploration),
            approachClip = AssetDatabase.GetAssetPath(approach),
            stopLength = stop.length,
            leftFootTravel = Vector3.ProjectOnPlane(stopFrames.Last().leftFoot - stopFrames[0].leftFoot, Vector3.up).magnitude,
            rightFootTravel = Vector3.ProjectOnPlane(stopFrames.Last().rightFoot - stopFrames[0].rightFoot, Vector3.up).magnitude,
            leftSoleExcursion = stopFrames.Max(f => f.leftSole.y) - stopFrames.Min(f => f.leftSole.y),
            rightSoleExcursion = stopFrames.Max(f => f.rightSole.y) - stopFrames.Min(f => f.rightSole.y)
        };
        report.matches.Add(Match("Approach to CombatIdle", walkFrames, combatFrames, 0, samples, 0, samples));
        report.matches.Add(Match("Approach to Walk_Stop entry", walkFrames, stopFrames, 0, samples, 0, 0));
        report.matches.Add(Match("Walk_Stop exit to CombatIdle", stopFrames, combatFrames, samples, samples, 0, samples));
        report.matches.Add(Match("Walk_Stop exit to exploration idle", stopFrames, explorationFrames, samples, samples, 0, samples));
        File.WriteAllText("Library/LucianCoherence/brake-donor-audit.json", JsonUtility.ToJson(report, true));
    }

    static BrakeMatch Match(string pair, ActorPoseSampler.Frame[] sources, ActorPoseSampler.Frame[] targets,
        int sourceFirst, int sourceLast, int targetFirst, int targetLast)
    {
        var match = new BrakeMatch { pair = pair };
        float best = float.PositiveInfinity;
        for (int i = sourceFirst; i <= sourceLast; ++i)
        for (int j = targetFirst; j <= targetLast; ++j)
        {
            var source = sources[i];
            var target = targets[j];
            float left = Vector3.ProjectOnPlane(target.leftFoot - source.leftFoot, Vector3.up).magnitude;
            float right = Vector3.ProjectOnPlane(target.rightFoot - source.rightFoot, Vector3.up).magnitude;
            float cost = Mathf.Max(left, right);
            if (cost >= best) continue;
            best = cost;
            match.sourcePhase = i / (sources.Length - 1f);
            match.targetPhase = j / (targets.Length - 1f);
            match.leftDistance = left;
            match.rightDistance = right;
            match.pelvisDistance = Vector3.Distance(source.body, target.body);
            match.pelvisAngle = Quaternion.Angle(source.bodyRotation, target.bodyRotation);
        }
        return match;
    }

    internal static void ConfigurePlayProbe(LitPresentationCharacterIK solver, List<Object> transient, bool includeCombatIdle,
        bool keepCombatDirection, float combatBrakeFade)
    {
        var original = AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        NativeFootSupportPreparation.Validate(original, original.footSupports, true);
        var source = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder + "/LucianCadence.overrideController");
        var graph = (AnimatorController)source.runtimeAnimatorController;
        var states = LocomotionPresentationCalibration.States(graph.layers[0].stateMachine).ToArray();
        var rest = ((BlendTree)states.Single(s => s.name == "Locomotion").motion).children.Single(c => Mathf.Abs(c.threshold) < .0001f);
        var stopChild = ((BlendTree)states.Single(s => s.name == "Walk_Stop").motion).children.Single();
        if (stopChild.timeScale != 1 || stopChild.cycleOffset != 0 || rest.timeScale != 1)
            throw new InvalidOperationException("The measured stop/idle bindings have changed.");
        var idleKey = (AnimationClip)rest.motion;
        var stopKey = (AnimationClip)stopChild.motion;
        var stop = source[stopKey] ?? stopKey;
        var profile = Object.Instantiate(original);
        var data = Object.Instantiate(original.footSupports);
        transient.Add(profile);
        transient.Add(data);
        profile.hideFlags = data.hideFlags = HideFlags.HideAndDontSave;
        profile.footSupports = data;
        using var sampler = new ActorPoseSampler(ActorAnimationAudit.Actor(false));
        AddIdle(profile, data, sampler, source[idleKey] ?? idleKey, rest.cycleOffset);
        var frames = sampler.Sample(stop, sampleCount: 120, measureSoles: true, applyFootIK: true);
        var stopGoals = GroundedGoals(frames, data, keepRelativeHeight: true);
        LocomotionPresentationProfile.Cycle combatIdle = null;
        if (includeCombatIdle)
        {
            var restState = states.Single(s => s.name == "CombatIdle");
            var restKey = (AnimationClip)restState.motion;
            var combatRest = source[restKey] ?? restKey;
            var restFrames = sampler.Sample(combatRest, sampleCount: 120, measureSoles: true);
            combatIdle = new LocomotionPresentationProfile.Cycle
            {
                clip = combatRest,
                cycleOffset = restState.cycleOffset,
                footGoals = GroundedGoals(restFrames, data, keepRelativeHeight: false)
            };
        }
        solver.ConfigureStopProbe(profile, stop, stopGoals, combatIdle, keepCombatDirection, combatBrakeFade);
    }

    static NativeFootGoalCurves GroundedGoals(ActorPoseSampler.Frame[] frames, NativeFootSupportSet data, bool keepRelativeHeight)
    {
        float Height(ActorPoseSampler.Frame f, bool left) =>
            keepRelativeHeight ? Mathf.Min(f.leftSole.y, f.rightSole.y) : left ? f.leftSole.y : f.rightSole.y;
        var leftOffset = Quaternion.Inverse(data.leftGoalToBone);
        var rightOffset = Quaternion.Inverse(data.rightGoalToBone);
        return new NativeFootGoalCurves
        {
            leftPosition = LucianFootGoalExport.Positions(frames.Select(f => f.leftFoot + Vector3.up * (LucianContactBlendAudit.Floor - Height(f, true))).ToArray()),
            rightPosition = LucianFootGoalExport.Positions(frames.Select(f => f.rightFoot + Vector3.up * (LucianContactBlendAudit.Floor - Height(f, false))).ToArray()),
            leftRotation = LucianFootGoalExport.Rotations(frames.Select(f => f.leftRotation * leftOffset).ToArray()),
            rightRotation = LucianFootGoalExport.Rotations(frames.Select(f => f.rightRotation * rightOffset).ToArray())
        };
    }

    public static void RunStopTransition()
    {
        var original = AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        NativeFootSupportPreparation.Validate(original, original.footSupports, true);
        var source = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder + "/LucianCadence.overrideController");
        var graph = (AnimatorController)source.runtimeAnimatorController;
        var states = LocomotionPresentationCalibration.States(graph.layers[0].stateMachine).ToArray();
        var rest = ((BlendTree)states.Single(s => s.name == "Locomotion").motion).children.Single(c => Mathf.Abs(c.threshold) < .0001f);
        var stopChild = ((BlendTree)states.Single(s => s.name == "Walk_Stop").motion).children.Single();
        if (stopChild.timeScale != 1 || stopChild.cycleOffset != 0 || rest.timeScale != 1)
            throw new InvalidOperationException("The measured stop/idle bindings have changed.");
        var idleKey = (AnimationClip)rest.motion;
        var stopKey = (AnimationClip)stopChild.motion;
        var idle = source[idleKey] ?? idleKey;
        var stop = source[stopKey] ?? stopKey;
        var profile = Object.Instantiate(original);
        var data = Object.Instantiate(original.footSupports);
        var transient = new List<Object> { profile, data };
        var report = new StopReport();
        try
        {
            using var sampler = new ActorPoseSampler(ActorAnimationAudit.Actor(false));
            AddIdle(profile, data, sampler, idle, rest.cycleOffset);
            const int steps = 120;
            var stopPoses = sampler.Sample(stop, sampleCount: steps, measureSoles: true, applyFootIK: true);
            var authored = LucianContactBlendAudit.CreateMuscleController(source, transient, "Locomotion", false, "Walk_Stop", true);
            var candidate = LucianContactBlendAudit.CreateMuscleController(source, transient, "Locomotion", false, "Walk_Stop");
            AnimatorControllerPlayable active = default;
            void Configure(AnimatorControllerPlayable p)
            {
                active = p;
                p.SetFloat("LitSpeed", 0);
                p.SetFloat("LocomotionPlaybackRate", 1);
                p.Play("Base Layer.Walk_Stop", 0, .75f);
            }

            StopFrame Read()
            {
                var current = active.GetCurrentAnimatorStateInfo(0);
                var currentClips = active.GetCurrentAnimatorClipInfo(0);
                bool transitioning = active.IsInTransition(0);
                bool stopping = current.shortNameHash == Animator.StringToHash("Walk_Stop");
                var nextClips = transitioning ? active.GetNextAnimatorClipInfo(0) : Array.Empty<AnimatorClipInfo>();
                float stopWeight = stopping ? currentClips.Where(c => c.clip == stop).Sum(c => c.weight) : 0;
                float idleWeight = (stopping ? nextClips : currentClips).Where(c => c.clip == idle).Sum(c => c.weight);
                if (Mathf.Abs(stopWeight + idleWeight - 1) > .002f)
                    throw new InvalidOperationException("Unity transition weights do not cover the complete pose.");
                return new StopFrame
                {
                    stopPhase = stopping ? current.normalizedTime : 1,
                    idlePhase = transitioning ? active.GetNextAnimatorStateInfo(0).normalizedTime : stopping ? 0 : current.normalizedTime,
                    stopWeight = stopWeight,
                    idleWeight = idleWeight
                };
            }

            var reference = sampler.Sample(stop, sampleCount: steps, measureSoles: true, controller: authored, configureController: Configure,
                warmControllerPose: true, geometrySample: (_, _, _) => report.authored.Add(Read()));
            ActorPoseSampler.Frame[] Solve(List<StopFrame> frames, bool groundStop)
            {
                return sampler.Sample(stop, sampleCount: steps, measureSoles: true, controller: candidate, configureController: Configure,
                    warmControllerPose: true, modifyIKGoals: animator =>
                {
                    var row = Read();
                    frames.Add(row);
                    float index = Mathf.Clamp01(row.stopPhase) * steps;
                    int i = Mathf.Min(steps - 1, (int)index);
                    float fraction = index - i;
                    var a = stopPoses[i];
                    var b = stopPoses[i + 1];
                    Vector3 lift = Vector3.zero;
                    if (groundStop)
                    {
                        float lowerA = Mathf.Min(a.leftSole.y, a.rightSole.y);
                        float lowerB = Mathf.Min(b.leftSole.y, b.rightSole.y);
                        lift = Vector3.up * (LucianContactBlendAudit.Floor - Mathf.Lerp(lowerA, lowerB, fraction));
                    }
                    float blend = row.idleWeight / (row.stopWeight + row.idleWeight);
                    var idleSupport = profile.explorationIdleSupport;
                    idleSupport.footGoals.Evaluate(row.idlePhase + idleSupport.cycleOffset, out var left, out var right, out var leftRotation, out var rightRotation);
                    var pose = new NativeFootGoalCurves.Pose
                    {
                        left = Vector3.Lerp(Vector3.Lerp(a.leftFoot, b.leftFoot, fraction) + lift, left, blend),
                        right = Vector3.Lerp(Vector3.Lerp(a.rightFoot, b.rightFoot, fraction) + lift, right, blend),
                        leftRotation = Quaternion.Slerp(Quaternion.Slerp(a.leftRotation, b.leftRotation, fraction) * Quaternion.Inverse(data.leftGoalToBone), leftRotation, blend),
                        rightRotation = Quaternion.Slerp(Quaternion.Slerp(a.rightRotation, b.rightRotation, fraction) * Quaternion.Inverse(data.rightGoalToBone), rightRotation, blend)
                    };
                    NativeFootGoalCurves.Apply(animator, pose);
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1);
                    animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1);
                    animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 1);
                    animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 1);
                });
            }
            var solved = Solve(report.blendedGoals, false);
            var grounded = Solve(report.groundedGoals, true);
            MeasureSteps(reference, report.authored);
            MeasureSteps(solved, report.blendedGoals);
            MeasureSteps(grounded, report.groundedGoals);
            for (int i = 0; i < solved.Length; ++i)
            {
                var a = report.authored[i];
                var b = report.blendedGoals[i];
                if (Mathf.Abs(a.stopPhase - b.stopPhase) > .0001f || Mathf.Abs(a.idlePhase - b.idlePhase) > .0001f ||
                    Mathf.Abs(a.stopWeight - b.stopWeight) > .0001f ||
                    Mathf.Abs(b.stopPhase - report.groundedGoals[i].stopPhase) > .0001f ||
                    Mathf.Abs(b.idlePhase - report.groundedGoals[i].idlePhase) > .0001f ||
                    Mathf.Abs(b.stopWeight - report.groundedGoals[i].stopWeight) > .0001f)
                    throw new InvalidOperationException("Transition comparisons must use identical clocks and weights.");
                report.maxHipsDifference = Mathf.Max(report.maxHipsDifference,
                    Vector3.Distance(reference[i].body, solved[i].body), Vector3.Distance(reference[i].body, grounded[i].body));
            }
            File.WriteAllText("Library/LucianCoherence/stop-transition-goals.json", JsonUtility.ToJson(report, true));
        }
        finally
        {
            for (int i = transient.Count - 1; i >= 0; --i)
                if (transient[i] != null) Object.DestroyImmediate(transient[i]);
        }
    }

    static void MeasureSteps(ActorPoseSampler.Frame[] poses, List<StopFrame> rows)
    {
        if (poses.Length != rows.Count || !rows.Any(row => row.stopWeight > .01f && row.idleWeight > .01f) ||
            rows[0].stopWeight < .99f || rows[rows.Count - 1].idleWeight < .99f)
            throw new InvalidOperationException("The complete authored transition must be sampled.");
        for (int i = 0; i < poses.Length; ++i)
        {
            var pose = poses[i];
            var row = rows[i];
            row.lowerSole = Mathf.Min(pose.leftSole.y, pose.rightSole.y);
            if (i == 0) continue;
            var previous = poses[i - 1];
            row.leftStep = Vector3.Distance(pose.leftFoot, previous.leftFoot);
            row.rightStep = Vector3.Distance(pose.rightFoot, previous.rightFoot);
            row.footAngle = Mathf.Max(Quaternion.Angle(pose.leftRotation, previous.leftRotation), Quaternion.Angle(pose.rightRotation, previous.rightRotation));
        }
    }

    public static void Run()
    {
        var original = AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        NativeFootSupportPreparation.Validate(original, original.footSupports, true);
        var source = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder + "/LucianCadence.overrideController");
        var graph = (AnimatorController)source.runtimeAnimatorController;
        var state = LocomotionPresentationCalibration.States(graph.layers[0].stateMachine).Single(s => s.name == "Locomotion");
        var rest = ((BlendTree)state.motion).children.Single(c => Mathf.Abs(c.threshold) < .0001f);
        var idleKey = (AnimationClip)rest.motion;
        var idle = source[idleKey] ?? idleKey;
        var report = new Report { idleClip = AssetDatabase.GetAssetPath(idle) };
        var profile = Object.Instantiate(original);
        var data = Object.Instantiate(original.footSupports);
        var transient = new List<Object> { profile, data };
        try
        {
            using var sampler = new ActorPoseSampler(ActorAnimationAudit.Actor(false));
            AddIdle(profile, data, sampler, idle, rest.cycleOffset);
            var controller = LucianContactBlendAudit.CreateMuscleController(source, transient, "Locomotion", false);
            foreach (float weight in new[] { 0f, .02f, .22f, .5f, .78f, .98f, 1f })
                report.blends.Add(Measure(profile, data, sampler, controller, weight));

            bool passed = report.blends.All(row => row.maxSoleError <= .02f && row.maxHipsError <= .0001f &&
                row.maxIdleFootHorizontalError <= .003f && row.maxIdleFootRotation <= .5f &&
                row.minLowerSole >= LucianContactBlendAudit.Floor - .02f &&
                row.maxLowerSole <= LucianContactBlendAudit.Floor + .02f);
            report.status = passed ? "PASS_OFFLINE" : "REVIEW";
            File.WriteAllText("Library/LucianCoherence/idle-support-prototype.json", JsonUtility.ToJson(report, true));
            if (!passed) throw new InvalidOperationException("Idle support prototype did not pass the pose bounds.");
        }
        finally
        {
            for (int i = transient.Count - 1; i >= 0; --i)
                if (transient[i] != null) Object.DestroyImmediate(transient[i]);
        }
    }

    static void AddIdle(LocomotionPresentationProfile profile, NativeFootSupportSet data, ActorPoseSampler sampler,
        AnimationClip idle, float offset)
    {
        var geometry = new NativeFootSupportSet.Cycle
        {
            clip = idle,
            cycleOffset = offset,
            left = new Vector3[(data.steps + 1) * data.leftPointIds.Length],
            right = new Vector3[(data.steps + 1) * data.rightPointIds.Length],
        };
        var frames = sampler.Sample(idle, sampleCount: data.steps, measureSoles: true, geometrySample: (animator, soles, index) =>
        {
            CaptureSole(animator.GetBoneTransform(HumanBodyBones.LeftFoot), soles, true, data.leftPointIds, geometry.left, index);
            CaptureSole(animator.GetBoneTransform(HumanBodyBones.RightFoot), soles, false, data.rightPointIds, geometry.right, index);
        });
        float floor = LucianContactBlendAudit.Floor;
        // Ground the actual rest footprint at the measured rig floor. The
        // clip's built-in IK targets and its uncorrected feet are both below it.
        geometry.heights = frames.Select(_ => new Vector2(floor, floor)).ToArray();
        var leftOffset = Quaternion.Inverse(data.leftGoalToBone);
        var rightOffset = Quaternion.Inverse(data.rightGoalToBone);
        profile.explorationIdleSupport = new LocomotionPresentationProfile.Cycle
        {
            clip = idle,
            cycleOffset = offset,
            footGoals = new NativeFootGoalCurves
            {
                leftPosition = LucianFootGoalExport.Positions(frames.Select(f => f.leftFoot + Vector3.up * (floor - f.leftSole.y)).ToArray()),
                rightPosition = LucianFootGoalExport.Positions(frames.Select(f => f.rightFoot + Vector3.up * (floor - f.rightSole.y)).ToArray()),
                leftRotation = LucianFootGoalExport.Rotations(frames.Select(f => f.leftRotation * leftOffset).ToArray()),
                rightRotation = LucianFootGoalExport.Rotations(frames.Select(f => f.rightRotation * rightOffset).ToArray()),
            },
        };
        data.cycles = data.cycles.Append(geometry).ToArray();
    }

    static void CaptureSole(Transform foot, ActorSoleSampler soles, bool left, string[] ids, Vector3[] frames, int index)
    {
        var points = soles.FootPoints(left).ToDictionary(point => point.Id);
        var inverse = Quaternion.Inverse(foot.rotation);
        for (int i = 0; i < ids.Length; ++i)
        {
            if (!points.TryGetValue(ids[i], out var point) || !soles.TryPosition(point, out var worldPosition))
                throw new InvalidOperationException("Unresolved rest support vertex: " + ids[i]);
            frames[index * ids.Length + i] = inverse * (worldPosition - foot.position);
        }
    }

    static Case Measure(LocomotionPresentationProfile profile, NativeFootSupportSet data, ActorPoseSampler sampler,
        RuntimeAnimatorController controller, float weight)
    {
        var walk = profile.explorationWalk.clip;
        var clips = new List<AnimatorClipInfo>();
        var inputs = new List<NativeFootSupportSet.Input>();
        var targets = new List<Vector2>();
        AnimatorControllerPlayable active = default;
        void Configure(AnimatorControllerPlayable playable)
        {
            active = playable;
            playable.SetFloat("LitSpeed", profile.explorationBlendThresholds.x * weight);
            playable.SetFloat("LocomotionPlaybackRate", 1);
            playable.Play("Base Layer.Locomotion", 0, 0);
        }

        var reference = sampler.Sample(walk, sampleCount: 120, measureSoles: true, controller: controller,
            configureController: Configure, warmControllerPose: true);
        var solved = sampler.Sample(walk, sampleCount: 120, measureSoles: true, controller: controller,
            configureController: Configure, warmControllerPose: true, modifyIKGoals: animator =>
            {
                clips.Clear();
                clips.AddRange(active.GetCurrentAnimatorClipInfo(0));
                if (Mathf.Abs(clips.Where(c => c.clip == walk).Sum(c => c.weight) - weight) > .002f)
                    throw new InvalidOperationException("Unexpected exploration weights.");
                var inverse = Quaternion.Inverse(animator.transform.rotation);
                if (!data.TryBlend(profile, clips, active.GetCurrentAnimatorStateInfo(0).normalizedTime,
                    inverse * animator.GetBoneTransform(HumanBodyBones.LeftFoot).rotation,
                    inverse * animator.GetBoneTransform(HumanBodyBones.RightFoot).rotation, inputs, out var pose))
                    throw new InvalidOperationException("Idle–walk support blend did not resolve.");
                var target = Vector2.zero;
                float total = inputs.Sum(input => input.weight);
                foreach (var input in inputs)
                    target += Vector2.LerpUnclamped(input.geometry.heights[input.index], input.geometry.heights[input.index + 1], input.fraction) * (input.weight / total);
                targets.Add(target);
                NativeFootGoalCurves.Apply(animator, pose);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1);
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 1);
                animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 1);
            });
        if (targets.Count != solved.Length) throw new InvalidOperationException("Unexpected number of IK evaluations.");
        var row = new Case
        {
            walkWeight = weight, samples = solved.Length,
            minLowerSole = float.PositiveInfinity, maxLowerSole = float.NegativeInfinity
        };
        for (int i = 0; i < solved.Length; ++i)
        {
            var pose = solved[i];
            var original = reference[i];
            row.maxSoleError = Mathf.Max(row.maxSoleError, Mathf.Abs(pose.leftSole.y - targets[i].x), Mathf.Abs(pose.rightSole.y - targets[i].y));
            row.maxHipsError = Mathf.Max(row.maxHipsError, Vector3.Distance(pose.body, original.body));
            float lower = Mathf.Min(pose.leftSole.y, pose.rightSole.y);
            row.minLowerSole = Mathf.Min(row.minLowerSole, lower);
            row.maxLowerSole = Mathf.Max(row.maxLowerSole, lower);
            if (weight != 0) continue;
            row.maxIdleFootHorizontalError = Mathf.Max(row.maxIdleFootHorizontalError,
                Vector3.ProjectOnPlane(pose.leftFoot - original.leftFoot, Vector3.up).magnitude,
                Vector3.ProjectOnPlane(pose.rightFoot - original.rightFoot, Vector3.up).magnitude);
            row.maxIdleFootRotation = Mathf.Max(row.maxIdleFootRotation, Quaternion.Angle(pose.leftRotation, original.leftRotation), Quaternion.Angle(pose.rightRotation, original.rightRotation));
        }
        return row;
    }
}
