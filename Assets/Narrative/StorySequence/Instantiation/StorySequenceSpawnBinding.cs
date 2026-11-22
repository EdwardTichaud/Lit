using System;
using UnityEngine;
using UnityEngine.Events;

namespace Lit.Story
{
    [Serializable]
    public sealed class StorySequenceSpawnBinding
    {
        public string spawnPointId;
        public Transform point;
        [Tooltip("Receives the created instance, including when restoring a completed sequence.")]
        public UnityEvent<GameObject> onSpawned = new UnityEvent<GameObject>();
    }
}
