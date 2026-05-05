using UnityEngine;

public class SwingMotion : MonoBehaviour
{
    [Header("Pivot")]
    [Tooltip("Point autour duquel la lanterne oscille.")]
    public Transform pivot;

    [Header("Primary Swing")]
    [Tooltip("Premier axe local de balancement.")]
    public Vector3 axisA = Vector3.right;

    [Tooltip("Angle max du premier axe.")]
    public float angleA = 10f;

    [Tooltip("Vitesse du premier axe.")]
    public float speedA = 1.1f;

    [Header("Secondary Swing")]
    [Tooltip("Second axe local de balancement.")]
    public Vector3 axisB = Vector3.forward;

    [Tooltip("Angle max du second axe.")]
    public float angleB = 6f;

    [Tooltip("Vitesse du second axe.")]
    public float speedB = 1.7f;

    [Header("Phase")]
    [Tooltip("Décalage de phase du second axe. Permet de créer ellipse / 8 / arcs.")]
    public float phaseOffsetB = 0.8f;

    [Header("Motion Style")]
    [Tooltip("Ajoute une légère variation automatique pour un rendu moins mécanique.")]
    public bool organicMotion = true;

    [Tooltip("Force de la variation organique.")]
    public float organicAmount = 0.15f;

    [Header("Startup")]
    [Tooltip("Décalage aléatoire au lancement.")]
    public bool randomStartOffset = true;

    private Vector3 initialOffset;
    private Quaternion initialRotation;
    private float timeOffset;

    void Start()
    {
        if (pivot == null)
        {
            pivot = transform.parent != null ? transform.parent : transform;
        }

        initialOffset = transform.position - pivot.position;
        initialRotation = transform.rotation;

        if (randomStartOffset)
            timeOffset = Random.Range(0f, 100f);
    }

    void LateUpdate()
    {
        float t = Time.time + timeOffset;

        float currentAngleA = Mathf.Sin(t * speedA) * angleA;
        float currentAngleB = Mathf.Sin(t * speedB + phaseOffsetB) * angleB;

        if (organicMotion)
        {
            currentAngleA += Mathf.Sin(t * 0.37f) * angleA * organicAmount;
            currentAngleB += Mathf.Cos(t * 0.29f) * angleB * organicAmount;
        }

        Quaternion rotA = Quaternion.AngleAxis(currentAngleA, axisA.normalized);
        Quaternion rotB = Quaternion.AngleAxis(currentAngleB, axisB.normalized);

        // Important : combiner les deux rotations
        Quaternion combinedRotation = rotA * rotB;

        Vector3 rotatedOffset = combinedRotation * initialOffset;
        transform.position = pivot.position + rotatedOffset;

        transform.rotation = combinedRotation * initialRotation;
    }

    void OnDrawGizmosSelected()
    {
        if (pivot != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pivot.position, transform.position);
            Gizmos.DrawSphere(pivot.position, 0.03f);
        }
    }
}
