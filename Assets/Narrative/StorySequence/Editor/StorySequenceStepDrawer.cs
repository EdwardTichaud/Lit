using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Lit.Story.Editor
{
    [CustomPropertyDrawer(typeof(StorySequenceStep))]
    public sealed class StorySequenceStepDrawer : PropertyDrawer
    {
        private static IEnumerable<string> Fields(SerializedProperty step)
        {
            yield return "label";
            yield return "type";
            yield return "skippable";
            switch ((StorySequenceStepType)step.FindPropertyRelative("type").intValue)
            {
                case StorySequenceStepType.Dialogue:
                    yield return "actorId";
                    yield return "listenerId";
                    yield return "speakerNameOverride";
                    yield return "voiceLine";
                    yield return "dialogueMaxDisplayDuration";
                    yield return "focusCameraOnSpeaker";
                    if (step.FindPropertyRelative("focusCameraOnSpeaker").boolValue)
                    {
                        yield return "dialogueCameraProfile";
                        yield return "cameraTransitionDuration";
                    }
                    break;
                case StorySequenceStepType.CameraShot:
                    yield return "cameraPointId";
                    yield return "actorId";
                    yield return "listenerId";
                    yield return "cameraProfile";
                    yield return "cameraTransitionDuration";
                    yield return "cameraHoldDuration";
                    break;
                case StorySequenceStepType.Fade:
                    yield return "fadeAlpha";
                    yield return "fadeDuration";
                    break;
                case StorySequenceStepType.Wait:
                    yield return "duration";
                    break;
                case StorySequenceStepType.AnimatorTrigger:
                    yield return "actorId";
                    yield return "animatorTrigger";
                    yield return "duration";
                    break;
                case StorySequenceStepType.Sitting:
                    yield return "applyToWholeSquad";
                    if (!step.FindPropertyRelative("applyToWholeSquad").boolValue) yield return "actorId";
                    yield return "sitting";
                    yield return "startDirectlyInSittingIdle";
                    yield return "duration";
                    break;
                case StorySequenceStepType.Timeline:
                    yield return "directorId";
                    yield return "timeline";
                    yield return "timelineBindingProfile";
                    yield return "waitForTimelineCompletion";
                    yield return "fadeToBlackBeforeTimelineEnd";
                    if (step.FindPropertyRelative("fadeToBlackBeforeTimelineEnd").boolValue)
                        yield return "timelineEndFadeDuration";
                    break;
                case StorySequenceStepType.SceneEvent:
                    yield return "eventId";
                    break;
                case StorySequenceStepType.ProgressivePlayerStop:
                    yield return "playerStopTimeout";
                    yield return "playerStopVelocityThreshold";
                    break;
                case StorySequenceStepType.LaunchCombat:
                    yield return "openingCombatDamage";
                    break;
            }
            yield return "oneShotAudioClips";
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded)
                foreach (string name in Fields(property))
                    height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(name), true) + 4f;
            return height + 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var type = (StorySequenceStepType)property.FindPropertyRelative("type").intValue;
            string name = property.FindPropertyRelative("label").stringValue;
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded,
                $"{label.text} - {type}" + (string.IsNullOrWhiteSpace(name) ? "" : $" : {name}"), true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (string field in Fields(property))
                {
                    var child = property.FindPropertyRelative(field);
                    line.y += line.height + 4f;
                    line.height = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(line, child, true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
