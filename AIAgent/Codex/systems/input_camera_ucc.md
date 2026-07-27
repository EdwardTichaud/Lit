# Input, UCC et caméra

## Rôle

Centraliser les actions locales, les transmettre au bon personnage et adapter
la façade gameplay Lit à Opsive UCC et à sa caméra.

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

Comme le lock pilote directement la `Main Camera`, son composant conserve un
solveur d'obstacles visuels par `SphereCastNonAlloc` : il rapproche la camera
avant un decor bloquant tout en ignorant Lucian et la cible verrouillee. Le
masque, rayon, marge et distance minimale sont reglabes dans l'Inspector.
Il calcule aussi les bounds des renderers de Lucian afin de garder le personnage
entier dans une zone viewport configurable : le FOV augmente jusqu'a
`maximumLockedFieldOfView`, puis la camera recule si cette limite ne suffit pas.
La direction d'orbite joueur-vers-ennemi possede son propre lissage
`orbitDirectionSharpness`, distinct du lissage de position/rotation, et une
vitesse angulaire maximale `maximumOrbitDegreesPerSecond` pour absorber les
demi-tours brusques pendant un lock sans retournement instantane.

Le lock du combat temps reel commence une seule fois quand un ennemi entre dans
le `VisionField` de Lucian. Il se termine automatiquement hors de la distance de
desengagement; `Player/LeftShoulder` peut sinon le deverrouiller ou reverrouiller
manuellement a portee. `SwitchEnemyLock`, lie a `Gamepad/dpad/left`, change de
cible hors palette. `SquadManager` conserve le panneau d'escouade si aucun lock
manuel n'est possible. `RealTimeCombatInput` gere reactions, palette et
changement de cible. Les maps `Player` et `Camera` restent actives pour le
deplacement libre.

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
