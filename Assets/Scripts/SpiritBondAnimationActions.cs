using UnityEngine;

/// <summary>
/// SpiritBond actions invoked by the shared AnimationEvents receiver.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AnimationEvents))]
public sealed class SpiritBondAnimationActions : MonoBehaviour
{
    private static readonly int RuptureStateHash = Animator.StringToHash("Rupture");

    [SerializeField] private SpiritBondController bond;
    [Header("Melt")]
    [Tooltip("Profil de caméra joué pendant Melt. S'il est renseigné ici, il est prioritaire sur celui du SpiritBondController.")]
    public CameraProfilSO meltCameraProfile;
    [SerializeField, Tooltip("Prefab spawned by the HandleInstantiateAtSpine AnimationEvent.")]
    private GameObject spineAnimationPrefab;
    [SerializeField, Tooltip("Optional explicit spine bone. Empty resolves from the humanoid rig, then by bone name.")]
    private Transform spineBone;

    private void Awake()
    {
        ResolveBond();
        ApplyMeltCameraProfile();
    }

    private void OnValidate()
    {
        ResolveBond();
        ApplyMeltCameraProfile();
    }

    /// <summary>AnimationEvent for the Holy burst in Melt or Rupture.</summary>
    public void HandleTriggerHolyEffect()
    {
        ResolveBond();
        bond?.TriggerHolyEffectFromAnimationEvent();
    }

    /// <summary>
    /// Compatibility entry point for existing clips. The actual CharacterEffect
    /// lives on CC_Base_Body, while events are received by the Animator root.
    /// A legacy Play event on Rupture is interpreted as the intended stop.
    /// </summary>
    public void HandlePlayEffect_CharacterEffect()
    {
        ResolveBond();
#if UNITY_EDITOR || DEBUG
        Debug.Log($"[SpiritBond] Frame {Time.frameCount}: HandlePlayEffect_CharacterEffect received.", this);
#endif
        if (bond != null && !bond.IsCinematicFusion && IsRupturePlaying())
        {
#if UNITY_EDITOR || DEBUG
            Debug.Log($"[SpiritBond] Frame {Time.frameCount}: legacy PlayEffect on Rupture routed to Holy stop.", this);
#endif
            bond.StopHolyEffectFromAnimationEvent();
            return;
        }

        bond?.TriggerHolyEffectFromAnimationEvent();
    }

    /// <summary>
    /// AnimationEvent counterpart of HandlePlayEffect_CharacterEffect: stops the
    /// active CharacterEffect cleanly, without disabling its GameObject.
    /// </summary>
    public void HandleStopEffect_CharacterEffect()
    {
#if UNITY_EDITOR || DEBUG
        Debug.Log($"[SpiritBond] Frame {Time.frameCount}: HandleStopEffect_CharacterEffect received.", this);
#endif
        ResolveBond();
        bond?.StopHolyEffectFromAnimationEvent();
    }

    /// <summary>
    /// Legacy AnimationEvent entry point. Use HandleStopEffect_CharacterEffect for
    /// new clips; this forwarding method keeps existing clips functional.
    /// </summary>
    public void HandleStopEffect()
    {
        HandleStopEffect_CharacterEffect();
    }

    /// <summary>AnimationEvent at the moment Melt becomes active.</summary>
    public void HandleConfirmMeltFusion()
    {
        ResolveBond();
        bond?.ConfirmMeltFusionFromAnimationEvent();
    }

    /// <summary>AnimationEvent at the moment Rupture releases the spirit.</summary>
    public void HandleConfirmRuptureDefusion()
    {
        ResolveBond();
        bond?.ConfirmRuptureDefusionFromAnimationEvent();
    }

    /// <summary>
    /// AnimationEvent: instantiates the configured presentation prefab as a
    /// child of the character spine, so it follows the current animation.
    /// </summary>
    public void HandleInstantiateAtSpine()
    {
        if (spineAnimationPrefab == null)
        {
            Debug.LogWarning("[SpiritBondAnimationActions] Aucun prefab n'est configure pour HandleInstantiateAtSpine.", this);
            return;
        }

        Transform targetSpine = ResolveSpineBone();
        if (targetSpine == null)
        {
            Debug.LogWarning("[SpiritBondAnimationActions] Os Spine introuvable pour HandleInstantiateAtSpine.", this);
            return;
        }

        GameObject instance = Instantiate(spineAnimationPrefab, targetSpine, false);
        instance.name = spineAnimationPrefab.name + " (Spine Animation Event)";
    }

    private void ResolveBond()
    {
        if (bond == null)
        {
            bond = SpiritBondController.FindForCharacter(gameObject);
        }
    }

    private void ApplyMeltCameraProfile()
    {
        if (meltCameraProfile != null)
        {
            bond?.SetMeltCameraProfile(meltCameraProfile);
        }
    }

    private bool IsRupturePlaying()
    {
        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            return false;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.shortNameHash == RuptureStateHash)
        {
            return true;
        }

        return animator.IsInTransition(0) &&
               animator.GetNextAnimatorStateInfo(0).shortNameHash == RuptureStateHash;
    }

    private Transform ResolveSpineBone()
    {
        if (spineBone != null)
        {
            return spineBone;
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null && animator.isHuman)
        {
            spineBone = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (spineBone != null)
            {
                return spineBone;
            }
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (string.Equals(transforms[i].name, "spine_02", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(transforms[i].name, "spine", System.StringComparison.OrdinalIgnoreCase))
            {
                spineBone = transforms[i];
                return spineBone;
            }
        }

        return null;
    }
}
