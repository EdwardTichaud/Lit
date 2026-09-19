using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class WorldInteractionUiTests
{
    [Test]
    public void SharedPromptPreservesPrefabStyleAndLegacyLabelWithoutChangingAssets()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/World/UI_World_InteractionBox.prefab");
        Assert.That(prefab, Is.Not.Null);
        var source = new GameObject("Legacy prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
        GameObject instance = null;
        try
        {
            source.GetComponent<TMP_Text>().text = "Traverser";
            source.GetComponent<TMP_Text>().fontSize = 99;
            float originalAlpha = prefab.GetComponent<CanvasGroup>().alpha;
            string originalText = prefab.GetComponent<TMP_Text>().text;
            instance = WorldInteractionUiSettings.Create(null, source);
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.GetComponent<TMP_Text>().text, Is.EqualTo("Traverser"));
            Assert.That(instance.GetComponent<TMP_Text>().fontSize, Is.EqualTo(prefab.GetComponent<TMP_Text>().fontSize));
            Assert.That(instance.transform.localScale, Is.EqualTo(prefab.transform.localScale));
            Assert.That(instance.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Assert.That(instance.GetComponent<TMP_Text>().raycastTarget, Is.False);
            Assert.That(prefab.GetComponent<CanvasGroup>().alpha, Is.EqualTo(originalAlpha));
            Assert.That(prefab.GetComponent<TMP_Text>().text, Is.EqualTo(originalText));
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(source);
        }
    }
}
