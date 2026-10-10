using UnityEngine;
using UnityEditor;

namespace Ultrabolt.BrainsAI
{
    [CustomEditor(typeof(BrainTactical))]
    public class BrainTacticalEditor : BrainAIEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();
            BrainTactical brain = (BrainTactical)target;
            if (brain.fov != null)
                DrawSimpleFoldoutGroup("Tactical Settings", "d_NavMeshData Icon", new string[] { "hidingTime", "visibleTime" }, ref brain.drawTacticalSettings);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
