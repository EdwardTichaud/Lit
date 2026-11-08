using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

/// <summary>Authors one phase-specific combat brake in memory and checks its Humanoid round trip.</summary>
public static class LucianCombatBrakePilot
{
    const string Folder = "Library/LucianCoherence";
    const string PilotPath = "Assets/Characters/1_Squad/Lucian/Animation/CoherencePilot/Lucian_CombatBrake_FL_RightSupport_Pilot.anim";
    const string TransferPath = "Assets/Characters/1_Squad/Lucian/Animation/CoherencePilot/Lucian_CombatBrake_FL_WeightTransfer_Pilot.anim";
    const int Steps = 120;
    const int TransferSample = 92;

    [Serializable] sealed class DonorMatch
    {
        public string pair;
        public float sourcePhase, targetPhase;
    }

    [Serializable] sealed class Donors
    {
        public string stopClip, combatIdleClip, approachClip;
        public DonorMatch[] matches;
    }

    [Serializable] sealed class Result
    {
        public string status;
        public float duration, rightStanceSolePath, leftStanceSolePath, minimumSole, maximumLowerSole;
        public float endpointFeet, endpointHips, endpointHipsAngle, rootDistance, rootYaw;
        public float maximumGoalError, maximumSwingLift;
        public bool reconstructedLift;
        public string previewAsset;
        public float persistedPoseDifference, persistedRotationDifference;
        public float peakSwingPelvisToSupport, subframeMinimumSole, subframeMaximumLowerSole;
    }

    [Serializable] sealed class CapturePose
    {
        public string series;
        public int frame;
        public Vector3 leftFoot, rightFoot, pelvis;
    }

    [Serializable] sealed class CaptureReport
    {
        public List<CapturePose> poses = new List<CapturePose>();
    }

    public static void Run() => Run(false);
    public static void PublishPilot() => Run(true);
    public static void PublishWeightTransferPilot()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("The comparison requires a dedicated batch editor.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Run(true, weightTransfer: true);
    }
    public static void RenderComparison()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("The comparison requires a dedicated batch editor.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Run(false, renderComparison: true);
    }

    static void Run(bool publish, bool renderComparison = false, bool weightTransfer = false)
    {
        var donors = JsonUtility.FromJson<Donors>(File.ReadAllText(Folder + "/brake-donor-audit.json"));
        var match = donors.matches.Single(m => m.pair == "Approach to CombatIdle");
        var actor = ActorAnimationAudit.Actor(false);
        var profile = AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        NativeFootSupportPreparation.Validate(profile, profile.footSupports, true);
        var data = profile.footSupports;
        var approach = AssetDatabase.LoadAssetAtPath<AnimationClip>(donors.approachClip);
        var stop = ActorAnimationAudit.Source(donors.stopClip);
        var idle = ActorAnimationAudit.Source(donors.combatIdleClip);
        using var sampler = new ActorPoseSampler(actor);
        var from = ReadPose(sampler, approach, match.sourcePhase, true, out var start);
        var to = ReadPose(sampler, idle, match.targetPhase, false, out var end);
        var donorFrames = sampler.Sample(stop, sampleCount: Steps, measureSoles: true, applyFootIK: true);
        string outputAsset = weightTransfer ? TransferPath : PilotPath;
        var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(outputAsset), frameRate = 60 };
        var settings = AnimationUtility.GetAnimationClipSettings(stop);
        settings.loopTime = settings.loopBlend = false;
        settings.loopBlendOrientation = settings.loopBlendPositionY = settings.loopBlendPositionXZ = true;
        settings.keepOriginalOrientation = settings.keepOriginalPositionY = settings.keepOriginalPositionXZ = true;
        settings.heightFromFeet = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        try
        {
            var phases = Enumerable.Range(0, Steps + 1).Select(i => i / (float)Steps).ToArray();
            float[] lifted = phases.Select((p, i) => Mathf.Max(0, donorFrames[i].leftSole.y -
                Mathf.Lerp(donorFrames[0].leftSole.y, donorFrames[Steps].leftSole.y, p))).ToArray();
            bool reconstructedLift = lifted.Max() < .005f;
            if (reconstructedLift)
            {
                int peak = Enumerable.Range(0, Steps + 1).OrderByDescending(i => donorFrames[i].leftSole.y).First();
                if (peak == Steps || donorFrames[peak].leftSole.y - donorFrames[Steps].leftSole.y < .02f)
                    throw new InvalidOperationException("The donor does not contain a usable left-foot descent.");
                for (int i = 0; i <= Steps; ++i)
                {
                    // The existing stop starts in the air. Use its descent as the
                    // shape for both the authored lift and the following landing.
                    float progress = Mathf.Abs(2 * phases[i] - 1);
                    float sample = Mathf.Lerp(peak, Steps, progress);
                    int k = Mathf.Min(Steps - 1, (int)sample);
                    float y = Mathf.Lerp(donorFrames[k].leftSole.y, donorFrames[k + 1].leftSole.y, sample - k);
                    lifted[i] = Mathf.Max(0, y - donorFrames[Steps].leftSole.y);
                }
            }
            // Land the left foot before the right foot changes its placement.
            // This is authored into the clip and does not depend on a runtime anchor.
            var fullLift = lifted;
            lifted = new float[Steps + 1];
            for (int i = 0; i < TransferSample; ++i)
            {
                float index = i * Steps / (float)TransferSample;
                int lower = Mathf.FloorToInt(index);
                lifted[i] = Mathf.Lerp(fullLift[lower], fullLift[lower + 1], index - lower);
            }
            float rightStepHeight = donorFrames.Max(f => f.rightSole.y) - donorFrames.Min(f => f.rightSole.y);
            var rightLift = new float[Steps + 1];
            var swing = new float[Steps + 1];
            for (int i = 1; i <= Steps; ++i) swing[i] = swing[i - 1] + (lifted[i - 1] + lifted[i]) * .5f;
            if (swing[Steps] < .001f) throw new InvalidOperationException("The donor has no measurable swing.");
            float floor = LucianContactBlendAudit.Floor;
            Vector3 Ground(Vector3 ankle, Vector3 sole) => ankle + Vector3.up * (floor - sole.y);
            var leftStart = Ground(start.leftFoot, start.leftSole);
            var leftEnd = Ground(end.leftFoot, end.leftSole);
            var rightStart = Ground(start.rightFoot, start.rightSole);
            var rightEnd = Ground(end.rightFoot, end.rightSole);

            float Ease(float p) => Mathf.SmoothStep(0, 1, p);
            float duration = stop.length;
            Vector3 BodyPosition(float p)
            {
                var body = Vector3.Lerp(from.bodyPosition, to.bodyPosition, Ease(p));
                if (!weightTransfer) return body;
                var hips = Vector3.Lerp(start.body, end.body, Ease(p));
                float transfer = Ease(Mathf.InverseLerp(.58f, .92f, p));
                var support = Vector3.Lerp(rightStart, leftEnd, transfer);
                var towardSupport = Vector3.ProjectOnPlane(support - hips, Vector3.up);
                float envelope = Mathf.Sin(p * Mathf.PI);
                return body + Vector3.ClampMagnitude(towardSupport * .48f, .12f) *
                    (envelope * envelope / actor.humanScale);
            }
            Write(clip, "RootT.", "xyz", LucianFootGoalExport.Positions(phases.Select(BodyPosition).ToArray()), duration);
            Write(clip, "RootQ.", "xyzw", LucianFootGoalExport.Rotations(phases.Select(p => Quaternion.Slerp(from.bodyRotation, to.bodyRotation, Ease(p))).ToArray()), duration);
            for (int muscle = 0; muscle < HumanTrait.MuscleCount; ++muscle)
            {
                var curve = new AnimationCurve(phases.Select(p => new Keyframe(p * duration, Mathf.Lerp(from.muscles[muscle], to.muscles[muscle], Ease(p)))).ToArray());
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), HumanTrait.MuscleName[muscle]), curve);
            }

            var goals = phases.Select((p, i) =>
            {
                float step = renderComparison ? Ease(p) : swing[i] / swing[Steps];
                float transfer = renderComparison ? p : Mathf.InverseLerp(TransferSample, Steps, i);
                rightLift[i] = renderComparison ? 0 : Mathf.Sin(Mathf.PI * transfer) * rightStepHeight;
                if (renderComparison) lifted[i] = 0;
                return new NativeFootGoalCurves.Pose
                {
                    left = Vector3.Lerp(leftStart, leftEnd, step) + Vector3.up * lifted[i],
                    right = Vector3.Lerp(rightStart, rightEnd, Ease(transfer)) + Vector3.up * rightLift[i],
                    leftRotation = Quaternion.Slerp(start.leftRotation, end.leftRotation, step) * Quaternion.Inverse(data.leftGoalToBone),
                    rightRotation = Quaternion.Slerp(start.rightRotation, end.rightRotation, Ease(transfer)) * Quaternion.Inverse(data.rightGoalToBone)
                };
            }).ToArray();

            var leftT = new Vector3[Steps + 1]; var rightT = new Vector3[Steps + 1];
            var leftQ = new Quaternion[Steps + 1]; var rightQ = new Quaternion[Steps + 1];
            var bodyRotations = new Quaternion[Steps + 1];
            int evaluation = 0;
            HumanPoseHandler handler = null;
            try
            {
                sampler.Sample(clip, sampleCount: Steps, applyFootIK: true, modifyIKGoals: animator =>
                {
                    NativeFootGoalCurves.Apply(animator, goals[evaluation]);
                    foreach (var foot in new[] { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot })
                    {
                        animator.SetIKPositionWeight(foot, 1);
                        animator.SetIKRotationWeight(foot, 1);
                    }
                }, poseCorrection: animator =>
                {
                    handler ??= new HumanPoseHandler(animator.avatar, animator.transform);
                    var pose = new HumanPose();
                    handler.GetHumanPose(ref pose);
                    bodyRotations[evaluation] = pose.bodyRotation;
                    leftT[evaluation] = pose.ikGoalPositions[0]; rightT[evaluation] = pose.ikGoalPositions[1];
                    leftQ[evaluation] = pose.internalIkGoalRotations[0]; rightQ[evaluation] = pose.internalIkGoalRotations[1];
                    ++evaluation;
                });
            }
            finally { handler?.Dispose(); }
            if (evaluation != Steps + 1) throw new InvalidOperationException("The full brake was not sampled.");
            Write(clip, "LeftFootT.", "xyz", LucianFootGoalExport.Positions(leftT), duration);
            Write(clip, "RightFootT.", "xyz", LucianFootGoalExport.Positions(rightT), duration);
            Write(clip, "LeftFootQ.", "xyzw", LucianFootGoalExport.Rotations(leftQ), duration);
            Write(clip, "RightFootQ.", "xyzw", LucianFootGoalExport.Rotations(rightQ), duration);
            clip.EnsureQuaternionContinuity();

            var baked = sampler.Sample(clip, sampleCount: Steps, measureSoles: true, applyFootIK: true);
            // The encoded goal frame and the solved bone position can differ.
            // Fit the authored world targets back into the measured body frame.
            for (int pass = 0; pass < 3; ++pass)
            {
                for (int i = 0; i <= Steps; ++i)
                {
                    var goal = goals[i];
                    goal.right.y += floor + rightLift[i] - baked[i].rightSole.y;
                    goal.left.y += floor + lifted[i] - baked[i].leftSole.y;
                    goals[i] = goal;
                    var inverse = Quaternion.Inverse(bodyRotations[i]);
                    leftT[i] += inverse * (goal.left - baked[i].leftFoot) / actor.humanScale;
                    rightT[i] += inverse * (goal.right - baked[i].rightFoot) / actor.humanScale;
                }
                Write(clip, "LeftFootT.", "xyz", LucianFootGoalExport.Positions(leftT), duration);
                Write(clip, "RightFootT.", "xyz", LucianFootGoalExport.Positions(rightT), duration);
                baked = sampler.Sample(clip, sampleCount: Steps, measureSoles: true, applyFootIK: true);
            }
            if (renderComparison)
            {
                var pilot = AssetDatabase.LoadAssetAtPath<AnimationClip>(PilotPath);
                if (pilot == null || Mathf.Abs(pilot.length - duration) > .00001f)
                    throw new InvalidOperationException("The published brake preview is missing or has a different duration.");
                sampler.Dispose();
                CaptureComparison(actor, clip, pilot, "combat-brake-visual");
                return;
            }
            var result = new Result
            {
                duration = clip.length, maximumSwingLift = lifted.Max(), reconstructedLift = reconstructedLift,
                minimumSole = baked.Min(f => Mathf.Min(f.leftSole.y, f.rightSole.y)) - floor,
                maximumLowerSole = baked.Max(f => Mathf.Min(f.leftSole.y, f.rightSole.y)) - floor,
                endpointHips = Mathf.Max(Vector3.Distance(start.body, baked[0].body), Vector3.Distance(end.body, baked[Steps].body)),
                endpointHipsAngle = Mathf.Max(Quaternion.Angle(start.bodyRotation, baked[0].bodyRotation), Quaternion.Angle(end.bodyRotation, baked[Steps].bodyRotation))
            };
            int swingPeak = Array.IndexOf(lifted, lifted.Max());
            result.peakSwingPelvisToSupport = Vector3.ProjectOnPlane(baked[swingPeak].body - baked[swingPeak].rightFoot, Vector3.up).magnitude;
            var subframes = sampler.Sample(clip, sampleCount: Steps * 2 + 1, measureSoles: true, applyFootIK: true);
            result.subframeMinimumSole = subframes.Min(f => Mathf.Min(f.leftSole.y, f.rightSole.y)) - floor;
            result.subframeMaximumLowerSole = subframes.Max(f => Mathf.Min(f.leftSole.y, f.rightSole.y)) - floor;
            for (int i = 0; i <= Steps; ++i)
            {
                float error = Mathf.Max(Vector3.Distance(baked[i].leftFoot, goals[i].left), Vector3.Distance(baked[i].rightFoot, goals[i].right));
                result.maximumGoalError = Mathf.Max(result.maximumGoalError, error);
                if (i == 0 || i == Steps) result.endpointFeet = Mathf.Max(result.endpointFeet, error);
                if (i == 0) continue;
                if (i <= TransferSample)
                    result.rightStanceSolePath += Vector3.ProjectOnPlane(baked[i].rightSole - baked[i - 1].rightSole, Vector3.up).magnitude;
                else
                    result.leftStanceSolePath += Vector3.ProjectOnPlane(baked[i].leftSole - baked[i - 1].leftSole, Vector3.up).magnitude;
            }
            using (var motion = new PlayerInPlaceSampling(actor))
            {
                var measured = motion.Sample(clip);
                result.rootDistance = measured.MaxDisplacement;
                result.rootYaw = measured.MaxYaw;
            }
            result.status = result.endpointHips <= .01f && result.endpointHipsAngle <= 3 && result.maximumGoalError <= .01f &&
                result.minimumSole >= -.02f && result.maximumLowerSole <= .02f &&
                result.subframeMinimumSole >= -.02f && result.subframeMaximumLowerSole <= .02f &&
                result.rightStanceSolePath <= .03f && result.leftStanceSolePath <= .03f &&
                result.rootDistance <= .005f && result.rootYaw <= .1f ? "PASS_OFFLINE" : "REVIEW";
            if (publish && result.status == "PASS_OFFLINE")
                SavePilot(outputAsset, clip, baked, sampler, result);
            File.WriteAllText(Folder + (weightTransfer ? "/combat-brake-weight-transfer.json" : "/combat-brake-pilot.json"), JsonUtility.ToJson(result, true));
            if (publish && result.status != "PASS_OFFLINE")
                throw new InvalidOperationException("The brake preview did not pass the numerical bounds.");
            if (publish && weightTransfer)
            {
                sampler.Dispose();
                CaptureComparison(actor, AssetDatabase.LoadAssetAtPath<AnimationClip>(PilotPath),
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(outputAsset), "combat-brake-weight-transfer-visual");
            }
        }
        finally { Object.DestroyImmediate(clip); }
    }

    static void CaptureComparison(Animator actor, AnimationClip direct, AnimationClip pilot, string outputName)
    {
        if (direct == null || pilot == null) throw new InvalidOperationException("The comparison clips are missing.");
        string output = Folder + "/" + outputName;
        Directory.CreateDirectory(output);
        Material material = null;
        try
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = Vector3.up * (LucianContactBlendAudit.Floor - .05f);
            floor.transform.localScale = new Vector3(12, .1f, 12);
            material = new Material(Shader.Find("HDRP/Lit"));
            material.SetColor("_BaseColor", new Color(.32f, .35f, .38f));
            material.SetFloat("_Smoothness", 0);
            floor.GetComponent<Renderer>().sharedMaterial = material;

            var camera = new GameObject("Brake inspection camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 34;
            camera.backgroundColor = new Color(.12f, .16f, .20f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            ActorAnimationLabValidation.ConfigureInspectionCamera(camera);
            var hd = camera.GetComponent<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR = camera.backgroundColor;
            foreach (var field in new[] { FrameSettingsField.ExposureControl, FrameSettingsField.Postprocess })
            {
                hd.renderingPathCustomFrameSettings.SetEnabled(field, true);
                hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
            }
            var volume = new GameObject("Brake inspection exposure").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Characters/3_Enemy/Iluvilirae/Iluvilirae_Test_Volume.asset");
            foreach (float yaw in new[] { 155f, 225f })
            {
                var light = new GameObject("Brake inspection light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.gameObject.AddComponent<HDAdditionalLightData>();
                light.intensity = yaw == 155 ? 12000 : 2500;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(35, yaw, 0);
            }

            var keyframes = new[] { 0, 24, 48, 72, 92, 106, 120 };
            var report = new CaptureReport();
            using var sampler = new ActorPoseSampler(actor);
            foreach (var entry in new[] { (label: "direct", motion: direct), (label: "pilot", motion: pilot) })
            {
                sampler.Sample(entry.motion, sampleCount: Steps, applyFootIK: true, measureSoles: true, geometrySample: (rig, _, frame) =>
                {
                    if (!keyframes.Contains(frame)) return;
                    report.poses.Add(new CapturePose
                    {
                        series = entry.label, frame = frame,
                        leftFoot = rig.GetBoneTransform(HumanBodyBones.LeftFoot).position,
                        rightFoot = rig.GetBoneTransform(HumanBodyBones.RightFoot).position,
                        pelvis = rig.GetBoneTransform(HumanBodyBones.Hips).position
                    });
                    camera.transform.position = new Vector3(2.5f, 1.52f, 3.25f);
                    camera.transform.LookAt(new Vector3(0, 1.04f, 0));
                    RenderBakedPose(rig, camera, $"{output}/{entry.label}-{frame:D3}.png");
                });
            }
            File.WriteAllText(output + "/poses.json", JsonUtility.ToJson(report, true));
        }
        finally
        {
            if (material != null) Object.DestroyImmediate(material);
        }
    }

    static void RenderBakedPose(Animator rig, Camera camera, string path)
    {
        var originals = new List<(SkinnedMeshRenderer skin, bool hidden)>();
        var meshes = new List<Mesh>();
        var proxies = new List<GameObject>();
        try
        {
            foreach (var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skin.enabled || skin.sharedMesh == null) continue;
                var mesh = new Mesh();
                meshes.Add(mesh);
                skin.BakeMesh(mesh);
                var proxy = new GameObject(skin.name + " captured pose");
                proxies.Add(proxy);
                proxy.transform.SetParent(skin.transform, false);
                proxy.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = proxy.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = skin.sharedMaterials;
                renderer.shadowCastingMode = skin.shadowCastingMode;
                originals.Add((skin, skin.forceRenderingOff));
                skin.forceRenderingOff = true;
            }
            ActorAnimationLabValidation.RenderSnapshot(camera, path);
        }
        finally
        {
            foreach (var entry in originals) entry.skin.forceRenderingOff = entry.hidden;
            foreach (var proxy in proxies) Object.DestroyImmediate(proxy);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
        }
    }

    static void SavePilot(string path, AnimationClip clip, ActorPoseSampler.Frame[] expected, ActorPoseSampler sampler, Result result)
    {
        var persisted = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool created = persisted == null;
        try
        {
            if (created)
            {
                if (File.Exists(path)) throw new InvalidOperationException("The brake preview path is occupied.");
                persisted = Object.Instantiate(clip);
                persisted.name = clip.name;
                AssetDatabase.CreateAsset(persisted, path);
                AssetDatabase.SaveAssetIfDirty(persisted);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                persisted = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            }
            else if (EditorJsonUtility.ToJson(persisted) != EditorJsonUtility.ToJson(clip))
                throw new InvalidOperationException("The brake preview was modified. Review before replacing it.");

            if (persisted == null || !persisted.isHumanMotion || persisted.isLooping ||
                Mathf.Abs(persisted.length - clip.length) > .00001f)
                throw new InvalidOperationException("The serialized brake clip does not retain the tested contract.");
            var actual = sampler.Sample(persisted, sampleCount: Steps, measureSoles: true, applyFootIK: true);
            for (int i = 0; i < expected.Length; ++i)
            {
                result.persistedPoseDifference = Mathf.Max(result.persistedPoseDifference,
                    Vector3.Distance(actual[i].leftFoot, expected[i].leftFoot), Vector3.Distance(actual[i].rightFoot, expected[i].rightFoot),
                    Vector3.Distance(actual[i].body, expected[i].body));
                result.persistedRotationDifference = Mathf.Max(result.persistedRotationDifference,
                    Quaternion.Angle(actual[i].leftRotation, expected[i].leftRotation), Quaternion.Angle(actual[i].rightRotation, expected[i].rightRotation));
            }
            if (result.persistedPoseDifference > .0001f || result.persistedRotationDifference > .1f)
                throw new InvalidOperationException("The serialized brake pose differs from the verified in-memory pose.");
            result.previewAsset = path;
        }
        catch
        {
            if (created && persisted != null && AssetDatabase.GetAssetPath(persisted) == path)
                AssetDatabase.DeleteAsset(path);
            throw;
        }
    }

    static HumanPose ReadPose(ActorPoseSampler sampler, AnimationClip clip, float phase, bool footIk, out ActorPoseSampler.Frame frame)
    {
        int index = Mathf.Clamp(Mathf.RoundToInt(phase * Steps * 2), 0, Steps * 2);
        int i = 0;
        var pose = new HumanPose();
        var frames = sampler.Sample(clip, sampleCount: Steps * 2, measureSoles: true, applyFootIK: footIk, poseCorrection: animator =>
        {
            if (i++ != index) return;
            using var handler = new HumanPoseHandler(animator.avatar, animator.transform);
            handler.GetHumanPose(ref pose);
        });
        frame = frames[index];
        return pose;
    }

    static void Write(AnimationClip clip, string prefix, string axes, AnimationCurve[] curves, float duration)
    {
        for (int axis = 0; axis < axes.Length; ++axis)
        {
            var keys = curves[axis].keys;
            for (int i = 0; i < keys.Length; ++i)
            {
                keys[i].time *= duration;
                keys[i].inTangent /= duration;
                keys[i].outTangent /= duration;
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), prefix + axes[axis]), new AnimationCurve(keys));
        }
    }
}
