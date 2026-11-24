using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ZoneAudioPresentationTests
{
    [Test]
    public void RepeatedMusicRequestDoesNotRestartTheIncomingTrack()
    {
        var root = new GameObject("Music transition fixture"); root.SetActive(false);
        var clip = ScriptableObject.CreateInstance<AudioClipSO>();
        var sound = AudioClip.Create("Menu", 100, 1, 100, false);
        try
        {
            clip.audioClip = sound;
            var manager = root.AddComponent<AudioManager>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(AudioManager).GetField("requestedMusicClip", flags).SetValue(manager, clip);
            typeof(AudioManager).GetField("musicTransitionActive", flags).SetValue(manager, true);
            manager.PlayMusic(clip);
            Assert.That(typeof(AudioManager).GetField("fadeRoutine", flags).GetValue(manager), Is.Null,
                "An identical pending request must not start a new fade.");
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(clip); Object.DestroyImmediate(sound); }
    }

    [Test]
    public void MenuHasTheSameZonePresentationAndOneMusic()
    {
        var profile = AssetDatabase.LoadAssetAtPath<ZoneAudioProfileSO>("Assets/Scenes/MainMenu/MainMenu_ZoneAudio.asset");
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.music, Is.Not.Null);
        Assert.That(profile.Ambiences, Is.Not.Null);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/MainMenu/MainMenu.unity");
        try
        {
            ZoneRuntimeContext context = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<MainMenuController>() != null) context = root.GetComponent<ZoneRuntimeContext>();
            Assert.That(context, Is.Not.Null);
            var serialized = new SerializedObject(context);
            Assert.That(serialized.FindProperty("audioProfile").objectReferenceValue, Is.SameAs(profile));
            Assert.That(serialized.FindProperty("startOnEnable").boolValue, Is.True);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void AmbienceLayersAreDeduplicatedReusedAndComposed()
    {
        var root = new GameObject("Audio fixture"); root.SetActive(false);
        var a = ScriptableObject.CreateInstance<AudioClipSO>();
        var b = ScriptableObject.CreateInstance<AudioClipSO>();
        var sound = AudioClip.Create("Loop", 100, 1, 100, false);
        try
        {
            a.audioClip = b.audioClip = sound;
            var manager = root.AddComponent<AudioManager>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var set = typeof(AudioManager).GetMethod("SetAmbienceLayers", flags);
            var layers = (IList)typeof(AudioManager).GetField("ambienceLayers", flags).GetValue(manager);
            set.Invoke(manager, new object[] { new[] { a, a, b, null }, 1f });
            Assert.That(layers.Count, Is.EqualTo(2));
            var first = layers[0];
            set.Invoke(manager, new object[] { new[] { a }, 1f });
            Assert.That(layers.Count, Is.EqualTo(2), "Outgoing layer fades instead of being replaced abruptly.");
            Assert.That(layers[0], Is.SameAs(first));
        }
        finally
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(a);
            Object.DestroyImmediate(b); Object.DestroyImmediate(sound);
        }
    }
}
