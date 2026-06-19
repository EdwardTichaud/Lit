using UnityEngine;

[DefaultExecutionOrder(-4500)]
[DisallowMultipleComponent]
public sealed class SnowManager : MonoBehaviour
{
    public enum FootprintShape
    {
        Ellipse,
        Sole,
        Boot,
        Rectangle,
        CustomMesh
    }

    [System.Serializable]
    public sealed class SurfaceSamplingSettings
    {
        public LayerMask GroundMask = ~0;
        [Min(0f)] public float RaycastHeight = 0.9f;
        [Min(0.05f)] public float RaycastDistance = 2.2f;
        [Range(0f, 1f)] public float MinimumGroundNormalY = 0.55f;
        [Range(0f, 1f)] public float FallbackSnowAmount;
    }

    [System.Serializable]
    public sealed class ParticleSettings
    {
        public bool Enabled = true;
        public Material Material;
        [Min(1)] public int MaxParticles = 220;
        [Min(0f)] public float FollowHeight = 0.4f;
        [Min(0.1f)] public float ShapeRadius = 3.5f;
        [Min(0.1f)] public float ShapeHeight = 0.8f;
        [Min(0f)] public float MaxEmissionRate = 46f;
        [Min(0f)] public float AmountSmoothTime = 0.2f;
        [Min(0.01f)] public float MinLifetime = 0.35f;
        [Min(0.01f)] public float MaxLifetime = 1.4f;
        [Min(0f)] public float MinStartSpeed = 0.01f;
        [Min(0f)] public float MaxStartSpeed = 0.18f;
        [Min(0.001f)] public float MinStartSize = 0.012f;
        [Min(0.001f)] public float MaxStartSize = 0.045f;
        [Range(0f, 1f)] public float MinAlpha = 0.015f;
        [Range(0f, 1f)] public float MaxAlpha = 0.55f;
        [Min(0f)] public float MinIntensity = 0.6f;
        [Min(0f)] public float MaxIntensity = 2.8f;
    }

    [System.Serializable]
    public sealed class FootprintSettings
    {
        public bool Enabled = true;
        public Material Material;
        public FootprintShape Shape = FootprintShape.Ellipse;
        public Mesh CustomMesh;
        [Range(0f, 1f)] public float MinimumSnowAmount = 0.08f;
        public bool ControlledCharacterOnly = true;
        public bool RequireGrounded = true;
        [Min(0.1f)] public float StepDistance = 0.62f;
        [Min(0.01f)] public float MinimumStepInterval = 0.18f;
        [Min(1)] public int MaxFootprints = 70;
        [Min(0.5f)] public float Lifetime = 24f;
        [Min(0f)] public float LateralFootOffset = 0.16f;
        [Min(0f)] public float ForwardFootOffset = 0.08f;
        [Min(0.01f)] public float Width = 0.17f;
        [Min(0.01f)] public float Length = 0.42f;
        [Min(0f)] public float SurfaceOffset = 0.012f;
        public Color LowSnowColor = new Color(0.20f, 0.25f, 0.30f, 0.12f);
        public Color HighSnowColor = new Color(0.08f, 0.12f, 0.16f, 0.42f);
    }

    private const string SparklesObjectName = "Snow Sparkle Controller";
    private const string FootprintsRootName = "Snow Footprints";

    private static SnowManager instance;

    [Header("Binding")]
    [SerializeField] private bool effectsEnabled = true;
    [SerializeField] private bool autoBindControlledCharacter = true;
    [SerializeField] private Transform explicitTarget;
    [SerializeField, Min(0.05f)] private float characterRefreshInterval = 0.25f;

    [Header("Surface Detection")]
    [SerializeField] private SurfaceSamplingSettings particleSurface = new SurfaceSamplingSettings
    {
        RaycastHeight = 1.3f,
        RaycastDistance = 3f
    };
    [SerializeField] private SurfaceSamplingSettings footprintSurface = new SurfaceSamplingSettings();

    [Header("Particles")]
    [SerializeField] private ParticleSettings particles = new ParticleSettings();

    [Header("Footprints")]
    [SerializeField] private FootprintSettings footprints = new FootprintSettings();

    private SnowSparkleController sparkleController;
    private SnowFootprintEmitter currentEmitter;
    private GameObject currentCharacter;
    private Transform footprintRoot;
    private Material runtimeParticleMaterial;
    private Material runtimeFootprintMaterial;
    private Mesh generatedFootprintMesh;
    private FootprintShape generatedFootprintShape;
    private float nextCharacterRefreshTime;

    public bool EffectsEnabled => effectsEnabled;
    public bool AutoBindControlledCharacter => autoBindControlledCharacter;
    public bool ParticlesEnabled => effectsEnabled && particles.Enabled;
    public bool FootprintsEnabled => effectsEnabled && footprints.Enabled;
    public SurfaceSamplingSettings ParticleSurface => particleSurface;
    public SurfaceSamplingSettings FootprintSurface => footprintSurface;
    public ParticleSettings Particles => particles;
    public FootprintSettings Footprints => footprints;

    internal static SnowManager Instance => instance;

    internal static void RefreshActiveManagers(bool force)
    {
        SnowManager[] managers = FindObjectsByType<SnowManager>(FindObjectsInactive.Exclude);
        for (int i = 0; i < managers.Length; i++)
        {
            managers[i].RefreshControlledCharacter(force);
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("SnowManager: plusieurs managers actifs dans la scene. Le premier reste utilise.", this);
        }
        else
        {
            instance = this;
        }

        EnsureControllers();
        ApplySettingsToControllers();
    }

    private void OnEnable()
    {
        if (instance == null)
        {
            instance = this;
        }

        EnsureControllers();
        ApplySettingsToControllers();
        RefreshControlledCharacter(force: true);
    }

    private void OnDisable()
    {
        if (sparkleController != null)
        {
            sparkleController.ApplySettings(null);
        }

        if (currentEmitter != null)
        {
            currentEmitter.ApplySettings(null);
            currentEmitter = null;
        }

        currentCharacter = null;

        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    private void Update()
    {
        EnsureControllers();
        RefreshControlledCharacter(force: false);
    }

    public void ReapplySettings()
    {
        ClampSettings();
        ApplySettingsToControllers();
        RefreshControlledCharacter(force: true);
    }

    internal Material GetParticleMaterial()
    {
        if (particles.Material != null)
        {
            return particles.Material;
        }

        if (runtimeParticleMaterial == null)
        {
            runtimeParticleMaterial = SnowRuntimeUtility.CreateTransparentRuntimeMaterial(
                "Runtime Snow Sparkle Material",
                Color.white,
                additive: true);
        }

        return runtimeParticleMaterial;
    }

    internal Material GetFootprintMaterial()
    {
        if (footprints.Material != null)
        {
            return footprints.Material;
        }

        if (runtimeFootprintMaterial == null)
        {
            runtimeFootprintMaterial = SnowRuntimeUtility.CreateTransparentRuntimeMaterial(
                "Runtime Snow Footprint Material",
                new Color(0.10f, 0.14f, 0.18f, 0.35f),
                additive: false);
        }

        return runtimeFootprintMaterial;
    }

    internal Mesh GetFootprintMesh()
    {
        if (footprints.Shape == FootprintShape.CustomMesh && footprints.CustomMesh != null)
        {
            return footprints.CustomMesh;
        }

        FootprintShape shape = footprints.Shape == FootprintShape.CustomMesh
            ? FootprintShape.Ellipse
            : footprints.Shape;

        if (generatedFootprintMesh != null && generatedFootprintShape == shape)
        {
            return generatedFootprintMesh;
        }

        generatedFootprintShape = shape;
        generatedFootprintMesh = CreateFootprintMesh(shape);
        return generatedFootprintMesh;
    }

    internal Transform GetFootprintRoot()
    {
        if (footprintRoot != null)
        {
            return footprintRoot;
        }

        Transform existing = transform.Find(FootprintsRootName);
        if (existing != null)
        {
            footprintRoot = existing;
            return footprintRoot;
        }

        GameObject root = new GameObject(FootprintsRootName);
        root.transform.SetParent(transform, worldPositionStays: false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        footprintRoot = root.transform;
        return footprintRoot;
    }

    private void EnsureControllers()
    {
        if (sparkleController == null)
        {
            sparkleController = GetComponentInChildren<SnowSparkleController>(true);
        }

        if (sparkleController == null)
        {
            GameObject sparkleObject = new GameObject(SparklesObjectName);
            sparkleObject.transform.SetParent(transform, worldPositionStays: false);
            sparkleObject.transform.localPosition = Vector3.zero;
            sparkleObject.transform.localRotation = Quaternion.identity;
            sparkleObject.transform.localScale = Vector3.one;
            sparkleController = sparkleObject.AddComponent<SnowSparkleController>();
        }
    }

    private void ApplySettingsToControllers()
    {
        if (sparkleController != null)
        {
            sparkleController.ApplySettings(this);
        }

        if (currentEmitter != null)
        {
            currentEmitter.ApplySettings(this);
        }
    }

    private void RefreshControlledCharacter(bool force)
    {
        if (!force && Time.time < nextCharacterRefreshTime)
        {
            return;
        }

        nextCharacterRefreshTime = Time.time + characterRefreshInterval;

        Transform target = ResolveTarget();
        GameObject controlledCharacter = target != null ? target.gameObject : null;
        if (!force && controlledCharacter == currentCharacter)
        {
            return;
        }

        SnowFootprintEmitter previousEmitter = currentEmitter;
        currentCharacter = controlledCharacter;
        EnsureControllers();
        sparkleController.SetTarget(target);

        SnowFootprintEmitter nextEmitter = null;
        if (controlledCharacter == null)
        {
            if (previousEmitter != null)
            {
                previousEmitter.ApplySettings(null);
            }

            currentEmitter = null;
            return;
        }

        nextEmitter = controlledCharacter.GetComponent<SnowFootprintEmitter>();
        if (nextEmitter == null)
        {
            nextEmitter = controlledCharacter.AddComponent<SnowFootprintEmitter>();
        }

        if (previousEmitter != null && previousEmitter != nextEmitter)
        {
            previousEmitter.ApplySettings(null);
        }

        currentEmitter = nextEmitter;
        currentEmitter.ApplySettings(this);
    }

    private Transform ResolveTarget()
    {
        if (!autoBindControlledCharacter)
        {
            return explicitTarget;
        }

        GameObject controlledCharacter = LocalPlayerUtils.GetControlledCharacter();
        return controlledCharacter != null ? controlledCharacter.transform : explicitTarget;
    }

    private void ClampSettings()
    {
        characterRefreshInterval = Mathf.Max(0.05f, characterRefreshInterval);
        ClampSurfaceSettings(particleSurface);
        ClampSurfaceSettings(footprintSurface);

        particles.MaxParticles = Mathf.Max(1, particles.MaxParticles);
        particles.ShapeRadius = Mathf.Max(0.1f, particles.ShapeRadius);
        particles.ShapeHeight = Mathf.Max(0.1f, particles.ShapeHeight);
        particles.MaxEmissionRate = Mathf.Max(0f, particles.MaxEmissionRate);
        particles.AmountSmoothTime = Mathf.Max(0f, particles.AmountSmoothTime);
        particles.MinLifetime = Mathf.Max(0.01f, particles.MinLifetime);
        particles.MaxLifetime = Mathf.Max(particles.MinLifetime, particles.MaxLifetime);
        particles.MinStartSpeed = Mathf.Max(0f, particles.MinStartSpeed);
        particles.MaxStartSpeed = Mathf.Max(particles.MinStartSpeed, particles.MaxStartSpeed);
        particles.MinStartSize = Mathf.Max(0.001f, particles.MinStartSize);
        particles.MaxStartSize = Mathf.Max(particles.MinStartSize, particles.MaxStartSize);
        particles.MaxAlpha = Mathf.Max(particles.MinAlpha, particles.MaxAlpha);
        particles.MaxIntensity = Mathf.Max(particles.MinIntensity, particles.MaxIntensity);

        footprints.StepDistance = Mathf.Max(0.1f, footprints.StepDistance);
        footprints.MinimumStepInterval = Mathf.Max(0.01f, footprints.MinimumStepInterval);
        footprints.MaxFootprints = Mathf.Max(1, footprints.MaxFootprints);
        footprints.Lifetime = Mathf.Max(0.5f, footprints.Lifetime);
        footprints.Width = Mathf.Max(0.01f, footprints.Width);
        footprints.Length = Mathf.Max(0.01f, footprints.Length);
        footprints.SurfaceOffset = Mathf.Max(0f, footprints.SurfaceOffset);
    }

    private static void ClampSurfaceSettings(SurfaceSamplingSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        settings.RaycastHeight = Mathf.Max(0f, settings.RaycastHeight);
        settings.RaycastDistance = Mathf.Max(0.05f, settings.RaycastDistance);
        settings.MinimumGroundNormalY = Mathf.Clamp01(settings.MinimumGroundNormalY);
        settings.FallbackSnowAmount = Mathf.Clamp01(settings.FallbackSnowAmount);
    }

    private static Mesh CreateFootprintMesh(FootprintShape shape)
    {
        switch (shape)
        {
            case FootprintShape.Sole:
                return CreatePolygonMesh(
                    "Runtime Snow Footprint Sole Mesh",
                    new[]
                    {
                        new Vector2(0f, 0.5f),
                        new Vector2(0.18f, 0.44f),
                        new Vector2(0.25f, 0.25f),
                        new Vector2(0.22f, -0.24f),
                        new Vector2(0.12f, -0.48f),
                        new Vector2(-0.12f, -0.48f),
                        new Vector2(-0.22f, -0.24f),
                        new Vector2(-0.25f, 0.25f),
                        new Vector2(-0.18f, 0.44f)
                    });

            case FootprintShape.Boot:
                return CreatePolygonMesh(
                    "Runtime Snow Footprint Boot Mesh",
                    new[]
                    {
                        new Vector2(0f, 0.5f),
                        new Vector2(0.23f, 0.42f),
                        new Vector2(0.28f, 0.18f),
                        new Vector2(0.21f, -0.06f),
                        new Vector2(0.18f, -0.38f),
                        new Vector2(0.11f, -0.5f),
                        new Vector2(-0.11f, -0.5f),
                        new Vector2(-0.18f, -0.38f),
                        new Vector2(-0.21f, -0.06f),
                        new Vector2(-0.28f, 0.18f),
                        new Vector2(-0.23f, 0.42f)
                    });

            case FootprintShape.Rectangle:
                return CreatePolygonMesh(
                    "Runtime Snow Footprint Rectangle Mesh",
                    new[]
                    {
                        new Vector2(-0.24f, -0.5f),
                        new Vector2(0.24f, -0.5f),
                        new Vector2(0.24f, 0.5f),
                        new Vector2(-0.24f, 0.5f)
                    });

            default:
                return SnowRuntimeUtility.CreateEllipseMesh("Runtime Snow Footprint Ellipse Mesh", segments: 18);
        }
    }

    private static Mesh CreatePolygonMesh(string meshName, Vector2[] outline)
    {
        Vector3[] vertices = new Vector3[outline.Length + 1];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[outline.Length * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 point = outline[i];
            vertices[i + 1] = new Vector3(point.x, 0f, point.y);
            uvs[i + 1] = new Vector2(point.x + 0.5f, point.y + 0.5f);

            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i == outline.Length - 1 ? 1 : i + 2;
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
}
