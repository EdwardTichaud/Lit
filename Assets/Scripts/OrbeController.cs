using UnityEngine;

// Controle l'instanciation d'une orbe qui tourne autour d'un personnage.
[DisallowMultipleComponent]
public class OrbeController : MonoBehaviour
{
    [Header("Orbit")]
    [Tooltip("Prefab de l'orbe a instancier (optionnel, sinon utilise l'objet courant).")]
    public GameObject orbePrefab;
    [Tooltip("Offset du pivot d'orbite par rapport au personnage.")]
    public Vector3 orbitOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("Rayon de l'orbite.")]
    public float orbitRadius = 1f;
    [Tooltip("Axe d'orbite local.")]
    public Vector3 orbitAxis = Vector3.up;
    [Tooltip("Vitesse d'orbite (degres par seconde).")]
    public float orbitSpeed = 90f;
    [Tooltip("Duree de l'orbite en secondes.")]
    public float orbitDuration = 5f;
    [Tooltip("Utilise le temps non scale.")]
    public bool useUnscaledTime = false;
    [Tooltip("Angle de depart aleatoire.")]
    public bool randomStartAngle = true;
    [Tooltip("Angle de depart si pas aleatoire.")]
    public float startAngle = 0f;
    [Tooltip("Detruit l'orbe a la fin si l'objet courant est utilise.")]
    public bool destroyOnStop = true;

    private Transform orbitTarget;
    private Transform orbitPivot;
    private GameObject orbeInstance;
    private float remainingTime;
    private bool useDurationLimit;
    private bool usingSelf;
    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;

    public bool IsActive => orbeInstance != null;

    public bool Play(SquadCharacterController controller)
    {
        return Play(controller != null ? controller.transform : null, orbitDuration);
    }

    public bool Play(GameObject target)
    {
        return Play(target != null ? target.transform : null, orbitDuration);
    }

    public bool Play(Transform target)
    {
        return Play(target, orbitDuration);
    }

    public bool Play(Transform target, float durationSeconds)
    {
        Stop();

        if (target == null)
        {
            return false;
        }

        orbitTarget = target;

        GameObject pivotObject = new GameObject("OrbeOrbitPivot");
        orbitPivot = pivotObject.transform;
        orbitPivot.SetParent(target, false);
        orbitPivot.localPosition = orbitOffset;
        orbitPivot.localRotation = Quaternion.identity;

        float initialAngle = randomStartAngle ? Random.Range(0f, 360f) : startAngle;
        orbitPivot.localRotation = Quaternion.AngleAxis(initialAngle, GetOrbitAxis());

        if (orbePrefab != null)
        {
            orbeInstance = Instantiate(orbePrefab, orbitPivot);
            usingSelf = false;
        }
        else
        {
            usingSelf = true;
            orbeInstance = gameObject;
            originalParent = transform.parent;
            originalLocalPosition = transform.localPosition;
            originalLocalRotation = transform.localRotation;
            originalLocalScale = transform.localScale;
            transform.SetParent(orbitPivot, false);
        }

        orbeInstance.transform.localPosition = new Vector3(Mathf.Max(0f, orbitRadius), 0f, 0f);
        orbeInstance.transform.localRotation = Quaternion.identity;
        if (usingSelf)
        {
            transform.localScale = originalLocalScale;
        }

        float duration = durationSeconds > 0f ? durationSeconds : orbitDuration;
        useDurationLimit = duration > 0f;
        remainingTime = duration;

        return true;
    }

    public void Stop()
    {
        if (orbitPivot == null)
        {
            orbitTarget = null;
            remainingTime = 0f;
            useDurationLimit = false;
            usingSelf = false;
            orbeInstance = null;
            return;
        }

        if (usingSelf)
        {
            if (!destroyOnStop)
            {
                transform.SetParent(originalParent, false);
                transform.localPosition = originalLocalPosition;
                transform.localRotation = originalLocalRotation;
                transform.localScale = originalLocalScale;
            }

            Destroy(orbitPivot.gameObject);
        }
        else
        {
            if (orbeInstance != null)
            {
                Destroy(orbeInstance);
            }

            Destroy(orbitPivot.gameObject);
        }

        orbeInstance = null;
        orbitPivot = null;
        orbitTarget = null;
        remainingTime = 0f;
        useDurationLimit = false;
        usingSelf = false;
    }

    private void OnDisable()
    {
        Stop();
    }

    private void Update()
    {
        if (orbitPivot == null || orbitTarget == null)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        float angleDelta = orbitSpeed * deltaTime;
        orbitPivot.Rotate(GetOrbitAxis(), angleDelta, Space.Self);

        if (useDurationLimit)
        {
            remainingTime -= deltaTime;
            if (remainingTime <= 0f)
            {
                Stop();
            }
        }
    }

    private Vector3 GetOrbitAxis()
    {
        if (orbitAxis.sqrMagnitude <= 0.0001f)
        {
            return Vector3.up;
        }

        return orbitAxis.normalized;
    }
}
