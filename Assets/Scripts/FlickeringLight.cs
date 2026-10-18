using UnityEngine;

/// <summary>
/// Slightly moves the GameObject carrying a Light so its shadows flicker naturally.
/// It never changes the Light's colour, intensity, range, shadows or type.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class FlickeringLight : MonoBehaviour
{
    [SerializeField] private Light targetLight;
    [SerializeField] private bool searchInChildren = true;
    [SerializeField, Tooltip("Anime la lumiere en mode edition. Desactive par defaut pour eviter les mises a jour d'ombres pendant l'authoring.")]
    private bool previewInEditMode;

    [Header("Shadow movement")]
    [Tooltip("Maximum local offset, in metres, applied to the GameObject carrying the Light.")]
    [SerializeField, Min(0f)] private float movementAmplitude = 0.015f;
    [SerializeField, Min(0.1f)] private float movementSpeed = 1.6f;

    private Transform movedTransform;
    private Vector3 authoredLocalPosition;
    private bool hasAuthoredPosition;
    private bool hasAppliedMovement;
    private float noiseSeedX;
    private float noiseSeedZ;

    /// <summary>Targets this Light and leaves every Light setting authored by its owner unchanged.</summary>
    public void ConfigureForFlame(Light light)
    {
        targetLight = light;
        searchInChildren = false;
        CacheTarget();
        CacheAuthoredPosition();
        InitializeNoiseSeeds();
    }

    private void Reset()
    {
        CacheTarget();
        CacheAuthoredPosition();
        InitializeNoiseSeeds();
    }

    private void Awake()
    {
        CacheTarget();
        CacheAuthoredPosition();
        InitializeNoiseSeeds();
    }

    private void OnEnable()
    {
        CacheTarget();
        CacheAuthoredPosition();
        InitializeNoiseSeeds();
        ApplyMovement(Time.time);
    }

    private void OnDisable()
    {
        RestoreAuthoredPosition();
    }

    private void OnValidate()
    {
        if (!Application.IsPlaying(gameObject) && !previewInEditMode) RestoreAuthoredPosition();
        movementAmplitude = Mathf.Max(0f, movementAmplitude);
        movementSpeed = Mathf.Max(0.1f, movementSpeed);
        CacheTarget();
        CacheAuthoredPosition();
    }

    private void LateUpdate()
    {
        ApplyMovement(Time.time);
    }

    private void CacheTarget()
    {
        if (targetLight == null)
            targetLight = searchInChildren ? GetComponentInChildren<Light>(true) : GetComponent<Light>();

        movedTransform = targetLight != null ? targetLight.transform : null;
    }

    private void CacheAuthoredPosition()
    {
        if (movedTransform == null)
            return;

        authoredLocalPosition = movedTransform.localPosition;
        hasAuthoredPosition = true;
    }

    private void InitializeNoiseSeeds()
    {
        int seed = Mathf.Abs(GetEntityId().GetHashCode()) + 1;
        noiseSeedX = (seed * 0.173f) + 3.1f;
        noiseSeedZ = (seed * 0.317f) + 11.7f;
    }

    private void ApplyMovement(float timeValue)
    {
        if (!Application.IsPlaying(gameObject) && !previewInEditMode)
        {
            RestoreAuthoredPosition();
            return;
        }
        if (movedTransform == null || targetLight == null || !targetLight.isActiveAndEnabled)
            return;

        if (!hasAuthoredPosition)
            CacheAuthoredPosition();

        float offsetX = SampleSignedNoise(noiseSeedX, timeValue, movementSpeed) * movementAmplitude;
        float offsetZ = SampleSignedNoise(noiseSeedZ, timeValue, movementSpeed * 1.17f) * movementAmplitude;
        movedTransform.localPosition = authoredLocalPosition + new Vector3(offsetX, 0f, offsetZ);
        hasAppliedMovement = true;
    }

    private void RestoreAuthoredPosition()
    {
        if (movedTransform != null && hasAuthoredPosition && hasAppliedMovement)
            movedTransform.localPosition = authoredLocalPosition;
        hasAppliedMovement = false;
    }

    private static float SampleSignedNoise(float seed, float timeValue, float speed)
    {
        return (Mathf.PerlinNoise(seed, timeValue * speed) * 2f) - 1f;
    }
}
