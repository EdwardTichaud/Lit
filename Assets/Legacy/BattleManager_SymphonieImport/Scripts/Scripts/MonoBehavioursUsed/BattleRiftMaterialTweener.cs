using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleRiftMaterialTweener : MonoBehaviour
{
    [Header("Référence Material")]
    [Tooltip("Le Material instancié qui utilise ton shader RiftDistort_Waves")]
    public Material riftMaterial;

    [Header("Scène de combat")]
    [Tooltip("Nom exact de la scène de combat à comparer")]
    public string battleSceneName = "BattleScene";

    [Header("Cibles de la tween")]
    public float targetFrequency = 50f;
    public float targetAmplitude = 0.1f;
    public float targetSpeed = 1f;
    public float tweenDuration = 2f;
    public float tweenToZeroDuration = 0.5f;

    // Valeurs de départ, capturées au lancement
    private float startFrequency;
    private float startAmplitude;
    private float startSpeed;

    private Coroutine _tweenCoroutine;

    void Awake()
    {
        // Si on n'est pas dans la scène de combat, on force tout à zéro
        if (SceneManager.GetActiveScene().name != battleSceneName)
        {
            riftMaterial.SetFloat("_WaveFrequency", 0f);
            riftMaterial.SetFloat("_WaveAmplitude", 0f);
            riftMaterial.SetFloat("_WaveSpeed", 0f);

            // On considère que les valeurs de départ sont aussi 0
            startFrequency = startAmplitude = startSpeed = 0f;
        }
        else
        {
            // Sinon, on lit les valeurs existantes dans le material
            startFrequency = riftMaterial.GetFloat("_WaveFrequency");
            startAmplitude = riftMaterial.GetFloat("_WaveAmplitude");
            startSpeed = riftMaterial.GetFloat("_WaveSpeed");
        }
    }

    /// <summary>
    /// Démarre (ou relance) la tween des paramètres du shader.
    /// </summary>
    public void PlayCombatTween(
        float? frequency = null,
        float? amplitude = null,
        float? speed = null)
    {
        if (_tweenCoroutine != null)
            StopCoroutine(_tweenCoroutine);

        if (frequency.HasValue) targetFrequency = frequency.Value;
        if (amplitude.HasValue) targetAmplitude = amplitude.Value;
        if (speed.HasValue) targetSpeed = speed.Value;

        _tweenCoroutine = StartCoroutine(TweenRoutine());
    }

    private IEnumerator TweenRoutine()
    {
        float elapsed = 0f;

        // 🔁 lire les valeurs réelles actuelles du material (et pas startFrequency qui date d'Awake)
        float currentFreq = riftMaterial.GetFloat("_WaveFrequency");
        float currentAmp = riftMaterial.GetFloat("_WaveAmplitude");
        float currentSpeed = riftMaterial.GetFloat("_WaveSpeed");

        while (elapsed < 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.5f);

            float freq = Mathf.Lerp(currentFreq, targetFrequency, t);
            float amp = Mathf.Lerp(currentAmp, targetAmplitude, t);
            float speed = Mathf.Lerp(currentSpeed, targetSpeed, t);

            riftMaterial.SetFloat("_WaveFrequency", freq);
            riftMaterial.SetFloat("_WaveAmplitude", amp);
            riftMaterial.SetFloat("_WaveSpeed", speed);

            yield return null;
        }

        // Fin de tween : appliquer les cibles finales
        riftMaterial.SetFloat("_WaveFrequency", targetFrequency);
        riftMaterial.SetFloat("_WaveAmplitude", targetAmplitude);
        riftMaterial.SetFloat("_WaveSpeed", targetSpeed);

        _tweenCoroutine = null;
    }

    public IEnumerator TweenToZeroRoutine()
    {
        float elapsed = 0f;

        // capture valeurs de départ (état actuel du shader)
        float currentFreq = riftMaterial.GetFloat("_WaveFrequency");
        float currentAmp = riftMaterial.GetFloat("_WaveAmplitude");
        float currentSpeed = riftMaterial.GetFloat("_WaveSpeed");

        while (elapsed < tweenToZeroDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / tweenToZeroDuration);

            float freq = Mathf.Lerp(currentFreq, 0f, t);
            float amp = Mathf.Lerp(currentAmp, 0f, t);
            float speed = Mathf.Lerp(currentSpeed, 0f, t);

            riftMaterial.SetFloat("_WaveFrequency", freq);
            riftMaterial.SetFloat("_WaveAmplitude", amp);
            riftMaterial.SetFloat("_WaveSpeed", speed);

            yield return null;
        }

        // assurer que c'est bien zéro à la fin
        riftMaterial.SetFloat("_WaveFrequency", 0f);
        riftMaterial.SetFloat("_WaveAmplitude", 0f);
        riftMaterial.SetFloat("_WaveSpeed", 0f);

        _tweenCoroutine = null;
    }

}
