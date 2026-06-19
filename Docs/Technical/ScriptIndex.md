# Lit — Carte du code

Cet index indique où chercher. Il ne tente plus de lister statiquement chaque
script : l'ancien index exhaustif était déjà obsolète et référençait des fichiers
supprimés ou déplacés.

## Recherche rapide

Lister le code principal du projet :

```bash
rg --files Assets/Scripts Assets/Editor Assets/ScriptableObjects \
  Assets/CameraSystem_Legacy -g '*.cs'
```

Trouver une classe ou une méthode :

```bash
rg -n "class NomDeClasse|NomDeMethode" \
  Assets/Scripts Assets/Editor Assets/ScriptableObjects Assets/CameraSystem_Legacy
```

Trouver les usages dans les scènes, prefabs et données :

```bash
rg -n "NomDeClasse|Assembly-CSharp::NomDeClasse" \
  Assets/Scenes Assets/Prefabs Assets/ScriptableObjects
```

Pour les classes UCC, chercher aussi dans :

```text
Packages/com.opsive.shared/
Packages/com.opsive.ultimatecharactercontroller/
```

## Joueur et locomotion

Façade gameplay :

- `Assets/Scripts/SquadManager.cs`
- `Assets/Scripts/SquadCharacterController.cs`
- `Assets/Scripts/SquadCharacterController.Health.cs`
- `Assets/Scripts/SquadCharacterController.Lockpick.cs`
- `Assets/Scripts/Movement/SquadCharacterController.Interactions.cs`
- `Assets/Scripts/Movement/SquadCharacterController.UccLocomotion.cs`

Intégration UCC :

- `Assets/Scripts/OpsiveIntegration/LitOpsiveLocomotionBridge.cs`
- `Assets/Scripts/OpsiveIntegration/LitOpsivePlayerInput.cs`
- `Assets/Scripts/OpsiveIntegration/LitOpsiveLookSource.cs`
- `Assets/Scripts/OpsiveIntegration/LitUccInteractionBridge.cs`
- `Assets/Scripts/OpsiveIntegration/LitUccDamageBridge.cs`
- `Assets/Scripts/OpsiveIntegration/LitUccFollowerBridge.cs`
- `Assets/Scripts/OpsiveIntegration/LitUccFlightAbility.cs`

Tests de locomotion :

- `Assets/Scenes/MovementLab.unity`
- `Assets/Scripts/MovementLabDoor.cs`
- `Assets/Scripts/MovementLabInteractable.cs`
- `Assets/Scripts/MovementLabMovingPlatform.cs`

## Input et contexte local

- `Assets/PlayerInputs.inputactions`
- `Assets/PlayerInputs.cs` — généré, ne pas modifier
- `Assets/Scripts/InputFocusStack.cs`
- `Assets/Scripts/Netcode/LocalInputRouter.cs`
- `Assets/Scripts/Netcode/LocalPlayerContext.cs`
- `Assets/Scripts/Netcode/LocalPlayerInput.cs`
- `Assets/Scripts/Netcode/LocalPlayerUtils.cs`
- `Assets/Scripts/Netcode/NetworkCharacterInput.cs`

## Caméra et visibilité du joueur

Caméra principale de `Maison` :

- UCC `CameraController` dans
  `Packages/com.opsive.ultimatecharactercontroller/Runtime/Camera/`
- `Assets/Scripts/OpsiveIntegration/LitUccCameraCharacterBinder.cs`

XRay et ancienne caméra :

- `Assets/CameraSystem_Legacy/Scripts/XRayVisibilityController.cs`
- `Assets/CameraSystem_Legacy/Scripts/XRayMaskFollower.cs`
- `Assets/CameraSystem_Legacy/Scripts/XRayCameraSync.cs`
- `Assets/CameraSystem_Legacy/Scripts/CameraController.cs`
- `Assets/CameraSystem_Legacy/Scripts/CrpgCameraCollision.cs`
- `Assets/CameraSystem_Legacy/Scripts/FixedCameraPointTrigger.cs`

Le nom `CameraSystem_Legacy` ne signifie pas que tout le dossier est inutilisé.
Vérifier les références de scène avant suppression.

## Interactions et monde

- `Assets/Scripts/CharacterInteractionDetection.cs`
- `Assets/Scripts/InteractableItem.cs`
- `Assets/Scripts/Door.cs`
- `Assets/Scripts/Lever.cs`
- `Assets/Scripts/Flame.cs`
- `Assets/Scripts/LadderInteractable.cs`
- `Assets/Scripts/LadderController.cs`
- `Assets/Scripts/PortalController.cs`
- `Assets/Scripts/TrouEtroit.cs`
- `Assets/Scripts/TwoLeverPuzzle.cs`
- `Assets/Scripts/WorldPickupUtility.cs`
- `Assets/Scripts/WorldPlacementUtility.cs`

## Inventaire et items

- `Assets/ScriptableObjects/Item/Item.cs`
- `Assets/Scripts/InventoryPanelController.cs`
- `Assets/Scripts/InventoryUISettings.cs`
- `Assets/Scripts/ItemPassiveEffectSystem.cs`
- `Assets/Scripts/Effect.cs`
- `Assets/Scripts/Netcode/NetworkInventory.cs`

### Building — legacy désactivé, ne pas utiliser pour de nouveaux contenus

- `Assets/Scripts/LegacyBuildingSystemSettings.cs`
- `Assets/Scripts/LegacyBuildingPersistenceMigration.cs`
- `Assets/Scripts/CraftingConstructionPanel.cs`
- `Assets/Scripts/BuilderController.cs`
- `Assets/Scripts/BuildingInfoInteractable.cs`
- `Assets/Scripts/BuildingPanelController.cs`
- `Assets/Scripts/BuildingRuntimeState.cs`

Activation centralisée par
`Assets/Resources/LegacyBuildingSystemSettings.asset`. Les anciennes données de
sauvegarde et de Netcode restent compatibles mais dormantes.

## Temps, Flames et dissolve

- `Assets/Scripts/Temporal/AgeManager.cs`
- `Assets/Scripts/Temporal/TemporalAge.cs`
- `Assets/Scripts/Temporal/TemporalZone.cs`
- `Assets/Scripts/Temporal/TemporalObject.cs`
- `Assets/Scripts/Temporal/HumanModificationTag.cs`
- `Assets/Scripts/GlobalAgeZone.cs`
- `Assets/Scripts/TimePeriodVisibility.cs`
- `Assets/Scripts/TimePeriodValueMode.cs`
- `Assets/Scripts/AncientFlameDisplayManager.cs`
- `Assets/Scripts/AncientFlameVolumeByYear.cs`
- `Assets/Scripts/AncientFlameYearDisplay.cs`
- `Assets/Scripts/DissolveRevealSystem.cs`
- `Assets/Scripts/DissolveRevealTarget.cs`
- `Assets/Scripts/FlameLightReceiver.cs`

## Narration, registres et fantômes

Connaissances et readables :

- `Assets/Scripts/KnowledgeManager.cs`
- `Assets/Scripts/KnowledgeTypes.cs`
- `Assets/Scripts/KnowledgeUnlockTrigger.cs`
- `Assets/Scripts/ReadableContentRuntime.cs`
- `Assets/Scripts/DistrictRegistryReadable.cs`

Données :

- `Assets/ScriptableObjects/Knowledge/KnowledgeSO.cs`
- `Assets/Scripts/NarrativeData/DistrictRegistry.cs`
- `Assets/Scripts/NarrativeData/ResidentRecord.cs`
- `Assets/Scripts/NarrativeData/FamilyRecord.cs`
- `Assets/Scripts/NarrativeData/LineageRecord.cs`
- `Assets/Scripts/NarrativeData/RegistryEntry.cs`
- `Assets/Scripts/NarrativeData/TemporalReadableMetadata.cs`
- `Assets/Scripts/NarrativeData/TransgenerationalObjectRecord.cs`
- `Assets/Scripts/NarrativeData/GhostData.cs`

Fantômes :

- `Assets/Scripts/GhostController.cs`
- `Assets/Scripts/VisualEffects/GhostDissolveController.cs`

## Sauvegarde, Netcode et persistance

Session et personnages :

- `Assets/Scripts/Menu/SaveSessionManager.cs`
- `Assets/Scripts/CharacterStateStore.cs`
- `Assets/Scripts/CharacterSaveData.cs`

Netcode :

- `Assets/Scripts/Netcode/NetcodeBootstrap.cs`
- `Assets/Scripts/Netcode/NetcodeLauncher.cs`
- `Assets/Scripts/Netcode/NetcodePlayerSpawner.cs`
- `Assets/Scripts/Netcode/NetcodePlayerAssignment.cs`
- `Assets/Scripts/Netcode/WorldInteractionService.cs`
- `Assets/Scripts/Netcode/NetcodePrefabRegistry.cs`

Monde persistant :

- `Assets/Scripts/Netcode/Persistence/PersistentNetworkObject.cs`
- `Assets/Scripts/Netcode/Persistence/IPersistentStateProvider.cs`
- `Assets/Scripts/Netcode/Persistence/NetworkObjectRegistry.cs`
- `Assets/Scripts/Netcode/Persistence/WorldStateManager.cs`
- `Assets/Scripts/Netcode/Persistence/SnapshotSerializer.cs`
- `Assets/Scripts/Netcode/Persistence/JoinSyncSystem.cs`
- `Assets/Scripts/Netcode/Persistence/SpawnManager.cs`
- `Assets/Scripts/Netcode/Persistence/WorldRulesStateManager.cs`
- `Assets/Scripts/Netcode/Persistence/WorldSaveAdapter.cs`
- `Assets/Scripts/Netcode/Persistence/PersistentWorldSceneInstaller.cs`
- `Assets/Scripts/Netcode/Persistence/PersistentGhostState.cs`

## Combat

- `Assets/Scripts/Combat/CombatAggroEnemy.cs`
- `Assets/Scripts/Combat/CombatEnemyDefinition.cs`
- `Assets/Scripts/Combat/CombatHealth.cs`
- `Assets/Scripts/Combat/CombatHudController.cs`
- `Assets/Scripts/Combat/CombatRuntimeEnemy.cs`
- `Assets/Scripts/Combat/CombatSessionManager.cs`
- `Assets/Scripts/Combat/CombatSessionState.cs`
- `Assets/Scripts/Combat/CombatTransitionController.cs`
- `Assets/Scripts/Combat/IustiaIdolPrayer.cs`

## UI et menus

- `Assets/Scripts/Menu/`
- `Assets/Scripts/ConfirmationManager.cs`
- `Assets/Scripts/InfoBoxUI.cs`
- `Assets/Scripts/PausePanelController.cs`
- `Assets/Scripts/RuntimeOutlineSelectionManager.cs`
- `Assets/Scripts/RuntimeOutlineTarget.cs`
- `Assets/Scripts/RuntimeOutlineUtility.cs`

Menu principal :

- `Assets/Scripts/Menu/MainMenuController.cs`
- `Assets/Scripts/Menu/MainMenuPointerCursor.cs`
- `Assets/Scripts/Menu/MainMenuTitleDecorController.cs`
- `Assets/Scripts/Menu/SaveSessionManager.cs`

## Environnement et rendu

- `Assets/Scripts/Environment/`
- `Assets/Scripts/Zone.cs`
- `Assets/Scripts/ZoneAudioProfileSO.cs`
- `Assets/Scripts/SnowRuntimeEffects.cs`
- `Assets/Scripts/SceneLightOcclusionEnforcer.cs`
- `Assets/Scripts/VisibilityOptimization/`

Munin :

- `Assets/Scripts/MuninController.cs`
- `Assets/Scripts/MuninUI.cs`
- `Assets/Scripts/MuninChargeReward.cs`
- `Assets/Scripts/MemoryShard.cs`
- `Assets/Scripts/PacifiedMemoryReward.cs`
- `Assets/Scripts/VigilAltar.cs`
- `Assets/Scripts/Netcode/Persistence/PersistentMuninChargeRewardState.cs`
- `Assets/Editor/MuninMemoryChargeSetup.cs`

## Outils Editor

Les outils projet sont sous `Assets/Editor/`. Points d'entrée notables :

- `Assets/Editor/LitOpsiveUccMigrationUtility.cs`
- `Assets/Editor/LitPerformanceAuditUtility.cs`
- `Assets/Editor/VisibilityOptimizationTools.cs`
- `Assets/Editor/MainMenuTitleSceneInstaller.cs`
- `Assets/Editor/LucianCC5HqIntegration.cs`
- `Assets/Editor/DistrictRegistryEditorTools.cs`
- `Assets/Editor/SceneHierarchyOrganizer.cs`

Lire l'outil avant d'exécuter un menu qui modifie scènes, prefabs ou assets.

## Règle de maintenance

Cette carte doit rester courte et orientée vers les points d'entrée. Pour ajouter
un nouveau système :

1. ajouter son dossier ou ses deux à cinq fichiers centraux ;
2. ne pas recopier automatiquement chaque helper ;
3. vérifier les chemins avec le système de fichiers ;
4. conserver les détails de fonctionnement dans
   [Architecture.md](Architecture.md).
