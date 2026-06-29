using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Déclenche un effet de bris de caméra en combat.
/// </summary>
public class BattleCameraShatter : MonoBehaviour
{
    [Tooltip("Son joué lors du bris de la caméra")]
    public AudioClipSO shatterSound;
    [Tooltip("Volume contenant les effets de post-processing")]
    public Volume volume;
    [Tooltip("Texture utilisée pour la salissure de l'objectif")]
    public Texture fractureTex;

    private LensDistortion lensDistortion;
    private Bloom bloom;

    private float baseLensIntensity;
    private float baseDirtIntensity;
    private Texture baseDirtTexture;
    private float baseBloomThreshold;
    private float baseBloomIntensity;

    private void Awake()
    {
        if (volume != null && volume.profile != null)
        {
            volume.profile.TryGet(out lensDistortion);
            volume.profile.TryGet(out bloom);

            if (lensDistortion != null)
                baseLensIntensity = lensDistortion.intensity.value;

            if (bloom != null)
            {
                baseDirtIntensity = bloom.dirtIntensity.value;
                baseDirtTexture = bloom.dirtTexture.value;
                baseBloomThreshold = bloom.threshold.value;
                baseBloomIntensity = bloom.intensity.value;
            }
        }
    }

    /// <summary>
    /// Joue le son et l'effet de bris de caméra.
    /// </summary>
    public void Break()
    {
        if (shatterSound != null)
            AudioManager.Instance?.PlaySound(shatterSound);

        if (lensDistortion != null && bloom != null)
            StartCoroutine(ShatterRoutine());
    }

    /// <summary>
    /// Réinitialise les paramètres de post-processing modifiés par l'effet.
    /// </summary>
    public void ResetEffect()
    {
        if (lensDistortion != null)
            lensDistortion.intensity.value = baseLensIntensity;

        if (bloom != null)
        {
            bloom.dirtIntensity.value = baseDirtIntensity;
            bloom.dirtTexture.value = baseDirtTexture;
            bloom.threshold.value = baseBloomThreshold;
            bloom.intensity.value = baseBloomIntensity;
        }
    }

    private IEnumerator ShatterRoutine()
    {
        bloom.dirtIntensity.value = 50f;
        bloom.dirtTexture.value = fractureTex;
        bloom.threshold.value = 0.4f;
        bloom.intensity.value = 1f;

        float duration = 0.4f;
        float timer = 0f;
        while (timer < duration)
        {
            float normalized = timer / duration;
            float strength = Mathf.Sin(normalized * Mathf.PI);
            lensDistortion.intensity.value = Mathf.Lerp(0f, -0.6f, strength);
            timer += Time.deltaTime;
            yield return null;
        }

        lensDistortion.intensity.value = 0f;
    }
}
