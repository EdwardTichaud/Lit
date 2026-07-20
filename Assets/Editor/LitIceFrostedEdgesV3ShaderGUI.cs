using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;

public sealed class LitIceFrostedEdgesV3ShaderGUI : LightingShaderGraphGUI
{
    private static bool s_FrostExpanded = true;
    private static bool s_NormalExpanded = true;
    private static bool s_EmissionExpanded = true;
    private static bool s_ReflectionsExpanded = true;
    private static bool s_WallsFloorsExpanded = true;
    private static bool s_TransitionExpanded = true;
    private static string s_LastBakeMessage;
    private static MessageType s_LastBakeMessageType = MessageType.Info;
    private static GUIStyle s_SectionHeaderStyle;

    private readonly MaterialUIBlockList m_HdrpBlocks = new MaterialUIBlockList
    {
        new SurfaceOptionUIBlock(
            MaterialUIBlock.ExpandableBit.Base,
            features: SurfaceOptionUIBlock.Features.Lit
                | SurfaceOptionUIBlock.Features.ShowDepthOffsetOnly),
        new TransparencyUIBlock(
            MaterialUIBlock.ExpandableBit.Transparency,
            TransparencyUIBlock.Features.Refraction),
        new AdvancedOptionsUIBlock(
            MaterialUIBlock.ExpandableBit.Advance,
            ~AdvancedOptionsUIBlock.Features.SpecularOcclusion)
    };

    protected override void OnMaterialGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        Material[] materials = materialEditor.targets.OfType<Material>().ToArray();

        DrawBakePanel(materialEditor, materials);
        DrawSection("STATE: FROST", ref s_FrostExpanded, () =>
            DrawFrostProperties(materialEditor, properties));
        DrawSection("STATE: NORMAL", ref s_NormalExpanded, () =>
            DrawNormalProperties(materialEditor, properties));
        DrawSection("MATERIAL: EMISSION", ref s_EmissionExpanded, () =>
            DrawEmissionProperties(materialEditor, properties));
        DrawSection("MATERIAL: REFLECTIONS", ref s_ReflectionsExpanded, () =>
            DrawReflectionProperties(materialEditor, properties));
        DrawSection("OPTION: WALLS / FLOORS", ref s_WallsFloorsExpanded, () =>
            DrawWallsAndFloorsProperties(materialEditor, properties));
        DrawSection("FLAME TRANSITION", ref s_TransitionExpanded, () =>
            DrawTransitionProperties(materialEditor, properties));

        EditorGUILayout.Space(4f);
        m_HdrpBlocks.OnGUI(materialEditor, properties);
    }

    private static void DrawBakePanel(MaterialEditor materialEditor, Material[] materials)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("EDGE MASK", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Bake les arêtes géométriques dans une copie courte du mesh. "
                + "La sélection compatible est prioritaire ; sinon tous les renderers chargés utilisant ce matériau sont traités.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(3f);

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.58f, 0.88f, 1.0f);
            if (GUILayout.Button("BAKE EDGE MASK", GUILayout.Height(34f)))
            {
                LitIceEdgeMaskBaker.BakeSummary summary =
                    LitIceEdgeMaskBaker.BakeForMaterials(materials);
                s_LastBakeMessage = summary.ToDisplayMessage();
                s_LastBakeMessageType = summary.ErrorCount > 0
                    ? MessageType.Error
                    : summary.CandidateRendererCount == 0
                        ? MessageType.Warning
                        : MessageType.Info;
                Debug.Log($"[Lit Ice] {s_LastBakeMessage}");
                SceneView.RepaintAll();
                materialEditor.Repaint();
                GUI.backgroundColor = previousBackground;
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = previousBackground;

            if (GUILayout.Button("OPEN V3 MATERIAL CENTRALIZER", GUILayout.Height(26f)))
            {
                LitIceV3MaterialCentralizerWindow.OpenWindow();
            }

            if (!string.IsNullOrEmpty(s_LastBakeMessage))
                EditorGUILayout.HelpBox(s_LastBakeMessage, s_LastBakeMessageType);
        }

        EditorGUILayout.Space(5f);
    }

    private void DrawFrostProperties(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawProperty(editor, properties, "_IceDeepColor", "Ice Deep Color",
            "Couleur interne des zones profondes de la glace.");
        DrawProperty(editor, properties, "_FrostColor", "Frost / Edge Color",
            "Couleur visible du givre et des arêtes.");
        DrawProperty(editor, properties, "_FrostWidth", "Frost Width",
            "Largeur du givre automatique et des lignes du bake. 0 coupe le givre d’arête ; 10 crée une bande très large. Toute la plage est progressive.");
        DrawProperty(editor, properties, "_IceScale", "Ice Scale",
            "Échelle du volume procédural de glace et des fissures.");
        DrawProperty(editor, properties, "_CrackColor", "Crack Color",
            "Couleur visible des fissures internes.");
        DrawProperty(editor, properties, "_CrackTexture", "Crack Texture / Sprite",
            "Image qui remplace progressivement les fissures procédurales. Une texture noir et blanc ou un sprite avec transparence peuvent être utilisés.", true);
        DrawProperty(editor, properties, "_CrackTextureStrength", "Crack Texture Strength",
            "0 conserve les fissures procédurales ; 1 les remplace entièrement par l’image.");
        MaterialProperty crackTextureStrength = FindProperty("_CrackTextureStrength", properties, false);
        if (crackTextureStrength != null
            && (crackTextureStrength.hasMixedValue || crackTextureStrength.floatValue > 0.001f))
        {
            EditorGUI.indentLevel++;
            DrawProperty(editor, properties, "_CrackTextureScale", "Crack Texture Scale",
                "Nombre de répétitions du motif sur les UV ou dans la projection monde.");
            DrawToggleProperty(editor, properties, "_CrackTextureInvert", "Invert Crack Texture",
                "À activer lorsque les fissures sont noires sur un fond blanc.");
            EditorGUI.indentLevel--;
        }
        DrawProperty(editor, properties, "_Transparency", "Ice Transparency",
            "Opacité de l’état glace.");
        DrawProperty(editor, properties, "_NormalStrength", "Ice Normal Strength",
            "Intensité des aspérités procédurales de la glace.");
        DrawProperty(editor, properties, "_EdgeSensitivity", "Geometry Edge Sensitivity",
            "Sensibilité de la détection automatique de courbure. Les arêtes nettes utilisent surtout le bake.");
        DrawProperty(editor, properties, "_NoiseOffset", "Noise Offset",
            "Décale le volume procédural dans l’espace.");
        DrawProperty(editor, properties, "_MicroScale", "Micro Scale",
            "Fréquence des micro-aspérités procédurales : faible = détails larges, élevé = détails fins. Son effet est surtout visible avec Ice Normal Strength.");
        DrawProperty(editor, properties, "_CrackWidth", "Crack Width",
            "Largeur et densité apparente des fissures.");
        DrawProperty(editor, properties, "_Smoothness", "Ice Smoothness",
            "Lissage et netteté des réflexions de l’état glace.");
        DrawProperty(editor, properties, "_Metallic", "Ice Metallic",
            "Réponse métallique de l’état glace.");
        DrawProperty(editor, properties, "_EdgeBakedBoost", "Baked Edge Boost",
            "Force du masque d’arêtes produit par le bouton Bake.");
    }

    private void DrawEmissionProperties(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawToggleProperty(editor, properties, "_EnableEmission", "Enable Material Emission",
            "OFF : aucune émission. ON : la glace et l’apparence révélée par Base Texture émettent de la lumière.");
        MaterialProperty enableEmission = FindProperty("_EnableEmission", properties, false);
        if (enableEmission != null
            && (enableEmission.hasMixedValue || enableEmission.floatValue > 0.5f))
        {
            EditorGUI.indentLevel++;
            DrawProperty(editor, properties, "_EmissionIntensity", "Material Emission Intensity",
                "Intensité commune à l’émission de la glace et à celle de Base Texture. La transition entre les deux reste progressive.");
            EditorGUI.indentLevel--;
        }
    }

    private void DrawReflectionProperties(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawProperty(editor, properties, "_ReflectionStrength", "Reflection Strength",
            "Renforce et affine progressivement les reflets HDRP des Reflection Probes sur la glace et sur l’état normal. 0 conserve les Smoothness existantes ; 1 donne un reflet presque miroir.");
    }

    private void DrawNormalProperties(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawProperty(editor, properties, "_BaseTexture", "Base Texture",
            "Texture couleur révélée quand Transition Progress atteint 1.", true);
        DrawProperty(editor, properties, "_NormalTexture", "Normal Texture",
            "Normal Map de l’apparence révélée, également réutilisable sous la glace.", true);
        DrawProperty(editor, properties, "_BaseColor", "Base Color",
            "Teinte appliquée à Base Texture dans l’état normal.");
        DrawProperty(editor, properties, "_BaseNormalStrength", "Base Normal Strength",
            "Intensité de la Normal Map dans l’état normal.");
        DrawProperty(editor, properties, "_BaseSmoothness", "Base Smoothness",
            "Smoothness de secours lorsque la Roughness Texture est désactivée.");
        DrawProperty(editor, properties, "_BaseMetallic", "Base Metallic",
            "Metallic de secours lorsque la Metallic Texture est désactivée.");
        DrawProperty(editor, properties, "_BaseRoughnessTexture", "Base Roughness Texture",
            "Roughness de l’apparence normale.", true);
        DrawProperty(editor, properties, "_BaseMetallicTexture", "Base Metallic Texture",
            "Metallic de l’apparence normale.", true);
        DrawProperty(editor, properties, "_BaseOcclusionTexture", "Base Occlusion Texture",
            "Occlusion de l’apparence normale.", true);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Texture Projection", EditorStyles.miniBoldLabel);
        DrawProperty(editor, properties, "_UseScaleTiling", "Use World Scale Tiling",
            "Désactive les UV du mesh et projette les textures selon la face dominante en coordonnées monde.");
        DrawProperty(editor, properties, "_TilingMultiplier", "Tiling Multiplier",
            "Échelle de la projection monde lorsque World Scale Tiling est activé.");
    }

    private void DrawWallsAndFloorsProperties(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawProperty(editor, properties, "_UseBaseRoughnessTexture", "Use Roughness Texture",
            "Active la Roughness Texture dans l’état normal et ses contributions optionnelles sous la glace.");
        DrawProperty(editor, properties, "_UseBaseMetallicTexture", "Use Metallic Texture",
            "Active la Metallic Texture dans l’état normal.");
        DrawProperty(editor, properties, "_UseBaseOcclusionTexture", "Use Occlusion Texture",
            "Active l’Occlusion Texture dans l’état normal.");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Relief Under Ice", EditorStyles.miniBoldLabel);
        DrawProperty(editor, properties, "_IceReliefNormalStrength", "Ice Relief Normal Strength",
            "Quantité de la Normal Map conservée lorsque Transition Progress vaut 0.");
        DrawProperty(editor, properties, "_IceReliefRoughnessInfluence", "Ice Relief Roughness Influence",
            "Quantité de Roughness conservée sous la glace.");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Texture Edge Detection", EditorStyles.miniBoldLabel);
        DrawProperty(editor, properties, "_TextureEdgeStrength", "Texture Edge Strength",
            "Intensité visuelle du givre détecté dans les textures de relief.");
        DrawProperty(editor, properties, "_TextureEdgeWidth", "Texture Edge Width",
            "Distance d’échantillonnage autour du texel, donc largeur des contours détectés.");
        DrawProperty(editor, properties, "_TextureEdgeThreshold", "Texture Edge Threshold",
            "Filtre le bruit fin. Augmenter pour moins de contours, diminuer pour en détecter davantage.");
        DrawProperty(editor, properties, "_TextureEdgeNormalInfluence", "Normal Edge Influence",
            "Contribution de la Normal Map à la détection des contours texturés.");
        DrawProperty(editor, properties, "_TextureEdgeRoughnessInfluence", "Roughness Edge Influence",
            "Contribution de la Roughness Map à la détection des contours texturés.");
    }

    private void DrawTransitionProperties(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawProperty(editor, properties, "_FlameCenter", "Flame Center",
            "Centre monde de l’influence active, normalement piloté par LitInfluenceSource.");
        DrawProperty(editor, properties, "_FlameInfluenceRadius", "Flame Influence Radius",
            "Rayon monde de l’influence. Zéro désactive entièrement la transition spatiale.");
        DrawProperty(editor, properties, "_TransitionSoftness", "Transition Softness",
            "Largeur du fondu spatial autour du rayon.");
        DrawProperty(editor, properties, "_TransitionProgress", "Transition Progress",
            "Progression manuelle : 0 = glace, 1 = apparence normale dans la zone de flamme.");
    }

    private static void DrawSection(string title, ref bool expanded, Action drawContents)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            expanded = EditorGUILayout.Foldout(expanded, title, true, SectionHeaderStyle);
            if (!expanded)
                return;

            Rect line = EditorGUILayout.GetControlRect(false, 2f);
            EditorGUI.DrawRect(line, new Color(0.28f, 0.72f, 0.92f, 0.8f));
            EditorGUILayout.Space(2f);
            EditorGUI.indentLevel++;
            drawContents();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(3f);
    }

    private void DrawProperty(MaterialEditor editor, MaterialProperty[] properties,
        string propertyName, string label, string tooltip, bool texture = false)
    {
        MaterialProperty property = FindProperty(propertyName, properties, false);
        if (property == null)
            return;

        var content = new GUIContent(label, tooltip);
        if (texture)
            editor.TexturePropertySingleLine(content, property);
        else
            editor.ShaderProperty(property, content);
    }

    private void DrawToggleProperty(MaterialEditor editor, MaterialProperty[] properties,
        string propertyName, string label, string tooltip)
    {
        MaterialProperty property = FindProperty(propertyName, properties, false);
        if (property == null)
            return;

        EditorGUI.showMixedValue = property.hasMixedValue;
        EditorGUI.BeginChangeCheck();
        bool enabled = EditorGUILayout.Toggle(
            new GUIContent(label, tooltip),
            property.floatValue > 0.5f);
        if (EditorGUI.EndChangeCheck())
        {
            editor.RegisterPropertyChangeUndo(label);
            property.floatValue = enabled ? 1f : 0f;
        }
        EditorGUI.showMixedValue = false;
    }

    private static GUIStyle SectionHeaderStyle
    {
        get
        {
            if (s_SectionHeaderStyle == null)
            {
                s_SectionHeaderStyle = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 12,
                    fixedHeight = 22f
                };
            }
            return s_SectionHeaderStyle;
        }
    }
}
