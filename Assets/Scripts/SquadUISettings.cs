using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
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
    [Tooltip("Croix UI du membre controle par un joueur.")]
    public GameObject squadPanelCross;
    [Tooltip("Offset applique a la croix.")]
    public Vector3 squadPanelCrossOffset;

    [Header("Player Crowns")]
    [SerializeField] private Color localPlayerCrownColor = new Color(0.25f, 0.6f, 1f, 1f);
    [SerializeField] private Color otherPlayerCrownColor = Color.white;
    [SerializeField] private bool overrideOtherPlayerCrownColor = false;

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

    [Header("Squad Panel Visibility")]
    [Tooltip("Garde le panel visible meme quand inactif.")]
    public bool keepPanelVisibleWhenInactive = false;
    [Tooltip("Alpha applique quand le panel est actif.")]
    public float squadPanelActiveAlpha = 1f;
    [Tooltip("Alpha applique quand le panel est inactif.")]
    public float squadPanelInactiveAlpha = 1f;

    [Header("Squad Panel Scale")]
    [Tooltip("Agrandit le panel quand le squad panel est actif.")]
    public bool scalePanelWhenActive = true;
    [Tooltip("Multiplicateur d'echelle applique quand actif.")]
    public float squadPanelActiveScale = 1.2f;

    [Header("Runtime")]
    [Tooltip("Slots UI instancies pour chaque membre.")]
    public List<GameObject> squadUnitsUI = new List<GameObject>();

    private CanvasGroup squadPanelCanvasGroup;
    private Coroutine squadPanelFadeRoutine;
    private Coroutine crownRefreshRoutine;
    private Vector3 basePanelScale = Vector3.one;
    private bool baseScaleCached;
    private GameObject crownPrefab;
    private bool panelActiveState;
    private readonly Dictionary<ulong, GameObject> playerCrowns = new Dictionary<ulong, GameObject>();
    private WorldInteractionService subscribedService;
    private bool assignmentsDirty;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

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
        ResolvePlayerMarkerPrefab();
    }

    private void OnEnable()
    {
        ResolvePlayerMarkerPrefab();
        LocalPlayerContext.LocalCharacterChanged += OnLocalCharacterChanged;
        assignmentsDirty = true;
    }

    private void OnDisable()
    {
        LocalPlayerContext.LocalCharacterChanged -= OnLocalCharacterChanged;
        UnsubscribeAssignments();
    }

    private void ResolvePlayerMarkerPrefab()
    {
        crownPrefab = squadPanelCross;
        if (crownPrefab != null && crownPrefab.scene.IsValid())
        {
            crownPrefab.SetActive(false);
        }
    }

    public void InitializePanel(bool visibleOnStart)
    {
        if (squadPanel == null)
        {
            return;
        }

        bool shouldShowPanelRoot = visibleOnStart || keepPanelVisibleWhenInactive;
        if (shouldShowPanelRoot)
        {
            GameObject panelRoot = GetSquadPanelCanvasGroupHost();
            if (panelRoot != null && !panelRoot.activeSelf)
            {
                panelRoot.SetActive(true);
            }

            if (!squadPanel.activeSelf)
            {
                squadPanel.SetActive(true);
            }
        }

        CacheBasePanelScale();

        squadPanelCanvasGroup = GetSquadPanelCanvasGroup();
        if (squadPanelCanvasGroup == null)
        {
            return;
        }

        panelActiveState = visibleOnStart;
        float targetAlpha = GetTargetAlpha(visibleOnStart);
        SetSquadPanelAlpha(squadPanelCanvasGroup, targetAlpha);
        SetPanelActiveScale(visibleOnStart);
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

        assignmentsDirty = true;
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
        assignmentsDirty = true;
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
        if (IsMultiplayerActive())
        {
            assignmentsDirty = true;
            return;
        }

        EnsureSinglePlayerCrown(index);
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

    private void LateUpdate()
    {
        if (!IsMultiplayerActive())
        {
            return;
        }

        EnsureAssignmentsSubscription();
        if (assignmentsDirty)
        {
            RebuildPlayerCrowns();
            assignmentsDirty = false;
        }

        UpdatePlayerCrownPositions();
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
        panelActiveState = visible;
        if (visible)
        {
            ShowSquadPanel(immediate);
        }
        else
        {
            HideSquadPanel(immediate);
        }

        SetPanelActiveScale(visible);
    }

    private void ShowSquadPanel(bool immediate)
    {
        if (squadPanel == null)
        {
            return;
        }

        GameObject panelRoot = GetSquadPanelCanvasGroupHost();
        if (panelRoot != null && !panelRoot.activeSelf)
        {
            panelRoot.SetActive(true);
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

        float targetAlpha = GetTargetAlpha(true);
        if (immediate)
        {
            SetSquadPanelAlpha(squadPanelCanvasGroup, targetAlpha);
            return;
        }

        FadeSquadPanelTo(targetAlpha, squadPanelFadeDuration);
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

        float targetAlpha = GetTargetAlpha(false);
        if (immediate)
        {
            SetSquadPanelAlpha(squadPanelCanvasGroup, targetAlpha);
            return;
        }

        FadeSquadPanelTo(targetAlpha, squadPanelFadeDuration);
    }

    public void SetPanelActiveScale(bool active)
    {
        if (!scalePanelWhenActive || squadPanel == null)
        {
            return;
        }

        CacheBasePanelScale();
        float multiplier = active ? Mathf.Max(0.01f, squadPanelActiveScale) : 1f;
        squadPanel.transform.localScale = basePanelScale * multiplier;
    }

    private float GetTargetAlpha(bool active)
    {
        float alpha = active
            ? squadPanelActiveAlpha
            : (keepPanelVisibleWhenInactive ? squadPanelInactiveAlpha : 0f);
        return Mathf.Clamp01(alpha);
    }

    private CanvasGroup GetSquadPanelCanvasGroup()
    {
        GameObject canvasGroupHost = GetSquadPanelCanvasGroupHost();
        if (canvasGroupHost == null)
        {
            return null;
        }

        CanvasGroup canvasGroup = canvasGroupHost.GetComponent<CanvasGroup>();
        if (canvasGroup == null && squadPanelAddCanvasGroupIfMissing)
        {
            canvasGroup = canvasGroupHost.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private GameObject GetSquadPanelCanvasGroupHost()
    {
        if (squadPanel == null)
        {
            return null;
        }

        Transform panelTransform = squadPanel.transform;
        if (panelTransform == transform || panelTransform.IsChildOf(transform))
        {
            return gameObject;
        }

        return squadPanel;
    }

    private void CacheBasePanelScale()
    {
        if (baseScaleCached || squadPanel == null)
        {
            return;
        }

        basePanelScale = squadPanel.transform.localScale;
        baseScaleCached = true;
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
            bool interactable = panelActiveState;
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
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
            bool interactable = panelActiveState && alpha > 0.001f;
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
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

    private void EnsureSinglePlayerCrown(int index)
    {
        GameObject crown = GetOrCreateCrown(ulong.MaxValue, true);
        if (crown == null)
        {
            return;
        }

        if (squadUnitsUI == null || squadUnitsUI.Count == 0)
        {
            crown.SetActive(false);
            return;
        }

        if (index < 0 || index >= squadUnitsUI.Count)
        {
            index = 0;
        }

        GameObject unit = squadUnitsUI[index];
        if (unit == null)
        {
            crown.SetActive(false);
            return;
        }

        crown.SetActive(true);
        crown.transform.position = unit.transform.position + (Vector3)squadPanelCrossOffset;
    }

    private void EnsureAssignmentsSubscription()
    {
        WorldInteractionService service = WorldInteractionService.Instance;
        if (ReferenceEquals(subscribedService, service))
        {
            return;
        }

        UnsubscribeAssignments();
        subscribedService = service;
        if (subscribedService != null)
        {
            subscribedService.AssignmentsChanged += OnAssignmentsChanged;
        }

        assignmentsDirty = true;
    }

    private void UnsubscribeAssignments()
    {
        if (subscribedService != null)
        {
            subscribedService.AssignmentsChanged -= OnAssignmentsChanged;
        }

        subscribedService = null;
    }

    private void OnAssignmentsChanged()
    {
        assignmentsDirty = true;
    }

    private void OnLocalCharacterChanged(Transform _)
    {
        assignmentsDirty = true;
    }

    private void RebuildPlayerCrowns()
    {
        if (subscribedService == null || crownPrefab == null)
        {
            ClearCrowns();
            return;
        }

        HashSet<ulong> desired = new HashSet<ulong>();
        int count = subscribedService.AssignmentCount;
        bool hasLocal = TryGetLocalClientId(out ulong localClientId);
        for (int i = 0; i < count; i++)
        {
            NetPlayerAssignment entry = subscribedService.GetAssignment(i);
            desired.Add(entry.ClientId);
            GetOrCreateCrown(entry.ClientId, hasLocal && entry.ClientId == localClientId);
        }

        List<ulong> toRemove = new List<ulong>();
        foreach (KeyValuePair<ulong, GameObject> pair in playerCrowns)
        {
            if (!desired.Contains(pair.Key))
            {
                toRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            ulong clientId = toRemove[i];
            if (playerCrowns.TryGetValue(clientId, out GameObject crown) && crown != null)
            {
                Destroy(crown);
            }

            playerCrowns.Remove(clientId);
        }
    }

    private void ClearCrowns()
    {
        foreach (KeyValuePair<ulong, GameObject> pair in playerCrowns)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value);
            }
        }

        playerCrowns.Clear();
    }

    private void UpdatePlayerCrownPositions()
    {
        if (subscribedService == null)
        {
            return;
        }

        if (squadUnitsUI == null || squadUnitsUI.Count == 0)
        {
            return;
        }

        int count = subscribedService.AssignmentCount;
        for (int i = 0; i < count; i++)
        {
            NetPlayerAssignment entry = subscribedService.GetAssignment(i);
            if (!playerCrowns.TryGetValue(entry.ClientId, out GameObject crown) || crown == null)
            {
                continue;
            }

            int index = ResolveSquadIndex(entry.CharacterId.ToString());
            if (index < 0 || index >= squadUnitsUI.Count)
            {
                crown.SetActive(false);
                continue;
            }

            GameObject unit = squadUnitsUI[index];
            if (unit == null)
            {
                crown.SetActive(false);
                continue;
            }

            crown.SetActive(true);
            crown.transform.position = unit.transform.position + (Vector3)squadPanelCrossOffset;
        }
    }

    private GameObject GetOrCreateCrown(ulong clientId, bool isLocal)
    {
        if (crownPrefab == null)
        {
            return null;
        }

        if (playerCrowns.TryGetValue(clientId, out GameObject existing) && existing != null)
        {
            ApplyCrownColor(existing, isLocal);
            return existing;
        }

        Transform parent = squadPanel != null ? squadPanel.transform : transform;
        GameObject instance = Instantiate(crownPrefab, parent);
        instance.name = $"SquadPanel_PlayerMarker_{clientId}";
        playerCrowns[clientId] = instance;
        ApplyCrownColor(instance, isLocal);
        return instance;
    }

    private void ApplyCrownColor(GameObject crown, bool isLocal)
    {
        if (crown == null)
        {
            return;
        }

        if (!isLocal && !overrideOtherPlayerCrownColor)
        {
            return;
        }

        Color color = isLocal ? localPlayerCrownColor : otherPlayerCrownColor;
        Graphic graphic = crown.GetComponentInChildren<Graphic>(true);
        if (graphic != null)
        {
            graphic.color = color;
        }

        Renderer renderer = crown.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(ColorId))
            {
                block.SetColor(ColorId, color);
            }
            else if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(BaseColorId))
            {
                block.SetColor(BaseColorId, color);
            }
            renderer.SetPropertyBlock(block);
        }
    }

    private static bool TryGetLocalClientId(out ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            clientId = 0;
            return false;
        }

        clientId = NetworkManager.Singleton.LocalClientId;
        return true;
    }

    private int ResolveSquadIndex(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return -1;
        }

        SquadManager manager = SquadManager.Instance;
        if (manager == null || manager.currentSquad == null)
        {
            return -1;
        }

        for (int i = 0; i < manager.currentSquad.Count; i++)
        {
            CharacterData character = manager.currentSquad[i];
            if (character == null)
            {
                continue;
            }

            if (GetCharacterId(character) == characterId)
            {
                return i;
            }
        }

        return -1;
    }

    private static string GetCharacterId(CharacterData character)
    {
        if (character == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(character.UniqueId))
        {
            return character.UniqueId;
        }

        if (!string.IsNullOrWhiteSpace(character.characterId))
        {
            return character.characterId;
        }

        if (!string.IsNullOrWhiteSpace(character.characterName))
        {
            return character.characterName;
        }

        return character.name;
    }

    private static bool IsMultiplayerActive()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
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

        Text fallbackText = hpRoot.GetComponent<Text>();
        if (fallbackText != null)
        {
            fallbackText.text = textValue;
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
