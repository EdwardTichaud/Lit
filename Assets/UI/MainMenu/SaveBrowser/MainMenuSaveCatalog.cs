using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class MainMenuSaveCatalog
{
    public static bool IsPlayable(SaveSlotInfo slot)
    {
        if (slot == null || !slot.validMetadata || string.IsNullOrEmpty(slot.directoryPath)) return false;
        try
        {
            string path = Path.Combine(slot.directoryPath, "CharacterState.json");
            if (!File.Exists(path)) return false;
            CharacterSaveData data = JsonUtility.FromJson<CharacterSaveData>(File.ReadAllText(path));
            return data != null && data.characters != null && data.characters.Count > 0;
        }
        catch (Exception) { return false; }
    }
    public static double SafePlaytime(float seconds) => float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0 : Math.Min(Math.Max(0, seconds), 3155760000d);
    public static string SceneLabel(string scene)
    {
        if (string.IsNullOrEmpty(scene) || scene == "MainMenu") return "Partie préparée";
        if (scene.StartsWith("Maison", StringComparison.OrdinalIgnoreCase)) return "La Maison";
        if (scene.StartsWith("District_", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = scene.Split('_');
            return parts.Length > 1 ? "District " + parts[1] : "District";
        }
        return scene.Replace('_', ' ');
    }
}

// Menu-owned LRU: at most four decoded thumbnails, no decode on repeated hover.
public sealed class MainMenuPreviewCache : IDisposable
{
    private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    private readonly LinkedList<string> order = new LinkedList<string>();
    public Texture2D Get(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        string key = path + "|" + File.GetLastWriteTimeUtc(path).Ticks;
        if (textures.TryGetValue(key, out Texture2D existing))
        { order.Remove(key); order.AddLast(key); return existing; }
        if (new FileInfo(path).Length > 8 * 1024 * 1024) return null;
        byte[] bytes = File.ReadAllBytes(path);
        // PNG IHDR dimensions are checked before allocating a decoded image.
        if (bytes.Length < 24 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71) return null;
        uint width = ReadBigEndian(bytes, 16), height = ReadBigEndian(bytes, 20);
        if (width == 0 || height == 0 || width > 4096 || height > 4096 || (ulong)width * height > 8388608) return null;
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(bytes, true)) { UnityEngine.Object.Destroy(texture); return null; }
        }
        catch { UnityEngine.Object.Destroy(texture); throw; }
        while (textures.Count >= 4)
        { string oldest = order.First.Value; order.RemoveFirst(); UnityEngine.Object.Destroy(textures[oldest]); textures.Remove(oldest); }
        textures[key] = texture; order.AddLast(key);
        return texture;
    }
    private static uint ReadBigEndian(byte[] bytes, int i) => ((uint)bytes[i] << 24) | ((uint)bytes[i + 1] << 16) | ((uint)bytes[i + 2] << 8) | bytes[i + 3];
    public void Dispose()
    {
        foreach (Texture2D texture in textures.Values) UnityEngine.Object.Destroy(texture);
        textures.Clear(); order.Clear();
    }
}
