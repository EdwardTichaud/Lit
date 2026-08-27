using NUnit.Framework;
using UnityEngine;

public sealed class UiPanelTests
{
    [Test]
    public void ImmediateVisibilitySynchronizesCanvasGroupAndRaycasts()
    {
        GameObject panelObject = new GameObject("UiPanelTest", typeof(CanvasGroup), typeof(UiPanel));
        try
        {
            UiPanel panel = panelObject.GetComponent<UiPanel>();
            CanvasGroup group = panelObject.GetComponent<CanvasGroup>();

            panel.Show(true);
            Assert.That(panel.IsVisible, Is.True);
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(group.blocksRaycasts, Is.True);

            panel.Hide(true);
            Assert.That(panel.IsVisible, Is.False);
            Assert.That(group.alpha, Is.EqualTo(0f));
            Assert.That(group.interactable, Is.False);
            Assert.That(group.blocksRaycasts, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
        }
    }
}
