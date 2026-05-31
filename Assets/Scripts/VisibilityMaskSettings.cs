using System;
using UnityEngine;

[Serializable]
public sealed class VisibilityMaskSettings
{
    public const string MaskCenterPropertyName = "_PlayerVisibilityMaskCenter";
    public const string MaskParamsPropertyName = "_PlayerVisibilityMaskParams";
    public const string MaskDebugPropertyName = "_PlayerVisibilityMaskDebug";

    [Header("Mask")]
    [SerializeField, Range(0.01f, 0.5f), Tooltip("Rayon du masque en coordonnees viewport. 0.12 couvre environ 12% de la hauteur d'ecran.")]
    private float maskRadius = 0.14f;
    [SerializeField, Range(0.001f, 0.25f), Tooltip("Largeur de la transition douce du bord du masque en coordonnees viewport.")]
    private float edgeSoftness = 0.045f;
    [SerializeField, Range(0f, 1f), Tooltip("Intensite du composite joueur dans le masque.")]
    private float intensity = 1f;
    [SerializeField, Tooltip("Offset du centre du masque en pixels ecran.")]
    private Vector2 screenOffset;

    [Header("Activation")]
    [SerializeField, Tooltip("Affiche le masque uniquement quand un obstacle bloque la ligne camera -> joueur.")]
    private bool onlyShowWhenObstructed = true;
    [SerializeField, Min(0f), Tooltip("Vitesse de fondu du masque.")]
    private float activationSharpness = 12f;

    [Header("Layers")]
    [SerializeField, Tooltip("Layer(s) du joueur a rendre dans le pass dedie.")]
    private LayerMask playerLayer = 1 << 6;
    [SerializeField, Tooltip("Layers pris en compte par la detection d'obstruction camera -> joueur.")]
    private LayerMask obstacleLayers = (1 << 0) | (1 << 3) | (1 << 7) | (1 << 9);

    [Header("Debug")]
    [SerializeField, Tooltip("Active les gizmos et les globals de debug du masque.")]
    private bool debugMode;
    [SerializeField, Tooltip("Force l'affichage du masque et ajoute une teinte de debug meme sans obstruction.")]
    private bool forceMaskVisibleForDebug;

    public float MaskRadius => maskRadius;
    public float EdgeSoftness => edgeSoftness;
    public float Intensity => intensity;
    public Vector2 ScreenOffset => screenOffset;
    public bool OnlyShowWhenObstructed => onlyShowWhenObstructed;
    public float ActivationSharpness => activationSharpness;
    public LayerMask PlayerLayer => playerLayer;
    public LayerMask ObstacleLayers => obstacleLayers;
    public bool DebugMode => debugMode;
    public bool ForceMaskVisibleForDebug => forceMaskVisibleForDebug;

    public void Validate()
    {
        maskRadius = Mathf.Clamp(maskRadius, 0.01f, 0.5f);
        edgeSoftness = Mathf.Clamp(edgeSoftness, 0.001f, 0.25f);
        intensity = Mathf.Clamp01(intensity);
        activationSharpness = Mathf.Max(0f, activationSharpness);
    }

    public Vector2 ResolveViewportCenter(Vector2 targetViewport, Camera camera)
    {
        if (camera == null)
        {
            return targetViewport;
        }

        float width = Mathf.Max(1f, camera.pixelWidth);
        float height = Mathf.Max(1f, camera.pixelHeight);
        Vector2 viewportOffset = new Vector2(screenOffset.x / width, screenOffset.y / height);
        return targetViewport + viewportOffset;
    }
}
