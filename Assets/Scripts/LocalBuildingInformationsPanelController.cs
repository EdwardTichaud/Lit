using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Panel d'informations local instancie pour un batiment.
public class LocalBuildingInformationsPanelController : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root du panel d'informations.")]
    public GameObject informationPanel;
    [Tooltip("Desactive le panel a la fermeture.")]
    public bool deactivatePanelOnClose = true;
    [Tooltip("Duree du fade du panel.")]
    public float panelFadeDuration = 0.15f;
    [Tooltip("Desactive les raycasts quand le panel est cache.")]
    public bool disableRaycastsWhenHidden = true;
    [Tooltip("Alpha a 0 au demarrage si CanvasGroup present.")]
    public bool setAlphaToZeroOnStart = true;

    [Header("Text References")]
    [Tooltip("Champ du nom du batiment.")]
    public TMP_Text buildingNameText;
    [Tooltip("Champ de la description d'effet.")]
    public TMP_Text effectDescriptionText;
    [Tooltip("Champ du niveau actuel.")]
    public TMP_Text currentLevelText;
    [Tooltip("Champ du bonus actuel.")]
    public TMP_Text currentBonusText;

    [Header("Format")]
    [Tooltip("Format du niveau (ex: \"Niveau actuel: {0}\").")]
    public string currentLevelFormat = "{0}";
    [Tooltip("Format du bonus (ex: \"Bonus actuel: {0}\").")]
    public string currentBonusFormat = "{0}";

    [Header("Orientation")]
    [Tooltip("Oriente le panel vers la camera en world space.")]
    public bool faceCamera = true;

    private CanvasGroup panelCanvasGroup;
    private Coroutine fadeRoutine;
    private BuildingInfoInteractable currentBuilding;
    private bool panelOpen;

    private const string BuildingNameFallback = "Name";
    private const string EffectDescriptionFallback = "EffectDescription";
    private const string CurrentLevelFallback = "Niveau actuel";
    private const string CurrentBonusFallback = "Bonus Actuel";

    public bool IsOpen => panelOpen;
    public BuildingInfoInteractable CurrentBuilding => currentBuilding;

    private void Awake()
    {
        if (informationPanel == null)
        {
            informationPanel = gameObject;
        }

        CacheTextTargets();
        panelCanvasGroup = GetPanelCanvasGroup();
        if (panelCanvasGroup != null && setAlphaToZeroOnStart)
        {
            panelCanvasGroup.alpha = 0f;
            if (disableRaycastsWhenHidden)
            {
                panelCanvasGroup.blocksRaycasts = false;
                panelCanvasGroup.interactable = false;
            }
        }

        if (deactivatePanelOnClose && informationPanel != null)
        {
            informationPanel.SetActive(false);
        }
    }

    public void OpenPanel(BuildingInfoInteractable building)
    {
        if (building == null)
        {
            return;
        }

        currentBuilding = building;
        UpdatePanel(building);

        if (informationPanel != null)
        {
            informationPanel.SetActive(true);
        }

        panelOpen = true;
        FadePanelTo(1f, panelFadeDuration);
    }

    public void ClosePanel()
    {
        if (!panelOpen)
        {
            currentBuilding = null;
            return;
        }

        panelOpen = false;
        currentBuilding = null;
        FadePanelTo(0f, panelFadeDuration);
    }

    public void RefreshPanel()
    {
        if (!panelOpen || currentBuilding == null)
        {
            return;
        }

        UpdatePanel(currentBuilding);
    }

    private void UpdatePanel(BuildingInfoInteractable building)
    {
        if (building == null)
        {
            SetText(buildingNameText, string.Empty);
            SetText(effectDescriptionText, string.Empty);
            SetText(currentLevelText, string.Empty);
            SetText(currentBonusText, string.Empty);
            return;
        }

        Item item = building.BuildingItem;
        int level = Mathf.Max(1, building.Level);
        if (item != null && item.isBuilding)
        {
            level = Mathf.Clamp(level, 1, Mathf.Max(1, item.buildingMaxLevel));
        }

        string effectDescription = ResolveItemDescription(item, level);
        string bonusDescription = BuildBonusDescription(item, level);

        SetText(buildingNameText, ResolveBuildingName(building, item));
        SetText(effectDescriptionText, effectDescription);
        SetText(currentLevelText, FormatValue(currentLevelFormat, level.ToString()));
        SetText(currentBonusText, FormatValue(currentBonusFormat, bonusDescription));
    }

    private string BuildEffectDescription(Item building, int level)
    {
        if (building == null || !building.isBuilding)
        {
            return "Aucun effet";
        }

        IReadOnlyList<Effect> effects = building.GetBuildingEffectsForLevel(level);
        if (effects == null || effects.Count == 0)
        {
            return "Aucun effet";
        }

        List<string> lines = new List<string>();
        for (int i = 0; i < effects.Count; i++)
        {
            Effect effect = effects[i];
            if (effect == null)
            {
                continue;
            }

            string description = effect.effectDescription;
            if (string.IsNullOrWhiteSpace(description))
            {
                description = effect.GetDescription();
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                lines.Add(description);
            }
        }

        if (lines.Count == 0)
        {
            return "Aucun effet";
        }

        return string.Join("\n", lines);
    }

    private string ResolveItemDescription(Item building, int level)
    {
        if (building != null && !string.IsNullOrWhiteSpace(building.description))
        {
            return building.description;
        }

        Item.BuildingLevelConfig config = building != null ? building.GetBuildingLevelConfig(level) : null;
        if (config != null && !string.IsNullOrWhiteSpace(config.effectDescription))
        {
            return config.effectDescription;
        }

        return BuildEffectDescription(building, level);
    }

    private string BuildBonusDescription(Item building, int level)
    {
        if (building == null || !building.isBuilding)
        {
            return "Aucun bonus";
        }

        if (building.HasBuildingLevelConfigs())
        {
            Item.BuildingLevelConfig config = building.GetBuildingLevelConfig(level);
            if (config != null && !string.IsNullOrWhiteSpace(config.bonusDescription))
            {
                return config.bonusDescription;
            }

            if (building.isCraftingBuilding)
            {
                if (building.HasCraftUnlocks())
                {
                    int total = building.availableCrafts != null ? building.availableCrafts.Count : 0;
                    int unlocked = building.GetUnlockedCraftsForLevel(level).Count;
                    if (total > 0)
                    {
                        return $"Crafts debloques: {unlocked}/{total}";
                    }
                }
                else if (config != null)
                {
                    int craftSlots = config.craftSlots > 0 ? config.craftSlots : building.GetCraftSlotsForLevel(level);
                    if (craftSlots > 0)
                    {
                        return $"Slots de craft: {craftSlots}";
                    }
                }
            }

            return BuildBonusDescriptionFromEffects(building.GetBuildingEffectsForLevel(level), level);
        }

        return BuildBonusDescriptionFromEffects(building.buildingEffects, level);
    }

    private string BuildBonusDescriptionFromEffects(IReadOnlyList<Effect> effects, int level)
    {
        if (effects == null || effects.Count == 0)
        {
            return "Aucun bonus";
        }

        List<string> lines = new List<string>();
        for (int i = 0; i < effects.Count; i++)
        {
            Effect effect = effects[i];
            if (effect == null)
            {
                continue;
            }

            string bonus = effect.GetBonusDescriptionForLevel(level);
            if (string.IsNullOrWhiteSpace(bonus))
            {
                bonus = effect.GetDescriptionForLevel(level);
            }

            if (string.IsNullOrWhiteSpace(bonus))
            {
                bonus = effect.name;
            }

            lines.Add(bonus);
        }

        if (lines.Count == 0)
        {
            return "Aucun bonus";
        }

        return string.Join("\n", lines);
    }


    private void CacheTextTargets()
    {
        Transform root = informationPanel != null ? informationPanel.transform : transform;
        if (buildingNameText == null)
        {
            buildingNameText = FindTextTarget(root, BuildingNameFallback, "Nom", "BuildingName", "NomBatiment", "Building");
        }

        if (effectDescriptionText == null)
        {
            effectDescriptionText = FindTextTarget(root, EffectDescriptionFallback);
        }

        if (currentLevelText == null)
        {
            currentLevelText = FindTextTarget(root, CurrentLevelFallback, "NiveauActuel", "CurrentLevel");
        }

        if (currentBonusText == null)
        {
            currentBonusText = FindTextTarget(root, CurrentBonusFallback, "BonusActuel", "CurrentBonus");
        }
    }

    private TMP_Text FindTextTarget(Transform root, params string[] names)
    {
        if (root == null || names == null || names.Length == 0)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null)
            {
                continue;
            }

            if (!MatchesName(child.name, names))
            {
                continue;
            }

            return child.GetComponent<TMP_Text>();
        }

        return null;
    }

    private bool MatchesName(string candidate, string[] names)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string normalized = NormalizeName(candidate);
        for (int i = 0; i < names.Length; i++)
        {
            if (NormalizeName(names[i]) == normalized)
            {
                return true;
            }
        }

        return false;
    }

    private string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] buffer = new char[value.Length];
        int index = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsWhiteSpace(c) || c == '_' || c == '-')
            {
                continue;
            }

            buffer[index++] = char.ToLowerInvariant(c);
        }

        return new string(buffer, 0, index);
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target == null)
        {
            return;
        }

        target.text = value ?? string.Empty;
    }

    private string FormatValue(string format, string value)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return value ?? string.Empty;
        }

        if (!format.Contains("{0}"))
        {
            return $"{format} {value}";
        }

        return string.Format(format, value);
    }

    private string ResolveBuildingName(BuildingInfoInteractable info, Item building)
    {
        if (building != null)
        {
            if (!string.IsNullOrWhiteSpace(building.itemName))
            {
                return building.itemName;
            }

            if (!string.IsNullOrWhiteSpace(building.name))
            {
                return building.name;
            }
        }

        if (info != null)
        {
            if (!string.IsNullOrWhiteSpace(info.BuildId))
            {
                return info.BuildId;
            }

            if (!string.IsNullOrWhiteSpace(info.BuildingItemId))
            {
                return info.BuildingItemId;
            }
        }

        return "Batiment";
    }

    private CanvasGroup GetPanelCanvasGroup()
    {
        if (informationPanel == null)
        {
            return null;
        }

        CanvasGroup group = informationPanel.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = informationPanel.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private void FadePanelTo(float targetAlpha, float duration)
    {
        panelCanvasGroup = GetPanelCanvasGroup();
        if (panelCanvasGroup == null)
        {
            if (targetAlpha <= 0f && deactivatePanelOnClose && informationPanel != null)
            {
                informationPanel.SetActive(false);
            }
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (!CanRunCoroutines() || duration <= 0f)
        {
            panelCanvasGroup.alpha = targetAlpha;
            if (disableRaycastsWhenHidden)
            {
                panelCanvasGroup.blocksRaycasts = targetAlpha > 0f;
                panelCanvasGroup.interactable = targetAlpha > 0f;
            }

            if (targetAlpha <= 0f && deactivatePanelOnClose && informationPanel != null)
            {
                informationPanel.SetActive(false);
            }
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(panelCanvasGroup, panelCanvasGroup.alpha, targetAlpha, duration));
    }

    private IEnumerator FadeRoutine(CanvasGroup group, float start, float target, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            if (group != null)
            {
                group.alpha = alpha;
                if (disableRaycastsWhenHidden)
                {
                    group.blocksRaycasts = alpha > 0f;
                    group.interactable = alpha > 0f;
                }
            }

            yield return null;
        }

        if (group != null)
        {
            group.alpha = target;
            if (disableRaycastsWhenHidden)
            {
                group.blocksRaycasts = target > 0f;
                group.interactable = target > 0f;
            }
        }

        fadeRoutine = null;
        if (target <= 0f && deactivatePanelOnClose && informationPanel != null)
        {
            informationPanel.SetActive(false);
        }
    }

    private bool CanRunCoroutines()
    {
        return isActiveAndEnabled && informationPanel != null && informationPanel.activeInHierarchy;
    }
}
