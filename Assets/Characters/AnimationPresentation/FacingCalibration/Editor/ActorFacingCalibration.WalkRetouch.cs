using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class ActorFacingCalibration
{
    [Serializable] public sealed class WalkRetouchReport
    {
        public string source, candidate, status, sourceGuid, sourceHash, metadataHash, avatarGuid;
        public long sourceFileId;
        public bool productionInstalled;
        public float maximumHipChange, maximumRootMovement, maximumUpperBodyAngle, maximumAnkleHorizontalChange;
        public float beforeRightMinimum, afterRightMinimum, maximumContactError, maximumLegCorrection;
        public float visualPelvisOffset, maximumHipTrajectoryError;
        public float subframeMaximumContactError, maximumFootRotationChange, additionalLoopSeam;
        public bool eventsPreserved;
        public int changedMuscles;
    }

    // Offline authoring on an exact avatar. No runtime foot lock, Animator IK or physical movement.
    [MenuItem("Lit/Animation Presentation/Facing Calibration/Prepare First Walk Retouch (Lab Only)")]
    public static void PrepareWalkRetouch() => PrepareWalkRetouch("Juggernaut", "Walk_L.anim", -.035f);

    [MenuItem("Lit/Animation Presentation/Facing Calibration/Prepare Four Walk Retouches (Lab Only)")]
    public static void PrepareWalkRetouches()
    {
        var failures = new List<string>();
        foreach (var item in new[] { ("Juggernaut", "Walk_L.anim", -.035f), ("Juggernaut", "Walk_B.anim", 0f),
            ("Juggernaut", "Walk_FL.anim", 0f), ("Lucian", "Walk_BR_Inplace.FBX", 0f) })
        {
            try { PrepareWalkRetouch(item.Item1, item.Item2, item.Item3); }
            catch (Exception e) { failures.Add(item.Item1 + " " + item.Item2 + ": " + e.Message); }
        }
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
    }
    static void PrepareWalkRetouch(string actorName, string clipSuffix, float pelvisOffset)
    {
        var contact = JsonUtility.FromJson<ContactReport>(File.ReadAllText(ReportFolder + "/contacts.json"))
            .cases.Single(c => !c.nativeIK && c.actor == actorName && c.clip.EndsWith(clipSuffix));
        var actor = Resolve().First(i => i.entry.actor == contact.actor).actor;
        var source = ActorAnimationAudit.Source(contact.clip);
        // A constant pose offset preserves the complete authored hip oscillation. It gives
        // near-extended legs enough reach; unlike a per-frame ground clamp it adds no bobbing.
        var report = new WalkRetouchReport { source = contact.clip, status = "Prevalidation", visualPelvisOffset = pelvisOffset };
        report.sourceHash = Hash(contact.clip); report.metadataHash = Hash(contact.clip + ".meta");
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out report.sourceGuid, out report.sourceFileId);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actor.avatar, out report.avatarGuid, out long _);
        string label = actorName + "_" + Path.GetFileNameWithoutExtension(clipSuffix);
        AnimationClip candidate = null;
        try
        {
            using (var sampler = new ActorPoseSampler(actor))
            {
                int count = Mathf.Max(120, Mathf.CeilToInt(source.length * 60));
                var before = sampler.Sample(source, sampleCount: count, measureSoles: true);
                float leftMinimum = before.Min(f => f.leftSole.y), rightMinimum = before.Min(f => f.rightSole.y);
                var muscles = Enumerable.Range(0, HumanTrait.MuscleCount).Where(i => IsLegMuscle(HumanTrait.MuscleName[i])).ToArray();
                var curves = muscles.ToDictionary(i => i, i => new AnimationCurve());
                int frame = 0;
                HumanPoseHandler handler = null;
                ActorSoleSampler soles = null;
                try
                {
                    sampler.Sample(source, sampleCount: count, measureSoles: true, poseCorrection: animator =>
                    {
                        if (handler == null)
                        {
                            handler = new HumanPoseHandler(animator.avatar, animator.transform);
                            soles = new ActorSoleSampler(animator, true);
                        }
                        soles.Sample(out var left, out var right);
                        float dyLeft = SoleCorrection(left.y, leftMinimum, contact.groundReferenceY);
                        float dyRight = SoleCorrection(right.y, rightMinimum, contact.groundReferenceY);
                        report.maximumLegCorrection = Mathf.Max(report.maximumLegCorrection, Mathf.Abs(dyLeft), Mathf.Abs(dyRight));
                        animator.GetBoneTransform(HumanBodyBones.Hips).position += Vector3.up * pelvisOffset;
                        SolveLeg(animator, true, dyLeft - pelvisOffset);
                        SolveLeg(animator, false, dyRight - pelvisOffset);
                        var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                        foreach (int muscle in muscles)
                            curves[muscle].AddKey(before[frame].time, pose.muscles[muscle]);
                        frame++;
                    });
                }
                finally { handler?.Dispose(); soles?.Dispose(); }
                candidate = new AnimationClip(); EditorUtility.CopySerialized(source, candidate);
                foreach (var pair in curves)
                {
                    for (int i = 0; i < pair.Value.length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(pair.Value, i, AnimationUtility.TangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(pair.Value, i, AnimationUtility.TangentMode.ClampedAuto);
                    }
                    AnimationUtility.SetEditorCurve(candidate, EditorCurveBinding.FloatCurve("", typeof(Animator), HumanTrait.MuscleName[pair.Key]), pair.Value);
                }
                report.changedMuscles = muscles.Length;
                // Humanoid RootT represents the body centre of mass. Changing leg muscles can
                // otherwise move the pelvis even though its authored curves were copied intact.
                // Compensate that reconstruction offset, rather than flattening the hip motion.
                for (int pass = 0; pass < 2; pass++)
                {
                    var rebuilt = sampler.Sample(candidate, sampleCount: count);
                    foreach (int axis in new[] { 0, 1, 2 })
                    {
                        var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT." + "xyz"[axis]);
                        var previous = AnimationUtility.GetEditorCurve(candidate, binding);
                        if (previous == null) throw new InvalidOperationException("Missing body centre curve");
                        var values = new AnimationCurve();
                        for (int i = 0; i <= count; i++)
                            values.AddKey(before[i].time, previous.Evaluate(before[i].time) + (before[i].body[axis] + (axis == 1 ? pelvisOffset : 0f) - rebuilt[i].body[axis]) / actor.humanScale);
                        for (int i = 0; i < values.length; i++)
                        {
                            AnimationUtility.SetKeyLeftTangentMode(values, i, AnimationUtility.TangentMode.ClampedAuto);
                            AnimationUtility.SetKeyRightTangentMode(values, i, AnimationUtility.TangentMode.ClampedAuto);
                        }
                        AnimationUtility.SetEditorCurve(candidate, binding, values);
                    }
                }
                var after = sampler.Sample(candidate, sampleCount: count, measureSoles: true);
                report.beforeRightMinimum = rightMinimum - contact.groundReferenceY;
                report.afterRightMinimum = after.Min(f => f.rightSole.y) - contact.groundReferenceY;
                for (int i = 0; i <= count; i++)
                {
                    report.maximumHipChange = Mathf.Max(report.maximumHipChange, Vector3.Distance(before[i].body, after[i].body));
                    report.maximumHipTrajectoryError = Mathf.Max(report.maximumHipTrajectoryError, Vector3.Distance(before[i].body + Vector3.up * pelvisOffset, after[i].body));
                    report.maximumRootMovement = Mathf.Max(report.maximumRootMovement, after[i].root.magnitude);
                    foreach (var bone in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm })
                        report.maximumUpperBodyAngle = Mathf.Max(report.maximumUpperBodyAngle, Quaternion.Angle(before[i].boneRotations[(int)bone], after[i].boneRotations[(int)bone]));
                    report.maximumAnkleHorizontalChange = Mathf.Max(report.maximumAnkleHorizontalChange,
                        Vector3.ProjectOnPlane(before[i].leftFoot - after[i].leftFoot, Vector3.up).magnitude,
                        Vector3.ProjectOnPlane(before[i].rightFoot - after[i].rightFoot, Vector3.up).magnitude);
                    report.maximumFootRotationChange = Mathf.Max(report.maximumFootRotationChange,
                        Quaternion.Angle(before[i].leftRotation, after[i].leftRotation), Quaternion.Angle(before[i].rightRotation, after[i].rightRotation));
                    if (before[i].leftSole.y <= leftMinimum + .015f)
                        report.maximumContactError = Mathf.Max(report.maximumContactError, Mathf.Abs(after[i].leftSole.y - contact.groundReferenceY));
                    if (before[i].rightSole.y <= rightMinimum + .015f)
                        report.maximumContactError = Mathf.Max(report.maximumContactError, Mathf.Abs(after[i].rightSole.y - contact.groundReferenceY));
                }
                foreach (bool left in new[] { true, false })
                {
                    var oldSeam = (left ? before[0].leftFoot - before[count].leftFoot : before[0].rightFoot - before[count].rightFoot).magnitude;
                    var newSeam = (left ? after[0].leftFoot - after[count].leftFoot : after[0].rightFoot - after[count].rightFoot).magnitude;
                    report.additionalLoopSeam = Mathf.Max(report.additionalLoopSeam, newSeam - oldSeam);
                }
                var subBefore = sampler.Sample(source, sampleCount: count * 2, measureSoles: true);
                var subAfter = sampler.Sample(candidate, sampleCount: count * 2, measureSoles: true);
                for (int i = 0; i < subBefore.Length; i++)
                {
                    if (subBefore[i].leftSole.y <= leftMinimum + .015f)
                        report.subframeMaximumContactError = Mathf.Max(report.subframeMaximumContactError, Mathf.Abs(subAfter[i].leftSole.y - contact.groundReferenceY));
                    if (subBefore[i].rightSole.y <= rightMinimum + .015f)
                        report.subframeMaximumContactError = Mathf.Max(report.subframeMaximumContactError, Mathf.Abs(subAfter[i].rightSole.y - contact.groundReferenceY));
                }
                report.eventsPreserved = JsonUtility.ToJson(new Entry { events = ActorAnimationAudit.EventDetails(source) }) ==
                    JsonUtility.ToJson(new Entry { events = ActorAnimationAudit.EventDetails(candidate) });
                if (report.maximumHipTrajectoryError > .001f || report.maximumRootMovement > .001f || report.maximumUpperBodyAngle > .1f ||
                    report.maximumAnkleHorizontalChange > .01f || report.maximumContactError > .02f || report.subframeMaximumContactError > .02f ||
                    report.maximumFootRotationChange > 1f || report.additionalLoopSeam > .001f || !report.eventsPreserved ||
                    Hash(contact.clip) != report.sourceHash || Hash(contact.clip + ".meta") != report.metadataHash)
                    throw new InvalidOperationException("Retouch did not pass pose/contact gates; no asset saved.");
                Directory.CreateDirectory(Folder + "/RetouchedWalks"); AssetDatabase.Refresh();
                var input = new Input { actor = actor, source = source, candidate = candidate,
                    entry = new Entry { actor = contact.actor, cycle = label + "_Retouched", target = Folder + "/RetouchedWalks/" + label + ".anim" } };
                report.candidate = input.entry.target;
                report.status = "Lab copy only: pose and vertical contact gates passed; horizontal stance and rendered style review pending";
                foreach (float phase in new[] { 0f, .25f, .5f, .75f })
                {
                    input.entry.cycle = label + "_Retouched_p" + Mathf.RoundToInt(phase * 100);
                    CaptureInputs(new List<Input> { input }, "RetouchedWalkCaptures", phase, contact.groundReferenceY, legRetouch: true, pelvisPoseOffset: pelvisOffset);
                }
                // The head-only calibration saver intentionally rejects changed feet. Keep
                // these explicit retouch gates separate rather than weakening that contract.
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(input.entry.target);
                if (existing != null)
                {
                    if (!SameAuthoring(existing, candidate)) { EditorUtility.CopySerialized(candidate, existing); EditorUtility.SetDirty(existing); }
                    Object.DestroyImmediate(candidate); candidate = null;
                    AssetDatabase.SaveAssetIfDirty(existing);
                }
                else { AssetDatabase.CreateAsset(candidate, input.entry.target); AssetDatabase.SaveAssetIfDirty(candidate); candidate = null; }
            }
        }
        catch (Exception e) { report.status = "Rejected: " + e.Message; throw; }
        finally
        {
            if (candidate != null && !AssetDatabase.Contains(candidate)) Object.DestroyImmediate(candidate);
            File.WriteAllText(ReportFolder + "/walk-retouch-" + label + ".json", JsonUtility.ToJson(report, true));
        }
    }
    static bool IsLegMuscle(string name) => new[] { "Left Upper Leg", "Right Upper Leg", "Left Lower Leg", "Right Lower Leg", "Left Foot", "Right Foot", "Left Toes", "Right Toes" }.Any(name.StartsWith);
    static float SoleCorrection(float height, float minimum, float floor)
    {
        // Keep the swing arc; a smooth envelope only edits the low portion of each authored step.
        float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.015f, .065f, height - minimum));
        return Mathf.Clamp(floor - height, -.1f, .1f) * weight;
    }
    static void SolveLeg(Animator animator, bool left, float dy)
    {
        var thigh = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
        var knee = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
        var ankle = animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        var ankleRotation = ankle.rotation;
        Vector3 origin = thigh.position, goal = ankle.position + Vector3.up * dy;
        float upper = Vector3.Distance(origin, knee.position), lower = Vector3.Distance(knee.position, ankle.position);
        var direction = (goal - origin).normalized;
        float distance = Mathf.Clamp(Vector3.Distance(origin, goal), Mathf.Abs(upper - lower) + .0001f, upper + lower - .0001f);
        var pole = Vector3.ProjectOnPlane(knee.position - origin, direction).normalized;
        if (pole.sqrMagnitude < .5f) throw new InvalidOperationException("Ambiguous knee plane");
        float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
        var kneeGoal = origin + direction * along + pole * Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
        thigh.rotation = Quaternion.FromToRotation(knee.position - origin, kneeGoal - origin) * thigh.rotation;
        knee.rotation = Quaternion.FromToRotation(ankle.position - knee.position, goal - knee.position) * knee.rotation;
        ankle.rotation = ankleRotation;
    }
}
