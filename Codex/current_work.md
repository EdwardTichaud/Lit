# Travail en cours

## Objectif actuel

Reprise du traversal d'échelle pour placer le personnage au bon point haut/bas
selon sa position et l'orienter avec une rotation calculée depuis l'axe de
l'échelle.

## Contraintes

Patch minimal, sans modification de scènes/prefabs/assets Unity. Réutiliser
`LadderController`, `LadderInteractable`, `SquadCharacterController` et le
traversal scripté UCC existant.

## Systèmes concernés

Input, UCC et mouvement : interaction d'échelle, traversal scripté et placement
runtime du personnage.

## Notes temporaires

À tester dans Unity : utilisation depuis le bas, depuis le haut, depuis chaque
côté accessible d'une échelle auto-détectée, puis host/client si la scène
utilise les interactions réseau.
