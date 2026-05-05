using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CombatSceneUiInstaller
{
    private const string MaisonScenePath = "Assets/Scenes/Maison.unity";

    [MenuItem("Lit/Combat/Install Scene Combat UI")]
    public static void InstallMaisonCombatUi()
    {
        Scene scene = EditorSceneManager.OpenScene(MaisonScenePath, OpenSceneMode.Single);
        InstallInActiveScene();
        EditorSceneManager.SaveScene(scene);
    }

    private static void InstallInActiveScene()
    {
        Canvas overlayCanvas = FindSceneComponentByName<Canvas>("UI_Overlay");
        if (overlayCanvas == null)
        {
            GameObject overlay = new GameObject("UI_Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlayCanvas = overlay.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 100;

            CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        RectTransform battlePanel = EnsureRectChild(overlayCanvas.transform, "BattlePanel");
        Stretch(battlePanel);
        CanvasGroup battleGroup = EnsureComponent<CanvasGroup>(battlePanel.gameObject);
        battleGroup.alpha = 0f;
        battleGroup.interactable = false;
        battleGroup.blocksRaycasts = false;

        TextMeshProUGUI titleText = CreateTopBar(battlePanel, out TextMeshProUGUI turnText, out TextMeshProUGUI timerText, out Image timerFill);
        TextMeshProUGUI playerHpText = CreateUnitStatus(
            battlePanel,
            "CombatPlayerStatus",
            "CombatPlayerHpText",
            "CombatPlayerHpFill",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(36f, 36f),
            new Vector2(420f, 150f),
            new Color(0.04f, 0.07f, 0.08f, 0.86f),
            new Color(0.18f, 0.78f, 0.57f, 1f),
            out Image playerHpFill);
        TextMeshProUGUI enemyHpText = CreateUnitStatus(
            battlePanel,
            "CombatEnemyStatus",
            "CombatEnemyHpText",
            "CombatEnemyHpFill",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-36f, -150f),
            new Vector2(420f, 170f),
            new Color(0.10f, 0.05f, 0.05f, 0.86f),
            new Color(0.83f, 0.24f, 0.19f, 1f),
            out Image enemyHpFill);
        TextMeshProUGUI prayerText = CreatePrayerPanel(battlePanel);
        TextMeshProUGUI messageText = CreateMessagePanel(battlePanel);
        TextMeshProUGUI actionsText = CreateActionsPanel(battlePanel, out CanvasGroup baseAttackGroup, out TextMeshProUGUI baseAttackText);

        CombatHudController controller = FindSceneComponentByName<CombatHudController>("BattleManager");
        if (controller == null)
        {
            GameObject battleManager = FindSceneObject("BattleManager");
            if (battleManager == null)
            {
                battleManager = new GameObject("BattleManager");
            }

            controller = battleManager.GetComponent<CombatHudController>();
            if (controller == null)
            {
                controller = battleManager.AddComponent<CombatHudController>();
            }
        }

        SerializedObject serializedController = new SerializedObject(controller);
        Assign(serializedController, "allowRuntimeFallback", false);
        Assign(serializedController, "battlePanelCanvasGroup", battleGroup);
        Assign(serializedController, "baseAttackCanvasGroup", baseAttackGroup);
        Assign(serializedController, "playerHpFillImage", playerHpFill);
        Assign(serializedController, "enemyHpFillImage", enemyHpFill);
        Assign(serializedController, "timerFillImage", timerFill);
        Assign(serializedController, "baseAttackText", baseAttackText);
        Assign(serializedController, "titleText", titleText);
        Assign(serializedController, "turnText", turnText);
        Assign(serializedController, "timerText", timerText);
        Assign(serializedController, "playerHpText", playerHpText);
        Assign(serializedController, "enemyHpText", enemyHpText);
        Assign(serializedController, "prayerText", prayerText);
        Assign(serializedController, "messageText", messageText);
        Assign(serializedController, "actionsText", actionsText);
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static TextMeshProUGUI CreateTopBar(RectTransform battlePanel, out TextMeshProUGUI turnText, out TextMeshProUGUI timerText, out Image timerFill)
    {
        RectTransform panel = EnsurePanel(
            battlePanel,
            "CombatTopBar",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -28f),
            new Vector2(900f, 112f),
            new Color(0.025f, 0.028f, 0.032f, 0.88f));

        TextMeshProUGUI titleText = EnsureText(panel, "CombatTitleText", "COMBAT", 28f, FontStyles.Bold, TextAlignmentOptions.Left);
        ConfigureRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -22f), new Vector2(190f, 34f));

        turnText = EnsureText(panel, "CombatTurnText", "Tour", 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        ConfigureRect(turnText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(420f, 38f));

        timerText = EnsureText(panel, "CombatTimerText", "Temps: 30 s", 24f, FontStyles.Bold, TextAlignmentOptions.Right);
        ConfigureRect(timerText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(180f, 34f));

        RectTransform timerBar = EnsurePanel(
            panel,
            "CombatTimerBar",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 22f),
            new Vector2(820f, 14f),
            new Color(0.12f, 0.13f, 0.14f, 0.95f));
        timerFill = EnsureFill(timerBar, "CombatTimerFill", new Color(0.91f, 0.72f, 0.33f, 1f));
        return titleText;
    }

    private static TextMeshProUGUI CreateUnitStatus(
        RectTransform battlePanel,
        string panelName,
        string textName,
        string fillName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        Color panelColor,
        Color fillColor,
        out Image fillImage)
    {
        RectTransform panel = EnsurePanel(battlePanel, panelName, anchorMin, anchorMax, pivot, anchoredPosition, size, panelColor);
        TextMeshProUGUI hpText = EnsureText(panel, textName, "PV", 26f, FontStyles.Bold, TextAlignmentOptions.Left);
        ConfigureRect(hpText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -22f), new Vector2(-48f, 84f));

        RectTransform hpBar = EnsurePanel(
            panel,
            fillName.Replace("Fill", "Bar"),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 22f),
            new Vector2(-48f, 18f),
            new Color(0.07f, 0.075f, 0.08f, 1f));
        fillImage = EnsureFill(hpBar, fillName, fillColor);
        return hpText;
    }

    private static TextMeshProUGUI CreatePrayerPanel(RectTransform battlePanel)
    {
        RectTransform panel = EnsurePanel(
            battlePanel,
            "CombatPrayerPanel",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-36f, -336f),
            new Vector2(420f, 72f),
            new Color(0.05f, 0.045f, 0.075f, 0.82f));
        TextMeshProUGUI text = EnsureText(panel, "CombatPrayerText", "Soutien: aucune priere active", 20f, FontStyles.Normal, TextAlignmentOptions.Left);
        StretchWithPadding(text.rectTransform, 22f, 12f, 22f, 12f);
        return text;
    }

    private static TextMeshProUGUI CreateMessagePanel(RectTransform battlePanel)
    {
        RectTransform panel = EnsurePanel(
            battlePanel,
            "CombatMessagePanel",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 178f),
            new Vector2(920f, 94f),
            new Color(0.025f, 0.028f, 0.032f, 0.86f));
        TextMeshProUGUI text = EnsureText(panel, "CombatMessageText", "Combat en cours.", 24f, FontStyles.Normal, TextAlignmentOptions.Center);
        StretchWithPadding(text.rectTransform, 28f, 16f, 28f, 16f);
        return text;
    }

    private static TextMeshProUGUI CreateActionsPanel(RectTransform battlePanel, out CanvasGroup baseAttackGroup, out TextMeshProUGUI baseAttackText)
    {
        RectTransform panel = EnsurePanel(
            battlePanel,
            "CombatActionsPanel",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 58f),
            new Vector2(920f, 108f),
            new Color(0.025f, 0.028f, 0.032f, 0.90f));

        RectTransform baseAttack = EnsureRectChild(panel, "BaseAttackUI");
        ConfigureRect(baseAttack, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(300f, 76f));
        baseAttackGroup = EnsureComponent<CanvasGroup>(baseAttack.gameObject);
        baseAttackGroup.alpha = 0f;
        baseAttackGroup.interactable = false;
        baseAttackGroup.blocksRaycasts = false;

        Image inputImage = EnsureComponent<Image>(EnsureRectChild(baseAttack, "BaseAttack_Input").gameObject);
        inputImage.color = new Color(0.91f, 0.72f, 0.33f, 1f);
        ConfigureRect(inputImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(74f, 74f));

        baseAttackText = EnsureText(baseAttack, "BaseAttack_Text", "Attaquer", 28f, FontStyles.Bold, TextAlignmentOptions.Left);
        ConfigureRect(baseAttackText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(92f, 0f), new Vector2(-92f, 0f));

        TextMeshProUGUI actionsText = EnsureText(panel, "CombatActionsText", "Interagir/RB: attaquer | Inventaire: item | Retour: passer", 22f, FontStyles.Normal, TextAlignmentOptions.Left);
        ConfigureRect(actionsText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(348f, 0f), new Vector2(-380f, -28f));
        return actionsText;
    }

    private static RectTransform EnsurePanel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform rect = EnsureRectChild(parent, name);
        ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, size);
        Image image = EnsureComponent<Image>(rect.gameObject);
        image.color = color;
        image.raycastTarget = false;
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;
        return rect;
    }

    private static TextMeshProUGUI EnsureText(RectTransform parent, string name, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        RectTransform rect = EnsureRectChild(parent, name);
        TextMeshProUGUI text = EnsureComponent<TextMeshProUGUI>(rect.gameObject);
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(12f, fontSize * 0.58f);
        text.fontSizeMax = fontSize;
        text.margin = Vector4.zero;
        return text;
    }

    private static Image EnsureFill(RectTransform bar, string name, Color color)
    {
        RectTransform fill = EnsureRectChild(bar, name);
        Stretch(fill);
        Image image = EnsureComponent<Image>(fill.gameObject);
        image.color = color;
        image.raycastTarget = false;
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = 0;
        image.fillAmount = 1f;
        return image;
    }

    private static RectTransform EnsureRectChild(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found == null)
        {
            GameObject existing = FindSceneObject(name);
            if (existing != null)
            {
                found = existing.transform;
                found.SetParent(parent, false);
            }
        }

        if (found == null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            found = obj.transform;
            found.SetParent(parent, false);
        }

        RectTransform rect = found as RectTransform;
        if (rect == null)
        {
            rect = found.gameObject.AddComponent<RectTransform>();
        }

        return rect;
    }

    private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void Stretch(RectTransform rect)
    {
        ConfigureRect(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    private static void StretchWithPadding(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static T EnsureComponent<T>(GameObject obj) where T : Component
    {
        T component = obj.GetComponent<T>();
        if (component == null)
        {
            component = obj.AddComponent<T>();
        }

        return component;
    }

    private static T FindSceneComponentByName<T>(string name) where T : Component
    {
        GameObject obj = FindSceneObject(name);
        return obj != null ? obj.GetComponent<T>() : null;
    }

    private static GameObject FindSceneObject(string name)
    {
        foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj.name != name || !obj.scene.IsValid() || obj.hideFlags != HideFlags.None)
            {
                continue;
            }

            return obj;
        }

        return null;
    }

    private static void Assign(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void Assign(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }
}
