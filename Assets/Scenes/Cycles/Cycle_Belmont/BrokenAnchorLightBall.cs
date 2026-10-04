using UnityEngine;
using UnityEngine.VFX;
using INab.VFXAssets;

/// <summary>
/// Projectile prefab used exclusively by L'Ancre brisee. Its simulation is
/// host-authoritative: client instances are visual replicas with collisions
/// disabled, while the host decides whether Lucian is hurt or a torch is lit.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
[DisallowMultipleComponent]
public sealed class BrokenAnchorLightBall : MonoBehaviour
{
    [SerializeField] private Rigidbody body;
    [SerializeField] private Collider hitCollider;
    [SerializeField, Tooltip("Effet CharacterEffect joue a chaque apparition de la boule.")]
    private CharacterEffect characterEffect;

    [Header("Fixed trajectory")]
    [SerializeField, Min(0f), Tooltip("Hauteur de montee verticale avant la suspension.")]
    private float ascentHeight = 3f;
    [Min(0.01f), Tooltip("Duree de la montee verticale.")]
    public float riseSeconds = 2f;
    [Min(0f), Tooltip("Duree de suspension en l'air.")]
    public float hoverSeconds = 2f;
    [Min(0.01f), Tooltip("Duree de la charge vers la position verrouillee du joueur.")]
    public float dashSeconds = 1f;

    private Vector3 launchPosition;
    private Vector3 impactPosition;
    private Vector3 hoverPosition;
    private float flightElapsed;
    private int damage;
    private bool authoritative;
    private bool resolved;
    private bool collisionArmed;
    private bool dashDirectionTriggered;
    private BrokenAnchorBoss owner;
    private Transform ownerRoot;
    private Collider[] ownerColliders = System.Array.Empty<Collider>();
    private readonly RaycastHit[] sweepHits = new RaycastHit[16];
    private CharacterEffect[] childCharacterEffects = System.Array.Empty<CharacterEffect>();
    private Coroutine deferredEffectRestart;

    private void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        if (hitCollider == null) hitCollider = GetComponent<Collider>();
        if (characterEffect == null) characterEffect = GetComponentInChildren<CharacterEffect>(true);
        childCharacterEffects = GetComponentsInChildren<CharacterEffect>(true);
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
        if (hitCollider != null) hitCollider.isTrigger = true;
    }

    public void Launch(Vector3 destination, float _, float __, int playerDamage, bool isAuthoritative, BrokenAnchorBoss boss, Transform sourceRoot)
    {
        launchPosition = transform.position;
        impactPosition = destination;
        hoverPosition = launchPosition + Vector3.up * ascentHeight;
        flightElapsed = 0f;
        collisionArmed = false;
        dashDirectionTriggered = false;
        damage = Mathf.Max(0, playerDamage);
        authoritative = isAuthoritative;
        owner = boss;
        ownerRoot = sourceRoot;
        ownerColliders = ownerRoot != null ? ownerRoot.GetComponentsInChildren<Collider>(true) : System.Array.Empty<Collider>();
        if (hitCollider != null)
        {
            foreach (Collider collider in ownerColliders)
                if (collider != null) Physics.IgnoreCollision(hitCollider, collider, true);
        }
        if (hitCollider != null) hitCollider.enabled = false;
        PlaySpawnCharacterEffects();
        Trace("tir cree | origine=" + launchPosition.ToString("F2") + " destination=" + impactPosition.ToString("F2"));
    }

    private void Update()
    {
        if (owner != null && owner.Enemy != null && owner.Enemy.IsFlameDormant) return;
        Vector3 previousPosition = transform.position;
        flightElapsed += Time.unscaledDeltaTime;
        Vector3 nextPosition;
        float riseDuration = Mathf.Max(.01f, riseSeconds);
        float hoverDuration = Mathf.Max(0f, hoverSeconds);
        float dashDuration = Mathf.Max(.01f, dashSeconds);
        if (flightElapsed < riseDuration)
        {
            nextPosition = Vector3.Lerp(launchPosition, hoverPosition, flightElapsed / riseDuration);
        }
        else if (flightElapsed < riseDuration + hoverDuration)
        {
            nextPosition = hoverPosition;
        }
        else
        {
            if (!collisionArmed)
            {
                collisionArmed = true;
                if (hitCollider != null) hitCollider.enabled = authoritative;
            }

            // La direction de l'attaque n'est revelee qu'a la sortie du hover.
            // Pendant la montee et la suspension, la boule ne "regarde" donc pas
            // deja le joueur.
            if (!dashDirectionTriggered)
            {
                dashDirectionTriggered = true;
                Vector3 dashDirection = impactPosition - hoverPosition;
                if (dashDirection.sqrMagnitude > .0001f)
                    transform.rotation = Quaternion.LookRotation(dashDirection.normalized, Vector3.up);
                Trace("direction de charge declenchee | destination=" + impactPosition.ToString("F2"));
            }
            float dashProgress = Mathf.Clamp01((flightElapsed - riseDuration - hoverDuration) / dashDuration);
            nextPosition = Vector3.Lerp(hoverPosition, impactPosition, dashProgress);
            if (authoritative && TrySweepImpact(previousPosition, nextPosition)) return;
            if (dashProgress >= 1f)
            {
                Trace("expiration sans impact");
                Destroy(gameObject);
                return;
            }
        }
        transform.position = nextPosition;
        Vector3 direction = transform.position - previousPosition;
        if (direction.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

    }

    private void OnTriggerEnter(Collider other)
    {
        if (!authoritative || !collisionArmed || resolved || other == null) return;
        HandleImpact(other);
    }

    private bool TrySweepImpact(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= .0001f) return false;
        float radius = hitCollider != null ? Mathf.Max(.04f, hitCollider.bounds.extents.magnitude * .55f) : .12f;
        int count = Physics.SphereCastNonAlloc(from, radius, delta / distance, sweepHits, distance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(sweepHits, 0, count, RaycastHitDistanceComparer.Instance);
        for (int index = 0; index < count; index++)
        {
            Collider collider = sweepHits[index].collider;
            if (collider == null || collider == hitCollider || IsOwnerCollider(collider)) continue;
            HandleImpact(collider);
            return resolved;
        }
        return false;
    }

    private void HandleImpact(Collider other)
    {
        if (owner != null && owner.Enemy != null && owner.Enemy.IsFlameDormant) return;
        if (IsOwnerCollider(other))
        {
            Trace("collision ignoree avec l'Ancre");
            return;
        }

        BrokenAnchorTorchReceiverStandalone torch = other.GetComponentInParent<BrokenAnchorTorchReceiverStandalone>();
        if (torch != null)
        {
            resolved = true;
            bool lit = torch.TryLight();
            Trace(lit ? "impact torche" : "impact torche deja allumee");
            // BrokenAnchorBoss écoute directement l'état de la torche. Cela couvre aussi
            // les restaurations de sauvegarde et évite qu'un projectile dupliqué compte deux fois.
            Destroy(gameObject);
            return;
        }

        SquadCharacterController player = other.GetComponentInParent<SquadCharacterController>();
        if (player != null)
        {
            resolved = true;
            Trace("impact Lucian | degats=" + damage);
            player.ApplyDamage(damage, "BrokenAnchorLightBall");
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
        {
            resolved = true;
            Trace("impact decor | " + other.name);
            Destroy(gameObject);
        }
    }

    private bool IsOwnerCollider(Collider collider)
    {
        if (collider == null || ownerRoot == null) return false;
        return collider.transform == ownerRoot || collider.transform.IsChildOf(ownerRoot);
    }

    private void PlaySpawnCharacterEffects()
    {
        if (childCharacterEffects == null || childCharacterEffects.Length == 0)
            childCharacterEffects = GetComponentsInChildren<CharacterEffect>(true);
        if (characterEffect != null && childCharacterEffects.Length == 0)
            childCharacterEffects = new[] { characterEffect };
        foreach (CharacterEffect effect in childCharacterEffects)
        {
            if (effect == null) continue;
            RestartCharacterEffect(effect);
        }

        // CharacterEffect initialise aussi son VFX dans Start(). Une boule est lancee
        // le meme frame que son instanciation : rejouer l'effet au frame suivant evite
        // que cette initialisation ne reinitialise un VFX qui vient juste de demarrer.
        if (deferredEffectRestart != null) StopCoroutine(deferredEffectRestart);
        deferredEffectRestart = StartCoroutine(RestartCharacterEffectsAfterInitialisation());
    }

    private System.Collections.IEnumerator RestartCharacterEffectsAfterInitialisation()
    {
        yield return null;
        deferredEffectRestart = null;

        foreach (CharacterEffect effect in childCharacterEffects)
        {
            if (effect != null) RestartCharacterEffect(effect);
        }
    }

    private void RestartCharacterEffect(CharacterEffect effect)
    {
        CharacterEffectRuntimeRepair.EnsureReady(effect);
        VisualEffect vfx = effect.vfxComponent;
        if (vfx == null)
        {
            Trace("CharacterEffect ignore : VisualEffect introuvable");
            return;
        }

        // StartEffect ne fait qu'un Play(). Pour un VFX non-boucle deja termine,
        // Play seul ne garantit pas une nouvelle emission. Reinit repart du debut.
        effect.StartEffect();
        vfx.Reinit();
        effect.SetProperty_EffectActive(true);
        vfx.Play();
        Trace("CharacterEffect joue | " + effect.name);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void Trace(string message) => Debug.Log("[BrokenAnchorLightBall] " + message, this);

    private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new();
        public int Compare(RaycastHit left, RaycastHit right) => left.distance.CompareTo(right.distance);
    }
}
