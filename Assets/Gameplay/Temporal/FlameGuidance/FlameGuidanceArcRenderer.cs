using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Feedback visuel local reliant une Ancient Flame proche aux Flames eteintes les plus proches.
[DefaultExecutionOrder(-15)]
[DisallowMultipleComponent]
public sealed class FlameGuidanceArcRenderer : MonoBehaviour
{
    private const string RuntimeHostName = "FlameGuidanceArcRenderer";

    private static FlameGuidanceArcRenderer runtimeInstance;

    [Header("Activation")]
    [SerializeField, Min(0.25f), Tooltip("Distance maximale du joueur a une Ancient Flame pour afficher les arcs.")]
    private float ancientProximityRadius = 7f;
    [SerializeField, Min(0.05f), Tooltip("Intervalle de recherche des Flames cibles.")]
    private float targetRefreshInterval = 0.35f;

    [Header("Arc")]
    [SerializeField, Min(2), Tooltip("Nombre de segments par arc.")]
    private int arcSegments = 16;
    [SerializeField, Min(0f), Tooltip("Hauteur verticale du point de controle de l'arc.")]
    private float arcHeight = 1.35f;
    [SerializeField, Min(0.001f), Tooltip("Epaisseur des traits.")]
    private float arcWidth = 0.025f;
    [SerializeField, Tooltip("Offset depuis l'ancre de la Flame source.")]
    private Vector3 sourceOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField, Tooltip("Offset depuis l'ancre de la Flame cible.")]
    private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField, Tooltip("Couleur du lien vers l'Ancient Flame eteinte la plus proche.")]
    private Color ancientArcColor = new Color(1f, 0.68f, 0.16f, 0.72f);
    [SerializeField, Tooltip("Couleur du lien vers la Flame commune eteinte la plus proche.")]
    private Color commonArcColor = new Color(1f, 1f, 1f, 0.62f);

    private LineRenderer ancientArc;
    private LineRenderer commonArc;
    private Material lineMaterial;
    private Transform localCharacter;
    private Flame sourceAncientFlame;
    private Flame targetAncientFlame;
    private Flame targetCommonFlame;
    private float nextTargetRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeInstance()
    {
        runtimeInstance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (runtimeInstance != null)
        {
            return;
        }

        FlameGuidanceArcRenderer existing = FindAnyObjectByType<FlameGuidanceArcRenderer>();
        if (existing != null)
        {
            runtimeInstance = existing;
            return;
        }

        GameObject host = new GameObject(RuntimeHostName);
        DontDestroyOnLoad(host);
        runtimeInstance = host.AddComponent<FlameGuidanceArcRenderer>();
    }

    private void Awake()
    {
        if (runtimeInstance != null && runtimeInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        runtimeInstance = this;
        BuildRenderers();
    }

    private void OnEnable()
    {
        LocalPlayerContext.LocalCharacterChanged += OnLocalCharacterChanged;
        RefreshTargets();
    }

    private void OnDisable()
    {
        LocalPlayerContext.LocalCharacterChanged -= OnLocalCharacterChanged;
        HideArcs();
    }

    private void OnDestroy()
    {
        if (runtimeInstance == this)
        {
            runtimeInstance = null;
        }

        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
            lineMaterial = null;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextTargetRefreshTime)
        {
            RefreshTargets();
        }

        UpdateArcs();
    }

    private void OnLocalCharacterChanged(Transform characterRoot)
    {
        localCharacter = characterRoot;
        RefreshTargets();
    }

    private void RefreshTargets()
    {
        nextTargetRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, targetRefreshInterval);

        localCharacter = ResolveLocalCharacter();
        sourceAncientFlame = FindNearbyAncientFlame(localCharacter);
        if (!IsUsableFlame(sourceAncientFlame))
        {
            targetAncientFlame = null;
            targetCommonFlame = null;
            HideArcs();
            return;
        }

        Vector3 sourcePosition = GetFlamePosition(sourceAncientFlame);
        targetAncientFlame = FindClosestUnlitAncientFlame(sourceAncientFlame, sourcePosition);
        targetCommonFlame = FindClosestUnlitCommonFlame(sourcePosition);
        UpdateArcs();
    }

    private Transform ResolveLocalCharacter()
    {
        if (LocalPlayerContext.LocalCharacterRoot != null)
        {
            return LocalPlayerContext.LocalCharacterRoot;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        return controlled != null ? controlled.transform : null;
    }

    private Flame FindNearbyAncientFlame(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return null;
        }

        Flame closest = null;
        float maxDistance = Mathf.Max(0.25f, ancientProximityRadius);
        float closestSqr = maxDistance * maxDistance;
        Vector3 characterPosition = characterRoot.position;

        AgeManager ageManager = AgeManager.ActiveInstance;
        if (ageManager != null)
        {
            EvaluateNearbyAncientFlames(ageManager.AncientFlames, characterPosition, ref closest, ref closestSqr);
        }

        if (closest == null)
        {
            Flame[] sceneFlames = FindObjectsByType<Flame>(FindObjectsInactive.Exclude);
            EvaluateNearbyAncientFlames(sceneFlames, characterPosition, ref closest, ref closestSqr);
        }

        return closest;
    }

    private void EvaluateNearbyAncientFlames(
        IReadOnlyList<Flame> flames,
        Vector3 characterPosition,
        ref Flame closest,
        ref float closestSqr)
    {
        if (flames == null)
        {
            return;
        }

        for (int i = 0; i < flames.Count; i++)
        {
            Flame flame = flames[i];
            if (!IsUsableFlame(flame) || !flame.IsAncientFlame || !flame.IsEffectivelyLit)
            {
                continue;
            }

            float distanceSqr = GetFlatSqrDistance(characterPosition, GetFlamePosition(flame));
            if (distanceSqr < closestSqr)
            {
                closest = flame;
                closestSqr = distanceSqr;
            }
        }
    }

    private Flame FindClosestUnlitAncientFlame(Flame source, Vector3 sourcePosition)
    {
        Flame closest = null;
        float closestSqr = float.PositiveInfinity;

        AgeManager ageManager = AgeManager.ActiveInstance;
        if (ageManager != null)
        {
            EvaluateUnlitFlames(ageManager.AncientFlames, source, sourcePosition, ancientOnly: true, ref closest, ref closestSqr);
        }

        if (closest == null)
        {
            Flame[] sceneFlames = FindObjectsByType<Flame>(FindObjectsInactive.Exclude);
            EvaluateUnlitFlames(sceneFlames, source, sourcePosition, ancientOnly: true, ref closest, ref closestSqr);
        }

        return closest;
    }

    private Flame FindClosestUnlitCommonFlame(Vector3 sourcePosition)
    {
        Flame closest = null;
        float closestSqr = float.PositiveInfinity;
        Flame[] sceneFlames = FindObjectsByType<Flame>(FindObjectsInactive.Exclude);
        EvaluateUnlitFlames(sceneFlames, null, sourcePosition, ancientOnly: false, ref closest, ref closestSqr);
        return closest;
    }

    private void EvaluateUnlitFlames(
        IReadOnlyList<Flame> flames,
        Flame excludedFlame,
        Vector3 sourcePosition,
        bool ancientOnly,
        ref Flame closest,
        ref float closestSqr)
    {
        if (flames == null)
        {
            return;
        }

        for (int i = 0; i < flames.Count; i++)
        {
            Flame flame = flames[i];
            if (!IsUsableFlame(flame) || flame == excludedFlame || flame.IsEffectivelyLit)
            {
                continue;
            }

            if (ancientOnly != flame.IsAncientFlame)
            {
                continue;
            }

            float distanceSqr = GetFlatSqrDistance(sourcePosition, GetFlamePosition(flame));
            if (distanceSqr < closestSqr)
            {
                closest = flame;
                closestSqr = distanceSqr;
            }
        }
    }

    private void UpdateArcs()
    {
        bool hasSource = IsUsableFlame(sourceAncientFlame);
        UpdateArc(ancientArc, hasSource ? sourceAncientFlame : null, targetAncientFlame, ancientArcColor);
        UpdateArc(commonArc, hasSource ? sourceAncientFlame : null, targetCommonFlame, commonArcColor);
    }

    private void UpdateArc(LineRenderer line, Flame source, Flame target, Color color)
    {
        if (line == null || !IsUsableFlame(source) || !IsUsableFlame(target) || target.IsEffectivelyLit)
        {
            SetLineVisible(line, false);
            return;
        }

        Vector3 start = GetFlamePosition(source) + sourceOffset;
        Vector3 end = GetFlamePosition(target) + targetOffset;
        if ((end - start).sqrMagnitude <= 0.01f)
        {
            SetLineVisible(line, false);
            return;
        }

        int segmentCount = Mathf.Max(2, arcSegments);
        line.positionCount = segmentCount + 1;
        line.startWidth = arcWidth;
        line.endWidth = arcWidth;
        line.startColor = color;
        line.endColor = color;
        line.enabled = true;

        Vector3 control = (start + end) * 0.5f + Vector3.up * Mathf.Max(0f, arcHeight);
        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            line.SetPosition(i, QuadraticBezier(start, control, end, t));
        }
    }

    private void HideArcs()
    {
        SetLineVisible(ancientArc, false);
        SetLineVisible(commonArc, false);
    }

    private static void SetLineVisible(LineRenderer line, bool visible)
    {
        if (line == null)
        {
            return;
        }

        line.enabled = visible;
        if (!visible)
        {
            line.positionCount = 0;
        }
    }

    private void BuildRenderers()
    {
        if (ancientArc != null && commonArc != null)
        {
            return;
        }

        lineMaterial = CreateLineMaterial();
        ancientArc = CreateLineRenderer("AncientFlameArc");
        commonArc = CreateLineRenderer("CommonFlameArc");
        HideArcs();
    }

    private LineRenderer CreateLineRenderer(string objectName)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (lineMaterial != null)
        {
            line.material = lineMaterial;
        }

        return line;
    }

    private static Material CreateLineMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("UI/Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Hidden/Internal-Colored");
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
        if (material.HasProperty("_Color"))
        {
            material.color = Color.white;
        }

        return material;
    }

    private static bool IsUsableFlame(Flame flame)
    {
        return flame != null
            && flame.isActiveAndEnabled
            && flame.gameObject.activeInHierarchy;
    }

    private static Vector3 GetFlamePosition(Flame flame)
    {
        if (flame == null)
        {
            return Vector3.zero;
        }

        Transform anchor = flame.GetInteractionAnchor();
        return anchor != null ? anchor.position : flame.transform.position;
    }

    private static float GetFlatSqrDistance(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        return delta.sqrMagnitude;
    }

    private static Vector3 QuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }
}
