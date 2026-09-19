# Interactions et inventaire

## Rôle

Détecter une cible monde, afficher son Outline, exécuter son interaction et
gérer loot, inventaire, lecture, placement et actions contextuelles.

## Classes principales

- `CharacterInteractionDetection` : résolution, collider, portée et visibilité.
- `SquadCharacterController.Interactions` : collecte et sélection locale.
- `RuntimeOutlineSelectionManager` / `RuntimeOutlineTarget` : Outline unique,
  avec suspension temporaire possible par un systeme comme `BattleTransition`.
- `RuntimeOutlineInspector` sur `GameplaySessionRoot/OutlineManager` : activation,
  couleur et epaisseur dans l'Inspector ; diagnostic en lecture seule de la cible,
  des renderers selectionnes et des suspensions. L'objet persiste avec la session
  dans DontDestroyOnLoad. Les reglages durables se font sur le prefab hors Play.
  Le materiau fullscreen est copie pour la session et restaure a la desactivation.
  Le seuil de transparence du masque est reglable (0.1 par defaut). RuntimeOutlineTarget
  transmet texture, tiling/offset et opacite via MaterialPropertyBlock ; le shader
  tient aussi compte de l'alpha des sommets des ParticleSystems. Le shader GlitteringStars
  utilise le canal rouge de MainTex pour Alpha : detection automatique de ce cas.
  Les shaders personnalises peuvent choisir le canal et le nom de propriete texture
  sur la cible. Les effets proceduraux/dissolve du shader source ne sont pas reproduits.
- `ICharacterDetectedInteractable` / `ILocalInteractHandler` : contrats.
- `InteractableItem` : conteneurs, objets récupérables, serrures et pièges.
- `InventoryPanelController` : UI, dépôt, lecture et placement.
- `NetworkInventory` / `WorldInteractionService` : autorité réseau.
- `SquadCharacterController` porte aussi les 3 items defensifs actives pour le
  combat, separes de l'inventaire complet.
- `CharacterRuntimeState`, possédé par `SquadManager`, conserve cet état entre
  despawn/spawn et sert de fallback de sauvegarde; il ne vit jamais dans
  `CharacterData`.

## Flux principaux

1. Le personnage effectue un `OverlapSphere`.
2. Chaque collider est résolu vers un interactable et validé par portée,
   direction, visibilité et règles temporelles.
3. La cible retenue est la cible valide ayant la plus haute priorité, puis la
   plus proche à priorité égale; l’ancien `SwitchTarget` ne force plus une
   cible manuelle.
4. La cible reçoit le personnage détecté et devient l’Outline actif.
   Si une suspension globale est active, la cible reste suivie mais son outline
   est masque jusqu'a la restauration.
5. `Interact` appelle d’abord le handler actif, puis ouvre l’UI ou demande une
   mutation au serveur.
6. En combat, l'inventaire ne s'ouvre pas au-dessus du HUD. Pendant la réaction
   défensive du tour ennemi, le choix passe par `CombatDefensePanel` et par les
   actions `UseItem1/2/3`; la validation, la suppression/casse et la
   synchronisation restent côté autorité.

## Pièges observés

- Un seul propriétaire doit contrôler l’Outline global.
- Un `RuntimeOutlineTarget` ne change que le layer de son propre GameObject,
  jamais celui de ses enfants. Chaque renderer à entourer doit donc porter sa
  propre cible. `RuntimeOutlineRendererReference` ne fait que sélectionner un
  renderer déjà équipé ; aucun `RuntimeOutlineTarget` n'est créé automatiquement
  en jeu ou par les outils de configuration.
- Les suspensions d'Outline doivent toujours etre relachees par leur owner
  (`PopSuspension`) pour eviter un outline durablement invisible.
- Les UI d’inventaire utilisent `InputFocusStack` et des verrous de squad.
- Les interactions peuvent être masquées par `TimePeriodVisibility` ou exiger
  une influence lumineuse.
- En réseau, le client ne doit pas modifier seul un inventaire ou un objet monde.
- Un livre, parchemin ou note posé dans le monde est d'abord lu : ses knowledges
  de lecture sont alors révélés, même si le joueur choisit ensuite de le laisser.
  Pendant cette lecture, **A / SouthButton** le prend et ferme le panneau ;
  **B / EastButton** ferme simplement le panneau. Seul A ajoute l'item à
  l'inventaire via le chemin serveur existant. L'UI auteur doit fournir
  `ReadableActionInputs`, avec ses enfants `A` et `B` ; il est réutilisé pour
  livre et parchemin, jamais créé au runtime. Les stèles restent des lectures
  fixes et non récupérables.
- Tous les fantômes suivent la révélation de proximité de `GhostController` :
  hors de leur rayon ils sont invisibles, sans interaction ni outline ; en
  approchant ils retrouvent progressivement leur présentation et leur contrat
  de détection. Les adaptateurs narratifs ne peuvent modifier que leurs
  préconditions de scénario, pas contourner cette distance.
- La `planche de bois` est un item défensif à 1 PV : une unité absorbe jusqu'à
  1 dégât de l'attaque ennemie puis est retirée si elle casse.
- Le Building legacy est désactivé via
  `Resources/LegacyBuildingSystemSettings.asset`; conserver ses données pour les
  anciennes sauvegardes.

## Notes recentes

- Une `AncientFlame` a moins de 8 metres d'un ennemi temps reel vivant peut etre
  allumee mais devient bleue et inerte. Son etat est conserve pour la sauvegarde,
  tandis que sa revelation, son influence, ses activations et son effet temporel
  restent suspendus jusqu'a la disparition de l'ennemi.
  Seuls les EnemyController actifs avec CombatEnabled peuvent la neutraliser ;
  un fantome avant son combat ou un controleur desactive ne bloque pas le brasero.
- Le brasero de Maelle (`scene-flame:Maison:E6C2F108`) est une exception
  narrative : il conserve toujours son influence pour activer Necklace et la
  porte de sa chambre, meme lorsqu'un ennemi s'approche.
- La porte de Maelle (`Moon_Room_Door_Maelle`) porte son propre
  `RuntimeOutlineTarget` sur le GameObject qui rend son mesh. Après la
  récupération de Necklace, sa priorité est évaluée normalement et elle devient
  la cible locale interactive.
- Les objets et portes soumis a la flamme verifient aussi sa portee effective
  directement si aucune notification physique n'a encore ete recue. Une flamme
  eteinte, neutralisee ou desactivee ne satisfait pas cette verification. Les
  MeshColliders non convexes utilisent leurs bounds pour le calcul de proximite.

- Pendant le gel d'entree combat, `BattleTransition` suspend
  `RuntimeOutlineSelectionManager` pour masquer les outlines monde encore actifs
  (par exemple un brasero vise juste avant le combat), puis restaure l'etat a la
  fin de la transition.
- Toute sortie de combat, y compris une fuite, annule l'action de Lucian et
  restaure l'etat de locomotion propre au combat avant de relancer la detection
  locale. La presentation quitte explicitement son animation de combat au sol,
  sans remplacer la mort, le vol ou une traversee. Aucun arret differe,
  repositionnement ou StopAllAbilities n'est lance apres le relachement du stick.
  Un resultat masque libere son propre mode UI. Le diagnostic CombatExit indique
  le mode input, le focus, les verrous squad/UCC et la capacite bloquant la detection.
- Hors combat, l'ActionBox d'inventaire permet d'ajouter/retirer un item
  defensif des 3 items combat actives. Ces ids sont synchronises par
  `NetworkInventory` et sauvegardes dans `CharacterSaveData`.
- En combat, un item defensif non assigne aux 3 items combat reste dans le sac,
  mais n'est pas a portee de main pour la reaction ennemie.
- L'input inventaire est ignore pendant une session de combat locale ou quand
  `CombatDefensePanel` est visible; NorthButton doit alors passer par
  `UseItem1`.
- Les actions `UseItem1`, `UseItem2` et `UseItem3` selectionnent
  respectivement les trois items combat actifs pendant que
  `CombatDefensePanel` est visible. Le dernier choix remplace les precedents et
  reste le seul resolu a l'impact; le slot choisi est surligne et agrandi. Les
  libelles `EnableItem_1/2/3/Text` affichent les noms de ces items. Ces racines
  sont aussi des boutons UI, et les slots sans item assigne restent masques.
- L'affichage des 3 slots lit les items combat assignes sans les purger si
  l'inventaire runtime n'est pas encore initialise; l'utilisation reste ensuite
  refusee par `CombatSessionManager` si l'item n'est pas reellement dans
  l'inventaire.
- L'application des starter items preserve les 3 items combat preassignes, puis
  les revalide apres ajout des objets de depart.
- Les sauvegardes `CharacterSaveData` persistent les items avec
  `CombatReactionProfile`; les trois assignations combat sont restaurees depuis
  la sauvegarde, sans default mutable sur `CharacterData`.
- Un item combat actif peut etre defensif ou porter un `CombatReactionProfile`.
  `Item_Weapon_Sword` est configure comme premier `MeleeCounterImpale` : il ne
  sert pas de bouclier, mais declenche un empalement si l'attaque ennemie est
  melee.
  `Item_Shield_WoodShield` utilise `MeleeDefense` : il bloque une attaque melee,
  perd des PV defensifs persistants entre les combats, reste reutilisable s'il
  en conserve et retire une unite de l'inventaire s'il casse. L'inventaire
  regroupe ces items par PV restants identiques et affiche leurs PV actuels/max
  sur le slot et dans la description.

## References des panneaux monde

InventoryUISettings porte explicitement les prefabs d'information item et
batiment (Bootstrap/Arena). BuildingInfoInteractable les reutilise aussi en build,
et reprend la resolution si l'UI arrive apres l'objet. Les chemins AssetDatabase
restent un secours editeur. Les identifiants runtime d'influence utilisent EntityId
dans les collections ; les identifiants persistants des objets sont inchanges.

## Nettoyage des commandes de lecture

La fermeture du panneau de lecture ne cherche jamais ReadableActionInputs dans
la scene. Elle masque uniquement sa reference memorisee a l'ouverture, si elle
existe encore. Pendant OnDisable, elle ne change pas son parent. Cela evite les
recherches globales et changements de hierarchie pendant le demontage de scene.

## Libelles d'interaction dans le monde

Les prompts de GhostController, LadderController, StabReading,
HubCompanionSwapTrigger, LabyrinthStartTrigger, ReturnHomeTrigger et TrouEtroit
utilisent tous UI/World/UI_World_InteractionBox.prefab via
WorldInteractionUiSettings.Create. L'asset Resources/WorldInteractionUiSettings
reference le prefab pour fonctionner egalement en build et en scene isolee.
L'apparence (police, taille, couleur, echelle) se regle sur ce prefab ; les
libelles explicites et dynamiques restent geres par les interactifs. Les anciennes
references interactionBox servent seulement a reprendre leur texte, sans reprendre
leur style. Sans reference locale, le texte du prefab commun sert de defaut.
Les instances affichent leur CanvasGroup et ne capturent pas les raycasts UI.
Les anciens fallbacks qui construisaient du TMP a la volee ont ete retires.
Pour ajouter un nouveau prompt monde, utiliser cette meme fabrique.
Validation : compilation C# runtime/editeur reussie. WorldInteractionUiTests
verifie le style commun, le libelle historique, la visibilite et la preservation
du prefab source ; test ajoute et compile, non execute dans Unity.
En Play : verifier Monter/Descendre aux deux bouts d'une ladder, Ecouter sur un
fantome, lire une stele, les quatre triggers, puis eloignement/changement de scene.