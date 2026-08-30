#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Samples the actual Mecanim blends used by Player_Model and reports continuity
/// problems without ever saving the loaded prefabs or animation assets.
/// </summary>
public static class PlayerModelAnimatorContinuityValidator
{
    private const string ControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private static readonly string[] PrefabPaths =
    {
        "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab",
        "Assets/Characters/1_Squad/Link/Player_Model_Link.prefab",
        "Assets/Characters/1_Squad/Luna/Player_Model_Luna.prefab",
        "Assets/Characters/1_Squad/Mia/Player_Model_Mia.prefab"
    };

    private const float SampleDeltaTime = 1f / 60f;
    private const int MaximumTransitionFrames = 180;
    private const float BonePositionTolerance = 0.08f;
    private const float BoneRotationTolerance = 18f;
    private const float PlanarVelocityDiscontinuityTolerance = 0.75f;
    private const float AngularVelocityDiscontinuityTolerance = 90f;
    private const float VerticalRootStepTolerance = 0.05f;
    private const float FootPlantSpeedTolerance = 0.10f;
    private const float FootPlantVerticalTolerance = 0.01f;
    private const float FootSlideTolerance = 0.05f;

    private sealed class StateReference
    {
        public AnimatorState State;
        public string Path;
        public int LayerIndex;
    }

    private sealed class StateMachineReference
    {
        public AnimatorStateMachine StateMachine;
        public int LayerIndex;
    }

    private sealed class TransitionReference
    {
        public AnimatorStateTransition Transition;
        public StateReference Source;
        public StateReference Destination;
        public bool IsAnyState;
        public string Label;
    }

    private sealed class FrameSample
    {
        public Vector3 RootPosition;
        public Quaternion RootRotation;
        public Vector3 RootDeltaPosition;
        public Quaternion RootDeltaRotation;
        public Vector3 LeftFoot;
        public Vector3 RightFoot;
        public readonly Dictionary<HumanBodyBones, BonePose> Bones = new Dictionary<HumanBodyBones, BonePose>();
    }

    private struct BonePose
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    private sealed class Measurement
    {
        public string Avatar;
        public string Transition;
        public string Status;
        public string Reason;
        public string WorstBone;
        public float MaxBonePosition;
        public float MaxBoneRotation;
        public float MaxPlanarVelocityChange;
        public float MaxAngularVelocityChange;
        public float MaxVerticalRootStep;
        public float MaxFootSlide;

        public bool HasWarning => Status == "Avertissement";
        public bool IsNotMeasured => Status == "Non mesuree";
    }

    [MenuItem("Lit/Animation/Validate Player_Model Continuity", priority = 120)]
    private static void Validate()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("[Player_Model Continuity] Controller introuvable : " + ControllerPath);
            return;
        }

        List<StateReference> states = new List<StateReference>();
        List<StateMachineReference> stateMachines = new List<StateMachineReference>();
        for (int layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
        {
            AnimatorControllerLayer layer = controller.layers[layerIndex];
            CollectStates(layer.stateMachine, layer.name, layerIndex, states, stateMachines);
        }

        Dictionary<AnimatorState, StateReference> stateLookup = new Dictionary<AnimatorState, StateReference>();
        for (int i = 0; i < states.Count; i++)
        {
            stateLookup[states[i].State] = states[i];
        }

        List<TransitionReference> transitions = CollectTransitions(states, stateMachines, stateLookup);
        List<Measurement> measurements = new List<Measurement>();
        List<string> structuralTransitions = CollectStructuralTransitions(states, stateMachines);

        try
        {
            for (int prefabIndex = 0; prefabIndex < PrefabPaths.Length; prefabIndex++)
            {
                EvaluatePrefab(PrefabPaths[prefabIndex], controller, transitions, measurements);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("[Player_Model Continuity] Validation interrompue : " + exception);
            return;
        }

        Report(controller, transitions.Count, structuralTransitions, measurements);
    }

    private static void CollectStates(
        AnimatorStateMachine stateMachine,
        string path,
        int layerIndex,
        List<StateReference> states,
        List<StateMachineReference> stateMachines)
    {
        if (stateMachine == null)
        {
            return;
        }

        stateMachines.Add(new StateMachineReference { StateMachine = stateMachine, LayerIndex = layerIndex });
        ChildAnimatorState[] childStates = stateMachine.states;
        for (int i = 0; i < childStates.Length; i++)
        {
            AnimatorState state = childStates[i].state;
            if (state != null)
            {
                states.Add(new StateReference { State = state, Path = path + "." + state.name, LayerIndex = layerIndex });
            }
        }

        ChildAnimatorStateMachine[] childMachines = stateMachine.stateMachines;
        for (int i = 0; i < childMachines.Length; i++)
        {
            AnimatorStateMachine child = childMachines[i].stateMachine;
            if (child != null)
            {
                CollectStates(child, path + "." + child.name, layerIndex, states, stateMachines);
            }
        }
    }

    private static List<TransitionReference> CollectTransitions(
        List<StateReference> states,
        List<StateMachineReference> stateMachines,
        Dictionary<AnimatorState, StateReference> stateLookup)
    {
        List<TransitionReference> result = new List<TransitionReference>();
        for (int i = 0; i < states.Count; i++)
        {
            AnimatorStateTransition[] transitions = states[i].State.transitions;
            for (int j = 0; j < transitions.Length; j++)
            {
                AddTransition(result, transitions[j], states[i], false, stateLookup);
            }
        }

        // Any State has no intrinsic source pose. Test it from every playable
        // state so that it cannot be silently considered valid.
        for (int machineIndex = 0; machineIndex < stateMachines.Count; machineIndex++)
        {
            StateMachineReference machine = stateMachines[machineIndex];
            AnimatorStateTransition[] transitions = machine.StateMachine.anyStateTransitions;
            for (int transitionIndex = 0; transitionIndex < transitions.Length; transitionIndex++)
            {
                for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
                {
                    if (states[stateIndex].LayerIndex == machine.LayerIndex)
                    {
                        AddTransition(result, transitions[transitionIndex], states[stateIndex], true, stateLookup);
                    }
                }
            }
        }

        return result;
    }

    private static void AddTransition(
        List<TransitionReference> result,
        AnimatorStateTransition transition,
        StateReference source,
        bool anyState,
        Dictionary<AnimatorState, StateReference> stateLookup)
    {
        if (transition == null || transition.isExit || transition.destinationState == null ||
            !stateLookup.TryGetValue(transition.destinationState, out StateReference destination))
        {
            return;
        }

        string prefix = anyState ? "Any State " : string.Empty;
        result.Add(new TransitionReference
        {
            Transition = transition,
            Source = source,
            Destination = destination,
            IsAnyState = anyState,
            Label = prefix + source.Path + " -> " + destination.Path
        });
    }

    private static List<string> CollectStructuralTransitions(List<StateReference> states, List<StateMachineReference> stateMachines)
    {
        List<string> result = new List<string>();
        for (int i = 0; i < states.Count; i++)
        {
            AnimatorStateTransition[] transitions = states[i].State.transitions;
            for (int j = 0; j < transitions.Length; j++)
            {
                if (transitions[j] != null && (transitions[j].isExit || transitions[j].destinationState == null))
                {
                    result.Add(states[i].Path + " -> Exit/StateMachine");
                }
            }
        }

        for (int i = 0; i < stateMachines.Count; i++)
        {
            AnimatorTransition[] entries = stateMachines[i].StateMachine.entryTransitions;
            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                result.Add("Entry -> " + stateMachines[i].StateMachine.name);
            }
        }

        return result;
    }

    private static void EvaluatePrefab(
        string prefabPath,
        AnimatorController controller,
        List<TransitionReference> transitions,
        List<Measurement> measurements)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Animator animator = FindAnimator(root, controller);
            if (animator == null || !animator.isHuman)
            {
                measurements.Add(new Measurement
                {
                    Avatar = prefabPath,
                    Transition = "Tous",
                    Status = "Non mesuree",
                    Reason = "Animator Humanoid utilisant Player_Model.controller introuvable."
                });
                return;
            }

            List<MonoBehaviour> disabledBehaviours = DisableEvaluationBehaviours(root);
            try
            {
                animator.enabled = true;
                animator.fireEvents = false;
                animator.applyRootMotion = true;
                for (int i = 0; i < transitions.Count; i++)
                {
                    measurements.Add(EvaluateTransition(animator, controller, transitions[i], System.IO.Path.GetFileNameWithoutExtension(prefabPath)));
                }
            }
            finally
            {
                for (int i = 0; i < disabledBehaviours.Count; i++)
                {
                    if (disabledBehaviours[i] != null)
                    {
                        disabledBehaviours[i].enabled = true;
                    }
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Animator FindAnimator(GameObject root, AnimatorController controller)
    {
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i].runtimeAnimatorController == controller)
            {
                return animators[i];
            }
        }

        return null;
    }

    private static List<MonoBehaviour> DisableEvaluationBehaviours(GameObject root)
    {
        // Do not request the base Behaviour type here. Some imported prefab
        // components are native Behaviour subclasses and Unity can return a
        // heterogeneous internal array for that query in edit mode.
        List<MonoBehaviour> disabled = new List<MonoBehaviour>();
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null || !behaviours[i].enabled)
            {
                continue;
            }

            behaviours[i].enabled = false;
            disabled.Add(behaviours[i]);
        }

        return disabled;
    }

    private static Measurement EvaluateTransition(Animator animator, AnimatorController controller, TransitionReference transition, string avatar)
    {
        Measurement measurement = new Measurement { Avatar = avatar, Transition = transition.Label, Status = "Conforme" };
        Transform root = animator.transform;
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ResetParameters(animator, controller);

        int layer = transition.Source.LayerIndex;
        float sourceTime = transition.Transition.hasExitTime
            ? Mathf.Max(0f, transition.Transition.exitTime - 0.001f)
            : 0.5f;
        int sourceHash = Animator.StringToHash(transition.Source.Path);
        animator.Play(sourceHash, layer, sourceTime);
        animator.Update(0f);
        if (animator.GetCurrentAnimatorStateInfo(layer).fullPathHash != sourceHash)
        {
            measurement.Status = "Non mesuree";
            measurement.Reason = "Etat source introuvable dans sa layer Mecanim.";
            return measurement;
        }
        ApplyConditions(animator, controller, transition.Transition.conditions);

        List<FrameSample> samples = new List<FrameSample>();
        samples.Add(Capture(animator));
        bool reachedExpectedDestination = false;
        int expectedHash = Animator.StringToHash(transition.Destination.Path);
        for (int frame = 0; frame < MaximumTransitionFrames; frame++)
        {
            animator.Update(SampleDeltaTime);
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
            if (current.fullPathHash == expectedHash || next.fullPathHash == expectedHash)
            {
                reachedExpectedDestination = true;
            }

            samples.Add(Capture(animator));
            if (reachedExpectedDestination && !animator.IsInTransition(layer) && current.fullPathHash == expectedHash)
            {
                break;
            }
        }

        if (!reachedExpectedDestination)
        {
            measurement.Status = "Non mesuree";
            measurement.Reason = "Transition non declenchee ou destination ambigue avec les conditions du controller.";
            return measurement;
        }

        MeasureContinuity(samples, measurement);
        return measurement;
    }

    private static void ResetParameters(Animator animator, AnimatorController controller)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Float:
                    animator.SetFloat(parameter.name, parameter.defaultFloat);
                    break;
                case AnimatorControllerParameterType.Int:
                    animator.SetInteger(parameter.name, parameter.defaultInt);
                    break;
                case AnimatorControllerParameterType.Bool:
                    animator.SetBool(parameter.name, parameter.defaultBool);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    animator.ResetTrigger(parameter.name);
                    break;
            }
        }
    }

    private static void ApplyConditions(Animator animator, AnimatorController controller, AnimatorCondition[] conditions)
    {
        const float epsilon = 0.01f;
        for (int i = 0; i < conditions.Length; i++)
        {
            AnimatorCondition condition = conditions[i];
            AnimatorControllerParameter parameter = FindParameter(controller, condition.parameter);
            if (parameter == null)
            {
                continue;
            }

            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(parameter.name);
                continue;
            }

            if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(parameter.name, condition.mode == AnimatorConditionMode.If);
                continue;
            }

            float value = ResolveConditionValue(condition, epsilon);
            if (parameter.type == AnimatorControllerParameterType.Int)
            {
                animator.SetInteger(parameter.name, Mathf.RoundToInt(value));
            }
            else
            {
                animator.SetFloat(parameter.name, value);
            }
        }
    }

    private static AnimatorControllerParameter FindParameter(AnimatorController controller, string name)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name)
            {
                return parameters[i];
            }
        }

        return null;
    }

    private static float ResolveConditionValue(AnimatorCondition condition, float epsilon)
    {
        switch (condition.mode)
        {
            case AnimatorConditionMode.Greater:
                return condition.threshold + epsilon;
            case AnimatorConditionMode.Less:
                return condition.threshold - epsilon;
            case AnimatorConditionMode.NotEqual:
                return condition.threshold + epsilon;
            default:
                return condition.threshold;
        }
    }

    private static FrameSample Capture(Animator animator)
    {
        FrameSample sample = new FrameSample
        {
            RootPosition = animator.transform.position,
            RootRotation = animator.transform.rotation,
            RootDeltaPosition = animator.deltaPosition,
            RootDeltaRotation = animator.deltaRotation
        };
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null)
        {
            return sample;
        }

        for (HumanBodyBones bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
        {
            Transform transform = animator.GetBoneTransform(bone);
            if (transform == null)
            {
                continue;
            }

            sample.Bones[bone] = new BonePose
            {
                Position = hips.InverseTransformPoint(transform.position),
                Rotation = Quaternion.Inverse(hips.rotation) * transform.rotation
            };
        }

        Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        sample.LeftFoot = leftFoot != null ? leftFoot.position : Vector3.zero;
        sample.RightFoot = rightFoot != null ? rightFoot.position : Vector3.zero;
        return sample;
    }

    private static void MeasureContinuity(List<FrameSample> samples, Measurement measurement)
    {
        if (samples.Count < 3)
        {
            measurement.Status = "Non mesuree";
            measurement.Reason = "Echantillons insuffisants.";
            return;
        }

        float previousPlanarSpeed = 0f;
        float previousAngularSpeed = 0f;
        bool hasVelocity = false;
        for (int index = 1; index < samples.Count; index++)
        {
            FrameSample previous = samples[index - 1];
            FrameSample current = samples[index];
            foreach (KeyValuePair<HumanBodyBones, BonePose> pair in current.Bones)
            {
                if (!previous.Bones.TryGetValue(pair.Key, out BonePose previousPose))
                {
                    continue;
                }

                float positionDelta = Vector3.Distance(previousPose.Position, pair.Value.Position);
                float rotationDelta = Quaternion.Angle(previousPose.Rotation, pair.Value.Rotation);
                if (positionDelta > measurement.MaxBonePosition || rotationDelta > measurement.MaxBoneRotation)
                {
                    measurement.WorstBone = pair.Key.ToString();
                }
                measurement.MaxBonePosition = Mathf.Max(measurement.MaxBonePosition, positionDelta);
                measurement.MaxBoneRotation = Mathf.Max(measurement.MaxBoneRotation, rotationDelta);
            }

            Vector3 rootDelta = current.RootDeltaPosition;
            float planarSpeed = new Vector2(rootDelta.x, rootDelta.z).magnitude / SampleDeltaTime;
            float angularSpeed = Quaternion.Angle(Quaternion.identity, current.RootDeltaRotation) / SampleDeltaTime;
            if (hasVelocity)
            {
                measurement.MaxPlanarVelocityChange = Mathf.Max(measurement.MaxPlanarVelocityChange, Mathf.Abs(planarSpeed - previousPlanarSpeed));
                measurement.MaxAngularVelocityChange = Mathf.Max(measurement.MaxAngularVelocityChange, Mathf.Abs(angularSpeed - previousAngularSpeed));
            }
            previousPlanarSpeed = planarSpeed;
            previousAngularSpeed = angularSpeed;
            hasVelocity = true;
            measurement.MaxVerticalRootStep = Mathf.Max(measurement.MaxVerticalRootStep, Mathf.Abs(rootDelta.y));
        }

        measurement.MaxFootSlide = Mathf.Max(MeasureFootSlide(samples, true), MeasureFootSlide(samples, false));
        if (measurement.MaxBonePosition > BonePositionTolerance ||
            measurement.MaxBoneRotation > BoneRotationTolerance ||
            measurement.MaxPlanarVelocityChange > PlanarVelocityDiscontinuityTolerance ||
            measurement.MaxAngularVelocityChange > AngularVelocityDiscontinuityTolerance ||
            measurement.MaxVerticalRootStep > VerticalRootStepTolerance ||
            measurement.MaxFootSlide > FootSlideTolerance)
        {
            measurement.Status = "Avertissement";
            measurement.Reason = "Seuil de continuite depasse.";
        }
    }

    private static float MeasureFootSlide(List<FrameSample> samples, bool leftFoot)
    {
        Vector3 first = leftFoot ? samples[0].LeftFoot : samples[0].RightFoot;
        Vector3 second = leftFoot ? samples[1].LeftFoot : samples[1].RightFoot;
        Vector3 penultimate = leftFoot ? samples[samples.Count - 2].LeftFoot : samples[samples.Count - 2].RightFoot;
        Vector3 last = leftFoot ? samples[samples.Count - 1].LeftFoot : samples[samples.Count - 1].RightFoot;
        float startSpeed = Vector3.Distance(first, second) / SampleDeltaTime;
        float endSpeed = Vector3.Distance(penultimate, last) / SampleDeltaTime;
        float verticalVariation = Mathf.Abs(first.y - last.y);
        if (startSpeed > FootPlantSpeedTolerance || endSpeed > FootPlantSpeedTolerance || verticalVariation > FootPlantVerticalTolerance)
        {
            return 0f;
        }

        return new Vector2(last.x - first.x, last.z - first.z).magnitude;
    }

    private static void Report(AnimatorController controller, int testedTransitions, List<string> structuralTransitions, List<Measurement> measurements)
    {
        int warnings = 0;
        int notMeasured = structuralTransitions.Count;
        int contextualAnyStateCases = 0;
        for (int i = 0; i < measurements.Count; i++)
        {
            if (measurements[i].Transition.StartsWith("Any State ", StringComparison.Ordinal))
            {
                contextualAnyStateCases++;
                continue;
            }

            warnings += measurements[i].HasWarning ? 1 : 0;
            notMeasured += measurements[i].IsNotMeasured ? 1 : 0;
        }

        Debug.Log($"[Player_Model Continuity] controller={controller.name} cas-evalues={testedTransitions} avatars={PrefabPaths.Length} avertissements={warnings} non-mesurees={notMeasured} (dont structurelles={structuralTransitions.Count}) any-state-contextuels={contextualAnyStateCases}.");
        for (int i = 0; i < structuralTransitions.Count; i++)
        {
            Debug.LogWarning("[Player_Model Continuity] Non mesuree | transition=" + structuralTransitions[i] + " | aucune pose source ou destination unique.");
        }
        for (int i = 0; i < measurements.Count; i++)
        {
            Measurement result = measurements[i];
            // An Any State edge deliberately allows unrelated actions to be
            // interrupted. Its source-dependent pose gap is useful telemetry,
            // but it is not an actionable continuity failure by itself.
            if (result.Transition.StartsWith("Any State ", StringComparison.Ordinal) ||
                (!result.HasWarning && !result.IsNotMeasured))
            {
                continue;
            }

            Debug.LogWarning(
                $"[Player_Model Continuity] {result.Status} | avatar={result.Avatar} | transition={result.Transition} | " +
                $"os={result.WorstBone ?? "-"} pos={result.MaxBonePosition:F3}m rot={result.MaxBoneRotation:F1}deg " +
                $"rootPlan={result.MaxPlanarVelocityChange:F3}m/s rootYaw={result.MaxAngularVelocityChange:F1}deg/s " +
                $"rootY={result.MaxVerticalRootStep:F3}m pied={result.MaxFootSlide:F3}m | {result.Reason}");
        }
    }
}
#endif
