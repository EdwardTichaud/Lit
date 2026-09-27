using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime containment for a combat arena. The visual BattleWall collider is
/// still allowed to provide normal physical feedback, while this component
/// closes the holes left by scripted movement, root motion and impulses.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleWallContainment : MonoBehaviour
{
    private static readonly List<BattleWallContainment> ActiveWalls = new List<BattleWallContainment>();

    [SerializeField, Min(0f), Tooltip("Safety space kept between a combatant and the wall surface.")]
    private float boundarySkin = 0.06f;
    [SerializeField, Tooltip("Optional diagnostics for an attempted arena exit.")]
    private bool logContainment;

    private Collider boundaryCollider;
    private Transform configuredPlayer;
    private EnemyController configuredEnemy;
    private readonly List<Transform> targetBuffer = new List<Transform>(2);

    private void Awake() => ResolveBoundaryCollider();

    private void OnEnable()
    {
        if (!ActiveWalls.Contains(this))
        {
            ActiveWalls.Add(this);
        }
    }

    private void OnDisable() => ActiveWalls.Remove(this);

    /// <summary>Called by the wall owner on every peer after the wall is spawned.</summary>
    public void Initialize(EnemyController enemy, Transform player)
    {
        configuredEnemy = enemy;
        configuredPlayer = player;
        ResolveBoundaryCollider();
    }

    private void LateUpdate()
    {
        ResolveCombatants();
        for (int i = 0; i < targetBuffer.Count; i++)
        {
            ConstrainCombatant(targetBuffer[i]);
        }
    }

    /// <summary>
    /// Removes only the component of a normal locomotion input which points
    /// out of the active arena. Tangential motion remains fully responsive.
    /// </summary>
    public static Vector2 ConstrainPlanarInput(Transform actor, Vector2 worldInput)
    {
        if (actor == null || worldInput.sqrMagnitude <= 0.0001f)
        {
            return worldInput;
        }

        Vector3 planar = new Vector3(worldInput.x, 0f, worldInput.y);
        for (int i = ActiveWalls.Count - 1; i >= 0; i--)
        {
            BattleWallContainment wall = ActiveWalls[i];
            if (wall == null || !wall.TryGetPlanarBoundary(actor, out Vector3 center, out float allowedRadius))
            {
                continue;
            }

            Vector3 radial = actor.position - center;
            radial.y = 0f;
            float distance = radial.magnitude;
            if (distance < allowedRadius - 0.02f || distance <= 0.0001f)
            {
                continue;
            }

            Vector3 outward = radial / distance;
            float outwardInput = Vector3.Dot(planar, outward);
            if (outwardInput > 0f)
            {
                planar -= outward * outwardInput;
            }
        }

        return new Vector2(planar.x, planar.z);
    }

    private void ResolveCombatants()
    {
        targetBuffer.Clear();
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        Transform player = configuredPlayer != null ? configuredPlayer : manager != null ? manager.PlayerRoot : null;
        EnemyController enemy = configuredEnemy != null ? configuredEnemy : manager != null ? manager.EngagedEnemy : null;
        AddTarget(player);
        AddTarget(enemy != null ? enemy.transform : null);
    }

    private void AddTarget(Transform target)
    {
        if (target != null && target.gameObject.activeInHierarchy && !targetBuffer.Contains(target))
        {
            targetBuffer.Add(target);
        }
    }

    private void ConstrainCombatant(Transform actor)
    {
        if (!TryGetPlanarBoundary(actor, out Vector3 center, out float allowedRadius))
        {
            return;
        }

        Vector3 position = actor.position;
        Vector3 radial = position - center;
        radial.y = 0f;
        float distance = radial.magnitude;
        if (distance <= allowedRadius || distance <= 0.0001f)
        {
            return;
        }

        Vector3 corrected = center + radial / distance * allowedRadius;
        corrected.y = position.y;

        LitOpsiveLocomotionBridge playerBridge = actor.GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
        if (playerBridge != null)
        {
            playerBridge.ConstrainToBattleWall(corrected);
        }
        else
        {
            EnemyController enemy = actor.GetComponentInChildren<EnemyController>(true);
            if (enemy != null)
            {
                enemy.ConstrainToBattleWall(corrected);
            }
            else if (actor.TryGetComponent(out Rigidbody body))
            {
                body.position = corrected;
                Physics.SyncTransforms();
            }
            else
            {
                actor.position = corrected;
            }
        }

        if (logContainment)
        {
            Debug.Log("[BattleWall] Exit prevented | actor='" + actor.name + "' | requested=" + position + " | corrected=" + corrected + ".", this);
        }
    }

    private bool TryGetPlanarBoundary(Transform actor, out Vector3 center, out float allowedRadius)
    {
        ResolveBoundaryCollider();
        center = boundaryCollider != null ? boundaryCollider.bounds.center : transform.position;
        allowedRadius = 0f;
        if (boundaryCollider == null || !boundaryCollider.enabled)
        {
            return false;
        }

        Bounds bounds = boundaryCollider.bounds;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        allowedRadius = radius - ResolveActorRadius(actor) - Mathf.Max(0f, boundarySkin);
        return allowedRadius > 0.01f;
    }

    private void ResolveBoundaryCollider()
    {
        if (boundaryCollider == null)
        {
            boundaryCollider = GetComponentInChildren<MeshCollider>(true);
            if (boundaryCollider == null)
            {
                boundaryCollider = GetComponentInChildren<Collider>(true);
            }
        }
    }

    private static float ResolveActorRadius(Transform actor)
    {
        Collider[] colliders = actor.GetComponentsInChildren<Collider>(false);
        float radius = 0.35f;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            radius = Mathf.Max(radius, Mathf.Max(bounds.extents.x, bounds.extents.z));
        }

        return radius;
    }
}
