# Travail en cours

## Objectif actuel

Utiliser `AIAgent` comme espace documentaire leger pour preparer des prompts
Codex efficaces et economes en tokens.

## Etat actuel

`AIStudio_Legacy` a ete supprime. `AIAgent` contient un dossier `Codex/` avec
les regles de travail, l'etat courant et les fiches systeme utiles. Les anciennes
fiches systeme Markdown ont ete migrees depuis `AIStudio_Legacy/docs/systems/`.

Le nouveau flux ne depend plus d'une application Python, d'un environnement
virtuel ou d'un appel LLM local. Les prompts sont prepares manuellement depuis
`AIAgent/prompts/codex_task.md`.

Le prototype de chute libre est archive dans `Assets/FallingPhase_Legacy/` avec
sa scene, son Animator, son grappin, ses scripts et son manifest. Il ne fait plus
partie des scenes de build; l'ActionMap partagee `Falling` est conservee afin que
l'archive reste testable manuellement.

`CharacterData` est maintenant une definition d'auteur immuable : son unique
prefab est `WorldPrefab`. Les donnees de session (inventaire, flamme, trois
objets combat, usure des boucliers et charges Munin) vivent dans
`CharacterRuntimeState`, possede par `SquadManager` et indexe par personnage.
`CharacterStateStore` conserve le format de sauvegarde existant et hydrate ce
cache avant le spawn; les compagnons non instancies gardent donc leurs donnees.
`CharacterInfo` est le point d'accès commun au `CharacterData` des personnages
et ennemis de scène; l'ancien `EnemyInfo` a été retiré. `CombatHealth`
initialise son maximum depuis ce `CharacterData` avant de remplir une vie
initialement vide.
Les ennemis issus d'un `SceneMarker` restent enfants du marker tout en étant
positionnés en coordonnées monde; hors session Netcode, aucun `NetworkTransform`
n'est ajouté ou activé sur ce clone. En host/client, le clone est parenté par
`NetworkObject.TrySetParent` et son `NetworkTransform` bascule en espace local.
Un état persistant ennemi dont la position diverge de plus de 20 m du marker est
ignoré pour éviter de restaurer une position invalide d'une ancienne session.

Le chantier performance repart de zero. Les systemes de culling manuel,
budget de lumieres, budget de portails, XRay et instrumentation de migration
ont ete retires des scenes et prefabs. Les LOD et l'Occlusion Culling natif
Unity de `Maison` sont conserves. La prochaine architecture sera fondee sur
`Maison` comme hub permanent et sur les environnements `Castle` et `Arena`
charges progressivement en additif.

Le combat temps reel est actif dans `GameplaySessionRoot` et sur
`Juggernaut_Combat`. Il porte les definitions d'attaques, le loadout de huit
slots, le ledger de lumiere des ennemis, les fenetres de reaction pilotees par
Animation Events, le lock camera, les inputs dedies et les vues HUD/loadout.
Le verrouillage est entierement manuel : `LeftShoulder` verrouille l'ennemi le
plus proche a portee, puis le deverrouille au prochain appui. Les portees de
lock et de deverrouillage augmentent avec la plus grande composante de scale de
l'ennemi. Une step finale `LaunchCombat` de `StorySequenceAsset` peut lancer
le combat a la fin reelle de la sequence : lock de l'ennemi le plus grand puis
le plus proche, musique combat et degats de lumiere `openingCombatDamage` (50
par defaut). Un orbe lumineux pulse et un signal sonore sont joues sur
`EnemyLockPoint`; la croix directionnelle gauche bascule entre les ennemis
visibles verrouillables.
Chaque lock reapplique le profil d'ActionMaps `Combat` au singleton
`LocalPlayerInput`, ce qui garantit l'activation de `RealTimeCombat` apres un
reset UI ou scene. `RealTimeCombatInput` journalise egalement les refus de
BasicAttack afin d'identifier sans ambiguite une map inactive, une roue ouverte
ou un skill indisponible.
`GameplayRuntimeReset` purge aussi le contexte gamepad, les suppressions
locales et la map combat : une session de test ne peut plus heriter d'un
verrou d'input invisible de la precedente.
La restitution d'une UI, d'une action combat ou d'une LightSkill relit
desormais l'etat reel du stick et de `RightShoulder` apres le retour de map :
un input maintenu pendant `Cinematic` ne laisse plus Lucian immobile jusqu'a
une nouvelle pression. Cette reinjection repasse par `SquadManager` et le
bridge UCC normaux. Le bridge ajoute aussi une faible impulsion UCC plafonnee
au premier pas de course, y compris apres une action, pour rendre la reprise
plus nette sans contourner les collisions.
Quand aucun ennemi n'est verrouillable, `LeftShoulder` ne declenche aucune UI
ni focus gameplay : il reste reserve au lock/unlock et ne peut donc plus ouvrir
le panneau d'escouade ou interrompre le mouvement.
Le verrouillage combat affiche aussi un contour rouge HDRP autonome sur les
renderers de l'ennemi. Il utilise la layer `CombatOutline` et la passe
`CombatLockOutlinePass` de `GameplaySessionRoot`, sans partager l'etat ou la
couleur des contours bleus d'interactables.
Sous lock, le mouvement de Lucian est maintenant calcule une seule fois dans
`LitOpsiveLocomotionBridge`, dans le repere de `EnemyLockPoint` (ou du root
ennemi). Avant et arriere approchent ou eloignent; gauche et droite orbitent.
Un strafe lateral pur conserve son rayon initial par une faible intention
radiale UCC, sans ecriture directe de Transform. Lucian reste face a la cible;
seules roulade et saut peuvent prendre une orientation d'evasion temporaire.
Le blend tree combat de `Player_Model` contient un echantillon idle neutre afin
qu'un lock sans Move ne joue plus une animation de marche root.
Pendant ce lock, la camera conserve un evitement des obstacles visuels par
SphereCast, configurable sur `CombatLockOnCameraController`, sans laisser les
drivers UCC reprendre le cadrage de cible.
Hors lock, la vue UCC `Adventure` utilise un offset rapproche (`z = -1.9`);
le lock combat applique un offset distinct plus recule (`z = -6.5`) et un FOV
de 66, exposes sur `CombatLockOnCameraController` du `GameplaySessionRoot`.
La Main Camera et ses trois drivers UCC (`CameraController`, handler et binder)
sont references explicitement dans `GameplaySessionRoot`; le lock les maintient
desactives a chaque frame jusqu'au deverrouillage.
Le lock garantit aussi un cadrage complet de Lucian : il augmente son FOV jusqu'a
une limite reglable puis recule si necessaire. Son orbite suit la direction
joueur-ennemi avec un lissage distinct et une vitesse angulaire maximale afin
que les demi-tours et les deplacements root des BasicSkills restent fluides.
Les vitesses finales de position, rotation
et FOV sont aussi bornees pour absorber les changements de cadrage pres des
obstacles.
Exploration et combat restent dans le meme espace : aucune transition, vague,
BattleSphere, teleportation ou arene n'est activee.
Le combat tour par tour actif a ete retire : `RealTimeCombatSceneUiController`,
place sur `UI_Overlay`, pilote les panels existants de `Bootstrap` et `Arena` (`CombatEngagedPanel`,
`CombatScreenInfosPanel`, `VictoryPanel`, `DefeatPanel`) sans creation UI
runtime. `CombatDefensePanel`, l'ActionMap `Combat`, les transitions d'arene,
la camera de phase et les composants historiques ne font plus partie du
gameplay. `Assets/Legacy/BattleManager_SymphonieImport` reste une archive
intacte et hors compilation/gameplay actif.
Le premier scenario jouable est configure : Lucian dispose de `Lueur faible`
et `Lueur intense` dans les slots 1 et 2, tandis que le Juggernaut joue
`Skill_Juggernaut_Assomoir` via `EnemySkills` et ouvre sa fenetre d'esquive par
Animation Events. Son impact melee est configure entre 0 et 5 metres.
Le prototype temps reel distingue maintenant garde, esquive et contre. `NorthButton`
maintenu active une garde qui reduit les degats recus de 60 % par defaut; une
nouvelle pression dans une fenetre Animation Event acceptant `Counter` fige le
temps, suspend l'attaque ennemie et lance le `defaultCounterSkill` configure.
Un contre joue une Timeline non scale liee dynamiquement a Lucian,
l'ennemi verrouille et une Virtual Camera, puis son Animation Event applique les
degats propres au `CounterSkillSO`. `EastButton` est l'unique esquive root avec
i-frames; le dash combat precedent a ete retire de ce mapping.
Le maintien de North joue `Guard_Block` depuis le pack Super Fast Fighting,
avec `Twinblades_Defense_Hit_Root` comme fallback tant que la state n'a pas ete
installee dans `Player_Model`. Relacher North rend l'Idle de combat; seule une
pression commencant dans une fenetre qui accepte `Counter` declenche la parade.
`CombatWarningOn` et `CombatWarningOff`, poses sur les clips ennemis, pilotent
une alerte HDRP locale et une surcharge de focus temporaire vers
`EnemyLockPoint`. `OpenReactionWindow(float)` ouvre la fenetre en temps reel
non scale sans jamais resoudre les degats: impact et fin restent les Animation
Events existants.
Les actions combat orientent Lucian vers `EnemyLockPoint`. L'esquive fait
exception : une direction explicite du stick a priorite, Lucian s'oriente alors
dans cette direction et y roule; sans direction, il reste face a l'ennemi et
effectue une roulade arriere.
`Saut de rupture` est equipe dans le quatrieme slot de Lucian. Son `SkillSO`
utilise `Skill_3_JumpKick`, refuse de demarrer hors de la plage 0-3.25 m et
declenche `ResolveSkillImpactAndRetreat` au contact : degats 15, stagger ennemi,
VFX/Audio d'impact, onde monde et ScreenWave locale cyan. L'impulsion UCC
projette ensuite Lucian a l'oppose de `EnemyLockPoint`; ses controles restent
verrouilles jusqu'au prochain atterrissage, avec une borne de secours de 3.5 s.
Son profil conserve un elan horizontal decroissant pendant `0.6 s`, puis 35 %
de sa vitesse initiale jusqu'a l'atterrissage, pour eviter un arret en plein air.
Les Skills 1 a 4 et les BasicSkills partagent maintenant un
`CombatImpactFeedbackProfile` directement configure dans chaque `SkillSO`.
Apres un hit effectivement applique, `CombatImpactFeedbackController` du
`BattleManager` joue le hit-stop non scale, la ScreenWave locale, les cues
optionnels et une impulsion amortie du cadrage UCC. `CombatLockOnCameraController`
attenue et borne globalement chaque impulsion, puis remplace progressivement la
precedente : un combo ne peut plus cumuler des deplacements de camera. Les BasicSkills sont regles
a 5, 10 et 15 degats; leur epee reste visible entre deux coups bufferises puis
se masque a la sortie effective du combo. Les clips utilisent
`ResolveSkillImpact` pour les impacts standards et
`ResolveSkillImpactAndRetreat` pour le Saut de rupture.
La mobilite temps reel est centralisee par `CombatMobilityController` sur
`BattleManager`. En combat, `NorthButton` est reserve a la garde et au contre;
`EastButton` declenche l'esquive root directionnelle avec 0.05 s de preparation
et 0.18 s d'invulnerabilite; `SouthButton` declenche le saut UCC reel. Esquive
et saut continuent de valider les fenetres de reaction Animation Event. Une
seule commande de mobilite peut
etre bufferisee 0.12 s pendant une action. Chaque `PlayerActionPresentationProfile`
expose maintenant `mobilityCancelNormalizedTime` et `allowMobilityCancel`;
les BasicSkills 1, 2 et 3 ouvrent cette annulation a 0.35, 0.42 et 0.55.
Le lock garde le cadrage cible, mais Lucian se tourne vers son mouvement hors
d'action engagee.
Chaque impact ajoute aussi un micro-tremblement lateral et vertical, tres court
et amorti en temps non scale; son amplitude, sa duree et sa frequence sont
reglables globalement dans `GameplaySessionRoot`.
Le `LightSkillPanel` de `Bootstrap` est visible uniquement pendant
un combat temps reel. Sa jauge est alimentee par les degats effectivement
appliques par Lucian selon `Light Charge On Hit`. Les LightSkills sont ecrites
dans `AnimationLab`, puis exportees avec `Bake LightSkill` dans l'Inspector du
`LightSkillTimelineAuthoringRig`. Le bake refuse une Timeline invalide, demande
confirmation avant ecrasement et genere un package runtime fige dans le dossier
du `LightSkillSO` : prefab de rig et copie `Runtime` de la Timeline. Les
preview actors, Main Camera de preview, Brain et AudioListener restent dans
`AnimationLab`. Seules les Cinemachines utilisees et les objets marques
`LightSkillRuntimeExport` sont copies. Le package lie dynamiquement les tracks
`Player.Animator`, `Enemy.Animator`, `Cinemachine`, `Signals` et les tracks
d'objets exportes aux vrais acteurs au lancement. Les cameras declarent
optionnellement leur cible Player, Enemy ou EnemyLockPoint et leurs modules
Cinemachine dans AnimationLab; une camera sans cible est valide lorsqu'elle est
entierement cadree par la Timeline. Le bake les conserve a l'identique.
La Main Camera passe sous Cinemachine avant la premiere evaluation de
Timeline et la piste reste liee a la Brain de `CombatLockOnCameraController`,
jamais a une camera de preview. Un nouveau bake est requis apres
toute modification de Timeline, camera ou objet exporte.
`LightSkill_Devastation` est la base active; Furie a ete supprime integralement.
Le bake valide une Timeline temporaire, puis remplace la seule Timeline runtime
stable `<LightSkill>_Runtime.playable` et le prefab au meme chemin. En cas
d'echec le package precedent reste intact; apres succes, les anciens assets
`Runtime_Baked*` sont supprimes. Le bouton `Log Framing Audit` du rig auteur
capture les poses AnimationLab a `0 s`, `1 s` et aux changements de plan; le
rig runtime compare automatiquement Player, Enemy, camera, rotation et FOV aux
memes instants afin d'isoler tout decalage sans compenser le montage.
`Attach Framing Audit To Active Rig` permet d'ajouter ces seuls releves au
package runtime deja actif, sans rebaker ni modifier son montage.
Les LightSkills a pistes acteurs `ApplySceneOffsets` suspendent le relais de
root motion pendant la Timeline : elle reste l'unique proprietaire du
deplacement et evite une double application des deltas.
Le `PlayableDirector` baked est inerte (`Play On Awake` desactive,
`UnscaledGameTime`, `DirectorWrapMode.None`). Le rig est l'unique proprietaire
du lancement : placement, bindings, reconstruction du graphe, `Evaluate(0)`,
mise a jour manuelle de la Brain et verification stricte de la premiere
`CinemachineShot` precedent `Play()`. Une camera d'ouverture non stabilisee
refuse le lancement et restitue proprement la session; aucun lancement
approximatif n'est accepte.
Le bake compare maintenant un snapshot de chaque camera d'auteur (cle Timeline,
transform, FOV, priorite, Output Channel, targets, Follow offset et pipeline)
au prefab exporte. Une divergence, un script manquant ou une dependance a
`AnimationLab` refuse integralement le package : aucun module camera n'est plus
ajoute en runtime pour masquer un bake incomplet. Au lancement, le rig instancie
journalise son prefab, sa Timeline, la Brain de la camera explicitement exposee
par `CombatLockOnCameraController`, la vcam active et ses vraies cibles; il ne
consulte jamais `Camera.main` ni `AnimationLab`. La restitution est centralisee
et idempotente a la fin, a l'interruption, a la desactivation et au retour pool.
Le bridge Timeline remet explicitement l'autorite a `LitCameraDirector` sur la
Main Camera resolue par `CombatLockOnCameraController`; cette restitution coupe
la Brain Cinemachine et restaure les drivers UCC, sans arbitrage vers une autre
vcam.
Les pistes d'animation qui enregistrent une Cinemachine (transform ou Lens) sont
des pistes camera bakees : leur binding est copie vers l'Animator de la camera
exportee. Devastation relie ainsi `Animation Track` a `Camera_1` dans son
package runtime, au lieu de laisser `None (Animator)` dans la Timeline. Ces
pistes conservent exactement leur mode d'offset Timeline d'auteur dans le
package runtime : leurs poses restent fideles au montage de `AnimationLab` tout
en etant portees par le rig aligne sur Lucian et l'ennemi. `LitCameraDirector`
est l'unique proprietaire du handoff
UCC pendant une LightSkill; le lock suspend seulement son cadrage et ne coupe
plus les drivers UCC une seconde fois.
`Lucian_Anchor` et `Enemy_Anchor` deviennent les reperes canoniques de roots
dans AnimationLab. Le bake les exporte comme `PlayerStageAnchor` et
`EnemyStageAnchor`, puis refuse tout package qui ne contient pas ces deux
locators. Au runtime, le rig est pose au midpoint de Lucian et de l'ennemi,
aligne sur leur axe horizontal et les vrais roots sont poses directement sur les
anchors avant la premiere evaluation de Timeline. Il n'y a ni recherche de
plateau, ni deplacement peripherique, ni test de collision, NavMesh ou flash de
transition. La LightSkill joue toujours immediatement sur place selon le
midpoint et les anchors bakes. Les positions finales sont conservees.
Les pistes `Player.Animator` et `Enemy.Animator` sont converties au bake en
`ApplySceneOffsets`. Une LightSkill posee sur un plateau laisse alors Timeline
etre l'unique pilote des transforms acteurs pendant la lecture : le rig ne lit
ni ne repose UCC ou l'ennemi chaque frame. Cela evite toute boucle de
retroaction entre Timeline et UCC; le root motion reste relatif au plateau
runtime. Un package plus ancien est refuse et doit etre rebake. Les
CounterSkills, qui n'utilisent pas ce placement de plateau, conservent leur
chemin root motion existant.
Le contrat de plateau version 3 enregistre aussi l'orientation monde du rig
d'auteur; tout prefab LightSkill plus ancien doit etre rebake avant lecture.
Le contrat `CombatActorAnimationRoot` centralise maintenant le root gameplay,
son `AnimationRoot`, l'Animator de gameplay et `EnemyLockPoint` lorsqu'il
existe. Le combat, les skills, UCC et les Timelines resolvent cet Animator
explicitement au lieu de choisir un enfant. `Lit/Combat/Normalize Actor
Animation Hierarchies` encapsule sans deformer les skeletons importes les
Animators des deux ennemis sous un `AnimationRoot` identite, retire l'Animator
vide du Giant et rebranche les references de combat. Lucian conserve
provisoirement son Animator racine: ses clips generiques dependants des paths
ne doivent pas etre reparentes automatiquement. Son comportement passe neanmoins
par le meme contrat et le meme relais de root motion cinematographique.
En sortie, les poses locales imposees par les Animation Tracks sur les Animators
Player et Enemy sont remises a zero avant la remise en pool du rig : chaque
declenchement repart donc d'un etat propre sans reemployer l'offset precedent.
Chaque `LightSkillSO` expose aussi deux profils optionnels de state post-Timeline
(Lucian, ennemi : state, fondu, temps normalise). Ils ne sont appliques qu'a la
fin naturelle de la Timeline et ne peuvent jamais ecraser une mort. Devastation
laisse le profil joueur vide et enchaine l'ennemi vers la state `GetUp` du
Juggernaut, basee sur le clip `EnterTheBattle_1` en attendant un clip de releve
dedie.
Le rig poolé efface aussi tous ses bindings Timeline et references Cinemachine,
arrete son director au temps zero puis revient a une transform identite avant sa
prochaine acquisition.
Pendant la Timeline, l'auto-desengagement de combat est suspendu : l'IA cible
est volontairement inactive et ne doit jamais interrompre la sequence avant sa
fin.
La pose de Lucian passe par `SetCinematicPositionAndRotation` du bridge UCC :
elle reste donc autorisee pendant le verrou d'input de la Timeline, sans jamais
le liberer.
La Brain passe en `Cut` pendant une Timeline LightSkill, puis retrouve son
blend precedent a la restitution : aucun mélange UCC/Cinemachine ne modifie le
premier plan cinematographique.
Le lancement d'une LightSkill resout explicitement le bridge UCC de Lucian;
tout refus (references combat, portee, locomotion UCC, camera Cinemachine ou sequence deja active)
est affiche dans la Console et par un feedback world-space.
`Skill_2_Fleche de lumiere` restitue maintenant la locomotion a 78 % de son
clip, apres le tir, au lieu d'attendre sa quasi-totalite.
Quand les PV de Lucian atteignent zero, `PlayerActionPresentationController`
verrouille la state `Death` avant la resolution de la defaite. Les actions,
combos, hurts et retours automatiques vers la locomotion ne peuvent plus la
remplacer; le verrou ne disparait qu'au rechargement de la partie via
`Revivre`.
L'arc et l'epee restent pilotables par leurs Animation Events, mais une fin ou
interruption d'action notifie aussi `RealTimeCombatAnimationEvents`, qui les
range automatiquement. Cette securite evenementielle couvre les clips dont le
`HideBow` ou `HideSword` final n'est pas atteint.
La sortie des BasicSkills est aussi securisee par le controleur de presentation :
si une state d'attaque est perdue avant son seuil de recuperation, il force le
retour a `Locomotion` plutot que de laisser Lucian fige dans sa pose finale.
`RealTimeCombatEnemyBehaviour` fournit la poursuite reutilisable : la vision
reste normale en patrouille, puis passe a son `alertedVisionDistance` apres une
detection et conserve cette alerte pendant `alertMemorySeconds` sans ligne de
vue. Le Juggernaut utilise `24 m` et `12 s` par defaut. Le lock combat utilise
ce rayon alerte plutot que le rayon initial de 7 m. Cette detection seule ne
declenche pas la musique : elle commence seulement lorsque Lucian inflige son
premier degat de lumiere, puis reste active pendant l'alerte provoquee.
Quand un combat se ferme automatiquement, le manager attend le relachement du
stick puis purge l'input et la vitesse UCC residuels afin que Lucian ne continue
pas a marcher dans une direction obsolete.
Un ennemi alerte regarde Lucian, mais ne le poursuit pour convertir son ledger
en riposte qu'apres avoir recu une attaque de lumiere. Apres une perte de vue,
il inspecte une fois sa derniere position connue pendant une courte duree, sans
utiliser la position reelle du joueur hors ligne de vue, puis retourne a son
point de patrouille. Son inspecteur expose une preference melee en pourcentage :
quand les deux types sont disponibles, il choisit d'abord melee ou distance
selon ce tirage, puis s'approche jusqu'a la portee correspondante. Le Juggernaut
porte actuellement le seul skill melee `Skill_Juggernaut_Assomoir`. Le
GiantJuggernaut utilise `Skill_GiantJuggernaut_Jump` via le meme flux : son
skill est utilisable de 0 a 30 m, sa state Animator est
`GiantJuggernaut_Jump` et son clip clot la riposte avec `EndEnemyAttack`. Son
clip n'est pas boucle. Les degats de lumiere recus pendant une attaque active
sont conserves dans un ledger suivant : a `EndEnemyAttack`, seul leur maximum
prepare une nouvelle riposte apres le delai normal, sans interrompre ni
relancer immediatement l'attaque en cours.
`RealTimeCombatEnemy` resout toujours l'Animator de gameplay portant un
Controller, avec priorite a celui configure dans `EnemySkills`; le
GiantJuggernaut utilise explicitement son Animator `MidPoly` pour ses hits et
son retour a `Idle`. Sa Timeline d'introduction ne deplace plus Lucian : il
reste a sa position d'entree jusqu'au lancement du combat.
Pendant son alerte, un ennemi provoque conserve Lucian comme cible de combat
meme si une animation root le tourne temporairement hors de son cone de vision :
il se retourne vers lui et peut consommer son ledger suivant tant que sa memoire
d'alerte reste active.
`AnimationGroundRecovery`, dans `Assets/Scripts/Animation/`, reste reserve a
Lucian. Les deux ennemis utilisent desormais `CombatEnemyPhysicsMotor` avec un
`Rigidbody` cinematique et une `CapsuleCollider` configures dans leurs prefabs :
il est l'unique autorite verticale pendant une attaque, ignore le root motion Y
des skills au sol et attend un contact sol avant de rendre l'IA et `Idle`.
Une sonde de sol ne peut corriger verticalement un `ActorRoot` que de `0.75 m`
au maximum : un collider d'un autre niveau du decor est journalise puis ignore.
Son audit de pose journalise temporairement spawn, hit, NavMesh, Rigidbody,
parent, Animator et synchronisation Netcode ainsi que tout saut superieur a
0.5 m, afin d'identifier une ecriture de transform concurrente.
Les skills ennemis portent un profil `Grounded` ou `Airborne`; `BeginEnemyAirborne`
et `RequestEnemyLanding` ouvrent et ferment la trajectoire balistique. Une mort
ou une interruption force d'abord une chute controlee, ce qui empeche un ennemi
de rester suspendu.
`RealTimeCombatEnemyBehaviour.Can Pursue Player` est actif par defaut. S'il est
desactive, l'ennemi reste immobile pendant son alerte, conserve son regard sur
Lucian et n'attaque que si la portee du skill choisi est deja respectee.
Tant qu'au moins un ennemi est dans ce mode, le manager applique l'override de
musique combat; il le relache apres le desengagement, la mort ou la fin de
l'affrontement.
Chaque degat du prototype temps reel produit aussi un nombre world-space local,
attache au combattant touche : cyan pour la lumiere recue par l'ennemi, rouge
pale pour Lucian. Il est billboard vers la camera, pulse, monte legerement et
s'efface en temps non scale sans prefab ni Canvas de scene.
Un impact ennemi non letal effectivement applique joue aussi la state joueur
`Base Layer.RealTimeCombat_RootMotion.TwinSword_Defense_Hit_Root`, configurable
sur `RealTimeCombatManager`, puis rend la locomotion a Lucian a la fin du clip.
Un coup letal conserve exclusivement la state `Death`.
Quand les PV de Lucian atteignent zero, UCC bloque normalement sa locomotion et
le manager ferme le combat; les skills de roue et attaques de loadout refusent
alors aussi toute nouvelle execution. La state `Base Layer.Death` est jouee
avant l'affichage du `DefeatPanel` de scene, adapte temporairement aux choix
`Revivre` et `Quitter le jeu`. Revivre recharge la scene du dernier checkpoint
de la sauvegarde active et supprime l'autosave de mort juste avant ce chargement.
`Revivre` recoit le focus manette initial; haut/bas navigue entre les deux choix,
avec une mise en avant visuelle interpolee du bouton selectionne et une
attenuation des autres choix. La croix directionnelle et le bouton Sud de la
manette pilotent directement cette navigation, meme avec un `EventSystem`.
Les `StatsSO` portent exclusivement les checks historiques. Les `SkillSO` de
combat portent nom, icone, clip, degats, une liste de VFX et une presentation
d'arme optionnelle, et sont listes dans
`CharacterData.combatSkills`. `SkillsManager`, place sur `SkillsPanel`, filtre les
competences de combat connues de Lucian et maintient jusqu'a huit competences equipees,
repercutees par evenement sur les `SkillWheelSlot` sans creation d'UI runtime
ni polling `Update`. Ce loadout n'est pas encore persiste dans les sauvegardes
personnage.
Tant que `LeftTrigger` est maintenu, l'alpha de `SkillsWheelSlots` passe de
`0` a `0.4` et le joystick droit survole le slot radial correspondant, avec une
echelle x1.5 et un alpha renforce; le relachement le ramene a `0`. `SouthButton`
valide la competence selectionnee et reste prioritaire sur le saut UCC tant que
le trigger est maintenu, sans bloquer la locomotion. A la fin du clip de skill,
le joueur revient a sa locomotion normale. Ce retour est borne par la duree du
clip du SkillSO, synchronise d'abord la pose finale root avec UCC, arrete les capacites UCC residuelles et le controleur personnage, puis declenche
`MoveStopTrigger` et reprend `Base Layer.Locomotion` avec ses parametres a zero,
sans ramener Lucian a sa pose initiale. La pose root finale est capturee apres
la derniere frame evaluee du clip puis reappliquee a UCC apres le `CrossFade`,
pour que les skills root comme `Skill_2_Fleche de lumiere` conservent leur
deplacement. Les attaques root du prototype appliquent
la meme synchronisation avant leur retour vers la locomotion. Les states taguees
`RealTimeCombatRootMotion` sont reconnues par UCC comme du root motion actif :
leur deplacement et leur rotation ne sont donc pas supprimes quand l'input est nul.
La validation oriente d'abord Lucian horizontalement vers `EnemyLockPoint` via
UCC, puis joue l'etat Animator explicitement configure sur le `SkillSO`
selectionne (avec fallback sur le nom du `AnimationClip`);
ses Animation Events restent responsables des VFX et degats. Les slots sans
SkillSO sont masques et exclus de la navigation de la roue. Les Skills et
BasicSkills de Lucian utilisent `UccBody` : la capsule conserve donc l'orientation
vers la cible apres le retour a locomotion, au lieu de ne tourner que le rig.
`SkillsManager` expose aussi une liste `BasicSkills` de `BasicSkillsSO`.
Cette preparation distingue maintenant les combos `GroundBasicSkills` et
`AirBasicSkills`. Chaque BasicSkill aerien peut, dans son Inspector, rester
en l'air ou demander une descente UCC a une seconde precise de l'animation,
sans teleporter Lucian au sol.
Pendant un lock, `WestButton` ajoute le prochain basic skill a un buffer de
combo : les clips sont joues dans l'ordre de la liste puis bouclent. Les Basic
Skills reutilisent les memes Animation Events de VFX et de hit que les skills
de la roue. Une pause de plus de `0.85 s` entre deux pressions reprend le combo
au premier basic skill. Une seule attaque peut etre bufferisee derriere celle
en cours; les pressions excedentaires sont ignorees. Chaque profil de
presentation expose une ouverture de chaine et une transition de chaine : une
BasicSkill bufferisee interrompt alors le clip courant a cette transition, sans
attendre sa recuperation. Les trois BasicSkills de Lucian ouvrent a `0.55`,
transitionnent entre `0.66` et `0.70` puis restituent la locomotion entre `0.68`
et `0.74`, avec un blend de sortie de `0.05 s`. `ToggleTorch` ignore
`WestButton` tant qu'une cible est verrouillee.
Chaque `SkillSO`, donc aussi chaque `BasicSkillsSO`, expose une distance
horizontale minimale et maximale de hit. `HitEnemy` ne blesse la cible qu'a la
frame de son Animation Event si Lucian est dans cette plage autour de
`EnemyLockPoint`. Les VFX `DirectOnTarget` et `Projectile` sont soumis a la
meme verification; les cues `PlayerHand` restent joues pour la presentation du

Les `SkillSO` peuvent maintenant choisir une Timeline optionnelle bakee depuis
`CombatSkillTimelineAuthoringRig`. Cette session s'execute sur place et utilise
le pool de `CombatCinematicRig` deja eprouve par les LightSkills. Elle suspend
le combat entier sans modifier le temps global; `ResolveCinematicSkillImpact`
reste l'unique impact valide. Les BasicSkills cinematographiques conservent leur
combo a la restitution, tandis que les EnemySkills finalisent leur ledger une
seule fois.
caster. Un hit refuse affiche aussi un feedback world-space `Raté (trop près)`
ou `Raté (trop loin)` sur l'ennemi.
La meme plage est appliquee a l'impact d'un skill ennemi, entre sa racine et
Lucian : hors plage, le joueur ne subit aucun degat.
`Skill_3_Entaille` utilise explicitement `Base Layer.Skill_3_Entaille` et porte
le tag `RealTimeCombatRootMotion`, afin que son retour a locomotion ne laisse
pas une intention UCC residuelle.
`Skill_2_Fleche de lumiere` cible explicitement sa state root plutot que le
fallback par nom de clip.
Les clips de skill peuvent aussi appeler `Dash`, qui propulse Lucian sur la
droite vers `EnemyLockPoint` avec un depassement reglable, puis `StopDash`, qui
freine cette impulsion sur une courte duree plutot que de l'annuler instantanement.
Les clips ennemis peuvent appeler `ShowInput(Sprite)` et `HideInput()` sur
`RealTimeCombatAnimationEvents` pour afficher puis retirer un prompt
world-space ancre au `EnemyLockPoint`. Le Sprite 2D est assigne directement
dans l'Animation Event, independamment de la fenetre logique de reaction.
Les clips d'attaque joueur peuvent maintenant declencher `InstantiateSkillVFX`
ou `InstantiateSkillVFXAtIndex` sur `RealTimeCombatAnimationEvents` : chaque
VFX du `SkillSO` peut apparaitre directement sur `EnemyLockPoint`, ou rester
sur la main tenant l'arc, ou rester sur le joueur puis devenir un projectile avec
ses delais configures. Chaque cue peut aussi jouer son `AudioClipSO` au point de
depart de sa presentation. `HitEnemy`
applique ensuite ses degats a l'ennemi locke
et son etat Animator `Hit` configurable est joue.
Le Juggernaut possede aussi `EnemySkills`, avec une liste de `SkillSO` a
assigner dans son Inspector, sans roue. Ses clips peuvent utiliser le meme
`RealTimeCombatAnimationEvents` avec `SetEnemySkill(int)`, `PlayEnemySkill(int)`,
`InstantiateEnemySkillVFX`, `InstantiateEnemySkillVFXAtIndex(int)` et
`HitPlayer` pour synchroniser animation, VFX et impact sur Lucian. La portee,
le multiplicateur et les reactions ennemies sont portes par le `SkillSO`; les
degats finaux restent calcules par le ledger de lumiere.
`HitPlayerIf("Grounded")` permet maintenant a un Animation Event ennemi de ne
toucher Lucian que s'il est au sol selon UCC. Les conditions sont des noms
extensibles; une condition inconnue est refusee avec un avertissement unique.
Une attaque ennemie active reste prioritaire sur son animation `Hit`, afin de
ne jamais interrompre le root motion d'un saut comme `Assomoir`.
Les ennemis temps reel ne dependent pas de transitions implicites de leur
Animator : un `Hit` suspend temporairement son root motion puis revient
doucement a la state `Idle` configuree, et `EndEnemyAttack` ramene aussi le
skill ennemi termine a cette state.
Le `Bow` deja attache a la main de Lucian est pilote strictement par les
Animation Events `ShowBow` et `HideBow` des clips; les VFX optionnels associes
sont configures sur le composant `PlayerBow` du modele Lucian.
L'epee `Sword` suit le meme flux via `PlayerSword` et les Animation Events
`ShowSword` et `HideSword`; elle est masquee par defaut et peut posseder ses
propres VFX optionnels d'apparition/disparition.
Une Ancient Flame proche d'un ennemi temps reel vivant devient bleue quand elle
est allumee, mais reste inerte : elle ne revele rien, n'active aucun objet et
ne compte pas pour la restauration temporelle avant la disparition de l'ennemi.

Le combat tour par tour existant pilote encore la camera locale par phase via
`CombatCameraPresentationController`; pendant le combat, Opsive est suspendu
comme driver camera spatial puis restaure a la sortie.
`CombatSessionManager` peut instancier un prefab public a mi-chemin entre le
joueur et l'ennemi au pic de la transition d'entree en combat; en reseau,
cette presentation est demandee a tous les clients et detruite a la sortie
manuelle du combat. L'entree combat est orchestree par `BattleTransition` sur le
`BattleManager` de `Maison`, mais la vague HDRP est maintenant un systeme dedie :
`ScreenWaveController` pilote le `CustomPassVolume` de scene `ScreenWavePass`
avec le material `MAT_ScreenWave`. Le bouton inspecteur `PlayScreenWave` permet
de tester une vague unique hors Play Mode depuis le `BattleManager`, sans
creation runtime. Le composant reste actif afin que son `Update` non scale
termine toujours le cycle et coupe le Custom Pass. La vague expose son origine,
sa direction, sa frequence, sa
vitesse de propagation, son amplitude, sa duree, son attenuation et un lisere
lumineux reglable pour rester lisible dans les scenes sombres, puis
`BattleTransition` la declenche au debut du combat pendant que BattleSphere, VFX
et shaders sont prechauffes au demarrage de scene. Quand le joueur rencontre un
ennemi, l'entree combat fige localement le temps pendant la premiere vague,
masque l'Outline monde actif pendant ce gel, effectue le placement combat a la
fin de cette vague, puis laisse le meme
Custom Pass enchaîner une deuxieme vague inversee pour revenir a un rendu normal
dans l'arene. Le bouton `PlayScreenWave` teste ce cycle complet hors Play Mode.
Le snapshot de retry pre-combat est aussi capture au pic de cette vague, avant tout
deplacement, pour eviter un gel visible avant l'effet. Ses validations
persistent en memoire sans etre loguees comme erreurs console.
Si le prefab contient un ou plusieurs `CharacterEffect`, ils sont joues a
l'apparition, stoppes a la sortie, puis detruits apres un delai par defaut de 2
secondes.
Chaque phase shot peut aussi ajouter une vitesse de deplacement locale a son
offset camera pour creer un drift cinematographique pendant la phase.
Le ralenti de combat est maintenant strictement pilote par les `AnimationEvent`
des clips via `CombatAnimationEvents`; la camera, le manager de combat et les
impacts ne declenchent plus de ralenti automatique.
Un composant `CombatAnimationEvents` permet aux clips d'attaque de declencher
un ralenti local, une ruee vers la victime et un retour a la pose initiale.
`Griffe` du Juggernaut est maintenant pilotee par son clip : le manager lance
seulement `Attack_Griffe`, sans saut/dash/audio/VFX specifiques codes.
Les clips peuvent notifier l'impact avec `NotifyCombatImpact`; le manager
applique alors l'impact pending une seule fois. Les impacts de combat attendent
ce `AnimationEvent` et ne sont plus resolus par timer fallback.
Le ralenti Animation Event descend maintenant a `0.1`, cible l'acteur et la
victime, et les anciens hooks legacy autonomes qui modifiaient `Time.timeScale`
ont ete retires.
Le joueur peut maintenant assigner hors combat jusqu'a 3 items defensifs comme
items combat. Les clips d'attaque ouvrent/ferment `CombatDefensePanel` via
`CombatAnimationEvents`; le panel ne propose que ces items a portee de main, et
la selection reste validee par `CombatSessionManager`, synchronisee par
`NetworkInventory` et sauvegardee avec l'etat personnage.
La fenetre defensive autorisee correspond maintenant a l'affichage reel de
`CombatDefensePanel` : tant que le panel est visible, le joueur peut remplacer
son choix, et seul le dernier item selectionne est resolu a l'impact avec un
surlignage, un tint de fond et un agrandissement du slot choisi. Les messages
positifs de choix defensif sont ajoutes au journal `CombatLog`; les erreurs
restent affichees dans `InfoBoxUI`.
Quand une attaque ennemie normale commence, le joueur local peut jouer une
animation de preparation defensive configurable et une voix `AudioClipSO`
optionnelle, avec fallback animation sur `Defense` puis `Block`.
Pendant toute la session de combat, le HUD garde l'ActionMap locale `Combat`
active, prend le focus exclusif et ferme l'inventaire s'il etait deja ouvert;
Au lancement de l'intro combat, il ferme aussi les UI monde deja ouvertes
(loot, pause, dialogue, InfoBox, confirmation, lecture, craft et construction)
afin de ne laisser visibles que les panels pilotes par le combat;
`UseItem1`, `UseItem2` et `UseItem3` activent les trois slots quand
`CombatDefensePanel` est visible. Le panel force aussi l'ActionMap `Combat` a
son affichage afin qu'un AnimationEvent ne laisse jamais NorthButton ouvrir
l'inventaire. Les enfants `Text` affichent le nom de l'item assigne, les slots
sans item restent masques, et l'affichage ne purge plus les items combat si
l'inventaire runtime n'est pas encore initialise;
l'initialisation des starter items conserve aussi ces assignations quand l'item
existe dans les objets de depart. Les anciennes sauvegardes `CharacterSaveData`
avant version 5 migrent les items combat manquants depuis les defaults du
personnage, puis les nouvelles sauvegardes persistent aussi les items avec
`CombatReactionProfile`. Les racines `EnableItem_1/2/3` sont aussi garanties
comme boutons UI pour la souris et la navigation manette/clavier.
Les items peuvent porter un `CombatReactionProfile` optionnel. `Item_Weapon_Sword`
est configure comme premier contre melee : il declenche `Counter_Sword`,
`Impaled`, un shot camera `CounterAction`, un court ralenti local, et peut
configurer ses attaches, SFX/VFX/voix depuis l'item. Les clips de contre peuvent
maintenant piloter le visuel via les AnimationEvents `Take` (apparition en main)
et `Release` (plantage sur l'ennemi), puis interrompre l'attaque ennemie via
`CounterHit` pour jouer `Impaled` et resoudre l'impact logique du contre au
meme frame, sans delai `impactDelaySeconds` cote manager. Si ce `CounterHit`
tue l'ennemi, `Impaled` est ignore et la resolution de victoire demarre sur
l'animation de mort.
`Item_Shield_WoodShield` utilise maintenant le type `MeleeDefense` : le joueur
sort son visuel en main, bloque les attaques melee, l'item perd des PV defensifs
persistes sur le personnage entre les combats, reste reutilisable s'il lui en
reste et casse sinon. L'inventaire separe les boucliers en piles par PV
restants identiques, par exemple une pile de boucliers pleins et des piles
distinctes pour les boucliers abimes.
Le profil peut aussi jouer un `enemyAnimationClip` direct via Playables, sans
state Animator par ennemi; `Impaled` reste le fallback par nom. Le Juggernaut
expose une state Animator `Impaled` vide pour y brancher le clip si besoin, et
le visuel plante oriente automatiquement son axe Z a l'inverse du Z local de
l'ennemi.
L'UI de combat joue maintenant `CombatEngagedPanel_Trigger` sur
`CombatEngagedPanel` des l'entree en session, sans attendre le premier snapshot,
affiche ensuite `CombatScreenInfosPanel`, puis masque ces infos quand un
AnimationEvent demande `CombatDefensePanel`; a la fermeture du panel, les infos
de combat reviennent.
Les demandes `CombatDefensePanel` issues des AnimationEvents ont priorite sur
l'intro de combat afin que la fenetre defensive s'ouvre meme si l'attaque
ennemie commence pendant `CombatEngagedPanel_Trigger`.
Les panels combat restaurent aussi leur hierarchie UI active et une echelle non
nulle afin de rester visibles si la racine `UI_Overlay` a ete sauvegardee a
`localScale` zero.
Les transitions d'entree et sortie du ralenti combat jouent des sons via
`ActionAudioCue.CombatTimeSlow` et `ActionAudioCue.CombatTimeResume`, relies a
des `AudioClipSO` de la banque de sons, et appliquent un leger ducking de la
musique pendant toute la duree du ralenti.
`RuntimePersistenceUtility` ignore les objets sous `NetworkObject`; les objets
reseau de scene restent donc dans leur hierarchie Netcode et ne sont plus
reparentes en `DontDestroyOnLoad` avant le demarrage host/server.
L'import legacy Symphonie est isole dans son assembly non auto-referencee,
Editor-only et compilee seulement avec le define manuel
`SYMPHONIE_IMPORT_COMPILE`; le combat actuel ne reference pas ses types ni ses
GUIDs.
La fin de combat n'est plus une sortie automatique immediatement apres la
resolution : le perdant joue sa mort, le gagnant tente un taunt (`Taunt`,
`Victory` ou `Celebrate`), et une victoire declenche le travelling camera
ralenti stylise des le coup fatal joueur, puis affiche le `VictoryPanel` apres
un delai sur : au moins 3 secondes apres ce coup, la fin de l'action fatale, la
mort ennemie et une petite marge. Les panels de scene `VictoryPanel` ou `DefeatPanel`
attendent ensuite une validation manuelle du joueur avant de restaurer les
positions, camera, mouvement et resultats monde.
En cas de defaite, la resolution remplace aussi la musique de combat par une
musique `Game Over` issue de `CombatAudioLibrary` jusqu'a la sortie manuelle.
Le `DefeatPanel` garde son texte de scene et expose maintenant trois boutons :
retour `MainMenu`, retry immediat du combat courant, ou retour au dernier
checkpoint/sauvegarde active. Les retours menu/checkpoint ignorent la prochaine
sauvegarde automatique pour ne pas persister l'etat de defaite.
Le retry capture maintenant un snapshot runtime pre-combat et le restaure avant
de relancer la session, afin de revenir aux inventaires, PV de bouclier, monde
persistant et PV joueur du debut de combat. Il remet aussi les Animator joueur
et ennemi a leur etat par defaut avant la nouvelle entree combat, y compris sur
le client proprietaire en reseau, pour eviter de conserver une mort ou un taunt
de la tentative precedente.

## Contraintes

- Le prototype de combat temps reel route les degats recus par Lucian via
  `SquadCharacterController.ApplyDamage`, afin de conserver la synchronisation
  de sante UCC, les retours de degats et la mort. Le degat ennemi est le plus
  grand degat de lumiere recu depuis sa precedente attaque : avec 10 PV,
  recevoir une riposte apres un skill a 10 ou 20 degats est volontairement
  letal et termine le combat.

- Garder `Codex/AGENTS.md` et `Codex/current_work.md` courts.
- Lire uniquement les fiches pertinentes de `Codex/systems/`.
- Ne pas recreer l'ancien pipeline AIStudio sans demande explicite.
- Ne pas stocker de secrets, caches ou environnements virtuels dans `AIAgent`.

La telegraphie de reaction temps reel est maintenant en trois temps et reste
exclusivement pilotee par les Animation Events ennemis
`BeginReactionTelegraph`, `OpenReactionWindow` et
`ResolveEnemyAttackImpact`. `CombatReactionTelegraphController`, sur
`BattleManager`, pilote le prompt world-space unique, les pulses
`AttackLightAlert`, les AudioClipSO et le micro-ralenti de fenetre parfaite.
Le menu Unity `Lit/Combat/Configure Reaction Telegraph` configure le prototype
Assomoir et cree le prompt de scene lorsqu'il est absent.
`LocalPlayerInput` detruit son `InputActionAsset` immediatement hors Play Mode,
afin qu'un dechargement de scene editeur ne laisse plus un GameObject
`LocalPlayerInput` residuel.
Le prompt de reaction reacquiert sa reference de scene avant chaque phase et
force son Canvas world-space sur la camera principale avec un ordre de rendu
eleve, afin de rester visible apres un changement de scene.

AnimationLab adopte maintenant le meme contrat que `Juggernaut_Combat` pour
son ennemi preview : `Enemy_Preview` porte l'Animator `Juggernaut.controller`,
le skeleton `MidPoly` reste visuel et les bakers enregistrent les poses des
`ActorRoot` plutot que celles d'un enfant Animator. Les rigs de Skills et de
LightSkills resolvent donc explicitement les roots preview avant de binder les
pistes `Player.Animator` et `Enemy.Animator`. Le menu
`Lit/Combat/Update AnimationLab Root Animators` resynchronise la scene et le
prefab AnimationLab avec ce contrat pour les futures migrations.

La locomotion combat est maintenant separee par autorite : le `NavMeshAgent`
deplace les ennemis hors action, `CombatEnemyLocomotionController` traduit sa
vitesse en strafe Root (sans lui ceder le deplacement monde), et
`CombatEnemyPhysicsMotor` possede les actions
engagees. `Skill_Juggernaut_Assomoir` est aerien : `BeginEnemyAirborne`,
`BeginEnemyRush`, `EndEnemyRush` et `RequestEnemyLanding` remplacent son ancien
deplacement ponctuel `DashToTarget`. La ruée suit la cible horizontalement,
s'arrete avant elle et ne peut pas ajouter de root motion concurrent.
Le lock ne remet plus a zero une intention de deplacement deja maintenue, et le
masque sol du Juggernaut couvre maintenant tout le decor afin que sa recuperation
physique ne puisse pas tomber sous le niveau.

L'orbe de Munin est maintenant visuellement independante de celle du chargement:
elle conserve ses materiaux HDRP, n'applique plus de transparence derivee du noir
et utilise des pulses de presentation relies aux evenements de charges. La
distorsion permanente est desactivee; elle ne joue qu'au court etat d'action.

Munin peut maintenant fusionner avec Lucian en exploration et en combat via
l'action `Melt`, sur un clic bref du stick droit. Le recentrage reste disponible
par maintien du stick droit (et `C` au clavier), tandis que le mode locomotion
est sur la croix droite. `Melt` declenche l'animation `Melt` hors fusion et
`Rupture` pendant une fusion; leurs AnimationEvents determinent le frame de
l'effet Holy et confirment l'etat visuel de Munin. Une LightSkill force cette
fusion, annule sans animation une
fusion precedente, puis defusionne automatiquement apres sa Timeline. Munin est
aussi masque tant que l'arc ou l'epee est manifeste, ou qu'une future arme porte
`SpiritWeaponManifestation`. Un AnimationEvent `InstantiateAtSpine` peut aussi
instancier un prefab configure sur l'os Spine de Lucian. Les triggers Animator
`Melt` et `Rupture` partent de `Any State` et reviennent a la locomotion en fin
de clip (sans passer par `Sit_Down`). `StopEffect_CharacterEffect` arrete proprement le graphe
VFX, sans masquer son GameObject. Holy est porte par `CC_Base_Body` pour rester cale sur son
`SkinnedMeshRenderer`; l'AnimationEvent historique `PlayEffect_CharacterEffect`
est relaye depuis la racine Animee de Lucian; pendant une Rupture legacy, il est
interprete comme un arret. `StopEffect_CharacterEffect` arrete explicitement Holy. Son instance VFX est egalement
enfant de `CC_Base_Body`; son binder Transform reste actif pour conserver une
AABB VFX valide. Les offsets du prefab Holy sont neutralises sur l'origine du
body de Lucian. `StopEffect_CharacterEffect` est uniquement pilote par l'AnimationEvent de
`Rupture`; sa sortie attend `1.1` temps normalise pour laisser son evenement de
fin de clip s'executer.
`MuninOrbVisualController` ne modifie plus les matériaux, les UV ni le mode de
rendu des enfants de `Munin_v4` en Play : le rendu de jeu conserve donc celui du
prefab en édition. Il applique seulement un plafond de taille écran de 0,06 aux
particules (ou une valeur auteur plus basse), afin d'éviter que les flares ne
deviennent de grands panneaux près de la caméra. Les couches `Flare4_Additive`,
`Flare_Ultrawide` et `FuzzAdd`, qui restent rectangulaires avec le shader du
pack sous HDRP, sont masquées uniquement au runtime.
La racine de `Munin_v4` ne doit pas être agrandie : certains systèmes du pack
deviennent invalides sous HDRP; un agrandissement futur doit viser les modules
de taille des particules. `MuninOrbVisualController` applique actuellement un
facteur de 2,5 à leur `startSize` et restaure les valeurs auteurs à sa désactivation.

## Prochaine utilisation

Pour une nouvelle tache, partir du modele `prompts/codex_task.md`, remplacer
`[TACHE]`, puis fournir le prompt a Codex depuis le contexte `AIAgent`.
