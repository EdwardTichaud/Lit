using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class CharacterAnimationCleanup
{
    // Retain the newest graph; keep older, compatible state entry points still used by scripts.
    static void MergeGraph(AnimatorController source, AnimatorController target, bool human)
    {
        if (source == target) return;
        bool direct = target.layers.All(l => IsDirect(l.stateMachine));
        var parameters = target.parameters.ToList();
        foreach (var p in source.parameters)
        {
            var old = parameters.SingleOrDefault(n => n.name == p.name);
            if (old != null && old.type != p.type) throw new InvalidOperationException("Animator parameter type conflict: " + p.name);
            if (old == null) parameters.Add(new AnimatorControllerParameter { name=p.name, type=p.type, defaultBool=p.defaultBool, defaultFloat=p.defaultFloat, defaultInt=p.defaultInt });
        }
        target.parameters = parameters.ToArray();
        var map = new Dictionary<Object,Object> { [source] = target };
        var added = new List<AnimatorState>();
        var machines = new List<(AnimatorStateMachine source, AnimatorStateMachine target, bool fresh)>();
        bool Compatible(Motion motion)
        {
            if (motion == null) return true;
            if (motion is AnimationClip clip) return clip.isHumanMotion == human;
            return motion is BlendTree tree && tree.children.All(c => Compatible(c.motion));
        }
        bool IsDirect(AnimatorStateMachine machine) => machine.anyStateTransitions.Length == 0 && machine.entryTransitions.Length == 0 &&
            machine.states.All(s => s.state.transitions.Length == 0) && machine.stateMachines.All(s => IsDirect(s.stateMachine));
        void Walk(AnimatorStateMachine original, AnimatorStateMachine destination, bool fresh)
        {
            map[original] = destination; machines.Add((original,destination,fresh));
            foreach (var child in original.states)
            {
                var existing = destination.states.FirstOrDefault(s => s.state.name == child.state.name).state;
                if (existing != null) { map[child.state] = existing; continue; }
                if (!Compatible(child.state.motion) || child.state.behaviours.Length != 0) { map[child.state] = null; report.notes.Add("Incompatible legacy state not imported: " + source.name + "/" + child.state.name); continue; }
                var clone = destination.AddState(child.state.name, child.position);
                EditorUtility.CopySerialized(child.state, clone); map[child.state] = clone; added.Add(clone);
                if (direct) clone.transitions = Array.Empty<AnimatorStateTransition>();
                report.mergedStates++;
            }
            foreach (var child in original.stateMachines)
            {
                var existing = destination.stateMachines.FirstOrDefault(s => s.stateMachine.name == child.stateMachine.name).stateMachine;
                Walk(child.stateMachine, existing != null ? existing : destination.AddStateMachine(child.stateMachine.name, child.position), existing == null);
            }
        }
        foreach (var layer in source.layers)
        {
            var layers = target.layers;
            int index = Array.FindIndex(layers, l => l.name == layer.name);
            bool fresh = index < 0;
            if (fresh)
            {
                target.AddLayer(layer.name); layers = target.layers; index = layers.Length - 1;
                layers[index].avatarMask = layer.avatarMask; layers[index].blendingMode = layer.blendingMode;
                layers[index].defaultWeight = layer.defaultWeight; layers[index].iKPass = layer.iKPass;
                target.layers = layers;
            }
            Walk(layer.stateMachine, target.layers[index].stateMachine, fresh);
        }
        Object Resolve(Object value)
        {
            if (value == null) return null;
            if (map.TryGetValue(value, out var mapped)) return mapped;
            if (AssetDatabase.GetAssetPath(value) != AssetDatabase.GetAssetPath(source)) return value;
            var clone = Object.Instantiate(value); clone.name = value.name; clone.hideFlags = value.hideFlags;
            AssetDatabase.AddObjectToAsset(clone, target); map[value] = clone;
            RemapProperties(clone, Resolve); EditorUtility.SetDirty(clone); return clone;
        }
        foreach (var state in added)
        {
            // Resolve graph-owned Blend Trees, behaviours and transition destinations before source removal.
            RemapProperties(state, Resolve);
            state.transitions = state.transitions.Where(t => t != null && (t.isExit || t.destinationState != null || t.destinationStateMachine != null)).ToArray();
            EditorUtility.SetDirty(state);
        }
        foreach (var pair in machines)
        {
            var original = pair.source; var destination = pair.target;
            if (pair.fresh)
            {
                destination.defaultState = Resolve(original.defaultState) as AnimatorState;
                destination.anyStatePosition = original.anyStatePosition; destination.entryPosition = original.entryPosition; destination.exitPosition = original.exitPosition;
            }
            if (direct) { EditorUtility.SetDirty(destination); continue; }
            var any = destination.anyStateTransitions.ToList();
            foreach (var transition in original.anyStateTransitions)
            {
                var clone = Resolve(transition) as AnimatorStateTransition;
                if (clone != null && (clone.isExit || clone.destinationState != null || clone.destinationStateMachine != null)) any.Add(clone);
            }
            destination.anyStateTransitions = any.ToArray();
            var entries = destination.entryTransitions.ToList();
            foreach (var transition in original.entryTransitions)
            {
                var clone = Resolve(transition) as AnimatorTransition;
                if (clone != null && (clone.isExit || clone.destinationState != null || clone.destinationStateMachine != null)) entries.Add(clone);
            }
            destination.entryTransitions = entries.ToArray(); EditorUtility.SetDirty(destination);
        }
        EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target);
    }
    static bool RemapProperties(Object target, Func<Object,Object> resolve)
    {
        if (target == null) return false;
        var data = new SerializedObject(target); var property = data.GetIterator(); bool changed = false;
        while (property.Next(true))
        {
            if(property.propertyType==SerializedPropertyType.String && property.editable && pathReplacements.TryGetValue(property.stringValue,out var replacementPath))
            {property.stringValue=replacementPath;changed=true;continue;}
            if (property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath == "m_Script" || !property.editable) continue;
            var old = property.objectReferenceValue; if (old == null) continue;
            var replacement = resolve(old);
            if (replacement == old) continue;
            property.objectReferenceValue = replacement; changed = true;
        }
        if (changed) { data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }
        return changed;
    }
}
