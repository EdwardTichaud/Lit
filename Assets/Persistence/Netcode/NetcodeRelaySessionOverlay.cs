using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Affiche le code Relay de l'hote dans l'UI RelayService preparee dans
/// Bootstrap. Relay_Root reste toujours actif : sa visibilite est pilotee
/// uniquement par l'alpha de son CanvasGroup.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetcodeRelaySessionOverlay : MonoBehaviour
{
    private const string RelayRootName = "Relay_Root";
    private const string SessionTitle = "SESSION AMIS";

    private CanvasGroup relayRoot;
    private TMP_Text titleText;
    private TMP_Text codeText;
    private TMP_InputField codeInput;
    private TMP_Text codePlaceholder;
    private string originalTitle;
    private string lastCode = string.Empty;
    private bool panelRequested;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        LocalInputRouter.Select += HandleSelect;
        ResolveRelayUi();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        LocalInputRouter.Select -= HandleSelect;
    }

    private void Update()
    {
        ResolveRelayUi();

        NetcodeLauncher launcher = GetComponent<NetcodeLauncher>();
        NetworkManager manager = NetworkManager.Singleton;
        string joinCode = launcher != null ? launcher.ActiveRelayJoinCode : string.Empty;
        bool sessionAvailable = manager != null && manager.IsHost && !string.IsNullOrWhiteSpace(joinCode);
        if (!sessionAvailable)
        {
            panelRequested = false;
        }

        SetVisible(sessionAvailable && panelRequested);
        if (!sessionAvailable || joinCode == lastCode)
        {
            return;
        }

        lastCode = joinCode;
        if (titleText != null)
        {
            titleText.text = SessionTitle;
        }

        if (codeText != null)
        {
            codeText.text = joinCode;
        }

        if (codeInput != null)
        {
            codeInput.SetTextWithoutNotify(joinCode);
            codeInput.interactable = false;
            codeInput.readOnly = true;
        }

        // Certaines versions de RelayService ont un TMP_InputField dont le
        // Text Component n'est plus serialize. Le placeholder reste alors le
        // seul texte visible : il devient notre affichage de secours.
        if (codePlaceholder != null)
        {
            codePlaceholder.text = joinCode;
            Color color = codePlaceholder.color;
            color.a = 1f;
            codePlaceholder.color = color;
        }
    }

    private void HandleSelect(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        NetcodeLauncher launcher = GetComponent<NetcodeLauncher>();
        NetworkManager manager = NetworkManager.Singleton;
        bool sessionAvailable = manager != null &&
                                manager.IsHost &&
                                launcher != null &&
                                !string.IsNullOrWhiteSpace(launcher.ActiveRelayJoinCode);
        if (!sessionAvailable)
        {
            return;
        }

        panelRequested = !panelRequested;
        SetVisible(panelRequested);
    }

    private void ResolveRelayUi()
    {
        if (relayRoot == null)
        {
            CanvasGroup[] canvasGroups = Resources.FindObjectsOfTypeAll<CanvasGroup>();
            for (int i = 0; i < canvasGroups.Length; i++)
            {
                CanvasGroup candidate = canvasGroups[i];
                if (candidate != null && candidate.name == RelayRootName && candidate.gameObject.scene.IsValid())
                {
                    relayRoot = candidate;
                    break;
                }
            }
        }

        if (relayRoot == null)
        {
            return;
        }

        if (titleText == null)
        {
            Transform title = relayRoot.transform.Find("Join_Panel/Join_Title");
            titleText = title != null ? title.GetComponent<TMP_Text>() : null;
            if (titleText != null && originalTitle == null)
            {
                originalTitle = titleText.text;
            }
        }

        if (codeText == null)
        {
            TMP_Text[] textComponents = relayRoot.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < textComponents.Length; i++)
            {
                TMP_Text candidate = textComponents[i];
                if (candidate != null && string.Equals(candidate.name, "code_text", System.StringComparison.OrdinalIgnoreCase))
                {
                    codeText = candidate;
                    break;
                }
            }
        }

        if (codeInput == null)
        {
            codeInput = relayRoot.GetComponentInChildren<TMP_InputField>(true);
        }

        if (codePlaceholder == null)
        {
            Transform placeholder = relayRoot.transform.Find("Join_Panel/Join_CodeField/Code_Placeholder");
            codePlaceholder = placeholder != null ? placeholder.GetComponent<TMP_Text>() : null;
        }
    }

    private void SetVisible(bool visible)
    {
        if (relayRoot == null)
        {
            return;
        }

        relayRoot.alpha = visible ? 1f : 0f;
        relayRoot.interactable = false;
        relayRoot.blocksRaycasts = false;

        if (!visible && titleText != null && originalTitle != null)
        {
            titleText.text = originalTitle;
        }
    }
}
