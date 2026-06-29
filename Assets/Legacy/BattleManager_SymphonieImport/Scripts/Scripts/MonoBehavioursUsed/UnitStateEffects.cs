using UnityEngine;

[RequireComponent(typeof(Animator))]
public class UnitStateEffects : MonoBehaviour
{
    [Header("Effets visuels")]
    public GameObject enterEffectPrefab;
    public GameObject exitEffectPrefab;
    public Vector3 effectOffset = new(0f, 2f, 0f);

    [Header("Animations")]
    public AnimationClip enterAnimation;
    public AnimationClip exitAnimation;

    [Header("Effets sonores")]
    public AudioClipSO enterClip;
    public AudioClipSO exitClip;

    [Header("Camera")]
    public OrbitAroundTriggerSO cameraPath;

    protected Animator animator;
    private GameObject currentEffect;

    protected virtual void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void EnterState()
    {
        if (enterClip != null)
            AudioManager.Instance?.PlaySfx(enterClip);
        if (enterAnimation != null)
            animator?.Play(enterAnimation.name);
        cameraPath?.StartOrbit();
        if (enterEffectPrefab != null)
            currentEffect = Instantiate(enterEffectPrefab, transform.position + effectOffset, Quaternion.identity, transform);
    }

    public void ExitState()
    {
        if (exitClip != null)
            AudioManager.Instance?.PlaySfx(exitClip);
        if (exitAnimation != null)
            animator?.Play(exitAnimation.name);
        cameraPath?.StopOrbit();
        if (currentEffect != null)
        {
            Destroy(currentEffect);
            currentEffect = null;
        }
        if (exitEffectPrefab != null)
            Instantiate(exitEffectPrefab, transform.position + effectOffset, Quaternion.identity);
    }
}
