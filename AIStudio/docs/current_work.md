# Travail en cours

## Objectif actuel

Ajouter un système extensible de surfaces pour les bruits de pas et futurs
feedbacks contextuels.

## Contraintes

Patch minimal de code et documentation : ne pas modifier les scènes, prefabs ou
assets Unity.

## Systèmes concernés

Surfaces/audio : `SurfaceDefinition`, `SurfaceProvider`, `SurfaceResolver` et
`SurfaceFootstepPlayer` résolvent la surface sous le personnage et jouent les
pas via `AudioManager`.

## Notes temporaires

À tester dans Unity : créer quelques assets `SurfaceDefinition` par type de
surface, poser des `SurfaceProvider` sur des sols, ajouter
`SurfaceFootstepPlayer` au personnage joueur avec une surface par défaut, puis
vérifier que les clips changent selon le sol raycasté.
