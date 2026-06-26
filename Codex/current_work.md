# Travail en cours

## Objectif actuel

Adaptation de `AncientFlameCompassUI` pour utiliser le prefab `CompassUI` :
le cadran suit la rotation de la caméra et la flèche pointe vers l'Ancient Flame
active la plus proche.

## Contraintes

Patch minimal, sans modification de scènes/prefabs/assets Unity. Réutiliser
`AncientFlameCompassUI`, `AgeManager`, `LocalPlayerContext` et la hiérarchie
existante `Compass_Render` / `Arrow` du prefab.

## Systèmes concernés

Temps et environnement : affichage local des Ancient Flames et repère cardinal
lié à la caméra.

## Notes temporaires

À tester dans Unity : `Compass_Render` tourne avec le yaw caméra, `Arrow` pointe
vers l'Ancient Flame active la plus proche, et aucune boussole n'est créée si
la scène ne fournit pas déjà l'UI.
