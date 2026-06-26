# Travail en cours

## Objectif actuel

Rendre le suivi de Munin moins attaché à une position relative fixe au
personnage, afin que son avance sur les déplacements soit respectée.

## Contraintes

Patch minimal. Corriger le suivi dans `MuninController` sans modifier scènes ou
prefabs, et préserver la résolution de Munin comme enfant logique du personnage.

## Systèmes concernés

Squad/personnages : `MuninController` estime la vitesse du personnage à partir
du Rigidbody quand il est fiable, sinon depuis le delta de position du Transform.

## Notes temporaires

À tester dans Unity : marcher, courir et changer brusquement de direction avec
Munin actif, vérifier qu'il prend de l'avance sans rester plaqué à un offset
fixe, puis déclencher une interaction de flamme et vérifier son retour.
