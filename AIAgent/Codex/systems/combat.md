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
  quand un `AnimationEvent` de combat le demande, puis route `UseItem1/2/3`
  vers ces slots via l'ActionMap `Combat`.
- `CombatCameraPresentationController` : pilote camera cinematographique locale
  par phase de combat et expose le shot temporaire `CounterAction`.
- `CombatCounterItemPresentation` : presentation locale des items de contre
  configures par `Item.CombatReactionProfile`.
- `CombatAnimationEvents` : hooks Animation Event pour ralentir la presentation,
  ouvrir/fermer `CombatDefensePanel`, deplacer l'attaquant, revenir a sa pose
  initiale et notifier l'impact.
- `TimeManager` : multiplicateur local de presentation combat declenche par les
  `AnimationEvent`, sans `Time.timeScale`.
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
   défensive locale : l'inventaire peut s'ouvrir, et un item défensif choisi est
   validé puis résolu côté autorité. Le ralenti n'est pas declenche par cette
   phase; il doit venir des `AnimationEvent` places dans les clips d'attaque.
   Quand un `AnimationEvent` le demande, `CombatDefensePanel` devient le seul
   panel combat visible et ne propose que les 3 items defensifs assignes au
   personnage comme items combat. Pendant son affichage, l'ActionMap locale
   `Combat` remplace `Player`/`Camera`; `UseItem1`, `UseItem2` et `UseItem3`
   selectionnent les slots 1 a 3. Les slots sans item assigne restent masques.
   Les racines `EnableItem_1/2/3` sont resolues comme boutons UI pour permettre
   aussi la selection souris et la navigation manette/clavier.
   Cette demande issue d'un AnimationEvent est prioritaire sur l'intro
   `CombatEngagedPanel_Trigger` : si l'attaque ennemie commence pendant
   l'intro, celle-ci est masquee et `CombatDefensePanel` s'affiche
   immediatement.
   Quand le clip ferme ce panel,
   `CombatScreenInfosPanel` redevient visible.
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

Les ralentis de combat sont exclusivement declenches par les `AnimationEvent`
exposes par `CombatAnimationEvents`. La camera de combat, `CombatSessionManager`
et les impacts ne demarrent plus de ralenti ou de hit-stop automatiques. Les
animations, UCC et mouvements scriptes de presentation lisent le meme
multiplicateur quand un clip a lance `SlowCombatTime`.
Les entrees/sorties de ce ralenti jouent aussi les cues
`CombatTimeSlow`/`CombatTimeResume` de l'`ActionAudioLibrary` par defaut, via
des assets `AudioClipSO`.
Pendant ce meme ralenti, `TimeManager` demande aussi un leger ducking de la
musique via `AudioManager.BeginMusicDucking`, puis le relache au retour a la
vitesse normale.
L'affichage de `CombatDefensePanel` ne depend pas de l'etat du ralenti dans
`TimeManager` : il est demande explicitement par `CombatAnimationEvents`.

Un item peut porter un `CombatReactionProfile` optionnel. Les items de reaction
peuvent etre gardes dans les 3 items combat meme s'ils n'absorbent pas de
degats. Le premier type supporte est `MeleeCounterImpale` : si le joueur choisit
l'item avant l'impact d'une attaque ennemie melee, l'attaque est interrompue,
les animations joueur/ennemi configurees se jouent, le visuel de l'item passe de
l'attache joueur a l'attache ennemie, puis le shot camera `CounterAction`, le
ralenti local, les SFX, VFX et voix optionnelles du profil soulignent l'impact.
Contre une attaque non melee, ce profil ne remplace pas une defense.
Si le profil renseigne `enemyAnimationClip`, `CombatReactionClipPlayer` lit ce
clip directement via Playables sur l'Animator ennemi; sinon le manager retombe
sur la state ou le trigger `enemyAnimationName`, puis sur la duree fallback.
Au moment ou le visuel reste plante sur l'ennemi, son axe Z monde est force a
l'inverse du Z local de l'ennemi; seul le roll Z du profil sert encore
d'ajustement fin. Le Juggernaut fournit une state Animator `Impaled` vide pour
recevoir le clip d'empalement.

Les clips peuvent aussi declencher des evenements via `CombatAnimationEvents`
pour controler finement le ralenti, l'ouverture/fermeture de
`CombatDefensePanel` et une ruee cosmetique. Le composant resout la victime
depuis le contexte local de combat, capture la pose de depart au moment de la
ruee, restaure uniquement cette presentation et peut notifier
`NotifyCombatImpact` au frame d'impact. Les degats restent resolus une seule
fois par `CombatSessionManager`; le timer autoritaire reste le fallback si
l'evenement n'est pas declenche.
Les panels UI de combat reactivenent aussi leur hierarchie et corrigent une
echelle locale nulle sur les parents au moment de l'affichage, afin que
`CombatEngagedPanel`, `CombatScreenInfosPanel` et `CombatDefensePanel` restent
visibles meme si la racine `UI_Overlay` a ete sauvegardee a `localScale` zero.
`SlowCombatTime` descend par defaut a `0.1` en entree rapide, suit les
`Animator`/UCC sous l'acteur et inclut aussi la victime de combat pour rendre
le ralenti visible sur les deux corps. Les appels `SlowCombatTime` ouvrent
aussi `CombatDefensePanel`; `RestoreCombatTime` le ferme.
L'attaque Juggernaut `Griffe` est animation-driven : le manager force seulement
le lancement de `Attack_Griffe` et ne joue plus de saut, dash, audio ou VFX
specifiques pour cette attaque. Ces elements doivent etre places dans le clip.
Les anciens hooks autonomes `AnimationEventsManager` et `FirstStrikeEffect` du
systeme legacy ont ete retires pour eviter les appels accidentels a
`Time.timeScale`.

Le systeme de combat actuel ne doit pas dependre du legacy Symphonie importe
dans `Assets/Legacy/BattleManager_SymphonieImport`. Cet import reste isole dans
son assembly `BattleManager.SymphonieImport`, non auto-referencee, compilee
uniquement dans l'Editor et seulement si le define `SYMPHONIE_IMPORT_COMPILE`
est ajoute manuellement. Aucun code, scene ou asset de combat actuel ne doit
referencer ses types ou ses GUIDs afin que le dossier puisse etre supprime plus
tard.

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
- Le ralenti de combat est cosmétique, local au client engagé et
  animation-driven; ne pas utiliser `Time.timeScale` ni le declencher depuis le
  manager ou la camera.
- Les items défensifs de réaction ennemie sont choisis depuis l'inventaire local,
  mais l'absorption, la casse et la synchronisation d'inventaire restent côté
  autorité.
- Un item defensif doit etre assigne aux 3 items combat du personnage pour etre
  utilisable dans `CombatDefensePanel` et dans l'inventaire de reaction.
- Le joueur local doit être résolu via `LocalPlayerContext`; éviter les fallbacks
  arbitraires qui peuvent viser le mauvais personnage en Netcode.
- Tester victoire, défaite, déconnexion et destruction pendant une transition.
- Les approches d'attaque doivent toujours restaurer la position de combat en
  fin d'action ou lors d'un abort/despawn.
