using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(650)]
[DisallowMultipleComponent]
public sealed class CombatLockOnCameraController : MonoBehaviour
{
    [SerializeField] private Camera controlledCamera;
    [SerializeField] private Behaviour[] gameplayCameraDrivers;
    [SerializeField] private Vector3 playerOffset = new Vector3(0.8f, 1.5f, -4.5f);
    [SerializeField, Range(0f, 1f)] private float enemyFocusBias = 0.68f;
    [SerializeField, Min(0f)] private float positionSharpness = 9f;
    [SerializeField, Min(0f)] private float rotationSharpness = 12f;
    [SerializeField, Range(15f, 100f)] private float lockedFieldOfView = 56f;
    [SerializeField, Min(0f)] private float orbitDirectionSharpness = 5f;
    [SerializeField, Range(1f, 360f)] private float maximumOrbitDegreesPerSecond = 120f;
    [Header("Stability")]
    [SerializeField, Min(0f)] private float maximumPositionMetersPerSecond = 12f;
    [SerializeField, Range(1f, 720f)] private float maximumRotationDegreesPerSecond = 180f;
    [SerializeField, Min(0f)] private float maximumFieldOfViewDegreesPerSecond = 35f;
    [SerializeField, Min(0f), Tooltip("Lissage du point regarde pour filtrer les secousses d'animation de la cible.")]
    private float focusPointSharpness = 16f;
    [SerializeField, Min(0f), Tooltip("Vitesse maximale de deplacement du point regarde.")]
    private float maximumFocusPointMetersPerSecond = 12f;

    [Header("Player Framing")]
    [SerializeField, Range(0.1f, 0.95f)] private float playerViewportHeight = 0.72f;
    [SerializeField, Range(0.1f, 0.95f)] private float playerViewportWidth = 0.82f;
    [SerializeField, Min(0f)] private float playerFramingPadding = 0.15f;
    [SerializeField, Range(15f, 100f)] private float maximumLockedFieldOfView = 72f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private bool avoidVisualObstacles = true;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField, Min(0.01f)] private float obstacleProbeRadius = 0.2f;
    [SerializeField, Min(0f)] private float obstaclePadding = 0.08f;
    [SerializeField, Min(0.05f)] private float minimumObstacleDistance = 0.45f;
    [SerializeField, Min(0f), Tooltip("Temps pendant lequel la camera conserve sa distance reduite apres la disparition ponctuelle d'un obstacle.")]
    private float obstacleReleaseGraceSeconds = 0.12f;
    [SerializeField, Min(0f), Tooltip("Lissage applique lorsque la camera peut reprendre de la distance apres un obstacle.")]
    private float obstacleReleaseSharpness = 5f;
    [SerializeField, Min(0f), Tooltip("Ignore les petites variations de distance causees par des collisions voisines.")]
    private float obstacleDistanceDeadZone = 0.08f;

    private bool active;
    private bool[] previousDriverStates;
    private float originalFieldOfView;
    private Vector3 smoothedFlatDirection;
    private bool hasSmoothedFlatDirection;
    private Vector3 smoothedFocusPoint;
    private bool hasSmoothedFocusPoint;
    private Transform framedPlayer;
    private Renderer[] playerRenderers;
    // A dense environment can produce more than 16 hits. A truncated non-alloc query
    // may omit the closest obstacle on alternate frames, which makes the camera jump.
    private readonly RaycastHit[] obstacleHits = new RaycastHit[64];
    private bool hasResolvedObstacleDistance;
    private float resolvedObstacleDistance;
    private float lastObstacleTime;

    private void Awake()
    {
        if (controlledCamera == null)
        {
            controlledCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        if (RealTimeCombatManager.Instance != null)
        {
            RealTimeCombatManager.Instance.LockChanged += OnLockChanged;
        }
    }

    private void OnDisable()
    {
        if (RealTimeCombatManager.Instance != null)
        {
            RealTimeCombatManager.Instance.LockChanged -= OnLockChanged;
        }

        RestoreGameplayCamera();
    }

    private void LateUpdate()
    {
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        if (!active || manager == null || manager.PlayerRoot == null || manager.LockedEnemy == null || controlledCamera == null)
        {
            return;
        }

        EnforceExclusiveCameraControl();

        Transform player = manager.PlayerRoot;
        Transform enemy = manager.LockedEnemy.LockPoint;
        Vector3 flatDirection = Vector3.ProjectOnPlane(enemy.position - player.position, Vector3.up).normalized;
        if (flatDirection.sqrMagnitude < 0.001f)
        {
            flatDirection = player.forward;
        }

        UpdateSmoothedOrbitDirection(flatDirection);
        Vector3 desiredPosition = player.position + Quaternion.LookRotation(smoothedFlatDirection, Vector3.up) * playerOffset;
        Vector3 rawLookPoint = Vector3.Lerp(player.position + Vector3.up * 1.25f, enemy.position + Vector3.up * 1.1f, enemyFocusBias);
        Vector3 lookPoint = UpdateSmoothedFocusPoint(rawLookPoint);
        float desiredFieldOfView = lockedFieldOfView;
        ApplyPlayerFraming(ref desiredPosition, lookPoint, player, ref desiredFieldOfView);
        desiredPosition = ResolveObstacleFreePosition(lookPoint, desiredPosition, player, manager.LockedEnemy.transform);
        ApplyPlayerFramingFieldOfView(desiredPosition, lookPoint, player, ref desiredFieldOfView);
        float positionBlend = 1f - Mathf.Exp(-positionSharpness * Time.unscaledDeltaTime);
        float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.unscaledDeltaTime);
        Vector3 currentPosition = controlledCamera.transform.position;
        Vector3 blendedPosition = Vector3.Lerp(currentPosition, desiredPosition, positionBlend);
        float maximumPositionStep = maximumPositionMetersPerSecond * Time.unscaledDeltaTime;
        controlledCamera.transform.position = maximumPositionMetersPerSecond > 0f
            ? Vector3.MoveTowards(currentPosition, blendedPosition, maximumPositionStep)
            : blendedPosition;

        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - controlledCamera.transform.position, Vector3.up);
        Quaternion blendedRotation = Quaternion.Slerp(controlledCamera.transform.rotation, desiredRotation, rotationBlend);
        float maximumRotationStep = maximumRotationDegreesPerSecond * Time.unscaledDeltaTime;
        controlledCamera.transform.rotation = Quaternion.RotateTowards(
            controlledCamera.transform.rotation,
            blendedRotation,
            maximumRotationStep);

        float blendedFieldOfView = Mathf.Lerp(controlledCamera.fieldOfView, desiredFieldOfView, positionBlend);
        float maximumFieldOfViewStep = maximumFieldOfViewDegreesPerSecond * Time.unscaledDeltaTime;
        controlledCamera.fieldOfView = maximumFieldOfViewDegreesPerSecond > 0f
            ? Mathf.MoveTowards(controlledCamera.fieldOfView, blendedFieldOfView, maximumFieldOfViewStep)
            : blendedFieldOfView;
    }

    private void UpdateSmoothedOrbitDirection(Vector3 targetDirection)
    {
        if (!hasSmoothedFlatDirection)
        {
            smoothedFlatDirection = targetDirection;
            hasSmoothedFlatDirection = true;
            return;
        }

        float blend = 1f - Mathf.Exp(-orbitDirectionSharpness * Time.unscaledDeltaTime);
        float desiredDegrees = Vector3.Angle(smoothedFlatDirection, targetDirection) * blend;
        float maximumDegrees = maximumOrbitDegreesPerSecond * Time.unscaledDeltaTime;
        smoothedFlatDirection = Vector3.RotateTowards(
            smoothedFlatDirection,
            targetDirection,
            Mathf.Min(desiredDegrees, maximumDegrees) * Mathf.Deg2Rad,
            0f).normalized;
    }

    private Vector3 UpdateSmoothedFocusPoint(Vector3 targetPoint)
    {
        if (!hasSmoothedFocusPoint)
        {
            smoothedFocusPoint = targetPoint;
            hasSmoothedFocusPoint = true;
            return smoothedFocusPoint;
        }

        float blend = 1f - Mathf.Exp(-focusPointSharpness * Time.unscaledDeltaTime);
        Vector3 blendedPoint = Vector3.Lerp(smoothedFocusPoint, targetPoint, blend);
        float maximumStep = maximumFocusPointMetersPerSecond * Time.unscaledDeltaTime;
        smoothedFocusPoint = maximumFocusPointMetersPerSecond > 0f
            ? Vector3.MoveTowards(smoothedFocusPoint, blendedPoint, maximumStep)
            : blendedPoint;
        return smoothedFocusPoint;
    }

    private void ApplyPlayerFraming(ref Vector3 cameraPosition, Vector3 lookPoint, Transform player, ref float desiredFieldOfView)
    {
        if (!TryGetPlayerBounds(player, out Bounds bounds))
        {
            return;
        }

        Quaternion lookRotation = Quaternion.LookRotation(lookPoint - cameraPosition, Vector3.up);
        float requiredDepth = GetRequiredPlayerDepth(bounds, cameraPosition, lookRotation);
        float currentDepth = Vector3.Dot(bounds.center - cameraPosition, lookRotation * Vector3.forward);
        if (requiredDepth > currentDepth)
        {
            Vector3 retreatDirection = (cameraPosition - lookPoint).normalized;
            cameraPosition += retreatDirection * (requiredDepth - currentDepth);
        }

        ApplyPlayerFramingFieldOfView(cameraPosition, lookPoint, player, ref desiredFieldOfView);
    }

    private void ApplyPlayerFramingFieldOfView(Vector3 cameraPosition, Vector3 lookPoint, Transform player, ref float desiredFieldOfView)
    {
        if (!TryGetPlayerBounds(player, out Bounds bounds))
        {
            return;
        }

        Quaternion lookRotation = Quaternion.LookRotation(lookPoint - cameraPosition, Vector3.up);
        float depth = Mathf.Max(0.01f, Vector3.Dot(bounds.center - cameraPosition, lookRotation * Vector3.forward));
        Vector3 localCenter = Quaternion.Inverse(lookRotation) * (bounds.center - cameraPosition);
        float verticalExtent = Mathf.Abs(localCenter.y) + bounds.extents.y + playerFramingPadding;
        float horizontalExtent = Mathf.Abs(localCenter.x) + bounds.extents.x + playerFramingPadding;
        float verticalHalfAngle = Mathf.Atan(verticalExtent / (depth * playerViewportHeight));
        float horizontalHalfAngle = Mathf.Atan(horizontalExtent / (depth * playerViewportWidth * Mathf.Max(0.01f, controlledCamera.aspect)));
        float framingFieldOfView = Mathf.Rad2Deg * 2f * Mathf.Max(verticalHalfAngle, horizontalHalfAngle);
        desiredFieldOfView = Mathf.Clamp(Mathf.Max(desiredFieldOfView, framingFieldOfView), 15f, maximumLockedFieldOfView);
    }

    private float GetRequiredPlayerDepth(Bounds bounds, Vector3 cameraPosition, Quaternion lookRotation)
    {
        Vector3 localCenter = Quaternion.Inverse(lookRotation) * (bounds.center - cameraPosition);
        float verticalExtent = Mathf.Abs(localCenter.y) + bounds.extents.y + playerFramingPadding;
        float horizontalExtent = Mathf.Abs(localCenter.x) + bounds.extents.x + playerFramingPadding;
        float maximumHalfAngleTangent = Mathf.Tan(Mathf.Deg2Rad * maximumLockedFieldOfView * 0.5f);
        float verticalDepth = verticalExtent / Mathf.Max(0.001f, maximumHalfAngleTangent * playerViewportHeight);
        float horizontalDepth = horizontalExtent / Mathf.Max(0.001f, maximumHalfAngleTangent * playerViewportWidth * Mathf.Max(0.01f, controlledCamera.aspect));
        return Mathf.Max(verticalDepth, horizontalDepth);
    }

    private bool TryGetPlayerBounds(Transform player, out Bounds bounds)
    {
        if (framedPlayer != player || playerRenderers == null)
        {
            framedPlayer = player;
            playerRenderers = player.GetComponentsInChildren<Renderer>(true);
        }

        bool hasBounds = false;
        bounds = default;
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            Renderer renderer = playerRenderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private Vector3 ResolveObstacleFreePosition(Vector3 origin, Vector3 desiredPosition, Transform player, Transform enemy)
    {
        if (!avoidVisualObstacles)
        {
            return desiredPosition;
        }

        Vector3 displacement = desiredPosition - origin;
        float distance = displacement.magnitude;
        if (distance <= 0.0001f)
        {
            return desiredPosition;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            obstacleProbeRadius,
            displacement / distance,
            obstacleHits,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = obstacleHits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(player) || hitCollider.transform.IsChildOf(enemy))
            {
                continue;
            }

            nearestDistance = Mathf.Min(nearestDistance, obstacleHits[i].distance);
        }

        bool obstructed = !float.IsPositiveInfinity(nearestDistance);
        float targetDistance = obstructed
            ? Mathf.Clamp(nearestDistance - obstaclePadding, minimumObstacleDistance, distance)
            : distance;

        if (obstructed)
        {
            lastObstacleTime = Time.unscaledTime;
        }

        if (!hasResolvedObstacleDistance)
        {
            resolvedObstacleDistance = targetDistance;
            hasResolvedObstacleDistance = true;
        }
        else if (obstructed)
        {
            // Never delay a move closer to an obstacle: preventing clipping takes priority.
            resolvedObstacleDistance = Mathf.Min(resolvedObstacleDistance, targetDistance);
        }
        else if (Time.unscaledTime - lastObstacleTime > obstacleReleaseGraceSeconds)
        {
            float delta = targetDistance - resolvedObstacleDistance;
            if (Mathf.Abs(delta) > obstacleDistanceDeadZone)
            {
                float blend = obstacleReleaseSharpness <= 0f
                    ? 1f
                    : 1f - Mathf.Exp(-obstacleReleaseSharpness * Time.unscaledDeltaTime);
                resolvedObstacleDistance = Mathf.Lerp(resolvedObstacleDistance, targetDistance, blend);
            }
        }

        // The desired orbit can get shorter while an old obstacle distance is cached.
        resolvedObstacleDistance = Mathf.Min(resolvedObstacleDistance, distance);
        return origin + displacement / distance * resolvedObstacleDistance;
    }

    private void OnLockChanged(RealTimeCombatEnemy enemy)
    {
        if (enemy != null) ActivateLockCamera();
        else RestoreGameplayCamera();
    }

    private void ActivateLockCamera()
    {
        if (active)
        {
            return;
        }

        if (controlledCamera == null) controlledCamera = Camera.main;
        if (controlledCamera != null) originalFieldOfView = controlledCamera.fieldOfView;
        ResolveGameplayCameraDrivers();
        previousDriverStates = new bool[gameplayCameraDrivers != null ? gameplayCameraDrivers.Length : 0];
        for (int i = 0; i < previousDriverStates.Length; i++)
        {
            Behaviour driver = gameplayCameraDrivers[i];
            if (driver == null) continue;
            previousDriverStates[i] = driver.enabled;
            driver.enabled = false;
        }

        active = true;
        hasSmoothedFlatDirection = false;
        hasSmoothedFocusPoint = false;
        hasResolvedObstacleDistance = false;
    }

    private void RestoreGameplayCamera()
    {
        if (!active)
        {
            return;
        }

        for (int i = 0; i < previousDriverStates.Length; i++)
        {
            if (gameplayCameraDrivers[i] != null) gameplayCameraDrivers[i].enabled = previousDriverStates[i];
        }

        if (controlledCamera != null) controlledCamera.fieldOfView = originalFieldOfView;
        active = false;
        hasSmoothedFlatDirection = false;
        hasSmoothedFocusPoint = false;
        hasResolvedObstacleDistance = false;
        framedPlayer = null;
        playerRenderers = null;
    }

    private void ResolveGameplayCameraDrivers()
    {
        if (gameplayCameraDrivers != null && gameplayCameraDrivers.Length > 0)
        {
            return;
        }

        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
        List<Behaviour> drivers = new List<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;
            if (typeName == "CameraController" || typeName == "CameraControllerHandler" || typeName == "LitUccCameraCharacterBinder")
            {
                drivers.Add(behaviour);
            }
        }

        gameplayCameraDrivers = drivers.ToArray();
    }

    private void EnforceExclusiveCameraControl()
    {
        if (gameplayCameraDrivers == null)
        {
            return;
        }

        for (int i = 0; i < gameplayCameraDrivers.Length; i++)
        {
            Behaviour driver = gameplayCameraDrivers[i];
            if (driver != null && driver.enabled)
            {
                driver.enabled = false;
            }
        }
    }
}
