using System.Collections;
using UnityEngine;

/// <summary>
/// Per-ActorRoot clock composed with the global Unity clock by TimeManager.
/// Actor systems consume this clock for movement and action timing.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatTimeDomain : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private float localScale = 1f;
    private float localTime;
    private float animatorBaseSpeed = 1f;
    private bool animatorSpeedCaptured;
    private readonly System.Collections.Generic.HashSet<object> intrinsicPauses = new System.Collections.Generic.HashSet<object>();
    public void SetIntrinsicPause(object owner, bool paused)
    {
        if (owner == null) return;
        if (paused) intrinsicPauses.Add(owner); else intrinsicPauses.Remove(owner);
        ApplyAnimatorSpeed();
    }

    public float Scale => intrinsicPauses.Count > 0 ? 0 : localScale;
    public float DeltaTime => Time.deltaTime * Scale;
    public float FixedDeltaTime => Time.fixedDeltaTime * Scale;
    public float LocalTime => localTime;
    public float BaseAnimatorSpeed => animatorBaseSpeed;

    private void Awake()
    {
        ResolveAnimator();
        TimeManager.EnsureInstance()?.RegisterDomain(this);
    }

    private void OnEnable()
    {
        TimeManager.EnsureInstance()?.RegisterDomain(this);
    }

    private void Update()
    {
        localTime += DeltaTime;
        ApplyAnimatorSpeed();
    }

    private void OnDisable()
    {
        if (animator != null && animatorSpeedCaptured) animator.speed = animatorBaseSpeed;
        TimeManager.Instance?.UnregisterDomain(this);
    }

    public IEnumerator WaitForLocalSeconds(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += DeltaTime;
            yield return null;
        }
    }

    internal void ApplyManagerScale(float scale)
    {
        localScale = Mathf.Clamp01(scale);
        ApplyAnimatorSpeed();
    }

    private void ResolveAnimator()
    {
        if (animator == null)
        {
            CharacterAnimationController contract = GetComponent<CharacterAnimationController>();
            animator = contract != null ? contract.Animator : GetComponentInChildren<Animator>(true);
        }

        if (animator != null && !animatorSpeedCaptured)
        {
            animatorBaseSpeed = animator.speed;
            animatorSpeedCaptured = true;
        }
    }

    private void ApplyAnimatorSpeed()
    {
        ResolveAnimator();
        if (animator != null && animatorSpeedCaptured)
        {
            animator.speed = animatorBaseSpeed * Scale;
        }
    }
}
