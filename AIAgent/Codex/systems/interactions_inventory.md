# Interactions et inventaire

## Rôle

Détecter une cible monde, afficher son Outline, exécuter son interaction et
gérer loot, inventaire, lecture, placement et actions contextuelles.

## Classes principales

- `CharacterInteractionDetection` : résolution, collider, portée et visibilité.
- `SquadCharacterController.Interactions` : collecte et sélection locale.
- `RuntimeOutlineSelectionManager` / `RuntimeOutlineTarget` : Outline unique,
  avec suspension temporaire possible par un systeme comme `BattleTransition`.
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
3. La cible retenue est toujours la cible valide la plus proche; l’ancien
   `SwitchTarget` ne force plus une cible manuelle.
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
- Les objets et portes soumis a la flamme verifient aussi sa portee effective
  directement si aucune notification physique n'a encore ete recue. Une flamme
  eteinte, neutralisee ou desactivee ne satisfait pas cette verification. Les
  MeshColliders non convexes utilisent leurs bounds pour le calcul de proximite.

- Pendant le gel d'entree combat, `BattleTransition` suspend
  `RuntimeOutlineSelectionManager` pour masquer les outlines monde encore actifs
  (par exemple un brasero vise juste avant le combat), puis restaure l'etat a la
  fin de la transition.
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
