using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Builds the direct-play production graph without importing unrelated legacy transitions.</summary>
internal static class ProductionBrainsAnimatorGraph
{
    public static AnimatorController Create(AnimatorController source,
        IEnumerable<KeyValuePair<string, AnimatorState>> requiredStates, string idlePath, string assetPath)
    {
        var required = requiredStates.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
        string rootPath = source.layers[0].name;
        if (!idlePath.StartsWith(rootPath + ".", StringComparison.Ordinal) || idlePath.Substring(rootPath.Length + 1).Contains('.'))
            throw new InvalidOperationException("Production entry requires the exact authored root-layer idle state.");
        foreach (var entry in required)
        {
            if (entry.Value == null || entry.Value.motion == null || entry.Value.behaviours.Length != 0)
                throw new InvalidOperationException("Required production state needs an explicit motion/behaviour adapter: " + entry.Key);
            ValidateSourceMotion(entry.Value.motion, new HashSet<Motion>());
        }
        var controller = AnimatorController.CreateAnimatorControllerAtPath(assetPath);
        controller.parameters = source.parameters.Select(parameter => new AnimatorControllerParameter {
            name = parameter.name, type = parameter.type, defaultBool = parameter.defaultBool,
            defaultFloat = parameter.defaultFloat, defaultInt = parameter.defaultInt
        }).ToArray();
        var layers = controller.layers;
        layers[0].name = rootPath;
        layers[0].avatarMask = source.layers[0].avatarMask;
        layers[0].blendingMode = source.layers[0].blendingMode;
        layers[0].defaultWeight = source.layers[0].defaultWeight;
        layers[0].iKPass = source.layers[0].iKPass;
        controller.layers = layers;
        var root = controller.layers[0].stateMachine;
        root.name = rootPath;
        var machines = new Dictionary<string, AnimatorStateMachine> { { rootPath, root } };
        var motions = new Dictionary<Motion, Motion>();
        int index = 0;
        foreach (var entry in required)
        {
            string parentPath = entry.Key.Substring(0, entry.Key.LastIndexOf('.'));
            var machine = EnsureMachine(parentPath, rootPath, machines, controller);
            var original = entry.Value;
            var state = machine.AddState(original.name, new Vector3(280, 80 + index++ * 70));
            EnsureOwned(state, controller);
            state.motion = CloneMotion(original.motion, controller, motions);
            state.tag = original.tag;
            state.speed = original.speed;
            state.cycleOffset = original.cycleOffset;
            state.mirror = original.mirror;
            state.iKOnFeet = original.iKOnFeet;
            state.writeDefaultValues = original.writeDefaultValues;
            state.speedParameter = original.speedParameter;
            state.speedParameterActive = original.speedParameterActive;
            state.cycleOffsetParameter = original.cycleOffsetParameter;
            state.cycleOffsetParameterActive = original.cycleOffsetParameterActive;
            state.mirrorParameter = original.mirrorParameter;
            state.mirrorParameterActive = original.mirrorParameterActive;
            state.timeParameter = original.timeParameter;
            state.timeParameterActive = original.timeParameterActive;
            if (entry.Key == idlePath) root.defaultState = state;
            EditorUtility.SetDirty(state);
        }
        foreach (var machine in machines.Values) EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        Validate(controller);
        return controller;
    }

    private static AnimatorStateMachine EnsureMachine(string path, string rootPath,
        Dictionary<string, AnimatorStateMachine> machines, AnimatorController controller)
    {
        if (machines.TryGetValue(path, out var existing)) return existing;
        if (!path.StartsWith(rootPath + ".", StringComparison.Ordinal)) throw new InvalidOperationException("State belongs to another layer: " + path);
        int separator = path.LastIndexOf('.');
        var parent = EnsureMachine(path.Substring(0, separator), rootPath, machines, controller);
        var machine = parent.AddStateMachine(path.Substring(separator + 1));
        EnsureOwned(machine, controller);
        machines.Add(path, machine);
        return machine;
    }

    private static void EnsureOwned(UnityEngine.Object value, AnimatorController controller)
    {
        if (!AssetDatabase.Contains(value)) AssetDatabase.AddObjectToAsset(value, controller);
        if (AssetDatabase.GetAssetPath(value) != AssetDatabase.GetAssetPath(controller))
            throw new InvalidOperationException("Generated graph object is not owned by its controller: " + value.name);
    }

    private static void ValidateSourceMotion(Motion motion, HashSet<Motion> stack)
    {
        if (motion == null) throw new InvalidOperationException("Required source motion contains an unresolved child reference.");
        if (!(motion is BlendTree tree))
        {
            if (!(motion is AnimationClip) || !AssetDatabase.Contains(motion))
                throw new InvalidOperationException("Required source motion is not a persistent authored clip: " + motion.name);
            return;
        }
        if (!stack.Add(tree)) throw new InvalidOperationException("Required source BlendTree is cyclic: " + tree.name);
        foreach (var child in tree.children) ValidateSourceMotion(child.motion, stack);
        stack.Remove(tree);
    }

    private static Motion CloneMotion(Motion original, AnimatorController controller, Dictionary<Motion, Motion> cache)
    {
        if (!(original is BlendTree tree)) return original;
        if (cache.TryGetValue(original, out var existing)) return existing;
        var clone = new BlendTree();
        EditorUtility.CopySerialized(tree, clone);
        clone.name = tree.name;
        AssetDatabase.AddObjectToAsset(clone, controller);
        cache.Add(original, clone);
        var children = tree.children;
        for (int i = 0; i < children.Length; i++) children[i].motion = CloneMotion(children[i].motion, controller, cache);
        clone.children = children;
        EditorUtility.SetDirty(clone);
        return clone;
    }

    public static void Validate(AnimatorController controller)
    {
        string path = AssetDatabase.GetAssetPath(controller);
        foreach (var layer in controller.layers) ValidateMachine(layer.stateMachine, path, new HashSet<Motion>());
    }

    private static void ValidateMachine(AnimatorStateMachine machine, string path, HashSet<Motion> visited)
    {
        if (machine == null || AssetDatabase.GetAssetPath(machine) != path || machine.anyStateTransitions.Length != 0 || machine.entryTransitions.Length != 0)
            throw new InvalidOperationException("Generated direct-play state machine has missing ownership or unexpected transitions.");
        if (machine.defaultState != null && !machine.states.Any(entry => entry.state == machine.defaultState))
            throw new InvalidOperationException("Generated state machine has an unresolved default state.");
        foreach (var entry in machine.states)
        {
            if (entry.state == null || AssetDatabase.GetAssetPath(entry.state) != path || entry.state.transitions.Length != 0)
                throw new InvalidOperationException("Generated production state is unresolved, externally owned or has legacy transitions.");
            ValidateMotion(entry.state.motion, path, visited);
        }
        foreach (var child in machine.stateMachines) ValidateMachine(child.stateMachine, path, visited);
    }

    private static void ValidateMotion(Motion motion, string path, HashSet<Motion> visited)
    {
        if (motion == null || !AssetDatabase.Contains(motion)) throw new InvalidOperationException("Generated motion has an unresolved reference.");
        if (!(motion is BlendTree tree) || !visited.Add(tree)) return;
        if (AssetDatabase.GetAssetPath(tree) != path) throw new InvalidOperationException("Generated BlendTree still references another controller's graph.");
        foreach (var child in tree.children) ValidateMotion(child.motion, path, visited);
    }
}
