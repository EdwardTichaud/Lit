# Travail en cours

## Objectif actuel

Faire passer la caméra gameplay de la scène `Maison` entièrement par UCC.

## Contraintes

Patch minimal de scène : conserver la `Main Camera` UCC, sortir la caméra des
anciens pivots legacy et ajouter explicitement le binder UCC.

## Systèmes concernés

Input/UCC/caméra : `Opsive.UltimateCharacterController.Camera.CameraController`
et `LitUccCameraCharacterBinder` doivent être les seuls pilotes gameplay de la
`Main Camera`.

## Notes temporaires

À tester dans Unity : lancer `Maison`, vérifier que la `Main Camera` bind le
personnage local via UCC, que les inputs caméra fonctionnent, et que les anciens
pivots `CameraAnchor/YawPivot/PitchPivot` restent inactifs.
