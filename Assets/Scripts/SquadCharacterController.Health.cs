using UnityEngine;

// Role: extension partielle de SquadCharacterController pour les degats et sons de sante.
// Usage: appelee par le combat, les dangers et tout systeme qui retire des PV a un personnage.
// Responsibilities: nettoyer le montant, router vers CharacterHealth quand UCC est l'autorite, jouer le cue audio approprie.
// Dependencies: SquadCharacterController, AudioManager, ActionAudioCue.
// Precautions: ne pas dupliquer la logique de CharacterHealth ici; cette methode doit rester un point d'entree simple.
/// <summary>
/// Partie sante/degats du controleur de personnage de squad.
/// </summary>
public partial class SquadCharacterController
{
    /// <summary>
    /// Applique des degats au personnage et retourne le montant effectivement retire.
    /// </summary>
    public int ApplyDamage(int amount, string source = null)
    {
        int sanitizedAmount = Mathf.Max(0, amount);
        if (sanitizedAmount <= 0)
        {
            return 0;
        }

        LitUccDamageBridge uccDamageBridge = GetComponent<LitUccDamageBridge>();
        if (uccDamageBridge != null
            && uccDamageBridge.TryApplyDamageToAuthority(sanitizedAmount, source, out int authorityApplied))
        {
            return authorityApplied;
        }

        int previousHp = currentHp;
        SetCurrentHpLocal(currentHp - sanitizedAmount);
        int applied = Mathf.Max(0, previousHp - currentHp);

        RecordDamageApplied(sanitizedAmount, applied, previousHp, currentHp, source);

        if (applied > 0)
        {
            if (uccDamageBridge != null)
            {
                uccDamageBridge.NotifyDamageApplied(applied, currentHp <= 0, source);
            }
        }

        return applied;
    }

    internal void RecordDamageApplied(int requestedAmount, int appliedAmount, int previousHp, int newHp, string source = null)
    {
        // Log volontairement detaille: il aide a diagnostiquer les pertes de PV pendant les tests de combat.
        Debug.Log(
            $"[Health] damage target='{name}' source='{source ?? "unspecified"}' amount={requestedAmount} applied={appliedAmount} hpBefore={previousHp} hpAfter={newHp}",
            this);

        if (appliedAmount > 0)
        {
            PlayActionAudio(newHp <= 0 ? ActionAudioCue.CharacterDeath : ActionAudioCue.CharacterDamage);
        }
    }

    private void PlayActionAudio(ActionAudioCue cue)
    {
        if (cue == ActionAudioCue.None)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        if (manager != null)
        {
            manager.PlayActionCue(cue, transform.position);
        }
    }
}
