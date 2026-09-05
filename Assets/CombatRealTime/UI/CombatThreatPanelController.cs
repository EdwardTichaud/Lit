using System.Collections;
using UnityEngine;

/// <summary>
/// Owns the existing scene-authored "EN GARDE !" panel. It deliberately uses
/// the explicit enemy-aggro event. Attack telegraphs and reaction windows do
/// not reopen this entry banner.
/// </summary>
[DefaultExecutionOrder(800)]
[DisallowMultipleComponent]
public sealed class CombatThreatPanelController : MonoBehaviour
{
    [SerializeField] private RealTimeCombatManager combatManager;
    [SerializeField] private CanvasGroup threatPanel;
    [SerializeField] private Animator threatAnimator;
    [SerializeField] private string triggerName = "CombatEngagedPanel_Trigger";
    [SerializeField, Min(0f)] private float fallbackVisibleSeconds = 1f;
    [SerializeField] private bool findScenePanelWhenUnassigned = true;

    private bool visible;
    private Coroutine hideRoutine;

    private void Awake()
    {
        ResolveReferences();
        SetVisible(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        RealTimeCombatManager resolvedManager = combatManager;
        Bind(null);
        Bind(resolvedManager);
    }

    private void OnDisable()
    {
        Bind(null);
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (combatManager == null && RealTimeCombatManager.Instance != null)
        {
            Bind(RealTimeCombatManager.Instance);
        }

        // The legacy scene UI controller still owns the combat-entry animation.
        // This late pass keeps this panel strictly threat-driven while the HUD
        // remains managed by that controller.
        if (!visible && threatPanel != null && threatPanel.gameObject.activeSelf)
        {
            SetVisible(false);
        }
    }

    private void OnEnemyAggroStarted(RealTimeCombatEnemy enemy)
    {
        if (enemy != null)
        {
            ShowAggro();
        }
    }

    private void OnCombatStateChanged(bool active)
    {
        if (!active)
        {
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
            SetVisible(false);
        }
    }

    private void ShowAggro()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        SetVisible(true);
        hideRoutine = StartCoroutine(HideAfterIntro());
    }

    private IEnumerator HideAfterIntro()
    {
        yield return null;
        float duration = fallbackVisibleSeconds;
        if (threatAnimator != null)
        {
            AnimatorClipInfo[] clips = threatAnimator.GetCurrentAnimatorClipInfo(0);
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].clip != null)
                {
                    duration = Mathf.Max(duration, clips[i].clip.length / Mathf.Max(0.01f, threatAnimator.speed));
                }
            }
        }

        if (duration > 0f)
        {
            yield return new WaitForSecondsRealtime(duration);
        }

        hideRoutine = null;
        SetVisible(false);
    }

    private void Bind(RealTimeCombatManager next)
    {
        if (combatManager == next)
        {
            return;
        }

        if (combatManager != null)
        {
            combatManager.EnemyAggroStarted -= OnEnemyAggroStarted;
            combatManager.CombatStateChanged -= OnCombatStateChanged;
        }

        combatManager = next;
        if (combatManager != null)
        {
            combatManager.EnemyAggroStarted += OnEnemyAggroStarted;
            combatManager.CombatStateChanged += OnCombatStateChanged;
        }
    }

    private void ResolveReferences()
    {
        if (combatManager == null)
        {
            combatManager = GetComponent<RealTimeCombatManager>();
        }

        if (threatPanel != null || !findScenePanelWhenUnassigned)
        {
            return;
        }

        CanvasGroup[] groups = FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null || groups[i].name != "CombatEngagedPanel")
            {
                continue;
            }

            threatPanel = groups[i];
            threatAnimator = groups[i].GetComponent<Animator>();
            break;
        }
    }

    private void SetVisible(bool shouldBeVisible)
    {
        if (threatPanel == null)
        {
            return;
        }

        if (visible == shouldBeVisible && threatPanel.gameObject.activeSelf == shouldBeVisible)
        {
            return;
        }

        visible = shouldBeVisible;
        threatPanel.gameObject.SetActive(shouldBeVisible);
        threatPanel.alpha = shouldBeVisible ? 1f : 0f;
        threatPanel.interactable = false;
        threatPanel.blocksRaycasts = false;
        if (shouldBeVisible && threatAnimator != null && !string.IsNullOrWhiteSpace(triggerName))
        {
            threatAnimator.ResetTrigger(triggerName);
            threatAnimator.SetTrigger(triggerName);
        }
    }
}
