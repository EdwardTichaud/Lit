using System.Collections.Generic;
using UnityEngine;
using UccCharacterLocomotion = Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion;

// Role: centralise le ralenti de presentation declenche par les Animation Events de combat.
// Usage: appele par CombatAnimationEvents; les autres systemes peuvent seulement restaurer par securite.
// Responsibilities: ralentir/restaurer les acteurs cibles sans toucher a Time.timeScale.
// Dependencies: Animator, Opsive UltimateCharacterLocomotion, AudioManager.
// Precautions: effet de presentation uniquement; le serveur Netcode pur ne doit pas l'appliquer directement.
public sealed class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Combat Time")]
    [SerializeField, Range(0.2f, 1f)] private float combatSlowMusicDuckMultiplier = 0.72f;

    private bool presentationTimeScaleActive;
    private float presentationTimeScale = 1f;
    private bool combatSlowAudioActive;
    private bool combatSlowMusicDuckingActive;
    private AudioManager combatSlowMusicDuckingManager;
    private readonly Dictionary<Animator, float> combatAnimatorSpeeds = new Dictionary<Animator, float>();
    private readonly Dictionary<UccCharacterLocomotion, float> combatCharacterTimeScales = new Dictionary<UccCharacterLocomotion, float>();
    private readonly List<Animator> animatorRemovalBuffer = new List<Animator>();
    private readonly List<UccCharacterLocomotion> locomotionRemovalBuffer = new List<UccCharacterLocomotion>();

    public float CombatPresentationDeltaTime => Time.unscaledDeltaTime * CombatTimeMultiplier;

    public static TimeManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindAnyObjectByType<TimeManager>();
#else
        Instance = FindObjectOfType<TimeManager>();
#endif
        if (Instance != null)
        {
            return Instance;
        }

        GameObject host = new GameObject("TimeManager");
        Instance = host.AddComponent<TimeManager>();
        return Instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDisable()
    {
        RestoreAllCombatTime();
    }

    private void OnDestroy()
    {
        RestoreAllCombatTime();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        if (!presentationTimeScaleActive)
        {
            UpdateCombatSlowAudioState();
            return;
        }

        ApplyCombatCharacterTimeScales();
        ApplyCombatAnimatorSpeeds();
        UpdateCombatSlowAudioState();
    }

    public void SetCombatPresentationTimeScale(Transform target, float timeScale, bool active)
    {
        presentationTimeScaleActive = active && target != null;
        presentationTimeScale = presentationTimeScaleActive ? Mathf.Clamp(timeScale, 0.01f, 1f) : 1f;

        if (presentationTimeScaleActive)
        {
            TrackCharacterLocomotions(target);
            TrackAnimators(target);
            ApplyCombatCharacterTimeScales();
            ApplyCombatAnimatorSpeeds();
            UpdateCombatSlowAudioState();
            return;
        }

        RestoreCombatTime();
    }

    public void RestoreCombatTime()
    {
        RestoreAllCombatTime();
    }

    public static float GetCombatPresentationDeltaTime()
    {
        return Instance != null ? Instance.CombatPresentationDeltaTime : Time.deltaTime;
    }

    private float CombatTimeMultiplier => presentationTimeScaleActive ? presentationTimeScale : 1f;

    private bool HasCombatSlowAudioState()
    {
        return presentationTimeScaleActive && presentationTimeScale < 0.999f;
    }

    private void UpdateCombatSlowAudioState()
    {
        bool active = HasCombatSlowAudioState();
        bool audioStateChanged = active != combatSlowAudioActive;
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null && active)
        {
            audioManager = AudioManager.EnsureInstance();
        }

        UpdateCombatSlowMusicDucking(active, audioManager);
        if (!audioStateChanged)
        {
            return;
        }

        combatSlowAudioActive = active;
        if (audioManager == null)
        {
            return;
        }

        audioManager.PlayUiActionCue(active
            ? ActionAudioCue.CombatTimeSlow
            : ActionAudioCue.CombatTimeResume);
    }

    private void UpdateCombatSlowMusicDucking(bool active, AudioManager audioManager)
    {
        if (active)
        {
            if (combatSlowMusicDuckingActive || audioManager == null)
            {
                return;
            }

            audioManager.BeginMusicDucking(combatSlowMusicDuckMultiplier);
            combatSlowMusicDuckingManager = audioManager;
            combatSlowMusicDuckingActive = true;
            return;
        }

        if (!combatSlowMusicDuckingActive)
        {
            return;
        }

        AudioManager duckingManager = combatSlowMusicDuckingManager != null
            ? combatSlowMusicDuckingManager
            : AudioManager.Instance;
        duckingManager?.EndMusicDucking();
        combatSlowMusicDuckingManager = null;
        combatSlowMusicDuckingActive = false;
    }

    private void TrackCharacterLocomotions(Transform root)
    {
        if (root == null)
        {
            return;
        }

        UccCharacterLocomotion[] locomotions = root.GetComponentsInChildren<UccCharacterLocomotion>(true);
        if (locomotions == null)
        {
            return;
        }

        for (int i = 0; i < locomotions.Length; i++)
        {
            UccCharacterLocomotion locomotion = locomotions[i];
            if (locomotion == null || combatCharacterTimeScales.ContainsKey(locomotion))
            {
                continue;
            }

            combatCharacterTimeScales.Add(locomotion, locomotion.TimeScale);
        }
    }

    private void TrackAnimators(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        if (animators == null)
        {
            return;
        }

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || combatAnimatorSpeeds.ContainsKey(animator))
            {
                continue;
            }

            combatAnimatorSpeeds.Add(animator, animator.speed);
        }
    }

    private void ApplyCombatAnimatorSpeeds()
    {
        if (combatAnimatorSpeeds.Count == 0)
        {
            return;
        }

        float multiplier = CombatTimeMultiplier;
        animatorRemovalBuffer.Clear();
        foreach (KeyValuePair<Animator, float> pair in combatAnimatorSpeeds)
        {
            if (pair.Key == null)
            {
                animatorRemovalBuffer.Add(pair.Key);
                continue;
            }

            pair.Key.speed = pair.Value * multiplier;
        }

        for (int i = 0; i < animatorRemovalBuffer.Count; i++)
        {
            combatAnimatorSpeeds.Remove(animatorRemovalBuffer[i]);
        }
    }

    private void ApplyCombatCharacterTimeScales()
    {
        if (combatCharacterTimeScales.Count == 0)
        {
            return;
        }

        float multiplier = CombatTimeMultiplier;
        locomotionRemovalBuffer.Clear();
        foreach (KeyValuePair<UccCharacterLocomotion, float> pair in combatCharacterTimeScales)
        {
            if (pair.Key == null)
            {
                locomotionRemovalBuffer.Add(pair.Key);
                continue;
            }

            pair.Key.TimeScale = Mathf.Max(0f, pair.Value * multiplier);
        }

        for (int i = 0; i < locomotionRemovalBuffer.Count; i++)
        {
            combatCharacterTimeScales.Remove(locomotionRemovalBuffer[i]);
        }
    }

    private void RestoreCombatCharacterTimeScales()
    {
        if (combatCharacterTimeScales.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<UccCharacterLocomotion, float> pair in combatCharacterTimeScales)
        {
            if (pair.Key != null)
            {
                pair.Key.TimeScale = pair.Value;
            }
        }

        combatCharacterTimeScales.Clear();
        locomotionRemovalBuffer.Clear();
    }

    private void RestoreCombatAnimatorSpeeds()
    {
        if (combatAnimatorSpeeds.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<Animator, float> pair in combatAnimatorSpeeds)
        {
            if (pair.Key != null)
            {
                pair.Key.speed = pair.Value;
            }
        }

        combatAnimatorSpeeds.Clear();
        animatorRemovalBuffer.Clear();
    }

    private void RestoreAllCombatTime()
    {
        presentationTimeScaleActive = false;
        presentationTimeScale = 1f;
        RestoreCombatCharacterTimeScales();
        RestoreCombatAnimatorSpeeds();
        UpdateCombatSlowAudioState();
    }
}
