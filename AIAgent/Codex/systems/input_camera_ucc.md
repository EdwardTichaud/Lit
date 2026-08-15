# Input, UCC et caméra

## Rôle

Centraliser les actions locales, les transmettre au bon personnage et adapter
la façade gameplay Lit à Opsive UCC et à sa caméra.

## Recuperation root motion

## Contrat ActorRoot

Lucian, Juggernaut et GiantJuggernaut utilisent `ActorRoot > AnimationRoot`.
L'`ActorRoot` est le seul Transform monde. Durant une Timeline LightSkill,
`CombatActorRootMotionRelay` transmet une seule fois chaque delta de l'Animator
vers `LitOpsiveLocomotionBridge` ou le comportement ennemi; aucun rig
cinematique ne deplace directement les acteurs apres leur placement initial.

`AnimationGroundRecovery`, dans `Assets/Scripts/Animation/`, est un garde-fou
generique attache aux racines de Lucian, Juggernaut et GiantJuggernaut. Apres
le root motion, il sonde le support sous le Transform reellement anime et ne
corrige qu'une penetration vers le haut. Il laisse intacts la trajectoire
horizontale, les sauts et les animations root valides; ce systeme ne depend pas
du combat.

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

`PlayerInputs` → `LocalPlayerInput` → `LocalInputRouter`.

Le flux se divise ensuite :

- solo vers `SquadManager` / `SquadCharacterController`;
- réseau vers `NetworkCharacterInput`;
- caméra vers les abonnés dédiés.
- combat vers `CombatDefensePanelController` via `UseItem1/2/3`.

`SquadCharacterController` convertit l’input en espace monde, puis
`LitOpsiveLocomotionBridge` pilote UCC.

La caméra gameplay doit passer par le `CameraController` Opsive UCC, avec
`LitUccCameraCharacterBinder` sur la caméra active pour suivre
`LocalPlayerContext`. Les anciens pivots `CameraAnchor` / `YawPivot` /
`PitchPivot` du système legacy ne doivent pas piloter la `Main Camera`.

Exception : en combat, `CombatSessionManager` devient la source de phase camera.
`CombatCameraPresentationController` suspend temporairement le driver camera
Opsive et pilote directement la `Main Camera`, puis restaure Opsive a la sortie
du combat.
Quand `CombatDefensePanel` est visible, `LocalPlayerInput` active seulement
l'ActionMap `Combat` et coupe `Player`/`Camera`; a la fermeture du panel, les
maps gameplay sont restaurees.
Les contres melee configures par item utilisent un override camera local
`CounterAction` dans `CombatCameraPresentationController`, sans changer la source
de phase camera du manager.

Le prototype `CombatRealTime` utilise son asset `RealTimeCombat.inputactions`
plutôt que le wrapper généré de `PlayerInputs`. `RealTimeCombatInput` active
seulement cette map dédiée pour réactions et palette. Le verrouillage passe
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

La source des actions runtime est maintenant `PlayerInputs.inputactions` : sa
map `RealTimeCombat` reprend les actions du prototype avec le layout gamepad
final (South garde/contre, East esquive, West attaque, North saut, LT roue, RT skill de
lumiere et D-pad bas changement de cible). `RealTimeCombatInput` place un
contexte `Combat` exclusif pendant son activation; `LocalPlayerInput` laisse
passer mouvement, camera et lock, mais ne transmet plus les actions monde
concurrentes (interaction, retour, inventaire, torche, Munin, loot ou multi).
`GamepadInputContextStack` est la couche de migration des futurs contextes UI,
placement et cinematique. Elle est purgee a chaque reset de session.

La map `RealTimeCombat` lie `Counter` a `Gamepad/buttonSouth` (`Space`) et
`Dodge` a `Gamepad/buttonEast` (`LeftAlt`). `SouthButton` maintient une garde et
peut ouvrir la roue de CounterSkill lors d'une fenetre parfaite; cette roue
consomme stick droit, South pour confirmer et East pour annuler. `NorthButton`
reste `Jump`. Les directions d'esquive viennent du vecteur monde deja calcule
par UCC; sans stick, l'esquive part vers l'arriere. Pendant un lock, la locomotion libre conserve
l'orientation de Lucian vers son mouvement; seules les actions engagees le
reorientent vers `EnemyLockPoint`.

Pour une attaque ennemie temps reel, les icones South/East/North ne proviennent
plus de `ShowInput` pendant les clips de combat. Le prompt world-space unique
lit les reactions acceptees par le `SkillSO`, les montre attenuees pendant la
menace, puis nettes pendant la fenetre ouverte. South commence une garde a tout
moment; seule une pression dans une fenetre qui accepte `Counter` ouvre la roue
de CounterSkill. East conserve l'esquive et North le saut, qui restent
enregistres comme reactions uniquement lorsque la fenetre Animation Event est
ouverte.

Les actions de combat font face a l'ennemi verrouille. Pour `Dodge`, un stick
gauche non nul a priorite : Lucian s'oriente vers la direction voulue et roule
dans celle-ci. Sans direction, il reste face a la cible et roule vers l'arriere.

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

- Modifier l’asset `.inputactions`, jamais le wrapper C# généré.
- Un input consommable (`Interact`, `TriggerMunin`) ne doit être traité qu’une fois.
- `InputFocusStack` est une pile : seul le propriétaire au sommet détient le focus.
- Le debounce utilise `Time.unscaledTime`; son état statique doit être remis à zéro
  à chaque session Play.
- Les cinématiques utilisent des verrous externes UCC; toujours restaurer le
  contrôle lors d’un abort ou `OnDisable`.
