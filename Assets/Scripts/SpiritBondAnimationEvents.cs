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
