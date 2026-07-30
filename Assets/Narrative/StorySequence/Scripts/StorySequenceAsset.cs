using System;
using System.Collections.Generic;
using Lit.Timeline;
using UnityEngine;
using UnityEngine.Playables;

namespace Lit.Story
{
    public enum StorySequenceStepType
    {
        Fade,
        Dialogue,
        CameraShot,
        Wait,
        AnimatorTrigger,
        Sitting,
        Timeline,
        SceneEvent,
        ProgressivePlayerStop
    }

    [Serializable]
    public sealed class StorySequenceStep
    {
        [Tooltip("Nom lisible dans l'Inspector.")]
        public string label;
        public StorySequenceStepType type;
        [Tooltip("Autorise Interact a terminer cette etape immediatement.")]
        public bool skippable = true;

        [Header("Dialogue")]
        public string actorId;
        public string listenerId;
        public string speakerNameOverride;
        public VoiceLineData voiceLine;
        [HideInInspector]
        public bool waitForInteractAfterLine;
        [Min(0f), Tooltip("Duree maximale d'affichage. 0 = attend indefiniment jusqu'a Interact.")]
        public float dialogueMaxDisplayDuration;
        [Tooltip("Change automatiquement le plan camera vers le locuteur.")]
        public bool focusCameraOnSpeaker = true;
        public StorySequenceCameraProfile dialogueCameraProfile;

        [Header("Camera")]
        [Tooltip("Point de camera declare dans StorySequenceSceneBindings. Vide = cadrage automatique.")]
        public string cameraPointId;
        public StorySequenceCameraProfile cameraProfile;
        [Min(0f)] public float cameraTransitionDuration = 0.6f;
        [Min(0f)] public float cameraHoldDuration;

        [Header("Fade")]
        [Range(0f, 1f)] public float fadeAlpha;
        [Min(0f)] public float fadeDuration = 0.5f;

        [Header("Wait / Animation")]
        [Min(0f)] public float duration = 1f;
        public string animatorTrigger;

        [Header("Sitting")]
        [Tooltip("Applique l'etat assis a tous les personnages actuellement presents dans la squad.")]
        public bool applyToWholeSquad;
        [Tooltip("Actif = assis, inactif = debout.")]
        public bool sitting = true;
        [Tooltip("Commence directement dans Sitting_Idle sans jouer l'animation Sit_Down.")]
        public bool startDirectlyInSittingIdle;

        [Header("Progressive Player Stop")]
        [Min(0.05f), Tooltip("Duree maximale avant de forcer l'arret complet du joueur.")]
        public float playerStopTimeout = 2f;
        [Min(0f), Tooltip("Vitesse horizontale consideree comme un arret.")]
        public float playerStopVelocityThreshold = 0.05f;

        [Header("Timeline")]
        public string directorId;
        public PlayableAsset timeline;
        [Tooltip("Profile explicite de toutes les pistes a resoudre avant lecture.")]
        public TimelineBindingProfile timelineBindingProfile;
        public bool waitForTimelineCompletion = true;

        [Header("Scene Event")]
        public string eventId;
    }

    [CreateAssetMenu(
        fileName = "StorySequence",
        menuName = "Lit/Story/Story Sequence",
        order = 10)]
    public sealed class StorySequenceAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Identifiant stable utilise pour la sauvegarde de progression.")]
        public string sequenceId;
        public string displayName;
        [TextArea] public string description;

        [Header("Playback")]
        public bool playOnce;
        public bool waitForLocalPlayer = true;
        [Min(0f), Tooltip("0 attend indefiniment.")]
        public float localPlayerWaitTimeout;
        public bool lockPlayerControl = true;
        public bool stopActiveAbilitiesOnLock = true;
        public bool useUnscaledTime = true;

        [Header("Automatic Opening")]
        public bool startFromBlack = true;
        [Min(0f)] public float openingFadeDuration = 1f;

        [Header("Automatic Gameplay Handoff")]
        public bool fadeToBlackBeforeGameplay = true;
        [Min(0f)] public float closingFadeDuration = 0.5f;
        public bool fadeFromBlackAfterGameplayRestore = true;
        [Min(0f)] public float gameplayFadeInDuration = 0.75f;

        [Header("Steps")]
        public List<StorySequenceStep> steps = new List<StorySequenceStep>();

        public string ProgressKey
        {
            get
            {
                string id = string.IsNullOrWhiteSpace(sequenceId) ? name : sequenceId.Trim();
                return $"lit.story.sequence.{id}.completed";
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(sequenceId))
            {
                sequenceId = name;
            }

            localPlayerWaitTimeout = Mathf.Max(0f, localPlayerWaitTimeout);
            openingFadeDuration = Mathf.Max(0f, openingFadeDuration);
            closingFadeDuration = Mathf.Max(0f, closingFadeDuration);
            gameplayFadeInDuration = Mathf.Max(0f, gameplayFadeInDuration);
        }
    }
}
