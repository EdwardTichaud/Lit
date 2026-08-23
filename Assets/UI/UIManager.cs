using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(2000)]
public class UIManager : MonoBehaviour
{
    private const string DefaultOverlayName = "UI_Overlay";
    private static readonly string[] PersistentHudPanelNames =
    {
        "MuninUIPanel",
        "CompassPanel"
    };

    [Header("Startup Visibility")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private List<RectTransform> visibleAtStartupPanels = new List<RectTransform>();

    private void Start()
    {
        ApplyStartupVisibility();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRecoveredOverlayVisibilityManager()
    {
        GameObject overlay = GameObject.Find(DefaultOverlayName);
        if (overlay != null && overlay.GetComponent<UIManager>() == null)
        {
            // UI_Overlay a ete recupere depuis une ancienne scene et ne porte
            // donc pas forcement son composant de demarrage. L'ajout runtime
            // laisse la scene source intacte tout en restaurant son contrat :
            // aucun menu d'action ne doit etre visible sans action du joueur.
            overlay.AddComponent<UIManager>();
        }
    }

    public void ApplyStartupVisibility()
    {
        Transform root = ResolveUiRoot();
        if (root == null)
        {
            return;
        }

        if (root.localScale.sqrMagnitude <= 0.0001f)
        {
            root.localScale = Vector3.one;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (ContainsStartupPanel(child) || IsPersistentHudPanel(child) || IsSelfManagedVisibility(child))
            {
                continue;
            }

            SetPanelVisible(child, false);
        }

        if (visibleAtStartupPanels == null)
        {
            return;
        }

        for (int i = 0; i < visibleAtStartupPanels.Count; i++)
        {
            SetPanelVisible(visibleAtStartupPanels[i], true, false);
        }
    }

    public void SetPanelVisible(Transform panel, bool visible)
    {
        SetPanelVisible(panel, visible, visible);
    }

    private static void SetPanelVisible(Transform panel, bool visible, bool receiveInput)
    {
        if (panel == null)
        {
            return;
        }

        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible && receiveInput;
        canvasGroup.blocksRaycasts = visible && receiveInput;
    }

    private Transform ResolveUiRoot()
    {
        if (uiRoot != null)
        {
            return uiRoot;
        }

        if (string.Equals(name, DefaultOverlayName, System.StringComparison.Ordinal))
        {
            uiRoot = transform;
            return uiRoot;
        }

        GameObject overlay = GameObject.Find(DefaultOverlayName);
        uiRoot = overlay != null ? overlay.transform : transform;
        return uiRoot;
    }

    private bool ContainsStartupPanel(Transform root)
    {
        if (root == null || visibleAtStartupPanels == null)
        {
            return false;
        }

        for (int i = 0; i < visibleAtStartupPanels.Count; i++)
        {
            RectTransform startupPanel = visibleAtStartupPanels[i];
            if (startupPanel != null && ContainsTransform(root, startupPanel))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsTransform(Transform root, Transform target)
    {
        return root == target || (target != null && target.IsChildOf(root));
    }

    // Certains panels, comme l'inventaire, pilotent eux-memes leur CanvasGroup et
    // leur fondu. Les masquer ici peut interrompre un fondu d'ouverture lance
    // avant Start (notamment lors de la premiere saisie joueur).
    private static bool IsSelfManagedVisibility(Transform panel)
    {
        if (panel == null)
        {
            return false;
        }

        // Ces composants gerent deja l'etat de leur CanvasGroup (et, pour
        // certains, l'activation de leur racine). Leur imposer un CanvasGroup
        // parent transparent au demarrage empecherait ensuite leur ouverture.
        // On laisse donc leur propre contrat d'affichage intact.
        MonoBehaviour[] behaviours = panel.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            switch (behaviour.GetType().Name)
            {
                case "DialoguePanelUI":
                case "BuildingPanelController":
                case "CraftingConstructionPanel":
                case "InfoBoxUI":
                case "InventoryPanelController":
                case "InventoryUISettings":
                case "LootUISettings":
                case "PausePanelController":
                case "StabReading":
                case "ConfirmationManager":
                case "QuantityBox":
                    return true;
            }
        }

        return false;
    }

    private static bool IsPersistentHudPanel(Transform panel)
    {
        if (panel == null)
        {
            return false;
        }

        for (int i = 0; i < PersistentHudPanelNames.Length; i++)
        {
            if (string.Equals(panel.name, PersistentHudPanelNames[i], System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        // MuninUI applique son propre alpha selon la presence du compagnon et
        // ses charges. Le cacher ici couperait ce rafraichissement initial.
        return panel.GetComponent<MuninUI>() != null;
    }
}
