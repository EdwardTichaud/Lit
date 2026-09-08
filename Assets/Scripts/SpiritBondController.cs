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
    private static readonly int RuptureStateHash = Animator.StringToHash("Rupture");

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
    private bool meltPresentationActive;
    private bool meltMovementLockHeld;
    private bool meltStateObserved;

    public bool IsFused => fused;
    /// <summary>True while the player is in the persistent manual Melted state.</summary>
    public bool IsMelted => fused && !cinematicFusion;
    public bool IsCinematicFusion => cinematicFusion;
    public event Action<SpiritBondController, bool> FusionStateChanged;

    /// <summary>
    /// Allows the Animator event relay to own the authored Melt camera profile.
    /// The value is read only when a new Melt starts.
    /// </summary>
    public void SetMeltCameraProfile(CameraProfilSO profile)
    {
        if (profile != null)
        {
            meltCameraProfile = profile;
        }
    }

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
        if (meltMovementLockHeld)
        {
            if (IsMeltPlaying() || IsRupturePlaying())
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
        RefreshSpiritVisibility();
        holyEffect?.StopEffect();
    }

    public bool ToggleManualFusion()
    {
        return !fused ? MeltTheIce() : Unmelted();
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

        return Unmelted();
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

    /// <summary>
    /// Leaves the persistent Melted state. The Rupture Animation Event stops
    /// CharacterEffect and confirms the state change at its authored frame.
    /// </summary>
    public bool Unmelted()
    {
        if (cinematicFusion || !fused || IsRupturePlaying())
        {
            return false;
        }

        ResolveReferences();
        if (hostAnimator == null)
        {
            return false;
        }

        AcquireMeltMovementLock();
        hostAnimator.ResetTrigger("Melt");
        hostAnimator.SetTrigger("Rupture");
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

        // Melt_df5b33c8 already contains this event at the authored moment
        // where the transformation becomes real. Keeping this fallback makes
        // the state robust even when a legacy clip has no explicit Confirm
        // event, while ConfirmMeltFusion remains safely idempotent.
        if (!cinematicFusion && IsMeltPlaying())
        {
            EnterMeltedState();
        }
    }

    /// <summary>AnimationEvent: stops Holy at the precise authored frame.</summary>
    public void StopHolyEffectFromAnimationEvent()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SpiritBond] Frame {Time.frameCount}: Holy stop requested by AnimationEvent.", this);
#endif
        StopHoly();

        // Legacy Rupture clips use StopEffect_CharacterEffect as their only
        // final event. Treat it as the authored exit point from Melted.
        if (!cinematicFusion && IsRupturePlaying())
        {
            ExitMeltedState();
        }
    }

    /// <summary>AnimationEvent: completes the Melt animation's fusion.</summary>
    public void ConfirmMeltFusionFromAnimationEvent()
    {
        if (cinematicFusion)
        {
            return;
        }

        EnterMeltedState();
    }

    /// <summary>AnimationEvent: completes the Rupture animation's defusion.</summary>
    public void ConfirmRuptureDefusionFromAnimationEvent()
    {
        if (cinematicFusion)
        {
            return;
        }

        ExitMeltedState();
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

    private void EnterMeltedState()
    {
        if (fused)
        {
            return;
        }

        CancelTransition(stopHoly: false);
        fused = true;
        RefreshSpiritVisibility();
        NotifyFusionStateChanged();
    }

    private void ExitMeltedState()
    {
        if (!fused)
        {
            return;
        }

        CancelTransition();
        fused = false;
        RefreshSpiritVisibility();
        NotifyFusionStateChanged();
    }

    private void CancelTransition(bool stopHoly = true)
    {
        if (transitionRoutine == null)
        {
            return;
        }

        StopCoroutine(transitionRoutine);
        transitionRoutine = null;
        if (stopHoly)
        {
            StopHoly();
        }
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
        float hdrIntensityStart = GetMeltProfileHdrIntensityStart();
        float hdrIntensityEnd = GetMeltProfileHdrIntensityEnd();
        float hdrIntensityLerp = GetMeltProfileHdrIntensityLerp();
        SetLightFrostHdrIntensity(hdrIntensityStart);

        float elapsed = 0f;
        float presentationDuration = Mathf.Max(meltFrostFadeSeconds, hdrIntensityLerp);
        while (elapsed < presentationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            muninUiEffectsCanvasGroup.alpha = DurationProgress(elapsed, meltFrostFadeSeconds);
            SetLightFrostHdrIntensity(Mathf.Lerp(hdrIntensityStart, hdrIntensityEnd, DurationProgress(elapsed, hdrIntensityLerp)));
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
            SetLightFrostHdrIntensity(Mathf.Lerp(hdrIntensityEnd, meltInitialHdrIntensity, t));
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
        meltInitialHdrColor = lightFrostMaterial.HasProperty("_HDRColor")
            ? lightFrostMaterial.GetColor("_HDRColor")
            : Color.white;
        meltInitialHdrIntensity = lightFrostMaterial.HasProperty("_HDRIntensity")
            ? lightFrostMaterial.GetFloat("_HDRIntensity")
            : HdrIntensityFromColor(meltInitialHdrColor);
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
            if (lightFrostMaterial.HasProperty("_HDRIntensity")) lightFrostMaterial.SetFloat("_HDRIntensity", meltInitialHdrIntensity);
            if (lightFrostMaterial.HasProperty("_HDRColor")) lightFrostMaterial.SetColor("_HDRColor", meltInitialHdrColor);
        }

        meltUiStateCaptured = false;
    }

    private float GetMeltProfileStartAndHoldDuration()
    {
        return meltCameraProfile != null
            ? Mathf.Max(0f, meltCameraProfile.startLerp) + Mathf.Max(0f, meltCameraProfile.duration)
            : meltFrostFadeSeconds;
    }

    private float GetMeltProfileReturnDuration()
    {
        return meltCameraProfile != null ? Mathf.Max(0f, meltCameraProfile.endLerp) : 0f;
    }

    private float GetMeltProfileHdrIntensityStart()
    {
        return meltCameraProfile != null ? meltCameraProfile.muninUiHdrIntensityStart : -10f;
    }

    private float GetMeltProfileHdrIntensityEnd()
    {
        return meltCameraProfile != null ? meltCameraProfile.muninUiHdrIntensityEnd : 10f;
    }

    private float GetMeltProfileHdrIntensityLerp()
    {
        return meltCameraProfile != null ? Mathf.Max(0f, meltCameraProfile.muninUiHdrIntensityLerp) : 0f;
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
        if (lightFrostMaterial.HasProperty("_HDRIntensity")) lightFrostMaterial.SetFloat("_HDRIntensity", intensity);
        float exposure = Mathf.Pow(2f, Mathf.Clamp(intensity, -10f, 10f));
        if (lightFrostMaterial.HasProperty("_HDRColor")) lightFrostMaterial.SetColor("_HDRColor", new Color(exposure, exposure, exposure, 1f));
    }

    private static float HdrIntensityFromColor(Color color)
    {
        float brightness = Mathf.Max(color.r, color.g, color.b, 0.0009765625f);
        return Mathf.Clamp(Mathf.Log(brightness, 2f), -10f, 10f);
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

    private bool IsRupturePlaying()
    {
        if (hostAnimator == null)
        {
            return false;
        }

        AnimatorStateInfo current = hostAnimator.GetCurrentAnimatorStateInfo(0);
        return current.shortNameHash == RuptureStateHash ||
               (hostAnimator.IsInTransition(0) &&
                hostAnimator.GetNextAnimatorStateInfo(0).shortNameHash == RuptureStateHash);
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
