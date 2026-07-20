using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal sealed class LitIceV3MaterialCentralizerWindow : EditorWindow
{
    private const string ShaderPath =
        "Assets/Materials/IceShader/ShaderGraph_LitIceFrostedEdges_v3.shadergraph";
    private const string CanonicalMaterialPath =
        "Assets/Materials/IceShader/Material_LitIceFrostedEdges_v3.mat";
    private const string GeneratedLibraryRoot = "Assets/Environment/Prefabs_Ice/";
    private const string ShaderName = "LIT/Ice/Lit Ice Frosted Edges V3";

    private enum MaterialScope
    {
        AllProjectV3,
        GeneratedLibraryOnly,
        SelectedMaterialsOnly
    }

    private enum PropertyKind
    {
        Float,
        Color,
        Vector,
        Texture
    }

    private readonly struct PropertySpec
    {
        public PropertySpec(string name, PropertyKind kind)
        {
            Name = name;
            Kind = kind;
        }

        public string Name { get; }
        public PropertyKind Kind { get; }
    }

    private static readonly PropertySpec[] FrostAppearanceProperties =
    {
        Color("_IceDeepColor"),
        Color("_FrostColor"),
        Float("_FrostWidth"),
        Float("_IceScale"),
        Color("_CrackColor"),
        Texture("_CrackTexture"),
        Float("_CrackTextureStrength"),
        Float("_CrackTextureScale"),
        Float("_CrackTextureInvert"),
        Float("_Transparency"),
        Float("_NormalStrength"),
        Float("_EdgeSensitivity"),
        Vector("_NoiseOffset"),
        Float("_MicroScale"),
        Float("_CrackWidth")
    };

    private static readonly PropertySpec[] EmissionProperties =
    {
        Float("_EnableEmission"),
        Float("_EmissionIntensity")
    };

    private static readonly PropertySpec[] ReflectionProperties =
    {
        Float("_ReflectionStrength")
    };

    private static readonly PropertySpec[] IceSurfaceProperties =
    {
        Float("_Smoothness"),
        Float("_Metallic")
    };

    private static readonly PropertySpec[] BakedEdgeProperties =
    {
        Float("_EdgeBakedBoost")
    };

    private static readonly PropertySpec[] ReliefAndTextureEdgeProperties =
    {
        Float("_IceReliefNormalStrength"),
        Float("_IceReliefRoughnessInfluence"),
        Float("_TextureEdgeStrength"),
        Float("_TextureEdgeWidth"),
        Float("_TextureEdgeThreshold"),
        Float("_TextureEdgeNormalInfluence"),
        Float("_TextureEdgeRoughnessInfluence")
    };

    private static readonly PropertySpec[] TransitionProperties =
    {
        Float("_TransitionSoftness"),
        Float("_TransitionProgress")
    };

    private static readonly PropertySpec[] NormalStateProperties =
    {
        Texture("_BaseTexture"),
        Texture("_NormalTexture"),
        Texture("_BaseRoughnessTexture"),
        Texture("_BaseMetallicTexture"),
        Texture("_BaseOcclusionTexture"),
        Color("_BaseColor"),
        Float("_BaseNormalStrength"),
        Float("_BaseSmoothness"),
        Float("_BaseMetallic"),
        Float("_UseBaseRoughnessTexture"),
        Float("_UseBaseMetallicTexture"),
        Float("_UseBaseOcclusionTexture")
    };

    private static readonly PropertySpec[] ProjectionProperties =
    {
        Float("_UseScaleTiling"),
        Float("_TilingMultiplier")
    };

    [SerializeField] private Material m_MasterMaterial;
    [SerializeField] private MaterialScope m_Scope = MaterialScope.AllProjectV3;
    [SerializeField] private bool m_CopyFrostAppearance = true;
    [SerializeField] private bool m_CopyEmission = true;
    [SerializeField] private bool m_CopyReflections = true;
    [SerializeField] private bool m_CopyIceSurface = true;
    [SerializeField] private bool m_CopyBakedEdges = true;
    [SerializeField] private bool m_CopyReliefAndTextureEdges;
    [SerializeField] private bool m_CopyTransition = true;
    [SerializeField] private bool m_CopyNormalStateScalars;
    [SerializeField] private bool m_CopyProjection;
    [SerializeField] private bool m_ConfirmBeforeApply = true;
    [SerializeField] private bool m_MasterInspectorExpanded = true;
    [SerializeField] private int m_PropertySelectionVersion;
    [SerializeField] private List<string> m_SelectedPropertyNames = new List<string>();
    [SerializeField] private bool m_FrostPropertiesExpanded = true;
    [SerializeField] private bool m_EmissionPropertiesExpanded = true;
    [SerializeField] private bool m_ReflectionPropertiesExpanded = true;
    [SerializeField] private bool m_IceSurfacePropertiesExpanded = true;
    [SerializeField] private bool m_BakedEdgePropertiesExpanded = true;
    [SerializeField] private bool m_ReliefPropertiesExpanded;
    [SerializeField] private bool m_TransitionPropertiesExpanded = true;
    [SerializeField] private bool m_NormalStatePropertiesExpanded;
    [SerializeField] private bool m_ProjectionPropertiesExpanded;

    private readonly List<Material> m_TargetMaterials = new List<Material>();
    private Vector2 m_Scroll;
    private MaterialEditor m_MasterEditor;
    private string m_StatusMessage;
    private MessageType m_StatusType = MessageType.Info;
    private const int CurrentPropertySelectionVersion = 1;

    [MenuItem("Lit/Shadergraph/Ice V3 Material Centralizer")]
    internal static void OpenWindow()
    {
        var window = GetWindow<LitIceV3MaterialCentralizerWindow>(
            utility: false,
            title: "Ice V3 Centralizer",
            focus: true);
        window.minSize = new Vector2(470f, 620f);
        window.Show();
    }

    private void OnEnable()
    {
        InitializePropertySelection();
        if (m_MasterMaterial == null)
            m_MasterMaterial = AssetDatabase.LoadAssetAtPath<Material>(CanonicalMaterialPath);

        RebuildMasterEditor();
        RefreshTargets();
    }

    private void OnDisable()
    {
        DestroyMasterEditor();
    }

    private void OnSelectionChange()
    {
        if (m_Scope == MaterialScope.SelectedMaterialsOnly)
        {
            RefreshTargets();
            Repaint();
        }
    }

    private void OnGUI()
    {
        m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
        DrawHeader();
        DrawScope();
        DrawMasterMaterial();
        DrawPropertyGroups();
        DrawApplyPanel();
        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("ICE V3 MATERIAL CENTRALIZER", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Modifiez le matériau maître, puis cochez précisément chaque variable à synchroniser. "
            + "Les sections servent uniquement à organiser l'affichage ; les choix restent indépendants.",
            MessageType.Info);
    }

    private void DrawScope()
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("TARGET MATERIALS", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            m_Scope = (MaterialScope)EditorGUILayout.EnumPopup("Scope", m_Scope);
            if (EditorGUI.EndChangeCheck())
                RefreshTargets();

            EditorGUILayout.LabelField($"V3 materials found: {m_TargetMaterials.Count}");
            if (GUILayout.Button("Refresh Material List"))
                RefreshTargets();
        }
    }

    private void DrawMasterMaterial()
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("MASTER MATERIAL", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            Material selected = (Material)EditorGUILayout.ObjectField(
                "Source", m_MasterMaterial, typeof(Material), false);
            if (EditorGUI.EndChangeCheck())
            {
                m_MasterMaterial = selected;
                RebuildMasterEditor();
            }

            if (m_MasterMaterial == null)
            {
                EditorGUILayout.HelpBox("Choose a V3 master material.", MessageType.Warning);
                return;
            }

            if (!IsV3Material(m_MasterMaterial))
            {
                EditorGUILayout.HelpBox(
                    "The selected material does not use ShaderGraph_LitIceFrostedEdges_v3.",
                    MessageType.Error);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Master in Project"))
                {
                    Selection.activeObject = m_MasterMaterial;
                    EditorGUIUtility.PingObject(m_MasterMaterial);
                }

                if (GUILayout.Button("Use Canonical V3"))
                {
                    m_MasterMaterial = AssetDatabase.LoadAssetAtPath<Material>(CanonicalMaterialPath);
                    RebuildMasterEditor();
                }
            }

            m_MasterInspectorExpanded = EditorGUILayout.Foldout(
                m_MasterInspectorExpanded, "Edit Master Material", true);
            if (m_MasterInspectorExpanded && m_MasterEditor != null)
            {
                EditorGUILayout.Space(3f);
                m_MasterEditor.OnInspectorGUI();
            }
        }
    }

    private void DrawPropertyGroups()
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("PROPERTIES TO SYNCHRONIZE", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"Selected variables: {BuildSelectedProperties().Count}",
                EditorStyles.miniLabel);

            DrawPropertyGroup("FROST APPEARANCE", FrostAppearanceProperties,
                ref m_FrostPropertiesExpanded);
            DrawPropertyGroup("MATERIAL EMISSION", EmissionProperties,
                ref m_EmissionPropertiesExpanded);
            DrawPropertyGroup("REFLECTIONS", ReflectionProperties,
                ref m_ReflectionPropertiesExpanded);
            DrawPropertyGroup("ICE SURFACE", IceSurfaceProperties,
                ref m_IceSurfacePropertiesExpanded);
            DrawPropertyGroup("BAKED EDGES", BakedEdgeProperties,
                ref m_BakedEdgePropertiesExpanded);
            DrawPropertyGroup("WALLS / FLOORS", ReliefAndTextureEdgeProperties,
                ref m_ReliefPropertiesExpanded);
            DrawPropertyGroup("FLAME TRANSITION", TransitionProperties,
                ref m_TransitionPropertiesExpanded);
            DrawPropertyGroup("NORMAL STATE", NormalStateProperties,
                ref m_NormalStatePropertiesExpanded);
            DrawPropertyGroup("TEXTURE PROJECTION", ProjectionProperties,
                ref m_ProjectionPropertiesExpanded);

            EditorGUILayout.Space(3f);
            EditorGUILayout.HelpBox(
                "Every displayed variable is independent. All and None only change the variables "
                + "inside their section. Normal-state textures are available but disabled by default; "
                + "selecting one intentionally replaces that texture on every target material.",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "FlameCenter and FlameInfluenceRadius remain excluded because flames write them at runtime. "
                + "TransitionProgress can be synchronized manually, but an active flame may overwrite it.",
                MessageType.None);
            if (IsPropertySelected("_EnableEmission")
                || IsPropertySelected("_EmissionIntensity"))
            {
                EditorGUILayout.HelpBox(
                    "Emission variables are independent too: enabling only Emission Intensity does not "
                    + "change the Enable Material Emission switch on target materials.",
                    MessageType.None);
            }
        }
    }

    private void DrawPropertyGroup(
        string title,
        PropertySpec[] properties,
        ref bool expanded)
    {
        int selectedCount = properties.Count(property => IsPropertySelected(property.Name));
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = EditorGUILayout.Foldout(
                    expanded,
                    $"{title}  ({selectedCount}/{properties.Length})",
                    true);

                if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(38f)))
                    SetGroupSelection(properties, true);
                if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(42f)))
                    SetGroupSelection(properties, false);
            }

            if (!expanded)
                return;

            EditorGUI.indentLevel++;
            for (int i = 0; i < properties.Length; i++)
            {
                PropertySpec property = properties[i];
                bool selected = IsPropertySelected(property.Name);
                string displayName = ObjectNames.NicifyVariableName(
                    property.Name.TrimStart('_'));
                bool newValue = EditorGUILayout.ToggleLeft(
                    new GUIContent($"{displayName}  [{property.Name}]"),
                    selected);
                if (newValue != selected)
                    SetPropertySelected(property.Name, newValue);
            }
            EditorGUI.indentLevel--;
        }
    }

    private void DrawApplyPanel()
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            m_ConfirmBeforeApply = EditorGUILayout.ToggleLeft(
                "Ask for confirmation before applying", m_ConfirmBeforeApply);

            using (new EditorGUI.DisabledScope(
                       m_MasterMaterial == null
                       || !IsV3Material(m_MasterMaterial)
                       || m_TargetMaterials.Count == 0
                       || BuildSelectedProperties().Count == 0))
            {
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.45f, 0.85f, 1f);
                if (GUILayout.Button(
                        $"APPLY TO {m_TargetMaterials.Count} V3 MATERIALS",
                        GUILayout.Height(38f)))
                {
                    ApplyToTargets();
                }
                GUI.backgroundColor = previous;
            }

            if (!string.IsNullOrEmpty(m_StatusMessage))
                EditorGUILayout.HelpBox(m_StatusMessage, m_StatusType);
        }
    }

    private void ApplyToTargets()
    {
        List<PropertySpec> properties = BuildSelectedProperties();
        if (properties.Count == 0 || m_MasterMaterial == null)
            return;

        if (m_ConfirmBeforeApply
            && !EditorUtility.DisplayDialog(
                "Apply shared Ice V3 settings?",
                $"Copy {properties.Count} selected properties from\n{m_MasterMaterial.name}\n\n"
                + $"to {m_TargetMaterials.Count} V3 materials?\n\n"
                + "Only the individually checked variables will be copied. Flame runtime positions and radii remain protected.",
                "Apply",
                "Cancel"))
        {
            return;
        }

        List<Material> changedMaterials = m_TargetMaterials
            .Where(material => material != null
                && material != m_MasterMaterial
                && WouldChange(material, properties))
            .ToList();

        if (changedMaterials.Count == 0)
        {
            SetStatus("All target materials already match the selected master settings.", MessageType.Info);
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply centralized Ice V3 settings");
        Undo.RecordObjects(changedMaterials.Cast<UnityEngine.Object>().ToArray(),
            "Apply centralized Ice V3 settings");

        bool cancelled = false;
        int updatedCount = 0;
        try
        {
            for (int i = 0; i < changedMaterials.Count; i++)
            {
                Material target = changedMaterials[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Ice V3 Material Centralizer",
                        $"Updating {target.name} ({i + 1}/{changedMaterials.Count})",
                        (float)i / changedMaterials.Count))
                {
                    cancelled = true;
                    break;
                }

                CopyProperties(m_MasterMaterial, target, properties);
                EditorUtility.SetDirty(target);
                updatedCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (cancelled)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            SetStatus("Operation cancelled. All partial material changes were reverted.", MessageType.Warning);
            return;
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        SetStatus(
            $"Updated {updatedCount} material(s) with {properties.Count} individually selected variable(s).",
            MessageType.Info);
    }

    private void RefreshTargets()
    {
        m_TargetMaterials.Clear();
        IEnumerable<Material> candidates;
        switch (m_Scope)
        {
            case MaterialScope.GeneratedLibraryOnly:
                candidates = FindProjectV3Materials().Where(material =>
                    AssetDatabase.GetAssetPath(material)
                        .Replace('\\', '/')
                        .StartsWith(GeneratedLibraryRoot, StringComparison.OrdinalIgnoreCase));
                break;
            case MaterialScope.SelectedMaterialsOnly:
                candidates = Selection.objects.OfType<Material>().Where(IsV3Material);
                break;
            default:
                candidates = FindProjectV3Materials();
                break;
        }

        m_TargetMaterials.AddRange(candidates
            .Where(material => material != null)
            .Distinct()
            .OrderBy(material => AssetDatabase.GetAssetPath(material), StringComparer.OrdinalIgnoreCase));
        Repaint();
    }

    private static IEnumerable<Material> FindProjectV3Materials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (IsV3Material(material))
                yield return material;
        }
    }

    private static bool IsV3Material(Material material)
    {
        if (material == null || material.shader == null)
            return false;

        Shader shaderAsset = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        return (shaderAsset != null && material.shader == shaderAsset)
            || string.Equals(material.shader.name, ShaderName, StringComparison.Ordinal);
    }

    private void InitializePropertySelection()
    {
        if (m_SelectedPropertyNames == null)
            m_SelectedPropertyNames = new List<string>();

        if (m_PropertySelectionVersion == CurrentPropertySelectionVersion)
            return;

        m_SelectedPropertyNames.Clear();
        SetGroupSelection(FrostAppearanceProperties, m_CopyFrostAppearance);
        SetGroupSelection(EmissionProperties, m_CopyEmission);
        SetGroupSelection(ReflectionProperties, m_CopyReflections);
        SetGroupSelection(IceSurfaceProperties, m_CopyIceSurface);
        SetGroupSelection(BakedEdgeProperties, m_CopyBakedEdges);
        SetGroupSelection(ReliefAndTextureEdgeProperties, m_CopyReliefAndTextureEdges);

        // Preserve the previous safe defaults during migration. Transition
        // Progress and model-specific normal-state textures start unchecked.
        SetPropertySelected("_TransitionSoftness", m_CopyTransition);
        if (m_CopyNormalStateScalars)
        {
            SetPropertySelected("_BaseColor", true);
            SetPropertySelected("_BaseNormalStrength", true);
            SetPropertySelected("_BaseSmoothness", true);
            SetPropertySelected("_BaseMetallic", true);
        }
        SetGroupSelection(ProjectionProperties, m_CopyProjection);
        m_PropertySelectionVersion = CurrentPropertySelectionVersion;
    }

    private List<PropertySpec> BuildSelectedProperties()
    {
        return EnumerateCentralizableProperties()
            .Where(property => IsPropertySelected(property.Name))
            .ToList();
    }

    private static IEnumerable<PropertySpec> EnumerateCentralizableProperties()
    {
        PropertySpec[][] groups =
        {
            FrostAppearanceProperties,
            EmissionProperties,
            ReflectionProperties,
            IceSurfaceProperties,
            BakedEdgeProperties,
            ReliefAndTextureEdgeProperties,
            TransitionProperties,
            NormalStateProperties,
            ProjectionProperties
        };

        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            PropertySpec[] group = groups[groupIndex];
            for (int propertyIndex = 0; propertyIndex < group.Length; propertyIndex++)
                yield return group[propertyIndex];
        }
    }

    private bool IsPropertySelected(string propertyName)
    {
        return m_SelectedPropertyNames != null
            && m_SelectedPropertyNames.Contains(propertyName);
    }

    private void SetPropertySelected(string propertyName, bool selected)
    {
        if (m_SelectedPropertyNames == null)
            m_SelectedPropertyNames = new List<string>();

        if (selected)
        {
            if (!m_SelectedPropertyNames.Contains(propertyName))
                m_SelectedPropertyNames.Add(propertyName);
        }
        else
        {
            m_SelectedPropertyNames.Remove(propertyName);
        }
    }

    private void SetGroupSelection(PropertySpec[] properties, bool selected)
    {
        for (int i = 0; i < properties.Length; i++)
            SetPropertySelected(properties[i].Name, selected);
    }

    private bool WouldChange(Material target, List<PropertySpec> properties)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            PropertySpec property = properties[i];
            if (!m_MasterMaterial.HasProperty(property.Name) || !target.HasProperty(property.Name))
                continue;

            switch (property.Kind)
            {
                case PropertyKind.Color:
                    if (!Approximately(m_MasterMaterial.GetColor(property.Name), target.GetColor(property.Name)))
                        return true;
                    break;
                case PropertyKind.Vector:
                    if (!Approximately(m_MasterMaterial.GetVector(property.Name), target.GetVector(property.Name)))
                        return true;
                    break;
                case PropertyKind.Texture:
                    if (m_MasterMaterial.GetTexture(property.Name) != target.GetTexture(property.Name))
                        return true;
                    break;
                default:
                    if (!Mathf.Approximately(
                            m_MasterMaterial.GetFloat(property.Name),
                            target.GetFloat(property.Name)))
                        return true;
                    break;
            }
        }

        return false;
    }

    private static void CopyProperties(
        Material source,
        Material target,
        List<PropertySpec> properties)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            PropertySpec property = properties[i];
            if (!source.HasProperty(property.Name) || !target.HasProperty(property.Name))
                continue;

            switch (property.Kind)
            {
                case PropertyKind.Color:
                    target.SetColor(property.Name, source.GetColor(property.Name));
                    break;
                case PropertyKind.Vector:
                    target.SetVector(property.Name, source.GetVector(property.Name));
                    break;
                case PropertyKind.Texture:
                    target.SetTexture(property.Name, source.GetTexture(property.Name));
                    break;
                default:
                    target.SetFloat(property.Name, source.GetFloat(property.Name));
                    break;
            }
        }
    }

    private void RebuildMasterEditor()
    {
        DestroyMasterEditor();
        if (m_MasterMaterial != null)
            m_MasterEditor = Editor.CreateEditor(m_MasterMaterial) as MaterialEditor;
    }

    private void DestroyMasterEditor()
    {
        if (m_MasterEditor != null)
            DestroyImmediate(m_MasterEditor);
        m_MasterEditor = null;
    }

    private void SetStatus(string message, MessageType type)
    {
        m_StatusMessage = message;
        m_StatusType = type;
        Repaint();
    }

    private static bool Approximately(Color a, Color b)
    {
        return Mathf.Approximately(a.r, b.r)
            && Mathf.Approximately(a.g, b.g)
            && Mathf.Approximately(a.b, b.b)
            && Mathf.Approximately(a.a, b.a);
    }

    private static bool Approximately(Vector4 a, Vector4 b)
    {
        return Mathf.Approximately(a.x, b.x)
            && Mathf.Approximately(a.y, b.y)
            && Mathf.Approximately(a.z, b.z)
            && Mathf.Approximately(a.w, b.w);
    }

    private static PropertySpec Float(string name) => new PropertySpec(name, PropertyKind.Float);
    private static PropertySpec Color(string name) => new PropertySpec(name, PropertyKind.Color);
    private static PropertySpec Vector(string name) => new PropertySpec(name, PropertyKind.Vector);
    private static PropertySpec Texture(string name) => new PropertySpec(name, PropertyKind.Texture);
}
