using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class MainMenuLoadBrowserTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private MainMenuController menu;
    private RectTransform sessions;
    private RectTransform saves;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Load browser test");
        root.SetActive(false);
        menu = root.AddComponent<MainMenuController>();
        sessions = Child("Sessions");
        saves = Child("Saves");
        Set("sessionsRoot", sessions);
        Set("savesRoot", saves);
        FieldInfo state = typeof(MainMenuController).GetField("currentMenu", Private);
        state.SetValue(menu, Enum.Parse(state.FieldType, "LoadMenu"));
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void HoveringAnotherSessionPreservesChosenSessionAndSaveEntries()
    {
        var chosen = new SaveSessionInfo { sessionId = "chosen", sessionName = "Chosen" };
        Set("selectedSession", chosen);
        RectTransform existing = Child("Existing save");
        existing.SetParent(saves, false);
        var entry = Child("Other session").gameObject.AddComponent<MainMenuSessionEntryUI>();
        entry.Initialize(menu, new SaveSessionInfo { sessionId = "other" });
        Invoke("OnSessionHovered", entry);
        Assert.That(Get("selectedSession"), Is.SameAs(chosen));
        Assert.That(saves.childCount, Is.EqualTo(1));
        Assert.That(existing.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void BackFromSavesReturnsToSessionsAndPreservesTheChosenSession()
    {
        var chosen = new SaveSessionInfo { sessionId = "chosen" };
        Set("selectedSession", chosen);
        Set("browsingSaves", true);
        Assert.That(menu.NavigationModalRoot, Is.SameAs(saves));
        menu.UI_Back();
        Assert.That(menu.NavigationModalRoot, Is.SameAs(sessions));
        Assert.That(Get("selectedSession"), Is.SameAs(chosen));
    }

    [Test]
    public void SaveFromAnotherSessionCannotReplaceTheSelection()
    {
        Set("selectedSession", new SaveSessionInfo { sessionId = "chosen" });
        var original = new SaveSlotInfo { sessionId = "chosen", saveId = "original" };
        Set("selectedSave", original);
        Invoke("OnSaveSelected", new SaveSlotInfo { sessionId = "other" }, null, false);
        Assert.That(Get("selectedSave"), Is.SameAs(original));
    }

    [Test]
    public void SaveDetailsContainItsDistrictAndName()
    {
        TMP_Text body = Child("Details").gameObject.AddComponent<TextMeshProUGUI>();
        Set("detailsBody", body);
        Invoke("ShowSaveDetails", new SaveSlotInfo { sceneName = "District_2_Rooms", saveName = "Avant le combat" });
        Assert.That(body.text, Does.Contain("District 2"));
        Assert.That(body.text, Does.Contain("Avant le combat"));
    }

    [Test]
    public void SceneBindsTheActualPreviewAndBothListsCanGrowAndScroll()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/MainMenu/MainMenu.unity");
        try
        {
            MainMenuController controller = null;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                if (controller == null) controller = sceneRoot.GetComponentInChildren<MainMenuController>(true);
            Assert.That(controller, Is.Not.Null);
            var serialized = new SerializedObject(controller);
            var image = serialized.FindProperty("previewImage").objectReferenceValue as RawImage;
            Assert.That(image, Is.Not.Null);
            Assert.That(image.GetComponent<AspectRatioFitter>(), Is.Not.Null);
            foreach (string name in new[] { "sessionsRoot", "savesRoot" })
            {
                var content = serialized.FindProperty(name).objectReferenceValue as RectTransform;
                Assert.That(content.GetComponent<ContentSizeFitter>().verticalFit, Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
                var scroll = content.GetComponentInParent<ScrollRect>();
                Assert.That(scroll.content, Is.SameAs(content));
                Assert.That(scroll.horizontal, Is.False);
            }

            ConfirmationManager confirmationManager = null;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                confirmationManager = sceneRoot.GetComponentInChildren<ConfirmationManager>(true);
                if (confirmationManager != null)
                {
                    break;
                }
            }

            Assert.That(confirmationManager, Is.Not.Null);
            var confirmation = new SerializedObject(confirmationManager);
            Assert.That(confirmation.FindProperty("confirmationBox").objectReferenceValue, Is.Not.Null);
            Assert.That(confirmation.FindProperty("createRuntimeFallback").boolValue, Is.False);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void NavigationRejectsThreeDimensionalDecorations()
    {
        var navigation = root.AddComponent<MainMenuNavigation>();
        var decoration = new GameObject("3D decoration");
        var canvas = new GameObject("Canvas", typeof(Canvas));
        var control = new GameObject("Menu control", typeof(RectTransform));
        control.transform.SetParent(canvas.transform, false);
        try
        {
            MethodInfo usable = typeof(MainMenuNavigation).GetMethod("Usable", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(usable.Invoke(null, new object[] { decoration }), Is.False);
            Assert.That(usable.Invoke(null, new object[] { control }), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(decoration);
            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(navigation);
        }
    }

    private RectTransform Child(string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(root.transform, false);
        return (RectTransform)child.transform;
    }
    private void Set(string name, object value) => typeof(MainMenuController).GetField(name, Private).SetValue(menu, value);
    private object Get(string name) => typeof(MainMenuController).GetField(name, Private).GetValue(menu);
    private void Invoke(string name, params object[] args) => typeof(MainMenuController).GetMethod(name, Private).Invoke(menu, args);
}
