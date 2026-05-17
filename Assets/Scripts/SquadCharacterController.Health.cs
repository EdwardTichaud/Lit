using UnityEngine;

public partial class SquadCharacterController
{
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

        Debug.Log(
            $"[Health] damage target='{name}' source='{source ?? "unspecified"}' amount={sanitizedAmount} applied={applied} hpBefore={previousHp} hpAfter={currentHp}",
            this);

        if (applied > 0)
        {
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
