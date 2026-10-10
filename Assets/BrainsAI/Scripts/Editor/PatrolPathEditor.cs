using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// Custom scene editor for <see cref="PatrolPath"/>.  
    /// Allows adding, removing, and moving patrol points directly in the Scene view:
    /// - <b>Shift + Click</b>: Add a patrol point (snapped to NavMesh).  
    /// - <b>Ctrl + Click</b>: Remove a patrol point.  
    /// - <b>Click + Drag</b>: Move a patrol point (snapped to NavMesh).  
    /// Points are always kept on the NavMesh surface.
    /// </summary>
    [CustomEditor(typeof(PatrolPath))]
    public class PatrolPathEditor : Editor
    {
        private void OnSceneGUI()
        {
            PatrolPath path = (PatrolPath)target;
            Event e = Event.current;

            // Force scene repaint for hover detection
            if (e.type == EventType.MouseMove)
                SceneView.RepaintAll();

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            int hoveredIndex = -1;
            Vector3 ghostPosition = Vector3.zero;
            int insertSegmentIndex = -1;
            float closestSegmentDistance = float.MaxValue;

            // Mouse raycast onto world
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0))
            {
                // Project hit point onto NavMesh
                if (!NavMesh.SamplePosition(hit.point, out var navHit, 2f, NavMesh.AllAreas))
                    return;

                ghostPosition = navHit.position;

                // --- Hover check over existing points ---
                for (int i = 0; i < path.points.Length; i++)
                {
                    Vector3 worldPoint = path.transform.TransformPoint(path.points[i]);
                    float handleSize = HandleUtility.GetHandleSize(worldPoint) * 0.1f;

                    if (Vector3.Distance(worldPoint, ghostPosition) < handleSize * 1.5f)
                    {
                        hoveredIndex = i;
                        break;
                    }
                }

                // --- Check for closest line segment (insert between points) ---
                for (int i = 0; i < path.points.Length - 1; i++)
                {
                    Vector3 a = path.transform.TransformPoint(path.points[i]);
                    Vector3 b = path.transform.TransformPoint(path.points[i + 1]);
                    Vector3 projected = HandleUtility.ProjectPointLine(hit.point, a, b);
                    float distance = Vector3.Distance(hit.point, projected);

                    if (distance < closestSegmentDistance &&
                        distance < HandleUtility.GetHandleSize(projected) * 0.2f)
                    {
                        closestSegmentDistance = distance;
                        ghostPosition = projected;
                        insertSegmentIndex = i + 1;
                    }
                }

                // --- Add new point (Shift + Click) ---
                if (e.shift && e.type == EventType.MouseDown && e.button == 0)
                {
                    Undo.RecordObject(path, "Add Patrol Point");
                    Vector3 localPoint = path.transform.InverseTransformPoint(ghostPosition);

                    if (insertSegmentIndex != -1)
                        ArrayUtility.Insert(ref path.points, insertSegmentIndex, localPoint);
                    else
                        ArrayUtility.Add(ref path.points, localPoint); // add at end

                    EditorUtility.SetDirty(path);
                    e.Use();
                }

                // --- Remove point (Ctrl + Click) ---
                if (e.control && hoveredIndex != -1 && e.type == EventType.MouseDown && e.button == 0)
                {
                    Undo.RecordObject(path, "Remove Patrol Point");
                    ArrayUtility.RemoveAt(ref path.points, hoveredIndex);
                    EditorUtility.SetDirty(path);
                    e.Use();
                }
            }

            // --- Draw all patrol points ---
            for (int i = 0; i < path.points.Length; i++)
            {
                Vector3 worldPos = path.transform.TransformPoint(path.points[i]);
                float size = HandleUtility.GetHandleSize(worldPos) * 0.1f;

                // Color based on state (hovered / remove mode / normal)
                Handles.color = (e.control && i == hoveredIndex) ? Color.red :
                                (i == hoveredIndex) ? new Color(1f, 0.5f, 0f) :
                                Color.white;

                // Move point with handle
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldPos = Handles.FreeMoveHandle(worldPos, size, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    // Snap moved position to NavMesh
                    if (NavMesh.SamplePosition(newWorldPos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                    {
                        Undo.RecordObject(path, "Move Patrol Point");
                        path.points[i] = path.transform.InverseTransformPoint(navHit.position);
                        EditorUtility.SetDirty(path);
                    }
                }

                // Draw label (P + index)
                GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    normal = { textColor = Color.white }
                };
                Handles.Label(worldPos + Vector3.up * size * 1.5f, $"P{i}", labelStyle);
            }

            // --- Draw ghost preview when holding Shift ---
            if (e.shift && ghostPosition != Vector3.zero)
            {
                float ghostSize = HandleUtility.GetHandleSize(ghostPosition) * 0.1f;
                Handles.color = new Color(0f, 1f, 0f, 0.4f); // ghost green
                Handles.SphereHandleCap(-1, ghostPosition, Quaternion.identity, ghostSize, EventType.Repaint);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            PatrolPath path = (PatrolPath)target;

            // Loop path toggle with icon
            GUIContent contentGUI = EditorGUIUtility.IconContent(path.loopPath ? "d_Linked" : "d_Unlinked");
            contentGUI.text = " Loop Path";
            path.loopPath = EditorGUILayout.ToggleLeft(contentGUI, path.loopPath);

            serializedObject.ApplyModifiedProperties();

            GUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Controls:\n" +
                "Shift + Click = Add point\n" +
                "Ctrl + Click = Delete point\n" +
                "Click + Drag = Move point",
                MessageType.Info);
        }
    }
}
