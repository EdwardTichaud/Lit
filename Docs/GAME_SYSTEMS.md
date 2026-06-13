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
- `Assets/Scripts/CameraLineOfSightObstructionDetector.cs`
- `Assets/Scripts/CameraObstacleFader.cs`
- `Assets/Scripts/CameraObstructionVignetteController.cs`
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
- L'obstruction type BG3 est visuelle : la caméra ne se rapproche pas des murs par défaut, le système fade/masque les renderers obstruants et pilote une vignette HDRP runtime avec overlay UI de secours.
- Le centre de la vignette suit la position écran du personnage, calculée à partir des bounds de ses renderers quand possible pour s'adapter à sa hauteur.
- Dans `Maison.unity`, `CameraSystem` porte explicitement les composants d'obstruction. Le mask initial utilise `Default`, `Ground`, `Stairs` et `CameraObstruction`; les objets à ignorer peuvent utiliser le tag `CameraNonObstructing`.
- Tester les obstacles caméra, les zones fixes et les transitions combat.
- Voir aussi `Docs/CameraObstruction.md`.

## Optimisation visibilité et rendu

**Rôle** : réduire le coût de rendu sans couper les logiques de gameplay.

**Fichiers principaux** :

- `Assets/Scripts/VisibilityOptimization/VisibilityOptimizationManager.cs`
- `Assets/Scripts/VisibilityOptimization/OptimizableObject.cs`
- `Assets/Scripts/VisibilityOptimization/CameraVisibilityProtection.cs`
- `Assets/Scripts/VisibilityOptimization/CameraVisibilityObstacle.cs`
- `Assets/Scripts/VisibilityOptimization/RoomLightZoneController.cs`

**Points d'attention** :

- Le système est opt-in par objet et ne fait pas de `SetActive(false)`.
- Le XRay/caméra-joueur reste prioritaire : les renderers protégés ne sont pas culled.
- Les scripts ne sont pausés que s'ils implémentent explicitement les interfaces de visibilité.
- Voir `Docs/VisibilityOptimization.md`.

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

La UI peut être placée en scène, instanciée depuis prefab, ou créée en fallback runtime. Le `MainMenu` utilise maintenant un pointeur visible commun souris/manette au lieu d'un curseur de sélection forcé. Ce pointeur est piloté par `MainMenuPointerCursor`, éclaire le décor 3D via une torche, et déclenche les `CursorIntercation` du décor pour afficher le contour runtime HDRP existant.

Le décor du titre est un vrai décor de scène, pas une texture. `MainMenuTitleDecorController` lit la dernière sauvegarde sous `Application.persistentDataPath/Saves`, utilise `meta.json` et `CharacterState.json`, puis active les variantes de décor selon la progression détectée.

**Points d'attention** :

- Beaucoup de références UI sont assignées dans l'inspecteur.
- Tester avec souris, clavier et manette si possible.
- Après une modification structurelle du MainMenu, relancer `Lit/MainMenu/Install Title Decor` si le décor/pointeur doit être régénéré dans la scène.
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

## Temps, strates et Braseros anciens

**Rôle** : représenter l'âge global du monde et les objets visibles selon une période.

**Fichiers principaux** :

- `Assets/Scripts/BraseroDisplayManager.cs`
- `Assets/Scripts/Temporal/AgeManager.cs`
- `Assets/Scripts/Brasero.cs`
- `Assets/Scripts/TimePeriodVisibility.cs`
- `Assets/Scripts/SceneLightOcclusionEnforcer.cs`
- `Assets/Scripts/TorchLightReceiver.cs`
- `Assets/Scripts/DissolveRevealSystem.cs`
- `Assets/Scripts/DissolveRevealTarget.cs`
- `Assets/Scripts/MasterShaderDissolveController.cs`
- `Assets/Scripts/FlickeringLight.cs`
- `Assets/Scripts/Temporal/*`

**Fonctionnement général** :

Le projet utilise `AgeManager` comme source canonique d'âge. Le joueur commence en 666, et chaque `Brasero` marqué `ancientBrasero` et allumé recule l'âge de 100 ans. Les torches, Munin et les braseros classiques ne changent plus l'âge du monde. `BraseroDisplayManager` diffuse cet état aux affichages, et `AgeManager` applique `_AgeAmount` aux renderers compatibles via `MaterialPropertyBlock`.

La révélation/disparition visuelle des items par lumière passe par `DissolveRevealSystem` et `DissolveRevealTarget`. Ce système pilote `_DissolveAmount` sur les matériaux compatibles avec une transition par défaut de 0,5 seconde et reste séparé du système temporel. Les portes peuvent aussi exiger une flamme active proche avant d'être détectées/interactives.

Pour un objet spécial ou un prop qui doit se dissoudre sans passer par la logique de lumière, `MasterShaderDissolveController` pilote les mêmes propriétés via `MaterialPropertyBlock`.

Le MasterShader HDRP expose le dissolve standardisé :

- `_DissolveAmount`
- `_DissolveTexture`
- `_DissolveSoftness`
- `_DissolveEdgeColor`
- `_DissolveEdgeWidth`
- `_DissolveEdgeIntensity`

`_AgeAmount` mélange uniquement `BaseTexture` et `OldTexture`. `_DissolveAmount` contrôle uniquement la visibilité finale en alpha clip HDRP.

**Points d'attention** :

- Pour ajouter une source temporelle, activer `ancientBrasero` sur un objet qui possède déjà `Brasero`.
- Pour les nouveaux contenus temporels, préférer `AgeManager` + `TimePeriodVisibility`, et `BraseroDisplayManager` pour les affichages.
- Les torches peuvent révéler des items par dissolve, mais ne doivent pas écrire `AgeAmount`.
- Les braseros classiques ne doivent pas écouter `Interact`; l'allumage passe par `ToggleTorch`.
- Les lumières de torche et de décor doivent garder des ombres temps réel. `SceneLightOcclusionEnforcer` force les lumières sans ombres à passer en `Soft Shadows` et s'assure que les renderers de décor des layers `Default`, `Ground`, `Stairs` et `CameraObstruction` castent/receivent les ombres.

## Readables, lore et données narratives

**Rôle** : afficher textes lisibles, fragments narratifs, registres, données de lignées et connaissances persistantes.

**Fichiers principaux** :

- `Assets/ScriptableObjects/Item/Item.cs`
- `Assets/ScriptableObjects/Knowledge/KnowledgeSO.cs`
- `Assets/Scripts/KnowledgeManager.cs`
- `Assets/Scripts/KnowledgeTypes.cs`
- `Assets/Scripts/KnowledgeUnlockTrigger.cs`
- `Assets/Scripts/GhostController.cs`
- `Assets/Scripts/VisualEffects/GhostDissolveController.cs`
- `Assets/Scripts/Netcode/Persistence/PersistentGhostState.cs`
- `Assets/Scripts/ReadableSentencePuzzle.cs`
- `Assets/Scripts/ReadableSentencePuzzleUI.cs`
- `Assets/Scripts/ReadableContentRuntime.cs`
- `Assets/Scripts/NarrativeData/*`
- `Docs/NarrativeData.md`

**Knowledge-driven narrative** :

Le chemin principal des nouvelles interactions narratives passe par `KnowledgeSO`
et `KnowledgeManager`. Les readables peuvent débloquer des connaissances via
`Item.knowledgeUnlockedOnRead`, les lieux ou anomalies via `KnowledgeUnlockTrigger`,
et les fantômes via `GhostData.reactions` + `KnowledgeRequirement`.

Quand plusieurs réactions de fantôme sont disponibles, `GhostController` peut
afficher une liste d'options générée depuis les connaissances possédées. L'état
"compris" d'un fantôme est sauvegardé par `PersistentGhostState`, tandis que les
faits appris restent sauvegardés globalement par `PersistentKnowledgeState`.

Les réactions peuvent déclencher des effets de scène via `triggerEffectIds`.
Le cas `GhostData_Luc` utilise `luc_dissolve` : si le joueur possède
`Knowledge_JonLocation`, parler à Luc utilise la réaction correspondante et le
`GhostController` déclenche le dissolve des GameObjects configurés dans
`dissolveEffectRules`.

Par défaut, les fantômes pilotés par `GhostController` peuvent aussi utiliser un
dissolve de proximité : hors rayon, ils restent à dissolve amount max ; quand le
personnage contrôlé entre dans la zone, ils lerp vers `0` en `1` seconde par
défaut.

Pour les connaissances implicites, `KnowledgeRequirement` supporte des seuils
par catégorie ou par tag : par exemple demander au moins trois connaissances
taguées `quartier_lune_pleine`.

`ReadableSentencePuzzle` reste un système legacy de réponse textuelle libre pour
les contenus déjà posés. Ne pas l'utiliser comme modèle principal pour les nouveaux
fantômes.

**Points d'attention** :

- Les textes sont souvent dans des assets `Item`.
- Les nouvelles métadonnées sont optionnelles pour ne pas casser les items existants.
- Les connaissances sont persistées par `PersistentKnowledgeState`; ne pas créer
  de sauvegarde parallèle pour les mêmes faits.
- Les fantômes de scène avec `GhostController` doivent aussi avoir un
  `PersistentNetworkObject`; `PersistentWorldSceneInstaller` ajoute le provider
  de sauvegarde automatiquement lors de la préparation de scène.

## Construction, bâtiments et décor

**Rôle** : placer, améliorer, afficher ou masquer des éléments construits/décoratifs.

**Fichiers principaux** :

- `Assets/Scripts/BuilderController.cs`
- `Assets/Scripts/BuildingInfoInteractable.cs`
- `Assets/Scripts/BuildingPanelController.cs`
- `Assets/Scripts/BuildingRuntimeState.cs`

**Points d'attention** :

- Le build est lié à l'inventaire et parfois au Netcode.
- Le décor peut avoir beaucoup d'instances en scène ; l'optimisation de visibilité est documentée dans la section dédiée.

## Environnement et zones

**Rôle** : gérer zones, ambiance, états environnementaux, hub et maison.

**Fichiers principaux** :

- `Assets/Scripts/Zone.cs`
- `Assets/Scripts/Environment/*`
- `Assets/Scripts/SnowRuntimeEffects.cs`
- `Assets/Scripts/Maison.cs`
- `Assets/Scripts/MaisonWaitingPoint.cs`
- `Assets/Scripts/HubZone.cs`
- `Assets/Scripts/HubRosterManager.cs`

**Neige et VFX runtime** :

`SnowRuntimeEffects` se crée automatiquement en Play Mode, suit le personnage contrôlé localement, sample la surface sous lui, lit `_SnowAmount` sur les matériaux compatibles, puis pilote un `ParticleSystem` runtime de scintillement et un émetteur d'empreintes. Voir [SnowRuntimeEffects.md](SnowRuntimeEffects.md).

**Points d'attention** :

- Les zones peuvent impacter audio, torche, IA et état de personnages.
- Tester les triggers en scène.
- Les effets de neige dépendent à la fois du collider physique, du layer raycasté et des propriétés shader `_SnowAmount`, `_SnowTopThreshold` et `_SnowBlendSoftness`.

## Outils Editor

**Rôle** : automatiser des tâches dans l'éditeur Unity.

**Fichiers principaux** :

- `Assets/Editor/*`

**Points d'attention** :

- Ces scripts ne tournent pas en build.
- Ils peuvent modifier des scènes, prefabs ou assets quand on clique sur un menu.
- Lire le code avant d'utiliser un outil qui bake, installe ou répare des objets.
