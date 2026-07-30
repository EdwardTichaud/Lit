#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Lit.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Lit.Editor
{
    [CustomEditor(typeof(TimelineBindingProfile))]
    public sealed class TimelineBindingProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty timeline;
        private SerializedProperty bindings;

        private void OnEnable()
        {
            timeline = serializedObject.FindProperty("timeline");
            bindings = serializedObject.FindProperty("bindings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(timeline);
            EditorGUILayout.Space(6f);

            TimelineAsset timelineAsset = timeline.objectReferenceValue as TimelineAsset;
            EditorGUILayout.LabelField("Bindings", EditorStyles.boldLabel);
            for (int i = 0; i < bindings.arraySize; i++)
            {
                DrawBinding(i, timelineAsset);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ajouter un binding"))
            {
                bindings.InsertArrayElementAtIndex(bindings.arraySize);
                SerializedProperty added = bindings.GetArrayElementAtIndex(bindings.arraySize - 1);
                added.FindPropertyRelative("track").objectReferenceValue = null;
                added.FindPropertyRelative("bindingId").stringValue = string.Empty;
                added.FindPropertyRelative("required").boolValue = true;
            }

            if (GUILayout.Button("Retirer le dernier") && bindings.arraySize > 0)
            {
                bindings.DeleteArrayElementAtIndex(bindings.arraySize - 1);
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBinding(int index, TimelineAsset timelineAsset)
        {
            SerializedProperty binding = bindings.GetArrayElementAtIndex(index);
            SerializedProperty track = binding.FindPropertyRelative("track");
            SerializedProperty bindingId = binding.FindPropertyRelative("bindingId");
            SerializedProperty required = binding.FindPropertyRelative("required");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Binding {index + 1}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(track);
            using (new EditorGUI.DisabledScope(timelineAsset == null))
            {
                if (GUILayout.Button("Choisir une piste de cette Timeline"))
                {
                    ShowTrackMenu(index, timelineAsset);
                }
            }

            if (timelineAsset == null)
            {
                EditorGUILayout.HelpBox("Assigne d'abord une Timeline pour choisir une piste.", MessageType.Info);
            }

            EditorGUILayout.PropertyField(bindingId, new GUIContent("Binding Id"));
            EditorGUILayout.PropertyField(required);
            EditorGUILayout.EndVertical();
        }

        private void ShowTrackMenu(int bindingIndex, TimelineAsset timelineAsset)
        {
            GenericMenu menu = new GenericMenu();
            List<TrackAsset> tracks = GetTracks(timelineAsset);
            if (tracks.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("Aucune piste compatible"));
            }

            for (int i = 0; i < tracks.Count; i++)
            {
                TrackAsset selectedTrack = tracks[i];
                string label = $"{selectedTrack.name} ({selectedTrack.GetType().Name})";
                menu.AddItem(new GUIContent(label), false, () => AssignTrack(bindingIndex, selectedTrack));
            }

            menu.ShowAsContext();
        }

        private void AssignTrack(int bindingIndex, TrackAsset track)
        {
            TimelineBindingProfile profile = (TimelineBindingProfile)target;
            if (bindingIndex < 0 || bindingIndex >= profile.Bindings.Count)
            {
                return;
            }

            Undo.RecordObject(profile, "Assign Timeline Binding Track");
            profile.Bindings[bindingIndex].track = track;
            EditorUtility.SetDirty(profile);
            Repaint();
        }

        private static List<TrackAsset> GetTracks(TimelineAsset timelineAsset)
        {
            List<TrackAsset> tracks = new List<TrackAsset>();
            foreach (PlayableBinding output in timelineAsset.outputs)
            {
                if (output.sourceObject is TrackAsset track && !tracks.Contains(track))
                {
                    tracks.Add(track);
                }
            }

            if (timelineAsset.markerTrack != null && !tracks.Contains(timelineAsset.markerTrack))
            {
                tracks.Add(timelineAsset.markerTrack);
            }

            return tracks;
        }
    }
}
#endif
