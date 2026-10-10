using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BrainsAI
{
    public class Note : MonoBehaviour
    {
        public bool edit;
        public string note;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(Note))]
    public class NoteEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Note note = (Note)target;
            Undo.RecordObject(note, "Note");
            note.edit = EditorGUILayout.ToggleLeft("Edit", note.edit);
            if (note.edit)
                note.note = EditorGUILayout.TextArea(note.note);
            else
                EditorGUILayout.HelpBox(note.note, MessageType.None);
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}