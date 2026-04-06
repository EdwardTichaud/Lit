using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

public static class LevelIconSpriteSetup
{
    private const string TexturePath = "Assets/UI/LevelIcons.png";
    private const string SpriteAssetPath = "Assets/UI/LevelIcons_SpriteAsset.asset";
    private static readonly Color[] DefaultColors =
    {
        new Color(0.93f, 0.82f, 0.35f, 1f),
        new Color(0.55f, 0.85f, 0.9f, 1f),
        new Color(0.92f, 0.55f, 0.8f, 1f)
    };

    [MenuItem("Lit/Create Default Level Icons (TMP)")]
    public static void CreateDefaultIcons()
    {
        int maxLevel = ResolveMaxLevel();
        EnsureTexture(maxLevel);
        EnsureSpriteAsset();
    }

    private static int ResolveMaxLevel()
    {
        int maxLevel = 3;
        Item[] all = Resources.FindObjectsOfTypeAll<Item>();
        if (all != null && all.Length > 0)
        {
            for (int i = 0; i < all.Length; i++)
            {
                Item item = all[i];
                if (item == null || !item.isBuilding)
                {
                    continue;
                }

                maxLevel = Mathf.Max(maxLevel, item.buildingMaxLevel);
            }
        }

        return Mathf.Clamp(maxLevel, 1, 9);
    }

    private static void EnsureTexture(int maxLevel)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(TexturePath) ?? "Assets");

        int width = 64 * Mathf.Max(1, maxLevel);
        Texture2D texture = new Texture2D(width, 64, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        ClearTexture(texture);
        for (int i = 0; i < maxLevel; i++)
        {
            int digit = i + 1;
            Color color = i < DefaultColors.Length ? DefaultColors[i] : DefaultColors[DefaultColors.Length - 1];
            DrawDigit(texture, digit, i * 64, 0, color);
        }
        texture.Apply(false);

        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        File.WriteAllBytes(TexturePath, png);
        AssetDatabase.ImportAsset(TexturePath);

        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 64;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        List<SpriteMetaData> spritesheet = new List<SpriteMetaData>();
        for (int i = 0; i < maxLevel; i++)
        {
            SpriteMetaData meta = new SpriteMetaData
            {
                name = $"Level{i + 1}",
                rect = new Rect(i * 64, 0, 64, 64),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };
            spritesheet.Add(meta);
        }

        importer.spritesheet = spritesheet.ToArray();
        importer.SaveAndReimport();
    }

    private static void EnsureSpriteAsset()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
        {
            Debug.LogWarning("LevelIconSpriteSetup: texture introuvable, creation impossible.");
            return;
        }

        TMP_SpriteAsset spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
        if (spriteAsset == null)
        {
            spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            AssetDatabase.CreateAsset(spriteAsset, SpriteAssetPath);
        }

        spriteAsset.spriteSheet = texture;
        spriteAsset.hashCode = TMP_TextUtilities.GetSimpleHashCode(spriteAsset.name);

        List<TMP_SpriteGlyph> spriteGlyphTable = new List<TMP_SpriteGlyph>();
        List<TMP_SpriteCharacter> spriteCharacterTable = new List<TMP_SpriteCharacter>();
        PopulateSpriteTables(texture, ref spriteCharacterTable, ref spriteGlyphTable);

        List<TMP_SpriteCharacter> characterTable = spriteAsset.spriteCharacterTable;
        if (characterTable == null)
        {
            characterTable = new List<TMP_SpriteCharacter>();
        }
        else
        {
            characterTable.Clear();
        }
        characterTable.AddRange(spriteCharacterTable);

        List<TMP_SpriteGlyph> glyphTable = spriteAsset.spriteGlyphTable;
        if (glyphTable == null)
        {
            glyphTable = new List<TMP_SpriteGlyph>();
        }
        else
        {
            glyphTable.Clear();
        }
        glyphTable.AddRange(spriteGlyphTable);

        if (spriteAsset.material == null)
        {
            AddDefaultMaterial(spriteAsset);
        }

        spriteAsset.UpdateLookupTables();
        EditorUtility.SetDirty(spriteAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(SpriteAssetPath);

        TMP_Settings.defaultSpriteAsset = spriteAsset;
        EditorUtility.SetDirty(TMP_Settings.instance);
        AssetDatabase.SaveAssets();
    }

    private static void PopulateSpriteTables(Texture source, ref List<TMP_SpriteCharacter> spriteCharacterTable, ref List<TMP_SpriteGlyph> spriteGlyphTable)
    {
        string filePath = AssetDatabase.GetAssetPath(source);
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(filePath)
            .Select(x => x as Sprite)
            .Where(x => x != null)
            .OrderByDescending(x => x.rect.y)
            .ThenBy(x => x.rect.x)
            .ToArray();

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            TMP_SpriteGlyph spriteGlyph = new TMP_SpriteGlyph
            {
                index = (uint)i,
                metrics = new GlyphMetrics(sprite.rect.width, sprite.rect.height, -sprite.pivot.x, sprite.rect.height - sprite.pivot.y, sprite.rect.width),
                glyphRect = new GlyphRect(sprite.rect),
                scale = 1.0f,
                sprite = sprite
            };
            spriteGlyphTable.Add(spriteGlyph);

            TMP_SpriteCharacter spriteCharacter = new TMP_SpriteCharacter(0xFFFE, spriteGlyph)
            {
                name = sprite.name
            };
            spriteCharacterTable.Add(spriteCharacter);
        }
    }

    private static void AddDefaultMaterial(TMP_SpriteAsset spriteAsset)
    {
        Shader shader = Shader.Find("TextMeshPro/Sprite");
        if (shader == null)
        {
            return;
        }

        Material material = new Material(shader);
        material.SetTexture(ShaderUtilities.ID_MainTex, spriteAsset.spriteSheet);
        spriteAsset.material = material;

        AssetDatabase.AddObjectToAsset(material, spriteAsset);
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(spriteAsset));
    }

    private static void ClearTexture(Texture2D texture)
    {
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int iy = 0; iy < texture.height; iy++)
        {
            for (int ix = 0; ix < texture.width; ix++)
            {
                texture.SetPixel(ix, iy, clear);
            }
        }
    }

    private static void DrawDigit(Texture2D texture, int digit, int offsetX, int offsetY, Color color)
    {
        int segments = GetDigitSegments(digit);
        if (segments == 0)
        {
            return;
        }

        float left = offsetX + 14f;
        float right = offsetX + 50f;
        float top = offsetY + 54f;
        float bottom = offsetY + 10f;
        float mid = (top + bottom) * 0.5f;
        float inset = 6f;
        float radius = 4.5f;
        float outlineRadius = radius + 1.5f;
        Color outline = new Color(0f, 0f, 0f, 0.9f);

        Color topColor = Color.Lerp(color, Color.white, 0.35f);
        Color bottomColor = Color.Lerp(color, Color.black, 0.25f);

        Vector2 a = new Vector2(left + inset, top);
        Vector2 b = new Vector2(right - inset, top);
        Vector2 c = new Vector2(right, top - inset);
        Vector2 d = new Vector2(right, mid + inset);
        Vector2 e = new Vector2(right, mid - inset);
        Vector2 f = new Vector2(right, bottom + inset);
        Vector2 g = new Vector2(left + inset, bottom);
        Vector2 h = new Vector2(right - inset, bottom);
        Vector2 i = new Vector2(left, mid - inset);
        Vector2 j = new Vector2(left, bottom + inset);
        Vector2 k = new Vector2(left, top - inset);
        Vector2 l = new Vector2(left, mid + inset);
        Vector2 m = new Vector2(left + inset, mid);
        Vector2 n = new Vector2(right - inset, mid);

        DrawIf(segments, SegmentA, a, b);
        DrawIf(segments, SegmentB, c, d);
        DrawIf(segments, SegmentC, e, f);
        DrawIf(segments, SegmentD, g, h);
        DrawIf(segments, SegmentE, i, j);
        DrawIf(segments, SegmentF, k, l);
        DrawIf(segments, SegmentG, m, n);

        void DrawIf(int mask, int flag, Vector2 p1, Vector2 p2)
        {
            if ((mask & flag) == 0)
            {
                return;
            }

            DrawSegment(texture, p1, p2, outlineRadius, outline, outline, offsetX, offsetY);
            DrawSegment(texture, p1, p2, radius, topColor, bottomColor, offsetX, offsetY);
        }
    }

    private const int SegmentA = 1 << 0;
    private const int SegmentB = 1 << 1;
    private const int SegmentC = 1 << 2;
    private const int SegmentD = 1 << 3;
    private const int SegmentE = 1 << 4;
    private const int SegmentF = 1 << 5;
    private const int SegmentG = 1 << 6;

    private static int GetDigitSegments(int digit)
    {
        switch (digit)
        {
            case 1:
                return SegmentB | SegmentC;
            case 2:
                return SegmentA | SegmentB | SegmentG | SegmentE | SegmentD;
            case 3:
                return SegmentA | SegmentB | SegmentG | SegmentC | SegmentD;
            case 4:
                return SegmentF | SegmentG | SegmentB | SegmentC;
            case 5:
                return SegmentA | SegmentF | SegmentG | SegmentC | SegmentD;
            case 6:
                return SegmentA | SegmentF | SegmentG | SegmentE | SegmentC | SegmentD;
            case 7:
                return SegmentA | SegmentB | SegmentC;
            case 8:
                return SegmentA | SegmentB | SegmentC | SegmentD | SegmentE | SegmentF | SegmentG;
            case 9:
                return SegmentA | SegmentB | SegmentC | SegmentD | SegmentF | SegmentG;
            case 0:
                return SegmentA | SegmentB | SegmentC | SegmentD | SegmentE | SegmentF;
            default:
                return 0;
        }
    }

    private static void DrawSegment(Texture2D texture, Vector2 a, Vector2 b, float radius, Color topColor, Color bottomColor, int offsetX, int offsetY)
    {
        float minX = Mathf.Min(a.x, b.x) - radius - 1f;
        float maxX = Mathf.Max(a.x, b.x) + radius + 1f;
        float minY = Mathf.Min(a.y, b.y) - radius - 1f;
        float maxY = Mathf.Max(a.y, b.y) + radius + 1f;

        int startX = Mathf.FloorToInt(minX);
        int endX = Mathf.CeilToInt(maxX);
        int startY = Mathf.FloorToInt(minY);
        int endY = Mathf.CeilToInt(maxY);

        for (int y = startY; y <= endY; y++)
        {
            if (y < offsetY || y >= offsetY + 64)
            {
                continue;
            }

            float t = Mathf.InverseLerp(offsetY + 8f, offsetY + 56f, y);
            Color color = Color.Lerp(bottomColor, topColor, t);

            for (int x = startX; x <= endX; x++)
            {
                if (x < offsetX || x >= offsetX + 64)
                {
                    continue;
                }

                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float dist = DistancePointToSegment(p, a, b);
                if (dist <= radius)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float abSqr = Vector2.Dot(ab, ab);
        if (abSqr <= 0.0001f)
        {
            return Vector2.Distance(p, a);
        }

        float t = Vector2.Dot(p - a, ab) / abSqr;
        t = Mathf.Clamp01(t);
        Vector2 proj = a + ab * t;
        return Vector2.Distance(p, proj);
    }
}
