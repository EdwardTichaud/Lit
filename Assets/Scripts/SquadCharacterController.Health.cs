using UnityEngine;

// Role: extension partielle de SquadCharacterController pour les degats et sons de sante.
// Usage: appelee par le combat, les dangers et tout systeme qui retire des PV a un personnage.
// Responsibilities: nettoyer le montant, modifier les PV via SetCurrentHp, jouer le cue audio approprie.
// Dependencies: SquadCharacterController, AudioManager, ActionAudioCue.
// Precautions: ne pas dupliquer la logique de SetCurrentHp ici; cette methode doit rester un point d'entree simple.
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

        int previousHp = currentHp;
        SetCurrentHp(currentHp - sanitizedAmount);
        int applied = Mathf.Max(0, previousHp - currentHp);

        // Log volontairement detaille: il aide a diagnostiquer les pertes de PV pendant les tests de combat.
        Debug.Log(
            $"[Health] damage target='{name}' source='{source ?? "unspecified"}' amount={sanitizedAmount} applied={applied} hpBefore={previousHp} hpAfter={currentHp}",
            this);

        if (applied > 0)
        {
            LitUccDamageBridge uccDamageBridge = GetComponent<LitUccDamageBridge>();
            if (uccDamageBridge != null)
            {
                uccDamageBridge.NotifyDamageApplied(applied, currentHp <= 0, source);
            }

            PlayActionAudio(currentHp <= 0 ? ActionAudioCue.CharacterDeath : ActionAudioCue.CharacterDamage);
        }

        return applied;
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
