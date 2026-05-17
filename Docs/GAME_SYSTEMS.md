# Lit - Systèmes de jeu

Ce document explique les systèmes principaux du projet pour aider un développeur débutant à savoir où regarder.

## Joueur, groupe et personnages

**Rôle** : gérer les personnages contrôlables, leur état, leurs interactions et leur affichage dans la squad.

**Fichiers principaux** :

- `Assets/Scripts/SquadManager.cs`
- `Assets/Scripts/SquadCharacterController.cs`
- `Assets/Scripts/SquadCharacterController.Health.cs`
- `Assets/Scripts/SquadCharacterController.Lockpick.cs`
- `Assets/Scripts/Movement/SquadCharacterController.*.cs`
- `Assets/Scripts/SquadFollowerAgent.cs`
- `Assets/ScriptableObjects/CharacterData/CharacterData.cs`

**Fonctionnement général** :

`SquadManager` connaît les personnages disponibles. `SquadCharacterController` porte le comportement runtime d'un personnage. Les fichiers partiels dans `Movement/` séparent des blocs spécialisés comme vol, saut, probing de surface et interactions.

**Points d'attention** :

- Ne pas renommer les champs sérialisés du controller sans migration.
- Tester les déplacements dans `Maison.unity` et `StarterMotorTest.unity`.
- Les personnages peuvent aussi être pris en charge par le Netcode.

## Input

**Rôle** : convertir les actions clavier/manette en intentions de jeu.

**Fichiers principaux** :

- `Assets/PlayerInputs.cs` : auto-généré, ne pas modifier.
- `Assets/Scripts/Netcode/LocalPlayerInput.cs`
- `Assets/Scripts/Netcode/LocalInputRouter.cs`
- `Assets/Scripts/InputFocusStack.cs`
- scripts de menu sous `Assets/Scripts/Menu/`

**Fonctionnement général** :

Unity génère `PlayerInputs.cs` depuis `Assets/PlayerInputs.inputactions`. Les scripts projet consomment ensuite ces actions et bloquent ou redirigent l'input quand une UI est ouverte.

**Points d'attention** :

- Modifier l'asset `.inputactions` dans Unity plutôt que le C# généré.
- Vérifier les menus, l'inventaire et le contrôle personnage après un changement d'input.

## Caméra

**Rôle** : suivre le personnage, gérer les plans spéciaux et certains effets de vitesse ou combat.

**Fichiers principaux** :

- `Assets/Scripts/CameraController.cs`
- `Assets/Scripts/CrpgCameraCollision.cs`
- `Assets/Scripts/CrpgCameraFocus.cs`
- `Assets/Scripts/CrpgCameraInput.cs`
- `Assets/Scripts/FixedCameraPointTrigger.cs`
- `Assets/Scripts/FallSpeedCameraEffect.cs`
- `Assets/Scripts/RunSpeedCameraEffect.cs`
- `Assets/Scripts/RunSpeedPeripheralBlur.cs`

**Points d'attention** :

- La caméra dépend souvent du personnage local et de l'état de gameplay.
- Les réglages sont sensibles au ressenti joueur.
- Tester les collisions caméra, les zones fixes et les transitions combat.

## Interactions monde

**Rôle** : permettre au joueur d'utiliser des objets de scène.

**Fichiers principaux** :

- `Assets/Scripts/CharacterInteractionDetection.cs`
- `Assets/Scripts/InteractableItem.cs`
- `Assets/Scripts/Door.cs`
- `Assets/Scripts/Lever.cs`
- `Assets/Scripts/Brasero.cs`
- `Assets/Scripts/LadderInteractable.cs`
- `Assets/Scripts/LadderController.cs`
- `Assets/Scripts/TrouEtroit.cs`
- `Assets/Scripts/TwoLeverPuzzle.cs`

**Fonctionnement général** :

Le personnage détecte un interactable proche, puis appelle le script associé. Certains scripts sont aussi reliés au Netcode ou à la persistance.

**Points d'attention** :

- Une interaction peut être locale, serveur, persistée ou seulement visuelle.
- Vérifier les prompts UI après modification.
- Ne pas changer les noms de méthodes publiques utilisées par UnityEvents.

## Inventaire, items et crafting

**Rôle** : stocker, afficher, utiliser, dropper et crafter des items.

**Fichiers principaux** :

- `Assets/ScriptableObjects/Item/Item.cs`
- `Assets/Scripts/InventoryPanelController.cs`
- `Assets/Scripts/InventoryUISettings.cs`
- `Assets/Scripts/Netcode/NetworkInventory.cs`
- `Assets/Scripts/ItemPassiveEffectSystem.cs`
- `Assets/Scripts/Effect.cs`
- `Assets/Scripts/*Effect.cs`
- `Assets/Scripts/CraftingConstructionPanel.cs`
- `Assets/Scripts/BuilderController.cs`

**Fonctionnement général** :

Les items sont des ScriptableObjects. Les effets d'items sont souvent des classes dérivées de `Effect`. L'inventaire réseau valide les actions importantes côté serveur quand Netcode est actif.

**Points d'attention** :

- Un item peut être utilisé par l'inventaire, le monde, le build, le combat ou la sauvegarde.
- Tester local et host/client après une modification.
- Ne pas changer `itemId` sur les assets existants.

## UI et menus

**Rôle** : afficher les menus, panneaux, confirmations, inventaire, squad, loot, feedbacks et clavier virtuel.

**Fichiers principaux** :

- `Assets/Scripts/Menu/*`
- `Assets/Scripts/InventoryPanelController.cs`
- `Assets/Scripts/SquadUISettings.cs`
- `Assets/Scripts/LootUISettings.cs`
- `Assets/Scripts/ConfirmationManager.cs`
- `Assets/Scripts/ConfirmationBox.cs`
- `Assets/Scripts/InfoBoxUI.cs`
- `Assets/Scripts/PausePanelController.cs`

**Fonctionnement général** :

La UI peut être placée en scène, instanciée depuis prefab, ou créée en fallback runtime. Les menus utilisent des curseurs et handlers spécifiques pour manette/clavier.

**Points d'attention** :

- Beaucoup de références UI sont assignées dans l'inspecteur.
- Tester avec souris, clavier et manette si possible.
- Ne pas modifier la hiérarchie UI sans vérifier les scripts qui cherchent des enfants par index ou nom.

## Sauvegarde et sessions

**Rôle** : créer, charger et restaurer l'état d'une partie.

**Fichiers principaux** :

- `Assets/Scripts/Menu/SaveSessionManager.cs`
- `Assets/Scripts/CharacterStateStore.cs`
- `Assets/Scripts/CharacterSaveData.cs`
- `Assets/Scripts/Netcode/Persistence/*`

**Fonctionnement général** :

Le menu gère les sessions. Le monde persistant utilise des snapshots pour reconstruire les objets, états et variables dérivées.

**Points d'attention** :

- C'est une zone à risque élevé.
- Ne jamais changer un ID stable sans migration.
- Tester nouvelle partie, sauvegarde, chargement, host et client tardif.

## Netcode multijoueur

**Rôle** : permettre host/client, attribution des personnages, synchronisation d'inventaire et interactions serveur.

**Fichiers principaux** :

- `Assets/Scripts/Netcode/NetcodeBootstrap.cs`
- `Assets/Scripts/Netcode/NetcodeLauncher.cs`
- `Assets/Scripts/Netcode/NetcodePlayerSpawner.cs`
- `Assets/Scripts/Netcode/WorldInteractionService.cs`
- `Assets/Scripts/Netcode/NetworkInventory.cs`
- `Assets/Scripts/Netcode/Persistence/*`

**Fonctionnement général** :

Le host est autoritaire. Les clients envoient des intentions. Le serveur valide et synchronise. La persistance gère aussi la reconstruction pour late join.

**Points d'attention** :

- Ne pas faire confiance au client pour modifier l'état important.
- Vérifier les RPC après changement de signature.
- Tester au moins un host et un client.

## Combat

**Rôle** : fournir un combat tour par tour ponctuel.

**Fichiers principaux** :

- `Assets/Scripts/Combat/*`
- `Assets/Scripts/CameraController.cs`
- `Assets/Scripts/InventoryPanelController.cs`
- `Assets/Scripts/Netcode/NetworkInventory.cs`
- `Assets/ScriptableObjects/CharacterData/CharacterData.cs`

**Fonctionnement général** :

`CombatAggroEnemy` déclenche une session. `CombatSessionManager` orchestre les tours, le déplacement en arène, les actions et le retour. `CombatHudController` affiche l'état.

**Points d'attention** :

- Le combat est conservé mais secondaire.
- Tester victoire, défaite, inventaire en combat et prière d'idole.
- Voir [TurnBasedCombat.md](TurnBasedCombat.md).

## Audio

**Rôle** : jouer musiques, ambiances, sons d'action et voix.

**Fichiers principaux** :

- `Assets/Scripts/AudioManager.cs`
- `Assets/Scripts/AudioClipSO.cs`
- `Assets/Scripts/ActionAudioLibrarySO.cs`
- `Assets/Scripts/LocalVoiceLineController.cs`
- `Assets/Scripts/ZoneAudioProfileSO.cs`
- `Assets/Scripts/Zone.cs`

**Fonctionnement général** :

Les sons sont souvent encapsulés dans des ScriptableObjects. Les zones peuvent pousser des profils audio. Certaines actions utilisent une librairie par défaut dans `Resources/Audio`.

**Points d'attention** :

- Ne pas déplacer les assets chargés via `Resources.Load` sans adapter le chemin.
- Tester les transitions de zone et les voix.

## Temps, strates et torche

**Rôle** : représenter les états temporels, les visions de torche et les objets visibles selon une période.

**Fichiers principaux** :

- `Assets/Scripts/BraseroTimeManager.cs`
- `Assets/Scripts/Brasero.cs`
- `Assets/Scripts/TimePeriodVisibility.cs`
- `Assets/Scripts/AgeTriggerZone.cs`
- `Assets/Scripts/TorchVisionSystem.cs`
- `Assets/Scripts/TorchVisionSensitive.cs`
- `Assets/Scripts/Temporal/*`

**Fonctionnement général** :

Le projet possède une couche historique par années/braseros et une couche plus récente par `TemporalAge`. Les visions de torche par couleur restent utiles comme lecture secondaire.

**Points d'attention** :

- Ne pas supprimer les systèmes existants tant que les scènes les référencent.
- Pour les nouveaux contenus temporels, préférer `TemporalZone` et `TemporalObject`.

## Readables, lore et données narratives

**Rôle** : afficher textes lisibles, fragments narratifs, registres et données de lignées.

**Fichiers principaux** :

- `Assets/ScriptableObjects/Item/Item.cs`
- `Assets/Scripts/ReadableSentencePuzzle.cs`
- `Assets/Scripts/ReadableSentencePuzzleUI.cs`
- `Assets/Scripts/ReadableContentRuntime.cs`
- `Assets/Scripts/NarrativeData/*`
- `Docs/NarrativeData.md`

**Points d'attention** :

- Les textes sont souvent dans des assets `Item`.
- Les nouvelles métadonnées sont optionnelles pour ne pas casser les items existants.

## Construction, bâtiments et décor

**Rôle** : placer, améliorer, afficher ou masquer des éléments construits/décoratifs.

**Fichiers principaux** :

- `Assets/Scripts/BuilderController.cs`
- `Assets/Scripts/BuildingInfoInteractable.cs`
- `Assets/Scripts/BuildingPanelController.cs`
- `Assets/Scripts/BuildingRuntimeState.cs`
- `Assets/Scripts/DecorCullable.cs`
- `Assets/Scripts/DecorCullingManager.cs`

**Points d'attention** :

- Le build est lié à l'inventaire et parfois au Netcode.
- Le décor peut avoir beaucoup d'instances en scène.

## Environnement et zones

**Rôle** : gérer zones, ambiance, états environnementaux, hub et maison.

**Fichiers principaux** :

- `Assets/Scripts/Zone.cs`
- `Assets/Scripts/Environment/*`
- `Assets/Scripts/Maison.cs`
- `Assets/Scripts/MaisonWaitingPoint.cs`
- `Assets/Scripts/HubZone.cs`
- `Assets/Scripts/HubRosterManager.cs`

**Points d'attention** :

- Les zones peuvent impacter audio, torche, IA et état de personnages.
- Tester les triggers en scène.

## Outils Editor

**Rôle** : automatiser des tâches dans l'éditeur Unity.

**Fichiers principaux** :

- `Assets/Editor/*`

**Points d'attention** :

- Ces scripts ne tournent pas en build.
- Ils peuvent modifier des scènes, prefabs ou assets quand on clique sur un menu.
- Lire le code avant d'utiliser un outil qui bake, installe ou répare des objets.
