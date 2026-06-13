using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-4500)]
public sealed class SnowRuntimeEffects : MonoBehaviour
{
    private const string RootName = "Snow Runtime Effects";

    private static SnowRuntimeEffects instance;
    private static bool initialized;

    [Header("Binding")]
    [SerializeField, Min(0.05f)] private float characterRefreshInterval = 0.25f;

    private SnowSparkleController sparkleController;
    private GameObject currentCharacter;
    private float nextCharacterRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
        initialized = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!initialized)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            initialized = true;
        }

        EnsureInstance();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SnowRuntimeEffects controller = EnsureInstance();
        if (controller == null)
        {
            return;
        }

        controller.currentCharacter = null;
        controller.nextCharacterRefreshTime = 0f;
        controller.RefreshControlledCharacter(force: true);
    }

    private static SnowRuntimeEffects EnsureInstance()
    {
        if (!Application.isPlaying)
        {
            return null;
        }

        if (instance != null)
        {
            return instance;
        }

        instance = FindAnyObjectByType<SnowRuntimeEffects>(FindObjectsInactive.Include);
        if (instance != null)
        {
            DontDestroyOnLoad(instance.gameObject);
            instance.EnsureSparkleController();
            return instance;
        }

        GameObject controllerObject = new GameObject(RootName)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        DontDestroyOnLoad(controllerObject);
        instance = controllerObject.AddComponent<SnowRuntimeEffects>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSparkleController();
    }

    private void Update()
    {
        RefreshControlledCharacter(force: false);
    }

    private void EnsureSparkleController()
    {
        if (sparkleController != null)
        {
            return;
        }

        sparkleController = GetComponent<SnowSparkleController>();
        if (sparkleController == null)
        {
            sparkleController = gameObject.AddComponent<SnowSparkleController>();
        }
    }

    private void RefreshControlledCharacter(bool force)
    {
        if (!force && Time.time < nextCharacterRefreshTime)
        {
            return;
        }

        nextCharacterRefreshTime = Time.time + characterRefreshInterval;

        GameObject controlledCharacter = LocalPlayerUtils.GetControlledCharacter();
        if (!force && controlledCharacter == currentCharacter)
        {
            return;
        }

        currentCharacter = controlledCharacter;
        EnsureSparkleController();
        sparkleController.SetTarget(controlledCharacter != null ? controlledCharacter.transform : null);

        if (controlledCharacter == null)
        {
            return;
        }

        SnowFootprintEmitter emitter = controlledCharacter.GetComponent<SnowFootprintEmitter>();
        if (emitter == null)
        {
            emitter = controlledCharacter.AddComponent<SnowFootprintEmitter>();
        }

        emitter.SetControlledCharacterOnly(true);
    }
}

internal sealed class SnowFootprintEmitter : MonoBehaviour
{
    private const string FootprintRootName = "Snow Footprints";

    private static Mesh footprintMesh;
    private static Material footprintMaterial;
    private static Transform footprintRoot;

    [Header("Surface Detection")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0f)] private float raycastHeight = 0.9f;
    [SerializeField, Min(0.05f)] private float raycastDistance = 2.2f;
    [SerializeField, Range(0f, 1f)] private float minimumSnowAmount = 0.08f;
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormalY = 0.55f;
    [SerializeField, Range(0f, 1f)] private float fallbackSnowAmount;

    [Header("Footsteps")]
    [SerializeField] private bool controlledCharacterOnly = true;
    [SerializeField] private bool requireGrounded = true;
    [SerializeField, Min(0.1f)] private float stepDistance = 0.62f;
    [SerializeField, Min(0.01f)] private float minimumStepInterval = 0.18f;
    [SerializeField, Min(1)] private int maxFootprints = 70;
    [SerializeField, Min(0.5f)] private float footprintLifetime = 24f;
    [SerializeField, Min(0f)] private float lateralFootOffset = 0.16f;
    [SerializeField, Min(0f)] private float forwardFootOffset = 0.08f;
    [SerializeField, Min(0.01f)] private float footprintWidth = 0.17f;
    [SerializeField, Min(0.01f)] private float footprintLength = 0.42f;
    [SerializeField, Min(0f)] private float surfaceOffset = 0.012f;

    private readonly List<FootprintInstance> footprints = new List<FootprintInstance>();
    private Vector3 lastPosition;
    private float distanceSinceLastFootprint;
    private float nextFootprintTime;
    private bool leftFootNext = true;
    private bool hasLastPosition;
    private SquadCharacterController squadController;

    public void SetControlledCharacterOnly(bool value)
    {
        controlledCharacterOnly = value;
    }

    private void OnEnable()
    {
        EnsureSharedResources();
        squadController = GetComponent<SquadCharacterController>();
        lastPosition = transform.position;
        hasLastPosition = true;
    }

    private void Update()
    {
        UpdateFootprintFades();

        if (controlledCharacterOnly && !IsControlledCharacter())
        {
            hasLastPosition = false;
            return;
        }

        if (requireGrounded && squadController != null && !squadController.IsGrounded)
        {
            distanceSinceLastFootprint = 0f;
            hasLastPosition = false;
            return;
        }

        Vector3 currentPosition = transform.position;
        if (!hasLastPosition)
        {
            lastPosition = currentPosition;
            hasLastPosition = true;
            return;
        }

        Vector3 planarDelta = Vector3.ProjectOnPlane(currentPosition - lastPosition, Vector3.up);
        lastPosition = currentPosition;

        float planarDistance = planarDelta.magnitude;
        if (planarDistance < 0.015f)
        {
            return;
        }

        distanceSinceLastFootprint += planarDistance;
        if (distanceSinceLastFootprint < stepDistance || Time.time < nextFootprintTime)
        {
            return;
        }

        Vector3 movementForward = planarDelta / planarDistance;
        TryPlaceFootprint(currentPosition, movementForward);
    }

    private bool IsControlledCharacter()
    {
        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        return SnowRuntimeUtility.SharesHierarchy(controlled != null ? controlled.transform : null, transform);
    }

    private void TryPlaceFootprint(Vector3 characterPosition, Vector3 movementForward)
    {
        Vector3 forward = Vector3.ProjectOnPlane(movementForward.sqrMagnitude > 0.0001f ? movementForward : transform.forward, Vector3.up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float side = leftFootNext ? -1f : 1f;
        Vector3 footprintCenter = characterPosition + forward * forwardFootOffset + right * (lateralFootOffset * side);
        Vector3 rayOrigin = footprintCenter + Vector3.up * raycastHeight;

        if (!SnowRuntimeUtility.TrySampleSnowSurface(
                rayOrigin,
                raycastDistance,
                groundMask,
                minimumGroundNormalY,
                fallbackSnowAmount,
                transform,
                out SnowSurfaceSample sample))
        {
            return;
        }

        if (sample.SnowAmount < minimumSnowAmount)
        {
            return;
        }

        Vector3 surfaceForward = Vector3.ProjectOnPlane(forward, sample.Normal);
        if (surfaceForward.sqrMagnitude <= 0.0001f)
        {
            surfaceForward = Vector3.ProjectOnPlane(transform.forward, sample.Normal);
        }

        if (surfaceForward.sqrMagnitude <= 0.0001f)
        {
            surfaceForward = Vector3.forward;
        }

        surfaceForward.Normalize();
        surfaceForward = Quaternion.AngleAxis(leftFootNext ? -4f : 4f, sample.Normal) * surfaceForward;

        float snowT = Mathf.InverseLerp(minimumSnowAmount, 1f, sample.SnowAmount);
        Color footprintColor = Color.Lerp(
            new Color(0.20f, 0.25f, 0.30f, 0.12f),
            new Color(0.08f, 0.12f, 0.16f, 0.42f),
            snowT);
        float width = footprintWidth * Mathf.Lerp(0.75f, 1.08f, snowT);
        float length = footprintLength * Mathf.Lerp(0.85f, 1.12f, snowT);

        SpawnFootprint(
            sample.Point + sample.Normal * surfaceOffset,
            Quaternion.LookRotation(surfaceForward, sample.Normal),
            new Vector3(width, 1f, length),
            footprintColor);

        distanceSinceLastFootprint = 0f;
        nextFootprintTime = Time.time + minimumStepInterval;
        leftFootNext = !leftFootNext;
    }

    private void SpawnFootprint(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
    {
        FootprintInstance footprint = GetReusableFootprint();
        footprint.Root.transform.SetPositionAndRotation(position, rotation);
        footprint.Root.transform.localScale = scale;
        footprint.Root.SetActive(true);
        footprint.SpawnTime = Time.time;
        footprint.Lifetime = footprintLifetime;
        footprint.BaseColor = color;
        footprint.ApplyColor(color);
    }

    private FootprintInstance GetReusableFootprint()
    {
        for (int i = 0; i < footprints.Count; i++)
        {
            if (!footprints[i].Root.activeSelf)
            {
                return footprints[i];
            }
        }

        if (footprints.Count >= maxFootprints)
        {
            FootprintInstance oldest = footprints[0];
            float oldestAge = Time.time - oldest.SpawnTime;
            for (int i = 1; i < footprints.Count; i++)
            {
                float age = Time.time - footprints[i].SpawnTime;
                if (age > oldestAge)
                {
                    oldest = footprints[i];
                    oldestAge = age;
                }
            }

            return oldest;
        }

        FootprintInstance created = CreateFootprintInstance();
        footprints.Add(created);
        return created;
    }

    private FootprintInstance CreateFootprintInstance()
    {
        EnsureSharedResources();
        Transform parent = GetFootprintRoot();
        GameObject footprintObject = new GameObject("Snow Footprint")
        {
            hideFlags = HideFlags.DontSave
        };
        footprintObject.transform.SetParent(parent, worldPositionStays: false);
        footprintObject.SetActive(false);

        MeshFilter filter = footprintObject.AddComponent<MeshFilter>();
        filter.sharedMesh = footprintMesh;

        MeshRenderer renderer = footprintObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = footprintMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return new FootprintInstance(footprintObject, renderer);
    }

    private void UpdateFootprintFades()
    {
        for (int i = 0; i < footprints.Count; i++)
        {
            FootprintInstance footprint = footprints[i];
            if (!footprint.Root.activeSelf)
            {
                continue;
            }

            float age = Time.time - footprint.SpawnTime;
            if (age >= footprint.Lifetime)
            {
                footprint.Root.SetActive(false);
                continue;
            }

            float fade = 1f - Mathf.Clamp01(age / footprint.Lifetime);
            Color color = footprint.BaseColor;
            color.a *= fade * fade;
            footprint.ApplyColor(color);
        }
    }

    private static Transform GetFootprintRoot()
    {
        if (footprintRoot != null)
        {
            return footprintRoot;
        }

        GameObject root = new GameObject(FootprintRootName)
        {
            hideFlags = HideFlags.DontSave
        };
        footprintRoot = root.transform;
        return footprintRoot;
    }

    private static void EnsureSharedResources()
    {
        if (footprintMesh == null)
        {
            footprintMesh = SnowRuntimeUtility.CreateEllipseMesh("Runtime Snow Footprint Mesh", segments: 18);
        }

        if (footprintMaterial == null)
        {
            footprintMaterial = SnowRuntimeUtility.CreateTransparentRuntimeMaterial(
                "Runtime Snow Footprint Material",
                new Color(0.10f, 0.14f, 0.18f, 0.35f),
                additive: false);
        }
    }

    private sealed class FootprintInstance
    {
        public readonly GameObject Root;
        private readonly MeshRenderer renderer;
        private readonly MaterialPropertyBlock propertyBlock;

        public float SpawnTime;
        public float Lifetime;
        public Color BaseColor;

        public FootprintInstance(GameObject root, MeshRenderer renderer)
        {
            Root = root;
            this.renderer = renderer;
            propertyBlock = new MaterialPropertyBlock();
        }

        public void ApplyColor(Color color)
        {
            if (renderer == null)
            {
                return;
            }

            propertyBlock.Clear();
            SnowRuntimeUtility.SetMaterialColor(propertyBlock, color);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}

internal sealed class SnowSparkleController : MonoBehaviour
{
    [Header("Surface Detection")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0f)] private float raycastHeight = 1.3f;
    [SerializeField, Min(0.05f)] private float raycastDistance = 3f;
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormalY = 0.55f;
    [SerializeField, Range(0f, 1f)] private float fallbackSnowAmount;

    [Header("Particles")]
    [SerializeField, Min(0f)] private float followHeight = 0.4f;
    [SerializeField, Min(0.1f)] private float shapeRadius = 3.5f;
    [SerializeField, Min(0.1f)] private float shapeHeight = 0.8f;
    [SerializeField, Min(0f)] private float maxEmissionRate = 46f;
    [SerializeField, Min(0f)] private float amountSmoothTime = 0.2f;

    private Transform target;
    private ParticleSystem sparkleSystem;
    private ParticleSystem.EmissionModule emissionModule;
    private ParticleSystem.MainModule mainModule;
    private ParticleSystem.ShapeModule shapeModule;
    private float smoothedSnowAmount;
    private float snowAmountVelocity;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target == null && sparkleSystem != null)
        {
            ParticleSystem.EmissionModule emission = sparkleSystem.emission;
            emission.rateOverTime = 0f;
        }
    }

    private void Awake()
    {
        EnsureParticleSystem();
    }

    private void OnEnable()
    {
        EnsureParticleSystem();
        if (!sparkleSystem.isPlaying)
        {
            sparkleSystem.Play();
        }
    }

    private void Update()
    {
        EnsureParticleSystem();

        float targetAmount = SampleTargetSnowAmount();
        smoothedSnowAmount = Mathf.SmoothDamp(
            smoothedSnowAmount,
            targetAmount,
            ref snowAmountVelocity,
            Mathf.Max(0.01f, amountSmoothTime));

        ApplySnowAmount(smoothedSnowAmount);
    }

    private float SampleTargetSnowAmount()
    {
        if (target == null)
        {
            return 0f;
        }

        Vector3 targetPosition = target.position;
        transform.position = targetPosition + Vector3.up * followHeight;

        if (!SnowRuntimeUtility.TrySampleSnowSurface(
                targetPosition + Vector3.up * raycastHeight,
                raycastDistance,
                groundMask,
                minimumGroundNormalY,
                fallbackSnowAmount,
                target,
                out SnowSurfaceSample sample))
        {
            return 0f;
        }

        return sample.SnowAmount;
    }

    private void ApplySnowAmount(float snowAmount)
    {
        snowAmount = Mathf.Clamp01(snowAmount);
        float visibleAmount = snowAmount;
        float emissionRate = Mathf.Lerp(0f, maxEmissionRate, visibleAmount);
        float alpha = Mathf.Lerp(0.015f, 0.55f, visibleAmount);
        float intensity = Mathf.Lerp(0.6f, 2.8f, visibleAmount);
        Color startColor = new Color(intensity, intensity, intensity * 1.05f, alpha);

        emissionModule.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate);
        mainModule.startColor = new ParticleSystem.MinMaxGradient(startColor);

        if (emissionRate <= 0.05f)
        {
            if (sparkleSystem.isPlaying)
            {
                sparkleSystem.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        else if (!sparkleSystem.isPlaying)
        {
            sparkleSystem.Play();
        }
    }

    private void EnsureParticleSystem()
    {
        if (sparkleSystem != null)
        {
            return;
        }

        GameObject particleObject = new GameObject("Snow Sparkles")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        particleObject.transform.SetParent(transform, worldPositionStays: false);
        particleObject.transform.localPosition = Vector3.zero;
        particleObject.transform.localRotation = Quaternion.identity;
        particleObject.transform.localScale = Vector3.one;

        sparkleSystem = particleObject.AddComponent<ParticleSystem>();
        mainModule = sparkleSystem.main;
        mainModule.loop = true;
        mainModule.playOnAwake = true;
        mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
        mainModule.maxParticles = 220;
        mainModule.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 1.4f);
        mainModule.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.18f);
        mainModule.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.045f);
        mainModule.gravityModifier = 0f;

        emissionModule = sparkleSystem.emission;
        emissionModule.rateOverTime = 0f;

        shapeModule = sparkleSystem.shape;
        shapeModule.enabled = true;
        shapeModule.shapeType = ParticleSystemShapeType.Box;
        shapeModule.scale = new Vector3(shapeRadius * 2f, shapeHeight, shapeRadius * 2f);
        shapeModule.position = Vector3.up * (shapeHeight * 0.5f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = sparkleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient alphaGradient = new Gradient();
        alphaGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.55f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.25f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGradient);

        ParticleSystemRenderer renderer = sparkleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = SnowRuntimeUtility.CreateTransparentRuntimeMaterial(
            "Runtime Snow Sparkle Material",
            Color.white,
            additive: true);
        renderer.sortingFudge = 0.25f;
    }
}

internal struct SnowSurfaceSample
{
    public Vector3 Point;
    public Vector3 Normal;
    public float SnowAmount;
}

internal static class SnowRuntimeUtility
{
    private static readonly int SnowAmountId = Shader.PropertyToID("_SnowAmount");
    private static readonly int SnowTopThresholdId = Shader.PropertyToID("_SnowTopThreshold");
    private static readonly int SnowBlendSoftnessId = Shader.PropertyToID("_SnowBlendSoftness");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int SurfaceTypeId = Shader.PropertyToID("_SurfaceType");
    private static readonly int BlendModeId = Shader.PropertyToID("_BlendMode");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
    private static readonly int CullModeId = Shader.PropertyToID("_CullMode");

    private static readonly RaycastHit[] RaycastHits = new RaycastHit[12];
    private static readonly List<Renderer> RendererBuffer = new List<Renderer>(8);

    public static bool TrySampleSnowSurface(
        Vector3 rayOrigin,
        float raycastDistance,
        LayerMask groundMask,
        float minimumGroundNormalY,
        float fallbackSnowAmount,
        Transform ignoredRoot,
        out SnowSurfaceSample sample)
    {
        sample = default;

        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            RaycastHits,
            raycastDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            return false;
        }

        bool found = false;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = RaycastHits[i];
            if (hit.collider == null || hit.distance >= closestDistance)
            {
                continue;
            }

            Transform hitTransform = hit.collider.transform;
            if (SharesHierarchy(ignoredRoot, hitTransform))
            {
                continue;
            }

            if (hit.normal.y < minimumGroundNormalY)
            {
                continue;
            }

            if (!TryResolveSnowAmount(hit, fallbackSnowAmount, out float snowAmount))
            {
                continue;
            }

            closestDistance = hit.distance;
            sample = new SnowSurfaceSample
            {
                Point = hit.point,
                Normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up,
                SnowAmount = Mathf.Clamp01(snowAmount)
            };
            found = true;
        }

        return found;
    }

    public static bool SharesHierarchy(Transform a, Transform b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        return a == b || a.IsChildOf(b) || b.IsChildOf(a);
    }

    public static Mesh CreateEllipseMesh(string meshName, int segments)
    {
        segments = Mathf.Max(8, segments);
        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            float x = Mathf.Cos(angle) * 0.5f;
            float z = Mathf.Sin(angle) * 0.5f;
            vertices[i + 1] = new Vector3(x, 0f, z);
            uvs[i + 1] = new Vector2(x + 0.5f, z + 0.5f);

            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i == segments - 1 ? 1 : i + 2;
        }

        Mesh mesh = new Mesh
        {
            name = meshName,
            hideFlags = HideFlags.DontSave
        };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Material CreateTransparentRuntimeMaterial(string materialName, Color color, bool additive)
    {
        Shader shader = FindFirstShader(
            "HDRP/Unlit",
            "Universal Render Pipeline/Unlit",
            "Unlit/Transparent",
            "Sprites/Default",
            "Standard");

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.DontSave,
            renderQueue = (int)RenderQueue.Transparent
        };

        ApplyTransparentMaterialSettings(material, additive);
        SetMaterialColor(material, color);
        return material;
    }

    public static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        SetColorIfPresent(material, BaseColorId, color);
        SetColorIfPresent(material, UnlitColorId, color);
        SetColorIfPresent(material, ColorId, color);
        SetColorIfPresent(material, TintColorId, color);
    }

    public static void SetMaterialColor(MaterialPropertyBlock block, Color color)
    {
        if (block == null)
        {
            return;
        }

        block.SetColor(BaseColorId, color);
        block.SetColor(UnlitColorId, color);
        block.SetColor(ColorId, color);
        block.SetColor(TintColorId, color);
    }

    private static bool TryResolveSnowAmount(RaycastHit hit, float fallbackSnowAmount, out float snowAmount)
    {
        snowAmount = Mathf.Clamp01(fallbackSnowAmount);
        bool foundSnowProperty = false;
        float normalY = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized.y : 1f;

        Terrain terrain = hit.collider.GetComponent<Terrain>();
        if (terrain != null && TryEvaluateMaterialSnowAmount(terrain.materialTemplate, normalY, out float terrainSnowAmount))
        {
            snowAmount = Mathf.Max(snowAmount, terrainSnowAmount);
            foundSnowProperty = true;
        }

        RendererBuffer.Clear();
        Renderer directRenderer = hit.collider.GetComponent<Renderer>();
        if (directRenderer != null)
        {
            RendererBuffer.Add(directRenderer);
        }

        Renderer parentRenderer = hit.collider.GetComponentInParent<Renderer>();
        if (parentRenderer != null && !RendererBuffer.Contains(parentRenderer))
        {
            RendererBuffer.Add(parentRenderer);
        }

        Renderer childRenderer = hit.collider.GetComponentInChildren<Renderer>();
        if (childRenderer != null && !RendererBuffer.Contains(childRenderer))
        {
            RendererBuffer.Add(childRenderer);
        }

        for (int rendererIndex = 0; rendererIndex < RendererBuffer.Count; rendererIndex++)
        {
            Renderer renderer = RendererBuffer[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (!TryEvaluateMaterialSnowAmount(material, normalY, out float materialSnowAmount))
                {
                    continue;
                }

                snowAmount = Mathf.Max(snowAmount, materialSnowAmount);
                foundSnowProperty = true;
            }
        }

        return foundSnowProperty || snowAmount > 0f;
    }

    private static bool TryEvaluateMaterialSnowAmount(Material material, float normalY, out float snowAmount)
    {
        snowAmount = 0f;
        if (material == null || !material.HasProperty(SnowAmountId))
        {
            return false;
        }

        snowAmount = Mathf.Clamp01(material.GetFloat(SnowAmountId));
        if (material.HasProperty(SnowTopThresholdId))
        {
            float topThreshold = material.GetFloat(SnowTopThresholdId);
            float blendSoftness = material.HasProperty(SnowBlendSoftnessId)
                ? Mathf.Max(0.0001f, material.GetFloat(SnowBlendSoftnessId))
                : 0.25f;
            snowAmount *= SmoothStep(topThreshold, topThreshold + blendSoftness, normalY);
        }

        return true;
    }

    private static Shader FindFirstShader(params string[] shaderNames)
    {
        for (int i = 0; i < shaderNames.Length; i++)
        {
            Shader shader = Shader.Find(shaderNames[i]);
            if (shader != null)
            {
                return shader;
            }
        }

        return Shader.Find("Standard");
    }

    private static void ApplyTransparentMaterialSettings(Material material, bool additive)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(SurfaceTypeId))
        {
            material.SetFloat(SurfaceTypeId, 1f);
        }

        if (material.HasProperty(BlendModeId))
        {
            material.SetFloat(BlendModeId, additive ? 1f : 0f);
        }

        if (material.HasProperty(SrcBlendId))
        {
            material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty(DstBlendId))
        {
            material.SetFloat(DstBlendId, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty(ZWriteId))
        {
            material.SetFloat(ZWriteId, 0f);
        }

        if (material.HasProperty(CullModeId))
        {
            material.SetFloat(CullModeId, (float)CullMode.Off);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword(additive ? "_BLENDMODE_ADD" : "_BLENDMODE_ALPHA");
    }

    private static void SetColorIfPresent(Material material, int propertyId, Color color)
    {
        if (material.HasProperty(propertyId))
        {
            material.SetColor(propertyId, color);
        }
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float denominator = Mathf.Max(0.0001f, edge1 - edge0);
        float t = Mathf.Clamp01((value - edge0) / denominator);
        return t * t * (3f - 2f * t);
    }
}
