using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public enum QTEFeedback
{
    Perfect,
    Good,
    Early,
    Late,
    Miss
}

public class ActionUIDisplayManager : MonoBehaviour
{
    public static ActionUIDisplayManager Instance { get; private set; }

    [Header("UI Elements")]
    public CanvasGroup displayGroup;
    public TextMeshProUGUI actionText;
    public float displayDuration = 5f;  // Durée potentielle d'affichage (non utilisée pour l'instant)
    public float fadeDuration = 0.5f;    // Durée de fondu éventuel

    [Header("QTE Feedback")]
    [SerializeField] private bool pulseQteResult = true;
    [SerializeField] private Color qteSuccessColor = new Color(0.25f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color qtePerfectColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color qteFailColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private float qtePulseDuration = 0.15f;
    [SerializeField] private float qteHoldDuration = 0.35f;
    [SerializeField] private float qtePulseScale = 1.15f;

    private Color actionTextBaseColor = Color.white;
    private Vector3 actionTextBaseScale = Vector3.one;
    private Coroutine qteResultRoutine;
    private int qteResultToken;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (displayGroup != null)
            displayGroup.alpha = 0f;

        if (actionText != null)
        {
            actionTextBaseColor = actionText.color;
            actionTextBaseScale = actionText.transform.localScale;
        }
    }

    // Affiche simplement le nom d'une attaque
    // Le message restera à l'écran jusqu'à ce qu'il soit remplacé
    public void DisplayAttackName(string attackName)
    {
        ShowMessage(attackName);
    }

    public void DisplayActionMessage(string actorName, string actionName, string targetName)
    {
        string message = $"{actorName} utilise {actionName}";
        if (!string.IsNullOrEmpty(targetName))
            message += $" sur {targetName}";
        ShowMessage(message);
    }


    /// <summary>
    /// Affiche un message lorsque le joueur découvre une nouvelle attaque musicale.
    /// </summary>
    /// <param name="melodyName">Nom de l'attaque</param>
    public void DisplayMoveDiscovery(string melodyName)
    {
        string message = $"Mouvement découvert : {melodyName}";
        ShowMessage(message);
    }

    /// <summary>
    /// Affiche la préparation de l'attaque d'un ennemi.
    /// Si moveName est null ou vide, indique qu'il s'agit d'un mouvement inconnu.
    /// </summary>
    public void DisplayEnemyPreparation(string enemyName, string moveName)
    {
        string message = string.IsNullOrEmpty(moveName)
            ? $"{enemyName} s'apprête à jouer un mouvement inconnu"
            : $"{enemyName} s'apprête à jouer {moveName}";
        ShowMessage(message);
    }

    public void DisplayInstruction(string instruction)
    {
        ShowMessage(instruction);
    }

    public void DisplayInstruction_SelectItemOrSkill()
    {
        ShowMessage("Choisissez une compétence ou un objet");
    }

    public void DisplayInstruction_SelectItemSkillOrPass()
    {
        ShowMessage("Choisissez une compétence, un objet ou passez le tour (Retour)");
    }

    public void DisplayInstruction_SelectItem()
    {
        ShowMessage("Choisissez un objet");
    }

    public void DisplayInstruction_SelectSkill()
    {
        ShowMessage("Choisissez une compétence");
    }

    public void DisplayInstruction_SelectGroup()
    {
        ShowMessage("Choisissez un groupe sur lequel affecter l'action : Ennemis (←) ou Alliés (→)");
    }

    public void DisplayInstruction_SelectTarget()
    {
        ShowMessage("Sélectionnez une cible");
    }

    public void DisplayInstruction_ConfirmOrCancel()
    {
        ShowMessage("Validez (A/Enter) ou Annulez (B/Echap)");
    }

    public void DisplayInstruction_ExecuteQTE()
    {
        ShowMessage("Appuyez en rythme !");
    }

    public void DisplayInstruction_TargetTooFar()
    {
        ShowMessage("Cible trop éloignée");
    }

    public void DisplayInstruction_NotEnoughHarmonics()
    {
        ShowMessage("Pas assez d'harmoniques");
    }

    public void DisplayInstruction_MoveOnCooldown()
    {
        ShowMessage("Compétence en recharge");
    }

    /// <summary>
    /// Signale que la position relative désirée est déjà occupée par une autre unité.
    /// Utilisé lorsque l'on tente d'exécuter une attaque musicale mais que l'espace est indisponible.
    /// </summary>
    public void DisplayInstruction_TargetPositionOccupied()
    {
        ShowMessage("Position relative occupée");
    }

    /// <summary>
    /// Affiche un message d'esquive.
    /// </summary>
    public void DisplayDodge(string unitName)
    {
        string message = $"{unitName} esquive !";
        ShowMessage(message);
    }

    /// <summary>
    /// Affiche un message de parade.
    /// </summary>
    public void DisplayParry(string unitName)
    {
        string message = $"{unitName} pare l'attaque !";
        ShowMessage(message);
    }

    /// <summary>
    /// Affiche un message lorsque l'unité subit des dégâts.
    /// Utilise coup dévastateur si l'attaque est très puissante.
    /// </summary>
    public void DisplayDamage(string unitName, bool devastating)
    {
        string message = devastating
            ? $"{unitName} subit un coup dévastateur !"
            : $"{unitName} subit des dégâts.";
        ShowMessage(message);
    }

    /// <summary>
    /// Indique qu'un coup critique vient d'être réalisé.
    /// </summary>
    public void DisplayCriticalHit()
    {
        ShowMessage("Coup critique !");
    }

    public void DisplayInterceptionResult(bool success)
    {
        DisplayInterceptionOutcome(null, success, allyWasInterceptor: true);
    }

    public void DisplayInterceptionAttempt()
    {
        ShowMessage("Tentative d'interception...");
    }

    public void DisplayInterceptionOutcome(bool success, bool allyWasInterceptor)
    {
        DisplayInterceptionOutcome(null, success, allyWasInterceptor);
    }

    public void DisplayInterceptionOutcome(Transform target, bool success, bool allyWasInterceptor)
    {
        string message;
        if (allyWasInterceptor)
            message = success ? "Interception réussie" : "Interception ratée";
        else
            message = success ? "Echappée ratée" : "Echapée réussie";

        bool isPositive = allyWasInterceptor == success;
        if (target != null && DamagePopupManager.Instance != null)
        {
            DamagePopupManager.Instance.ShowInterceptionOutcome(target, message, isPositive);
            return;
        }

        ShowMessage(message);
    }

    /// <summary>
    /// Retour visuel concis pour un QTE (parfait, ok, trop tôt/tard, raté).
    /// </summary>
    public void DisplayQTEResult(QTEFeedback feedback)
    {
        string message = feedback switch
        {
            QTEFeedback.Perfect => "Parfait !",
            QTEFeedback.Good => "Bien !",
            QTEFeedback.Early => "Trop tôt",
            QTEFeedback.Late => "Trop tard",
            _ => "Raté"
        };

        ShowMessage(message);
        PulseQteFeedback(feedback, message);
    }

    /// <summary>
    /// Affiche un message à l'écran sans délai d'expiration.
    /// Le texte reste visible jusqu'à ce qu'un autre message soit envoyé.
    /// </summary>
    private void ShowMessage(string message)
    {
        if (qteResultRoutine != null)
        {
            StopCoroutine(qteResultRoutine);
            qteResultRoutine = null;
        }

        // Mise à jour immédiate du texte affiché
        if (actionText != null)
        {
            actionText.color = actionTextBaseColor;
            actionText.transform.localScale = actionTextBaseScale;
            actionText.text = message;
        }

        // On s'assure que le groupe est visible
        if (displayGroup != null)
            displayGroup.alpha = 1f;
    }

    private void PulseQteFeedback(QTEFeedback feedback, string message)
    {
        if (!pulseQteResult || actionText == null)
            return;

        if (qteResultRoutine != null)
            StopCoroutine(qteResultRoutine);

        qteResultToken++;
        qteResultRoutine = StartCoroutine(PulseQteFeedbackRoutine(feedback, qteResultToken, message));
    }

    private IEnumerator PulseQteFeedbackRoutine(QTEFeedback feedback, int token, string message)
    {
        Color targetColor = feedback switch
        {
            QTEFeedback.Perfect => qtePerfectColor,
            QTEFeedback.Good => qteSuccessColor,
            _ => qteFailColor
        };

        float pulseDuration = Mathf.Max(0.05f, qtePulseDuration);
        float holdDuration = Mathf.Max(0f, qteHoldDuration);
        float scaleFactor = Mathf.Max(1f, qtePulseScale);
        Vector3 targetScale = actionTextBaseScale * scaleFactor;

        float timer = 0f;
        while (timer < pulseDuration)
        {
            if (token != qteResultToken || actionText.text != message)
                yield break;

            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / pulseDuration);
            actionText.color = Color.Lerp(actionTextBaseColor, targetColor, t);
            actionText.transform.localScale = Vector3.Lerp(actionTextBaseScale, targetScale, t);
            yield return null;
        }

        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        timer = 0f;
        while (timer < pulseDuration)
        {
            if (token != qteResultToken || actionText.text != message)
                yield break;

            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / pulseDuration);
            actionText.color = Color.Lerp(targetColor, actionTextBaseColor, t);
            actionText.transform.localScale = Vector3.Lerp(targetScale, actionTextBaseScale, t);
            yield return null;
        }

        if (token == qteResultToken && actionText.text == message)
        {
            actionText.color = actionTextBaseColor;
            actionText.transform.localScale = actionTextBaseScale;
        }
    }
}
