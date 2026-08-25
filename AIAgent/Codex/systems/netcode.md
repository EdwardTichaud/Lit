# Netcode

## Rôle

Créer l’infrastructure NGO, connecter les joueurs, attribuer les personnages et
faire respecter l’autorité serveur.

## Classes principales

- `NetcodeBootstrap` : crée `NetworkManager`, transport et services persistants.
- `NetcodeLauncher` / `NetcodeConnectionApproval` : host/client et validation.
- `NetcodePlayerSpawner` : attribution, spawn et ownership des personnages.
- `WorldInteractionService` : assignations client-personnage et interactions monde.
- `NetworkCharacterInput` : commandes du propriétaire vers le serveur.
- `NetworkInventory` : réplication d’inventaire.
- `NetcodePrefabRegistry` : mapping des prefabs réseau.

## Flux principaux

1. `NetcodeBootstrap` s’installe avant les scènes.
2. Le launcher démarre host ou client avec Unity Transport.
3. Le serveur choisit un `CharacterData`, crée ou réutilise son instance et
   donne l’ownership au client.
4. `WorldInteractionService` publie l’assignation.
5. `NetcodeLocalPlayer` / les utilitaires mettent à jour `LocalPlayerContext`.
6. Les inputs du propriétaire sont envoyés au serveur, qui pilote le gameplay.
7. Pendant une exploration réseau, la Maison reste chargée en arrière-plan : un
   joueur qui rejoint tardivement y apparaît, puis rejoint le groupe par portail.
   Les destinations de portail réservent toujours l'index 0 au personnage
   principal. Elles sont memorisees avant le dechargement de la scene source
   puis appliquees apres le chargement de la destination; `ZoneSpawnPoint`
   reste reserve au spawn initial de Maison.

## Pièges observés

- `AutoSpawnPlayerPrefabClientSide` est désactivé : le spawner projet est maître.
- L’ownership NGO et l’assignation métier sont deux informations distinctes.
- Les mutations monde, inventaire et combat sont autoritaires serveur.
- Les objets de scène sont préparés par `NetcodeSceneObjectInstaller` à chaque
  chargement.
- Les objets sous `NetworkObject` ne doivent pas etre reparentes par des helpers
  `DontDestroyOnLoad` avant que le host/server n'ecoute; les objets reseau de
  scene restent dans leur hierarchie Netcode.
- Les systèmes de snapshot/persistance réseau vivent sous
  `Assets/Persistence/Save/World/`; `NetcodeBootstrap` les crée toujours au
  runtime quand l’option dédiée est active.
- Tester les changements en host et client distant, pas seulement en host local.
