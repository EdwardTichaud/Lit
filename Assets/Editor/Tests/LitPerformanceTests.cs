using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LitPerformanceTests
{
    [Test]
    public void NetworkPrefabReferencesSurviveShutdownThenClearInEditMode()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var type = typeof(NetcodePrefabRegistry);
        var callback = type.GetMethod("OnEditorPlayModeChanged", flags);
        Assert.That(callback, Is.Not.Null);
        var names = new[] { "itemInfos", "characterInfos", "sceneMarkerCharacterInfos" };
        var nested = new[] { "ItemSpawnInfo", "CharacterSpawnInfo", "SceneMarkerCharacterSpawnInfo" };
        var saved = new List<DictionaryEntry>[names.Length];
        var dictionaries = new IDictionary[names.Length];
        var hashes = (HashSet<uint>)type.GetField("registeredHashes", flags).GetValue(null);
        var savedHashes = new List<uint>(hashes);
        var initialized = type.GetField("initialized", flags);
        bool wasInitialized = (bool)initialized.GetValue(null);
        var prefab = new GameObject("Network prefab cache retention regression");
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                dictionaries[i] = (IDictionary)type.GetField(names[i], flags).GetValue(null);
                saved[i] = new List<DictionaryEntry>();
                foreach (DictionaryEntry entry in dictionaries[i]) saved[i].Add(entry);
                dictionaries[i].Clear();
                var infoType = type.GetNestedType(nested[i], BindingFlags.NonPublic);
                var info = System.Activator.CreateInstance(infoType, true);
                infoType.GetField("sourcePrefab")?.SetValue(info, prefab);
                dictionaries[i].Add("cache-retention-test", info);
            }
            hashes.Add(123456789);
            initialized.SetValue(null, true);
            callback.Invoke(null, new object[] { PlayModeStateChange.ExitingPlayMode });
            foreach (var dictionary in dictionaries) Assert.That(dictionary.Count, Is.EqualTo(1), "Keep spawn data until shutdown completes.");
            callback.Invoke(null, new object[] { PlayModeStateChange.EnteredEditMode });
            foreach (var dictionary in dictionaries) Assert.That(dictionary.Count, Is.Zero, "Edit Mode must not retain runtime prefab references.");
            Assert.That(hashes, Is.Empty);
            Assert.That(initialized.GetValue(null), Is.False, "The next session must rebuild its registry.");
        }
        finally
        {
            for (int i = 0; i < dictionaries.Length; i++)
            {
                if (dictionaries[i] == null) continue;
                dictionaries[i].Clear();
                foreach (var entry in saved[i]) dictionaries[i].Add(entry.Key, entry.Value);
            }
            hashes.Clear(); foreach (var hash in savedHashes) hashes.Add(hash);
            initialized.SetValue(null, wasInitialized);
            Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void HiddenLoadingOrbStopsRenderingAndCanRestart()
    {
        var root = new GameObject("Loading orb camera regression");
        root.SetActive(false);
        var texture = new RenderTexture(16, 16, 0);
        try
        {
            var service = root.AddComponent<LoadingScreenService>();
            var camera = root.AddComponent<Camera>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(LoadingScreenService).GetField("loadingOrbCamera", flags).SetValue(service, camera);
            var stop = typeof(LoadingScreenService).GetMethod("StopLoadingOrb", flags);
            camera.enabled = true;
            stop.Invoke(service, null);
            Assert.That(camera.enabled, Is.False, "Even a missing particle prefab must not leave a hidden rendering camera.");
            typeof(LoadingScreenService).GetField("loadingOrbTexture", flags).SetValue(service, texture);
            typeof(LoadingScreenService).GetMethod("EnsureLoadingOrbCameraReady", flags).Invoke(service, null);
            Assert.That(camera.enabled, Is.True, "The next loading presentation must resume rendering.");
            stop.Invoke(service, null);
            Assert.That(camera.enabled, Is.False);
        }
        finally { Object.DestroyImmediate(root); if (texture != null) Object.DestroyImmediate(texture); }
    }

    [Test]
    public void AnimatorLookupTracksControllerAndAnimatorChanges()
    {
        var root = new GameObject("Animator metadata regression");
        var other = new GameObject("Replacement animator");
        var first = new AnimatorController();
        var second = new AnimatorController();
        first.AddLayer("Base Layer"); first.layers[0].stateMachine.AddState("Idle");
        second.AddLayer("Base Layer"); second.layers[0].stateMachine.AddState("Idle");
        first.AddParameter("Speed", AnimatorControllerParameterType.Float);
        second.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        var overrides = new AnimatorOverrideController(first);
        try
        {
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = first;
            var lookup = new AnimatorParameterLookup();
            Assert.That(lookup.Has(animator, "Speed", AnimatorControllerParameterType.Float), Is.True);
            Assert.That(lookup.Has(animator, "Speed", AnimatorControllerParameterType.Bool), Is.False);
            Assert.That(lookup.Has(animator, "Missing", AnimatorControllerParameterType.Float), Is.False);
            animator.runtimeAnimatorController = overrides;
            Assert.That(lookup.Has(animator, "Speed", AnimatorControllerParameterType.Float), Is.True);
            overrides.runtimeAnimatorController = second;
            animator.Rebind(); animator.Update(0);
            Assert.That(lookup.Has(animator, "Moving", AnimatorControllerParameterType.Bool), Is.True);
            Assert.That(lookup.Has(animator, "Speed", AnimatorControllerParameterType.Float), Is.False);
            second.AddParameter("Added", AnimatorControllerParameterType.Trigger);
            animator.Rebind(); animator.Update(0);
            Assert.That(lookup.Has(animator, "Added", AnimatorControllerParameterType.Trigger), Is.True);
            var replacement = other.AddComponent<Animator>();
            replacement.runtimeAnimatorController = first;
            Assert.That(lookup.Has(replacement, "Speed", AnimatorControllerParameterType.Float), Is.True);
            Assert.That(lookup.Has(null, "Speed", AnimatorControllerParameterType.Float), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(other);
            Object.DestroyImmediate(overrides); Object.DestroyImmediate(first); Object.DestroyImmediate(second);
        }
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterFailure()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    [Test]
    public void FlickeringLightUsesExplicitEditorPreviewAndRestoresPosition()
    {
        var root = new GameObject("Light preview regression");
        root.SetActive(false);
        try
        {
            root.transform.localPosition = new Vector3(2, 3, 4);
            var light = root.AddComponent<Light>();
            var flicker = root.AddComponent<FlickeringLight>();
            flicker.ConfigureForFlame(light);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var move = (System.Action<float>)System.Delegate.CreateDelegate(typeof(System.Action<float>), flicker,
                typeof(FlickeringLight).GetMethod("ApplyMovement", flags));
            root.SetActive(true);
            move(2);
            Assert.That(root.transform.localPosition, Is.EqualTo(new Vector3(2, 3, 4)));
            typeof(FlickeringLight).GetField("previewInEditMode", flags).SetValue(flicker, true);
            move(2);
            Assert.That(root.transform.localPosition, Is.Not.EqualTo(new Vector3(2, 3, 4)));
            typeof(FlickeringLight).GetField("previewInEditMode", flags).SetValue(flicker, false);
            move(2);
            Assert.That(root.transform.localPosition, Is.EqualTo(new Vector3(2, 3, 4)), "Stopping preview restores the authored pose.");
            root.transform.localPosition = new Vector3(5, 6, 7);
            flicker.enabled = false;
            Assert.That(root.transform.localPosition, Is.EqualTo(new Vector3(5, 6, 7)), "Without preview, disabling must preserve manual edits.");
            flicker.enabled = true;
            typeof(FlickeringLight).GetField("previewInEditMode", flags).SetValue(flicker, true);
            move(2);
            light.enabled = false;
            var previous = root.transform.localPosition;
            move(3);
            Assert.That(root.transform.localPosition, Is.EqualTo(previous));
            flicker.enabled = false;
            Assert.That(root.transform.localPosition, Is.EqualTo(new Vector3(5, 6, 7)));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator FlameRegistryFollowsActivationAndRebuildsWithoutSceneReload()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var root = new GameObject("Flame registry regression");
        root.SetActive(false);
        var flame = root.AddComponent<Flame>();
        var list = new List<Flame>();
        Flame.CopyActiveInfluenceFlames(list);
        Assert.That(list, Has.No.Member(flame));
        root.SetActive(true);
        Flame.CopyActiveInfluenceFlames(list);
        Assert.That(list, Has.Member(flame));
        flame.enabled = false;
        Flame.CopyActiveInfluenceFlames(list);
        Assert.That(list, Has.No.Member(flame));
        flame.enabled = true;
        // Exercise the precise SubsystemRegistration/AfterSceneLoad path used
        // with reload disabled, without synthesizing OnEnable in between.
        typeof(Flame).GetMethod("ResetInfluenceRegistry", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        Flame.CopyActiveInfluenceFlames(list);
        Assert.That(list, Has.Member(flame));
        Flame.CopyActiveInfluenceFlames(list);
        Assert.That(list.FindAll(f => f == flame).Count, Is.EqualTo(1));
        Object.Destroy(root);
        yield return null;
        Flame.CopyActiveInfluenceFlames(list);
        Assert.That(list, Has.No.Member(flame));
        yield return new ExitPlayMode();
    }
}
