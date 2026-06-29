using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AnimationEventsManager : MonoBehaviour
{
    private CameraMotifSO lastLockedMotif;
    public float windowDelay = 0.2f;
    private bool canInstantiate;
    private CharacterUnit target;
    private MusicalMoveSO move;

    public void TriggerQTE(float windowDelay)
    {
        RhythmQTEManager.Instance?.TriggerQTE(windowDelay);
    }

    public void TriggerNote(int noteIndex)
    {
        RhythmQTEManager.Instance?.TriggerNote(noteIndex);
    }
    
    public void TryToDamage()
    {
        target = NewBattleManager.Instance?.currentTargetCharacter;
        move = NewBattleManager.Instance?.currentMove;
        if (target == null || move == null)
            return;

        // Les moves avec QTE appliquent déjà leurs effets via le RhythmQTEManager.
        var qteManager = RhythmQTEManager.Instance;
        if (qteManager != null && qteManager.IsQteEnabled && qteManager.currentMove == move && move.notes != null && move.notes.Count > 0)
            return;

        if (target.isReadyToParry)
        {
            transform.parent.GetComponent<CharacterUnit>().TakeParry();
        }
        else
        {
            CharacterUnit caster = transform.GetComponentInParent<CharacterUnit>();
            // En supprimant le champ "power", toutes les animations se fient désormais
            // à la résolution centralisée des effets pour appliquer les dégâts, soins
            // ou effets spéciaux. Cela garantit que l'on profite d'EffectValue, des
            // multiplicateurs et des éventuels statuts associés.
            MusicalMoveExecutor.ApplyEffect(move, caster, target);
            qteManager?.RegisterMoveEffectApplied(move);
        }
    }

    public void TryToHeal(int healAmount)
    {
        target = NewBattleManager.Instance?.currentTargetCharacter;
        if (target != null)
        {
            target.Heal(healAmount);
        }
    }

    public void InstantiateHitEffectOnTarget(GameObject effect)
    {
        Transform targetChest = FindChildRecursive(NewBattleManager.Instance.currentTargetCharacter.transform, "Chest");
        if (effect != null && targetChest != null)
        {
            GameObject instantiatedEffect = Instantiate(effect, targetChest.position, Quaternion.identity);
            Destroy(instantiatedEffect, 3f); // Destroy after 3 seconds to clean up
            Debug.Log("InstantiateHitEffectOnTarget called with effect: " + effect.name + " on " + targetChest);
        }
    }

    public void InstantiateEffectOnTarget(GameObject effect)
    {
        Transform target = NewBattleManager.Instance.currentTargetCharacter.transform;
        if (effect != null && target != null)
        {
            GameObject instantiatedEffect = Instantiate(effect, target.position, Quaternion.identity);
            Destroy(instantiatedEffect, 3f); // Destroy after 3 seconds to clean up
            Debug.Log("InstantiateEffectOnTarget called with effect: " + effect.name + " on " + target);
        }
    }

    public void InstantiateEffectsOnAllAllies(GameObject effect)
    {
        if (effect == null)
            return;

        var caster = GetComponentInParent<CharacterUnit>();
        if (caster == null)
        {
            Debug.LogWarning("[AnimationEventsManager] Aucun CharacterUnit parent pour InstantiateEffectsOnAllAllies.");
            return;
        }

        var battleManager = NewBattleManager.Instance;
        if (battleManager == null)
        {
            Debug.LogWarning("[AnimationEventsManager] NewBattleManager introuvable pour InstantiateEffectsOnAllAllies.");
            return;
        }

        bool casterIsPlayer = caster.IsPlayerControlled;
        foreach (var unit in battleManager.unitsInBattle)
        {
            if (unit == null || unit.IsPlayerControlled != casterIsPlayer)
                continue;

            GameObject instantiatedEffect = Instantiate(effect, unit.transform.position, Quaternion.identity);
            Destroy(instantiatedEffect, 3f); // Destroy after 3 seconds to clean up
        }
    }

    public void SlowTime(float slowFactor)
    {
        Time.timeScale = slowFactor;
    }

    public void ResetTime()
    {
        Time.timeScale = 1;
    }

    public void PlayVoice(AudioClipSO audioClip)
    {
        if (audioClip != null)
        {
            CharacterUnit speaker = GetComponentInParent<CharacterUnit>();
            string speakerName = speaker != null && speaker.Data != null ? speaker.Data.characterName : null;
            AudioManager.Instance?.PlayVoice(audioClip, speakerName);
        }
        else
        {
            Debug.LogWarning("PlayVoice called with null audioClip");
        }
    }

    public void SetCameraMotif(CameraMotifSO motif)
    {
        if (motif == null)
        {
            Debug.LogWarning("[AnimationEventsManager] SetCameraMotif appelé avec un motif null.");
            return;
        }

        BattleCameraManager.Instance?.SetCameraMotif(motif);
    }

    public void LockCameraMotif(CameraMotifSO motif)
    {
        if (motif == null)
        {
            Debug.LogWarning("[AnimationEventsManager] LockCameraMotif appelé avec un motif null.");
            return;
        }

        lastLockedMotif = motif;
        BattleCameraManager.Instance?.LockCameraMotif(motif);
    }

    public void UnlockCameraMotif()
    {
        if (lastLockedMotif == null)
            return;

        BattleCameraManager.Instance?.UnlockCameraMotif(lastLockedMotif);
        lastLockedMotif = null;
    }

    public void ClearCameraMotif()
    {
        BattleCameraManager.Instance?.ClearCameraMotif();
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent.name == targetName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, targetName);
            if (result != null)
                return result;
        }
        return null;
    }
}
