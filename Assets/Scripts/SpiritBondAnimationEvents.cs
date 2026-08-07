using UnityEngine;

/// <summary>
/// AnimationEvent entry points placed on the incarnation's Animator object.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpiritBondAnimationEvents : MonoBehaviour
{
    [SerializeField] private SpiritBondController bond;
    [SerializeField, Tooltip("Prefab spawned by the InstantiateAtSpine AnimationEvent.")]
    private GameObject spineAnimationPrefab;
    [SerializeField, Tooltip("Optional explicit spine bone. Empty resolves from the humanoid rig, then by bone name.")]
    private Transform spineBone;

    private void Awake()
    {
        ResolveBond();
    }

    /// <summary>AnimationEvent for the Holy burst in Melt or Rupture.</summary>
    public void TriggerHolyEffect()
    {
        ResolveBond();
        bond?.TriggerHolyEffectFromAnimationEvent();
    }

    /// <summary>
    /// Compatibility entry point for the existing Melt AnimationEvent. The
    /// actual CharacterEffect lives on CC_Base_Body, while events are received
    /// by the Animator root.
    /// </summary>
    public void PlayEffect_CharacterEffect()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SpiritBond] Frame {Time.frameCount}: PlayEffect_CharacterEffect received.", this);
#endif
        TriggerHolyEffect();
    }

    /// <summary>
    /// AnimationEvent counterpart of PlayEffect_CharacterEffect: stops the
    /// active CharacterEffect cleanly, without disabling its GameObject.
    /// </summary>
    public void StopEffect_CharacterEffect()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SpiritBond] Frame {Time.frameCount}: StopEffect_CharacterEffect received.", this);
#endif
        ResolveBond();
        bond?.StopHolyEffectFromAnimationEvent();
    }

    /// <summary>
    /// Legacy AnimationEvent entry point. Use StopEffect_CharacterEffect for
    /// new clips; this forwarding method keeps existing clips functional.
    /// </summary>
    public void StopEffect()
    {
        StopEffect_CharacterEffect();
    }

    /// <summary>AnimationEvent at the moment Melt becomes active.</summary>
    public void ConfirmMeltFusion()
    {
        ResolveBond();
        bond?.ConfirmMeltFusionFromAnimationEvent();
    }

    /// <summary>AnimationEvent at the moment Rupture releases the spirit.</summary>
    public void ConfirmRuptureDefusion()
    {
        ResolveBond();
        bond?.ConfirmRuptureDefusionFromAnimationEvent();
    }

    /// <summary>
    /// AnimationEvent: instantiates the configured presentation prefab as a
    /// child of the character spine, so it follows the current animation.
    /// </summary>
    public void InstantiateAtSpine()
    {
        if (spineAnimationPrefab == null)
        {
            Debug.LogWarning("[SpiritBondAnimationEvents] Aucun prefab n'est configure pour InstantiateAtSpine.", this);
            return;
        }

        Transform targetSpine = ResolveSpineBone();
        if (targetSpine == null)
        {
            Debug.LogWarning("[SpiritBondAnimationEvents] Os Spine introuvable pour InstantiateAtSpine.", this);
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
