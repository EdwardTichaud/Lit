using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Local camera presentation. Every occluding layer shares the same target opening.</summary>
public sealed class LitTacticalVisibilityMask
{
    private readonly LitGameplayCameraModeController owner;
    private readonly Camera camera;
    private readonly LitTacticalMaskStack stack = new LitTacticalMaskStack();
    private readonly List<LitCameraOcclusionGroup> visualHits = new List<LitCameraOcclusionGroup>(128);
    private RaycastHit[] hits = new RaycastHit[128];
    private Collider[] overlaps = new Collider[64];
    private float nextQuery, nextRenderQuery;
    private int physicsRevision = -1, renderRevision = -1, lastFrame = -1, obstacleLayers;
    private bool active, rendering, hasEnemy;
    private Vector3 player, enemy;
    private float playerGround, enemyGround;
    private Transform previousEnemy, enemyTransform;
    private LitTacticalCameraProfile profile;
    public int LayerCount => stack.Layers.Count;
    public bool Covers(LitCameraOcclusionGroup group) => active && stack.Contains(group);

    public LitTacticalVisibilityMask(LitGameplayCameraModeController owner, Camera camera)
    {
        this.owner = owner; this.camera = camera;
        RenderPipelineManager.beginCameraRendering += BeginRender;
        RenderPipelineManager.endCameraRendering += EndRender;
    }

    public void Prepare(Vector3 desired, Vector3 playerAnchor, Transform lockedEnemy,
        float radius, int layers, LitTacticalCameraProfile settings)
    {
        if (!owner.MaskActive) { Clear(); return; }
        active = true; profile = settings; obstacleLayers = layers;
        player = playerAnchor; hasEnemy = lockedEnemy != null; enemyTransform = lockedEnemy;
        if (previousEnemy != lockedEnemy)
        {
            stack.ResetEnemy(); previousEnemy = lockedEnemy;
            nextQuery = nextRenderQuery = 0;
        }
        UpdateGroundHeights();
        if (hasEnemy) enemy = lockedEnemy.position + Vector3.up;
        if (Time.unscaledTime >= nextQuery || physicsRevision != LitCameraOcclusionGroup.RegistryRevision)
        {
            nextQuery = Time.unscaledTime + Mathf.Max(.01f, settings.occlusionInterval);
            physicsRevision = LitCameraOcclusionGroup.RegistryRevision;
            Detect(desired, player, false);
            if (hasEnemy) Detect(desired, enemy, true);
            // Raycasts cannot detect a collider containing their origin.
            int count = LitTacticalMaskQueries.Overlap(ref overlaps, desired, Mathf.Max(.01f, radius), layers);
            for (int i = 0; i < count; i++)
            {
                if (owner.ShouldIgnoreTacticalCollider(overlaps[i])) continue;
                var group = overlaps[i].GetComponentInParent<LitCameraOcclusionGroup>();
                Mark(group, false); if (hasEnemy) Mark(group, true);
            }
        }
    }

    private void UpdateGroundHeights()
    {
        playerGround = owner.GetRenderedGroundHeight() + Mathf.Max(0, profile.maskGroundClearance);
        enemyGround = hasEnemy ? enemyTransform.position.y + Mathf.Max(0, profile.maskGroundClearance) : 0;
    }
    private void Detect(Vector3 origin, Vector3 target, bool isEnemy)
    {
        if (camera != null)
        {
            // Include ALL configured groups, not only the automatic zone's entries.
            LitCameraOcclusionGroup.Collect(camera, target, profile.maskRadius + profile.maskFeather,
                isEnemy ? enemyGround : playerGround, visualHits);
            foreach (var group in visualHits) Mark(group, isEnemy);
        }
        Vector3 delta = target - origin;
        if (delta.sqrMagnitude < .0001f) return;
        int count = LitTacticalMaskQueries.Raycast(ref hits, origin, delta.normalized, delta.magnitude, obstacleLayers);
        for (int i = 0; i < count; i++)
            if (!owner.ShouldIgnoreTacticalCollider(hits[i].collider))
                Mark(hits[i].collider.GetComponentInParent<LitCameraOcclusionGroup>(), isEnemy);
    }
    private void Mark(LitCameraOcclusionGroup group, bool isEnemy)
    {
        if (group != null && group.IsAboveProtectedGround(isEnemy ? enemyGround : playerGround))
            stack.Mark(group, isEnemy, Time.unscaledTime);
    }
    public static Vector4 ProjectTarget(Camera camera, Vector3 target, float strength)
    {
        Vector3 viewport = camera.WorldToViewportPoint(target);
        return new Vector4(viewport.x, viewport.y, viewport.z, viewport.z > camera.nearClipPlane ? strength : 0);
    }
    private void BeginRender(ScriptableRenderContext context, Camera renderedCamera)
    {
        if (renderedCamera != camera) return;
        RestoreBlocks();
        if (!active || !owner.MaskActive || profile == null) return;
        // Query and project the actual displayed pose too: a simulation-pose-only
        // query can miss the second wall on the edge of the interpolated opening.
        player = owner.GetRenderedAnchor(); hasEnemy = enemyTransform != null;
        if (hasEnemy) enemy = enemyTransform.position + Vector3.up;
        UpdateGroundHeights();
        if (Time.unscaledTime >= nextRenderQuery || renderRevision != LitCameraOcclusionGroup.RegistryRevision)
        {
            nextRenderQuery = Time.unscaledTime + Mathf.Max(.01f, profile.occlusionInterval);
            renderRevision = LitCameraOcclusionGroup.RegistryRevision;
            Detect(camera.transform.position, player, false);
            if (hasEnemy) Detect(camera.transform.position, enemy, true);
        }
        if (lastFrame != Time.frameCount)
        {
            lastFrame = Time.frameCount;
            stack.Advance(Time.unscaledTime, Time.unscaledDeltaTime, profile.maskFadeTime,
                profile.occlusionRestoreDelay, hasEnemy);
        }
        rendering = true;
        Vector4 position = camera.transform.position, forward = camera.transform.forward;
        position.w = playerGround; forward.w = enemyGround;
        var data = new LitTacticalMaskProperties.Data
        {
            settings = new Vector4(profile.maskRadius, profile.maskFeather, camera.aspect, .05f),
            cameraPosition = position, cameraForward = forward
        };
        foreach (var pair in stack.Layers)
        {
            if (pair.Key == null) continue;
            data.target0 = ProjectTarget(camera, player, pair.Value.playerStrength);
            data.target1 = hasEnemy ? ProjectTarget(camera, enemy, pair.Value.enemyStrength) : Vector4.zero;
            pair.Key.ApplyMask(this, data);
        }
    }
    private void EndRender(ScriptableRenderContext context, Camera renderedCamera)
    { if (renderedCamera == camera) RestoreBlocks(); }
    private void RestoreBlocks()
    {
        if (!rendering) return;
        foreach (var pair in stack.Layers) if (pair.Key != null) pair.Key.RestoreMask(this);
        rendering = false;
    }
    public void Clear()
    {
        RestoreBlocks(); stack.Clear(); visualHits.Clear(); active = false;
        nextQuery = nextRenderQuery = 0; lastFrame = physicsRevision = renderRevision = -1;
        previousEnemy = enemyTransform = null;
    }
    public void Dispose()
    {
        Clear(); RenderPipelineManager.beginCameraRendering -= BeginRender;
        RenderPipelineManager.endCameraRendering -= EndRender;
    }
}
