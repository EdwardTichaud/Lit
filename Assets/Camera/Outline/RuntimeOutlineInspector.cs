using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>Inspector controls for the interaction outline on the persistent session's OutlineManager.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CustomPassVolume))]
[AddComponentMenu("Lit/Outlines/Gestion des outlines")]
public sealed class RuntimeOutlineInspector : MonoBehaviour
{
    [SerializeField, Tooltip("Affiche les contours des interactifs. La selection et les interactions restent actives.")]
    private bool showOutlines = true;
    [SerializeField, ColorUsage(true), Tooltip("Couleur du contour ; alpha = opacite.")]
    private Color outlineColor = Color.white;
    [SerializeField, Range(0.5f, 12f), Tooltip("Epaisseur en pixels du rendu. Valeur initiale du projet : 4.")]
    private float thickness = 4f;
    [Header("Epaisseur selon la distance")]
    [SerializeField, Tooltip("Ajuste l'epaisseur du contour selon la distance entre la camera active et l'objet interactif selectionne.")]
    private bool scaleThicknessWithCameraDistance = true;
    [SerializeField, Min(0.1f), Tooltip("Distance a laquelle le contour conserve son epaisseur de proximite.")]
    private float nearDistance = 3f;
    [SerializeField, Min(0.1f), Tooltip("Distance a laquelle le contour atteint son epaisseur minimale.")]
    private float farDistance = 16f;
    [SerializeField, Range(0.5f, 12f), Tooltip("Epaisseur du contour a proximite.")]
    private float nearThickness = 4f;
    [SerializeField, Range(0.5f, 12f), Tooltip("Epaisseur du contour a distance, notamment en camera tactique.")]
    private float farThickness = 1.5f;
    [SerializeField, Range(0.001f, 1f), Tooltip("Pixels sous ce seuil d'opacite exclus du contour. Augmenter resserre le contour des particules diffuses.")]
    private float alphaThreshold = 0.1f;

    private FullScreenCustomPass outlinePass;
    private Material originalMaterial;
    private Material runtimeMaterial;
    private bool originalPassEnabled;

    public CustomPassVolume Volume => GetComponent<CustomPassVolume>();
    public bool IsConfigured => runtimeMaterial != null;

    private void OnEnable()
    {
        foreach (var pass in Volume.customPasses)
        {
            if (!(pass is FullScreenCustomPass fullscreen) || fullscreen.fullscreenPassMaterial == null)
                continue;
            Material material = fullscreen.fullscreenPassMaterial;
            if (!material.HasProperty("_OutlineColor") || !material.HasProperty("_Thickness")) continue;
            outlinePass = fullscreen;
            originalMaterial = material;
            originalPassEnabled = fullscreen.enabled;
            runtimeMaterial = new Material(material) { name = material.name + " (Session)", hideFlags = HideFlags.DontSave };
            outlinePass.fullscreenPassMaterial = runtimeMaterial;
            ApplySettings();
            return;
        }
        Debug.LogWarning("OutlineManager : passe fullscreen de contour introuvable dans le Custom Pass Volume.", this);
    }

    private void Update() => ApplySettings();

    private void ApplySettings()
    {
        if (runtimeMaterial == null || outlinePass == null) return;
        outlinePass.enabled = showOutlines;
        runtimeMaterial.SetColor("_OutlineColor", outlineColor);
        runtimeMaterial.SetFloat("_Thickness", GetEffectiveThickness());
        Shader.SetGlobalFloat("_RuntimeOutlineAlphaThreshold", Mathf.Clamp(alphaThreshold, 0.001f, 1f));
    }

    private float GetEffectiveThickness()
    {
        if (!scaleThicknessWithCameraDistance)
        {
            return Mathf.Max(0.5f, thickness);
        }

        Camera camera = Camera.main;
        if (camera == null || RuntimeOutlineSelectionManager.SelectedTargets.Count == 0)
        {
            return Mathf.Max(0.5f, thickness);
        }

        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < RuntimeOutlineSelectionManager.SelectedTargets.Count; i++)
        {
            RuntimeOutlineTarget target = RuntimeOutlineSelectionManager.SelectedTargets[i];
            if (target == null) continue;

            Renderer renderer = target.GetComponent<Renderer>();
            Vector3 point = renderer != null
                ? renderer.bounds.ClosestPoint(camera.transform.position)
                : target.transform.position;
            closestDistance = Mathf.Min(closestDistance, Vector3.Distance(camera.transform.position, point));
        }

        if (float.IsPositiveInfinity(closestDistance))
        {
            return Mathf.Max(0.5f, thickness);
        }

        float maxDistance = Mathf.Max(nearDistance + 0.01f, farDistance);
        float t = Mathf.InverseLerp(nearDistance, maxDistance, closestDistance);
        return Mathf.Max(0.5f, Mathf.Lerp(nearThickness, farThickness, t));
    }

    private void OnDisable()
    {
        if (outlinePass != null && outlinePass.fullscreenPassMaterial == runtimeMaterial)
        {
            outlinePass.fullscreenPassMaterial = originalMaterial;
            outlinePass.enabled = originalPassEnabled;
        }
        if (runtimeMaterial != null)
        {
            if (Application.isPlaying) Destroy(runtimeMaterial);
            else DestroyImmediate(runtimeMaterial);
        }
        runtimeMaterial = null;
        outlinePass = null;
    }
}
