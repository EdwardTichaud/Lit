using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewCameraMotif", menuName = "Symphonie/Camera Motif")]
public class CameraMotifSO : ScriptableObject
{
    public enum ReferencePoint
    {
        Caster,
        Target,
        [InspectorName("Last Unit Enemy")]
        LastUnitEnemy
    }

    [Header("Blend")]
    [Min(0f)]
    [Tooltip("Durée de transition lorsque le motif devient actif.")]
    public float blendDuration = 0.25f;

    [Header("Edge Blur")]
    [Range(0f, 1f)]
    [Tooltip("Etendue du flou depuis les bords (0 = bords uniquement, 1 = jusqu'au centre).")]
    public float edgeBlurAmount = 0f;

    [Header("Animation")]
    [Tooltip("Active l'animation des paramètres via des courbes (temps normalisé 0..1).")]
    public bool animate;
    [Min(0f)]
    [Tooltip("Durée (en secondes) utilisée pour parcourir les courbes. 0 = statique.")]
    public float animationDuration = 0f;
    [Tooltip("Boucle l'animation des courbes.")]
    public bool loopAnimation = true;

    [Header("Animation Curves - Reference Offset Position")]
    [Tooltip("Décalage X additionnel (droite) appliqué via courbe.")]
    public AnimationCurve referenceOffsetPositionX;
    [Tooltip("Décalage Y additionnel (haut) appliqué via courbe.")]
    public AnimationCurve referenceOffsetPositionY;
    [Tooltip("Décalage Z additionnel (avant) appliqué via courbe.")]
    public AnimationCurve referenceOffsetPositionZ;

    [Header("Animation Curves - Reference Offset Rotation")]
    [Tooltip("Rotation X additionnelle (pitch) appliquée via courbe.")]
    public AnimationCurve referenceOffsetRotationX;
    [Tooltip("Rotation Y additionnelle (yaw) appliquée via courbe.")]
    public AnimationCurve referenceOffsetRotationY;
    [Tooltip("Rotation Z additionnelle (roll) appliquée via courbe.")]
    public AnimationCurve referenceOffsetRotationZ;

    [Header("Animation Curves - Timing")]
    [Tooltip("Variation additionnelle du smoothTime via courbe.")]
    public AnimationCurve referenceOffsetSmoothTimeCurve;
    [Tooltip("Variation additionnelle du délai de référence via courbe.")]
    public AnimationCurve referenceOffsetDelayCurve;

    [Header("Animation Curves - Orbit")]
    [Tooltip("Variation additionnelle de la vitesse d'orbite via courbe.")]
    public AnimationCurve orbitSpeedCurve;

    [Header("Animation Curves - Edge Blur")]
    [Tooltip("Variation additionnelle de l'etendue du flou via courbe.")]
    public AnimationCurve edgeBlurAmountCurve;

    [Header("Reference")]
    [Tooltip("Point de référence utilisé pour positionner la caméra.")]
    public ReferencePoint referencePoint = ReferencePoint.Caster;
    [Tooltip("Fige la position de référence lors de l'activation du motif.")]
    public bool lockReferencePosition;
    [Tooltip("Fige la rotation de référence lors de l'activation du motif.")]
    public bool lockReferenceRotation;

    [Header("Size Compensation")]
    [Tooltip("Ajuste le FOV si la cible de référence sort du cadre.")]
    public bool compensateReferenceSize;
    [Min(0f)]
    [Tooltip("Limite d'augmentation du FOV (en degrés) lorsque la compensation est active.")]
    public float maxCompensationFovIncrease = 12f;

    [Header("Reference Offset")]
    [FormerlySerializedAs("casterOffsetLocal")]
    [FormerlySerializedAs("casterOffsetPosition")]
    [Tooltip("Position locale appliquée à la caméra autour du point de référence (X=right, Y=up, Z=forward).")]
    public Vector3 referenceOffsetPosition = Vector3.zero;
    [FormerlySerializedAs("casterOffsetSmoothTime")]
    [Tooltip("Temps de lissage (en secondes) pour suivre l'offset de référence. -1 = conserver la valeur de base.")]
    public float referenceOffsetSmoothTime = -1f;
    [Min(0f)]
    [Tooltip("Délai (en secondes) avant que la caméra suive le point de référence.")]
    public float referenceOffsetDelay = 0f;
    [FormerlySerializedAs("casterOffsetRotationEuler")]
    [FormerlySerializedAs("casterOffsetRotation")]
    [Tooltip("Rotation (en degrés) appliquée à la caméra, dans l'espace local du point de référence.")]
    public Vector3 referenceOffsetRotation = Vector3.zero;

    [Header("Orbit")]
    [Tooltip("Active une rotation continue de la caméra autour du point choisi.")]
    public bool orbitEnabled;

    [Tooltip("Point autour duquel la caméra orbitera.")]
    public ReferencePoint orbitReferencePoint = ReferencePoint.Caster;

    [Tooltip("Vitesse de rotation orbitale (en degrés par seconde).")]
    public float orbitSpeed;

    [Header("Look At")]
    [Tooltip("Force la caméra à regarder un point de référence.")]
    public bool lookAtEnabled;

    [Tooltip("Point regardé par la caméra lorsqu'un Look At est actif.")]
    public ReferencePoint lookAtReferencePoint = ReferencePoint.Caster;
}
