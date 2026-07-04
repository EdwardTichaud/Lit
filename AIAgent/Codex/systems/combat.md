# Combat

## Rôle

Gérer les combats tour par tour en solo et Netcode, leur présentation et leur
résolution dans le monde.

## Classes principales

- `CombatAggroEnemy` : détection et création des définitions ennemies.
- `CombatSessionManager` : autorité, sessions, tours, actions et RPC.
- `CombatSessionState`, `CombatTurn`, `CombatRuntimeEnemy` : état runtime.
- `CombatHudController` : commandes et affichage local.
  Il orchestre aussi `CombatEngagedPanel`, `CombatScreenInfosPanel` et
  l'exclusivite de `CombatDefensePanel`.
- `CombatDefensePanelController` : affiche les 3 items defensifs assignes
  pendant la reaction ennemie.
- `CombatCameraPresentationController` : pilote camera cinematographique locale
  par phase de combat.
- `CombatAnimationEvents` : hooks Animation Event pour ralentir la presentation,
  deplacer l'attaquant, revenir a sa pose initiale et notifier l'impact.
- `TimeManager` : profils de temps et hit-stop locaux de presentation combat,
  sans `Time.timeScale`.
- `CombatTransitionController` : transition visuelle/audio.
- `CombatHealth` : santé persistante des ennemis de scène.

## Flux principaux

1. Un trigger d’aggro demande une session au manager.
2. L’autorité capture les positions de retour et construit les ennemis runtime.
3. Le joueur engagé est téléporté instantanément vers l’arène et verrouillé.
4. Le HUD joue une fois par session l'intro `CombatEngagedPanel_Trigger` sur
   `CombatEngagedPanel`, puis affiche `CombatScreenInfosPanel`.
5. Chaque tour commence par une courte phase de décision locale : HUD/focus et
   caméra se suspendent visuellement sans utiliser `Time.timeScale` global.
6. Pendant la décision ennemie, le joueur engagé dispose d'une réaction
   défensive locale : la présentation entre rapidement en ralenti dynamique sans
   `Time.timeScale`, l'inventaire peut s'ouvrir, et un item défensif choisi est
   validé puis résolu côté autorité. Le ralenti local reste actif pendant
   l'action ennemie pour rendre l'attaque lisible.
   `CombatDefensePanel` s'ouvre aussi pendant cette fenetre, masque les infos de
   combat, et ne propose que les 3 items defensifs assignes au personnage comme
   items combat.
7. Le manager alterne joueur puis ennemi, applique les intentions validées côté
   autorité et synchronise les clients.
8. La résolution restaure les positions, la caméra et le mouvement, puis applique
   le résultat à l’ennemi monde.

Pendant une session, la camera locale de combat est la seule source de pilotage
spatial de la `Main Camera`. `CombatCameraPresentationController`, cree par
`CombatSessionManager`, lit uniquement le contexte local du manager, suspend les
pilotes camera Opsive (`CameraController`, handler et binder), applique un plan
de camera par phase, puis restaure Opsive a la sortie. La phase `EnemyAction`
utilise un cadrage cinematographique proche du joueur, un FOV plus large, un
focus biaise vers l'ennemi et une respiration lente pour suivre l'attaque.

Pendant une attaque de mêlée, la présentation peut déplacer temporairement
l'attaquant vers sa cible puis le ramener à sa position de combat. Ce mouvement
reste cosmétique : l'impact est toujours appliqué par `CombatSessionManager` à
un timing autoritaire, et les attaques distance/support restent sur place. Les
attaques dont l'animation embarque déjà le déplacement ne reçoivent pas
d'approche scriptée supplémentaire.

Les ralentis de combat passent par des profils `TimeManager` locaux. Les
animations, UCC et mouvements scriptes de presentation lisent le meme
multiplicateur afin de rester synchronises avec la camera. Un hit-stop tres
court est declenche aux impacts joueur/ennemi pour accentuer le contact sans
modifier `Time.timeScale`.

Les clips peuvent aussi declencher des evenements via `CombatAnimationEvents`
pour controler finement le ralenti et une ruee cosmetique. Le composant resout
la victime depuis le contexte local de combat, capture la pose de depart au
moment de la ruee, restaure uniquement cette presentation et peut notifier
`NotifyCombatImpact` au frame d'impact. Les degats restent resolus une seule
fois par `CombatSessionManager`; le timer autoritaire reste le fallback si
l'evenement n'est pas declenche.
`SlowCombatTime` descend par defaut a `0.1` en entree rapide, suit les
`Animator`/UCC sous l'acteur et inclut aussi la victime de combat pour rendre
le ralenti visible sur les deux corps.
L'attaque Juggernaut `Griffe` est animation-driven : le manager force seulement
le lancement de `Attack_Griffe` et ne joue plus de saut, dash, audio ou VFX
specifiques pour cette attaque. Ces elements doivent etre places dans le clip.
Les anciens hooks autonomes `AnimationEventsManager` et `FirstStrikeEffect` du
systeme legacy ont ete retires pour eviter les appels accidentels a
`Time.timeScale`.

La musique de combat peut aussi être demandée localement par proximité d'un
`CombatAggroEnemy`, avant qu'une session tour par tour ne démarre réellement.
Cette demande reste cosmétique et utilise l'override musical de
`CombatAudioLibrary` exposée par `AudioManager`; elle est relâchée avec
hystérésis quand le joueur local sort assez loin du trigger d'aggro.

## Pièges observés

- `CombatSessionManager` coordonne plusieurs systèmes : limiter les changements.
- Ne pas confondre `CombatHealth` avec la santé du `SquadCharacterController`.
- Une action couverte de transition doit être exécutée même si la transition est
  interrompue.
- Le serveur est l’autorité en multijoueur; les clients gardent une présentation locale.
- Le ralenti/pause de combat est cosmétique et local au client engagé; ne pas
  utiliser `Time.timeScale` pour ce flux multijoueur.
- Les items défensifs de réaction ennemie sont choisis depuis l'inventaire local,
  mais l'absorption, la casse et la synchronisation d'inventaire restent côté
  autorité.
- Un item defensif doit etre assigne aux 3 items combat du personnage pour etre
  utilisable dans `CombatDefensePanel` ou via l'inventaire pendant cette reaction.
- Le joueur local doit être résolu via `LocalPlayerContext`; éviter les fallbacks
  arbitraires qui peuvent viser le mauvais personnage en Netcode.
- Tester victoire, défaite, déconnexion et destruction pendant une transition.
- Les approches d'attaque doivent toujours restaurer la position de combat en
  fin d'action ou lors d'un abort/despawn.
