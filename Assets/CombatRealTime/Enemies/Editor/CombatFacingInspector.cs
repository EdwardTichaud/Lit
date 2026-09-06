using UnityEditor;
using UnityEngine;

/// <summary>Read-only axes: bone axes are diagnostic, not a correction to the authored pose.</summary>
[InitializeOnLoad]
public static class CombatFacingInspector
{
    private static bool enabled;
    static CombatFacingInspector() { SceneView.duringSceneGui += Draw; }

    [MenuItem("Lit/Combat/Toggle Selected Actor Facing Axes")]
    private static void Toggle()
    {
        enabled = !enabled;
        SceneView.RepaintAll();
    }

    private static void Draw(SceneView view)
    {
        if (!enabled || Selection.activeGameObject == null) return;
        var contract = Selection.activeGameObject.GetComponentInParent<CombatActorAnimationRoot>();
        if (contract == null) return;
        var root = contract.transform;
        Axis(root.position, root.forward, Color.blue, "ActorRoot +Z");
        var animator = contract.Animator;
        if (animator != null && animator.isHuman)
        {
            Bone(animator.GetBoneTransform(HumanBodyBones.Hips), "Bassin +Z", Color.yellow);
            Bone(animator.GetBoneTransform(HumanBodyBones.Chest), "Torse +Z", Color.green);
        }
        var target = root.GetComponent<EnemyCombatBrain>()?.Target;
        if (target != null) Axis(root.position, target.transform.position - root.position, Color.red, "Cible");
        // Selecting the target alongside Lucian gives a shared, explicit reference in edit mode.
        else foreach (var selected in Selection.transforms)
            if (selected != root && !selected.IsChildOf(root))
            { Axis(root.position, selected.position - root.position, Color.red, "Reference selectionnee"); break; }
    }

    private static void Bone(Transform bone, string label, Color color)
    {
        if (bone != null) Axis(bone.position, bone.forward, color, label);
    }

    private static void Axis(Vector3 origin, Vector3 direction, Color color, string label)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < .0001f) return;
        using (new Handles.DrawingScope(color))
        {
            Vector3 end = origin + direction.normalized * 1.5f;
            Handles.DrawAAPolyLine(3f, origin, end);
            Handles.Label(end, label + " yaw=" + (Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg).ToString("F1"));
        }
    }
}
