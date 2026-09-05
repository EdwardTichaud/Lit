using System;

// Donnees minimales pour ouvrir une confirmation centralisee.
public sealed class ConfirmationRequest
{
    public object Owner;
    public string Title;
    public string Message;
    public string ConfirmLabel;
    public string CancelLabel;
    public string DebugContext;
    public Action OnConfirm;
    public Action OnCancel;
    /// <summary>
    /// Quand les deux boutons sont deux actions positives, Retour ferme la
    /// fenetre sans executer le second choix.
    /// </summary>
    public bool DismissOnReturn;
    public bool PreferCancel;

    public ConfirmationRequest()
    {
    }

    public ConfirmationRequest(object owner, string message, Action onConfirm, Action onCancel = null)
    {
        Owner = owner;
        Message = message;
        OnConfirm = onConfirm;
        OnCancel = onCancel;
    }
}
