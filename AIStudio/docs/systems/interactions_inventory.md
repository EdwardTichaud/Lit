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

## Flux principaux

1. Le personnage effectue un `OverlapSphere`.
2. Chaque collider est résolu vers un interactable et validé par portée,
   direction, visibilité et règles temporelles.
3. La cible retenue est toujours la cible valide la plus proche; l’ancien
   `SwitchTarget` ne force plus une cible manuelle.
4. La cible reçoit le personnage détecté et devient l’Outline actif.
5. `Interact` appelle d’abord le handler actif, puis ouvre l’UI ou demande une
   mutation au serveur.

## Pièges observés

- Un seul propriétaire doit contrôler l’Outline global.
- Les UI d’inventaire utilisent `InputFocusStack` et des verrous de squad.
- Les interactions peuvent être masquées par `TimePeriodVisibility` ou exiger
  une influence lumineuse.
- En réseau, le client ne doit pas modifier seul un inventaire ou un objet monde.
- Le Building legacy est désactivé via
  `Resources/LegacyBuildingSystemSettings.asset`; conserver ses données pour les
  anciennes sauvegardes.
