using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Local camera presentation. Property blocks exist only during this camera's render.</summary>
public sealed class LitTacticalVisibilityMask
{
    private sealed class State
    {
        public float playerSeen = float.NegativeInfinity, enemySeen = float.NegativeInfinity;
        public float playerStrength, enemyStrength;
    }
    private readonly LitGameplayCameraModeController owner;
    private readonly Camera camera;
    private readonly Dictionary<LitCameraOcclusionGroup, State> groups = new Dictionary<LitCameraOcclusionGroup, State>();
    private readonly List<LitCameraOcclusionGroup> expired = new List<LitCameraOcclusionGroup>();
    private readonly RaycastHit[] hits = new RaycastHit[128];
    private readonly Collider[] overlaps = new Collider[64];
    private float nextQuery;
    private int lastFrame = -1;
    private bool active, rendering;
    private Vector3 player, enemy;
    private bool hasEnemy;
    private Transform previousEnemy;
    private LitTacticalCameraProfile profile;
    public bool Covers(LitCameraOcclusionGroup group) => active && groups.ContainsKey(group);

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
        active = true; profile = settings;
        player = playerAnchor; hasEnemy = lockedEnemy != null;
        if (previousEnemy != lockedEnemy)
        {
            foreach (var pair in groups) { pair.Value.enemySeen = float.NegativeInfinity; pair.Value.enemyStrength = 0; }
            previousEnemy = lockedEnemy; nextQuery = 0;
        }
        if (hasEnemy) enemy = lockedEnemy.position + Vector3.up;
        if (Time.unscaledTime >= nextQuery)
        {
            nextQuery = Time.unscaledTime + Mathf.Max(.01f, settings.occlusionInterval);
            Detect(desired, player, false, layers);
            if (hasEnemy) Detect(desired, enemy, true, layers);
            // Raycasts cannot detect a collider containing their origin.
            int count = Physics.OverlapSphereNonAlloc(desired, Mathf.Max(.01f, radius), overlaps, layers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (owner.ShouldIgnoreTacticalCollider(overlaps[i])) continue;
                var group = overlaps[i].GetComponentInParent<LitCameraOcclusionGroup>();
                Mark(group, false); if (hasEnemy) Mark(group, true);
            }
        }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        expired.Clear();
        float step = Mathf.Min(Time.unscaledDeltaTime, .1f) / Mathf.Max(.01f, settings.maskFadeTime);
        foreach (var pair in groups)
        {
            State state = pair.Value;
            bool compatible = pair.Key != null && pair.Key.IsMaskCompatible(out _);
            state.playerStrength = Mathf.MoveTowards(state.playerStrength,
                compatible && Time.unscaledTime - state.playerSeen <= settings.occlusionRestoreDelay ? 1 : 0, step);
            state.enemyStrength = Mathf.MoveTowards(state.enemyStrength,
                compatible && hasEnemy && Time.unscaledTime - state.enemySeen <= settings.occlusionRestoreDelay ? 1 : 0, step);
            if (!compatible || (state.playerStrength == 0 && state.enemyStrength == 0 &&
                Time.unscaledTime - Mathf.Max(state.playerSeen, state.enemySeen) > settings.occlusionRestoreDelay)) expired.Add(pair.Key);
        }
        foreach (var group in expired) { if (group != null) group.RestoreMask(this); groups.Remove(group); }
    }

    private void Detect(Vector3 origin, Vector3 target, bool isEnemy, int layers)
    {
        Vector3 delta = target - origin;
        if (delta.sqrMagnitude < .0001f) return;
        int count = Physics.RaycastNonAlloc(origin, delta.normalized, hits, delta.magnitude, layers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!owner.ShouldIgnoreTacticalCollider(hits[i].collider)) Mark(hits[i].collider.GetComponentInParent<LitCameraOcclusionGroup>(), isEnemy);
    }
    private void Mark(LitCameraOcclusionGroup group, bool isEnemy)
    {
        if (group == null || !group.CanBypassCameraCollision()) return;
        if (!groups.TryGetValue(group, out State state)) { state = new State(); groups.Add(group, state); }
        if (isEnemy) state.enemySeen = Time.unscaledTime; else state.playerSeen = Time.unscaledTime;
    }
    public static Vector4 ProjectTarget(Camera camera, Vector3 target, float strength)
    {
        Vector3 viewport = camera.WorldToViewportPoint(target);
        return new Vector4(viewport.x, viewport.y, viewport.z, viewport.z > camera.nearClipPlane ? strength : 0);
    }
    private void BeginRender(ScriptableRenderContext context, Camera renderedCamera)
    {
        // Nested reflections are excluded by the HLSL camera-pose guard. Do not
        // clear the parent's effect while HDRP renders a nested reflection.
        if (renderedCamera != camera) return;
        RestoreBlocks();
        if (!active || !owner.MaskActive || profile == null) return;
        rendering = true;
        var data = new LitTacticalMaskProperties.Data
        {
            settings = new Vector4(profile.maskRadius, profile.maskFeather, camera.aspect, .05f),
            cameraPosition = camera.transform.position,
            cameraForward = camera.transform.forward
        };
        foreach (var pair in groups)
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
        foreach (var pair in groups) if (pair.Key != null) pair.Key.RestoreMask(this);
        rendering = false;
    }
    public void Clear()
    {
        RestoreBlocks(); groups.Clear(); expired.Clear(); active = false; nextQuery = 0; lastFrame = -1; previousEnemy = null;
    }
    public void Dispose()
    {
        Clear(); RenderPipelineManager.beginCameraRendering -= BeginRender;
        RenderPipelineManager.endCameraRendering -= EndRender;
    }
}
