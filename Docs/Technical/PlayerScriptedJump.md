# Saut scripté des héros

Le saut jouable est implémenté par
`PlayerScriptedJumpController`, présent sur Lucian, Link, Luna et Mia. Les clips
du controller `Player_Model` sont exclusivement visuels et in-place ; UCC garde
la collision, l'inertie et la position.

## Statut de référence

Le rendu de ce système a été explicitement validé par le propriétaire du projet.
Ne pas modifier son fonctionnement, ses phases, ses clips, ses transitions, son
calcul de hauteur ou sa physique sans une demande explicite de sa part.

Les réglages ci-dessous sont les seules variations autorisées sans refonte du
contrat :

| Réglage | Effet | Valeur de référence |
| --- | --- | --- |
| `Jump Height` | Hauteur physique cible. | `5` |
| `Jump Start Takeoff Normalized Time` | Instant du décollage dans `Jump_Start`. | `0.13` |
| `Jump Start Planar Slowdown` | Fraction de la vitesse horizontale retirée au décollage. `0` conserve toute l’inertie, `1` l’annule. | `0` |
| `Backward Jump Input Dot Threshold` | Seuil de détection d’un recul en combat verrouillé. Lorsque l’input éloigne suffisamment le héros de l’ennemi locké, le controller joue `Jump_Start_Back`. | `-0.25` |

`Jump Start Planar Slowdown` est appliqué une seule fois, au même instant que
l'impulsion verticale. Il sert uniquement à régler la rupture perçue entre la
locomotion et le saut ; il ne doit ni écrire le Transform ni modifier le root
motion.

Lorsqu'un ennemi est verrouillé, toutes les phases du saut conservent le yaw
du héros vers cette cible (`Jump_Start`, `Jump_Start_Back`, boucle, chute et
atterrissage). Cette contrainte n'agit ni sur la trajectoire physique ni sur le
root motion : elle est automatiquement levée lorsque l'atterrissage se termine
ou que le saut est interrompu.

Pendant un saut scripté, le moteur de locomotion UCC est neutralisé : le
contrôleur de saut est l'unique propriétaire de la vitesse plan. Les inputs
reçus pendant l'arc sont mémorisés puis repris après l'atterrissage.

`Jump_Start_Back` est un état visuel créé et maintenu manuellement dans le
controller `Player_Model`. Son clip et son raccord vers `Jump_Loop` sont
également authorés manuellement. Le runtime ne le déclenche qu'en combat avec
une cible lockée, lorsque l'input s'éloigne de cette cible ; un paramètre
`JumpStartBackTrigger` est optionnel, non requis.

Au déclenchement d'un `Jump_Start_Back`, la norme de vitesse plan présente est
mémorisée, puis appliquée sur l'axe réel de recul de la cible lockée vers le
héros. Cette trajectoire reste figée jusqu'au handoff d'atterrissage : le héros
reste face à l'ennemi tout en s'en éloignant réellement.

## Validation

Après une migration, lancer :

`Lit > Animation > Validate Player Scripted Jump Contract`

Tester au minimum le saut immobile, le saut en course et le saut après un
demi-tour, sur les quatre héros.
