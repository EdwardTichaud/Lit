using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public sealed partial class LitGameplayCameraModeController
{
    private Camera renderingCamera;
    private LitTacticalNearClip nearClip;
    private Transform clipPlayer, clipEnemy;
    private Bounds clipPlayerBody, clipEnemyBody;
    private bool hasClipPlayerBody, hasClipEnemyBody;
    private Vector3 clipLastPlayerPosition;
    private bool hasClipPlayerPosition;
    private int clipFrame = -1;

    public bool IgnoringObstacles => EffectiveObstacleMode == TacticalObstacleMode.IgnoreObstacles;
    public bool AdaptiveNearClipActive => isActiveAndEnabled && profile != null && profile.adaptiveNearClip &&
        IgnoringObstacles && !inspection && tactical != null && tactical.Following && ucc != null &&
        ucc.enabled && ucc.Character != null && ucc.ActiveViewType is LitTacticalUccViewType;
    public float InitialNearClip => nearClip != null ? nearClip.Baseline : 0;
    public float EffectiveNearClip => nearClip != null ? nearClip.Effective : 0;

    private void InitializeNearClip()
    {
        renderingCamera = GetComponent<Camera>();
        nearClip = new LitTacticalNearClip(renderingCamera);
    }

    private void ResetNearClip()
    {
        nearClip?.Restore();
        clipPlayer = clipEnemy = null;
        hasClipPlayerBody = hasClipEnemyBody = hasClipPlayerPosition = false;
        clipFrame = -1;
    }

    // HDRP caches projection and culling parameters BEFORE beginCameraRendering.
    // Begin-context is after displayed poses, but before those HDRP caches are built.
    private void OnNearClipRendering(ScriptableRenderContext context, List<Camera> cameras)
    {
        if (renderingCamera == null || !cameras.Contains(renderingCamera)) return;
        Camera camera = renderingCamera;
        if (!AdaptiveNearClipActive) { ResetNearClip(); return; }
        var player = ucc.Character.transform;
        if (hasClipPlayerPosition && (player.position - clipLastPlayerPosition).sqrMagnitude >
            profile.teleportDistance * profile.teleportDistance) ResetNearClip();
        clipLastPlayerPosition = player.position;
        hasClipPlayerPosition = true;
        if (clipPlayer != player)
        {
            nearClip.Restore();
            clipPlayer = player;
            var motor = player.GetComponent<Opsive.UltimateCharacterController.Character.CharacterLocomotion>();
            hasClipPlayerBody = LitTacticalNearClip.TryCaptureBody(player, out clipPlayerBody, motor != null ? motor.Colliders : null);
        }
        if (!hasClipPlayerBody) { nearClip.Restore(); return; }
        Vector3 position = camera.transform.position, forward = camera.transform.forward;
        float playerDepth = Vector3.Dot(GetRenderedAnchor() - position, forward);
        float safeDepth = LitTacticalNearClip.NearestDepth(clipPlayerBody, player, position, forward);
        var enemyController = RealTimeCombatManager.Instance != null ? RealTimeCombatManager.Instance.LockedEnemy : null;
        var enemy = enemyController != null ? enemyController.transform : null;
        if (clipEnemy != enemy)
        {
            clipEnemy = enemy;
            hasClipEnemyBody = LitTacticalNearClip.TryCaptureBody(enemy, out clipEnemyBody);
        }
        if (enemy != null && Vector3.Dot(enemy.position - position, forward) > 0)
        {
            if (!hasClipEnemyBody) { nearClip.Restore(); return; }
            safeDepth = Mathf.Min(safeDepth, LitTacticalNearClip.NearestDepth(clipEnemyBody, enemy, position, forward));
        }
        float dt = clipFrame == Time.frameCount ? 0 : Mathf.Min(Time.unscaledDeltaTime, .1f);
        clipFrame = Time.frameCount;
        nearClip.Apply(playerDepth, safeDepth, profile.nearClipPlayerMargin, profile.nearClipBodySafety,
            profile.nearClipSmoothTime, dt);
    }
}
