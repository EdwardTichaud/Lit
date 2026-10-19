# Input, UCC et caméra

## Raccord Du Laboratoire Au Jeu (2026-10-19)

Les rotations sont portees par PlayerSettings : marche 720, sprint 1080,
demi-tour et facing combat 1440 degres/s, avec rateLimitedCombatFacing explicite.
Le bridge consomme son clone de module par acteur ; l'arene ne le surcharge plus.
Les profils auteur Juggernaut portent leur tracking a 1080 degres/s, copie dans
leur profil de production sans remplacer les patterns. La camera conserve sa base
large et son rapprochement selon les actions, sans zoom permanent de combat.

CombatWorldRuntime reference le prefab du curseur et son offset monde +3 Y.
Les facades de lock commune et historique le transmettent au meme indicateur.
Le visuel est non parente, dans la scene du proprietaire, sans Canvas ni heritage
de son echelle. Desactivation/destruction liberent uniquement ce visuel ; un
rafraichissement tardif ne le recree pas sur un composant desactive.

ProductionCombatMigration installe GameplayCombatCamera sur la camera existante
de GameplaySessionRoot. Le profil est cree depuis la camera de production :
distance, pitch, FOV, controles, obstacles et amortissements sont preserves.
Seul actionFraming est synchronise avec le profil valide du laboratoire.
TacticalDefault n'est pas modifie ; aucune lumiere de test n'est ajoutee aux zones.
Le zoom manuel, l'inspection et les cinematiques restent prioritaires.

BasicTail est aussi raccorde a la presentation externe reseau : maintien mesure
au serveur, generation/epoque repliquees, cadence propre au Basic et cadrage de
charge pour son proprietaire uniquement. Le relachement restitue la cadence de
la meme session ; la continuation validee ne demande pas de locomotion intermediaire.
Une validation locale automatisee ne remplace pas le controle sur client distant.

## Cadrage De Combat Du Laboratoire

CombatFeelCamera garde la base exploration tactique large (10 m, pitch 50,
FOV 60), selon le retour auteur : pas de rapprochement permanent. Les valeurs de
zoom/orbite choisies manuellement restent la base, jamais ecrasees par une action.
LitTacticalUccViewType applique une distance relative lisse : Basic 0,88, skill
0,78, preparation/frappe adverse proche 0,92 ; repos/recuperation reviennent a 1.
Entree 0,25 s, retour 0,6 s non scales. Le zoom manuel prend priorite pendant
1,5 s apres chaque commande ; L3 inspection et cinematiques ne sont pas recadres.
La marge analytique de projection limite le rapprochement pour garder les corps
participants dans le champ ; ni yaw, ni pitch, ni FOV ne sont changes par ce suivi.
La charge garde son cadrage valide et n'empile pas ce rapprochement automatique.
Le profil TacticalDefault partage reste inchange ; actionFraming est desactive
par defaut. L'outil Configure Action Camera active uniquement le profil du lab.
UCC conserve seul l'ecriture physique de la camera. Le lock et les commandes
existantes ne sont pas remplaces. Lisibilite complete a qualifier en rencontre.

## Preparation Chargee Sur Le Coup Courant (2026-10-19)

Protection renforcee sans changement de ressenti : TryPlay refuse egalement les
appels directs pendant la charge, et la notification directe de degats ne peut
plus annuler sa Basic. Un nettoyage preserveChargedHold conserve aussi la Basic
de destination encore bufferisee. Relachement volontaire et priorites forcees
(mort/cinematique/desactivation) conservent leur nettoyage.

Le mode BasicTail du ChargedSkill lab garde la session Basic terrestre courante.
Une pression bufferisee attend le vrai demarrage de son Basic avant d'armer sa
generation. X tenu ralentit sa fin a 10 %, X relache restitue la cadence en 0,08 s
et laisse finir le geste. Seul le parametre des etats Basic est modifie, pas le
temps global ou Animator.speed. Le verrou UCC et le cadrage de charge sont acquis
au debut de la portion ralentie et restitues par leur generation. La transition
vers Ascension ne passe ni par Idle ni par ChargedPreparation. Le maintien ne
declenche qu'une fois ; le reseau utilise desormais ce meme contrat de preparation.
Une demande explicite de continuite Combat -> Combat preserve egalement les maps
deja actives (comme Exploration -> Combat), sans contourner un contexte superieur.
Cela evite l'annulation artificielle de X lors du premier contact en mode Combat.

## Trois Basics Aeriens Au Laboratoire (2026-10-19)

Lucian_ImpactLab equipe maintenant trois coups aeriens manuels via la liste
CharacterData.airBasicSkills existante. Aucune nouvelle commande : nouvelles
pressions de X, buffer et generations de session habituels. Les deux ajouts
ne suspendent pas la gravite et ne prolongent pas le maintien charge. Le retour
au sol conserve la revalidation de contexte ; aucun combo automatique ajoute.

## ChargedSkill Impulse (2026-10-18)

Le raccord vers le flottement attend l'ability physique, pas un timer Update.
Le prototype Impulse corrige sa progression verticale depuis locomotion.Position,
pas le Transform interpole de rendu ; vitesse bornee et 0,15 s maximum de
rattrapage. Les collisions UCC restent prioritaires. La camera n'est pas changee.

La meme ability LitUccActionTakeoffAbility accepte une courbe de montee du
ChargedSkillSO au lieu d'une vitesse verticale constante. Il ne s'agit pas d'un
second moteur : UCC conserve collisions, sol et gravite a la restitution.
Le prototype garde maintien X, preparation et commandes aeriennes. A expiration
normale du flottement, une demande de chute donne son impulsion initiale ; mort,
cinematique et remplacement prioritaire ne la recoivent pas. La camera de charge
reste celle deja presente ; aucun effet camera de lancement supplementaire.
Le test de deconnexion autorise temporairement les updates joueur du package
en Edit Mode et restaure ce switch en finally. Sans cela, seule la valeur brute
de X changeait, pas la phase de l'action. Le test exige Performed puis canceled,
et une lecture sure du controle retire. Groupe ChargedSkillTests : 8/8 passes
(ChargedInputLifecycleFinalTests.xml), sans modification du runtime d'input.

## Entree des frappes et continuite (2026-10-18)

Sans modifier les bindings, RealTimeCombatInput soumet immediatement les Basics
du runtime libre a la session de presentation, comme au laboratoire. La session
conserve au maximum une nouvelle pression jusqu'a sa transition auteur, sans
auto-repetition du maintien X. Les gardes, roulades, charge et priorites existantes
ne sont pas contournees. Les entrees Animator en secondes fixes remplacent
l'interpretation normalisee de entryBlendSeconds ; les sorties restent inchangees.
Les effets de poids modifies sont uniquement ceux des assets Basic du laboratoire.

Les dashs de contact peuvent utiliser ContactDashMotion.Burst, actuellement
active uniquement au laboratoire : courbe de distance temporelle plutot
qu'une vitesse de convergence. DriveScriptedPlanarMotion reste l'unique
commande moteur ; sol, capsule, BattleWall et bypass cible sont conserves.
Les controles/camera, cooldowns et restitutions par generation ne changent pas.
Pour Rupture, retreatLandingDeceleration du meme profil peut freiner la vitesse
sur l'axe du recul apres un decollage puis atterrissage observes. Ce freinage
UCC cesse avec la generation et ne touche ni vitesse verticale ni axe lateral.
Valeur 60 au laboratoire, 0 en production ; la fin d'animation reste protegee.

## Performance des parametres Animator (2026-10-17)

Le pont UCC et la facade squad consultent un cache de metadonnees par composant
au lieu de recreer `animator.parameters` a chaque parametre ecrit. L'Animator,
le controller et la base d'override, le nombre de parametres et les revisions
d'authoring/session invalident ce cache. Aucun moteur, input, timing ni valeur
d'animation n'est modifie. Preuves et limites : `performance.md`.

## Presentation des participants reseau (2026-10-17)

La garde et la roulade sont admises par le serveur sur l'acteur assigne, avec
horodatage B/Y. Le client ne lance pas de moteur concurrent pour leur replication.
Les fondus cosmetiques, leurs generations et le flottement passif d'Ascension
suivent l'acteur ; leur nettoyage n'interrompt pas une action plus recente.
Une preparation/cinematique perdant son ownership restitue son cadrage local.

Les QTE de paliers passent par ForParticipant sur la presentation emettrice.
Les contacts de Counter/Light/Skill cinematographiques rejoignent le rig de cet
acteur et non le singleton du joueur local. Les non-participants n'acquierent
ni camera, ni map d'input, ni ralentissement. Recette en jeu encore differee.

## Restitution des engagements confirmes

Le client restitue camera/inputs/interactions apres une fin d'engagement recue
pendant une cinematique ou un dash, sans relancer la perception cote client.
La session encore legitime n'est pas annulee ; une nouvelle menace invalide
l'attente. La mort de la cible de lock nettoie le binding de strafe meme sans
autorite serveur. Aucun raccord de camera ni fenetre de contre n'est modifie.

## Impulsion de Rupture et Impact de lumiere

Rupture est une evasion depuis le corps-a-corps : ennemi verrouille obligatoire
a 3 m maximum, y compris au laboratoire (mode SkillSO LockedTarget). Son event de recul utilise
l'oppose de la direction capturee de session, et non une position cible relue
tardivement. Il ne soumet qu'une impulsion par generation et conserve le recul
si le contact est manque apres un lancement valide. Degats et stun restent
conditionnes au contact. Aucune ecriture de Transform ni collision desactivee.

LitUccActionTakeoffAbility est une ability concurrente manuelle UCC, sans
parametre Animator. Pendant 0,08 s elle soumet une intention verticale dans
UpdatePosition avant les collisions, comme Jump, sans ecrire le Transform ni
forcer artificiellement Grounded. La session de presentation annule cette
ability et son propre handle d'impulsion lors d'un remplacement/interruption.
Le handle ne desactive pas le handler UCC quand une session possede deja le
verrou d'input : IsDriving reste vrai pour permettre Rupture -> Impact de lumiere.
La plongee d'Impact utilise UCC et attend le sol reel ; elle ne restaure aucune
position et laisse gravite/collisions actives. Les verrous externes restent prioritaires.

## Croix directionnelle et kits (2026-10-13)

En combat solo : haut Lucian, droite Vesta, bas Soren, gauche Aurell. Dans la
roue, gauche/haut/bas/droite lancent les slots stables 0/1/2/3. B (EstButton)
ferme toutes les roues sans skill ni esquive ; X/Y/A ne lancent plus de skills.
La croix choisit un personnage uniquement lorsque la roue est fermée ; dans
la roue elle lance ses skills (haut puis haut = Éclair). Pour changer de kit,
faire B puis une direction. Le maintien ne relance pas au changement de map.
Start reste disponible via System pour la pause. Le coordinateur
active CombatWheel + Camera + System et les seules actions Player/Move et
Player/RightShoulder pour marcher/sprinter. CombatSkillKitInput en est l'owner.
La réconciliation du stick maintenu et le laboratoire acceptent CombatWheel
comme contexte de locomotion, sans ouvrir les actions habituelles X/Y/A/B.
Compilation et Play du laboratoire validés : mouvement réel avec stick maintenu,
sélection puis lancement directionnel, retour B et maintien sans rebascule, isolation
des boutons de face et restitutions. Logs TwoStepSkillWheel.log et
TwoStepSkillWheelAllKits.log / TwoStepSkillWheelDomainReload.log (retour B testé
sur chaque kit, avec et sans Domain Reload) dans
Library/ActorAnimationSandbox/Logs.
R3 en combat libre : tap pour verrouiller/changer de cible, hold 0,6 s pour
deverrouiller. R3 dans la roue est reserve au ciblage de soutien. Le recadrage
camera R3 et l'ancien binding de fusion sont retires du combat. Hors combat,
boost de torche R3 et commandes monde de la croix restent inchanges.
LT n'ouvre plus la roue : son ancien binding OpenPalette est vide dans l'asset
et le wrapper. La croix directionnelle reste l'entree manette des kits.
CombatWheel autorise la camera : le stick droit continue l'orbite habituelle,
sans reactiver les commandes normales d'attaque. Les
interfaces clavier et les loadouts reseau gardent leur voie existante.
Recette WheelCameraValidationFinal.log : compilation, bindings asset/wrapper,
LT ignore, ouverture croix, input/calcul d'orbite et retour B reussis dans le
laboratoire isole. Le test batch sans focus appelle explicitement ReadInput
et le calcul de vue UCC ; il ne valide pas le ressenti visuel au premier plan.
PlayerInputs.inputactions est la source ; le wrapper est regenere par Unity.
Les champs m_GenerateWrapperCode/m_WrapperCodePath de l'importer sont corriges
pour retrouver aussi la generation automatique lors des prochains imports.

## Fusion automatique de combat (2026-10-13)

Le callback Melt du combat n'est plus abonne : la fusion est possedee par
l'entree/sortie du combat, sans commande manuelle et sans verrou de mouvement.
Le LightSkill ne defusionne plus le personnage quand le combat continue.
Le boost de torche R3 hors combat est conserve. Le raccord de kits ci-dessus
branche les roues directionnelles et le verrouillage R3, separement de la fusion.

## Anti-spam contre

TryHandleEnemyReaction applique Counter Retry Delay Seconds (0,4 s par
defaut) a chaque nouvelle tentative Y, avant de verifier la fenetre ennemie.
Horloge unscaled : le ralenti ne prolonge pas ce verrou. Aucun buffering de
contre ni prolongation sur spam ; le cooldown survit aux ouvertures/fermetures
d'opportunite. Une pression pendant le verrou est consommee (handled=true),
sans lancer de contre ni de nouvelle garde. Une premiere pression autorisee
hors fenetre peut lancer la garde normale ; son maintien/relachement reste
libre. B et les QTE de paliers ne consomment pas ce verrou.

## Raccords camera cinematique (2026-10-11)

LitCameraDirector conserve l'autorite UCC/Cinemachine. Les durées du rig
cameraEntryBlendSeconds/cameraExitBlendSeconds valent 0 par defaut ; seul
CombatLab_CounterRig est configure a 0,25 s. Transition SmoothStep en temps
reel, quaternion Slerp et FOV interpole, destination reevaluee chaque image.
Le raccord est rendu avant la preparation des matrices HDRP puis la camera
retrouve sa pose source : aucune interpolation reinjectee dans les moteurs.
La premiere image, pas le pre-roll, demarre le timer. Retour UCC recalcule
sur la pose actuelle du joueur, sans prolonger les verrous du combat.
Generations idempotentes ; nettoyage du raccord sur desactivation/changement
de scene. Les bakers conservent les reglages et le preview AnimationLab du
CounterSkill utilise le meme rendu, sans controler la camera gameplay.

## Reglage de rotation au laboratoire (2026-10-10)

ConfigureTurnRates active le facing tactique a vitesse bornee dans
LitCombatLockMovementType : compensation du blend yaw moteur, comme en
FreeSprint, pour eviter une restitution de roulade ralentie deux fois.
Le calcul reste dans la simulation UCC et conserve son arc court ; hors
laboratoire le chemin tactique initial est conserve jusqu'a migration.

LucianBrainsCombatArena expose les taux marche/sprint/virage/facing et applique
ConfigureTurnRates au bridge apres binding. Les reglages restent prives a
l'acteur runtime ; CharacterData conserve ses valeurs production. Premiere
passe : 720/1080/1440/1440 degres/s, sans nouvelle autorite de yaw. Le profil
ennemi est egalement clone pour regler 1080 degres/s uniquement au laboratoire.

## Saut interrompu et restitution (2026-10-10)

La presentation Jump_Start suit l'etat destination pendant un fondu : un clip
sortant ne peut plus appliquer une impulsion tardive. Sortie avant decollage,
Animator desactive ou absence de progression pendant 1 s locale libere l'etat
saut actif. Les pauses globales/locales ne consomment pas ce watchdog.
Une impulsion restant au sol sans evenement de decollage est nettoyee apres
0,5 s locale. Desactivation : gravite restituee, triggers et flags effaces.
Aucune vitesse/pose UCC n'est remise a zero ; la chute reste physique.
Les roulades gardent leur nettoyage par generation PlayerAction ; les verrous
externes valides ne sont jamais liberes par ce nettoyage.

## Course libre sous lock (2026-10-10)

LitOpsiveLocomotionBridge resout TargetStrafe, FreeSprint ou DirectionalEvasion
avant la conversion du mouvement. Sans sprint, les axes restent relatifs a la
cible et le regard revient progressivement vers elle. RT + stick hors dead zone
autorise FreeSprint : repere camera, yaw vers le mouvement dans la simulation
LitCombatLockMovementType, animation de course avant InPlace. Le lock camera
reste actif. Garde, actions, traversees et verrous UCC empechent ce changement.
La vue tactique ne constitue plus une seconde autorite pendant un lock.

Tout changement de mode efface le rayon d'orbite et le repere de mouvement
memorise. Squad, laboratoire et NetworkCharacterInput transmettent le sprint
avant le mouvement ; le client reseau convertit le stick une seule fois avant
envoi, le serveur recoit l'intention monde et le sprint.

Une roulade directionnelle capture le vecteur monde du mode courant, prend le
yaw et utilise le clip avant, diagonales comprises. Sans stick, la roulade
arriere reste disponible. PlayerScriptedDodgeController conserve son facing
jusqu'a la restitution de la session, puis reconcilie les inputs tenus : reprise
directe FreeSprint ou retour progressif TargetStrafe, sans snap intermediaire.
Les anciens alignements optionnels lateral/arriere sont retires.

## Commandes manette (2026-10-10)

Le polling du sprint ne depend pas directement d'Application.isFocused :
l'Input System gere le routage du focus et la desactivation des appareils.
Cela preserve la course en Game View lorsque la manette continue d'etre
transmise en mode editeur. Les appareils deconnectes/desactives sont ignores,
et les blocages de mode, focus UI, contexte gamepad et synchronisation reseau
restent prioritaires.
Le sprint accepte explicitement les contextes Gamepad Gameplay et Combat.
IsGameplayInputSuppressed reste reserve au filtrage des actions exploration :
l'utiliser pour le sprint bloquait toute course en combat. UI, Placement et
Cinematic restent exclus, sans modifier les autres callbacks exploration.

LB/L1 eloigne la camera, RB/R1 la rapproche. RT/R2 pilote le sprint dans les
deux vues ; le sprint automatique au stick en camera tactique est retire.
LT/L2 maintenu ouvre la roue de combat ; le relachement la ferme. Croix bas
acquiert le verrou en exploration, puis change de cible en combat. Le callback
Player/LeftShoulder est filtre hors exploration sur gamepad afin de ne pas
executer aussi SwitchEnemyLock. Les noms internes LeftShoulder/RightShoulder
restent historiques ; les bindings .inputactions font autorite.

## Verrous UCC possedes et restitution des actions

TryAcquireExternalLock(owner, out handle, ...) remplace BeginExternalLock/EndExternalLock. Chaque ExternalLockHandle est lie a son bridge et ne peut etre libere qu'une fois par Dispose. Un handle conserve d'un ancien acteur ou d'une ancienne zone ne libere jamais un verrou nouveau. Les options de suppression d'input sont agregees entre proprietaires, y compris lors d'une acquisition imbriquee.

SquadCharacterController et RealTimeCombatManager transmettent ces handles aux cinematiques, sequences narratives, echelles et transitions d'age. Les slots planar motion et les impulsions conservent leur propre handle. Desactiver un proprietaire libere sa ressource, pas les verrous des autres. L'invalidation de scene rend les handles anciens inertes.

La generation PlayerActionPresentationController accompagne la roulade et ses protections. La restitution demande la reconciliation des inputs tenus apres liberation effective ; UI, traversee et cinematique encore actives restent bloquantes. Une fin d'action ne teleporte pas, ne supprime pas la chute et ne redemarre pas un combo interrompu. La fin normale d'un rebond conserve sa trajectoire legitime jusqu'au sol ; son interruption retire le pilotage restant sans remettre la verticale a zero.

## Reglages de locomotion avant affectation Squad

Les blocs playerSettings des quatre fiches ont ete reserialises avec Unity :
la migration textuelle etait presente sur disque mais les donnees importees
conservaient des valeurs par defaut, dont le pilotage Animator desactive et
le saut reduit. Verifier les donnees chargees et leurs clones apres toute
migration, pas seulement le YAML. Les tests couvrent les parametres importes
et la premiere marche/course sur le vrai prefab avec CharacterInfo conserve.

PlayerModuleConfiguration utilise la fiche CharacterInfo tant que le composant
Squad present n'a pas encore de CharacterData. L'affectation ulterieure d'une
fiche Squad renouvelle la copie locale du module. Les quatre fiches joueurs
utilisant Player_Model pilotent LitSpeed (float); Speed reste distinct du
parametre du blend tree Lit. La physique reste geree par Opsive.

## Reservations planaires et modules joueur

BeginScriptedPlanarMotion, DriveScriptedPlanarMotion, ApplyScriptedPlanarImpulse et EndScriptedPlanarMotion exigent un proprietaire. Un module etranger est refuse avant de modifier le verrou UCC. Les consommateurs utilisent leur instance; la desactivation et les changements de zone effacent la reservation. Le saut, l esquive et les trajectoires conservent leurs modules et la physique UCC. Reglages dans CharacterData.playerSettings; references de scripts retrouvees automatiquement.

## Fenetres de reaction ennemies

EnemyAttack ferme l'eligibilite B/Y au contact sans supprimer la protection
de roulade liee a l'action. Il verifie cette protection et l'invulnerabilite
avant garde, degats et feedback. La garde conserve sa reduction et ses inputs;
un feedback de garde propre au SkillSO remplace le feedback global correspondant.
Aucun effet camera ni changement de map n'est introduit par EnemyAttack.

RealTimeCombatInput route les actions Counter/Y et Dodge/B avant garde/roulade
normales vers TryHandleEnemyReaction. La map de combat et le mouvement restent
actifs; seule une sequence de palier utilise CombatQTE et son overlay.
Les actions canceled rearment individuellement B/Y si maintenus a l'ouverture.
Une roulade temporelle refusee n'est jamais bufferisee; les autres boutons ne
consomment pas l'opportunite. Y tardif garde son comportement de garde.
TryDodgeImmediate reutilise les controles, la direction, les collisions et les
priorites de CombatMobilityController; aucun second controleur n'est ajoute.

## Controller du Juggernaut

Contrat simplifie des clips : OpenEnemyReactionOpportunity, orientation engagee, mouvement physique,
hitbox et EndEnemyAttack. Aucune modification de camera depuis ces clips.
Les anciens evenements Warning/Telegraph/ReactionWindow n'ont plus de recepteur
AnimationEvent apres audit des assets; les services partages restent disponibles.
La validation des quatre clips garantit une paire de degats equilibree et une
fin unique. Les evenements physiques conservent leur temps auteur; aucun
changement de locomotion ou de trajectoire n'est introduit par ce nettoyage.

Le contre simple Countered est retire du controller gameplay Juggernaut et de
son installateur. Le clip peut rester utilise par la Timeline CounterSkill.
Le QTE d'attaque reutilise CombatQTE et QTEPanelController, sans autre map ni UI.
La fenetre dure attackQteDuration en temps reel avec un handle global
attackQteTimeScale; fermeture/annulation libere handle, map et intention tenue.
Le CounterSkill conserve sa camera baked et le verrou UCC existant.

CombatLocomotion seul utilise CombatLocomotionPlaybackRate. Idle, actions et
QTE ne recoivent pas ce multiplicateur ; Animator.speed reste hors de ce flux.
Les cycles directionnels sont harmonises sur le clip avant de chaque allure,
sans modifier les evenements, orientations ou poses neutres. Le controle
Toggle Selected Actor Facing Axes affiche root, bassin, torse et cible/reference
selectionnee : les axes des os ne sont pas une consigne de correction automatique.

Les clips gameplay sont des copies InPlace dediees au Juggernaut. Le mode
ScriptedOnly interdit translation/yaw extraits hors cinematique ; UCC Lucian
reste inchange. L'allure demandee pilote LocomotionTier, la vitesse NavMesh
reelle pilote l'activite. Les CrossFade consultent l'etat courant et suivant.
L'ancien installateur ne peut plus restaurer le controller ou les reactions legacy.

`Juggernaut_Model` est une copie specialisee de `Player_Model`, sans ses
transitions d'exploration. Il utilise HorizontalMovement, ForwardMovement,
CombatMoveMagnitude et LocomotionTier pour ses blend trees InPlace. Les skills
possedent leurs etats et leurs clips auteur ; les phases d'action, de reaction
et de suspension interdisent a la locomotion de remplacer leur presentation.
Lucian conserve son controller. Les paliers QTE continuent d'utiliser leurs
overrides de clips sur Lucian et ne prennent pas possession de la camera.

## Rôle

Centraliser les actions locales, les transmettre au bon personnage et adapter
la façade gameplay Lit à Opsive UCC et à sa caméra.

## Recuperation root motion

`AnimationGroundRecovery`, dans `Assets/Scripts/Animation/`, est un garde-fou
generique conserve pour les acteurs qui en ont encore besoin. Il est retire
de Lucian et des prefabs joueurs migres : UCC y gere le support physique. Apres
le root motion, il sonde le support sous le Transform reellement anime et ne
corrige qu'une penetration vers le haut. Il laisse intacts la trajectoire
horizontale, les sauts et les animations root valides; ce systeme ne depend pas
du combat.

## Gameplay joueur InPlace

Pendant Jump_End, Landing et Landing_Hard, le pilote UCC bloque le deplacement
horizontal et la rotation du joueur au contact du sol, y compris pendant le
fondu de sortie. Le verrou se libere a la sortie de l'animation, a la
desactivation et en cinematique ; les inputs maintenus sont alors acceptes.
La gravite et les collisions restent actives. L'anticipation visuelle du
landing en l'air ne bloque pas la trajectoire du saut.

`Player_Model` utilise des clips gameplay InPlace, y compris ses sous-machines
et Blend Trees. Les prefabs Lucian, Link, Luna et Mia partagent le contrat
InPlace du bridge. Le saut de Lucian conserve son script, ses clips et ses
reglages, dont la hauteur cible 100 ; la migration historique du saut ne doit
pas etre relancee. Les reglages des huit esquives sont egalement preserves.

Les nouvelles trajectoires d'action passent par une ability UCC concurrente,
dans `UpdateDesiredMovement`, puis par les collisions et la gravite existantes.
Le pilote libere son verrou a la fin, sur interruption ou desactivation et
avant une cinematique. Il ne compense jamais une distance bloquee par un mur.
Le temps du mouvement utilise la cadence UCC et la vitesse Animator, sans
multiplier une seconde fois le temps local du combat. La politique de Root
n'est plus deduite des tags ou des phases d'animation ; ces phases restent
utiles a la presentation. Les seuls deltas Root joueur autorises appartiennent
au relay d'une session cinematique explicite.

## Classes principales

- `PlayerInputs.inputactions` : source des bindings.
- `LocalPlayerInput` : capture Input System et singleton local.
- `LocalInputRouter` : événements, valeurs continues, debounce et consommation.
- `InputFocusStack` : priorité UI/cinématique sur le gameplay.
- `LitOpsiveLocomotionBridge` : mouvement, saut, vol, franchissement d'obstacles
  bas et verrous UCC.
- `LitUccCameraCharacterBinder` : liaison de la caméra au `LocalPlayerContext`.
- `NetworkCharacterInput` : relais des commandes du propriétaire vers le serveur.

## Flux principaux

Les QTE de palier combat utilisent l'`InputMode.ThresholdSequence` hors
fenêtre puis l'`InputMode.CombatQTE` pendant la fenêtre ouverte par
l'événement d'animation `QTE(...)`. Ces deux modes gardent la map `Camera`
active : les paliers sont InPlace et ne possèdent aucune caméra cinématique;
seuls mouvement et actions restent verrouillés. Les QTE de palier ne reposent
ni sur une Timeline ni sur des Signals auteur.
Une `ThresholdSequence` contient exclusivement des `steps` complets. Chaque
step impose un unique event `QTE(Y/B/A/X)` dans son clip de presentation et
porte ses propres duree, ralenti, watchdog, fondus, succes et echec. Le succes
joue le clip du step, attend sa fin en temps gameplay, puis injecte et lance le
clip QTE suivant. Les succes intermediaires restent visuels; seul le dernier
step resout le palier. Il n'existe aucun chemin legacy a clip multi-events.
La pose face-a-face est authorisee dans `CharacterData` pour chaque ennemi;
`CombatHealthThresholdController` ne porte aucun tuning dans son Inspector.
La pose des QTE de palier ne dépend pas d'un unique collider enfant : elle
résout la capsule Unity ou le `CharacterController` UCC, puis possède un volume
de secours pour les frames d'initialisation différée.

`TimeManager` arbitre toutes les ecritures de temps global et compose les
demandes par le scale le plus restrictif. Un QTE de palier obtient son handle
uniquement pendant la fenetre `CombatQTE`; ses input, UI et watchdogs utilisent
le temps reel et restent donc repondants a `0,4`. Les domaines acteur et le
pitch audio consomment le scale publie par cette autorite. La presentation de
ralenti est egalement un observateur exclusif de cette autorite :
`TimeSlowMotionPresentationController` ne peut ni demander ni modifier le
temps. Il joue une onde ecran et le SFX d'entree une fois lors de l'entree en
ralenti, maintient une vignette et une aberration chromatique HDRP en temps
reel, puis joue le SFX de sortie une fois au retour a la vitesse normale. Une
pause globale (`scale = 0`) ne declenche pas ces retours de ralenti. Son onde
est une deformation tres ample a basse frequence sans surbrillance; les impacts
restent des ondes locales, rapides et de haute frequence, authorisees dans les
profils des skills. `cameraEffectsEnabled` peut desactiver temporairement la
camera du ralenti tout en conservant ses SFX; il est actuellement desactive.

Le retour au menu et le quit emploient `NetcodeBootstrap.ShutdownActiveNetworkManager`
comme point de sortie réseau unique. Le package NGO embarqué protège ensuite le
second `OnDestroy` Unity : il ne réutilise pas un SpawnManager ou un
NetworkSceneManager déjà détruit. `NetworkTimeSystem` conserve son propre état
d'abonnement au tick puis est remis à `null` par `NetworkManager`; son arrêt ne
dépend donc jamais d'un client déjà réinitialisé.

## Contrat exploration UCC

En exploration, UCC est l'unique propriétaire de la position, rotation
physique, gravité, saut, collision et pose de la caméra. Le bridge Lit fournit
les entrées et la présentation, mais ne crée aucun saut de secours et ne
modifie pas la force de `Jump`. `LucianJumpPresentationController` applique
uniquement un profil de gravité UCC borné : normale au départ, réduite avant
et pendant la fenêtre d'apex, puis légèrement réduite pendant la descente; elle
est restaurée dès le contact sol. Le seuil physique est indépendant du seuil
d'animation afin que la chute ne soit pas jouée trop tôt. Il ne freine pas le
moteur à l'atterrissage. `Jump_Loop` couvre la montée et l'apex; l'état
`Falling` ne commence qu'après une vitesse verticale négative et emploie le
clip `Falling_Loop` archivé sans activer ses anciens scripts de chute libre.
La transition `Jump_Loop` vers `Falling` fond les poses sur 0,18 s ; les
transitions d'atterrissage restent courtes afin de conserver leur impact.
`LitOpsiveLookSource` est le repère enfant de direction requis par le profil
exploration forward-only ; il peut mettre à jour ce repère, mais ne pilote jamais
la `Main Camera` ni le root physique du personnage.

La vue Adventure UCC retourne directement la pose résolue par UCC, incluant
son suivi et ses collisions : aucun `SmoothDamp`, rattrapage de vitesse ou snap
de collision Lit ne s'ajoute en exploration. `LitSmoothUccCameraViewAdapter`
conserve le branchement de vue, les diagnostics et les demandes de pose
immédiate. Les snaps sont limités au bind initial, changement de personnage,
téléportation, recentrage explicite et retour autorisé d'une caméra externe.
Un bind déjà valide ne recentre plus la caméra. La vue de lock de combat garde
son propre lissage, indépendant et hors de ce contrat exploration.
La liberté de pivot horizontal native d'Adventure est à zéro : la caméra reste
sur son épaule configurée au lieu de franchir un seuil latéral dont le signe
peut alterner autour du personnage et créer une vibration directionnelle.

Les contours Runtime des fantômes ne sont pas pilotés par la caméra : leur gate
de visibilité utilise la même portée de détection que l'interaction, afin que le
retour visuel ne disparaisse pas avant que le joueur puisse interagir.

Les feedbacks courts du monde (ramassage, dépôt et erreur d'interaction) sont
des toasts sans focus. Les textes de stèle utilisent la présentation du
DialoguePanel mais restent eux aussi dans le mode `Exploration`; seul un vrai
dialogue ou un panneau de décision peut suspendre la locomotion. Ces retours
automatiques durent deux secondes et ne demandent jamais `Interact` pour se fermer.
Un `InteractableItem` bloque par
`requireLitInfluenceForInteraction` reste un objet de monde lisible : il
enregistre un signal aupres du `VFXManager` global tant qu'aucune Flame allumee
ne l'influence. Le manager maintient une unique instance de VFX boucle a
l'ancre de l'objet, sans la cadencer. L'entree dans une zone de Flame fond
toutes les emissions a zero; la sortie restaure progressivement leurs valeurs
initiales. Le root du VFX est echelle selon la taille du monde de l'objet. Ce
feedback n'ouvre aucune UI et ne modifie aucun InputMode. Les containers, portes et recompenses Munin n'enregistrent jamais ce signal; parmi les documents fixes, seul un `Stab` est eligible.

Le combat ennemi autonome ne modifie pas la camera ni l'InputMode. Il conserve
NavMesh pour les deplacements ordinaires et le `CombatEnemyPhysicsMotor` pour
le root motion d'action; les impacts sont des hitboxes monde declenchees par
Animation Events, jamais une ecriture directe de pose ou de camera.
Après chacun de ces messages, `LocalPlayerInput` relit au frame suivant l'axe
de déplacement réellement maintenu : un ancien reset d'ActionMap ne peut plus
laisser le mouvement à zéro jusqu'à un saut ou une nouvelle pression.

Le panneau de décision des fantômes est une UI authorisée de `UI_Overlay`, pas
un canvas de secours créé par un contrôleur de monde. `GhostController` le
résout même lorsqu'il est inactif, le renseigne, puis le masque sans le détruire
à la fermeture. Son `CanvasGroup` porte l'alpha, les raycasts et
l'interactivité : le layout reste donc stable entre deux ouvertures.

Dans l'Inspector du bridge, **Locomotion Diagnostics** active en même temps le
relevé locomotion et le buffer de caméra. Les logs ne sont construits qu'au
dump; ils contiennent input, vitesse, sol/air, gravité, abilities, root motion,
vue caméra et snaps. Le menu **Lit/UCC/Create Exploration Test Course** génère
`Assets/Scenes/Tests/UccExplorationCourse.unity`, un parcours additif pour les
tests de course, demi-tour, marche, pente, saut et couloir caméra.

`PlayerInputs` → `LocalPlayerInput` → `LocalInputRouter`.

Le flux se divise ensuite :

- solo vers `SquadManager` / `SquadCharacterController`;
- réseau vers `NetworkCharacterInput`;
- caméra vers les abonnés dédiés.
- combat vers `RealTimeCombatInput` et ses roues de skills/contres.

`SquadCharacterController` convertit l’input en espace monde, puis
`LitOpsiveLocomotionBridge` pilote UCC.

Sous lock, `SquadCharacterController` transmet d'abord l'axe brut au bridge.
`TryResolveCombatLockMove` le convertit en approche/recul/orbite relatif a
`EnemyLockPoint`; il ne passe donc pas par le calcul camera-relative libre.
Le bridge conserve la cible et le face-a-face. Les
clips InPlace de `CombatLocomotion` affichent les huit directions sans fournir
de translation ni de yaw : UCC applique le mouvement physique et le lock reste
l'unique autorite de rotation. `CombatIdle` est sélectionné dès que l'input est
neutre; `CombatLocomotion` revient dès qu'un input est maintenu. Une roulade ou
un saut demande explicitement une orientation temporaire avant de revenir vers
la cible.
Un input strictement gauche ou droite produit une tangente autour de la cible,
stabilisee par une faible correction radiale UCC. Cette correction ne concerne
ni les diagonales, ni les actions, ni les evasions.

Dans Iluvilirae_Test seulement, LucianCombatTorsoFacing complete la presentation
sous lock : l'axe visible est mesure entre LeftUpperArm et RightUpperArm puis
aligne sur le forward physique via Spine/Chest (40/60 %, 65 degres maximum).
La correction ne tourne ni root, ni bassin, ni jambes et ne fournit aucun
deplacement. Entree sur 80 ms du CombatTimeDomain ; aucune ecriture Animator
de parametre, de vitesse ou d'etat. Seuls CombatIdle et CombatLocomotion sont
eligibles, sans action proprietaire ni transition vers une action. Sprint
libre, evasions, exploration et session cinematique sont exclus. OnDisable et
pause ne laissent pas de correction accumulee ; une pose deja reevaluee par
l'Animator n'est jamais remplacee par une ancienne pose memorisee.
Le laboratoire l'installe a chaque rencontre ; alignPlayerCombatTorso permet
la comparaison avant/apres. Aucun ajout aux prefabs de production.

La caméra gameplay doit passer par le `CameraController` Opsive UCC, avec
`LitUccCameraCharacterBinder` sur la caméra active pour suivre
`LocalPlayerContext`. Les anciens pivots `CameraAnchor` / `YawPivot` /
`PitchPivot` du système legacy ne doivent pas piloter la `Main Camera`.

Le combat tour par tour, son ActionMap `Combat`, sa caméra de phase et le
`CombatDefensePanel` ont été retirés. Le combat temps réel reste continu dans
le monde : `RealTimeCombat` est l'unique ActionMap de combat et
`CombatLockOnCameraController` est le pilote de lock; les Timelines de
LightSkill/CounterSkill reçoivent temporairement la Main Camera puis rendent
explicitement l'autorité à UCC.

`RealTimeCombatInput` s'abonne a la map partagee `PlayerInputs/RealTimeCombat`
pour les reactions, la palette et les attaques. Le verrouillage passe
par `Player/LeftShoulder`; les maps `Player` et `Camera` existantes restent actives pour préserver le déplacement
libre. La caméra de lock désactive uniquement les drivers renseignés dans
`CombatLockOnCameraController`, puis restaure leur état exact au déverrouillage.

Dans `GameplaySessionRoot`, ces drivers sont references explicitement :
`CameraController`, `CameraControllerHandler` et `LitUccCameraCharacterBinder`.
Pendant le lock, le controleur combat les maintient desactives a chaque frame,
ce qui empeche UCC de reprendre la transform de la `Main Camera` pendant une
attaque root ou un rebind de personnage.

Comme le lock pilote directement la `Main Camera`, son composant conserve un
solveur d'obstacles visuels par `SphereCastNonAlloc` : il rapproche la camera
avant un decor bloquant tout en ignorant Lucian et la cible verrouillee. Le
masque, rayon, marge et distance minimale sont reglabes dans l'Inspector.
Il calcule aussi les bounds des renderers de Lucian afin de garder le personnage
entier dans une zone viewport configurable : le FOV augmente jusqu'a
`maximumLockedFieldOfView`, puis la camera recule si cette limite ne suffit pas.
Le cadrage normal reste configure sur la vue UCC `Adventure`; celui de lock est
independant et expose par `CombatLockOnCameraController` (`Lock Camera Offset`,
`Lock Field Of View`). Les valeurs actuelles rapprochent l'exploration (`z =
-1.9`) et reculent le combat (`z = -6.5`, FOV 66), sans desactiver le solveur
d'obstacles UCC.
La direction d'orbite joueur-vers-ennemi possede son propre lissage
`orbitDirectionSharpness`, distinct du lissage de position/rotation, et une
vitesse angulaire maximale `maximumOrbitDegreesPerSecond` pour absorber les
demi-tours brusques pendant un lock sans retournement instantane.
Le `CombatLockAdventureViewType` lisse aussi cet axe avant de calculer son
orbite UCC et limite la rotation du lock. Ainsi, le root motion et les
changements de direction pendant une BasicSkill ne forcent plus un rattrapage
immediat de la camera. Ces reglages sont exposes par
`CombatLockOnCameraController` dans `GameplaySessionRoot`.
Le point de focus joueur/ennemi est lui aussi lisse et borne par
`focusPointSharpness` et `maximumFocusPointMetersPerSecond`, afin qu'un hit ou
un deplacement ponctuel du point de lock ne secoue pas brutalement la camera.
Les impacts de skills passent aussi par `CombatLockOnCameraController` : leur
offset et leur FOV sont attenues, bornes et remplacent progressivement
l'impulsion precedente. Les impacts successifs ne peuvent donc pas faire
deriver la camera pendant un combo; ces limites restent reglables dans
`GameplaySessionRoot`.
Le meme controleur peut ajouter un micro-tremblement lateral/vertical a chaque
impact. Il utilise le temps non scale, s'efface sur quelques centiemes de
seconde et reste borne par les reglages de `GameplaySessionRoot`.
Un `LightSkillSO` cinematographique peut aussi demander une suspension locale
temporaire de ce pilote : sa Timeline recoit alors la Main Camera, puis UCC
reprend le meme view de lock lorsque le `PlayableDirector` se termine.

Le lock du combat temps reel commence une seule fois quand un ennemi entre dans
le `VisionField` de Lucian. Il se termine automatiquement hors de la distance de
desengagement; `Player/LeftShoulder` peut sinon le deverrouiller ou reverrouiller
manuellement a portee. `SwitchEnemyLock`, lie a `Gamepad/dpad/left`, change de
cible hors palette. Si aucun lock manuel n'est possible, `LeftShoulder` est
ignore : il n'ouvre pas le panneau d'escouade et ne prend aucun focus.
`RealTimeCombatInput` gere reactions, palette et changement de cible. Les maps
`Player` et `Camera` restent actives pour le deplacement libre. Pendant un lock
actif, `WestButton` est reserve a
`BasicAttack` : l'action `Player/ToggleTorch` l'ignore.

La source des actions runtime est `PlayerInputs.inputactions` : sa
map `RealTimeCombat` reprend les actions du prototype avec le layout gamepad
final (South garde/contre, East esquive, West attaque, North saut, LT roue, RT skill de
lumiere et D-pad bas changement de cible). `RealTimeCombatInput` place un
contexte `Combat` exclusif pendant son activation; `LocalPlayerInput` laisse
passer mouvement, camera et lock, mais ne transmet plus les actions monde
concurrentes (interaction, retour, inventaire, torche, Munin, loot ou multi).
`GamepadInputContextStack` est la couche de migration des futurs contextes UI,
placement et cinematique. Elle est purgee a chaque reset de session.
`LocalPlayerInput` reapplique toujours le profil de base `Combat` a chaque
verrouillage valide, meme si un reset UI/scene a deja reconfigure les maps du
singleton persistant. Cela empeche `Player` de rester active sans
`RealTimeCombat` apres un lock.
`InputModeCoordinator` remet les valeurs locales a zero lors d'un changement
de map pour eviter un mouvement residuel, puis `LocalPlayerInput` relit les
actions `Player/Move` et `Player/RightShoulder` si le mode redevenu actif est
`Exploration` ou `Combat`. La reinjection attend la fin effective du focus et
des verrous UCC et reutilise `SquadManager` : un stick maintenu pendant une UI,
une roulade ou une LightSkill repart sans nouvelle pression.
`LitOpsiveLocomotionBridge` arme alors une reponse de premier pas optionnelle,
une faible impulsion UCC horizontale plafonnee et soumise aux collisions. Elle
ne peut pas se jouer durant une cinematique, un verrou externe, un vol, une
chute ou sans deplacement; son amplitude et son cooldown sont exposes sur le
bridge.
`GameplayRuntimeReset` applique egalement ce retour a `Exploration`: focus,
contexte gamepad, suppressions locales et map combat sont purges ensemble avant
le prochain test ou chargement de session.

La map `RealTimeCombat` lie `Counter` a `Gamepad/buttonNorth` (`Space`),
`Jump` a `Gamepad/buttonSouth` et `Dodge` a `Gamepad/buttonEast` (`LeftAlt`).
`NorthButton` maintient une garde et declenche immediatement le
`defaultCounterSkill` lors d'une nouvelle pression dans une fenetre parfaite qui
accepte `Counter`. `SouthButton` reste `Jump`.
Les directions d'esquive viennent du vecteur monde deja calcule
par UCC; sans stick, l'esquive part vers l'arriere. Pendant un lock, la locomotion libre conserve
l'orientation de Lucian vers son mouvement; seules les actions engagees le
reorientent vers `EnemyLockPoint`.

`CombatWarningPresentationController`, sur le `BattleManager` existant, est une
alerte locale pilotee par `CombatWarningOn`/`CombatWarningOff` depuis les clips
ennemis. Son Custom Pass HDRP est configure dans `GameplaySessionRoot` sous
`CombatWarningPass`; il dessine une alerte peripherique directionnelle et ne
cree aucun volume au runtime. Pendant l'alerte, le lock UCC conserve son
evitement d'obstacles mais recentre temporairement le regard vers
`EnemyLockPoint`, puis revient progressivement au cadrage de lock normal.

Pour une attaque ennemie temps reel, les icones South/East/North ne proviennent
plus de `ShowInput` pendant les clips de combat. Le prompt world-space unique
lit les reactions acceptees par le `SkillSO`, les montre attenuees pendant la
menace, puis nettes pendant la fenetre ouverte. South commence une garde a tout
moment; seule une nouvelle pression dans une fenetre qui accepte `Counter` lance
la Timeline du contre configure. East conserve l'esquive et North le saut, qui restent
enregistres comme reactions uniquement lorsque la fenetre Animation Event est
ouverte.

Les actions de combat font face a l'ennemi verrouille. Pour `Dodge`, un stick
gauche non nul a priorite : Lucian s'oriente vers la direction voulue et roule
dans celle-ci. Sans direction, il reste face a la cible et roule vers l'arriere.

Pendant un lock, `RealTimeCombatManager` active
`LitOpsiveLocomotionBridge.SetCombatStrafeMode(true, EnemyLockPoint)`. Le bridge
quitte alors seulement le profil visuel exploration forward-only, conserve Lucian
face a `EnemyLockPoint` en fin d'image sans reinjecter d'input, et transmet les
directions locales au blend tree combat InPlace; la position reste sous autorite
UCC. Ces clips sont maintenant InPlace : l'Animator ne fournit plus de
translation ni de yaw sous lock. Le bridge synchronise la rotation du moteur
UCC avec le `LookSource` de combat avant que les forces soient calculees : aucun
strafe ne peut etre reinterprete dans un repere de personnage obsolete. Au deverrouillage, le profil exploration est restaure sans
modifier ses reglages serialises.
Un strafe gauche/droite strict memorise son rayon autour de la cible et applique
une correction radiale limitee via l'intention UCC. Cette correction est effacee
des qu'une diagonale, une action, une evasion, une cinematique ou une sortie de
lock commence; les collisions UCC restent prioritaires sur le cercle parfait.

`LocalPlayerInput` est persistant en Play Mode. Pendant un dechargement de
scene dans l'editeur, son `InputActionAsset` doit etre detruit immediatement :
une destruction differee laisse Unity signaler un objet `LocalPlayerInput`
non nettoye.

Le franchissement automatique d'obstacles reste dans `LitOpsiveLocomotionBridge` :
les obstacles sous le seuil d'ignorance ne déclenchent rien, les obstacles bas
franchissables lancent un court traversal scripté avec trigger Animator
configurable, et les obstacles plus hauts restent bloquants. Le trigger par
défaut est `ObstacleTraversal` et cible la state `Obstacle_Traversal` du
controller `Player_Model` ; le clip peut être assigné ensuite sur cette state.
Le seuil d'ignore effectif est borné par la hauteur de marche UCC
(`CharacterLocomotion.MaxStepHeight`) afin d'éviter l'angle mort où une petite
marche serait trop haute pour le step UCC mais trop basse pour lancer le
traversal.
Quand le bridge UCC pilote le personnage, il relève aussi les réglages de
tolérance aux reliefs du sol (`MaxStepHeight`, `SlopeLimit`,
`StickToGroundDistance`) pour absorber les seuils et MeshColliders de sol
irréguliers sans patcher chaque tuile de scène.
La détection ignore aussi les surfaces progressives dont la normale pointe trop
vers le haut (`obstacleTraversalMaxSurfaceUpDot`) afin qu'une pente, une colline
ou un amas de terre ne déclenche pas l'animation de franchissement. Les murets,
barrières et branches restent détectés via leurs faces abruptes.
La surface supérieure validant la hauteur doit appartenir au même collider que
la face frontale détectée, et les probes utilisent le masque de collision UCC
effectif, afin qu'un mur, un volume de couloir ou un trigger de scène ne soit
pas validé par le sol situé derrière lui.
Les portes interactives restent des colliders bloquants pour les contrôles de
dégagement, mais ne sont pas des candidats de franchissement automatique : le
joueur doit les ouvrir via l'interaction plutôt que déclencher `ObstacleTraversal`.

Les échelles sont pilotées par `LadderController` comme traversal scripté :
le trajet monte ou descend selon la position du personnage projetée sur l'axe
réel de l'échelle. Les poses d'entrée, de boucle et de sortie sont calculées
depuis l'axe de l'échelle et le côté d'approche/sortie, pas depuis les rotations
potentiellement arbitraires des transforms d'ancrage.
La sortie haute d'une montée est traitée comme un passage de l'autre côté de
l'échelle : les offsets de sortie haute placés côté approche sont donc miroités
sur le plan perpendiculaire à l'axe réel de l'échelle.

## Pièges observés

Une notification de connaissance ne doit jamais forcer une state Animator sur
le personnage. `KnowledgeManager` laisse donc desactivee l'animation legacy
`Knowledge_Unlock`: UCC et `LitOpsiveLocomotionBridge` restent seuls ecrivains
de la locomotion. Les items et stabs utilisent la meme voie de connaissance;
un CrossFade ici peut bloquer la marche jusqu'a ce qu'une ability UCC, telle que
le saut, remplace l'etat anime.

Le choix de reaction d'un fantome bloque volontairement l'input de gameplay
uniquement tant que `GhostReactionChoicePanel` est visible et detient le focus.
Son `CanvasGroup` porte a la fois l'alpha, l'interaction et les raycasts; la
fermeture les remet tous a zero avant de rendre le focus a l'exploration.
Le curseur visuel des choix suit egalement le `OnSelect` EventSystem, afin que
le choix manette et le survol souris presentent le meme etat sans modifier les
ActionMaps.
`GhostController` consomme `UI/Navigate` comme `IInputModeHandler`: le focus UI
ne laisse donc pas la navigation stick/croix se perdre dans le coordinateur.
La navigation reste disponible pendant le court rearmement de validation apres
l'ouverture; seul `Submit` est bloque. Cela evite que le choix haut soit saute
alors que seul `Reculer` etait provisoirement interactif.

## Orchestration des ActionMaps

`InputModeCoordinator` est l'unique proprietaire runtime des ActionMaps de
`PlayerInputs`. Les modes actifs sont exclusifs et restaures par pile :
exploration (`Player` + `Camera`), dialogue (`Dialogue` + `Camera`), UI,
placement (`Placement` + `Camera`), combat (`Player` + `Camera` +
`RealTimeCombat`), roue de combat et cinematique.
`InputFocusStack` continue de servir au focus existant, mais pousse maintenant
le mode `UI`; un dialogue doit employer `PushDialogue` et une pose
`PushPlacement`. Ne plus activer ou desactiver directement une ActionMap hors du
coordinateur. L'audit `Lit/Input/Audit ActionMap Profiles` verifie les maps et
les bindings concurrents des profils qui partagent la camera.

Les piles de focus et de contexte purgent automatiquement leurs owners Unity
detruits. Les suppressions temporaires `Interact`/`Jump` sont purgées de la meme
facon et remises a zero au demarrage d'une nouvelle session. Un interactable ne
doit retourner `true` de `TryHandleLocalInteract` que s'il a effectivement
execute son interaction : lorsqu'un verrou de gameplay est deja actif, il doit
retourner `false` et ne jamais absorber le bouton. Enfin, un combat actif dont
la cible verrouillee est detruite, inactive ou morte se termine
automatiquement hors cinematique.

- Modifier l’asset `.inputactions`, jamais le wrapper C# généré.
- Un input consommable (`Interact`, `TriggerMunin`) ne doit être traité qu’une fois.
- `InputFocusStack` est une pile : seul le propriétaire au sommet détient le focus.
- Le debounce utilise `Time.unscaledTime`; son état statique doit être remis à zéro
  à chaque session Play.
- Les cinématiques utilisent des verrous externes UCC; toujours restaurer le
  contrôle lors d’un abort ou `OnDisable`.

# Echelles de temps et restitution UCC

`TimeManager` est la seule autorite du temps global. Un `CombatTimeDomain` sur l'ActorRoot ralentit localement l'intention UCC, les impulsions, le facing et l'Animator. Les QTE de palier demandent actuellement un ralentissement global via un handle `TimeManager`; les overlays et les inputs QTE utilisent le temps reel non scale et ne doivent jamais dependre de `Time.deltaTime`.

# Combat ennemi et navigation

Le deplacement ennemi ordinaire appartient exclusivement au `NavMeshAgent`, pilote par `CombatEnemyLocomotionController`; aucune IA ne doit faire de fallback par `Transform`. `EnemyNavigationController` attend que `SquadAIManager` ait bake le monde actif, demande un rebuild controle si aucune projection locale n'est disponible et n'autorise un Warp que lorsque la projection est pratiquement confondue avec l'acteur. Le serveur/hote est le seul a appeler le cerveau et cette preparation; les clients ne doivent pas choisir de cible, destination ou impact.

`EnemyCombatBrain` choisit les attaques et maintient une cible/menace, tandis que `CombatEnemyLocomotionController` gere translation NavMesh et presentation Animator. Le suivi de rotation reste ouvert pendant le wind-up mais est bloque par l'Animation Event `LockEnemyAttackDirection` avant la phase engagee : un joueur qui esquive ne peut donc pas etre suivi magiquement par la rotation de fin d'attaque. `EndEnemyAttack` reste l'autorite normale de fin; `EnemyAttackRecoverySafety` est seulement le filet de securite qui libere aussi le cerveau si un clip est mal auteurise.

## Laboratoire Brains AI (2026-10-08)

Iluvilirae_Test utilise PlayerInputs, RealTimeCombatInput et la camera tactique
UCC de GameplaySessionRoot. L3 sur gamepad appartient a LightSkill en combat :
LocalPlayerInput ne doit pas ouvrir TacticalInspection et desactiver la carte
combat sur cette meme pression. L'inspection clavier/souris reste disponible.
ApplyWorldMoveInput alimente la direction locale et les parametres Animator
pendant un verrouillage, meme sans camera tactique resolue ; seul le reset du
rayon d'orbite depend de cette camera. Une demande de reconciliation des inputs
maintenus ne cree plus de nouvel input host pendant le nettoyage d'une scene.
## Cadence de presentation mesuree (2026-10-10)

LocomotionPresentationProfile est optionnel et cible l'avatar reel. Dans le
chemin calibre, le bridge calcule l'activite et la cadence depuis la vitesse
planaire UCC obtenue, ramenee au temps acteur. Le stick indique l'intention
et l'allure, sans maximum stick/vitesse ni facteur fixe 0,22. La deceleration
physique continue de se presenter apres relachement de l'input. Le delta du
domaine acteur est utilise une seule fois ; pause conserve la presentation.

LocomotionPlaybackRate ne multiplie que Locomotion et CombatLocomotion.
Animator.speed reste au domaine temporel ; attaques, saut, esquives et Timeline
ne sont pas retimes. Les vitesses/accelerations UCC ne sont pas recalibrees.
LitPresentationAnimatorMonitor preserve les reglages du monitor UCC et
cede seulement ses ecritures HorizontalMovement/ForwardMovement au bridge
pendant la presentation calibree au sol ; vol/cinematique gardent leur chemin.
Sans ce monitor specialise, le profil ne s'active pas, pour eviter les doubles
ecritures de direction observees. La locomotion ne reprend pas un etat d'action.

Ces donnees et bindings sont pour l'instant uniquement dans le laboratoire
isole. Les courbes Inplace existantes de Lucian, torche, saut et esquives
ne sont pas reparees sans defaut mesure. Alignement directionnel par correlation
de phase : un arbre incomplet reste entierement intact. Les courses passent
le controle de correspondance ; les marches requierent des contacts auteur.
La correlation n'est pas une validation des 3 cm d'appui. Aucune nouvelle
correction IK des pieds n'a ete ajoutee.

## Compatibilite camera tactique (2026-10-10)

Presentation jambes (2026-10-13) : le bridge installe PlayerMovingLegsOverlay
sur l'acteur au demarrage. Moving Legs est une couche Override masquee uniquement
aux jambes, pilotee par la vitesse planaire UCC (pas par le stick seul).
Les parametres MovingLegs* sont independants des parametres de l'action du torse.
CombatStrafeActive selectionne la locomotion directionnelle existante.
Fondu court a l'arret ; poids nul hors sol, pendant un verrou UCC/cinematique
ou apres la mort. Aucun root motion, input ni velocite n'est ajoute par cette
presentation. Les controllers historiques non equipes restent inchanges.

Sous camera tactique, le stick reste camera-relative meme sous lock ; le
contrat target-relative concerne le mode third-person. Le MovementType UCC
consomme le facing tactique, sans corrections immediates SetRotation a chaque
frame. FreeSprint transmet le regard de mouvement, hors evasion directionnelle.
# Maintien X et ascension chargee (2026-10-14)

RealTimeCombatInput mesure le maintien avec Time.unscaledTime. La boucle de
BasicSkills maintenue est retiree ; les pressions discretes conservent le combo.
La preparation passe par une session externe de PlayerActionPresentationController
et un handle UCC libere sur sortie, sans changer de map.
Sous maintien X, la fin de Basic transfere directement vers cette session,
sans restitution intermediaire a la locomotion. ChargedWindup est un clip
statique sans events qui conserve la pose finale de BasicSkill_1 ; aucun
Animator.speed = 0 ni verrou supplementaire a restaurer.
Cette session est protegee : les degats non mortels ne retirent plus le maintien
X ni la pose, et les annulations de mobilite/garde/action ordinaire sont fermees.
Les roues ne s'ouvrent pas pendant le chargement. La Basic initiale est egalement
protegee si son maintien de charge est valide, ainsi que le passage vers la
preparation ; une Basic sans charge reste interruptible. Relacher X reste
une annulation volontaire. Mort,
cinematique prioritaire et sorties techniques nettoient toujours leurs handles.
IsChargingSkill utilise l'input proprietaire, son maintien valide et les generations
de Basic/preparation. Les recepteurs de degats ne relisent pas les controles physiques
hors phase d'input. La lecture physique reste dans le polling/callback de relachement.
Recette renforcee passee : ChargedHoldArmorVerified.log dans la copie isolee.
La preparation demande au LitCameraDirector un cadrage de charge : FOV reduit
et petit decalage derriere l'acteur, configures dans ChargedSkillSO. Composition
au rendu, puis restauration de la pose source, sans reinjection dans UCC.
Le handle camera est libere au relachement, lancement ou interruption ; les
cinematiques restent prioritaires et la restitution utilise le cadrage courant.
LitUccActionTakeoffAbility accepte une duree optionnelle (.08 s par defaut pour
les impulsions existantes) ; Ascension utilise .55 s et la collision UCC normale.
Un maintien aerien distinct survit a la restitution apres la montee, mais pas a
une action non basique, une cinematique ou une desactivation. Les BasicSkills
aeriennes conservent leurs propres handles de gravite bornes (clip +1 s local).
Chaque liberation cible uniquement son handle, idempotent. Les holds orphelins
expirent ou sont retires a la desactivation du proprietaire. Une traversee qui
suspend la gravite memorise sa valeur de base, pas la suspension d'un hold.

ChargedFloat est une presentation passive sans session ni verrou supplementaire.
Elle cede a tout Basic aerien ; la restitution normale peut reprendre le float
dans sa fenetre restante. Fin du hold : fondu vers Falling, seulement si la pose
passive possede encore l'Animator. Mort, cinematiques et autres actions restent
prioritaires. Les deux poses de flottement ne deplacent jamais les ActorRoots.

## Contre anime du laboratoire (2026-10-15)

RealTimeCombatInput transmet CallbackContext.time pour Y. La phase du clip
est lue sur l'etat entrant/courant de l'ennemi ; seule la frame non encore
evaluee peut etre projetee depuis la derniere lecture Animator. L'age reel
de l'evenement est converti avec les echelles globale et Animator, sans double
ralenti. La fenetre termine au contact, pas .2 s apres la telegraphie initiale.
La demande eligible est arbitree avant impact/clash, une seule fois pour sa
generation. Menu et changements de contexte ne transportent pas cette demande.
Pas de nouvelle map, de buffer de contre precoce, d'UI ou de controle camera.
La roulade conserve son timer reel ; les QTE de paliers restent independants.
## Attaques sans lock du laboratoire (2026-10-15)

Recette : 8 orientations de frappe a l'arret, 8 directions de stick avec sprint
et 8 attaques aeriennes ont termine leur session sans lock. Le test mesure la
vitesse UCC avant la frappe courue, verifie l'intention camera-relative capturee
et l'absence de verrou residuel. Les captures des trois modes ont ete inspectees,
sans les assimiler a un jugement manuel de fluidite ou de confort.
La reprise physique confirme restitution apres projection, Basic aerienne manuelle,
retour au sol et absence de verrou/collision ignoree residuels. Les dashs libres
s'arretent avant le mur et le vide de la fixture ; les distances mesurees figurent
dans current_work. Le clash cadence a 60 pas/s restitue egalement UCC apres recul.
La recette ne remplace ni le ressenti gamepad ni les essais reseau/multi-FPS.

Lors d'un premier contact accepte dans le laboratoire, SetInputActive demande
une transition continue au coordinateur. Seulement Exploration -> Combat sans
contexte prioritaire : les actions deja actives des maps conservees ne sont pas
desactivees et les valeurs Move/Camera ne sont pas remises a zero. Ainsi X tenu,
charge, buffers, sprint et direction restent valides, sans rejouer la pression.
Dialogue, menu, pause, roue et cinematique ne beneficient pas de cette exception.
L'abonnement aux callbacks reste idempotent. Le premier contact ne change pas
le lock, le facing, les annulations autorisees ou la restitution UCC des actions.

Opt-in FreeAttackProfile exclusivement dans Iluvilirae_Test.
Les BasicSkills ne dependent plus du lock ou de l'engagement dans ce mode.
Chaque session capture le stick dans le repere de la camera du laboratoire ;
aucun input utilise le repere d'un ennemi non verrouille. La rotation demandee
est bornee par le profil et le temps local, au plus une fois par frame.

En Exploration, RealTimeCombatInput active seulement BasicAttack et
SelectSkillKit. Le coordinateur conserve l'autorite des autres maps/contextes ;
menus, dialogues et traversees ne sont pas contournes. La roue peut executer
les skills libres sans fabriquer un etat de combat. Le lock, la camera et son
curseur ne changent jamais lors d'une acquisition temporaire.
Dans ce prototype, West est reserve a l'attaque libre : le callback de torche
manette partageant ce bouton est ignore dans ce contexte uniquement. La
commande clavier de torche et les bindings de production sont conserves.
Les dashs sans cible gardent UCC, gravite, capsule, controles de sol et collisions.
Toute interruption invalide le contexte avec sa generation et libere seulement
les mouvements et bypass appartenant a cette session.

SkillSO.targetingMode remplace les associations du profil de scene. La resolution
commune capture direction/cible/allie/zone au demarrage effectif ; un buffer ne
conserve pas une ancienne intention. Les skills a ennemi obligatoire peuvent
acquerir devant le joueur sans lock ; un lock invalide n'est jamais remplace
silencieusement. Les cinematics ordinaires recoivent cette victime explicitement.
Un rig sans CinemachineShot auteur ne prend pas la camera, meme si une piste
Cinemachine vide subsiste. Aucun changement des entrees CounterSkill/LightSkill.
# Integration combat production (2026-10-16)

CombatWorldRuntime suit LocalPlayerContext.LocalCharacterChanged pour nettoyer
les references de l'ancien personnage avant de relier le nouveau. En jeu normal,
SquadManager ou NetworkCharacterInput applique le mouvement ; le laboratoire
n'appelle plus Player.Move lui-meme. Le forwarding de secours ne concerne
qu'un acteur autonome sans pilote squad/reseau, sans nouveau moteur physique.
La roue solo conserve les kits debloques ; en multi une direction ouvre le kit
du personnage, puis une nouvelle pression directionnelle choisit son skill.
Le raccord d'engagement preserve les commandes et contextes en cours. Camera,
input modal et UI restent locaux au participant ; les decisions serveur ne
doivent jamais relier temporairement le manager local a un acteur distant.
La replication et la recette multijoueur complete restent des blocages de livraison.

## Continuation du raccord combat (2026-10-16)

La croix directionnelle garde son verrou de maintien au changement de map.
Un vrai relachement, une autre direction ou le retrait du peripherique sont
traites sans confondre canceled de desactivation et relachement physique.
Les tests utilisent les vrais callbacks Dynamic des deux maps en Edit Mode.

Les commandes reseau du nouveau runtime sont soumises a l'acteur et confirmees
par le serveur ; aucun cooldown de presentation n'est consomme sur simple envoi.
Une Basic externe peut conserver un PlayerAttackContext de sa propre generation
pour reutiliser la rotation fluide, sans relire le lock. Les observateurs suivent
la pose serveur. Les effets d'impact d'un autre joueur ne prennent pas la camera
locale. Charge reseau non migree : aucune preparation/camera speculative locale.
# Continuation reseau des actions (2026-10-17)

Le maintien X en reseau transmet pression/relachement au runtime de l'acteur.
Seul le serveur confirme preparation et lancement ; le cadrage de charge ne
s'applique qu'au proprietaire. La roulade reseau transmet une direction monde
et reutilise le moteur UCC/script de roulade existant. La garde peut reprendre
dans les fenetres autorisees de la presentation acteur.
Une restitution ou anomalie d'un acteur distant ne purge plus les buffers ni
ne reinjecte le mouvement du joueur local. Les maintiens aeriens sont restitues
sur sortie technique de monde ; un autre participant ne perd pas son flottement
parce que le manager local affiche une cinematique.
Cette passe ne constitue pas une validation host/client ou un test de ressenti.

