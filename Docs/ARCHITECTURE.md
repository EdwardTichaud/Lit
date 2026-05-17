# Lit - Architecture

## Vue d'ensemble

Le projet combine plusieurs couches :

1. **Scènes Unity** : placent les objets, managers, UI et points de gameplay.
2. **MonoBehaviours** : portent les comportements runtime sur les GameObjects.
3. **ScriptableObjects** : stockent les données éditables : items, personnages, audio, visions, effets.
4. **Managers runtime** : coordonnent les systèmes transverses comme sauvegarde, inventaire, audio, Netcode, combat et monde persistant.
5. **Services réseau/persistance** : synchronisent ou restaurent l'état du monde quand le mode multijoueur est actif.

La scène de gameplay principale actuelle est `Assets/Scenes/Maison.unity`. Le menu principal est `Assets/Scenes/MainMenu.unity`.

## Grandes familles de systèmes

### Joueur et groupe

Le joueur contrôle un ou plusieurs personnages via :

- `SquadManager`
- `SquadCharacterController`
- fichiers partiels sous `Assets/Scripts/Movement/`
- `CameraController`
- `CharacterInteractionDetection`

Le groupe est lié à des données `CharacterData` dans `Assets/ScriptableObjects/CharacterData/`.

### Input

L'Input System Unity génère `Assets/PlayerInputs.cs`. Ce fichier ne doit pas être modifié manuellement.

Les scripts projet utilisent ensuite :

- `LocalPlayerInput`
- `LocalInputRouter`
- `InputFocusStack`
- les contrôleurs de menu et de curseur.

### Interactions et inventaire

Les interactions passent par des composants de scène :

- `InteractableItem`
- `Door`
- `Lever`
- `Brasero`
- `LadderInteractable`
- `ReadableSentencePuzzle`
- `BuildingInfoInteractable`

Les données d'items sont dans `Item` et les assets `Assets/ScriptableObjects/Item/`.

### Temps, braseros et strates

Deux couches coexistent :

- couche canonique actuelle : `AgeManager`, `TimePeriodVisibility`, `LocalRuntimeAgeTrigger` ;
- couche temporelle d'objets : `TemporalAge`, `TemporalZone`, `TemporalTorch`, `TemporalObject`.

Les nouveaux contenus doivent privilégier la couche `Temporal/*` quand il s'agit d'archéologie temporelle, sans casser la couche déjà utilisée par la scène.

### Readables, registres et données narratives

Les readables passent principalement par `Item` et les données associées. Les métadonnées narratives récentes sont dans :

- `TemporalReadableMetadata`
- `LineageRecord`
- `FamilyRecord`
- `RegistryEntry`
- `TransgenerationalObjectRecord`

Ces structures sont volontairement simples.

### UI

La UI est très présente dans le projet :

- menus : `Assets/Scripts/Menu/`
- inventaire : `InventoryPanelController`, `InventoryUISettings`
- squad : `SquadUISettings`
- loot : `LootUISettings`
- confirmations : `ConfirmationManager`, `ConfirmationBox`
- feedback : `InfoBoxUI`, `SkillCheckFeedback`
- combat : `CombatHudController`

Beaucoup de scripts UI référencent directement des objets de scène ou prefabs. Il faut modifier ces scripts avec prudence.

### Netcode et persistance

Le multijoueur repose sur Unity Netcode for GameObjects :

- bootstrap : `NetcodeBootstrap`, `NetcodeLauncher`
- attribution joueur : `NetcodePlayerSpawner`, `NetcodePlayerAssignment`
- identité : `NetcodeClientIdentity`, `NetcodeCharacterIdentity`
- inventaire réseau : `NetworkInventory`
- interactions réseau : `WorldInteractionService`
- monde persistant : `Assets/Scripts/Netcode/Persistence/`

La persistance est un système à haut risque car elle relie sauvegarde, monde runtime, objets réseau et late join.

### Combat

Le combat tour par tour est conservé. Il est documenté dans [TurnBasedCombat.md](TurnBasedCombat.md).

Les scripts principaux sont :

- `CombatSessionManager`
- `CombatAggroEnemy`
- `CombatHudController`
- `CombatHealth`
- `CombatTransitionController`
- `IustiaIdolPrayer`

La direction actuelle garde le combat comme tension ponctuelle, pas comme boucle principale.

## Relations typiques

### Exemple : item ramassable

1. Un prefab ou objet de scène contient `InteractableItem`.
2. `InteractableItem` référence un `Item` ScriptableObject.
3. Le joueur interagit via `CharacterInteractionDetection`.
4. L'item est ajouté à l'inventaire local ou réseau.
5. `InventoryPanelController` met à jour l'interface.
6. En multijoueur, `NetworkInventory` et `WorldInteractionService` valident les actions côté serveur.

### Exemple : brasero et état du monde

1. Un `Brasero` est activé.
2. `AgeManager` recalcule l'année canonique depuis 666 en reculant de 111 ans par brasero allumé.
3. `TimePeriodVisibility` et les torches locales mettent à jour les objets visibles.
4. `BraseroDisplayManager` diffuse le snapshot d'âge aux affichages UI, Animator et Volume.
5. `BraseroTimeManager` reste seulement un pont de compatibilité pour les scènes qui le référencent.
5. En multijoueur, l'état peut être persisté par les systèmes sous `Netcode/Persistence`.

### Exemple : sauvegarde / chargement

1. `SaveSessionManager` gère la session et les métadonnées de sauvegarde.
2. `CharacterStateStore` restaure les personnages, inventaires et certains états.
3. `WorldSaveAdapter` et `WorldStateManager` restaurent les snapshots de monde.
4. Les providers `IPersistentStateProvider` réhydratent les détails de gameplay.

## Flux typique d'une partie

1. Le joueur ouvre `MainMenu.unity`.
2. Le menu crée ou charge une session.
3. Le projet charge la scène de gameplay.
4. Les managers runtime s'initialisent.
5. Le joueur reçoit un personnage contrôlable.
6. Les systèmes d'input, caméra, UI, inventaire et interactions se connectent au personnage.
7. Le joueur explore, interagit, ramasse, lit, active des braseros et modifie l'état du monde.
8. La sauvegarde et/ou la persistance réseau capturent les changements importants.

## Dépendances importantes

- Unity Input System : `Assets/PlayerInputs.cs` est généré.
- Unity Netcode for GameObjects : scripts sous `Assets/Scripts/Netcode/`.
- Unity Transport : utilisé par le Netcode.
- TextMesh Pro : utilisé par l'UI, mais le dossier TMP importé n'est pas à modifier.
- HDRP : pipeline de rendu du projet.
- NavMesh : utilisé par certains comportements de déplacement/IA.

## Risques de couplage

### Scène et code fortement liés

Plusieurs managers recherchent des objets par nom, singleton ou composants présents dans la scène. Renommer un GameObject ou déplacer un objet peut casser un système sans erreur de compilation.

### Champs sérialisés Unity

Les champs publics et `[SerializeField]` sont souvent renseignés dans l'inspecteur. Les renommer supprime la liaison côté Unity, sauf si `[FormerlySerializedAs]` est utilisé.

### Netcode et persistance

Les systèmes réseau et sauvegarde dépendent de IDs stables, de l'ordre d'initialisation et de snapshots. Une modification locale peut avoir des effets sur les clients tardifs ou les sauvegardes.

### UI runtime

La UI mélange parfois objets de scène, prefabs, fallback runtime et accès par hiérarchie. Tester en Play Mode après chaque changement est obligatoire.

### Assets tiers

Le projet contient beaucoup d'assets importés. Les modifier directement complique les mises à jour, le debug et la maintenance.

## Règle de décision

Quand tu veux modifier un système :

1. Cherche le ScriptableObject ou prefab qui porte la donnée.
2. Cherche le MonoBehaviour attaché en scène.
3. Cherche le manager qui orchestre l'action.
4. Vérifie si le Netcode ou la sauvegarde observent ce système.
5. Fais une petite modification testable.
