# Lit — Architecture et systèmes

Ce document est la référence technique consolidée du projet. Il décrit le
fonctionnement actuel, les points d'intégration et les garde-fous.

## Vue d'ensemble

Le projet combine :

1. des scènes Unity qui placent les objets, managers et interfaces ;
2. des `MonoBehaviour` qui portent les comportements runtime ;
3. des `ScriptableObject` qui stockent les données éditables ;
4. des managers qui coordonnent les systèmes transverses ;
5. Unity Netcode for GameObjects et une persistance par snapshots.

Scènes principales :

- `Assets/Scenes/MainMenu.unity` ;
- `Assets/Scenes/Maison.unity` ;
- `Assets/Scenes/MovementLab.unity`.

## Joueur, groupe et input

Fichiers principaux :

- `Assets/Scripts/SquadManager.cs`
- `Assets/Scripts/SquadCharacterController.cs`
- `Assets/Scripts/SquadCharacterController.*.cs`
- `Assets/Scripts/Movement/SquadCharacterController.*.cs`
- `Assets/Scripts/SquadFollowerAgent.cs`
- `Assets/ScriptableObjects/CharacterData/CharacterData.cs`
- `Assets/Scripts/Netcode/LocalPlayerInput.cs`
- `Assets/Scripts/Netcode/LocalInputRouter.cs`
- `Assets/Scripts/InputFocusStack.cs`

`SquadManager` connaît les personnages disponibles.
`SquadCharacterController` porte leur état runtime. Les fichiers partiels séparent
les responsabilités de mouvement, santé, crochetage et interactions.

`Assets/PlayerInputs.cs` est généré depuis `Assets/PlayerInputs.inputactions` et
ne doit pas être modifié manuellement.

Points d'attention :

- conserver les noms des champs sérialisés ;
- vérifier le personnage local en mode réseau ;
- tester mouvement, échelle, changement de personnage et menus après une
  modification d'input ;
- traiter Ultimate Character Controller comme la cible de locomotion, les
  scripts projet servant encore de couche de transition.

## Caméra et obstruction

Fichiers principaux :

- caméra UCC : package
  `Packages/com.opsive.ultimatecharactercontroller/Runtime/Camera/` ;
- binding au personnage local :
  `Assets/Scripts/OpsiveIntegration/LitUccCameraCharacterBinder.cs` ;
- visibilité XRay :
  `Assets/CameraSystem_Legacy/Scripts/XRayVisibilityController.cs` ;
- masque et vignette :
  `Assets/CameraSystem_Legacy/Scripts/XRayMaskFollower.cs` ;
- synchronisation de caméra XRay :
  `Assets/CameraSystem_Legacy/Scripts/XRayCameraSync.cs` ;
- ancien rig CRPG :
  `Assets/CameraSystem_Legacy/Scripts/CameraController.cs`.

`Maison.unity` utilise le `CameraController` UCC. Le
`LitUccCameraCharacterBinder` associe automatiquement cette caméra au personnage
local fourni par `LocalPlayerContext`.

Le dossier `CameraSystem_Legacy` contient encore le système XRay utilisé pour
garder le personnage visible derrière les obstacles. Malgré le nom du dossier,
ne pas considérer ses scripts XRay comme supprimables sans vérifier la scène.

`XRayVisibilityController` :

- détecte les obstacles par raycast ou spherecast ;
- exige par défaut qu'une proportion des bounds du renderer soit masquée ;
- déplace temporairement les obstacles vers le layer `Obstacle` ;
- protège les renderers concernés du culling global ;
- pilote `XRayMaskFollower`, qui positionne le masque UI et la vignette HDRP.

Le rig CRPG historique possède encore un solver de collision physique, désactivé
par défaut via `allowLegacyObstacleRepositioning`. Il n'est pas la caméra
principale de `Maison`.

Tests :

- personnage masqué par un mur et correctement révélé par le XRay ;
- restauration des layers après obstruction ;
- suivi du masque et de la vignette ;
- rotation, zoom, caméra fixe et combat ;
- plusieurs murs sans clignotement.

## Interactions et inventaire

Fichiers d'interaction :

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

Fichiers d'inventaire :

- `Assets/ScriptableObjects/Item/Item.cs`
- `Assets/Scripts/InventoryPanelController.cs`
- `Assets/Scripts/InventoryUISettings.cs`
- `Assets/Scripts/Netcode/NetworkInventory.cs`
- `Assets/Scripts/ItemPassiveEffectSystem.cs`
- `Assets/Scripts/Effect.cs`

Une interaction peut être locale, validée par le host, persistée ou purement
visuelle. Ne pas changer une méthode publique utilisée par un `UnityEvent`.

`PortalController` utilise la détection locale standard : lorsqu'un portail est
à portée et visible, ses renderers reçoivent l'outline runtime. Aucun texte
d'interaction n'est créé. L'action Interagir téléporte le personnage vers le
`destinationPoint`; en multijoueur, la requête est validée par
`WorldInteractionService`.

Les `Item` sont des ScriptableObjects. `itemId` est un identifiant stable et ne
doit pas changer sans migration. Tester ramassage, utilisation, drop, lecture,
inventaire et host/client après une modification structurante.

### Building legacy désactivé

Le gameplay de construction n'est pas actif. Le code historique est conservé
temporairement comme module réactivable, derrière
`Assets/Resources/LegacyBuildingSystemSettings.asset`.

- `systemEnabled` reste désactivé par défaut ;
- les panels, interactions, placements, effets et RPC Building sont bloqués ;
- les anciennes données JSON `builtConstructions` sont conservées sans être
  instanciées ;
- les snapshots Netcode `building:` sont conservés sans reconstruction ;
- ne pas étendre ce module tant qu'il n'est pas officiellement réintroduit.

La couche `LegacyBuildingPersistenceMigration` doit rester en place avant toute
suppression physique des scripts Building. Une future suppression devra d'abord
migrer ou purger explicitement les sauvegardes et snapshots existants.

## UI et menus

Fichiers principaux :

- `Assets/Scripts/Menu/*`
- `Assets/Scripts/InventoryPanelController.cs`
- `Assets/Scripts/SquadUISettings.cs`
- `Assets/Scripts/LootUISettings.cs`
- `Assets/Scripts/ConfirmationManager.cs`
- `Assets/Scripts/InfoBoxUI.cs`
- `Assets/Scripts/PausePanelController.cs`

La UI mélange objets de scène, prefabs et fallbacks runtime. Certaines références
sont retrouvées par nom ou hiérarchie.

Le menu principal utilise :

- `MainMenuPointerCursor` pour le pointeur souris/manette et l'interaction avec le
  décor 3D ;
- `MainMenuTitleDecorController` pour adapter le décor à la dernière sauvegarde
  lue dans `Application.persistentDataPath/Saves`.

Après une modification structurelle du menu, utiliser si nécessaire
`Lit/MainMenu/Install Title Decor`, puis retester souris, clavier et manette.

## Temps, strates et lumière

Cette section décrit l'implémentation. L'interprétation diégétique des strates
et des Ancient Flames relève du [lore canonique](../Design/Lore.md) : les
Explorateurs les parcourent après la fracture, et les Ancient Flames sont des
mécanismes du rituel interrompu.

Fichiers principaux :

- `Assets/Scripts/Temporal/AgeManager.cs`
- `Assets/Scripts/Temporal/TemporalAge.cs`
- `Assets/Scripts/Temporal/TemporalZone.cs`
- `Assets/Scripts/Temporal/TemporalObject.cs`
- `Assets/Scripts/TimePeriodVisibility.cs`
- `Assets/Scripts/Flame.cs`
- `Assets/Scripts/AncientFlameDisplayManager.cs`
- `Assets/Scripts/FlameLightReceiver.cs`
- `Assets/Scripts/DissolveRevealSystem.cs`
- `Assets/Scripts/DissolveRevealTarget.cs`

### Source runtime actuelle

`AgeManager` est la source canonique de l'âge global.

```text
currentYear = clamp(666 - litAncientFlameCount * yearsPerAncientFlame)
yearsPerAncientFlame = 111
```

Seules les `Flame` marquées `ancientFlame` — les `AncientFlame` — participent
au calcul. Munin et les `Flame` classiques n'écrivent pas l'âge.

`AgeManager` :

- collecte les `AncientFlame` ;
- écoute leurs changements ;
- met à jour `TimePeriodVisibility` ;
- met à jour `AncientFlameDisplayManager` ;
- pousse `_AgeAmount` via `MaterialPropertyBlock`.

### Grille temporelle

`TemporalAge` définit `Age000` à `Age666` par pas de 111 ans.
`AgeManager.DefaultYearsPerAncientFlame` réutilise
`TemporalAgeUtility.StepYears`, ce qui garde le calcul global aligné sur cette
grille. La progression est donc :

```text
666 -> 555 -> 444 -> 333 -> 222 -> 111 -> 000
```

`TemporalZone` peut piloter explicitement des `TemporalObject` locaux. Un Flame
ancien modifie la source globale, mais ne change pas automatiquement une zone
locale non reliée. Ne pas modifier les valeurs de l'enum sans migrer scènes et
assets.

### Dissolve par lumière

`DissolveRevealSystem` pilote `_DissolveAmount` indépendamment de `_AgeAmount`.
Le MasterShader expose :

- `_DissolveAmount`
- `_DissolveTexture`
- `_DissolveSoftness`
- `_DissolveEdgeColor`
- `_DissolveEdgeWidth`
- `_DissolveEdgeIntensity`

`_AgeAmount` mélange les états temporels. `_DissolveAmount` contrôle la visibilité
finale. Aucun des deux ne doit calculer l'autre.

`SceneLightOcclusionEnforcer` doit garder les ombres des flammes et du décor,
mais son scan continu est désactivé par défaut pour limiter le coût runtime.

## Munin

Fichier principal :

- `Assets/Scripts/MuninController.cs`
- `Assets/Scripts/MuninUI.cs`
- `Assets/Scripts/MuninChargeReward.cs`
- `Assets/Scripts/MemoryShard.cs`
- `Assets/Scripts/PacifiedMemoryReward.cs`
- `Assets/Scripts/VigilAltar.cs`

Le suivi indépendant, les mouvements manuels, les charges et les réactions sont
désormais consolidés dans `MuninController`. Munin reste enfant du personnage,
mais sa position est pilotée en world space pour éviter une rotation rigide avec
le joueur.

Réglages importants :

- `baseOffset` et `useWorldSpaceOffset` ;
- `followSmoothTime` ;
- `maxDistanceFromTarget` et `minDistanceFromTarget` ;
- dérive Perlin via `driftAmplitude`, `driftFrequency`, `driftSpeed` ;
- spasmes discrets via `spasmAmplitude` et les cooldowns.

`MuninController.MoveToWorldAndBack` suspend le suivi indépendant lors d'un trajet
vers une `Flame`. Pour un autre contrôle temporaire, appeler
`BeginExternalMotion(...)`, puis `EndExternalMotion()`.

### Économie des charges

Munin commence avec 10 charges. Une `Flame` légère consomme 1 charge à
l'allumage. Une grande `Flame` configurée à 2 et toute `AncientFlame` consomment
2 charges. `Flame.chargeCostToLight` reste exposé pour les exceptions de level
design.

L'extinction ne rend jamais de charge. Elle conserve uniquement ses effets de
monde : retour au noir, influence lumineuse retirée, réactions narratives,
Ombres éventuelles et mise à jour temporelle d'une `AncientFlame`.

Les recharges passent par `MuninChargeReward` :

- `MemoryShard` : +1, usage unique, révélable par l'influence d'une `Flame` ;
- `PacifiedMemoryReward` : +3 après apaisement d'un fantôme ou satisfaction
  d'un `KnowledgeRequirement` d'enquête ;
- `VigilAltar` : recharge complète, rare, réutilisable après cooldown.

`PersistentMuninChargeRewardState` sauvegarde les récompenses consommées.
L'autel `VigilAltar_Hub_Safeguard` près du spawn solo de `Maison` constitue la
protection anti-softlock. Il n'accorde rien automatiquement : le joueur doit
le rejoindre et interagir.

Pour ajouter une recharge, placer un `MuninChargeReward` ou l'un de ses trois
composants spécialisés, puis configurer les prérequis dans l'inspecteur. Une
enquête familiale utilise `optionalInvestigationRequirement` avec les
connaissances, catégories ou tags existants ; un fantôme utilise
`optionalGhostRequirement`.

## Données narratives

Fichiers principaux :

- `Assets/ScriptableObjects/Item/Item.cs`
- `Assets/ScriptableObjects/Knowledge/KnowledgeSO.cs`
- `Assets/Scripts/KnowledgeManager.cs`
- `Assets/Scripts/KnowledgeTypes.cs`
- `Assets/Scripts/KnowledgeUnlockTrigger.cs`
- `Assets/Scripts/GhostController.cs`
- `Assets/Scripts/NarrativeData/*`

Le chemin narratif utilise exclusivement `KnowledgeSO` et `KnowledgeManager`.
La saisie de réponses libres et la comparaison de chaînes ont été retirées.

Sources de connaissances :

- `Item.knowledgeUnlockedOnRead` ;
- `KnowledgeUnlockTrigger` ;
- `LocalVoiceLineController` ;
- réactions de `GhostController`.

`MuninChargeReward` écoute `KnowledgeManager.KnowledgeUnlocked` seulement si un
prérequis de connaissance ou d'enquête est réellement configuré.
`PacifiedMemoryReward` peut aussi écouter `GhostController.Understood`. Les
récompenses restent donc configurables et ne sont pas codées en dur dans chaque
connaissance.

`KnowledgeRequirement` supporte les faits précis, listes alternatives,
catégories, tags et seuils par catégorie/tag.

Les fantômes utilisent `GhostData.reactions` et des `KnowledgeRequirement`.

Persistance :

- connaissances : `PersistentKnowledgeState` ;
- état propre d'un fantôme : `PersistentGhostState`.

Un fantôme de scène persistant doit avoir un `PersistentNetworkObject`.
`PersistentWorldSceneInstaller` ajoute le provider associé lors de la préparation
de scène.

## Fantômes et dissolve

Fichiers :

- `Assets/Scripts/VisualEffects/GhostDissolveController.cs`
- `Assets/Shaders/GhostDissolve/SG_GhostDissolve_HDRP.shadergraph`
- `Assets/Shaders/GhostDissolve/Material_GhostDissolve.mat`
- `Assets/Shaders/GhostDissolve/GhostDissolveHDRP.hlsl`

`GhostDissolveController` :

- pilote les propriétés par `MaterialPropertyBlock` ;
- collecte `SkinnedMeshRenderer` et `MeshRenderer` ;
- calcule les bounds monde ;
- supporte plusieurs VFX Graph via `DustVfxBinding` ;
- expose apparition, disparition, reset et contrôle direct ;
- émet des `UnityEvent` de début et de fin.

Propriétés principales :

| Propriété | Valeur de départ recommandée |
| --- | ---: |
| `_DissolveAmount` | `-0.08`, animé vers `1.12` |
| `_DissolveNoiseScale` | `2.75` |
| `_DissolveEdgeWidth` | `0.055` |
| `_DissolveEdgeColor` | HDR cyan |
| `_GhostAlpha` | `0.68` |
| `_FineNoiseMultiplier` | `5.7` |
| `_NoiseInfluence` | `0.28` |
| `_FresnelPower` | `4.5` |
| `_FresnelIntensity` | `1.4` |
| `_DissolveEdgeIntensity` | `4.0` |

Configuration HDRP recommandée :

- Lit transparent ;
- Alpha ou Premultiply ;
- Alpha Clip actif ;
- Transparent Depth Prepass/Postpass pour les personnages importants ;
- pas d'ombres pour un fantôme pur ;
- émission pour le bord et le Fresnel.

Le Shader Graph appelle la custom function HLSL avec position et normale monde.
Le champ de dissolve combine hauteur normalisée et deux niveaux de bruit. Le bord
est la bande autour de `_DissolveAmount`; l'alpha final combine masque visible et
`_GhostAlpha`.

VFX Graph recommandé :

- un binding par `SkinnedMeshRenderer` ;
- spawn dans la bande de dissolve ;
- `BaseSpawnRate` environ `2200` pour un personnage principal, `900` pour un
  personnage secondaire ;
- durée de vie `1.3` à `3.2` secondes ;
- turbulence autour de `0.55`, drag autour de `1.6` ;
- quads HDRP Unlit pour les cendres ;
- motes additives optionnelles en faible proportion.

`GhostController` pilote le reveal de proximité. Sans personnage contrôlé dans le
rayon, la cible reste à `1.12`; à l'approche, elle lerp vers `0` en une seconde
par défaut.

Une réaction narrative peut déclencher un effet de scène avec
`triggerEffectIds`, mappé dans `dissolveEffectRules`.

Pièges :

- tri des transparents par renderer ;
- bloom surexposé ;
- alpha clip trop élevé ;
- bounds de skinned mesh hors écran ;
- une source VFX Graph par renderer ;
- noms de propriétés strictement identiques ;
- coût de l'overdraw et des depth passes.

## Neige runtime

Fichier principal :

- `Assets/Scripts/SnowRuntimeEffects.cs`

Le bootstrap `RuntimeInitializeOnLoadMethod` crée un objet persistant qui :

- recherche le personnage local ;
- ajoute un `SnowFootprintEmitter` ;
- suit le joueur avec `SnowSparkleController` ;
- raycast le sol ;
- lit les propriétés de neige du matériau.

Propriétés shader :

- `_SnowAmount`
- `_SnowThickness`
- `_SnowColor`
- `_SnowTopThreshold`
- `_SnowBlendSoftness`

Une surface doit avoir un collider, une normale suffisamment horizontale, être
dans le `groundMask` et utiliser un matériau avec `_SnowAmount > 0`.

Le système est local au personnage contrôlé. `FX_Snow` dans certains prefabs est
un effet statique distinct.

Paramètres de référence :

- rayon de scintillement `3.5` ;
- hauteur `0.8` ;
- émission maximale `46` ;
- lissage `0.2` ;
- échantillonnage de personnage environ toutes les `0.25` seconde.

## Optimisation de visibilité

Fichiers :

- `Assets/Scripts/VisibilityOptimization/VisibilityOptimizationManager.cs`
- `Assets/Scripts/VisibilityOptimization/OptimizableObject.cs`
- `Assets/Scripts/VisibilityOptimization/CameraVisibilityProtection.cs`
- `Assets/Scripts/VisibilityOptimization/CameraVisibilityObstacle.cs`
- `Assets/Scripts/VisibilityOptimization/RoomLightZoneController.cs`

Le système est opt-in. Il contrôle séparément renderers, lights et comportements
qui implémentent explicitement :

- `IPausableWhenInvisible` ;
- `IVisibilityUpdateRateTarget`.

Garde-fous :

- pas de `SetActive(false)` ;
- pas de collider désactivé ;
- pas de `MonoBehaviour.enabled = false` arbitraire ;
- priorité aux renderers protégés par la visibilité caméra-joueur ;
- catégories `Critical` et `Never Cull` exclues du culling ;
- hystérésis pour éviter le pop-in.

`RoomLightZoneController` pilote les lights d'une pièce sans raycast permanent par
light. Préférer baking/mixed pour les lumières statiques et réserver le realtime
aux flames, Flames et effets importants.

Menus Unity :

- `Tools/Lit/Visibility Optimization/Audit Active Scene`
- `Tools/Lit/Visibility Optimization/Install Manager In Active Scene`
- `Tools/Lit/Visibility Optimization/Add OptimizableObject To Selection`
- `Tools/Lit/Visibility Optimization/Mark Selection As Camera Visibility Obstacles`

Déployer progressivement sur des racines de décor simples, puis profiler.

## Sauvegarde, Netcode et monde persistant

Fichiers principaux :

- `Assets/Scripts/Menu/SaveSessionManager.cs`
- `Assets/Scripts/CharacterStateStore.cs`
- `Assets/Scripts/Netcode/NetcodeBootstrap.cs`
- `Assets/Scripts/Netcode/NetcodeLauncher.cs`
- `Assets/Scripts/Netcode/NetcodePlayerSpawner.cs`
- `Assets/Scripts/Netcode/WorldInteractionService.cs`
- `Assets/Scripts/Netcode/NetworkInventory.cs`
- `Assets/Scripts/Netcode/Persistence/*`

Le host est autoritaire. Les clients envoient des intentions ; le host valide et
modifie l'état.

Identités persistantes :

- ne jamais utiliser `NetworkObjectId`, l'instance ID Unity ou un GUID généré au
  runtime comme identité de sauvegarde ;
- objets de scène : ID sérialisé sur `PersistentNetworkObject` ;
- objets runtime : ID de session attribué par le host et ID stable de prefab.

Composants :

- `PersistentNetworkObject` : identité, type, prefab, transform et providers ;
- `IPersistentStateProvider` : sérialisation par fonctionnalité ;
- `NetworkObjectRegistry` : lookup et déduplication ;
- `WorldStateManager` : capture et application du snapshot ;
- `SnapshotSerializer` : sérialisation NGO ;
- `JoinSyncSystem` : transfert et acquittement de late join ;
- `SpawnManager` : spawn runtime et reconstruction ;
- `WorldRulesStateManager` : variables dérivées ;
- `WorldSaveAdapter` : pont sauvegarde/chargement.

Ordre de reconstruction late join :

1. résoudre les objets de scène ;
2. créer les objets runtime manquants ;
3. retirer les objets runtime absents du snapshot ;
4. appliquer transforms et état actif ;
5. appliquer les providers de gameplay ;
6. finaliser les références et règles dérivées ;
7. libérer le joueur et rebrancher contrôle/HUD.

Le client doit rester derrière l'écran de synchronisation jusqu'à la fin.

## Combat tour par tour

Fichiers :

- `Assets/Combat/*`
- `CombatSessionManager`
- `CombatAggroEnemy`
- `CombatHudController`
- `CombatCameraPresentationController`
- `CombatHealth`
- `CombatTransitionController`
- `IustiaIdolPrayer`

Déroulement :

1. `CombatAggroEnemy` détecte un joueur contrôlé.
2. Le manager téléporte uniquement ce joueur vers l'arène.
3. Le mouvement est suspendu côté serveur et client local.
4. Le tour joueur s'ouvre par une phase de décision locale.
5. Le joueur attaque, passe ou utilise un item pendant son tour.
6. Le tour ennemi applique le même principe avec une décision automatique.
7. La session restaure la position d'exploration à la fin.

La suspension visuelle de décision est locale au client engagé : caméra, HUD et
focus input utilisent du temps non scalé, mais le combat ne modifie pas
`Time.timeScale` global afin de préserver le serveur et les autres clients.

Les PV joueur réutilisent `SquadCharacterController`.
L'inventaire reste `InventoryPanelController`/`NetworkInventory`.

Chaque joueur hors combat qui prie auprès d'une idole de Iustia ajoute 20 % de
réduction des dégâts reçus, avec un cap par défaut de 80 %.

Limites :

- arène de scène, pas de scène additive ;
- ennemis encore simples dans l'arène ;
- HUD de scène requis ;
- vérifier la disparition réseau des ennemis non networkés.

## Audio, environnement et outils Editor

Audio :

- `AudioManager`
- `AudioClipSO`
- `ActionAudioLibrarySO`
- `LocalVoiceLineController`
- `ZoneAudioProfileSO`
- `Zone`

Ne pas déplacer un asset chargé par `Resources.Load` sans adapter le chemin.

Environnement :

- `Assets/Scripts/Environment/*`
- `Maison`
- `HubZone`
- `HubRosterManager`

Les scripts sous `Assets/Editor/` peuvent modifier scènes, prefabs et assets. Lire
le code avant d'utiliser un outil qui bake, installe, répare ou purge.

## Dépendances et risques de couplage

- HDRP ;
- Unity Input System ;
- Unity Netcode for GameObjects ;
- Unity Transport ;
- TextMesh Pro ;
- NavMesh ;
- assets tiers importés.

Risques principaux :

- objets cherchés par nom ou singleton ;
- champs sérialisés renseignés dans l'Inspector ;
- IDs de sauvegarde et de réseau ;
- UI hybride scène/prefab/runtime ;
- code legacy encore référencé ;
- modification directe d'assets vendeurs.

## Méthode d'intervention

Pour modifier un système :

1. trouver sa donnée (`ScriptableObject`) ;
2. trouver son composant de scène (`MonoBehaviour`) ;
3. trouver son manager ;
4. vérifier Netcode et persistance ;
5. vérifier les références dans scènes et prefabs ;
6. faire une modification limitée ;
7. utiliser les validations de
   [Operations.md](Operations.md).
