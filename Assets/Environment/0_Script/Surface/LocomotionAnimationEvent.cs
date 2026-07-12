using UnityEngine;

[DisallowMultipleComponent]
public class LocomotionAnimationEvent : MonoBehaviour
{
    private enum FootSide
    {
        Any,
        Left,
        Right
    }

    [Header("Surface")]
    [SerializeField] private SurfaceDefinition defaultSurface;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private Transform raycastOrigin;
    [SerializeField, Min(0f)] private float raycastHeight = 0.4f;
    [SerializeField, Min(0.05f)] private float raycastDistance = 1.6f;

    [Header("Feet")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightFoot;
    [SerializeField] private bool autoResolveHumanoidFeet = true;
    [SerializeField, Min(0f)] private float footRaycastHeight = 0.35f;
    [SerializeField, Min(0.05f)] private float footRaycastDistance = 0.8f;

    [Header("Audio")]
    [SerializeField, Min(0f)] private float minimumFootstepInterval = 0.05f;
    [SerializeField, Range(0f, 1f)] private float minimumAnimationEventWeight = 0.5f;

    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private AudioClip lastFootstepClip;
    private float nextFootstepTime;

    private void Awake()
    {
        ResolveFootReferences();
    }

    private void OnValidate()
    {
        raycastDistance = Mathf.Max(0.05f, raycastDistance);
        footRaycastDistance = Mathf.Max(0.05f, footRaycastDistance);
        minimumFootstepInterval = Mathf.Max(0f, minimumFootstepInterval);
        minimumAnimationEventWeight = Mathf.Clamp01(minimumAnimationEventWeight);
    }

    public void PlayFootstep()
    {
        PlayFootstep(FootSide.Any);
    }

    public void PlayFootstep(AnimationEvent animationEvent)
    {
        if (ShouldIgnoreAnimationEvent(animationEvent))
        {
            return;
        }

        PlayFootstep(ResolveFootSide(animationEvent));
    }

    public void PlayFootstepLeft()
    {
        PlayFootstep(FootSide.Left);
    }

    public void PlayFootstepRight()
    {
        PlayFootstep(FootSide.Right);
    }

    public void Footstep()
    {
        PlayFootstep();
    }

    public void Footstep(int footIndex)
    {
        PlayFootstep(footIndex == 0 ? FootSide.Left : FootSide.Right);
    }

    public void Footstep(string footName)
    {
        PlayFootstep(ParseFootSide(footName));
    }

    public void OnFootstep(AnimationEvent animationEvent)
    {
        PlayFootstep(animationEvent);
    }

    public void FootstepLeft()
    {
        PlayFootstep(FootSide.Left);
    }

    public void FootstepRight()
    {
        PlayFootstep(FootSide.Right);
    }

    public void LeftFootstep()
    {
        PlayFootstep(FootSide.Left);
    }

    public void RightFootstep()
    {
        PlayFootstep(FootSide.Right);
    }

    private void PlayFootstep(FootSide side)
    {
        if (Time.time < nextFootstepTime)
        {
            return;
        }

        ResolveFootReferences();
        if (!TrySampleFootGround(side, out RaycastHit groundHit) &&
            !TrySampleCharacterGround(out groundHit))
        {
            return;
        }

        SurfaceDefinition surface = SurfaceResolver.Resolve(groundHit, defaultSurface);
        if (surface == null || !surface.HasFootstepClips)
        {
            return;
        }

        AudioClip clip = surface.GetRandomFootstepClip(lastFootstepClip);
        if (clip == null)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        if (manager == null)
        {
            return;
        }

        lastFootstepClip = clip;
        nextFootstepTime = Time.time + minimumFootstepInterval;
        manager.PlayClip(clip, groundHit.point, surface.FootstepVolume, surface.GetRandomFootstepPitch());
    }

    private bool TrySampleFootGround(FootSide side, out RaycastHit hit)
    {
        if (side == FootSide.Any)
        {
            bool hasLeft = TrySampleFootGround(leftFoot, out RaycastHit leftHit, out float leftDistance);
            bool hasRight = TrySampleFootGround(rightFoot, out RaycastHit rightHit, out float rightDistance);
            if (hasLeft && (!hasRight || leftDistance <= rightDistance))
            {
                hit = leftHit;
                return true;
            }

            if (hasRight)
            {
                hit = rightHit;
                return true;
            }

            hit = default;
            return false;
        }

        Transform foot = ResolveFootTransform(side);
        return TrySampleFootGround(foot, out hit, out _);
    }

    private bool TrySampleFootGround(Transform foot, out RaycastHit hit, out float footGroundDistance)
    {
        hit = default;
        footGroundDistance = float.PositiveInfinity;
        if (foot == null)
        {
            return false;
        }

        Vector3 up = transform.up;
        float height = Mathf.Max(0f, footRaycastHeight);
        Vector3 origin = foot.position + up * height;
        float distance = height + Mathf.Max(0.05f, footRaycastDistance);
        if (!TryRaycastGround(origin, -up, out hit, distance))
        {
            return false;
        }

        footGroundDistance = hit.distance - height;
        return true;
    }

    private bool TrySampleCharacterGround(out RaycastHit hit)
    {
        Vector3 up = transform.up;
        Vector3 origin = raycastOrigin != null ? raycastOrigin.position : transform.position + up * raycastHeight;
        return TryRaycastGround(origin, -up, out hit, Mathf.Max(0.05f, raycastDistance));
    }

    private bool TryRaycastGround(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance)
    {
        hit = default;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            groundHits,
            Mathf.Max(0.01f, distance),
            groundMask,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.PositiveInfinity;
        bool foundGround = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = groundHits[i];
            Collider candidateCollider = candidate.collider;
            if (candidateCollider == null || candidateCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (candidate.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = candidate.distance;
            hit = candidate;
            foundGround = true;
        }

        return foundGround;
    }

    private FootSide ResolveFootSide(AnimationEvent animationEvent)
    {
        if (animationEvent == null)
        {
            return FootSide.Any;
        }

        if (!string.IsNullOrWhiteSpace(animationEvent.stringParameter))
        {
            return ParseFootSide(animationEvent.stringParameter);
        }

        return animationEvent.intParameter == 0 ? FootSide.Left : FootSide.Right;
    }

    private bool ShouldIgnoreAnimationEvent(AnimationEvent animationEvent)
    {
        return animationEvent != null && animationEvent.animatorClipInfo.weight < minimumAnimationEventWeight;
    }

    private void ResolveFootReferences()
    {
        if (!autoResolveHumanoidFeet)
        {
            return;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null && animator.isHuman)
        {
            if (leftFoot == null)
            {
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            }

            if (rightFoot == null)
            {
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
        }

        if (leftFoot == null)
        {
            leftFoot = FindChildByName(transform, "foot_l", "leftfoot", "left_foot");
        }

        if (rightFoot == null)
        {
            rightFoot = FindChildByName(transform, "foot_r", "rightfoot", "right_foot");
        }
    }

    private Transform ResolveFootTransform(FootSide side)
    {
        switch (side)
        {
            case FootSide.Left:
                return leftFoot != null ? leftFoot : rightFoot;
            case FootSide.Right:
                return rightFoot != null ? rightFoot : leftFoot;
            default:
                return leftFoot != null ? leftFoot : rightFoot;
        }
    }

    private static Transform FindChildByName(Transform root, params string[] names)
    {
        if (root == null || names == null || names.Length == 0)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null)
            {
                continue;
            }

            string childName = NormalizeName(child.name);
            for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
            {
                if (childName == NormalizeName(names[nameIndex]))
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static FootSide ParseFootSide(string footName)
    {
        string normalized = NormalizeName(footName);
        if (normalized.Contains("right") || normalized.EndsWith("r"))
        {
            return FootSide.Right;
        }

        if (normalized.Contains("left") || normalized.EndsWith("l"))
        {
            return FootSide.Left;
        }

        return FootSide.Any;
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    }
}
