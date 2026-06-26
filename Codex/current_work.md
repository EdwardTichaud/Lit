# Travail en cours

## Objectif actuel

Ajout d'une state Animator dédiée au franchissement automatique d'obstacle bas.

## Contraintes

Patch minimal. Réutiliser le franchissement scripté existant dans
`LitOpsiveLocomotionBridge` et limiter les changements Unity au controller
Animator et à la valeur sérialisée nécessaire du prefab joueur.

## Systèmes concernés

Input/UCC/caméra : `LitOpsiveLocomotionBridge` déclenche le trigger Animator
`ObstacleTraversal`, qui entre dans la state `Obstacle_Traversal`.

## Notes temporaires

À tester dans Unity : franchir un obstacle bas, vérifier que la state
`Obstacle_Traversal` est déclenchée puis revient à `Locomotion`, et assigner le
clip sur cette state quand il sera disponible.
