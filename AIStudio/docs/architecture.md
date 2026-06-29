# Architecture du projet

Projet Unity 6 utilisant Opsive Ultimate Character Controller, Unity Netcode for GameObjects et HDRP.

## Systèmes principaux

| Système | Responsabilité | Dépendances majeures |
|---|---|---|
| Session runtime | Création/chargement des slots, changement de scène, remise à zéro entre parties | `SaveSessionManager`, `GameplayRuntimeReset`, menu |
| Squad et personnages | Roster, personnage contrôlé, données runtime, inventaire, followers | `SquadManager`, `SquadCharacterController`, `CharacterData` |
| Input, UCC et caméra | Capture des actions, routage local, locomotion Opsive, liaison caméra | Input System, `LocalPlayerContext`, UCC |
| Interactions et inventaire | Détection des cibles, Outline, actions monde, loot et UI d’inventaire | physique Unity, input, squad, Netcode |
| Netcode | Démarrage réseau, attribution des personnages, ownership, commandes serveur | NGO, Unity Transport, squad |
| Persistance | Sauvegarde personnage et snapshot du monde, reconstruction, synchronisation late join | session active, Netcode, IDs persistants |
| Combat | Sessions tour par tour, présentation, HUD et résolution autoritaire | squad, Netcode, audio |
| Narration | Séquences, dialogues, connaissances, fantômes et contenus lisibles | sauvegarde, input, caméra, temps |
| Temps et environnement | Âge canonique, visibilité temporelle et profils HDRP locaux | flammes anciennes, personnage local, HDRP |

Fiches détaillées : `AIStudio/docs/systems/`.

## Flux principaux

### Démarrage d’une partie

`MainMenuController` sélectionne ou crée un slot via `SaveSessionManager`, appelle
`GameplayRuntimeReset.PrepareForGameplayStart`, puis charge la scène. Selon la
composition de la scène, `SquadManager` et `CharacterStateStore` reconstruisent
le roster et les états. `NetcodeBootstrap` existe avant le chargement des scènes
et prépare aussi les services réseau/persistants.

### Contrôle du joueur

`PlayerInputs` → `LocalPlayerInput` → `LocalInputRouter` →

- solo : `SquadManager` / `SquadCharacterController`;
- multijoueur : `NetworkCharacterInput` → serveur → `SquadCharacterController`.

`SquadCharacterController` délègue la locomotion à `LitOpsiveLocomotionBridge`.
`LocalPlayerContext` publie le personnage local aux caméras, à l’environnement,
aux interactions et aux séquences narratives.

### Interaction monde

`SquadCharacterController.Interactions` collecte les colliders proches,
`CharacterInteractionDetection` résout les composants
`ICharacterDetectedInteractable`, puis la cible retenue alimente
`RuntimeOutlineSelectionManager`. `Interact` tente d’abord
`ILocalInteractHandler`; les interactions réseau utilisent ensuite leurs RPC ou
`WorldInteractionService`.

### Sauvegarde et chargement

`SaveSessionManager` fournit le dossier du slot actif.
`CharacterStateStore` écrit l’état JSON des personnages et déclenche
`WorldSaveAdapter`. Celui-ci sérialise le `WorldSnapshot` produit par
`WorldStateManager`. Les objets persistants sont identifiés par
`PersistentNetworkObject` et délèguent leur état aux
`IPersistentStateProvider`.

### Synchronisation multijoueur

Le serveur attribue et spawn les personnages via `NetcodePlayerSpawner`.
`WorldInteractionService` publie les assignations. Pour un client entrant,
`JoinSyncSystem` transfère le snapshot du monde par blocs, bloque le gameplay,
applique le snapshot, rétablit le personnage local puis signale que le client
est prêt.

## Hypothèses à vérifier par scène

- Les managers exacts présents dans une scène dépendent des prefabs et installers
  de cette scène; plusieurs systèmes savent créer un fallback runtime.
- `AgeManager` est la source temporelle globale, tandis que `TemporalZone` peut
  imposer un âge local à ses objets explicitement enregistrés.
- Le Building legacy est conservé pour compatibilité de sauvegarde mais son
  gameplay est désactivé par défaut.
