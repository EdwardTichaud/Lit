using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class TorchColorLinkSystem : MonoBehaviour
{
    [SerializeField, Tooltip("Inclut les torches par defaut non colorees dans le systeme de liens.")]
    private bool includeDefaultTorchColor = false;
    [SerializeField, Tooltip("Material optionnel du LineRenderer. Tu peux l'assigner depuis la scene.")]
    private Material linkMaterial;
    [SerializeField, Min(0.001f), Tooltip("Epaisseur des lignes.")]
    private float lineWidth = 0.045f;
    [SerializeField, Range(0f, 1f), Tooltip("Alpha applique a la couleur de la ligne.")]
    private float lineAlpha = 0.92f;
    [SerializeField, Tooltip("Conserve le systeme au changement de scene.")]
    private bool dontDestroyOnLoad = true;
    [SerializeField, Tooltip("Prefab instancie aux croisements des lignes violettes.")]
    private GameObject treasureFinderPrefab;
    [SerializeField, Tooltip("Vision violette speciale utilisee pour les croisements.")]
    private TorchVisionDefinition violetVision;
    [SerializeField, Min(0.05f), Tooltip("Distance de fusion de deux croisements sur le plan XZ.")]
    private float intersectionMergeDistance = 0.75f;
    [SerializeField, Range(0f, 0.2f), Tooltip("Ignore les croisements trop proches des extremites des segments.")]
    private float endpointIntersectionEpsilon = 0.02f;
    [SerializeField, Min(0.00001f), Tooltip("Tolerance de parallelisme pour les intersections XZ.")]
    private float lineIntersectionEpsilon = 0.0001f;
    [SerializeField, Tooltip("Id de secours si la reference de la vision violette n'est pas renseignee.")]
    private string violetVisionId = "torchvisionviolet";

    private static TorchColorLinkSystem instance;
    private static Material fallbackLineMaterial;

    private readonly Dictionary<LinkKey, LineRenderer> linkRenderers = new Dictionary<LinkKey, LineRenderer>();
    private readonly HashSet<LinkKey> visibleLinks = new HashSet<LinkKey>();
    private readonly List<LinkKey> staleLinks = new List<LinkKey>();
    private readonly List<TorchVisionSystem.TorchSourceInfo> torchSources = new List<TorchVisionSystem.TorchSourceInfo>();
    private readonly List<TorchVisionSystem.TorchSourceInfo> violetSources = new List<TorchVisionSystem.TorchSourceInfo>();
    private readonly List<VioletLinkSegment> violetSegments = new List<VioletLinkSegment>();
    private readonly List<Vector3> violetIntersections = new List<Vector3>();
    private readonly List<TreasureFinder> spawnedTreasureFinders = new List<TreasureFinder>();
    private readonly HashSet<int> uniqueControllerIds = new HashSet<int>();

    private bool autoCreated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        TorchColorLinkSystem existing = FindFirstObjectByType<TorchColorLinkSystem>();
#else
        TorchColorLinkSystem existing = FindObjectOfType<TorchColorLinkSystem>();
#endif
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject host = new GameObject("TorchColorLinkSystem");
        TorchColorLinkSystem created = host.AddComponent<TorchColorLinkSystem>();
        created.autoCreated = true;
        instance = created;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            if (instance.autoCreated && !autoCreated)
            {
                instance.CleanupLinks();
                Destroy(instance.gameObject);
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            instance = this;
        }

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        intersectionMergeDistance = Mathf.Max(0.05f, intersectionMergeDistance);
        endpointIntersectionEpsilon = Mathf.Clamp(endpointIntersectionEpsilon, 0f, 0.2f);
        lineIntersectionEpsilon = Mathf.Max(0.00001f, lineIntersectionEpsilon);
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.RightShoulder += OnRightShoulderPerformed;
    }

    private void LateUpdate()
    {
        RefreshLinks();
    }

    private void OnDisable()
    {
        LocalInputRouter.RightShoulder -= OnRightShoulderPerformed;

        if (instance == this)
        {
            CleanupLinks();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            CleanupLinks();
            instance = null;
        }
    }

    public static bool TryActivateVioletTreasurePulse()
    {
        if (instance == null)
        {
            Bootstrap();
        }

        return instance != null && instance.TryActivateVioletTreasurePulseInternal();
    }

    private void OnRightShoulderPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        if (LocalInputRouter.MoveValue.sqrMagnitude > 0.0001f)
        {
            return;
        }

        TryActivateVioletTreasurePulseInternal();
    }

    private void RefreshLinks()
    {
        visibleLinks.Clear();
        TorchVisionSystem.GetTorchSources(torchSources, requireTorchEquipped: true);
        if (torchSources.Count < 2)
        {
            CleanupLinks();
            return;
        }

        for (int i = 0; i < torchSources.Count - 1; i++)
        {
            TorchVisionSystem.TorchSourceInfo a = torchSources[i];
            if (!CanSourceParticipate(a))
            {
                continue;
            }

            for (int j = i + 1; j < torchSources.Count; j++)
            {
                TorchVisionSystem.TorchSourceInfo b = torchSources[j];
                if (!CanSourceParticipate(b) || a.Controller == b.Controller)
                {
                    continue;
                }

                if (!HaveMatchingTorchColor(a.Color, b.Color))
                {
                    continue;
                }

                LinkKey key = new LinkKey(a.Controller, b.Controller);
                LineRenderer line = GetOrCreateLineRenderer(key);
                Color lineColor = a.Color;
                lineColor.a = Mathf.Clamp01(lineAlpha);
                line.startColor = lineColor;
                line.endColor = lineColor;
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                line.SetPosition(0, a.Position);
                line.SetPosition(1, b.Position);
                visibleLinks.Add(key);
            }
        }

        staleLinks.Clear();
        foreach (KeyValuePair<LinkKey, LineRenderer> entry in linkRenderers)
        {
            if (!visibleLinks.Contains(entry.Key) || entry.Value == null)
            {
                staleLinks.Add(entry.Key);
            }
        }

        for (int i = 0; i < staleLinks.Count; i++)
        {
            RemoveLineRenderer(staleLinks[i]);
        }
    }

    private bool CanSourceParticipate(TorchVisionSystem.TorchSourceInfo source)
    {
        if (source.Controller == null || !source.TorchEquipped)
        {
            return false;
        }

        if (includeDefaultTorchColor)
        {
            return true;
        }

        return source.Vision != null && !source.Vision.useDefaultLightSettings;
    }

    private static bool HaveMatchingTorchColor(Color a, Color b)
    {
        Color32 colorA = (Color32)a;
        Color32 colorB = (Color32)b;
        return colorA.r == colorB.r
            && colorA.g == colorB.g
            && colorA.b == colorB.b
            && colorA.a == colorB.a;
    }

    private LineRenderer GetOrCreateLineRenderer(LinkKey key)
    {
        if (linkRenderers.TryGetValue(key, out LineRenderer existing) && existing != null)
        {
            return existing;
        }

        GameObject lineObject = new GameObject($"TorchLink_{key.FirstId}_{key.SecondId}");
        lineObject.hideFlags = HideFlags.DontSave;
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = linkMaterial != null ? linkMaterial : GetFallbackLineMaterial();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.loop = false;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 6;
        line.numCornerVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        linkRenderers[key] = line;
        return line;
    }

    private void RemoveLineRenderer(LinkKey key)
    {
        if (!linkRenderers.TryGetValue(key, out LineRenderer line))
        {
            return;
        }

        if (line != null)
        {
            Destroy(line.gameObject);
        }

        linkRenderers.Remove(key);
    }

    private void CleanupLinks()
    {
        staleLinks.Clear();
        foreach (KeyValuePair<LinkKey, LineRenderer> entry in linkRenderers)
        {
            staleLinks.Add(entry.Key);
        }

        for (int i = 0; i < staleLinks.Count; i++)
        {
            RemoveLineRenderer(staleLinks[i]);
        }

        visibleLinks.Clear();
        torchSources.Clear();
    }

    private bool TryActivateVioletTreasurePulseInternal()
    {
        CollectActiveVioletSources(violetSources);
        if (violetSources.Count == 0)
        {
            return false;
        }

        if (violetSources.Count >= 3)
        {
            SpawnTreasureFindersAtLineIntersections(violetSources);
        }

        ResetActiveVioletTorches(violetSources);
        return true;
    }

    private void CollectActiveVioletSources(List<TorchVisionSystem.TorchSourceInfo> results)
    {
        results.Clear();
        uniqueControllerIds.Clear();

        TorchVisionSystem.GetTorchSources(torchSources, requireTorchEquipped: true);
        for (int i = 0; i < torchSources.Count; i++)
        {
            TorchVisionSystem.TorchSourceInfo source = torchSources[i];
            if (!CanSourceParticipate(source) || !IsVioletVision(source.Vision))
            {
                continue;
            }

            int controllerId = source.Controller != null ? source.Controller.GetInstanceID() : 0;
            if (controllerId == 0 || !uniqueControllerIds.Add(controllerId))
            {
                continue;
            }

            results.Add(source);
        }
    }

    private void SpawnTreasureFindersAtLineIntersections(List<TorchVisionSystem.TorchSourceInfo> sources)
    {
        violetSegments.Clear();
        violetIntersections.Clear();

        for (int i = 0; i < sources.Count - 1; i++)
        {
            for (int j = i + 1; j < sources.Count; j++)
            {
                violetSegments.Add(new VioletLinkSegment(sources[i], sources[j]));
            }
        }

        if (violetSegments.Count < 2)
        {
            return;
        }

        for (int i = 0; i < violetSegments.Count - 1; i++)
        {
            for (int j = i + 1; j < violetSegments.Count; j++)
            {
                if (!TryGetProjectedIntersectionXZ(violetSegments[i], violetSegments[j], out Vector3 intersection))
                {
                    continue;
                }

                if (ContainsIntersection(violetIntersections, intersection))
                {
                    continue;
                }

                violetIntersections.Add(intersection);
            }
        }

        for (int i = 0; i < violetIntersections.Count; i++)
        {
            SpawnTreasureFinder(violetIntersections[i]);
        }
    }

    private bool ContainsIntersection(List<Vector3> intersections, Vector3 candidate)
    {
        float mergeDistanceSqr = intersectionMergeDistance * intersectionMergeDistance;
        for (int i = 0; i < intersections.Count; i++)
        {
            Vector3 existing = intersections[i];
            Vector2 delta = new Vector2(existing.x - candidate.x, existing.z - candidate.z);
            if (delta.sqrMagnitude <= mergeDistanceSqr)
            {
                return true;
            }
        }

        return false;
    }

    private bool SpawnTreasureFinder(Vector3 position)
    {
        CleanupTreasureFinderReferences();
        if (HasTreasureFinderNear(position))
        {
            return false;
        }

        GameObject finderObject = treasureFinderPrefab != null
            ? Instantiate(treasureFinderPrefab, position, Quaternion.identity)
            : CreateFallbackTreasureFinder(position);

        if (finderObject == null)
        {
            return false;
        }

        finderObject.name = string.IsNullOrWhiteSpace(finderObject.name)
            ? "VioletTreasureFinder"
            : finderObject.name;

        TreasureFinder finder = finderObject.GetComponent<TreasureFinder>();
        if (finder == null)
        {
            finder = finderObject.AddComponent<TreasureFinder>();
        }

        finder.ConfigureRuntime(ResolveVioletVision());
        spawnedTreasureFinders.Add(finder);
        return true;
    }

    private GameObject CreateFallbackTreasureFinder(Vector3 position)
    {
        GameObject fallback = new GameObject("VioletTreasureFinder");
        fallback.transform.SetPositionAndRotation(position, Quaternion.identity);

        TorchVisionDefinition fallbackVision = ResolveVioletVision();
        Light fallbackLight = fallback.AddComponent<Light>();
        fallbackLight.type = LightType.Point;
        fallbackLight.color = fallbackVision != null && !fallbackVision.useDefaultLightSettings
            ? fallbackVision.lightColor
            : new Color(0.6406398f, 0f, 1f, 1f);
        fallbackLight.intensity = 200f;
        fallbackLight.range = 4f;
        fallbackLight.renderMode = LightRenderMode.ForcePixel;
        fallbackLight.shadows = LightShadows.Soft;
        fallbackLight.useColorTemperature = false;

        fallback.AddComponent<TorchLightReceiver>();
        fallback.AddComponent<FlickeringLight>();
        fallback.AddComponent<TreasureFinder>();
        return fallback;
    }

    private bool HasTreasureFinderNear(Vector3 position)
    {
        float mergeDistanceSqr = intersectionMergeDistance * intersectionMergeDistance;
        for (int i = 0; i < spawnedTreasureFinders.Count; i++)
        {
            TreasureFinder finder = spawnedTreasureFinders[i];
            if (finder == null)
            {
                continue;
            }

            Vector3 existingPosition = finder.FinderPosition;
            Vector2 delta = new Vector2(existingPosition.x - position.x, existingPosition.z - position.z);
            if (delta.sqrMagnitude <= mergeDistanceSqr)
            {
                return true;
            }
        }

        return false;
    }

    private void CleanupTreasureFinderReferences()
    {
        for (int i = spawnedTreasureFinders.Count - 1; i >= 0; i--)
        {
            if (spawnedTreasureFinders[i] == null)
            {
                spawnedTreasureFinders.RemoveAt(i);
            }
        }
    }

    private void ResetActiveVioletTorches(List<TorchVisionSystem.TorchSourceInfo> sources)
    {
        uniqueControllerIds.Clear();
        for (int i = 0; i < sources.Count; i++)
        {
            SquadCharacterController controller = sources[i].Controller;
            int controllerId = controller != null ? controller.GetInstanceID() : 0;
            if (controllerId == 0 || !uniqueControllerIds.Add(controllerId))
            {
                continue;
            }

            TorchVisionSystem.ClearVisionFor(controller);
        }
    }

    private bool TryGetProjectedIntersectionXZ(VioletLinkSegment first, VioletLinkSegment second, out Vector3 intersection)
    {
        intersection = default;

        Vector2 a = new Vector2(first.Start.x, first.Start.z);
        Vector2 b = new Vector2(first.End.x, first.End.z);
        Vector2 c = new Vector2(second.Start.x, second.Start.z);
        Vector2 d = new Vector2(second.End.x, second.End.z);

        Vector2 ab = b - a;
        Vector2 cd = d - c;
        float denominator = Cross(ab, cd);
        if (Mathf.Abs(denominator) <= lineIntersectionEpsilon)
        {
            return false;
        }

        Vector2 ac = c - a;
        float t = Cross(ac, cd) / denominator;
        float u = Cross(ac, ab) / denominator;
        if (t <= endpointIntersectionEpsilon
            || t >= 1f - endpointIntersectionEpsilon
            || u <= endpointIntersectionEpsilon
            || u >= 1f - endpointIntersectionEpsilon)
        {
            return false;
        }

        Vector2 pointXZ = a + ab * t;
        float yOnFirst = Mathf.Lerp(first.Start.y, first.End.y, t);
        float yOnSecond = Mathf.Lerp(second.Start.y, second.End.y, u);
        intersection = new Vector3(pointXZ.x, (yOnFirst + yOnSecond) * 0.5f, pointXZ.y);
        return true;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return (a.x * b.y) - (a.y * b.x);
    }

    private bool IsVioletVision(TorchVisionDefinition vision)
    {
        if (vision == null)
        {
            return false;
        }

        TorchVisionDefinition resolved = ResolveVioletVision();
        if (resolved != null)
        {
            return vision == resolved;
        }

        return string.Equals(vision.visionId, violetVisionId, System.StringComparison.OrdinalIgnoreCase);
    }

    private TorchVisionDefinition ResolveVioletVision()
    {
        if (violetVision != null)
        {
            return violetVision;
        }

        TorchVisionDefinition[] loadedDefinitions = Resources.FindObjectsOfTypeAll<TorchVisionDefinition>();
        for (int i = 0; i < loadedDefinitions.Length; i++)
        {
            TorchVisionDefinition definition = loadedDefinitions[i];
            if (definition != null
                && !string.IsNullOrWhiteSpace(definition.visionId)
                && string.Equals(definition.visionId, violetVisionId, System.StringComparison.OrdinalIgnoreCase))
            {
                violetVision = definition;
                return violetVision;
            }
        }

        return null;
    }

    private static Material GetFallbackLineMaterial()
    {
        if (fallbackLineMaterial != null)
        {
            return fallbackLineMaterial;
        }

        Shader shader =
            Shader.Find("Sprites/Default") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("HDRP/Unlit") ??
            Shader.Find("Standard");

        if (shader == null)
        {
            return null;
        }

        fallbackLineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };
        return fallbackLineMaterial;
    }

    private readonly struct VioletLinkSegment
    {
        public VioletLinkSegment(TorchVisionSystem.TorchSourceInfo first, TorchVisionSystem.TorchSourceInfo second)
        {
            Start = first.Position;
            End = second.Position;
        }

        public Vector3 Start { get; }
        public Vector3 End { get; }
    }

    private readonly struct LinkKey
    {
        public LinkKey(SquadCharacterController first, SquadCharacterController second)
        {
            int firstId = first != null ? first.GetInstanceID() : 0;
            int secondId = second != null ? second.GetInstanceID() : 0;
            if (firstId <= secondId)
            {
                FirstId = firstId;
                SecondId = secondId;
            }
            else
            {
                FirstId = secondId;
                SecondId = firstId;
            }
        }

        public int FirstId { get; }
        public int SecondId { get; }
    }
}
