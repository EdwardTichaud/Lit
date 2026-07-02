# Combat

## Rôle

Gérer les combats tour par tour en solo et Netcode, leur présentation et leur
résolution dans le monde.

## Classes principales

- `CombatAggroEnemy` : détection et création des définitions ennemies.
- `CombatSessionManager` : autorité, sessions, tours, actions et RPC.
- `CombatSessionState`, `CombatTurn`, `CombatRuntimeEnemy` : état runtime.
- `CombatHudController` : commandes et affichage local.
- `CombatCameraPresentationController` : cadrage caméra local joueur/ennemi.
- `CombatTransitionController` : transition visuelle/audio.
- `CombatHealth` : santé persistante des ennemis de scène.

## Flux principaux

1. Un trigger d’aggro demande une session au manager.
2. L’autorité capture les positions de retour et construit les ennemis runtime.
3. Le joueur engagé est téléporté instantanément vers l’arène et verrouillé.
4. Chaque tour commence par une courte phase de décision locale : HUD/focus et
   caméra se suspendent visuellement sans utiliser `Time.timeScale` global.
5. Pendant la décision ennemie, le joueur engagé dispose d'une réaction
   défensive locale : la présentation ralentit sur 2 secondes sans
   `Time.timeScale`, l'inventaire peut s'ouvrir, et un item défensif choisi est
   validé puis résolu côté autorité.
6. Le manager alterne joueur puis ennemi, applique les intentions validées côté
   autorité et synchronise les clients.
7. La résolution restaure les positions, la caméra et le mouvement, puis applique
   le résultat à l’ennemi monde.

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
- Le joueur local doit être résolu via `LocalPlayerContext`; éviter les fallbacks
  arbitraires qui peuvent viser le mauvais personnage en Netcode.
- Tester victoire, défaite, déconnexion et destruction pendant une transition.
