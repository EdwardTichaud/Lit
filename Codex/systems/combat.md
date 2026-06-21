# Combat

## Rôle

Gérer les combats tour par tour en solo et Netcode, leur présentation et leur
résolution dans le monde.

## Classes principales

- `CombatAggroEnemy` : détection et création des définitions ennemies.
- `CombatSessionManager` : autorité, sessions, tours, actions et RPC.
- `CombatSessionState`, `CombatTurn`, `CombatRuntimeEnemy` : état runtime.
- `CombatHudController` : commandes et affichage local.
- `CombatTransitionController` : transition visuelle/audio.
- `CombatHealth` : santé persistante des ennemis de scène.

## Flux principaux

1. Un trigger d’aggro demande une session au manager.
2. L’autorité capture les positions de retour et construit les ennemis runtime.
3. La transition couvre le déplacement vers l’arène et le verrouillage du joueur.
4. Le manager alterne les tours, applique les actions et synchronise les clients.
5. La résolution restaure les positions, la caméra et le mouvement, puis applique
   le résultat à l’ennemi monde.

## Pièges observés

- `CombatSessionManager` coordonne plusieurs systèmes : limiter les changements.
- Ne pas confondre `CombatHealth` avec la santé du `SquadCharacterController`.
- Une action couverte de transition doit être exécutée même si la transition est
  interrompue.
- Le serveur est l’autorité en multijoueur; les clients gardent une présentation locale.
- Tester victoire, défaite, déconnexion et destruction pendant une transition.

