using UnityEngine;
using UnityEditor;

namespace Ultrabolt.BrainsAI
{
    [CustomEditor(typeof(AlarmEmitter))]
    public class AlarmEmitterEditor : Editor
    {
        void DrawField(string propertyName) =>
            StaticMethods.DrawField(serializedObject, propertyName);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            GUIContent contentGUI = EditorGUIUtility.IconContent("d_SignalEmitter Icon");
            contentGUI.text = " Alarm Emitter";

            StaticMethods.CreateBoxGroup(contentGUI, () =>
            {
                DrawField("playOnEnable");
                DrawField("alarmRadius");
                DrawField("alertAllGroups");
                if (!((AlarmEmitter)target).alertAllGroups)
                    DrawField("alarmGroups");
            });
            serializedObject.ApplyModifiedProperties();
        }
    }
}
