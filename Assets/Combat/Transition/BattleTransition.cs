using System;
using System.Collections;
using INab.VFXAssets;
using UnityEngine;
using UnityEngine.VFX;

// Role: orchestrates the local combat entry transition from BattleManager.
// Usage: CombatSessionManager calls this before moving actors into the arena.
// Responsibilities: trigger the screen wave, start combat audio, and warm BattleSphere/VFX.
// Dependencies: ScreenWaveController, CombatTransitionController, CharacterEffect.
// Precaution: the covered action must always run, even when the transition is interrupted.
public sealed class BattleTransition : MonoBehaviour
{
    public static BattleTransition Instance { get; private set; }

    [Header("Screen Wave")]
    [SerializeField] private ScreenWaveController screenWaveController;
    [SerializeField, Min(0.2f)] private float fallbackEnterDuration = 0.9f;
    [SerializeField] private bool freezeTimeScaleDuringEntryWave = true;
    [SerializeField] private bool playInverseWaveAfterPlacement = true;

    [Header("Preload")]
    [SerializeField] private GameObject battleSpherePrefab;
    [SerializeField] private ShaderVariantCollection shaderVariants;
    [SerializeField] private Material[] preloadMaterials;
    [SerializeField] private GameObject[] preloadPrefabs;
    [SerializeField] private bool warmBattleSphereOnStart = true;
    [SerializeField] private Vector3 warmupPosition = new Vector3(0f, -10000f, 0f);

    public float EnterPeakDelaySeconds => ResolveEntryFreezeDuration();

    private Coroutine transitionRoutine;
    private Action pendingCoveredAction;
    private bool entryFreezeActive;
    private float previousTimeScale = 1f;

    public static BattleTransition EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindAnyObjectByType<BattleTransition>();
#else
        Instance = FindObjectOfType<BattleTransition>();
#endif
        return Instance;
    }

    public void PlayEnterTransition(Vector3 worldCenter, Action coveredAction, bool playVisual = true)
    {
        if (transitionRoutine != null)
        {
            RestoreEntryFreeze();
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        InvokePendingCoveredAction();
        pendingCoveredAction = coveredAction;
        transitionRoutine = StartCoroutine(EnterRoutine(worldCenter, playVisual));
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
    }

    private IEnumerator Start()
    {
        if (!Application.isPlaying)
        {
            yield break;
        }

        yield return PreloadRoutine();
    }

    private void OnDestroy()
    {
        RestoreEntryFreeze();
        InvokePendingCoveredAction();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private IEnumerator EnterRoutine(Vector3 worldCenter, bool playVisual)
    {
        ScreenWaveController wave = playVisual ? ResolveScreenWaveController() : null;
        float duration = ResolveEnterDuration(wave, playInverseWaveAfterPlacement);
        float freezeDuration = ResolveEntryFreezeDuration(wave);
        bool covered = false;
        Vector2 waveOrigin = ResolveWaveOrigin(worldCenter);

        if (playVisual)
        {
            CombatTransitionController combatTransition = CombatTransitionController.EnsureInstance();
            if (combatTransition != null)
            {
                combatTransition.BeginCombatEntryAudioAndMusic();
            }

            if (wave != null)
            {
                BeginEntryFreeze();
                if (playInverseWaveAfterPlacement)
                {
                    wave.PlayScreenWaveCycle(waveOrigin);
                }
                else
                {
                    wave.PlayScreenWavePhase(waveOrigin, false);
                }
            }
        }

        float time = 0f;
        while (time < duration)
        {
            if (!covered && time >= freezeDuration)
            {
                covered = true;
                RestoreEntryFreeze();
                InvokePendingCoveredAction();
            }

            time += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!covered)
        {
            RestoreEntryFreeze();
            InvokePendingCoveredAction();
        }

        transitionRoutine = null;
    }

    private IEnumerator PreloadRoutine()
    {
        if (shaderVariants != null)
        {
            shaderVariants.WarmUp();
        }

        ScreenWaveController wave = ResolveScreenWaveController();
        if (wave != null)
        {
            wave.WarmUp();
        }

        WarmMaterials(preloadMaterials);

        if (warmBattleSphereOnStart)
        {
            yield return WarmPrefabRoutine(ResolveBattleSpherePrefab());
        }

        if (preloadPrefabs == null)
        {
            yield break;
        }

        GameObject defaultBattleSphere = ResolveBattleSpherePrefab();
        for (int i = 0; i < preloadPrefabs.Length; i++)
        {
            GameObject prefab = preloadPrefabs[i];
            if (prefab == null || prefab == defaultBattleSphere)
            {
                continue;
            }

            yield return WarmPrefabRoutine(prefab);
        }
    }

    private ScreenWaveController ResolveScreenWaveController()
    {
        if (screenWaveController != null)
        {
            return screenWaveController;
        }

        screenWaveController = GetComponent<ScreenWaveController>();
        if (screenWaveController != null)
        {
            return screenWaveController;
        }

        screenWaveController = ScreenWaveController.EnsureInstance();
        return screenWaveController;
    }

    private float ResolveEnterDuration(ScreenWaveController wave = null, bool includeInversePhase = true)
    {
        ScreenWaveController resolvedWave = wave != null ? wave : ResolveScreenWaveController();
        if (resolvedWave != null)
        {
            float duration = includeInversePhase ? resolvedWave.TotalDuration : resolvedWave.SinglePhaseDuration;
            return Mathf.Max(0.2f, duration);
        }

        return Mathf.Max(0.2f, fallbackEnterDuration);
    }

    private float ResolveEntryFreezeDuration(ScreenWaveController wave = null)
    {
        ScreenWaveController resolvedWave = wave != null ? wave : ResolveScreenWaveController();
        return resolvedWave != null ? Mathf.Max(0.2f, resolvedWave.MainDuration) : Mathf.Max(0.2f, fallbackEnterDuration);
    }

    private void BeginEntryFreeze()
    {
        if (!freezeTimeScaleDuringEntryWave || entryFreezeActive)
        {
            return;
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        entryFreezeActive = true;
    }

    private void RestoreEntryFreeze()
    {
        if (!entryFreezeActive)
        {
            return;
        }

        Time.timeScale = previousTimeScale;
        entryFreezeActive = false;
    }

    private Vector2 ResolveWaveOrigin(Vector3 worldCenter)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return new Vector2(0.5f, 0.5f);
        }

        Vector3 viewport = camera.WorldToViewportPoint(worldCenter);
        if (viewport.z <= 0f ||
            float.IsNaN(viewport.x) ||
            float.IsNaN(viewport.y) ||
            float.IsInfinity(viewport.x) ||
            float.IsInfinity(viewport.y))
        {
            return new Vector2(0.5f, 0.5f);
        }

        return new Vector2(Mathf.Clamp01(viewport.x), Mathf.Clamp01(viewport.y));
    }

    private GameObject ResolveBattleSpherePrefab()
    {
        if (battleSpherePrefab != null)
        {
            return battleSpherePrefab;
        }

        CombatSessionManager manager = GetComponent<CombatSessionManager>();
        return manager != null ? manager.combatEntryMidpointPrefab : null;
    }

    private IEnumerator WarmPrefabRoutine(GameObject prefab)
    {
        if (prefab == null)
        {
            yield break;
        }

        GameObject instance = Instantiate(prefab, warmupPosition, Quaternion.identity);
        instance.name = $"{prefab.name}_Warmup";
        DisableColliders(instance);

        WarmRenderers(instance);
        VisualEffect[] visualEffects = instance.GetComponentsInChildren<VisualEffect>(true);
        CharacterEffect[] characterEffects = instance.GetComponentsInChildren<CharacterEffect>(true);

        for (int i = 0; i < visualEffects.Length; i++)
        {
            if (visualEffects[i] == null)
            {
                continue;
            }

            visualEffects[i].Reinit();
            visualEffects[i].Play();
        }

        for (int i = 0; i < characterEffects.Length; i++)
        {
            if (characterEffects[i] != null)
            {
                characterEffects[i].StartEffect();
            }
        }

        yield return null;

        for (int i = 0; i < characterEffects.Length; i++)
        {
            if (characterEffects[i] != null)
            {
                characterEffects[i].StopEffect();
            }
        }

        for (int i = 0; i < visualEffects.Length; i++)
        {
            if (visualEffects[i] != null)
            {
                visualEffects[i].Stop();
            }
        }

        Destroy(instance);
    }

    private static void DisableColliders(GameObject instance)
    {
        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private static void WarmRenderers(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            _ = renderers[i].sharedMaterials;
        }
    }

    private static void WarmMaterials(Material[] materials)
    {
        if (materials == null)
        {
            return;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null || material.shader == null)
            {
                continue;
            }

            material.SetPass(0);
        }
    }

    private void InvokePendingCoveredAction()
    {
        Action action = pendingCoveredAction;
        pendingCoveredAction = null;
        action?.Invoke();
    }
}
