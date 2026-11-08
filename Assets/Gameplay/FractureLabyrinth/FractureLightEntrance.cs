using UnityEngine;

namespace Lit.Fractures
{
    /// <summary>Camouflaged physical threshold, revealed by the same Flame/torch influence as interactions.</summary>
    public sealed class FractureLightEntrance : MonoBehaviour, ICharacterDetectedInteractable
    {
        public Collider lightProbe;
        public GameObject concealedWall;
        public GameObject revealedFrame;
        public bool IsIlluminated { get; private set; }
        private SquadCharacterController[] actors = System.Array.Empty<SquadCharacterController>();
        private float refreshActorsAt;
        private void OnEnable() { Apply(false, false); }
        private void Update()
        {
            bool fromInside = false;
            if (Time.unscaledTime >= refreshActorsAt)
            { actors = Object.FindObjectsByType<SquadCharacterController>(); refreshActorsAt = Time.unscaledTime + .25f; }
            foreach (var actor in actors)
            {
                if (actor == null || !actor.isActiveAndEnabled) continue;
                Vector3 delta = actor.transform.position - transform.position;
                if (Vector3.Dot(delta, transform.forward) > .1f && delta.sqrMagnitude < 36f)
                { fromInside = true; break; }
            }
            Apply(CharacterInteractionDetection.IsInActiveFlameInfluence(this), fromInside);
        }
        private void Apply(bool lit, bool fromInside)
        {
            IsIlluminated = lit;
            if (concealedWall != null) concealedWall.SetActive(!lit && !fromInside);
            if (revealedFrame != null) revealedFrame.SetActive(lit || fromInside);
        }
        public bool CanBeDetectedBy(SquadCharacterController actor) => false;
        public Collider GetInteractionDetectionCollider() => lightProbe;
        public Transform GetInteractionAnchor() => transform;
        public float GetInteractionMaxDistance(SquadCharacterController actor) => 0;
        public int GetInteractionPriority(SquadCharacterController actor) => 0;
        public void SetDetectedCharacter(GameObject actor) { }
    }
}
