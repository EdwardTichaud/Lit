# Travail en cours

## Objectif actuel

Feedback visuel discret ajouté côté code pour relier une Ancient Flame proche
aux Flames éteintes les plus proches. Validation Unity à effectuer.

## Contraintes

Patch minimal, runtime client-local, sans modification de scènes/prefabs/assets
Unity. Réutiliser `Flame`, `AgeManager`, `LocalPlayerContext` et
`LocalPlayerUtils`.

## Systèmes concernés

Temps et environnement : `Flame`, `AgeManager`, feedback local des Ancient Flames.

## Notes temporaires

À tester dans Unity : proximité d’une Ancient Flame, arc or vers l’Ancient Flame
éteinte la plus proche, arc blanc vers la Flame commune éteinte la plus proche,
solo puis host/client.
