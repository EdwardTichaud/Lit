using System;
using System.Collections;
using System.Collections.Generic;
using INab.VFXAssets;
using UnityEngine;

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

    private Coroutine transitionRoutine;
    private Animator hostAnimator;
    private PlayerSword[] swords = Array.Empty<PlayerSword>();
    private PlayerBow[] bows = Array.Empty<PlayerBow>();
    private readonly HashSet<SpiritWeaponManifestation> externalManifestations = new HashSet<SpiritWeaponManifestation>();
    private bool fused;
    private bool cinematicFusion;
    private bool holyEffectAwaitingMeltExit;

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
    }

    private void OnDisable()
    {
        UnbindWeaponManifestations();
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

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

        ResolveReferences();
        if (hostAnimator == null)
        {
            return false;
        }

        string trigger = fused ? "Rupture" : "Melt";
        string oppositeTrigger = fused ? "Melt" : "Rupture";
        hostAnimator.ResetTrigger(oppositeTrigger);
        hostAnimator.SetTrigger(trigger);
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
