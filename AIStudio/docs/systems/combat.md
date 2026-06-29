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
5. Le manager alterne joueur puis ennemi, applique les intentions validées côté
   autorité et synchronise les clients.
6. La résolution restaure les positions, la caméra et le mouvement, puis applique
   le résultat à l’ennemi monde.

## Pièges observés

- `CombatSessionManager` coordonne plusieurs systèmes : limiter les changements.
- Ne pas confondre `CombatHealth` avec la santé du `SquadCharacterController`.
- Une action couverte de transition doit être exécutée même si la transition est
  interrompue.
- Le serveur est l’autorité en multijoueur; les clients gardent une présentation locale.
- Le ralenti/pause de combat est cosmétique et local au client engagé; ne pas
  utiliser `Time.timeScale` pour ce flux multijoueur.
- Le joueur local doit être résolu via `LocalPlayerContext`; éviter les fallbacks
  arbitraires qui peuvent viser le mauvais personnage en Netcode.
- Tester victoire, défaite, déconnexion et destruction pendant une transition.
