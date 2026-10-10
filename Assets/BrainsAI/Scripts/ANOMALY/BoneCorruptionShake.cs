using UnityEngine;

namespace BrainsAI
{
    public class BoneCorruptionShake : MonoBehaviour
    {
        public Transform[] bonesToCorrupt;
        public float shakeAmount = 0.05f;
        public float shakeSpeed = 30f;

        void LateUpdate()
        {
            foreach (Transform bone in bonesToCorrupt)
            {
                if (bone == null) continue;
                float noiseSeed = bone.GetEntityId().GetHashCode();
                Vector3 noise = new Vector3(
                    Mathf.PerlinNoise(Time.time * shakeSpeed, noiseSeed) - 0.5f,
                    Mathf.PerlinNoise(Time.time * shakeSpeed * 1.1f, noiseSeed + 10) - 0.5f,
                    Mathf.PerlinNoise(Time.time * shakeSpeed * 1.2f, noiseSeed + 20) - 0.5f
                );

                bone.localRotation *= Quaternion.Euler(noise * shakeAmount * 360f);
            }
        }
    }
}
