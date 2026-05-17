// Role:
// Passive item effect that plays an audio echo when characters are nearby.
// Usage:
// Assigned to an item passive effect and ticked by ItemPassiveEffectSystem.
// Responsibilities:
// Check range/cooldown, then play the configured AudioClipSO at the item position.
// Dependencies:
// Effect, IItemPassiveEffect, AudioManager, ItemPassiveContext.
// Precautions:
// The fallback path creates an AudioSource GameObject; prefer AudioManager when available.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a spatial audio echo near an item source when a character is in range.
/// </summary>
[CreateAssetMenu(fileName = "EchoPassiveEffect", menuName = "Scriptable Objects/Effects/Passive Echo")]
public class EchoPassiveEffect : Effect, IItemPassiveEffect
{
    [Header("Audio")]
    /// <summary>Clip played by the echo.</summary>
    [Tooltip("Clip a jouer.")]
    public AudioClipSO audioClip;

    [Header("Range")]
    /// <summary>World-space range where a character can trigger the echo.</summary>
    [Tooltip("Rayon d'action de l'echo.")]
    public float radius = 6f;
    /// <summary>Minimum seconds between plays per source/item key.</summary>
    [Tooltip("Cooldown entre deux plays.")]
    public float cooldown = 3f;
    /// <summary>If true, uses Time.unscaledTime for cooldowns.</summary>
    [Tooltip("Utilise Time.unscaledTime.")]
    public bool useUnscaledTime = false;

    private readonly Dictionary<int, float> nextPlayTimeBySource = new Dictionary<int, float>();

    /// <summary>
    /// Plays the echo immediately at the controller position.
    /// </summary>
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        if (controller == null || audioClip == null)
        {
            return false;
        }

        PlayAt(controller.transform.position);
        if (AudioManager.Instance != null)
        {
            return true;
        }

        return audioClip.audioClip != null;
    }

    /// <summary>
    /// Evaluates one passive tick for the item source.
    /// </summary>
    public void Tick(ItemPassiveContext context)
    {
        if (audioClip == null || context.Source == null)
        {
            return;
        }

        if (!IsAnyCharacterInRange(context))
        {
            return;
        }

        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        int key = BuildKey(context);
        // Cooldown is tracked per source/item pair so identical items do not silence each other globally.
        if (nextPlayTimeBySource.TryGetValue(key, out float nextTime) && time < nextTime)
        {
            return;
        }

        PlayAt(context.Position);
        float delay = Mathf.Max(0.01f, cooldown);
        nextPlayTimeBySource[key] = time + delay;
    }

    /// <summary>Returns the default effect description.</summary>
    public override string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    /// <summary>Returns the default bonus description.</summary>
    public override string GetBonusDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    private bool IsAnyCharacterInRange(ItemPassiveContext context)
    {
        IReadOnlyList<SquadCharacterController> characters = context.Characters;
        if (characters == null || characters.Count == 0)
        {
            return false;
        }

        float range = Mathf.Max(0f, radius);
        if (range <= 0f)
        {
            // A zero range means the designer wants any known character to trigger the echo.
            return true;
        }

        float rangeSqr = range * range;
        Vector3 position = context.Position;
        for (int i = 0; i < characters.Count; i++)
        {
            SquadCharacterController controller = characters[i];
            if (controller == null)
            {
                continue;
            }

            Vector3 delta = controller.transform.position - position;
            if (delta.sqrMagnitude <= rangeSqr)
            {
                return true;
            }
        }

        return false;
    }

    private void PlayAt(Vector3 position)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClip(audioClip, position);
            return;
        }

        if (audioClip == null || audioClip.audioClip == null)
        {
            return;
        }

        // Fallback for tests or scenes that do not have AudioManager yet.
        AudioSource source = new GameObject("EchoPassiveAudio").AddComponent<AudioSource>();
        source.transform.position = position;
        source.clip = audioClip.audioClip;
        source.volume = Mathf.Clamp01(audioClip.volume);
        source.loop = audioClip.loop;
        source.spatialBlend = 1f;
        source.minDistance = 1f;
        source.maxDistance = 25f;
        source.Play();

        if (!audioClip.loop)
        {
            Object.Destroy(source.gameObject, audioClip.audioClip.length);
        }
    }

    private int BuildKey(ItemPassiveContext context)
    {
        int sourceId = context.Source != null ? context.Source.GetInstanceID() : 0;
        int itemId = context.Item != null ? context.Item.GetInstanceID() : 0;
        unchecked
        {
            return (sourceId * 397) ^ itemId;
        }
    }
}
