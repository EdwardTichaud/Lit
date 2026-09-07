using System;
using System.Collections;
using System.Collections.Generic;
using INab.VFXAssets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable bond between an incarnation and its companion spirit. The component
/// owns presentation state only: combat weapons remain driven by their existing
/// animation events.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpiritBondController : MonoBehaviour
{
    private static readonly int MeltStateHash = Animator.StringToHash("Melt");

    [SerializeField, Tooltip("Incarnation that hosts this spirit. Empty resolves to the parent character.")]
    private Transform hostCharacter;
    [SerializeField, Tooltip("Only this visual root is hidden while the spirit is fused; companion gameplay remains active.")]
    private GameObject spiritVisualRoot;
    [SerializeField, Tooltip("CharacterEffect configured with the Holy prefab on the incarnation.")]
    private CharacterEffect holyEffect;
    [SerializeField, Min(0f), Tooltip("Delay used to let Holy read before the spirit visual changes state.")]
    private float transitionSeconds = 0.35f;
    [Header("Melt Presentation")]
    [SerializeField, Tooltip("Local camera profile played before the Melt Animation Event confirms fusion.")]
    private CameraProfilSO meltCameraProfile;
    [SerializeField, Min(0f)] private float meltFrostFadeSeconds = 0.5f;
    [SerializeField, Min(0f)] private float meltHdrIntensitySeconds = 1f;

    private Coroutine transitionRoutine;
    private Coroutine meltPresentationRoutine;
    private Animator hostAnimator;
    private SquadCharacterController hostCharacterController;
    private CanvasGroup muninUiEffectsCanvasGroup;
    private Image lightFrostImage;
    private Material lightFrostMaterial;
    private float meltInitialCanvasAlpha;
    private bool meltInitialCanvasInteractable;
    private bool meltInitialCanvasBlocksRaycasts;
    private Color meltInitialHdrColor;
    private float meltInitialHdrIntensity;
    private bool meltUiStateCaptured;
    private PlayerSword[] swords = Array.Empty<PlayerSword>();
    private PlayerBow[] bows = Array.Empty<PlayerBow>();
    private readonly HashSet<SpiritWeaponManifestation> externalManifestations = new HashSet<SpiritWeaponManifestation>();
    private bool fused;
    private bool cinematicFusion;
    private bool holyEffectAwaitingMeltExit;
    private bool meltPresentationActive;
    private bool meltMovementLockHeld;
    private bool meltStateObserved;

    public bool IsFused => fused;
    public bool IsCinematicFusion => cinematicFusion;
    public event Action<SpiritBondController, bool> FusionStateChanged;

    private void Awake()
    {
        ResolveReferences();
        BindWeaponManifestations();
        RefreshSpiritVisibility();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindWeaponManifestations();
        RefreshSpiritVisibility();
    }

    private void Update()
    {
        // A transition can interrupt the clip before its trailing event runs.
        // Leaving Melt is therefore the fallback authority for its effect.
        if (holyEffectAwaitingMeltExit && !IsMeltPlaying())
        {
            holyEffectAwaitingMeltExit = false;
            StopHoly();
        }

        if (meltMovementLockHeld)
        {
            if (IsMeltPlaying())
            {
                meltStateObserved = true;
            }
            else if (meltStateObserved && !meltPresentationActive)
            {
                ReleaseMeltMovementLock();
            }
        }
    }

    private void OnDisable()
    {
        UnbindWeaponManifestations();
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (meltPresentationRoutine != null)
        {
            StopCoroutine(meltPresentationRoutine);
            meltPresentationRoutine = null;
        }

        meltPresentationActive = false;
        ReleaseMeltMovementLock();
        RestoreMeltUiPresentation();
        CameraProfilPlayer.GetOrCreate(LitCameraDirector.Instance)?.Cancel();

        cinematicFusion = false;
        fused = false;
        holyEffectAwaitingMeltExit = false;
        RefreshSpiritVisibility();
        holyEffect?.StopEffect();
    }

    public bool ToggleManualFusion()
    {
        if (cinematicFusion)
        {
            return false;
        }

        if (fused)
        {
            BeginManualDefusion();
        }
        else
        {
            BeginManualFusion();
        }

        return true;
    }

    /// <summary>
    /// Requests the player animation that changes the manual bond state. The
    /// corresponding AnimationEvent confirms the state at the authored frame.
    /// </summary>
    public bool RequestMeltAnimation()
    {
        if (cinematicFusion)
        {
            return false;
        }

        if (!fused)
        {
            return MeltTheIce();
        }

        ResolveReferences();
        if (hostAnimator == null)
        {
            return false;
        }

        hostAnimator.ResetTrigger("Melt");
        hostAnimator.SetTrigger("Rupture");
        return true;
    }

    /// <summary>
    /// Starts the local presentation for a manual fusion, then lets the Melt
    /// Animation Event remain the sole authority that confirms gameplay fusion.
    /// </summary>
    public bool MeltTheIce()
    {
        if (cinematicFusion || fused || meltPresentationActive || IsMeltPlaying())
        {
            return false;
        }

        ResolveReferences();
        if (hostAnimator == null)
        {
            return false;
        }

        meltPresentationActive = true;
        AcquireMeltMovementLock();
        StartMeltCameraPresentation();
        meltPresentationRoutine = StartCoroutine(PlayMeltUiPresentation());

        hostAnimator.ResetTrigger("Rupture");
        hostAnimator.SetTrigger("Melt");
        return true;
    }

    /// <summary>AnimationEvent: plays Holy at the precise authored frame.</summary>
    public void TriggerHolyEffectFromAnimationEvent()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SpiritBond] Frame {Time.frameCount}: Holy start requested by AnimationEvent.", this);
#endif
        ResolveReferences();
        PlayHoly();
        holyEffectAwaitingMeltExit = !cinematicFusion && IsMeltPlaying();
    }

    /// <summary>AnimationEvent: stops Holy at the precise authored frame.</summary>
    public void StopHolyEffectFromAnimationEvent()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SpiritBond] Frame {Time.frameCount}: Holy stop requested by AnimationEvent.", this);
#endif
        holyEffectAwaitingMeltExit = false;
        StopHoly();
    }

    /// <summary>AnimationEvent: completes the Melt animation's fusion.</summary>
    public void ConfirmMeltFusionFromAnimationEvent()
    {
        if (cinematicFusion)
        {
            return;
        }

        CancelTransition();
        fused = true;
        RefreshSpiritVisibility();
        NotifyFusionStateChanged();
    }

    /// <summary>AnimationEvent: completes the Rupture animation's defusion.</summary>
    public void ConfirmRuptureDefusionFromAnimationEvent()
    {
        if (cinematicFusion)
        {
            return;
        }

        CancelTransition();
        fused = false;
        RefreshSpiritVisibility();
        NotifyFusionStateChanged();
    }

    public void BeginLightSkillFusion()
    {
        ResolveReferences();
        CancelTransition();

        // A LightSkill always starts from a clean unfused state. If the player
        // was already fused, restore the companion immediately and without a
        // presentation transition before applying the cinematic fusion.
        if (fused)
        {
            fused = false;
            RefreshSpiritVisibility();
            NotifyFusionStateChanged();
        }

        cinematicFusion = true;
        fused = true;
        RefreshSpiritVisibility();
        PlayHoly();
        NotifyFusionStateChanged();
    }

    public void EndLightSkillFusion()
    {
        if (!cinematicFusion)
        {
            return;
        }

        cinematicFusion = false;
        fused = false;
        CancelTransition();
        PlayHoly();
        transitionRoutine = StartCoroutine(CompleteDefusionAfterEffect());
        NotifyFusionStateChanged();
    }

    public static SpiritBondController FindForCharacter(GameObject character)
    {
        return character != null ? character.GetComponentInChildren<SpiritBondController>(true) : null;
    }

    public void RegisterWeaponManifestation(SpiritWeaponManifestation manifestation)
    {
        if (manifestation != null && externalManifestations.Add(manifestation))
        {
            RefreshSpiritVisibility();
        }
    }

    public void UnregisterWeaponManifestation(SpiritWeaponManifestation manifestation)
    {
        if (manifestation != null && externalManifestations.Remove(manifestation))
        {
            RefreshSpiritVisibility();
        }
    }

    private void BeginManualFusion()
    {
        ResolveReferences();
        CancelTransition();
        fused = true;
        PlayHoly();
        transitionRoutine = StartCoroutine(CompleteFusionAfterEffect());
        NotifyFusionStateChanged();
    }

    private void BeginManualDefusion()
    {
        ResolveReferences();
        CancelTransition();
        fused = false;
        PlayHoly();
        transitionRoutine = StartCoroutine(CompleteDefusionAfterEffect());
        NotifyFusionStateChanged();
    }

    private IEnumerator CompleteFusionAfterEffect()
    {
        yield return WaitForTransition();
        RefreshSpiritVisibility();
        StopHoly();
        transitionRoutine = null;
    }

    private IEnumerator CompleteDefusionAfterEffect()
    {
        yield return WaitForTransition();
        RefreshSpiritVisibility();
        StopHoly();
        transitionRoutine = null;
    }

    private IEnumerator WaitForTransition()
    {
        if (transitionSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(transitionSeconds);
        }
    }

    private void CancelTransition()
    {
        if (transitionRoutine == null)
        {
            return;
        }

        StopCoroutine(transitionRoutine);
        transitionRoutine = null;
        StopHoly();
    }

    private void ResolveReferences()
    {
        if (hostCharacter == null)
        {
            SquadCharacterController controller = GetComponentInParent<SquadCharacterController>();
            hostCharacter = controller != null ? controller.transform : transform.parent;
        }

        if (hostCharacterController == null && hostCharacter != null)
        {
            hostCharacterController = hostCharacter.GetComponent<SquadCharacterController>();
            if (hostCharacterController == null)
            {
                hostCharacterController = GetComponentInParent<SquadCharacterController>();
            }
        }

        if (holyEffect == null && hostCharacter != null)
        {
            holyEffect = hostCharacter.GetComponentInChildren<CharacterEffect>(true);
        }

        if (hostAnimator == null && hostCharacter != null)
        {
            hostAnimator = hostCharacter.GetComponent<Animator>();
        }

        if (spiritVisualRoot == null)
        {
            Transform visual = transform.Find("Munin_Orbe");
            if (visual != null)
            {
                spiritVisualRoot = visual.gameObject;
            }
        }
    }

    private void StartMeltCameraPresentation()
    {
        if (meltCameraProfile == null || hostCharacter == null)
        {
            return;
        }

        LitCameraDirector director = LitCameraDirector.EnsureInstance();
        CameraProfilPlayer.GetOrCreate(director)?.Play(meltCameraProfile, hostCharacter);
    }

    private IEnumerator PlayMeltUiPresentation()
    {
        ResolveMeltUiReferences();
        if (muninUiEffectsCanvasGroup == null || lightFrostImage == null)
        {
            meltPresentationActive = false;
            meltPresentationRoutine = null;
            yield break;
        }

        EnsureLightFrostMaterial();
        CaptureMeltUiPresentationState();
        muninUiEffectsCanvasGroup.interactable = false;
        muninUiEffectsCanvasGroup.blocksRaycasts = false;
        muninUiEffectsCanvasGroup.alpha = 0f;
        SetLightFrostHdrIntensity(-10f);

        float elapsed = 0f;
        float presentationDuration = Mathf.Max(meltFrostFadeSeconds, meltHdrIntensitySeconds);
        while (elapsed < presentationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            muninUiEffectsCanvasGroup.alpha = DurationProgress(elapsed, meltFrostFadeSeconds);
            SetLightFrostHdrIntensity(Mathf.Lerp(-10f, 10f, DurationProgress(elapsed, meltHdrIntensitySeconds)));
            yield return null;
        }

        float holdDuration = Mathf.Max(0f, GetMeltProfileStartAndHoldDuration() - presentationDuration);
        elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        float returnDuration = GetMeltProfileReturnDuration();
        elapsed = 0f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = DurationProgress(elapsed, returnDuration);
            muninUiEffectsCanvasGroup.alpha = Mathf.Lerp(1f, meltInitialCanvasAlpha, t);
            SetLightFrostHdrIntensity(Mathf.Lerp(10f, meltInitialHdrIntensity, t));
            yield return null;
        }

        RestoreMeltUiPresentation();
        if (!meltStateObserved)
        {
            ReleaseMeltMovementLock();
        }
        meltPresentationActive = false;
        meltPresentationRoutine = null;
    }

    private void ResolveMeltUiReferences()
    {
        if (muninUiEffectsCanvasGroup == null || lightFrostImage == null)
        {
            GameObject uiEffects = GameObject.Find("MuninUIEffects");
            if (uiEffects != null)
            {
                muninUiEffectsCanvasGroup = uiEffects.GetComponent<CanvasGroup>();
                Transform frost = uiEffects.transform.Find("LightFrost");
                lightFrostImage = frost != null ? frost.GetComponent<Image>() : null;
            }
        }
    }

    private void EnsureLightFrostMaterial()
    {
        if (lightFrostMaterial != null || lightFrostImage == null || lightFrostImage.material == null)
        {
            return;
        }

        lightFrostMaterial = new Material(lightFrostImage.material)
        {
            name = $"{lightFrostImage.material.name} (Melt Local)"
        };
        lightFrostImage.material = lightFrostMaterial;
    }

    private void CaptureMeltUiPresentationState()
    {
        if (meltUiStateCaptured || muninUiEffectsCanvasGroup == null || lightFrostMaterial == null)
        {
            return;
        }

        meltInitialCanvasAlpha = muninUiEffectsCanvasGroup.alpha;
        meltInitialCanvasInteractable = muninUiEffectsCanvasGroup.interactable;
        meltInitialCanvasBlocksRaycasts = muninUiEffectsCanvasGroup.blocksRaycasts;
        meltInitialHdrColor = lightFrostMaterial.GetColor("_HDRColor");
        meltInitialHdrIntensity = lightFrostMaterial.GetFloat("_HDRIntensity");
        meltUiStateCaptured = true;
    }

    private void RestoreMeltUiPresentation()
    {
        if (!meltUiStateCaptured)
        {
            return;
        }

        if (muninUiEffectsCanvasGroup != null)
        {
            muninUiEffectsCanvasGroup.alpha = meltInitialCanvasAlpha;
            muninUiEffectsCanvasGroup.interactable = meltInitialCanvasInteractable;
            muninUiEffectsCanvasGroup.blocksRaycasts = meltInitialCanvasBlocksRaycasts;
        }

        if (lightFrostMaterial != null)
        {
            lightFrostMaterial.SetFloat("_HDRIntensity", meltInitialHdrIntensity);
            lightFrostMaterial.SetColor("_HDRColor", meltInitialHdrColor);
        }

        meltUiStateCaptured = false;
    }

    private float GetMeltProfileStartAndHoldDuration()
    {
        return meltCameraProfile != null
            ? Mathf.Max(0f, meltCameraProfile.startLerp) + Mathf.Max(0f, meltCameraProfile.duration)
            : Mathf.Max(meltFrostFadeSeconds, meltHdrIntensitySeconds);
    }

    private float GetMeltProfileReturnDuration()
    {
        return meltCameraProfile != null ? Mathf.Max(0f, meltCameraProfile.endLerp) : 0f;
    }

    private void AcquireMeltMovementLock()
    {
        if (meltMovementLockHeld)
        {
            return;
        }

        ResolveReferences();
        if (hostCharacterController == null)
        {
            return;
        }

        hostCharacterController.PushScriptedMovementSuppression();
        meltMovementLockHeld = true;
        meltStateObserved = false;
    }

    private void ReleaseMeltMovementLock()
    {
        if (!meltMovementLockHeld)
        {
            return;
        }

        hostCharacterController?.PopScriptedMovementSuppression();
        meltMovementLockHeld = false;
        meltStateObserved = false;
    }

    private void SetLightFrostHdrIntensity(float intensity)
    {
        if (lightFrostMaterial == null)
        {
            return;
        }

        // The graph exposes the EV value for authoring. HDR Color is updated to
        // the same 2^EV exposure so existing materials remain compatible while
        // the graph is reimported on every target platform.
        lightFrostMaterial.SetFloat("_HDRIntensity", intensity);
        float exposure = Mathf.Pow(2f, Mathf.Clamp(intensity, -10f, 10f));
        lightFrostMaterial.SetColor("_HDRColor", new Color(exposure, exposure, exposure, 1f));
    }

    private static float DurationProgress(float elapsed, float duration)
    {
        return duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
    }

    private void BindWeaponManifestations()
    {
        UnbindWeaponManifestations();
        if (hostCharacter == null)
        {
            return;
        }

        swords = hostCharacter.GetComponentsInChildren<PlayerSword>(true);
        for (int i = 0; i < swords.Length; i++)
        {
            if (swords[i] != null)
            {
                swords[i].ManifestationChanged += OnSwordManifestationChanged;
            }
        }

        bows = hostCharacter.GetComponentsInChildren<PlayerBow>(true);
        for (int i = 0; i < bows.Length; i++)
        {
            if (bows[i] != null)
            {
                bows[i].ManifestationChanged += OnBowManifestationChanged;
            }
        }
    }

    private void UnbindWeaponManifestations()
    {
        for (int i = 0; i < swords.Length; i++)
        {
            if (swords[i] != null)
            {
                swords[i].ManifestationChanged -= OnSwordManifestationChanged;
            }
        }

        for (int i = 0; i < bows.Length; i++)
        {
            if (bows[i] != null)
            {
                bows[i].ManifestationChanged -= OnBowManifestationChanged;
            }
        }

        swords = Array.Empty<PlayerSword>();
        bows = Array.Empty<PlayerBow>();
    }

    private void OnSwordManifestationChanged(PlayerSword _, bool __)
    {
        RefreshSpiritVisibility();
    }

    private void OnBowManifestationChanged(PlayerBow _, bool __)
    {
        RefreshSpiritVisibility();
    }

    private void PlayHoly()
    {
        if (holyEffect == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[SpiritBond] Holy start ignored: CharacterEffect is missing.", this);
#endif
            return;
        }

        CharacterEffectRuntimeRepair.EnsureReady(holyEffect);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SpiritBond] Frame {Time.frameCount}: CharacterEffect.StartEffect() on '{holyEffect.name}'.", holyEffect);
#endif
        holyEffect.StartEffect();
    }

    private void StopHoly()
    {
        if (holyEffect == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[SpiritBond] Holy stop ignored: CharacterEffect is missing.", this);
#endif
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SpiritBond] Frame {Time.frameCount}: CharacterEffect.StopEffect() on '{holyEffect.name}'.", holyEffect);
#endif
        holyEffect.StopEffect();
    }

    private bool IsMeltPlaying()
    {
        if (hostAnimator == null)
        {
            return false;
        }

        AnimatorStateInfo current = hostAnimator.GetCurrentAnimatorStateInfo(0);
        if (current.shortNameHash == MeltStateHash)
        {
            return true;
        }

        return hostAnimator.IsInTransition(0) &&
               hostAnimator.GetNextAnimatorStateInfo(0).shortNameHash == MeltStateHash;
    }

    private void SetSpiritVisible(bool visible)
    {
        if (spiritVisualRoot != null && spiritVisualRoot.activeSelf != visible)
        {
            spiritVisualRoot.SetActive(visible);
        }
    }

    private void RefreshSpiritVisibility()
    {
        SetSpiritVisible(!fused && !cinematicFusion && !HasActiveWeaponManifestation());
    }

    private bool HasActiveWeaponManifestation()
    {
        if (externalManifestations.Count > 0)
        {
            return true;
        }

        for (int i = 0; i < swords.Length; i++)
        {
            if (swords[i] != null && swords[i].IsManifested)
            {
                return true;
            }
        }

        for (int i = 0; i < bows.Length; i++)
        {
            if (bows[i] != null && bows[i].IsManifested)
            {
                return true;
            }
        }

        return false;
    }

    private void NotifyFusionStateChanged()
    {
        FusionStateChanged?.Invoke(this, fused);
    }
}
