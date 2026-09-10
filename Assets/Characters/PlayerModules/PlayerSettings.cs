using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public sealed class PlayerSettings
{
    [Tooltip("Controle, locomotion, orientation, vol et franchissement via UCC.")] public PlayerLocomotionSettings locomotion = new PlayerLocomotionSettings();
    [Tooltip("Configuration du module LitUccInteractionBridge, independante des autres modules.")] public PlayerInteractionSettings interactions = new PlayerInteractionSettings();
    [Tooltip("Configuration du module RealTimeCombatLoadout, independante des autres modules.")] public PlayerEquipmentSettings equipment = new PlayerEquipmentSettings();
    [Tooltip("Configuration du module LocomotionAnimationEvent, independante des autres modules.")] public PlayerFootstepSettings footsteps = new PlayerFootstepSettings();
    [Tooltip("Configuration du module FacialExpressionController, independante des autres modules.")] public PlayerFaceSettings face = new PlayerFaceSettings();
    [Tooltip("Configuration du module PlayerStateMotionController, independante des autres modules.")] public PlayerTrajectorySettings trajectories = new PlayerTrajectorySettings();
    [Tooltip("Impulsion et freinage des dash declenches par les clips.")] public PlayerDashSettings dash = new PlayerDashSettings();
    [Tooltip("Saut et atterrissage")] public PlayerJumpSettings jump = new PlayerJumpSettings();
    [Tooltip("Esquive et mouvements d action")] public PlayerDodgeSettings dodge = new PlayerDodgeSettings();
}

[System.Serializable]
public sealed class PlayerJumpSettings
{
    [Tooltip("Hauteur maximale du saut en metres. Determine l impulsion verticale.")] public float jumpHeight = 5f;
    [Tooltip("Instant du decollage dans Jump_Start, de 0 a 1.")] public float jumpStartTakeoffNormalizedTime = 0.13f;
    [Tooltip("Part de vitesse horizontale retiree au decollage : 0 conserve l inertie, 1 la supprime.")] public float jumpStartPlanarSlowdown = 0f;
    [Tooltip("Vitesse verticale en m/s sous laquelle la gravite du sommet est appliquee.")] public float apexGravityEntryVelocity = 2.4f;
    [Tooltip("Multiplicateur de gravite pres du sommet du saut.")] public float apexGravityMultiplier = 0.28f;
    [Tooltip("Multiplicateur de gravite pendant la descente.")] public float descentGravityMultiplier = 0.72f;
    [Tooltip("Vitesse verticale en m/s qui declenche l animation de chute.")] public float fallingAnimationEntryVelocity = -0.1f;
    [Tooltip("Hauteur de chute minimale en metres pour un atterrissage lourd.")] public float hardLandingHeight = 3f;
    [Tooltip("Conditions de contact, stabilisation et transition avant de rendre la locomotion.")] public MotionHandoffProfile landingHandoff = new MotionHandoffProfile {
        minimumContactSeconds = 0.15f,
        animationExitNormalizedTime = 0.82f,
        planarSettledSpeed = 0.12f,
        verticalSettledSpeed = 0.2f,
        planarDampingPerSecond = 7f,
        maximumSettleSeconds = 0.55f,
        locomotionBlendSeconds = 0.12f,
        preLandingProbeDistance = 1.2f,
        preLandingLeadSeconds = 0.14f
    };
}

[System.Serializable]
public sealed class PlayerDodgeSettings
{
    [Tooltip("Impulsion horizontale initiale de l esquive en m/s.")] public float impulseSpeed = 16f;
    [Tooltip("Multiplicateur de duree des esquives. 1 conserve la duree de l action.")] public float durationMultiplier = 1f;
    [Tooltip("Orienter le personnage vers son deplacement pendant une esquive sans cible verrouillee.")] public bool alignUnlockedDodgeToTravel = true;
    [Tooltip("Avec cible verrouillee, orienter seulement l esquive avant vers son deplacement.")] public bool alignLockedForwardDodgeToTravel = true;
}

[System.Serializable]
public sealed class PlayerDashSettings
{
    [Min(0), Tooltip("Distance en metres visee au-dela de la cible.")] public float dashOvershootDistance = 1.25f;
    [Min(0), Tooltip("Impulsion ajoutee par metre de distance a parcourir.")] public float dashImpulsePerMeter = 8f;
    [Min(0), Tooltip("Impulsion minimale du dash en m/s.")] public float minimumDashImpulse = 14f;
    [Min(0), Tooltip("Impulsion maximale du dash en m/s.")] public float maximumDashImpulse = 32f;
    [Min(0), Tooltip("Duree de blocage des entrees en secondes apres l impulsion.")] public float dashInputLockSeconds = .1f;
    [Min(.01f), Tooltip("Duree maximale du freinage en secondes.")] public float stopDashDuration = .18f;
    [Min(0), Tooltip("Deceleration du dash en metres par seconde carree.")] public float stopDashDeceleration = 65f;
}

[System.Serializable]
public sealed class PlayerInteractionSettings
{
[Tooltip("Exiger le contact avec le sol pour interagir.")] public bool requireGroundedForLitInteractions = true;
[Tooltip("Autoriser les interactions en position accroupie.")] public bool allowWhileHeightChange = true;
[Tooltip("Autoriser les interactions pendant un changement d allure.")] public bool allowWhileSpeedChange = true;
[Tooltip("Autoriser les interactions pendant la chute.")] public bool allowWhileFall;
[Tooltip("Autoriser les interactions pendant le saut.")] public bool allowWhileJump;
[Tooltip("Bloquer les interactions pendant une capacite d objet Opsive.")] public bool blockWhileItemAbilityActive = true;
}

[System.Serializable]
public sealed class PlayerEquipmentSettings
{
[Tooltip("Actions equipees dans les huit emplacements de combat. Un emplacement vide est indisponible.")] public List<CombatAttackDefinition> equippedAttacks = new List<CombatAttackDefinition>(8);
[Tooltip("Competence de lumiere equipee; vide pour aucune.")] public LightSkillSO equippedLightSkill;
}

[System.Serializable]
public sealed class PlayerFootstepSettings
{
[Tooltip("Surface utilisee si aucune surface specifique n est detectee.")] public SurfaceDefinition defaultSurface;
[Tooltip("Couches recherchees par les rayons de detection du sol.")] public LayerMask groundMask = ~0;
[Min(0f)] [Tooltip("Hauteur du depart du rayon de sol en metres.")] public float raycastHeight = 0.4f;
[Min(0.05f)] [Tooltip("Longueur du rayon de sol en metres.")] public float raycastDistance = 1.6f;
[Tooltip("Rechercher automatiquement les os des pieds sur un Animator humanoide.")] public bool autoResolveHumanoidFeet = true;
[Min(0f)] [Tooltip("Hauteur du rayon de contact au-dessus de chaque pied en metres.")] public float footRaycastHeight = 0.35f;
[Min(0.05f)] [Tooltip("Longueur du rayon de contact de chaque pied en metres.")] public float footRaycastDistance = 0.8f;
[Min(0f)] [Tooltip("Intervalle minimal en secondes entre deux sons de pas.")] public float minimumFootstepInterval = 0.05f;
[Range(0f, 1f)] [Tooltip("Poids minimal de l animation pour accepter un evenement de pas.")] public float minimumAnimationEventWeight = 0.5f;
}

[System.Serializable]
public sealed class PlayerFaceSettings
{
[Tooltip("Nom du renderer recherche si le visage n est pas assigne.")] public string preferredRendererName = "CC_Base_Body";
[Tooltip("Rechercher automatiquement le renderer du visage au lancement.")] public bool autoResolveFaceRenderer = true;
[Tooltip("Nom de l os recherche si la machoire n est pas assignee.")] public string jawRootName = "CC_Base_JawRoot";
[Tooltip("Rechercher automatiquement l os de la machoire.")] public bool autoResolveJawRoot = true;
[Tooltip("Retablir la pose neutre de la machoire apres les animations du corps.")] public bool enforceNeutralJawPoseInLateUpdate = true;
[Tooltip("Expressions faciales disponibles. Les ressources restent partagees.")] public List<FacialExpressionPreset> presets = new List<FacialExpressionPreset>();
[Tooltip("Expression appliquee au lancement du personnage.")] public FacialEmotion initialPassiveEmotion = FacialEmotion.Idle;
[Tooltip("Appliquer automatiquement l expression initiale au lancement.")] public bool playInitialPassiveOnStart = true;
[Tooltip("Faire avancer les transitions du visage en temps reel, meme pendant un ralentissement.")] public bool useUnscaledTime;
[Tooltip("Retablir les BlendShapes du visage apres les animations du corps.")] public bool enforceControlledWeightsInLateUpdate = true;
}

[System.Serializable]
public sealed class PlayerTrajectorySettings
{
[Tooltip("Bibliotheque partagee des trajectoires animees; vide desactive ces trajectoires.")] public PlayerStateMotionLibrary library;
}

[System.Serializable]
public sealed class PlayerLocomotionSettings
{
    [Tooltip("Journaliser le franchissement pour diagnostiquer ses interruptions.")] public bool logScriptedTraversalDiagnostics;
    [Min(1), Tooltip("Nombre de ticks entre deux traces de franchissement.")] public int scriptedTraversalDiagnosticTickInterval = 8;
    [Min(0f), Tooltip("Deplacement externe en metres a partir duquel signaler une correction pendant le franchissement.")] public float scriptedTraversalExternalCorrectionDistance = 0.02f;
    [Range(0f, 45f), Tooltip("Rotation externe en degres a partir de laquelle signaler une correction.")] public float scriptedTraversalExternalCorrectionDegrees = 1f;
    [Tooltip("Transmettre les commandes de la squad a UCC et desactiver la simulation de mouvement Lit.")] public bool driveFromSquadFacade = true;
    [Tooltip("Transmettre le mouvement au champ OverrideInput du handler UCC.")] public bool overrideOpsiveHandlerInput = true;
    [Tooltip("Orienter la source de regard selon le mouvement en espace monde.")] public bool orientLookSourceFromMovement = true;
    [Tooltip("Configurer le Rigidbody sans gravite et cinematique pendant le pilotage UCC.")] public bool configureRigidbodyForOpsive = true;
    [Tooltip("Ajouter une impulsion UCC au demarrage ou a la reprise du sprint.")] public bool enableRunStartResponse = true;
    [Min(0f), Tooltip("Vitesse horizontale maximale ajoutee au demarrage de la course, en m/s.")] public float runStartVelocityBonus = 0.55f;
    [Min(0f), Tooltip("Intervalle minimal entre deux impulsions de demarrage, en secondes.")] public float runStartResponseCooldown = 0.25f;
    [Min(0f), Tooltip("Vitesse horizontale maximale permettant l impulsion de demarrage, en m/s.")] public float runStartResponseMaximumPlanarSpeed = 4.25f;
    [Tooltip("Journaliser les impulsions de demarrage de locomotion.")] public bool logLocomotionResponseDiagnostics;
    [Tooltip("Installer automatiquement les adaptateurs compagnons requis par UCC.")] public bool autoInstallCompanionBridges = true;
    [Range(0f, 0.5f)] [Tooltip("Amplitude minimale de l entree pour accepter un mouvement.")] public float movementDeadZone = 0.08f;
    [Tooltip("Adapter les tolerances UCC aux petits reliefs du sol.")] public bool relaxGroundReliefTolerance = true;
    [Min(0f), Tooltip("Hauteur minimale de marche franchissable, en metres.")] public float groundReliefMinStepHeight = 0.6f;
    [Range(0f, 89f), Tooltip("Pente minimale autorisee par l adaptation du relief, en degres.")] public float groundReliefMinSlopeLimit = 58f;
    [Min(0f), Tooltip("Distance minimale de maintien au sol, en metres.")] public float groundReliefMinStickToGroundDistance = 0.55f;
    [Tooltip("Blends stronger relief tolerance while scripted locomotion is moving across uneven surfaces.")]
    [UnityEngine.Serialization.FormerlySerializedAs("adaptRootMotionGroundRelief")] public bool adaptMovingGroundRelief = true;
    [UnityEngine.Serialization.FormerlySerializedAs("rootMotionMovingStepHeight")]
    [Min(0f)] [Tooltip("Hauteur de marche franchissable en mouvement, en metres.")] public float movingStepHeight = 0.58f;
    [UnityEngine.Serialization.FormerlySerializedAs("rootMotionMovingSlopeLimit")]
    [Range(0f, 89f)] [Tooltip("Pente autorisee en mouvement, en degres.")] public float movingSlopeLimit = 62f;
    [UnityEngine.Serialization.FormerlySerializedAs("rootMotionMovingStickToGroundDistance")]
    [Min(0f)] [Tooltip("Distance de maintien au sol en mouvement, en metres.")] public float movingStickToGroundDistance = 0.86f;
    [UnityEngine.Serialization.FormerlySerializedAs("rootMotionIdleStickToGroundDistance")]
    [Min(0f)] [Tooltip("Distance de maintien au sol a l arret, en metres.")] public float idleStickToGroundDistance = 0.64f;
    [UnityEngine.Serialization.FormerlySerializedAs("rootMotionGroundReliefAdaptationSpeed")]
    [Min(0f)] [Tooltip("Vitesse de transition entre les tolerances de sol au repos et en mouvement.")] public float groundReliefAdaptationSpeed = 7.5f;
    [Tooltip("Autoriser le vol pilote par UCC.")] public bool enableUccFlight = true;
    [Min(0f)] [Tooltip("Vitesse verticale du decollage, en m/s.")] public float flightTakeoffVerticalSpeed = 6.5f;
    [Min(0f)] [Tooltip("Duree du decollage, en secondes.")] public float flightTakeoffDuration = 0.45f;
    [Min(0f)] [Tooltip("Amortissement du mouvement pendant le decollage.")] public float flightTakeoffDamping = 16f;
    [Min(0f)] [Tooltip("Vitesse horizontale de vol normal, en m/s.")] public float flightCruiseSpeed = 33f;
    [Min(0f)] [Tooltip("Vitesse horizontale de vol accelere, en m/s.")] public float flightBoostSpeed = 81f;
    [Min(0f)] [Tooltip("Acceleration horizontale du vol normal, en m/s2.")] public float flightAcceleration = 54f;
    [Min(0f)] [Tooltip("Acceleration horizontale du vol accelere, en m/s2.")] public float flightBoostAcceleration = 126f;
    [Min(0f)] [Tooltip("Freinage horizontal du vol, en m/s2.")] public float flightDeceleration = 36f;
    [Min(0f)] [Tooltip("Vitesse verticale maximale du vol, en m/s.")] public float flightVerticalSpeed = 24f;
    [Min(0f)] [Tooltip("Acceleration verticale du vol, en m/s2.")] public float flightVerticalAcceleration = 66f;
    [Min(0f)] [Tooltip("Freinage vertical du vol, en m/s2.")] public float flightVerticalDeceleration = 54f;
    [Range(0f, 0.4f)] [Tooltip("Amplitude minimale de l entree verticale pendant le vol.")] public float flightVerticalDeadZone = 0.05f;
    [Min(0f)] [Tooltip("Vitesse en dessous de laquelle presenter un vol immobile, en m/s.")] public float flightIdleSpeedThreshold = 0.08f;
    [Min(0f)] [Tooltip("Vitesse de rotation du vol normal, en degres/s.")] public float flightTurnRate = 760f;
    [Min(0f)] [Tooltip("Vitesse de rotation du vol accelere, en degres/s.")] public float flightBoostTurnRate = 460f;
    [Min(0f)] [Tooltip("Vitesse de descente pour l atterrissage du vol, en m/s.")] public float flightLandingSpeed = 12f;
    [Min(0f)] [Tooltip("Acceleration de la descente vers le sol, en m/s2.")] public float flightLandingAcceleration = 36f;
    [Min(0f), Tooltip("Vitesse d atterrissage apres une competence aerienne, en m/s.")] public float combatSkillLandingSpeed = 14f;
    [Tooltip("Autoriser le mode de vol autonome existant lorsque le chemin UCC ne peut pas piloter le vol.")] public bool allowStandaloneFlightFallback = true;
    [Min(0f)] [Tooltip("Marge de collision du vol autonome, en metres.")] public float fallbackFlightCollisionSkin = 0.03f;
    [Min(0f)] [Tooltip("Distance de recherche du sol du vol autonome, en metres.")] public float fallbackFlightGroundProbeDistance = 0.2f;
    [Tooltip("Mettre a jour les parametres de locomotion de l Animator depuis le bridge.")] public bool driveLitLocomotionAnimatorParameters = false;
    [Tooltip("Nom exact du parametre Animator pour speed.")] public string speedParam = "Speed";
    [Tooltip("Nom exact du parametre Animator pour horizontal movement.")] public string horizontalMovementParam = "HorizontalMovement";
    [Tooltip("Nom exact du parametre Animator pour forward movement.")] public string forwardMovementParam = "ForwardMovement";
    [Tooltip("Nom exact du parametre Animator pour is moving.")] public string isMovingParam = "IsMoving";
    [Tooltip("Nom exact du parametre Animator pour locomotion tier.")] public string locomotionTierParam = "LocomotionTier";
    [Tooltip("Nom exact du parametre Animator pour combat move magnitude.")] public string combatMoveMagnitudeParam = "CombatMoveMagnitude";
    [Tooltip("Nom exact du parametre Animator pour turn.")] public string turnParam = "Turn";
    [Tooltip("Nom exact du parametre Animator pour flight state.")] public string flightStateParam = "FlightState";
    [Tooltip("Nom exact du parametre Animator pour flight speed.")] public string flightSpeedParam = "FlightSpeed";
    [Tooltip("Nom exact du parametre Animator pour flight vertical.")] public string flightVerticalParam = "FlightVertical";
    [Tooltip("Nom exact du parametre Animator pour flight boost.")] public string flightBoostParam = "FlightBoost";
    [Tooltip("Nom exact du parametre Animator pour flight start trigger.")] public string flightStartTriggerParam = "FlightStartTrigger";
    [Tooltip("Vitesse de reference de la marche pour la presentation animee.")] public float walkPresentationSpeed = 1.35f;
    [Tooltip("Vitesse de reference de la course pour la presentation animee.")] public float runPresentationSpeed = 3.25f;
    [Min(1f), Tooltip("Vitesse de rotation vers la cible de combat, en degres/s.")] public float combatFacingSpeedDegreesPerSecond = 900f;
    [Range(0.01f, 0.5f), Tooltip("Seuil d entree laterale pour reconnaitre une orbite autour de la cible.")] public float combatOrbitPureLateralThreshold = 0.12f;
    [Min(0f), Tooltip("Tolerance du rayon d orbite avant correction, en metres.")] public float combatOrbitRadiusDeadZone = 0.035f;
    [Min(0f), Tooltip("Intensite de correction du rayon d orbite autour de la cible.")] public float combatOrbitRadiusCorrectionGain = 2.25f;
    [Range(0f, 1f), Tooltip("Correction maximale appliquee pour maintenir le rayon d orbite.")] public float combatOrbitMaximumCorrection = 0.35f;
    [Tooltip("Journaliser les mouvements autour d une cible verrouillee.")] public bool logCombatLockMotionDiagnostics;
    [Tooltip("Activer les diagnostics de blocage par le sol.")] public bool debugGroundBlockDiagnostics;
    [Min(0.1f), Tooltip("Intervalle entre les diagnostics de blocage, en secondes.")] public float groundBlockDiagnosticInterval = 0.75f;
    [Min(0.001f), Tooltip("Progression minimale attendue avant de signaler un blocage, en metres.")] public float groundBlockDiagnosticMinProgress = 0.04f;
    [Tooltip("Activer la presentation de locomotion au sol du projet.")] public bool enableCinematicGroundedFeel = true;
    [Tooltip("Appliquer les reglages physiques au sol definis ici au moteur UCC.")] public bool tuneGroundedUccPhysics = true;
    [Tooltip("Acceleration du moteur UCC au sol.")] public Vector3 groundedMotorAcceleration = new Vector3(4.6f, 0f, 4.6f);
    [Min(0f)] [Tooltip("Amortissement du moteur UCC au sol.")] public float groundedMotorDamping = 5.7f;
    [Range(0f, 1f)] [Tooltip("Influence de l acceleration precedente sur le mouvement actuel.")] public float groundedPreviousAccelerationInfluence = 0.88f;
    [Range(0f, 1f)] [Tooltip("Multiplicateur de deplacement vers l arriere.")] public float groundedBackwardsMultiplier = 0.6f;
    [Min(0f)] [Tooltip("Gravite UCC appliquee au personnage au sol.")] public float groundedGravityAmount = 0.65f;
    [Min(0f)] [Tooltip("Distance de maintien au sol, en metres.")] public float groundedStickToGroundDistance = 0.72f;
    [Range(0f, 89f)] [Tooltip("Pente maximale autorisee au sol, en degres.")] public float groundedSlopeLimit = 60f;
    [Min(0f)] [Tooltip("Hauteur maximale de marche franchissable, en metres.")] public float groundedMaxStepHeight = 0.5f;
    [Min(0f)] [Tooltip("Vitesse de separation des plateformes mobiles.")] public float groundedMovingPlatformSeparationVelocity = 7f;
    [Range(0f, 1f)] [Tooltip("Multiplicateur de mouvement conserve en quittant une plateforme.")] public float groundedMovingPlatformDisconnectMultiplier = 0.65f;
    [Min(0f)] [Tooltip("Amortissement des forces transmises par les plateformes mobiles.")] public float groundedMovingPlatformForceDamping = 0.18f;
    [Min(0f)] [Tooltip("Vitesse de montee de l entree de marche filtree.")] public float groundedInputAcceleration = 6.2f;
    [Min(0f)] [Tooltip("Vitesse de montee de l entree de sprint filtree.")] public float groundedSprintInputAcceleration = 5f;
    [Min(0f)] [Tooltip("Vitesse de retour au repos de l entree filtree.")] public float groundedInputDeceleration = 6.2f;
    [Min(0f)] [Tooltip("Vitesse de reponse lors d un changement de direction.")] public float groundedDirectionChangeAcceleration = 11.2f;
    [Range(-1f, 1f)] [Tooltip("Produit scalaire sous lequel reconnaitre un changement important de direction.")] public float groundedDirectionChangeDot = 0.2f;
    [Tooltip("Configurer la capacite UCC SpeedChange pour le sprint.")] public bool tuneGroundedSprintSpeedChange = true;
    [Range(1f, 2.4f)] [Tooltip("Multiplicateur de vitesse pendant le sprint.")] public float groundedSprintSpeedMultiplier = 1.65f;
    [Min(0f)] [Tooltip("Valeur du parametre de vitesse Animator pendant le sprint.")] public float groundedSprintSpeedParameterValue = 2.6f;
    [Tooltip("Empecher SpeedChange de concurrencer le pilotage de vitesse Animator du bridge.")] public bool suppressSpeedChangeAnimatorParameter = true;
    [Tooltip("Nom exact du parametre Animator pour move start trigger.")] public string moveStartTriggerParam = "MoveStartTrigger";
    [Tooltip("Nom exact du parametre Animator pour move stop trigger.")] public string moveStopTriggerParam = "MoveStopTrigger";
    [Tooltip("Nom exact du parametre Animator pour turn in place.")] public string turnInPlaceParam = "TurnInPlace";
    [Min(0.01f)] [Tooltip("Conversion de la vitesse physique vers le blend de locomotion.")] public float groundedAnimationSpeedToBlend = 0.48f;
    [Min(0f)] [Tooltip("Vitesse de montee du parametre de vitesse animee.")] public float groundedAnimatorSpeedRiseRate = 13.5f;
    [Min(0f)] [Tooltip("Vitesse de diminution du parametre de vitesse animee.")] public float groundedAnimatorSpeedFallRate = 5.4f;
    [Min(0f)] [Tooltip("Vitesse de mise a jour du parametre de rotation animee.")] public float groundedAnimatorTurnRate = 5.4f;
    [Range(0f, 1f)] [Tooltip("Angle minimal pour presenter une rotation sur place, en degres.")] public float groundedTurnInPlaceThreshold = 0.55f;
    [Min(0f)] [Tooltip("Vitesse maximale permettant la rotation sur place.")] public float groundedTurnInPlaceMaxSpeed = 0.35f;
    [Tooltip("Autoriser les clips de rotation sur place.")] public bool enableGroundedTurnInPlaceClips = false;

    [Min(0f), Tooltip("Delai avant de demander la presentation d arret, en secondes.")] public float groundedStopRequestDelay = 0.06f;
    [Min(0f), Tooltip("Vitesse en dessous de laquelle le personnage est considere stabilise.")] public float groundedStopSettledSpeed = 0.06f;
    [Min(0f), Tooltip("Duree de stabilisation requise avant de terminer l arret, en secondes.")] public float groundedStopSettledDuration = 0.05f;
    [Range(0f, 1f)] [Tooltip("Temps normalise minimal pour sortir de l animation d arret.")] public float groundedStopExitNormalizedTime = 0.9f;
    [Min(0f), Tooltip("Duree de maintien de direction pendant une transition de mouvement, en secondes.")] public float groundedMoveTransitionDirectionHoldTime = 0.18f;
    [Min(0f), Tooltip("Vitesse des parametres pendant une transition de mouvement.")] public float groundedMoveTransitionParameterSpeed = 1.22f;
    [Tooltip("Utiliser les animations avant pour la locomotion au sol.")] public bool useForwardOnlyGroundedLocomotion = true;
    [Tooltip("Uses root-motion turn clips when movement starts from a sharp angle change. Disabled by default because exploration direction changes must keep moving instead of pivoting on the spot.")]
    [UnityEngine.Serialization.FormerlySerializedAs("enableRootMotionPivotTurns")] public bool enableScriptedPivotTurns = false;
    [Range(45f, 180f)] [Tooltip("Angle minimal declenchant un pivot, en degres.")] public float groundedPivotMinAngle = 85f;
    [Range(90f, 180f)] [Tooltip("Angle minimal selectionnant le pivot a 180 degres.")] public float groundedPivot180Angle = 135f;




    [Min(0f)] [Tooltip("Vitesse maximale permettant un pivot.")] public float groundedPivotMaxSpeed = 0.45f;
    [Range(0f, 1f)] [Tooltip("Entree filtree maximale permettant un pivot.")] public float groundedPivotMaxSmoothedInput = 0.14f;
    [Min(0.05f)] [Tooltip("Duree de maintien du pivot, en secondes.")] public float groundedPivotHoldTime = 0.32f;
    [Min(0f)] [Tooltip("Intervalle minimal entre deux pivots, en secondes.")] public float groundedPivotCooldown = 0.34f;
    [Min(0f), Tooltip("Duree de tolerance au debut d un mouvement pour demander un pivot, en secondes.")] public float groundedPivotStartGraceTime = 0.12f;
    [Range(45f, 180f), Tooltip("Angle minimal du pivot pendant la tolerance de demarrage, en degres.")] public float groundedPivotStartGraceMinAngle = 128f;
    [Range(0f, 1f), Tooltip("Temps normalise du pivot a partir duquel rendre le mouvement.")] public float groundedPivotMovementReleaseStart = 0.38f;
    [Range(0f, 180f), Tooltip("Ecart angulaire maximal pour rendre le mouvement pendant le pivot, en degres.")] public float groundedPivotMovementReleaseMaxAngle = 72f;
    [Range(0f, 1f), Tooltip("Part de mouvement rendue progressivement pendant le pivot.")] public float groundedPivotMovementReleaseScale = 0.58f;
    [Tooltip("Commits the gameplay root rotation toward the authored turn target so turn clips cannot visually rotate and then snap back.")]
    [UnityEngine.Serialization.FormerlySerializedAs("commitRootRotationDuringPivot")] public bool commitBodyRotationDuringPivot = true;
    [Min(1f)] [Tooltip("Vitesse de rotation du corps pendant le pivot.")] public float groundedPivotRotationCommitRate = 960f;
    [Tooltip("Enregistrer les diagnostics de locomotion.")] public bool recordLocomotionDiagnostics;
    [Tooltip("Journaliser les diagnostics de locomotion.")] public bool debugLocomotionDiagnostics;
    [Min(0.1f), Tooltip("Intervalle entre les diagnostics de locomotion, en secondes.")] public float locomotionDiagnosticInterval = 0.1f;
    [Tooltip("Autoriser le franchissement des obstacles admissibles.")] public bool enableObstacleTraversal = true;
    [Min(0f), Tooltip("Hauteur en dessous de laquelle ignorer le franchissement, en metres.")] public float ignoredObstacleMaxHeight = 0.18f;
    [Min(0f), Tooltip("Hauteur maximale franchissable, en metres.")] public float traversableObstacleMaxHeight = 0.9f;
    [Min(0.05f), Tooltip("Distance de recherche d obstacle devant le personnage, en metres.")] public float obstacleProbeDistance = 0.75f;
    [Min(0.01f), Tooltip("Rayon de detection des obstacles, en metres.")] public float obstacleProbeRadius = 0.22f;
    [Min(0f), Tooltip("Hauteur de depart de la recherche d obstacle, en metres.")] public float obstacleProbeBaseHeight = 0.25f;
    [Range(0f, 1f), Tooltip("Limite de produit scalaire avec la verticale pour reconnaitre une surface franchissable.")] public float obstacleTraversalMaxSurfaceUpDot = 0.35f;
    [Min(0f), Tooltip("Distance visee au-dela de l obstacle, en metres.")] public float obstacleLandingDistance = 0.55f;
    [Min(0.01f), Tooltip("Duree du franchissement, en secondes.")] public float obstacleTraversalDuration = 0.42f;
    [Min(0f), Tooltip("Hauteur de base de la trajectoire de franchissement, en metres.")] public float obstacleTraversalArcHeight = 0.25f;
    [Min(0f), Tooltip("Marge au-dessus de l obstacle, en metres.")] public float obstacleTraversalTopClearance = 0.16f;
    [Range(0f, 1f), Tooltip("Influence de la hauteur d obstacle sur la hauteur de trajectoire.")] public float obstacleTraversalHeightArcMultiplier = 0.38f;
    [Range(0.1f, 1f), Tooltip("Anticipation de rotation pendant le franchissement.")] public float obstacleTraversalRotationLead = 0.68f;
    [Range(0f, 1f), Tooltip("Amplitude minimale de mouvement pour declencher un franchissement.")] public float obstacleTraversalMinInputMagnitude = 0.34f;
    [Min(0f), Tooltip("Intervalle minimal entre deux franchissements, en secondes.")] public float obstacleTraversalCooldown = 0.28f;
    [Tooltip("Couches physiques recherchees pour les obstacles.")] public LayerMask obstacleTraversalMask = ~0;
    [Tooltip("Nom exact du parametre Animator pour obstacle traversal trigger.")] public string obstacleTraversalTriggerParam = "ObstacleTraversal";
    [Tooltip("Activer le lissage d orientation du personnage.")] public bool enableCinematicOrientationFeel = true;
    [Range(0f, 1f)] [Tooltip("Amplitude minimale de l entree pour modifier l orientation.")] public float orientationInputDeadZone = 0.14f;
    [Min(1f)] [Tooltip("Vitesse de rotation pendant la marche, en degres/s.")] public float orientationWalkTurnRate = 360f;
    [Min(1f)] [Tooltip("Vitesse de rotation pendant le sprint, en degres/s.")] public float orientationSprintTurnRate = 300f;
    [Min(1f), Tooltip("Vitesse de rotation pour un virage important, en degres/s.")] public float orientationSharpTurnRate = 300f;
    [Range(0f, 180f)] [Tooltip("Angle a partir duquel appliquer le virage rapide, en degres.")] public float orientationSharpTurnAngle = 92f;
    [Range(0f, 1f), Tooltip("Poids de la vitesse physique dans la direction d orientation.")] public float orientationVelocityBlend = 0.1f;
}