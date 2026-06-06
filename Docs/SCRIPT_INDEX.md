# Lit - Index des scripts

Ce fichier sert de carte de navigation pour un développeur débutant. Il indexe les scripts C# propres au projet après exclusion des dossiers externes évidents.

Il ne remplace pas la lecture du code : le rôle, les dépendances et le risque sont des indications de maintenance, pas une preuve absolue.

## Méthode de tri

- Inclus : scripts sous `Assets/Scripts/`, `Assets/Editor/` et scripts de données sous `Assets/ScriptableObjects/`.
- Exclu : `Packages/`, `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`.
- Exclu : dossiers externes repérés sous `Assets/0 - UnityPackages/`, `Assets/TextMesh Pro/`, `Assets/Sketchfab For Unity/`, `Assets/TutorialInfo/`.
- Exclu : `Assets/PlayerInputs.cs`, car il est généré automatiquement par Unity Input System.
- Origine incertaine : scripts dans le projet mais probablement inspirés d un outil, d un asset externe ou d un prototype technique. Les lire avant modification.

## Légende

- **Risque faible** : donnée simple, enum, helper isolé, ou système récent peu couplé.
- **Risque moyen** : UI, interaction, outil Editor ou comportement avec dépendances de scène.
- **Risque élevé** : manager central, Netcode, sauvegarde, inventaire, personnage, combat ou système très référencé.
- **Scènes/prefabs probablement concernés** : estimation basée sur les références directes `Assembly-CSharp::Classe` dans scènes, prefabs et ScriptableObjects, ou sur le dossier du script.

## Scripts ignorés

| Groupe | Nombre | Raison |
|---|---:|---|
| `Assets/PlayerInputs.cs` | 1 | Auto-généré par Unity Input System. Ne pas modifier. |
| `Assets/0 - UnityPackages/` | 109 | Asset tiers, plugin, exemples ou package importé. |
| `Assets/Sketchfab For Unity/` | 11 | Asset tiers, plugin, exemples ou package importé. |
| `Assets/TextMesh Pro/` | 34 | Asset tiers, plugin, exemples ou package importé. |
| `Assets/TutorialInfo/` | 2 | Asset tiers, plugin, exemples ou package importé. |

## Scripts d origine incertaine

| Script | Classe | Pourquoi prudent | Risque |
|---|---|---|---|
| `Assets/Editor/FabHdrpMaterialRepair.cs` | `FabHdrpMaterialRepair` | Outil lie aux assets Fab/HDRP. Probablement support projet, mais touche un import externe. | élevé |
| `Assets/Editor/GlbToFbxConverterWindow.cs` | `GlbToFbxConverterWindow` | Outil d import GLB/FBX. Origine ou besoin exact a confirmer avant modification. | moyen |
| `Assets/Editor/StarterMotorTestSetupBuilder.cs` | `StarterMotorTestSetupBuilder` | Outil de test locomotion. Lie a StarterMotor, origine inspiree possible. | moyen |
| `Assets/Scripts/CrpgCameraCollision.cs` | `CrpgCameraCollision` | Camera CRPG generique; origine a confirmer avant refonte. | moyen |
| `Assets/Scripts/CrpgCameraFocus.cs` | `CrpgCameraFocus` | Camera CRPG generique; origine a confirmer avant refonte. | moyen |
| `Assets/Scripts/CrpgCameraInput.cs` | `CrpgCameraInput` | Camera CRPG generique; origine a confirmer avant refonte. | moyen |
| `Assets/Scripts/Movement/StarterInspiredThirdPersonMotor.cs` | `StarterInspiredThirdPersonMotor` | Moteur de mouvement inspire Starter Assets; traiter comme code projet adapte, mais prudent. | moyen |
| `Assets/Scripts/Movement/StarterMotorAnimatorDriver.cs` | `StarterMotorAnimatorDriver` | Driver d animation lie au moteur Starter-inspired; prudent. | moyen |
| `Assets/Scripts/Movement/StarterMotorLocalInputBridge.cs` | `StarterMotorLocalInputBridge` | Bridge input local pour moteur Starter-inspired; prudent. | moyen |

## Index des scripts projet

Nombre de scripts indexés : **251**.

| Script | Classe principale | Rôle | Dépendances | Scènes ou prefabs probablement concernés | Risque |
|---|---|---|---|---|---|
| `Assets/Editor/CharacterDataIdAssigner.cs` | `CharacterDataIdAssigner` | Outil Editor pour automatiser une tâche de production dans Unity. | Editor | Unity Editor uniquement | moyen |
| `Assets/Editor/CombatSceneUiInstaller.cs` | `CombatSceneUiInstaller` | Outil Editor pour automatiser une tâche de production dans Unity. | Editor, UI, Combat | Unity Editor uniquement | moyen |
| `Assets/Editor/DecorCullingTools.cs` | `DecorCullingTools` | Outil Editor pour automatiser une tâche de production dans Unity. | Editor, Netcode | Unity Editor uniquement | élevé |
| `Assets/Editor/FabHdrpMaterialRepair.cs *(origine incertaine)*` | `FabHdrpMaterialRepair` | Outil Editor pour automatiser une tâche de production dans Unity. | Editor, Persistence | Unity Editor uniquement | élevé |
| `Assets/Editor/GlbToFbxConverterWindow.cs *(origine incertaine)*` | `GlbToFbxConverterWindow` | Outil Editor pour automatiser une tâche de production dans Unity. | Editor, UI | Unity Editor uniquement | moyen |
| `Assets/Editor/HiddenRoomSceneInstaller.cs` | `HiddenRoomSceneInstaller` | Outil Editor pour automatiser une tâche de production dans Unity. | Editor | Unity Editor uniquement | moyen |
| `Assets/Editor/ItemSceneMarkerEditor.cs` | `ItemSceneMarkerEditor` | Outil Editor pour automatiser une tâche de production dans Unity. | Editor, Netcode, UI, Inventory, Physics | Unity Editor uniquement | élevé |
| `Assets/Editor/LevelIconSpriteSetup.cs` | `LevelIconSpriteSetup` | Outil Editor pour automatiser une tâche de production dans Unity. | Editor, UI, ScriptableObject | Unity Editor uniquement | moyen |
| `Assets/Editor/OcclusionCullingTools.cs` | `OcclusionCullingTools` | Outil Editor pour automatiser une tâche de production dans Unity. | Editor, Netcode, NavMesh, Physics | Unity Editor uniquement | élevé |
| `Assets/Editor/SaveTools.cs` | `SaveTools` | Outil Editor pour automatiser une tâche de production dans Unity. | Editor | Unity Editor uniquement | moyen |
| `Assets/Editor/SquadAIManagerEditor.cs` | `SquadAIManagerEditor` | Outil Editor pour automatiser une tâche de production dans Unity. | Editor, UI, NavMesh | Unity Editor uniquement | élevé |
| `Assets/Editor/StarterMotorTestSetupBuilder.cs *(origine incertaine)*` | `StarterMotorTestSetupBuilder` | Outil Editor pour automatiser une tâche de production dans Unity. | Editor, Physics | Unity Editor uniquement | moyen |
| `Assets/ScriptableObjects/CharacterData/CharacterData.cs` | `CharacterData` | Données de personnage utilisées par la squad, le combat et/ou le réseau. | ScriptableObject, Inventory | ScriptableObjects (6 fichiers) | faible |
| `Assets/ScriptableObjects/Item/Item.cs` | `Item` | Données d item, readable, construction ou ressource. | ScriptableObject, Inventory | ScriptableObjects (118 fichiers) | faible |
| `Assets/ScriptableObjects/Knowledge/KnowledgeSO.cs` | `KnowledgeSO` | Données de connaissance ou entrée narrative débloquable. | ScriptableObject | Assets ScriptableObject | faible |
| `Assets/ScriptableObjects/VoiceLine/VoiceLineData.cs` | `VoiceLineData` | Données de ligne vocale. | Audio, ScriptableObject | ScriptableObjects (Assets/ScriptableObjects/VoiceLine/VoiceLine_Builder_Que voulez-vous.asset, Assets/ScriptableObjects/VoiceLine/VoiceLine_HistoricStone_AuCommencement.asset) | faible |
| `Assets/Scripts/ActionAudioLibrarySO.cs` | `ActionAudioCue` | Définit des valeurs partagées utilisées par d autres scripts. | Audio, ScriptableObject, Inventory | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/DissolveRevealSystem.cs` | `DissolveRevealSystem` | Registre runtime des sources lumineuses capables de révéler des items par dissolve. | Rendering | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/DissolveRevealTarget.cs` | `DissolveRevealTarget` | Pilote `_DissolveAmount` sur les matériaux d'un item selon la proximité des sources lumineuses. | Rendering, Physics | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/MasterShaderDissolveController.cs` | `MasterShaderDissolveController` | Contrôleur optionnel de `_DissolveAmount` pour les matériaux du MasterShader via `MaterialPropertyBlock`. | Rendering | Ajouté runtime ou manuellement sur un objet à dissoudre | moyen |
| `Assets/Scripts/AudioClipSO.cs` | `AudioClipSO` | Audio, musique, ambiance ou voix. | Audio, ScriptableObject | ScriptableObjects (62 fichiers) | faible |
| `Assets/Scripts/AudioManager.cs` | `AudioManager` | Manager central qui coordonne un système runtime. | Audio | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/BeaconMarker.cs` | `BeaconMarker` | Comportement ou donnée spécifique au projet. | Physics | Prefabs (Assets/Prefabs/BeaconMarker.prefab) | moyen |
| `Assets/Scripts/Brasero.cs` | `Brasero` | Objet interactif de lumière/allumage. Seuls ceux dont `ancientBrasero` est actif modifient le temps. | Netcode, Input, Physics | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/BraseroAnimatorByYear.cs` | `BraseroAnimatorByYear` | Cible d'affichage Animator pilotee par BraseroDisplayManager. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/BraseroDisplayManager.cs` | `BraseroDisplayManager` | Diffuse l'etat canonique des braseros aux affichages UI, Animator et Volume. | Temporal, UI | Créé ou appelé runtime | élevé |
| `Assets/Scripts/BraseroRotationEffect.cs` | `BraseroRotationEffect` | Effet appliqué par un item, une action ou un état de gameplay. | Unity | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/BraseroVolumeByYear.cs` | `BraseroVolumeByYear` | Cible d'affichage Volume pilotee par BraseroDisplayManager. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/BraseroYearDisplay.cs` | `BraseroYearDisplay` | Cible d'affichage texte pilotee par BraseroDisplayManager. | UI | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/BuilderController.cs` | `BuilderController` | Contrôleur de comportement ou d interface. | Netcode, Input, Audio, Inventory, Physics | Prefabs (Assets/Prefabs/Character/Builder_Model_Trooper.prefab) | élevé |
| `Assets/Scripts/BuildingInfoInteractable.cs` | `BuildingInfoInteractable` | Interaction entre le joueur et un objet du monde. | Editor, UI, Input, Physics | Prefabs (Assets/Prefabs/Building_Brasier de l'Espoir_Model.prefab, Assets/Prefabs/Castle/Maison_Chest.prefab) | moyen |
| `Assets/Scripts/BuildingPanelController.cs` | `BuildingPanelController` | Contrôleur de comportement ou d interface. | Netcode, UI, Input, Audio, Inventory, Physics | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/BuildingRuntimeState.cs` | `BuildingRuntimeState` | Interface utilisateur ou feedback visuel. | ScriptableObject | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/CameraController.cs` | `CameraController` | Contrôleur de comportement ou d interface. | Combat | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/CameraLineOfSightObstructionDetector.cs` | `CameraLineOfSightObstructionDetector` | Détecte les murs entre caméra et personnage sans déplacer le rig caméra. | Physics, Camera | Ajouté runtime par `CameraController` ou manuellement sur le rig caméra | moyen |
| `Assets/Scripts/CameraObstacleFader.cs` | `CameraObstacleFader` | Applique/restaure le fade visuel des obstacles caméra via `MaterialPropertyBlock`. | Rendering | Ajouté runtime par `CameraController` ou manuellement sur le rig caméra | moyen |
| `Assets/Scripts/CameraObstructionVignetteController.cs` | `CameraObstructionVignetteController` | Pilote une vignette HDRP runtime quand un obstacle masque le joueur. | HDRP, Volume | Ajouté runtime par `CameraController` ou manuellement sur le rig caméra | moyen |
| `Assets/Scripts/CastleRoamingMonster.cs` | `CastleRoamingMonster` | Comportement ou donnée spécifique au projet. | Netcode, Persistence, NavMesh, Physics | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/CatalyseurOrbCraftEffect.cs` | `CatalyseurOrbCraftEffect` | Effet appliqué par un item, une action ou un état de gameplay. | ScriptableObject | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/CatalyseurPanelController.cs` | `CatalyseurPanelController` | Contrôleur de comportement ou d interface. | Netcode, UI, Input, Audio, Inventory | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/Character.cs` | `Character` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/CharacterInfo.cs` | `CharacterInfo` | Comportement ou donnée spécifique au projet. | Unity | Scènes/Prefabs (3 fichiers) | moyen |
| `Assets/Scripts/CharacterInteractionDetection.cs` | `ICharacterDetectedInteractable` | Interaction entre le joueur et un objet du monde. | Physics | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/CharacterLayerCollisionBootstrap.cs` | `CharacterLayerCollisionBootstrap` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/CharacterSaveData.cs` | `CharacterSaveData` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/CharacterStateStore.cs` | `CharacterStateStore` | Comportement ou donnée spécifique au projet. | Netcode, Persistence, Inventory | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/Combat/CombatAggroEnemy.cs` | `CombatAggroEnemy` | Système de combat tour par tour et ses données runtime. | Netcode, Combat, Physics | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/Combat/CombatEnemyDefinition.cs` | `CombatEnemyDefinition` | Système de combat tour par tour et ses données runtime. | Combat | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Combat/CombatHealth.cs` | `CombatHealth` | Système de combat tour par tour et ses données runtime. | Combat | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/Combat/CombatHudController.cs` | `CombatHudController` | Système de combat tour par tour et ses données runtime. | Persistence, UI, Input, Combat | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/Combat/CombatNetworkMessages.cs` | `CombatNetworkStrings` | Système de combat tour par tour et ses données runtime. | Netcode, Persistence, Combat | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/Combat/CombatRuntimeEnemy.cs` | `CombatRuntimeEnemy` | Système de combat tour par tour et ses données runtime. | Combat | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Combat/CombatSessionManager.cs` | `CombatSessionManager` | Système de combat tour par tour et ses données runtime. | Netcode, Persistence, Audio, Inventory, Combat, Physics | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/Combat/CombatSessionState.cs` | `CombatSessionPhase` | Définit des valeurs partagées utilisées par d autres scripts. | Persistence, Combat | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/Combat/CombatTransitionController.cs` | `CombatTransitionController` | Système de combat tour par tour et ses données runtime. | UI, Audio, Combat | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/Combat/CombatTurn.cs` | `CombatTurn` | Définit des valeurs partagées utilisées par d autres scripts. | Combat | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Combat/IustiaIdolPrayer.cs` | `IustiaIdolPrayer` | Système de combat tour par tour et ses données runtime. | Input, Combat, Physics | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Combat/RestoreHealthEffect.cs` | `RestoreHealthEffect` | Système de combat tour par tour et ses données runtime. | Audio, ScriptableObject, Combat | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/ConfirmationBox.cs` | `ConfirmationBox` | Interface utilisateur ou feedback visuel. | UI | Scènes (Assets/Scenes/MainMenu.unity, Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/ConfirmationManager.cs` | `ConfirmationManager` | Manager central qui coordonne un système runtime. | UI, Input | Scènes (Assets/Scenes/MainMenu.unity, Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/ConfirmationRequest.cs` | `ConfirmationRequest` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/CraftingConstructionPanel.cs` | `CraftingConstructionPanel` | Interface utilisateur ou feedback visuel. | Netcode, UI, Input, Audio, Inventory | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/CraftingSlotsPerLevelEffect.cs` | `CraftingSlotsPerLevelEffect` | Effet appliqué par un item, une action ou un état de gameplay. | ScriptableObject | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/CrpgCameraCollision.cs *(origine incertaine)*` | `CrpgCameraCollision` | Comportement ou donnée spécifique au projet. | Physics | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/CrpgCameraFocus.cs *(origine incertaine)*` | `CrpgCameraFocus` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/CrpgCameraInput.cs *(origine incertaine)*` | `CrpgCameraInput` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/CursorController.cs` | `CursorController` | Contrôleur de comportement ou d interface. | UI, Input, Audio | Scènes/Prefabs (4 fichiers) | élevé |
| `Assets/Scripts/DecorCullable.cs` | `DecorCullable` | Comportement ou donnée spécifique au projet. | Physics | Scènes/Prefabs (4 fichiers) | moyen |
| `Assets/Scripts/DecorCullingManager.cs` | `DecorCullingManager` | Manager central qui coordonne un système runtime. | Unity | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/DestructibleObject.cs` | `DestructibleObject` | Comportement ou donnée spécifique au projet. | Netcode, Input, Audio, Physics | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/Door.cs` | `Door` | Objet interactif de scène. | Netcode, UI, Input, Audio, Physics | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/EchoPassiveEffect.cs` | `EchoPassiveEffect` | Effet appliqué par un item, une action ou un état de gameplay. | Audio, ScriptableObject | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/Effect.cs` | `Effect` | Effet appliqué par un item, une action ou un état de gameplay. | ScriptableObject | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/EnemyInfo.cs` | `EnemyInfo` | Comportement ou donnée spécifique au projet. | Unity | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/Environment/EnvironmentManager.cs` | `EnvironmentManager` | Gestion d environnement, zones et état runtime associé. | ScriptableObject | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/Environment/EnvironmentRuntimeState.cs` | `EnvironmentRuntimeState` | Gestion d environnement, zones et état runtime associé. | ScriptableObject | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/Environment/EnvironmentZone.cs` | `EnvironmentZone` | Gestion d environnement, zones et état runtime associé. | Physics | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/FallSpeedCameraEffect.cs` | `FallSpeedCameraEffect` | Effet appliqué par un item, une action ou un état de gameplay. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/FixedCameraPointTrigger.cs` | `FixedCameraPointTrigger` | Comportement ou donnée spécifique au projet. | Physics | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/FollowTarget.cs` | `FollowTarget` | Comportement ou donnée spécifique au projet. | Unity | Scènes/Prefabs (3 fichiers) | moyen |
| `Assets/Scripts/GameplayRuntimeReset.cs` | `GameplayRuntimeReset` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/GhostController.cs` | `GhostController` | Lie un fantôme de scène à GhostData, propose des réactions selon les connaissances possédées et marque le souvenir compris. | ScriptableObject, UI, Input, Physics | Ajouté manuellement sur un GameObject de scène | moyen |
| `Assets/Scripts/GlobalAgeZone.cs` | `GlobalAgeZone` | Pont shader global optionnel, désactivé par défaut au profit d'AgeManager. | Unity | Scènes existantes | faible |
| `Assets/Scripts/HiddenRoom/HiddenRoomBootstrap.cs` | `HiddenRoomBootstrap` | Salle cachée, portail ou téléportation dédiée. | Physics | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/HiddenRoom/HiddenRoomPortalRenderer.cs` | `HiddenRoomPortalRenderer` | Salle cachée, portail ou téléportation dédiée. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/HiddenRoom/HiddenRoomPortalTeleporter.cs` | `HiddenRoomPortalTeleporter` | Salle cachée, portail ou téléportation dédiée. | Audio, Physics | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/HubCompanionSwapTrigger.cs` | `HubCompanionSwapTrigger` | Comportement ou donnée spécifique au projet. | Netcode, UI, Input, Physics | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/HubRosterManager.cs` | `HubRosterManager` | Manager central qui coordonne un système runtime. | Unity | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/HubZone.cs` | `HubZone` | Comportement ou donnée spécifique au projet. | Physics | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/ILeverTarget.cs` | `ILeverTarget` | Objet interactif de scène. | Unity | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/ImprovedCombustionEffect.cs` | `ImprovedCombustionEffect` | Effet appliqué par un item, une action ou un état de gameplay. | ScriptableObject | ScriptableObjects (Assets/ScriptableObjects/Effect/ImprovedCombustion.asset) | faible |
| `Assets/Scripts/ImprovedCombustionRuntime.cs` | `ImprovedCombustionRuntime` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/IncreaseTorchRemaining.cs` | `IncreaseTorchRemaining` | Comportement ou donnée spécifique au projet. | ScriptableObject | ScriptableObjects (Assets/ScriptableObjects/Effect/IncreaseTorchRemaining.asset) | faible |
| `Assets/Scripts/InfoBoxUI.cs` | `InfoBoxUI` | Interface utilisateur ou feedback visuel. | Persistence, UI | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/InputFocusStack.cs` | `ICameraInputPassthrough` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/InstantiateItemEffect.cs` | `InstantiateItemEffect` | Effet appliqué par un item, une action ou un état de gameplay. | ScriptableObject, Inventory | ScriptableObjects (Assets/ScriptableObjects/Effect/InstantiateOrbeBleu.asset) | faible |
| `Assets/Scripts/InteractableItem.cs` | `InteractableItem` | Interaction entre le joueur et un objet du monde. | Netcode, UI, Input, Audio, Inventory, Physics | Scènes/Prefabs (Assets/Prefabs/Castle/Maison_Chest.prefab, Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/InteractionCapability.cs` | `InteractionCapability` | Définit des valeurs partagées utilisées par d autres scripts. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/InventoryCraftEffect.cs` | `InventoryCraftEffect` | Effet appliqué par un item, une action ou un état de gameplay. | ScriptableObject, Inventory | ScriptableObjects (Assets/ScriptableObjects/Effect/InventoryCraft_Balise.asset, Assets/ScriptableObjects/Effect/InventoryCraft_BocalFerme.asset) | faible |
| `Assets/Scripts/InventoryPanelController.cs` | `InventoryPanelController` | Contrôleur de comportement ou d interface. | Netcode, UI, Input, Audio, Inventory, Combat | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/InventoryUISettings.cs` | `InventoryUISettings` | Interface utilisateur ou feedback visuel. | UI, Inventory | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/ItemPassiveEffectSystem.cs` | `ItemPassiveEffectSystem` | Effet appliqué par un item, une action ou un état de gameplay. | Inventory | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/ItemSceneMarker.cs` | `ItemSceneMarker` | Comportement ou donnée spécifique au projet. | Editor, ScriptableObject, Inventory, Physics | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/KnowledgeManager.cs` | `KnowledgeManager` | Manager central qui coordonne un système runtime. | UI, Audio | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/KnowledgeTypes.cs` | `KnowledgeCategory` | Vocabulaire et conditions réutilisables pour le système de connaissances. | ScriptableObject | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/KnowledgeUnlockTrigger.cs` | `KnowledgeUnlockTrigger` | Débloque des connaissances depuis un événement de scène, un trigger ou une observation. | ScriptableObject, Physics | Ajouté manuellement sur un GameObject de scène | moyen |
| `Assets/Scripts/LabyrinthStartTrigger.cs` | `LabyrinthStartTrigger` | Comportement ou donnée spécifique au projet. | Netcode, UI, Input, Audio, Physics | Scènes/Prefabs (Assets/Prefabs/Ray_v2.prefab, Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/LadderController.cs` | `LadderController` | Contrôleur de comportement ou d interface. | NavMesh, Physics | Scènes (Assets/Scenes/StarterMotorTest.unity) | élevé |
| `Assets/Scripts/LadderInteractable.cs` | `LadderInteractable` | Interaction entre le joueur et un objet du monde. | Netcode, UI, Input, Audio, Physics | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/LadderSceneInstaller.cs` | `LadderSceneInstaller` | Objet interactif de scène. | Physics | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Lever.cs` | `Lever` | Objet interactif de scène. | Netcode, Input, Audio, Physics | Scènes/Prefabs (Assets/Prefabs/Puzzle/Lever.prefab, Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/LeverPlayableDirectorTarget.cs` | `LeverPlayableDirectorTarget` | Objet interactif de scène. | Timeline | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/LightningPointLight.cs` | `LightningPointLight` | Comportement ou donnée spécifique au projet. | Unity | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/LoadingScreenService.cs` | `LoadingScreenService` | Comportement ou donnée spécifique au projet. | Persistence, UI | Scènes (Assets/Scenes/MainMenu.unity) | élevé |
| `Assets/Scripts/LocalBuildingInformationsPanelController.cs` | `LocalBuildingInformationsPanelController` | Contrôleur de comportement ou d interface. | UI | Prefabs (Assets/Prefabs/UI/LocalBuildingInformationsPanel.prefab, Assets/Prefabs/UI/LocalItemInformationsPanel.prefab) | élevé |
| `Assets/Scripts/LocalVoiceLineController.cs` | `LocalVoiceLineController` | Contrôleur de comportement ou d interface. | UI, Input, Audio, Physics | Scènes/Prefabs (3 fichiers) | élevé |
| `Assets/Scripts/LootUISettings.cs` | `LootUISettings` | Interface utilisateur ou feedback visuel. | UI | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/Maison.cs` | `Maison` | Comportement ou donnée spécifique au projet. | Unity | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/MaisonWaitingPoint.cs` | `MaisonWaitingPoint` | Comportement ou donnée spécifique au projet. | Unity | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/Menu/MainMenuBootstrap.cs` | `MainMenuBootstrap` | Menu principal, pause, navigation UI ou sauvegardes de session. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Menu/MainMenuController.cs` | `MainMenuController` | Menu principal, pause, navigation UI ou sauvegardes de session. | Netcode, UI, Input, Audio | Scènes (Assets/Scenes/MainMenu.unity) | élevé |
| `Assets/Scripts/Menu/MainMenuDisplayModeAction.cs` | `MainMenuDisplayModeAction` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI | Scènes (Assets/Scenes/MainMenu.unity) | moyen |
| `Assets/Scripts/Menu/MainMenuDisplaySettings.cs` | `MainMenuDisplaySettings` | Menu principal, pause, navigation UI ou sauvegardes de session. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Menu/MainMenuInputModeAction.cs` | `MainMenuInputModeAction` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Menu/MainMenuInputSettings.cs` | `MainMenuInputSettings` | Menu principal, pause, navigation UI ou sauvegardes de session. | Input | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Menu/MainMenuSaveEntryUI.cs` | `MainMenuSaveEntryUI` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI | Prefabs (Assets/Prefabs/SaveEntry.prefab) | moyen |
| `Assets/Scripts/Menu/MainMenuSessionEntryUI.cs` | `MainMenuSessionEntryUI` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI | Prefabs (Assets/Prefabs/SessionEntry.prefab) | moyen |
| `Assets/Scripts/Menu/MenuCursorAction.cs` | `MenuCursorAction` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI | Scènes/Prefabs (Assets/Prefabs/UI/VirtualKeyBoard/VirtualKeyboard.prefab, Assets/Scenes/MainMenu.unity) | moyen |
| `Assets/Scripts/Menu/MenuCursorButtonHandler.cs` | `MenuCursorButtonHandler` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/Menu/MenuCursorInputField.cs` | `MenuCursorInputField` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Menu/MenuCursorItem.cs` | `MenuCursorItem` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI, Inventory | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Menu/MenuCursorLink.cs` | `MenuCursorLink` | Menu principal, pause, navigation UI ou sauvegardes de session. | Unity | Scènes (Assets/Scenes/MainMenu.unity) | moyen |
| `Assets/Scripts/Menu/MenuCursorNavigator.cs` | `IMenuCursorHandler` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI, Input | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Menu/MenuCursorSyncUtility.cs` | `MenuCursorSyncUtility` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Menu/MenuInputFieldCaret.cs` | `MenuInputFieldCaret` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/Menu/PauseAudioOption.cs` | `PauseAudioOption` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI, Audio | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Menu/PauseCursorAction.cs` | `PauseCursorAction` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/Menu/SaveSessionManager.cs` | `SaveSessionManager` | Menu principal, pause, navigation UI ou sauvegardes de session. | Unity | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/Menu/VirtualKeyboardCursorController.cs` | `VirtualKeyboardCursorController` | Menu principal, pause, navigation UI ou sauvegardes de session. | UI, Input | Scènes (Assets/Scenes/MainMenu.unity) | élevé |
| `Assets/Scripts/Movement/SquadCharacterController.AnimationReconciliation.cs` | `SquadCharacterController` | Locomotion, animation, saut, vol ou probing du personnage. | Persistence | Prefabs (Assets/Prefabs/Character/Player_Model_Lucian.prefab) | élevé |
| `Assets/Scripts/Movement/SquadCharacterController.Flight.cs` | `SquadCharacterController` | Locomotion, animation, saut, vol ou probing du personnage. | Netcode, Audio, Physics | Prefabs (Assets/Prefabs/Character/Player_Model_Lucian.prefab) | élevé |
| `Assets/Scripts/Movement/SquadCharacterController.HeightProbeTraversal.cs` | `SquadCharacterController` | Locomotion, animation, saut, vol ou probing du personnage. | Physics | Prefabs (Assets/Prefabs/Character/Player_Model_Lucian.prefab) | élevé |
| `Assets/Scripts/Movement/SquadCharacterController.Interactions.cs` | `SquadCharacterController` | Locomotion, animation, saut, vol ou probing du personnage. | Physics | Prefabs (Assets/Prefabs/Character/Player_Model_Lucian.prefab) | élevé |
| `Assets/Scripts/Movement/SquadCharacterController.Jump.cs` | `SquadCharacterController` | Locomotion, animation, saut, vol ou probing du personnage. | Physics | Prefabs (Assets/Prefabs/Character/Player_Model_Lucian.prefab) | élevé |
| `Assets/Scripts/Movement/SquadCharacterController.SurfaceProbing.cs` | `SquadCharacterController` | Locomotion, animation, saut, vol ou probing du personnage. | Physics | Prefabs (Assets/Prefabs/Character/Player_Model_Lucian.prefab) | élevé |
| `Assets/Scripts/Movement/StarterInspiredThirdPersonMotor.cs *(origine incertaine)*` | `StarterInspiredThirdPersonMotor` | Locomotion, animation, saut, vol ou probing du personnage. | Physics | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Movement/StarterMotorAnimatorDriver.cs *(origine incertaine)*` | `StarterMotorAnimatorDriver` | Locomotion, animation, saut, vol ou probing du personnage. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Movement/StarterMotorLocalInputBridge.cs *(origine incertaine)*` | `StarterMotorLocalInputBridge` | Locomotion, animation, saut, vol ou probing du personnage. | Input | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/NarrativeData/FamilyRecord.cs` | `FamilyRecordStatus` | Définit des valeurs partagées utilisées par d autres scripts. | ScriptableObject, Temporal | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/NarrativeData/GhostData.cs` | `GhostData` | Données narratives de fantôme temporel, question, réactions Knowledge et indices. | ScriptableObject, Temporal | Assets ScriptableObject | faible |
| `Assets/Scripts/NarrativeData/LineageRecord.cs` | `LineageRecord` | Structure de données narrative : registres, lignées, objets transmis. | ScriptableObject | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/NarrativeData/RegistryEntry.cs` | `RegistryEntryType` | Définit des valeurs partagées utilisées par d autres scripts. | ScriptableObject, Temporal | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/NarrativeData/TemporalReadableMetadata.cs` | `ReligiousCurrent` | Définit des valeurs partagées utilisées par d autres scripts. | Temporal | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/NarrativeData/TransgenerationalObjectRecord.cs` | `TransgenerationalObjectRecord` | Structure de données narrative : registres, lignées, objets transmis. | ScriptableObject, Temporal | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/Netcode/ItemIdUtils.cs` | `ItemIdUtils` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, Inventory | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/ItemRegistry.cs` | `ItemRegistry` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, Inventory | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/LocalInputRouter.cs` | `LocalInputRouter` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, Input, Inventory | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/LocalPlayerContext.cs` | `LocalPlayerContext` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/LocalPlayerInput.cs` | `LocalPlayerInput` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, UI, Input, Inventory | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/LocalPlayerUtils.cs` | `LocalPlayerUtils` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetItemStack.cs` | `NetItemStack` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, Inventory | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeBootstrap.cs` | `NetcodeBootstrap` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeCharacterIdentity.cs` | `NetcodeCharacterIdentity` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, Inventory | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeClientIdentity.cs` | `NetcodeClientIdentity` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeConnectionApproval.cs` | `NetcodeConnectionApproval` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeLauncher.cs` | `NetcodeConnectionAttemptInfo` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, Persistence, Input | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeLobbyUI.cs` | `NetcodeLobbyUI` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, UI, Input | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeLocalPlayer.cs` | `NetcodeLocalPlayer` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodePlayerAssignment.cs` | `NetPlayerAssignment` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodePlayerSessionRegistry.cs` | `NetcodePlayerSessionRegistry` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodePlayerSpawner.cs` | `NetcodePlayerSpawner` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, Inventory | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodePlayerUtils.cs` | `NetcodePlayerUtils` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodePrefabRegistry.cs` | `NetcodePrefabRegistry` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, Inventory, Combat | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeRuntimeUtilities.cs` | `NetcodeRuntimeUtilities` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeSceneIdUtility.cs` | `NetcodeSceneIdUtility` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeSceneObjectInstaller.cs` | `NetcodeSceneObjectInstaller` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, Inventory | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeSessionCode.cs` | `NetcodeSessionCode` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeSessionEndpoint.cs` | `NetcodeSessionEndpoint` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeStableHash.cs` | `NetcodeStableHash` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetcodeTriggerRegistry.cs` | `NetcodeTriggerRegistry` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetworkCharacterInput.cs` | `NetworkCharacterInput` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, Input | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/NetworkInventory.cs` | `NetworkInventory` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode, Audio, Inventory, Combat | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/IPersistentStateProvider.cs` | `IPersistentStateProvider` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/JoinSyncSystem.cs` | `JoinSyncSystem` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/NetworkObjectRegistry.cs` | `NetworkObjectRegistry` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/PersistenceModels.cs` | `PersistentObjectKind` | Définit des valeurs partagées utilisées par d autres scripts. | Netcode, Persistence | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/PersistentGameplayStateProviders.cs` | `PersistentContainerState` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/PersistentGhostState.cs` | `PersistentGhostState` | Persiste l'état compris d'un fantôme knowledge-driven. | Netcode, Persistence | Ajouté automatiquement par PersistentWorldSceneInstaller | élevé |
| `Assets/Scripts/Netcode/Persistence/PersistentNetworkObject.cs` | `PersistentNetworkObject` | Persistance du monde, snapshots, providers ou reconstruction late join. | Editor, Netcode, Persistence | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/PersistentReadableSentencePuzzleState.cs` | `PersistentReadableSentencePuzzleState` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/PersistentWorldDebug.cs` | `PersistentWorldDebug` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/PersistentWorldSceneInstaller.cs` | `PersistentWorldSceneInstaller` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence, Inventory | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/PersistentWorldSyncOverlay.cs` | `PersistentWorldSyncOverlay` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence, UI | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/SnapshotSerializer.cs` | `SnapshotSerializer` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/SpawnManager.cs` | `SpawnManager` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/WorldRulesStateManager.cs` | `WorldRulesStateManager` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence, Temporal | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/WorldSaveAdapter.cs` | `WorldSaveAdapter` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/Persistence/WorldStateManager.cs` | `WorldStateManager` | Persistance du monde, snapshots, providers ou reconstruction late join. | Netcode, Persistence | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/Netcode/WorldInteractionService.cs` | `WorldInteractionService` | Infrastructure multijoueur, identité joueur, spawn, RPC ou synchronisation. | Netcode | Créé ou appelé runtime par Netcode | élevé |
| `Assets/Scripts/OrbeController.cs` | `OrbeController` | Contrôleur de comportement ou d interface. | Unity | ScriptableObjects (Assets/ScriptableObjects/Item/Item_Orbe bleu/Item_Orbe bleu_Model.prefab, Assets/ScriptableObjects/Item/Item_Orbe bleu/Item_Orbe rouge_Model.prefab) | élevé |
| `Assets/Scripts/PausePanelController.cs` | `PausePanelController` | Contrôleur de comportement ou d interface. | Netcode, UI, Input | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/Pivot.cs` | `Pivot` | Comportement ou donnée spécifique au projet. | Unity | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/Pulse.cs` | `Pulse` | Comportement ou donnée spécifique au projet. | Unity | Scènes/Prefabs (3 fichiers) | moyen |
| `Assets/Scripts/QuantityBox.cs` | `QuantityBox` | Interface utilisateur ou feedback visuel. | UI | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/RandomDriftReturn.cs` | `RandomDriftReturn` | Comportement ou donnée spécifique au projet. | Physics | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/ReadableContentRuntime.cs` | `ReadableContentRuntime` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/ReadableSentencePuzzle.cs` | `ReadableSentencePuzzle` | Comportement ou donnée spécifique au projet. | Netcode, UI, Input, Audio, Physics | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/ReadableSentencePuzzleUI.cs` | `ReadableSentencePuzzleUI` | Interface utilisateur ou feedback visuel. | UI | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/ReadableSentenceReference.cs` | `ReadableSentenceReference` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/ReturnHomeTrigger.cs` | `ReturnHomeTrigger` | Comportement ou donnée spécifique au projet. | Netcode, UI, Input, Audio, Inventory, Physics | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/RunSpeedCameraEffect.cs` | `RunSpeedCameraEffect` | Effet appliqué par un item, une action ou un état de gameplay. | ScriptableObject | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/RunSpeedPeripheralBlur.cs` | `RunSpeedPeripheralBlur` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Sablier.cs` | `Sablier` | Comportement ou donnée spécifique au projet. | Persistence | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/SceneLightOcclusionEnforcer.cs` | `SceneLightOcclusionEnforcer` | Force les lumières de scène à caster des ombres pour éviter que torches et lampes éclairent à travers les murs. | HDRP, Rendering | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/Skill.cs` | `StatType` | Définit des valeurs partagées utilisées par d autres scripts. | ScriptableObject | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/SkillCheckFeedback.cs` | `SkillCheckFeedback` | Comportement ou donnée spécifique au projet. | UI | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/SkillCheckFeedbackAnchor.cs` | `SkillCheckFeedbackAnchor` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/SkillCheckSystem.cs` | `SkillCheckSystem` | Comportement ou donnée spécifique au projet. | Audio | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/SquadAIManager.cs` | `SquadAIManager` | Manager central qui coordonne un système runtime. | Netcode, NavMesh, Physics | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/SquadCharacterController.Health.cs` | `SquadCharacterController` | Contrôleur de comportement ou d interface. | Audio | Prefabs (Assets/Prefabs/Character/Player_Model_Lucian.prefab) | élevé |
| `Assets/Scripts/SquadCharacterController.Lockpick.cs` | `SquadCharacterController` | Contrôleur de comportement ou d interface. | Inventory | Prefabs (Assets/Prefabs/Character/Player_Model_Lucian.prefab) | élevé |
| `Assets/Scripts/SquadCharacterController.cs` | `SquadCharacterController` | Contrôleur de comportement ou d interface. | Netcode, Persistence, Inventory, Physics | Prefabs (Assets/Prefabs/Character/Player_Model_Lucian.prefab) | élevé |
| `Assets/Scripts/SquadFollowerAgent.cs` | `SquadFollowerAgent` | Comportement ou donnée spécifique au projet. | NavMesh | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/SquadManager.cs` | `SquadManager` | Manager central qui coordonne un système runtime. | Netcode, UI, Input, ScriptableObject, Inventory | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/SquadUISettings.cs` | `SquadUISettings` | Interface utilisateur ou feedback visuel. | Netcode, UI | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/SwingMotion.cs` | `SwingMotion` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Temporal/AgeManager.cs` | `AgeManager` | Manager central qui calcule l'âge global depuis les braseros anciens allumés. | Temporal | Créé ou appelé runtime | élevé |
| `Assets/Scripts/Temporal/HumanModificationTag.cs` | `HumanModificationTag` | Définit des valeurs partagées utilisées par d autres scripts. | Temporal | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/Temporal/TemporalAge.cs` | `TemporalAge` | Définit des valeurs partagées utilisées par d autres scripts. | Temporal | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/Temporal/TemporalObject.cs` | `TemporalState` | Système temporel léger : âges, zones et objets à états. | Temporal, Physics | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/Temporal/TemporalZone.cs` | `TemporalAgeChangedEvent` | Système temporel léger : âges, zones et objets à états. | Temporal | Appelé par code ou ajouté runtime | faible |
| `Assets/Scripts/TimePeriodValueMode.cs` | `TimePeriodValueMode` | Définit des valeurs partagées utilisées par d autres scripts. | Temporal | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/TimePeriodVisibility.cs` | `TimePeriodVisibility` | Rend un objet visible selon une période inclusive de l'âge global. | Temporal | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/ToggleTorchEffect.cs` | `ToggleTorchEffect` | Effet appliqué par un item, une action ou un état de gameplay. | ScriptableObject | ScriptableObjects (Assets/ScriptableObjects/Effect/ToggleTorchEffect.asset) | faible |
| `Assets/Scripts/TorchEffect.cs` | `TorchEffect` | Effet appliqué par un item, une action ou un état de gameplay. | ScriptableObject | ScriptableObjects (Assets/ScriptableObjects/Effect/TorchEffect.asset) | faible |
| `Assets/Scripts/TorchLightReceiver.cs` | `TorchLightReceiver` | Comportement ou donnée spécifique au projet. | Unity | Scènes/Prefabs (3 fichiers) | moyen |
| `Assets/Scripts/TriggerPairTeleporter.cs` | `TriggerPairTeleporter` | Comportement ou donnée spécifique au projet. | Audio, Physics | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/TrouEtroit.cs` | `TrouEtroit` | Comportement ou donnée spécifique au projet. | UI, Input, Audio, Physics | Scènes (Assets/Scenes/Maison.unity) | moyen |
| `Assets/Scripts/TwoLeverPuzzle.cs` | `TwoLeverPuzzle` | Objet interactif de scène. | Audio | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/VisualEffects/GhostDissolveController.cs` | `GhostDissolveController` | Pilote le dissolve shader/VFX des fantômes, y compris l'apparition de proximité. | VFX, Physics | Ajouté runtime par GhostController ou manuellement sur un fantôme | moyen |
| `Assets/Scripts/WaterMotion.cs` | `WaterMotion` | Comportement ou donnée spécifique au projet. | Unity | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/WorldPickupUtility.cs` | `WorldPickupUtility` | Comportement ou donnée spécifique au projet. | Netcode, Physics | Appelé par code ou ajouté runtime | élevé |
| `Assets/Scripts/WorldPlacementUtility.cs` | `WorldPlacementUtility` | Comportement ou donnée spécifique au projet. | Physics | Appelé par code ou ajouté runtime | moyen |
| `Assets/Scripts/Zone.cs` | `Zone` | Comportement ou donnée spécifique au projet. | Netcode, NavMesh, Audio, Physics | Scènes (Assets/Scenes/Maison.unity) | élevé |
| `Assets/Scripts/ZoneAudioProfileSO.cs` | `ZoneAudioProfileSO` | Audio, musique, ambiance ou voix. | Audio, ScriptableObject | ScriptableObjects (Assets/ScriptableObjects/ZoneProfil/ZoneAudioProfile_Acte I.asset, Assets/ScriptableObjects/ZoneProfil/ZoneAudioProfile_Partie_5_BridgeCross.asset) | faible |

## Comment utiliser cet index

1. Chercher le système dans [GAME_SYSTEMS.md](GAME_SYSTEMS.md).
2. Repérer les scripts concernés dans cet index.
3. Lire les scripts à risque élevé avant de modifier les scripts à risque faible qui en dépendent.
4. Tester la scène ou le prefab indiqué après modification.
5. Mettre à jour cet index si un script est ajouté, supprimé ou déplacé.
