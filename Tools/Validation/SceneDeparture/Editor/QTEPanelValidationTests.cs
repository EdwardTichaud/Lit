#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class QTEPanelValidationTests
{
    private Scene scene;
    private QTEPanelController panel;
    private string path;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [SetUp]
    public void SetUp()
    {
        Assert.That(File.Exists("CycleSmokeValidationProject.marker"), Is.True, "This saved-scene fixture requires the explicitly disposable validation copy.");
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        path = "Assets/__QTEValidation_" + Guid.NewGuid().ToString("N") + ".unity";
        var root = new GameObject("QTE validation fixture"); root.SetActive(false);
        panel = root.AddComponent<QTEPanelController>();
        Validate();
        Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
        Assert.That(scene.isDirty, Is.False, "The owned baseline was just saved.");
    }

    [TearDown]
    public void TearDown()
    {
        if (scene.IsValid()) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
    }

    [Test]
    public void UnchangedDefaultsDoNotDirtyTheSavedSceneOnValidation()
    {
        string before = EditorJsonUtility.ToJson(panel);
        Validate();
        Assert.That(EditorJsonUtility.ToJson(panel), Is.EqualTo(before));
        Assert.That(scene.isDirty, Is.False, "An unchanged QTE must not force another Bootstrap save after every assembly reload.");
    }

    [Test]
    public void MissingDefaultsAreStillPopulatedAndMarkedForSaving()
    {
        var list = (IList)typeof(QTEPanelController).GetField("inputVisuals", Private).GetValue(panel);
        list.Clear(); // Direct fixture mutation deliberately does not mark the scene.
        Assert.That(scene.isDirty, Is.False);
        Validate();
        Assert.That(list.Count, Is.EqualTo(4));
        Assert.That(scene.isDirty, Is.True, "Real serialized default changes must remain saveable.");
    }

    private void Validate() => typeof(QTEPanelController).GetMethod("OnValidate", Private).Invoke(panel, null);
}
#endif
