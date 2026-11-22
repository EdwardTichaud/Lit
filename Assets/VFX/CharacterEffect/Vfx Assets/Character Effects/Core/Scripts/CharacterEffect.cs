using UnityEngine;

namespace INab.VFXAssets
{
    [ExecuteAlways]
    public class CharacterEffect : UniformMeshSample
    {
        public override string DefaultPrefabPath => "Assets/VFX/CharacterEffect/Vfx Assets/Character Effects/Effect Prefabs/";

        [Tooltip("Play automatically on non-playable objects. Playable characters and their equipment always require an explicit function call.")]
        public bool playOnEnable = true;
        public EffectState effectState = EffectState.Off;
        private bool rendererShown;

        protected override void OnEnable()
        {
            // Ignore the serialized preview state when the object starts a new session.
            currentEffectState = EffectState.Off;
            base.OnEnable();
            rendererShown = IsRendererShown;
            if (PlaysAutomatically && rendererShown) StartEffect();
            else StopEffect();
        }

        private bool IsRendererShown => meshRenderer != null && meshRenderer.enabled && meshRenderer.gameObject.activeInHierarchy;

        // Include inactive owners so unequipped weapons follow the same gameplay policy.
        private bool PlaysAutomatically => playOnEnable && GetComponentInParent<SquadCharacterController>(true) == null;

        protected override void Update()
        {
            base.Update();
            bool shown = IsRendererShown;
            if (shown != rendererShown && PlaysAutomatically)
            {
                if (shown) StartEffect(); else StopEffect();
            }
            rendererShown = shown;
        }

        protected override void OnDisable()
        {
            StopEffect();
            base.OnDisable();
        }

        public override void StartEffect()
        {
            base.StartEffect();
            effectState = currentEffectState;
        }

        public override void StopEffect()
        {
            base.StopEffect();
            effectState = currentEffectState;
        }

        // Keep existing Animation Events and UnityEvents compatible.
        public void PlayEffect_CharacterEffect() => StartEffect();
        public void StopEffect_CharacterEffect() => StopEffect();
    }
}
