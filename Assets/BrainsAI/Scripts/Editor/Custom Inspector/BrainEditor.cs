using UnityEngine;
using UnityEditor;

namespace Ultrabolt.BrainsAI
{
    [CustomEditor(typeof(Brain))]
    public class BrainAIEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Brain brainAI = (Brain)target;

            // Begin change check here — covers everything drawn below
            EditorGUI.BeginChangeCheck();

            if (brainAI.fov == null)
                DrawMissingFOV(brainAI);
            else
                DrawInspector(brainAI);

            // If something actually changed, mark objects dirty
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(brainAI, "Brain Inspector Change");
                EditorUtility.SetDirty(brainAI);

                if (brainAI.fov != null)
                {
                    Undo.RecordObject(brainAI.fov, "FOV Change");
                    EditorUtility.SetDirty(brainAI.fov);
                }

                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        void DrawMissingFOV(Brain brainAI)
        {
            brainAI.fov = brainAI.GetComponentInChildren<FOV>();
            if (brainAI.fov != null) return;

            EditorGUILayout.HelpBox(
                "Warning: No Field of View (FOV) script found!\n" +
                "Please add the component to this GameObject or one of its children. " +
                "This script will automatically detect it.",
                MessageType.Warning
            );

            GUIContent contentGUI = EditorGUIUtility.IconContent("d_scenevis_visible_hover@2x");
            contentGUI.text = " Add FOV on head";

            if (GUILayout.Button(contentGUI))
            {
                Animator animator = brainAI.GetComponentInChildren<Animator>();
                if (animator == null)
                {
                    Debug.LogError("No Animator found on this character. Can't add FOV to the head.");
                    return;
                }

                Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null)
                {
                    GameObject eyes = new GameObject("Eyes");
                    eyes.transform.SetParent(headBone);
                    eyes.transform.localPosition = Vector3.zero;
                    eyes.transform.rotation = brainAI.transform.rotation;
                    eyes.AddComponent<FOV>();
                }
                else
                {
                    Debug.Log("No head bone found. Are you sure this character is humanoid?");
                    brainAI.gameObject.AddComponent<FOV>();
                }
            }
        }

        void DrawInspector(Brain brainAI)
        {
            EditorGUILayout.BeginVertical("box");
            brainAI.brainType = StaticMethods.DrawEnumAsButtons(brainAI.brainType, "Behave Type");
            EditorGUILayout.EndVertical();

            DrawSimpleBoxGroup("Communication Settings", "d_NetworkIdentity Icon",
                new string[] { "drawAlarmGizmos", "alarmRadius", "groupName", "alarmGroups" }
            );

            DrawSimpleBoxGroup("Movement Settings", "d_MoveTool On@2x",
                new string[] { "drawMovementGizmos", "sneakSpeed", "walkSpeed", "runSpeed", "moveRange" }
            );

            DrawSimpleBoxGroup("Jump Settings", "d_OffMeshLink Icon",
                new string[] { "jumpCurve", "jumpHeight", "durationMultiplier" }
            );

            GUIContent contentGUI = EditorGUIUtility.IconContent("ViewToolOrbit On@2x");
            contentGUI.text = " FOV Reference";
            StaticMethods.CreateFoldoutGroup(contentGUI, ref brainAI.showFov, () =>
            {
                EditorGUI.BeginDisabledGroup(true);
                brainAI.fov = EditorGUILayout.ObjectField("FOV Script", brainAI.fov, typeof(FOV), true) as FOV;
                EditorGUI.EndDisabledGroup();

                Editor editor = CreateEditor(brainAI.fov);
                if (editor != null)
                    editor.OnInspectorGUI();
            });

            contentGUI = EditorGUIUtility.IconContent("d_Search Icon");
            contentGUI.text = brainAI.fov.canInvestigate ? "Eyes Can Investigate" : "Eyes Can't Investigate";
            StaticMethods.CreateFoldoutGroup(contentGUI, ref brainAI.fov.canInvestigate, () =>
            {
                brainAI.fov.investigateDuration = EditorGUILayout.FloatField("Investigate Duration", brainAI.fov.investigateDuration);
                EditorGUILayout.HelpBox($"This character will stay {brainAI.fov.investigateDuration} sec investigating before moving back to normal.", MessageType.Info);
            });

            DrawSimpleFoldoutGroup(brainAI.canPatrol ? "Character Can Patrol" : "Character Can't Patrol",
                "d_NavMeshAgent Icon",
                new string[] { "drawPatrolGizmos", "patrolPath", "patrolRadius", "standingTime" },
                ref brainAI.canPatrol
            );

            DrawSimpleFoldoutGroup(brainAI.canAttack ? "Character Can Attack" : "Character Can't Attack",
                "RaycastCollider Icon",
                new string[] { "attackCallType", "damage", "attackTime", "maxAttacks" },
                ref brainAI.canAttack
            );
        }

        protected void DrawSimpleBoxGroup(string groupName, string iconName, string[] fields) =>
            StaticMethods.CreateSimpleBoxGroup(serializedObject, groupName, iconName, fields);

        protected void DrawSimpleFoldoutGroup(string groupName, string iconName, string[] fields, ref bool value) =>
            StaticMethods.CreateSimpleFoldoutGroup(serializedObject, groupName, iconName, fields, ref value);
    }
}
