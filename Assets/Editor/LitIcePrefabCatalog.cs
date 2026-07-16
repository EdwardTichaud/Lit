using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LitIcePrefabCatalog : ScriptableObject
{
    public const int CurrentVersion = 1;

    [SerializeField] private int m_Version = CurrentVersion;
    [SerializeField] private List<LitIcePrefabCatalogEntry> m_Entries = new List<LitIcePrefabCatalogEntry>();

    public int Version
    {
        get => m_Version;
        set => m_Version = value;
    }

    public List<LitIcePrefabCatalogEntry> Entries => m_Entries;

    public LitIcePrefabCatalogEntry FindBySourceId(string sourceId)
    {
        return m_Entries.Find(entry => entry != null && entry.SourceMeshId == sourceId);
    }

    public LitIcePrefabCatalogEntry FindByBakedMesh(Mesh bakedMesh)
    {
        return bakedMesh == null
            ? null
            : m_Entries.Find(entry => entry != null && entry.BakedMesh == bakedMesh);
    }
}

[Serializable]
public sealed class LitIcePrefabCatalogEntry
{
    public string SourceMeshId;
    public string SourceMeshName;
    public string SourceAssetPath;
    public string SourceDependencyHash;
    public int BakeVersion;
    public string FolderPath;
    public Mesh SourceMesh;
    public Mesh BakedMesh;
    public List<LitIcePrefabVariantEntry> Variants = new List<LitIcePrefabVariantEntry>();
}

[Serializable]
public sealed class LitIcePrefabVariantEntry
{
    public string VariantId;
    public string PrefabPath;
    public GameObject Prefab;
    public List<string> SourceMaterialIds = new List<string>();
    public List<Material> SourceMaterials = new List<Material>();
    public List<Material> Materials = new List<Material>();
}
