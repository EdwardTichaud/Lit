using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SaveScreenshotTests
{
    private string directory;
    private const string Root = "Library/SaveScreenshots/Unit";
    [SetUp] public void Setup() { directory = Path.GetFullPath(Path.Combine(Root, Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(directory); }
    [TearDown] public void Cleanup()
    {
        if (Path.GetDirectoryName(directory) != Path.GetFullPath(Root)) throw new InvalidOperationException("Unsafe test cleanup.");
        Directory.Delete(directory, true);
    }
    [Test] public void FourKIsReducedWithoutChangingAspectRatio()
    {
        Assert.That(SaveScreenshotCapture.GetSize(3840, 2160, 1280), Is.EqualTo(new Vector2Int(1280, 720)));
        Assert.That(SaveScreenshotCapture.GetSize(800, 600, 1280), Is.EqualTo(new Vector2Int(800, 600)));
    }
    [Test] public void PortraitUltraWideAndSquareStayWithinTheMenuPreviewBudget()
    {
        foreach (var size in new[] { new Vector2Int(2160, 3840), new Vector2Int(12000, 2160), new Vector2Int(8000, 8000) })
        {
            var actual = SaveScreenshotCapture.GetSize(size.x, size.y, 1920);
            Assert.That(Math.Max(actual.x, actual.y), Is.LessThanOrEqualTo(1920));
            Assert.That((long)actual.x * actual.y, Is.LessThanOrEqualTo(SaveScreenshotCapture.MaximumPixels));
            Assert.That(Math.Abs(actual.y - (double)size.y * actual.x / size.x), Is.LessThanOrEqualTo(1d));
        }
    }
    [Test] public void InvalidDimensionsCannotCreateAnEmptyOrOversizedTexture()
    {
        Assert.That(SaveScreenshotCapture.GetSize(0, 0, int.MaxValue), Is.EqualTo(Vector2Int.one));
        var actual = SaveScreenshotCapture.GetSize(int.MaxValue, int.MaxValue, int.MaxValue);
        Assert.That(actual.x, Is.InRange(1, 1920)); Assert.That((long)actual.x * actual.y, Is.LessThanOrEqualTo(SaveScreenshotCapture.MaximumPixels));
    }
    [Test] public void MissingCameraDoesNotTouchTheActiveRenderTarget()
    {
        var original = RenderTexture.active;
        Assert.That(SaveScreenshotCapture.TryCapture(null, 1280, out var bytes, out string issue), Is.False);
        Assert.That(bytes, Is.Null); Assert.That(issue, Is.Not.Empty); Assert.That(RenderTexture.active, Is.SameAs(original));
    }
    [Test] public void ScreenshotPathBelongsToTheCommittedSlotAndRejectsParentTraversal()
    {
        var go = new GameObject("Inactive screenshot store"); go.SetActive(false);
        try
        {
            var store = go.AddComponent<CharacterStateStore>();
            var method = typeof(CharacterStateStore).GetMethod("GetScreenshotPath", BindingFlags.Instance | BindingFlags.NonPublic);
            string character = Path.Combine(directory, "slot-a", "CharacterState.json");
            Assert.That(method.Invoke(store, new object[] { character }), Is.EqualTo(Path.Combine(directory, "slot-a", "screenshot.png")));
            store.screenshotFileName = "../other.png";
            Assert.That(method.Invoke(store, new object[] { character }), Is.Null);
        }
        finally { Object.DestroyImmediate(go); }
    }
    [Test] public void PreviewAndSaveFilesPublishInTheSameTransaction()
    {
        string json = Path.Combine(directory, "CharacterState.json"), photo = Path.Combine(directory, "screenshot.png");
        File.WriteAllText(json, "old"); File.WriteAllBytes(photo, new byte[] { 1, 2 });
        SaveMetadataWriter.WriteBatchAtomic(new Dictionary<string, byte[]> {
            { json, new byte[] { 9 } }, { photo, new byte[] { 3, 4, 5 } } });
        CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(json));
        CollectionAssert.AreEqual(new byte[] { 3, 4, 5 }, File.ReadAllBytes(photo));
    }
    [Test] public void FailedPublicationRestoresThePreviousPhotoTogetherWithTheSave()
    {
        string json = Path.Combine(directory, "CharacterState.json"), photo = Path.Combine(directory, "screenshot.png");
        File.WriteAllText(json, "old"); File.WriteAllBytes(photo, new byte[] { 1, 2 });
        string blocked = Path.Combine(directory, "z-blocked"); Directory.CreateDirectory(blocked);
        Assert.That(() => SaveMetadataWriter.WriteBatchAtomic(new Dictionary<string, byte[]> {
            { json, new byte[] { 9 } }, { photo, new byte[] { 3, 4, 5 } }, { blocked, new byte[] { 6 } } }), Throws.Exception);
        Assert.That(File.ReadAllText(json), Is.EqualTo("old"));
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, File.ReadAllBytes(photo));
        Assert.That(File.Exists(Path.Combine(directory, ".lit-save-journal")), Is.False);
    }
}

[InitializeOnLoad]
internal static class SaveScreenshotNativeChecks
{
    static SaveScreenshotNativeChecks() => EditorApplication.update += Poll;
    private static void Poll()
    {
        const string folder = "Library/SaveScreenshots";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(folder + "/Checks.request")) return;
        File.Delete(folder + "/Checks.request");
        int pass = 0, fail = 0; var text = new System.Text.StringBuilder();
        foreach (var method in typeof(SaveScreenshotTests).GetMethods().Where(method => method.IsDefined(typeof(TestAttribute), false)))
        {
            var test = new SaveScreenshotTests();
            try { test.Setup(); method.Invoke(test, null); pass++; text.AppendLine("PASS " + method.Name); }
            catch (Exception exception) { fail++; text.AppendLine("FAIL " + method.Name + "\n" + (exception.InnerException ?? exception)); }
            finally { test.Cleanup(); }
        }
        File.WriteAllText(folder + "/NativeChecks.txt", $"{pass} passed; {fail} failed. Native NUnit assertions.\n" + text);
    }
}
