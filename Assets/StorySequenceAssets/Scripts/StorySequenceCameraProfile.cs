using UnityEngine;

namespace Lit.Story
{
    [CreateAssetMenu(
        fileName = "StoryCameraProfile",
        menuName = "Lit/Story/Camera Profile",
        order = 20)]
    public sealed class StorySequenceCameraProfile : ScriptableObject
    {
        [Tooltip("Offset exprime dans le repere du locuteur.")]
        public Vector3 localCameraOffset = new Vector3(0.85f, 1.65f, 2.4f);
        [Tooltip("Offset du point regarde par rapport a l'ancre visage du locuteur.")]
        public Vector3 lookAtOffset = new Vector3(0f, 0.05f, 0f);
        [Range(15f, 100f)] public float fieldOfView = 45f;
        [Min(0f)] public float transitionDuration = 0.6f;
        [Min(0f), Tooltip("Vitesse de suivi du locuteur apres la transition.")]
        public float followSharpness = 12f;
        [Tooltip("Cadre le milieu entre locuteur et interlocuteur.")]
        public bool frameSpeakerAndListener;
        [Range(0f, 1f)] public float speakerToListenerLookWeight = 0.35f;
    }
}
