using UnityEngine;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// A set of helper methods for spawning temporary audio clips and visual effects at runtime.  
    /// This is mainly used for quick one-shot sound effects and particle effects without needing a manager object.
    /// </summary>
    public static class GameMethods
    {
        /// <summary>
        /// Creates and plays a temporary audio clip at a given world position.
        /// The object is automatically destroyed after the clip finishes playing (if not looping).
        /// </summary>
        /// <param name="clip">The audio clip to play.</param>
        /// <param name="position">The world position where the audio will be spawned.</param>
        /// <param name="loop">If true, the audio will loop indefinitely until manually destroyed.</param>
        /// <param name="volume">The playback volume (0–1).</param>
        /// <param name="clipDimention">The spatial blend (0 = 2D, 1 = fully 3D).</param>
        public static void CreateAudioClip(AudioClip clip, Vector3 position, bool loop = false, float volume = 1f, float clipDimention = 0f)
        {
            if (clip == null) return;

            GameObject sfxParent = GameObject.Find("Temp Audio Clips") ?? new GameObject("Temp Audio Clips");
            GameObject newClip = new GameObject($"Temp Audio: {clip.name}")
            {
                transform =
                {
                    parent = sfxParent.transform,
                    position = position
                }
            };

            AudioSource audioSource = newClip.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.loop = loop;
            audioSource.volume = volume;
            audioSource.spatialBlend = clipDimention;
            audioSource.Play();

            if (!loop)
                MonoBehaviour.Destroy(newClip, clip.length);
        }

        /// <summary>
        /// Randomly selects an audio clip from an array and plays it as a temporary sound at a given world position.
        /// </summary>
        /// <param name="clips">An array of audio clips to choose from randomly.</param>
        /// <param name="position">The world position where the audio will be spawned.</param>
        /// <param name="loop">If true, the audio will loop indefinitely until manually destroyed.</param>
        /// <param name="volume">The playback volume (0–1).</param>
        /// <param name="clipDimention">The spatial blend (0 = 2D, 1 = fully 3D).</param>
        public static void CreateAudioClip(AudioClip[] clips, Vector3 position, bool loop = false, float volume = 1f, float clipDimention = 0f)
        {
            if (clips == null || clips.Length == 0) return;
            CreateAudioClip(clips[Random.Range(0, clips.Length)], position, loop, volume, clipDimention);
        }

        /// <summary>
        /// Spawns a visual effect prefab at a given position and rotation, and destroys it after a delay.
        /// </summary>
        /// <param name="effect">The effect prefab to spawn.</param>
        /// <param name="position">The world position for the effect.</param>
        /// <param name="rotation">The world rotation for the effect.</param>
        /// <param name="timeToDestroy">How long before the effect object is destroyed.</param>
        public static void CreateEffect(GameObject effect, Vector3 position, Quaternion rotation, float timeToDestroy = 3f)
        {
            if (effect == null) return;

            GameObject effectsParent = GameObject.Find("Effects Parent") ?? new GameObject("Effects Parent");

            GameObject newEffect = MonoBehaviour.Instantiate(effect, position, rotation);
            newEffect.transform.SetParent(effectsParent.transform);
            MonoBehaviour.Destroy(newEffect, timeToDestroy);
        }
    }
}
