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
- Les suspensions d'Outline doivent toujours etre relachees par leur owner
  (`PopSuspension`) pour eviter un outline durablement invisible.
- Les UI d’inventaire utilisent `InputFocusStack` et des verrous de squad.
- Les interactions peuvent être masquées par `TimePeriodVisibility` ou exiger
  une influence lumineuse.
- En réseau, le client ne doit pas modifier seul un inventaire ou un objet monde.
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
- Les sauvegardes `CharacterSaveData` version 5 persistent aussi les items avec
  `CombatReactionProfile`. Au chargement d'une sauvegarde plus ancienne sans
  items combat, les defaults du personnage sont migres si les items existent
  dans l'inventaire restaure.
- Un item combat actif peut etre defensif ou porter un `CombatReactionProfile`.
  `Item_Weapon_Sword` est configure comme premier `MeleeCounterImpale` : il ne
  sert pas de bouclier, mais declenche un empalement si l'attaque ennemie est
  melee.
  `Item_Shield_WoodShield` utilise `MeleeDefense` : il bloque une attaque melee,
  perd des PV defensifs persistants entre les combats, reste reutilisable s'il
  en conserve et retire une unite de l'inventaire s'il casse. L'inventaire
  regroupe ces items par PV restants identiques et affiche leurs PV actuels/max
  sur le slot et dans la description.
