using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Some third-party Vefects HDRP shaders inherit HDRP water declarations even
/// when the project has no water camera system. Bind a neutral fallback buffer
/// so D3D12 can safely render those variants instead of skipping their draws.
/// A real water system may replace this global buffer at any time.
/// </summary>
internal static class VefectsHdrpCompatibilityService
{
    private const string WaterCameraHeightBufferName = "_WaterCameraHeightBuffer";
    private static readonly int WaterCameraHeightBufferId = Shader.PropertyToID(WaterCameraHeightBufferName);
    private static ComputeBuffer fallbackWaterCameraHeightBuffer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        EnsureBuffer();
        RenderPipelineManager.beginCameraRendering -= BindFallbackBuffer;
        RenderPipelineManager.beginCameraRendering += BindFallbackBuffer;
        Application.quitting -= ReleaseBuffer;
        Application.quitting += ReleaseBuffer;
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void InitializeEditor()
    {
        Initialize();
        AssemblyReloadEvents.beforeAssemblyReload -= ReleaseBuffer;
        AssemblyReloadEvents.beforeAssemblyReload += ReleaseBuffer;
    }
#endif

    private static void BindFallbackBuffer(ScriptableRenderContext context, Camera camera)
    {
        if (fallbackWaterCameraHeightBuffer == null)
        {
            EnsureBuffer();
        }

        if (fallbackWaterCameraHeightBuffer != null)
        {
            Shader.SetGlobalBuffer(WaterCameraHeightBufferId, fallbackWaterCameraHeightBuffer);
        }
    }

    private static void EnsureBuffer()
    {
        if (fallbackWaterCameraHeightBuffer != null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            return;
        }

        fallbackWaterCameraHeightBuffer = new ComputeBuffer(1, sizeof(float) * 4, ComputeBufferType.Structured);
        fallbackWaterCameraHeightBuffer.SetData(new[] { Vector4.zero });
        Shader.SetGlobalBuffer(WaterCameraHeightBufferId, fallbackWaterCameraHeightBuffer);
    }

    private static void ReleaseBuffer()
    {
        RenderPipelineManager.beginCameraRendering -= BindFallbackBuffer;
        if (fallbackWaterCameraHeightBuffer == null)
        {
            return;
        }

        fallbackWaterCameraHeightBuffer.Release();
        fallbackWaterCameraHeightBuffer = null;
    }
}
