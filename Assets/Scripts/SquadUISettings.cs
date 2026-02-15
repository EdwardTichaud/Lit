using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Gere l'UI du SquadPanel (slots, curseur, fade).
[DisallowMultipleComponent]
public class SquadUISettings : MonoBehaviour
{
    public static SquadUISettings Instance { get; private set; }

    [Header("Squad Panel")]
    [Tooltip("Root du panel de squad.")]
    public GameObject squadPanel;
    [Tooltip("Prefab UI d'un membre de la squad.")]
    public GameObject squadUnitUIPrefab;
    [Tooltip("Curseur UI de selection.")]
    public GameObject squadPanelCursor;
    [Tooltip("Offset applique au curseur.")]
    public Vector3 squadPanelCursorOffset;
    [Tooltip("Couronne UI du membre controle.")]
    public GameObject squadPanelCrown;
    [Tooltip("Offset applique a la couronne.")]
    public Vector3 squadPanelCrownOffset;

    [Header("Cursor Navigation")]
    [Tooltip("Deadzone du stick.")]
    public float moveDeadzone = 0.5f;
    [Tooltip("Delai avant repetition.")]
    public float initialRepeatDelay = 0.35f;
    [Tooltip("Intervalle entre repetitions.")]
    public float repeatInterval = 0.12f;
    [Tooltip("Autorise le wrap du curseur.")]
    public bool wrapCursor = false;

    [Header("Squad Panel Fade")]
    [Tooltip("Duree du fade du panel.")]
    public float squadPanelFadeDuration = 0.35f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool squadPanelSetAlphaToZeroOnStart = true;
    [Tooltip("Ajoute un CanvasGroup si manquant.")]
    public bool squadPanelAddCanvasGroupIfMissing = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool squadPanelDisableRaycastsWhenHidden = true;

    [Header("Runtime")]
    [Tooltip("Slots UI instancies pour chaque membre.")]
    public List<GameObject> squadUnitsUI = new List<GameObject>();

    private CanvasGroup squadPanelCanvasGroup;
    private Coroutine squadPanelFadeRoutine;
    private Coroutine crownRefreshRoutine;

    public IReadOnlyList<GameObject> SquadUnitsUI => squadUnitsUI;
    private const int PortraitChildIndex = 1;
    private const int HpChildIndex = 2;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            return;
        }

        Instance = this;
    }

    public void InitializePanel(bool visibleOnStart)
    {
        if (squadPanel == null)
        {
            return;
        }

        squadPanelCanvasGroup = GetSquadPanelCanvasGroup();
        if (squadPanelCanvasGroup == null)
        {
            return;
        }

        if (squadPanelSetAlphaToZeroOnStart)
        {
            float targetAlpha = visibleOnStart ? 1f : 0f;
            SetSquadPanelAlpha(squadPanelCanvasGroup, targetAlpha);
        }
    }

    public void BuildSquadUnits(List<CharacterData> squad)
    {
        ClearSquadUnits();

        if (squadPanel == null || squadUnitUIPrefab == null || squad == null)
        {
            return;
        }

        for (int i = 0; i < squad.Count; i++)
        {
            CharacterData character = squad[i];
            if (character == null)
            {
                continue;
            }

            GameObject squadUnitUI = Instantiate(squadUnitUIPrefab, squadPanel.transform);
            SetPortraitForUnit(squadUnitUI, character.portrait);
            SetHealthForUnit(squadUnitUI, character.hp, character.hp);

            squadUnitsUI.Add(squadUnitUI);
        }
    }

    public void ClearSquadUnits()
    {
        if (squadUnitsUI == null)
        {
            squadUnitsUI = new List<GameObject>();
            return;
        }

        for (int i = squadUnitsUI.Count - 1; i >= 0; i--)
        {
            if (squadUnitsUI[i] != null)
            {
                Destroy(squadUnitsUI[i]);
            }
        }

        squadUnitsUI.Clear();
    }

    public int GetUnitCount()
    {
        return squadUnitsUI != null ? squadUnitsUI.Count : 0;
    }

    public GameObject GetUnitAt(int index)
    {
        if (squadUnitsUI == null || index < 0 || index >= squadUnitsUI.Count)
        {
            return null;
        }

        return squadUnitsUI[index];
    }

    public void RemoveUnitAt(int index)
    {
        if (squadUnitsUI == null || index < 0 || index >= squadUnitsUI.Count)
        {
            return;
        }

        if (squadUnitsUI[index] != null)
        {
            Destroy(squadUnitsUI[index]);
        }

        squadUnitsUI.RemoveAt(index);
    }

    public void SetUnitPortrait(int index, Sprite portrait)
    {
        GameObject unit = GetUnitAt(index);
        if (unit == null)
        {
            return;
        }

        SetPortraitForUnit(unit, portrait);
    }

    public void SetUnitHealth(int index, int currentHp, int maxHp)
    {
        GameObject unit = GetUnitAt(index);
        if (unit == null)
        {
            return;
        }

        SetHealthForUnit(unit, currentHp, maxHp);
    }

    public void UpdateCursorPosition(int index)
    {
        if (squadPanelCursor == null || squadUnitsUI == null || squadUnitsUI.Count == 0)
        {
            return;
        }

        if (index < 0 || index >= squadUnitsUI.Count)
        {
            return;
        }

        squadPanelCursor.transform.position =
            squadUnitsUI[index].transform.position + (Vector3)squadPanelCursorOffset;
    }

    public void UpdateCrownPosition(int index)
    {
        if (squadPanelCrown == null)
        {
            return;
        }

        squadPanelCrown.SetActive(true);

        if (squadUnitsUI == null || squadUnitsUI.Count == 0)
        {
            return;
        }

        if (index < 0 || index >= squadUnitsUI.Count)
        {
            index = 0;
        }

        GameObject unit = squadUnitsUI[index];
        if (unit == null)
        {
            return;
        }

        squadPanelCrown.transform.position = unit.transform.position + (Vector3)squadPanelCrownOffset;
    }

    public void RequestCrownReposition(int index)
    {
        if (!CanRunCoroutines())
        {
            UpdateCrownPosition(index);
            return;
        }

        if (crownRefreshRoutine != null)
        {
            StopCoroutine(crownRefreshRoutine);
        }

        crownRefreshRoutine = StartCoroutine(CrownRepositionRoutine(index));
    }

    public void SetCursorVisible(bool visible)
    {
        if (squadPanelCursor == null)
        {
            return;
        }

        squadPanelCursor.SetActive(visible);
    }

    public void ApplyPanelVisibility(bool visible, bool immediate)
    {
        if (visible)
        {
            ShowSquadPanel(immediate);
        }
        else
        {
            HideSquadPanel(immediate);
        }
    }

    private void ShowSquadPanel(bool immediate)
    {
        if (squadPanel == null)
        {
            return;
        }

        if (!squadPanel.activeSelf)
        {
            squadPanel.SetActive(true);
        }

        squadPanelCanvasGroup = GetSquadPanelCanvasGroup();
        if (squadPanelCanvasGroup == null)
        {
            if (squadPanel.activeSelf)
            {
                squadPanel.SetActive(false);
            }
            return;
        }

        if (immediate)
        {
            SetSquadPanelAlpha(squadPanelCanvasGroup, 1f);
            return;
        }

        if (squadPanelCanvasGroup.alpha <= 0.001f)
        {
            SetSquadPanelAlpha(squadPanelCanvasGroup, 0f);
        }

        FadeSquadPanelTo(1f, squadPanelFadeDuration);
    }

    private void HideSquadPanel(bool immediate)
    {
        if (squadPanel == null)
        {
            return;
        }

        squadPanelCanvasGroup = GetSquadPanelCanvasGroup();
        if (squadPanelCanvasGroup == null)
        {
            return;
        }

        if (immediate)
        {
            SetSquadPanelAlpha(squadPanelCanvasGroup, 0f);
            return;
        }

        FadeSquadPanelTo(0f, squadPanelFadeDuration);
    }

    private CanvasGroup GetSquadPanelCanvasGroup()
    {
        if (squadPanel == null)
        {
            return null;
        }

        CanvasGroup canvasGroup = squadPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null && squadPanelAddCanvasGroupIfMissing)
        {
            canvasGroup = squadPanel.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private void FadeSquadPanelTo(float targetAlpha, float duration)
    {
        CanvasGroup canvasGroup = GetSquadPanelCanvasGroup();
        if (canvasGroup == null)
        {
            return;
        }

        if (!CanRunCoroutines())
        {
            SetSquadPanelAlpha(canvasGroup, targetAlpha);
            return;
        }

        if (squadPanelFadeRoutine != null)
        {
            StopCoroutine(squadPanelFadeRoutine);
        }

        float startAlpha = canvasGroup.alpha;
        if (duration <= 0f)
        {
            SetSquadPanelAlpha(canvasGroup, targetAlpha);
            return;
        }

        squadPanelFadeRoutine = StartCoroutine(SquadPanelFadeRoutine(canvasGroup, startAlpha, targetAlpha, duration));
    }

    private IEnumerator SquadPanelFadeRoutine(CanvasGroup canvasGroup, float startAlpha, float targetAlpha, float duration)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        float time = 0f;
        if (squadPanelDisableRaycastsWhenHidden)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        SetSquadPanelAlpha(canvasGroup, targetAlpha);
    }

    private void SetSquadPanelAlpha(CanvasGroup canvasGroup, float alpha)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = alpha;
        if (squadPanelDisableRaycastsWhenHidden)
        {
            bool visible = alpha > 0.001f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    private bool CanRunCoroutines()
    {
        return isActiveAndEnabled && gameObject.activeInHierarchy;
    }

    private IEnumerator CrownRepositionRoutine(int index)
    {
        yield return null;

        if (squadPanel != null)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = squadPanel.GetComponent<RectTransform>();
            if (rect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }

        UpdateCrownPosition(index);
        crownRefreshRoutine = null;
    }

    private void SetPortraitForUnit(GameObject unit, Sprite portrait)
    {
        Image image = GetChildComponent<Image>(unit, PortraitChildIndex);
        if (image != null)
        {
            image.sprite = portrait;
        }
    }

    private void SetHealthForUnit(GameObject unit, int currentHp, int maxHp)
    {
        Transform hpRoot = GetChildTransform(unit, HpChildIndex);
        if (hpRoot == null)
        {
            return;
        }

        string textValue = BuildHpText(currentHp, maxHp);
        TMP_Text tmp = hpRoot.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = textValue;
        }

        Text legacyText = hpRoot.GetComponent<Text>();
        if (legacyText != null)
        {
            legacyText.text = textValue;
        }

        Image fill = hpRoot.GetComponent<Image>();
        if (fill != null)
        {
            float ratio = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
            fill.fillAmount = ratio;
        }
    }

    private Transform GetChildTransform(GameObject unit, int index)
    {
        if (unit == null)
        {
            return null;
        }

        Transform root = unit.transform;
        if (index < 0 || index >= root.childCount)
        {
            return null;
        }

        return root.GetChild(index);
    }

    private T GetChildComponent<T>(GameObject unit, int index) where T : Component
    {
        Transform child = GetChildTransform(unit, index);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static string BuildHpText(int currentHp, int maxHp)
    {
        if (maxHp > 0)
        {
            return $"{currentHp}/{maxHp}";
        }

        return currentHp.ToString();
    }
}
