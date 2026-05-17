using System.Collections;
using UnityEngine;

// Effet de brasero qui anime la rotation d'une cible.
public class BraseroRotationEffect : MonoBehaviour
{
    public enum RotationTrigger
    {
        WhenLit,
        WhenUnlit,
        OnStateChanged
    }

    public enum RotationLimitMode
    {
        Duration,
        Degrees
    }

    [Header("Source")]
    [SerializeField, Tooltip("Brasero observe. Si vide, utilise le Brasero du meme GameObject.")]
    private Brasero brasero;
    [SerializeField, Tooltip("Declenche l'effet au OnEnable si l'etat actuel du brasero correspond.")]
    private bool playOnEnableIfStateMatches = true;

    [Header("Target")]
    [SerializeField, Tooltip("GameObject a faire tourner. Si vide, utilise ce GameObject.")]
    private Transform target;
    [SerializeField, Tooltip("Etat du brasero qui declenche la rotation.")]
    private RotationTrigger trigger = RotationTrigger.WhenLit;
    [SerializeField, Tooltip("Stoppe la rotation si le brasero passe dans un etat qui ne correspond plus.")]
    private bool stopWhenStateNoLongerMatches = true;

    [Header("Rotation")]
    [SerializeField, Tooltip("Espace de rotation utilise pour la cible.")]
    private Space rotationSpace = Space.Self;
    [SerializeField, Tooltip("Mode de limite de rotation.")]
    private RotationLimitMode limitMode = RotationLimitMode.Degrees;
    [SerializeField, Tooltip("Ajoute la rotation a l'orientation courante. Si false, cible des angles absolus.")]
    private bool relativeToCurrentRotation = true;
    [SerializeField, Tooltip("Rotation par seconde en mode Duration.")]
    private Vector3 eulerDegreesPerSecond = new Vector3(0f, 90f, 0f);
    [SerializeField, Tooltip("Duree de rotation en mode Duration.")]
    private float rotationDuration = 1f;
    [SerializeField, Tooltip("Angles cibles en mode Degrees.")]
    private Vector3 targetEulerDegrees = new Vector3(0f, 90f, 0f);
    [SerializeField, Tooltip("Duree du lerp en mode Degrees.")]
    private float lerpDuration = 1f;
    [SerializeField, Tooltip("Courbe de lerp appliquee entre la rotation de depart et la rotation cible.")]
    private AnimationCurve lerpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Tooltip("Utilise le temps non scale.")]
    private bool useUnscaledTime;

    private Coroutine rotationRoutine;

    private void Reset()
    {
        brasero = GetComponent<Brasero>();
        target = transform;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (brasero != null)
        {
            brasero.StateChanged += OnBraseroStateChanged;
            if (playOnEnableIfStateMatches && trigger != RotationTrigger.OnStateChanged && ShouldTrigger(brasero.IsLit))
            {
                StartRotation();
            }
        }
    }

    private void OnDisable()
    {
        if (brasero != null)
        {
            brasero.StateChanged -= OnBraseroStateChanged;
        }

        StopRotation();
    }

    private void OnValidate()
    {
        rotationDuration = Mathf.Max(0f, rotationDuration);
        lerpDuration = Mathf.Max(0f, lerpDuration);
        if (lerpCurve == null)
        {
            lerpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }

    private void ResolveReferences()
    {
        if (brasero == null)
        {
            brasero = GetComponent<Brasero>();
        }

        if (target == null)
        {
            target = transform;
        }
    }

    private void OnBraseroStateChanged(Brasero source, bool lit)
    {
        if (ShouldTrigger(lit))
        {
            StartRotation();
            return;
        }

        if (stopWhenStateNoLongerMatches)
        {
            StopRotation();
        }
    }

    private bool ShouldTrigger(bool lit)
    {
        switch (trigger)
        {
            case RotationTrigger.WhenLit:
                return lit;
            case RotationTrigger.WhenUnlit:
                return !lit;
            case RotationTrigger.OnStateChanged:
                return true;
            default:
                return false;
        }
    }

    private void StartRotation()
    {
        if (target == null)
        {
            return;
        }

        StopRotation();

        Vector3 startEuler = GetCurrentEulerAngles();
        Vector3 endEuler = ResolveEndEulerAngles(startEuler);
        float duration = ResolveAnimationDuration();

        if (duration <= 0f)
        {
            SetEulerAngles(endEuler);
            return;
        }

        rotationRoutine = StartCoroutine(RotateRoutine(startEuler, endEuler, duration));
    }

    private void StopRotation()
    {
        if (rotationRoutine == null)
        {
            return;
        }

        StopCoroutine(rotationRoutine);
        rotationRoutine = null;
    }

    private Vector3 ResolveEndEulerAngles(Vector3 startEuler)
    {
        if (limitMode == RotationLimitMode.Duration)
        {
            return startEuler + eulerDegreesPerSecond * Mathf.Max(0f, rotationDuration);
        }

        return relativeToCurrentRotation ? startEuler + targetEulerDegrees : targetEulerDegrees;
    }

    private float ResolveAnimationDuration()
    {
        return limitMode == RotationLimitMode.Duration
            ? Mathf.Max(0f, rotationDuration)
            : Mathf.Max(0f, lerpDuration);
    }

    private IEnumerator RotateRoutine(Vector3 startEuler, Vector3 endEuler, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float normalized = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float eased = lerpCurve != null ? lerpCurve.Evaluate(normalized) : normalized;
            SetEulerAngles(Vector3.LerpUnclamped(startEuler, endEuler, eased));

            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        SetEulerAngles(endEuler);
        rotationRoutine = null;
    }

    private Vector3 GetCurrentEulerAngles()
    {
        return rotationSpace == Space.World ? target.eulerAngles : target.localEulerAngles;
    }

    private void SetEulerAngles(Vector3 eulerAngles)
    {
        if (rotationSpace == Space.World)
        {
            target.eulerAngles = eulerAngles;
            return;
        }

        target.localEulerAngles = eulerAngles;
    }
}
