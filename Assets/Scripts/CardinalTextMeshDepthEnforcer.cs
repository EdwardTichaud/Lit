using System.Collections.Generic;
using Lit.Performance;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CardinalTextMeshDepthEnforcer
{
    private const string DepthShaderName = "Lit/TextMesh Depth Test";
    private const float PeriodicScanInterval = 0.25f;

    private static readonly Dictionary<Material, Material> patchedMaterials = new();
    private static Shader depthShader;
    private static CardinalTextMeshDepthScanner scanner;
    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Font.textureRebuilt -= OnFontTextureRebuilt;
        patchedMaterials.Clear();
        depthShader = null;
        scanner = null;
        initialized = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (!initialized)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Font.textureRebuilt += OnFontTextureRebuilt;
            initialized = true;
        }

        EnsureScanner();
        ApplyToSceneTextMeshes();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneTransitionProfiler.Mark($"Initialisation TextMesh debut ({scene.name})");
        ApplyToSceneTextMeshes(scene);
        SceneTransitionProfiler.Mark($"Initialisation TextMesh fin ({scene.name})");
    }

    private static void OnFontTextureRebuilt(Font rebuiltFont)
    {
        if (IsTargetFont(rebuiltFont))
        {
            ApplyToSceneTextMeshes();
        }
    }

    private static void EnsureScanner()
    {
        if (scanner != null)
        {
            return;
        }

        GameObject scannerObject = new GameObject("CardinalTextMeshDepthEnforcer")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        Object.DontDestroyOnLoad(scannerObject);
        scanner = scannerObject.AddComponent<CardinalTextMeshDepthScanner>();
    }

    private static void ApplyToSceneTextMeshes()
    {
        Shader shader = ResolveDepthShader();
        if (shader == null)
        {
            return;
        }

        TextMesh[] textMeshes = Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include);
        for (int i = 0; i < textMeshes.Length; i++)
        {
            ApplyToTextMesh(textMeshes[i], shader);
        }
    }

    private static void ApplyToSceneTextMeshes(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        Shader shader = ResolveDepthShader();
        if (shader == null)
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            if (roots[rootIndex] == null)
            {
                continue;
            }

            TextMesh[] textMeshes = roots[rootIndex].GetComponentsInChildren<TextMesh>(true);
            for (int textIndex = 0; textIndex < textMeshes.Length; textIndex++)
            {
                ApplyToTextMesh(textMeshes[textIndex], shader);
            }
        }
    }

    private static void ApplyToTextMesh(TextMesh textMesh, Shader shader)
    {
        if (textMesh == null || !IsTargetFont(textMesh.font))
        {
            return;
        }

        Renderer renderer = textMesh.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Material current = renderer.sharedMaterial;
        if (current != null && current.shader == shader)
        {
            SyncFontTexture(current, textMesh.font, textMesh.font != null ? textMesh.font.material : null);
            return;
        }

        Material source = current != null ? current : textMesh.font.material;
        Material patched = GetPatchedMaterial(source, textMesh.font, shader);
        if (patched != null)
        {
            renderer.sharedMaterial = patched;
        }
    }

    private static Material GetPatchedMaterial(Material source, Font font, Shader shader)
    {
        if (source == null)
        {
            return null;
        }

        if (!patchedMaterials.TryGetValue(source, out Material patched) || patched == null)
        {
            patched = new Material(source)
            {
                shader = shader,
                name = $"{source.name} DepthTest",
                hideFlags = HideFlags.DontSave
            };
            patchedMaterials[source] = patched;
        }

        SyncFontTexture(patched, font, source);
        return patched;
    }

    private static void SyncFontTexture(Material material, Font font, Material source)
    {
        if (material == null)
        {
            return;
        }

        Texture texture = source != null ? source.mainTexture : null;
        if (texture == null && font != null && font.material != null)
        {
            texture = font.material.mainTexture;
        }

        if (texture != null)
        {
            material.mainTexture = texture;
        }
    }

    private static Shader ResolveDepthShader()
    {
        if (depthShader == null)
        {
            depthShader = Resources.Load<Shader>("Shaders/TextMeshDepthTest");
            if (depthShader == null)
            {
                depthShader = Shader.Find(DepthShaderName);
            }
        }

        return depthShader;
    }

    private static bool IsTargetFont(Font font)
    {
        if (font == null)
        {
            return false;
        }

        string fontName = font.name;
        return fontName == "Cardinal-Alternate-Stab" ||
               fontName == "Cardinal-Alternate" ||
               fontName == "Cardinal Alternate";
    }

    private sealed class CardinalTextMeshDepthScanner : MonoBehaviour
    {
        private float nextScanTime;

        private void Update()
        {
            if (Time.unscaledTime < nextScanTime)
            {
                return;
            }

            nextScanTime = Time.unscaledTime + PeriodicScanInterval;
            ApplyToSceneTextMeshes();
        }

        private void OnDestroy()
        {
            if (scanner == this)
            {
                scanner = null;
            }
        }
    }
}
