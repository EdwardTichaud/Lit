# Interactions et inventaire

## Rôle

Détecter une cible monde, afficher son Outline, exécuter son interaction et
gérer loot, inventaire, lecture, placement et actions contextuelles.

## Classes principales

- `CharacterInteractionDetection` : résolution, collider, portée et visibilité.
- `SquadCharacterController.Interactions` : collecte et sélection locale.
- `RuntimeOutlineSelectionManager` / `RuntimeOutlineTarget` : Outline unique.
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
5. `Interact` appelle d’abord le handler actif, puis ouvre l’UI ou demande une
   mutation au serveur.
6. En combat, pendant la réaction défensive du tour ennemi, l'inventaire peut
   passer au-dessus du focus du HUD. Seuls les items marqués défensifs sont alors
   routés vers `CombatSessionManager`; la suppression/casse reste validée côté
   autorité.

## Pièges observés

- Un seul propriétaire doit contrôler l’Outline global.
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

- Hors combat, l'ActionBox d'inventaire permet d'ajouter/retirer un item
  defensif des 3 items combat actives. Ces ids sont synchronises par
  `NetworkInventory` et sauvegardes dans `CharacterSaveData`.
- En combat, un item defensif non assigne aux 3 items combat reste dans le sac,
  mais n'est pas a portee de main pour la reaction ennemie.
- Les actions `UseItem1`, `UseItem2` et `UseItem3` consomment respectivement les
  trois items combat actifs; les libelles `EnableItem_1/2/3/Text` affichent les
  noms de ces items.
- Un item combat actif peut etre defensif ou porter un `CombatReactionProfile`.
  `Item_Weapon_Sword` est configure comme premier `MeleeCounterImpale` : il ne
  sert pas de bouclier, mais declenche un empalement si l'attaque ennemie est
  melee.
