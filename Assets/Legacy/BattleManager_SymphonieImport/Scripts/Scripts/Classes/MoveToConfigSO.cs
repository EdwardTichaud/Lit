#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public enum DestinationMode { WorldPosition, TargetByName }

public enum RotationTargetMode
{
    None,
    LookAtWorldPosition,
    LookAtTargetByName
}

[System.Serializable]
public struct AxisMask
{
    public bool X;
    public bool Y;
    public bool Z;

    public static AxisMask XYZ => new AxisMask { X = true, Y = true, Z = true };
}

[CreateAssetMenu(fileName = "MoveToConfig", menuName = "Symphonie/Move To Config", order = 10)]
public class MoveToConfigSO : ScriptableObject
{
    [Header("Sujet à déplacer / tourner")]
    [Tooltip("Nom du GameObject sujet (ex: 'Lucian')")]
    public string subjectToMoveName;

    // -----------------------
    // Déplacement (position)
    // -----------------------
    [Header("Destination (Position)")]
    public DestinationMode destinationMode = DestinationMode.WorldPosition;

    [Tooltip("Utilisé si destinationMode = WorldPosition")]
    public Vector3 worldPosition;

    [Tooltip("Nom du GameObject cible si destinationMode = TargetByName")]
    public string targetName;

    [Tooltip("Décalage appliqué à la destination en espace monde")]
    public Vector3 worldOffset = Vector3.zero;

    [Header("Durée & easing (Déplacement)")]
    [Min(0f)] public float duration = 1f;
    [Tooltip("Courbe de 0→1 (null = linéaire)")]
    public AnimationCurve ease = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Options (Déplacement)")]
    [Tooltip("True = utilise Time.unscaledDeltaTime")]
    public bool unscaledTime = false;

    [Tooltip("True = déplace en localPosition (si pertinent)")]
    public bool useLocalSpace = false;

    [Tooltip("S’il y a un déplacement en cours, le remplace")]
    public bool interruptCurrentMove = true;

    // -----------------------
    // Rotation (orientation)
    // -----------------------
    [Header("Rotation (Orientation)")]
    [Tooltip("Choix de la cible de rotation (aucune, position monde, cible par nom)")]
    public RotationTargetMode rotationTargetMode = RotationTargetMode.None;

    [Tooltip("Masque d'axes : coche les axes que TU veux faire tourner.")]
    public AxisMask rotateAxes = AxisMask.XYZ;

    [Tooltip("Offset supplémentaire appliqué à l'orientation finale (degrés euler)")]
    public Vector3 rotationEulerOffset = Vector3.zero;

    [Header("Durée & easing (Rotation)")]
    [Min(0f)] public float rotationDuration = 0.5f;

    [Tooltip("Courbe de 0→1 pour la rotation (null = linéaire)")]
    public AnimationCurve rotationEase = AnimationCurve.Linear(0, 0, 1, 1);

    [Tooltip("True = utilise Time.unscaledDeltaTime pour la rotation")]
    public bool rotationUnscaledTime = false;

    [Tooltip("S’il y a une rotation en cours, la remplace")]
    public bool interruptCurrentRotation = true;

    [Header("Options avancées (Rotation)")]
    [Tooltip("Up vector pour LookRotation (si tu veux forcer un 'haut'). Laisse (0,0,0) pour utiliser Vector3.up.")]
    public Vector3 customUp = Vector3.zero;

    /// <summary>
    /// Renvoie la position-cible (monde) à partir des réglages de position.
    /// </summary>
    public bool TryGetMoveTarget(out Vector3 moveTarget)
    {
        switch (destinationMode)
        {
            case DestinationMode.WorldPosition:
                moveTarget = worldPosition + worldOffset;
                return true;

            case DestinationMode.TargetByName:
                {
                    var go = GameObject.Find(targetName);
                    if (go != null)
                    {
                        moveTarget = go.transform.position + worldOffset;
                        return true;
                    }
                    moveTarget = Vector3.zero;
                    return false;
                }
        }

        moveTarget = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Renvoie la position-cible (monde) à regarder, en fonction de rotationTargetMode.
    /// </summary>
    public bool TryGetLookTarget(out Vector3 lookTarget)
    {
        switch (rotationTargetMode)
        {
            case RotationTargetMode.None:
                lookTarget = Vector3.zero;
                return false;

            case RotationTargetMode.LookAtWorldPosition:
                lookTarget = worldPosition; // volontairement SANS worldOffset pour l'orientation
                return true;

            case RotationTargetMode.LookAtTargetByName:
                {
                    // On réutilise targetName (le même que le déplacement) pour simplicité,
                    // mais tu peux créer un champ dédié si tu veux.
                    var go = GameObject.Find(targetName);
                    if (go != null)
                    {
                        lookTarget = go.transform.position;
                        return true;
                    }
                    lookTarget = Vector3.zero;
                    return false;
                }
        }

        lookTarget = Vector3.zero;
        return false;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MoveToConfigSO))]
public class MoveToConfigSOEditor : Editor
{
    SerializedProperty subjectToMoveName;

    SerializedProperty destinationMode;
    SerializedProperty worldPosition;
    SerializedProperty targetName;
    SerializedProperty worldOffset;
    SerializedProperty duration;
    SerializedProperty ease;
    SerializedProperty unscaledTime;
    SerializedProperty useLocalSpace;
    SerializedProperty interruptCurrentMove;

    // Rotation
    SerializedProperty rotationTargetMode;
    SerializedProperty rotateAxes;
    SerializedProperty rotationEulerOffset;
    SerializedProperty rotationDuration;
    SerializedProperty rotationEase;
    SerializedProperty rotationUnscaledTime;
    SerializedProperty interruptCurrentRotation;
    SerializedProperty customUp;

    void OnEnable()
    {
        subjectToMoveName   = serializedObject.FindProperty("subjectToMoveName");

        destinationMode     = serializedObject.FindProperty("destinationMode");
        worldPosition       = serializedObject.FindProperty("worldPosition");
        targetName          = serializedObject.FindProperty("targetName");
        worldOffset         = serializedObject.FindProperty("worldOffset");
        duration            = serializedObject.FindProperty("duration");
        ease                = serializedObject.FindProperty("ease");
        unscaledTime        = serializedObject.FindProperty("unscaledTime");
        useLocalSpace       = serializedObject.FindProperty("useLocalSpace");
        interruptCurrentMove= serializedObject.FindProperty("interruptCurrentMove");

        rotationTargetMode      = serializedObject.FindProperty("rotationTargetMode");
        rotateAxes              = serializedObject.FindProperty("rotateAxes");
        rotationEulerOffset     = serializedObject.FindProperty("rotationEulerOffset");
        rotationDuration        = serializedObject.FindProperty("rotationDuration");
        rotationEase            = serializedObject.FindProperty("rotationEase");
        rotationUnscaledTime    = serializedObject.FindProperty("rotationUnscaledTime");
        interruptCurrentRotation= serializedObject.FindProperty("interruptCurrentRotation");
        customUp                = serializedObject.FindProperty("customUp");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Sujet à déplacer / tourner", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(subjectToMoveName, new GUIContent("Nom du sujet"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Destination (Position)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(destinationMode);

        var mode = (DestinationMode)destinationMode.enumValueIndex;
        switch (mode)
        {
            case DestinationMode.WorldPosition:
                EditorGUILayout.PropertyField(worldPosition, new GUIContent("World Position"));
                break;
            case DestinationMode.TargetByName:
                EditorGUILayout.PropertyField(targetName, new GUIContent("Nom du GameObject cible"));
                break;
        }

        EditorGUILayout.PropertyField(worldOffset, new GUIContent("World Offset"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Durée & easing (Déplacement)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(duration);
        EditorGUILayout.PropertyField(ease);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Options (Déplacement)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(unscaledTime);
        EditorGUILayout.PropertyField(useLocalSpace);
        EditorGUILayout.PropertyField(interruptCurrentMove);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Rotation (Orientation)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(rotationTargetMode, new GUIContent("Cible de rotation"));

        var rmode = (RotationTargetMode)rotationTargetMode.enumValueIndex;
        if (rmode == RotationTargetMode.LookAtWorldPosition)
        {
            EditorGUILayout.PropertyField(worldPosition, new GUIContent("LookAt World Position"));
        }
        else if (rmode == RotationTargetMode.LookAtTargetByName)
        {
            EditorGUILayout.PropertyField(targetName, new GUIContent("Nom du GameObject à regarder"));
        }

        EditorGUILayout.PropertyField(rotateAxes, new GUIContent("Axes à faire tourner (X/Y/Z)"));
        EditorGUILayout.PropertyField(rotationEulerOffset, new GUIContent("Rotation Offset (Euler deg)"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Durée & easing (Rotation)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(rotationDuration);
        EditorGUILayout.PropertyField(rotationEase);
        EditorGUILayout.PropertyField(rotationUnscaledTime);
        EditorGUILayout.PropertyField(interruptCurrentRotation);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Options avancées (Rotation)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(customUp);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
