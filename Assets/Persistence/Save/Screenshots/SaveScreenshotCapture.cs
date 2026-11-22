using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One gameplay-camera render per save, independent of frame-end coroutines and overlay UI.</summary>
public static class SaveScreenshotCapture
{
    public const int MaximumPixels = 1920 * 1080;

    public static Camera ResolveGameplayCamera()
    {
        var camera = Camera.main;
        if (camera != null) return camera;
        // LoadingScreenService temporarily disables gameplay cameras before a departure save.
        // Render the tagged camera explicitly without resuming it or selecting the loading camera.
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
            if (candidate.CompareTag("MainCamera") && candidate.cameraType == CameraType.Game && candidate.targetTexture == null)
                return candidate;
        return null;
    }

    public static Vector2Int GetSize(int width, int height, int maximumDimension)
    {
        width = Mathf.Max(1, width); height = Mathf.Max(1, height);
        int limit = Mathf.Clamp(maximumDimension, 320, 1920);
        double scale = Math.Min(1d, (double)limit / Math.Max(width, height));
        scale = Math.Min(scale, Math.Sqrt((double)MaximumPixels / ((double)width * height)));
        return new Vector2Int(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)));
    }

    public static bool TryCapture(Camera camera, int maximumDimension, out byte[] png, out string issue)
    {
        png = null; issue = null;
        if (camera == null) { issue = "No gameplay camera."; return false; }
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { issue = "No graphics device."; return false; }
        var size = GetSize(camera.pixelWidth, camera.pixelHeight, maximumDimension);
        var previousActive = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        RenderTexture target = null;
        Texture2D pixels = null;
        var hiddenCanvases = new List<Canvas>();
        try
        {
            // Disable drawing only. Do not close pause, change focus, time or UI alpha.
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                if (canvas.enabled && canvas.renderMode != RenderMode.WorldSpace)
                { hiddenCanvases.Add(canvas); canvas.enabled = false; }
            target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                { issue = "The active render pipeline cannot capture this camera."; return false; }
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.targetTexture = target;
                camera.Render();
            }
            RenderTexture.active = target;
            pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0, false);
            png = pixels.EncodeToPNG();
            if (png == null || png.Length == 0) { issue = "Empty PNG capture."; return false; }
            return true;
        }
        catch (Exception exception) { png = null; issue = exception.Message; return false; }
        finally
        {
            if (camera != null) camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            foreach (var canvas in hiddenCanvases) if (canvas != null) canvas.enabled = true;
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (pixels != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(pixels);
                else UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
