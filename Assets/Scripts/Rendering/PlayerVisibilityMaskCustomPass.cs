using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[Serializable]
public sealed class PlayerVisibilityMaskCustomPass : CustomPass
{
    private const string CompositeShaderName = "Hidden/HDRP/PlayerVisibilityMaskComposite";
    private const string CompositePassName = "Player Visibility Mask Composite";

    private static readonly int PlayerVisibilityTextureId = Shader.PropertyToID("_PlayerVisibilityTexture");
    private static readonly int MaskCenterId = Shader.PropertyToID(VisibilityMaskSettings.MaskCenterPropertyName);
    private static readonly int MaskParamsId = Shader.PropertyToID(VisibilityMaskSettings.MaskParamsPropertyName);
    private static readonly int MaskDebugId = Shader.PropertyToID(VisibilityMaskSettings.MaskDebugPropertyName);

    [SerializeField, Tooltip("Layer(s) du joueur a rendre dans le buffer dedie.")]
    private LayerMask playerLayer = 1 << 6;
    [SerializeField, Tooltip("Render queues du joueur prises en compte.")]
    private RenderQueueType renderQueue = RenderQueueType.All;
    [SerializeField, Tooltip("Shader fullscreen HDRP utilise pour le composite.")]
    private Shader compositeShader;
    [SerializeField, Tooltip("Logs du pass, uniquement si le debug du masque est actif.")]
    private bool debugLogs;

    [NonSerialized] private Material compositeMaterial;
    [NonSerialized] private RTHandle playerColorBuffer;
    [NonSerialized] private int compositePassIndex = -1;
    [NonSerialized] private bool loggedMissingShader;
    [NonSerialized] private bool lastLoggedActive;
    [NonSerialized] private bool hasLoggedActiveState;
    [NonSerialized] private bool loggedFirstExecution;
    [NonSerialized] private bool loggedCompositeMaterial;
    [NonSerialized] private bool loggedMissingPlayerLayer;

    public PlayerVisibilityMaskCustomPass()
    {
        name = "Player Visibility Mask";
        targetColorBuffer = TargetBuffer.Camera;
        targetDepthBuffer = TargetBuffer.None;
        clearFlags = ClearFlag.None;
    }

    public void Configure(LayerMask targetPlayerLayer, bool enableDebugLogs, Shader overrideCompositeShader = null)
    {
        playerLayer = targetPlayerLayer;
        debugLogs = enableDebugLogs;

        if (overrideCompositeShader != compositeShader)
        {
            compositeShader = overrideCompositeShader;
            RecreateMaterial();
        }
    }

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        AllocatePlayerBuffer();
        EnsureCompositeMaterial();
    }

    protected override void Cleanup()
    {
        CoreUtils.Destroy(compositeMaterial);
        compositeMaterial = null;
        compositePassIndex = -1;

        if (playerColorBuffer != null)
        {
            RTHandles.Release(playerColorBuffer);
            playerColorBuffer = null;
        }
    }

    protected override void AggregateCullingParameters(ref ScriptableCullingParameters cullingParameters, HDCamera hdCamera)
    {
        if (playerLayer.value == 0)
        {
            return;
        }

        cullingParameters.cullingMask |= (uint)playerLayer.value;
        cullingParameters.cullingOptions &= ~CullingOptions.OcclusionCull;
    }

    protected override void Execute(CustomPassContext ctx)
    {
        Vector4 maskCenter = Shader.GetGlobalVector(MaskCenterId);
        Vector4 maskParams = Shader.GetGlobalVector(MaskParamsId);
        Vector4 maskDebug = Shader.GetGlobalVector(MaskDebugId);
        bool active = maskCenter.z > 0.5f && maskParams.w > 0.5f && maskParams.z > 0.001f;

        LogFirstExecution(ctx, active, maskCenter, maskParams, maskDebug);
        LogActiveState(active, maskCenter, maskParams, maskDebug);

        if (!active || playerLayer.value == 0)
        {
            if (active && playerLayer.value == 0 && !loggedMissingPlayerLayer)
            {
                Debug.LogWarning($"[{nameof(PlayerVisibilityMaskCustomPass)}] Player layer mask vide: aucun renderer joueur ne peut etre rendu dans le pass.");
                loggedMissingPlayerLayer = true;
            }

            return;
        }

        AllocatePlayerBuffer();
        if (playerColorBuffer == null || !EnsureCompositeMaterial())
        {
            return;
        }

        CoreUtils.SetRenderTarget(ctx.cmd, playerColorBuffer, ClearFlag.Color, Color.clear);

        var depthAlways = new RenderStateBlock(RenderStateMask.Depth)
        {
            depthState = new DepthState(false, CompareFunction.Always)
        };

        CustomPassUtils.DrawRenderers(
            ctx,
            playerLayer,
            renderQueue,
            overrideMaterial: null,
            overrideMaterialIndex: 0,
            overrideRenderState: depthAlways);

        ctx.propertyBlock.SetTexture(PlayerVisibilityTextureId, playerColorBuffer);
        HDUtils.DrawFullScreen(ctx.cmd, compositeMaterial, ctx.cameraColorBuffer, ctx.propertyBlock, compositePassIndex);
    }

    public override IEnumerable<Material> RegisterMaterialForInspector()
    {
        if (compositeMaterial != null)
        {
            yield return compositeMaterial;
        }
    }

    private void AllocatePlayerBuffer()
    {
        if (playerColorBuffer != null)
        {
            return;
        }

        playerColorBuffer = RTHandles.Alloc(
            Vector2.one,
            slices: TextureXR.slices,
            colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
            filterMode: FilterMode.Bilinear,
            wrapMode: TextureWrapMode.Clamp,
            dimension: TextureXR.dimension,
            useDynamicScale: true,
            name: "Player Visibility Mask Color");
    }

    private bool EnsureCompositeMaterial()
    {
        if (compositeMaterial != null)
        {
            return true;
        }

        Shader shader = compositeShader != null ? compositeShader : Shader.Find(CompositeShaderName);
        if (shader == null)
        {
            if (!loggedMissingShader)
            {
                Debug.LogWarning($"[{nameof(PlayerVisibilityMaskCustomPass)}] Shader introuvable: {CompositeShaderName}.");
                loggedMissingShader = true;
            }

            return false;
        }

        compositeMaterial = CoreUtils.CreateEngineMaterial(shader);
        compositePassIndex = compositeMaterial.FindPass(CompositePassName);
        if (compositePassIndex < 0)
        {
            compositePassIndex = 0;
        }

        loggedMissingShader = false;
        if ((debugLogs || Shader.GetGlobalVector(MaskDebugId).z > 0.5f) && !loggedCompositeMaterial)
        {
            Debug.Log($"[{nameof(PlayerVisibilityMaskCustomPass)}] Materiau composite pret: shader='{shader.name}', passIndex={compositePassIndex}, buffer='Player Visibility Mask Color'.");
            loggedCompositeMaterial = true;
        }

        return true;
    }

    private void RecreateMaterial()
    {
        CoreUtils.Destroy(compositeMaterial);
        compositeMaterial = null;
        compositePassIndex = -1;
    }

    private void LogFirstExecution(CustomPassContext ctx, bool active, Vector4 maskCenter, Vector4 maskParams, Vector4 maskDebug)
    {
        if (loggedFirstExecution || (!debugLogs && maskDebug.z <= 0.5f))
        {
            return;
        }

        loggedFirstExecution = true;
        string cameraName = ctx.hdCamera != null && ctx.hdCamera.camera != null ? ctx.hdCamera.camera.name : "(none)";
        Debug.Log($"[{nameof(PlayerVisibilityMaskCustomPass)}] Execute appele. camera={cameraName}, active={active}, layerMask={playerLayer.value}, center={maskCenter}, params={maskParams}.");
    }

    private void LogActiveState(bool active, Vector4 maskCenter, Vector4 maskParams, Vector4 maskDebug)
    {
        if ((!debugLogs || maskDebug.x <= 0.5f) && maskDebug.z <= 0.5f)
        {
            return;
        }

        if (hasLoggedActiveState && lastLoggedActive == active)
        {
            return;
        }

        hasLoggedActiveState = true;
        lastLoggedActive = active;
        Debug.Log($"[{nameof(PlayerVisibilityMaskCustomPass)}] Active={active}, center={maskCenter}, params={maskParams}.");
    }
}
