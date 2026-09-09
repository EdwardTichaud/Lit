using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Transient learning feedback, using the same presentation as knowledge unlocks.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UiPanel))]
public sealed class SkillUnlockPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UiPanel panel;
    [SerializeField] private TMP_Text messageText;
    [Header("Display")]
    [SerializeField, Min(0f)] private float displayDuration = 4.5f;
    [SerializeField] private string heading = "COMPÉTENCE APPRISE";

    private readonly Queue<string> pendingMessages = new Queue<string>();
    private bool displaying;
    private double hideAt;

    private void Awake() => ResolveReferences();

    private void ResolveReferences()
    {
        if (panel == null) panel = GetComponent<UiPanel>();
        if (messageText == null) messageText = GetComponentInChildren<TMP_Text>(true);
    }

    // Call only after a successful new grant, never while restoring saved skills.
    public static bool TryShow(SkillSO skill) => skill != null && TryShow(skill.SkillName, skill.description);
    public static bool TryShow(StatsSO skill) => skill != null && TryShow(
        string.IsNullOrWhiteSpace(skill.skillName) ? skill.name : skill.skillName, skill.description);

    private static bool TryShow(string title, string description)
    {
        var presenter = FindAnyObjectByType<SkillUnlockPanel>(FindObjectsInactive.Include);
        if (presenter == null || !presenter.enabled) return false;
        if (!presenter.gameObject.activeSelf) presenter.gameObject.SetActive(true);
        if (!presenter.isActiveAndEnabled) return false;
        presenter.ResolveReferences();
        if (presenter.panel == null || presenter.messageText == null) return false;
        presenter.pendingMessages.Enqueue(presenter.FormatMessage(title, description));
        if (!presenter.displaying) presenter.ShowNext();
        return true;
    }

    private string FormatMessage(string title, string description)
    {
        string message = $"{heading}\n<size=140%>{title}</size>";
        return string.IsNullOrWhiteSpace(description) ? message : $"{message}\n\n{description.Trim()}";
    }

    private void ShowNext()
    {
        displaying = true;
        messageText.text = pendingMessages.Dequeue();
        hideAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(0f, displayDuration);
        panel.Show();
    }

    private void Update()
    {
        if (!displaying || Time.realtimeSinceStartupAsDouble < hideAt) return;
        if (pendingMessages.Count > 0) ShowNext();
        else
        {
            displaying = false;
            panel.Hide();
        }
    }

    private void OnDisable()
    {
        pendingMessages.Clear();
        displaying = false;
        if (panel != null) panel.Hide(true);
    }
}
