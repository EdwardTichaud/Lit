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
  l'exclusivite de `CombatDefensePanel`, ainsi que l'ActionMap `Combat` pendant
  toute la session.
- `CombatDefensePanelController` : affiche les 3 items defensifs assignes
  quand un `AnimationEvent` de combat le demande, puis route `UseItem1/2/3`
  vers ces slots.
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
3. Si `combatEntryMidpointPrefab` est renseigne, le manager l'instancie a
   mi-chemin entre le joueur et l'ennemi, oriente vers l'ennemi, juste avant la
   teleportation d'entree en combat. En reseau, un RPC dedie demande cette
   presentation a tous les clients sans declencher le HUD/camera des joueurs
   non engages. L'instance est suivie par session et detruite quand la sortie
   manuelle du combat est validee apres l'ecran victoire/defaite; un retry la
   remplace par une nouvelle instance. Les `CharacterEffect` presents sur cette
   instance sont joues a l'apparition, stoppes a la sortie, puis detruits apres
   un delai par defaut de 2 secondes.
   Le joueur engage est ensuite teleporte instantanement vers l'arene et verrouille.
4. Le HUD ferme l'inventaire ouvert, prend le focus exclusif, active l'ActionMap
   locale `Combat` et joue une fois par session l'intro
   `CombatEngagedPanel_Trigger` sur `CombatEngagedPanel`, des l'entree en
   combat et sans attendre le premier snapshot, puis affiche
   `CombatScreenInfosPanel`.
5. Chaque tour commence par une courte phase de décision locale : HUD/focus et
   caméra se suspendent visuellement sans utiliser `Time.timeScale` global.
6. Pendant la décision ennemie, le joueur engagé dispose d'une réaction
   defensive locale : l'inventaire ne s'ouvre pas en combat, et un item choisi
   via `CombatDefensePanel` est valide puis resolu cote autorite. Le ralenti
   n'est pas declenche par cette phase; il doit venir des `AnimationEvent`
   places dans les clips d'attaque.
   Quand un `AnimationEvent` le demande, `CombatDefensePanel` devient le seul
   panel combat visible et ne propose que les 3 items defensifs assignes au
   personnage comme items combat. L'ActionMap locale `Combat` reste active
   pendant toute la session et le panel la force aussi quand il devient visible;
   `UseItem1`, `UseItem2` et `UseItem3` selectionnent les slots 1 a 3 quand le
   panel est visible. La fenetre defensive autorisee correspond a cet affichage
   reel : le joueur peut remplacer son choix jusqu'a ce que le panel se masque,
   et seul le dernier item choisi est resolu a l'impact. Les slots sans item
   assigne restent masques.
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
   Au debut d'une attaque ennemie normale, le joueur engage peut jouer une
   animation de preparation defensive configurable, avec fallback sur `Defense`
   puis `Block`, ainsi qu'une voix `AudioClipSO` optionnelle. Cette
   presentation est jouee localement chez le client proprietaire.
8. La resolution joue la mort du perdant puis un taunt du gagnant
   (`Taunt`, puis `Victory`/`Celebrate` en fallback si disponibles). Le HUD
   affiche ensuite le panel de scene `VictoryPanel` ou `DefeatPanel`; aucun
   panel de resultat n'est cree en runtime. La victoire garde une validation
   manuelle simple. En defaite, `CombatHudController` ne remplace pas le texte
   de `DefeatPanel` et route ses boutons de scene vers trois choix : retour
   `MainMenu`, retry immediat du combat courant, ou rechargement du dernier
   checkpoint/sauvegarde active. Le retry restaure le snapshot en memoire pris
   juste avant l'entree en combat : etat personnage/inventaire, snapshot monde
   persistant, PV joueur pre-combat et ennemis reconstruits depuis l'etat monde,
   puis relance la session sans la terminer. Les sorties menu et checkpoint
   terminent la session avant chargement de scene. En cas de defaite,
   la musique de combat est remplacee par la musique `Game Over` configuree
   dans `CombatAudioLibrary` jusqu'a cette sortie. Cette validation restaure
   alors les positions, la camera et le mouvement, puis applique le resultat a
   l'ennemi monde.

Pendant une session, la camera locale de combat est la seule source de pilotage
spatial de la `Main Camera`. `CombatCameraPresentationController`, cree par
`CombatSessionManager`, lit uniquement le contexte local du manager, suspend les
pilotes camera Opsive (`CameraController`, handler et binder), applique un plan
de camera par phase, puis restaure Opsive a la sortie. La phase `EnemyAction`
utilise un cadrage cinematographique proche du joueur, un FOV plus large, un
focus biaise vers l'ennemi et une respiration lente pour suivre l'attaque.
Chaque phase shot expose aussi une vitesse de deplacement locale de son offset :
une valeur X positive fait glisser la camera lateralement depuis son offset de
depart, Y la fait monter, et Z la pousse sur l'axe joueur-vers-ennemi pendant
la duree du shot.

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
Le type `MeleeDefense` sert aux objets comme `Item_Shield_WoodShield` : le
joueur sort le visuel de l'item en main, bloque une attaque melee sans subir de
degats, puis l'item perd des PV defensifs persistants portes par le personnage.
Si ses PV tombent a zero, une unite est retiree de l'inventaire; sinon elle peut
etre reutilisee lors des combats suivants avec ses PV restants. Quand plusieurs
boucliers identiques existent, le combat consomme d'abord une unite de la pile
la plus abimee.
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
fois par `CombatSessionManager`; il n'y a plus de timer fallback, donc un clip
d'attaque doit emettre `NotifyCombatImpact` pour appliquer l'impact.
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
La resolution de defaite empile un override musical `GameOverMusic` au-dessus
de cette musique de combat, puis le relache pendant la transition de sortie.
Les boutons de `DefeatPanel` sont resolus depuis la scene par nom ou texte
(`menu`, `retry`/`reessayer`, `checkpoint`), avec fallback sur l'ordre des
boutons enfants si necessaire; eviter de renommer ces boutons sans mettre a jour
les mots-cles ou les references serializees.
Le snapshot de retry est strictement runtime et n'ecrit aucun fichier : le
manager capture `CharacterStateStore.CaptureRuntimeState` et
`WorldStateManager.CaptureSnapshot` avant tout deplacement vers l'arene, puis
restaure ces donnees au bouton retry.

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
- Les PV restants des items defensifs sont un etat de personnage, pas un etat de
  session de combat; ne pas les stocker dans `CombatSession`.
- Un item defensif doit etre assigne aux 3 items combat du personnage pour etre
  utilisable dans `CombatDefensePanel` et dans l'inventaire de reaction.
- Le joueur local doit être résolu via `LocalPlayerContext`; éviter les fallbacks
  arbitraires qui peuvent viser le mauvais personnage en Netcode.
- Tester victoire, défaite, déconnexion et destruction pendant une transition.
- Les approches d'attaque doivent toujours restaurer la position de combat en
  fin d'action ou lors d'un abort/despawn.
