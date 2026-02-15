using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
// Point d'attente pour les personnages non controles dans la Maison.
public class MaisonWaitingPoint : MonoBehaviour
{
    [Tooltip("Index utilise par les CharacterData (maisonWaitingPoint).")]
    public int waitingIndex = 0;
    [Tooltip("Rayon du gizmo de debug.")]
    public float gizmoRadius = 0.2f;
    [Tooltip("Couleur du gizmo.")]
    public Color gizmoColor = new Color(0.2f, 0.9f, 1f, 0.9f);

    private static readonly List<MaisonWaitingPoint> activePoints = new List<MaisonWaitingPoint>();

    private void OnEnable()
    {
        if (!activePoints.Contains(this))
        {
            activePoints.Add(this);
        }
    }

    private void OnDisable()
    {
        activePoints.Remove(this);
    }

    public static bool TryGetPoint(int index, out Transform point)
    {
        for (int i = 0; i < activePoints.Count; i++)
        {
            MaisonWaitingPoint waitingPoint = activePoints[i];
            if (waitingPoint != null && waitingPoint.waitingIndex == index)
            {
                point = waitingPoint.transform;
                return true;
            }
        }

        point = null;
        return false;
    }

#if UNITY_EDITOR
    private void DrawLabel()
    {
        UnityEditor.Handles.color = gizmoColor;
        UnityEditor.Handles.Label(transform.position + Vector3.up * (gizmoRadius * 1.5f), $"Maison WP {waitingIndex}");
    }
#endif

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        float radius = Mathf.Max(0.01f, gizmoRadius);
        Gizmos.DrawSphere(transform.position, radius);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * radius * 2f);
#if UNITY_EDITOR
        DrawLabel();
#endif
    }
}
