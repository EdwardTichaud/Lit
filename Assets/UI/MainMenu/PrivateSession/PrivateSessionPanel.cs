using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PrivateSessionPanel : MonoBehaviour
{
    private PrivateSessionService session;
    private GameObject canvasObject;
    private UiPanel panel;
    private TMP_Text title, details, hint;
    private readonly TMP_Text[] rows = new TMP_Text[4];
    private readonly Button[] choices = new Button[4];
    private Button ready, launch, copy, leave;
    private bool visible;

    private void Start()
    {
        session = GetComponent<PrivateSessionService>();
        Build();
        session.Changed += Refresh;
        Refresh();
    }
    private void OnDestroy()
    {
        if (session != null) session.Changed -= Refresh;
        if (canvasObject != null) Destroy(canvasObject);
    }
    private void Build()
    {
        canvasObject = new GameObject("PrivateSessionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        GameObject root = MenuViewFactory.Column(canvasObject.transform, "PrivateSessionPanel");
        root.AddComponent<Image>().color = new Color(.035f, .045f, .055f, .98f);
        root.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(32, 32, 24, 24);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(.5f, .5f); rect.anchorMax = rect.anchorMin;
        rect.sizeDelta = new Vector2(860, 920);
        panel = root.AddComponent<UiPanel>();
        title = MenuViewFactory.Label(root.transform, "Salon coopératif privé", 48);
        details = MenuViewFactory.Label(root.transform, "", 76);
        copy = MenuViewFactory.Button(root.transform, "Copier le code d’invitation", () =>
        { GUIUtility.systemCopyBuffer = session.JoinCode; hint.text = "Code copié."; });
        for (int i = 0; i < 4; i++) rows[i] = MenuViewFactory.Label(root.transform, "Place libre", 36);
        for (int i = 0; i < 4; i++)
        {
            int slot = i;
            choices[i] = MenuViewFactory.Button(root.transform, "Personnage", () =>
            { if (slot < session.Lobby.characterIds.Length) session.Reserve(session.Lobby.characterIds[slot]); });
        }
        ready = MenuViewFactory.Button(root.transform, "Je suis prêt", () => session.ToggleReady());
        launch = MenuViewFactory.Button(root.transform, "Lancer la partie", () => session.Launch());
        leave = MenuViewFactory.Button(root.transform, "Quitter le salon", () => session.Leave());
        hint = MenuViewFactory.Label(root.transform, "", 60);
        MenuViewFactory.MakeScrollable(root);
        panel.Hide(true);
    }
    private void Refresh()
    {
        bool show = session.IsBusy || session.Phase == PrivateSessionPhase.Lobby;
        if (show != visible)
        {
            visible = show;
            panel.SetVisible(show, true);
            if (show) InputFocusStack.Push(this);
            else
            {
                InputFocusStack.Pop(this);
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == MainMenuController.DefaultMenuSceneName)
                    Cursor.visible = false;
            }
        }
        if (!show) return;
        bool lobby = session.Phase == PrivateSessionPhase.Lobby;
        ((RectTransform)panel.transform).sizeDelta = new Vector2(860, lobby ? 920 : 340);
        title.text = lobby ? "Salon coopératif privé" : session.Message;
        details.text = lobby ? $"{session.Lobby.sessionName} — {session.Lobby.saveName}\nCode : {session.JoinCode}" : "Vous pouvez revenir au menu sans perdre votre sauvegarde.";
        copy.gameObject.SetActive(lobby);
        ready.gameObject.SetActive(lobby);
        launch.gameObject.SetActive(lobby && session.IsHost);
        launch.interactable = session.Lobby.CanStart;
        leave.interactable = session.Phase != PrivateSessionPhase.Returning;
        leave.GetComponentInChildren<TMP_Text>().text = lobby ? "Quitter le salon" : "Annuler / revenir au menu";
        for (int i = 0; i < 4; i++)
        {
            rows[i].gameObject.SetActive(lobby);
            PrivateLobbyMember member = i < session.Lobby.members.Count ? session.Lobby.members[i] : null;
            int character = member == null ? -1 : System.Array.IndexOf(session.Lobby.characterIds, member.characterId);
            rows[i].text = member == null ? "Place libre" :
                $"{(member.clientId == session.LocalClientId ? "Vous" : "Joueur " + (i + 1))} — {(character >= 0 ? session.Lobby.characterNames[character] : "Personnage")} — {(member.ready ? "Prêt" : "En préparation")}";
            bool exists = lobby && i < session.Lobby.characterIds.Length;
            choices[i].gameObject.SetActive(exists);
            if (!exists) continue;
            string id = session.Lobby.characterIds[i];
            choices[i].GetComponentInChildren<TMP_Text>().text = session.Lobby.characterNames[i];
            choices[i].interactable = !session.Lobby.members.Any(m => m.clientId != session.LocalClientId && m.characterId == id);
        }
        PrivateLobbyMember local = session.Lobby.members.Find(m => m.clientId == session.LocalClientId);
        ready.GetComponentInChildren<TMP_Text>().text = local != null && local.ready ? "Je ne suis plus prêt" : "Je suis prêt";
        hint.text = lobby ? session.Message : "Retour / Échap : annuler";
    }
    private void LateUpdate()
    {
        if (!visible) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = !MainMenuNavigation.UsingGamepad;
    }
    private void OnDisable() => InputFocusStack.Pop(this);
}
