using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EchoPassiveEffect", menuName = "Scriptable Objects/Effects/Passive Echo")]
// Effet passif: joue un son si un personnage est dans un rayon autour de l'item.
public class EchoPassiveEffect : Effect, IItemPassiveEffect
{
    [Header("Audio")]
    [Tooltip("Clip a jouer.")]
    public AudioClipSO audioClip;

    [Header("Range")]
    [Tooltip("Rayon d'action de l'echo.")]
    public float radius = 6f;
    [Tooltip("Cooldown entre deux plays.")]
    public float cooldown = 3f;
    [Tooltip("Utilise Time.unscaledTime.")]
    public bool useUnscaledTime = false;

    private readonly Dictionary<int, float> nextPlayTimeBySource = new Dictionary<int, float>();

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
        if (nextPlayTimeBySource.TryGetValue(key, out float nextTime) && time < nextTime)
        {
            return;
        }

        PlayAt(context.Position);
        float delay = Mathf.Max(0.01f, cooldown);
        nextPlayTimeBySource[key] = time + delay;
    }

    public override string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

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
