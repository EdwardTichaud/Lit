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

`Jump Start Planar Slowdown` est appliqué une seule fois, au même instant que
l'impulsion verticale. Il sert uniquement à régler la rupture perçue entre la
locomotion et le saut ; il ne doit ni écrire le Transform ni modifier le root
motion.

## Validation

Après une migration, lancer :

`Lit > Animation > Validate Player Scripted Jump Contract`

Tester au minimum le saut immobile, le saut en course et le saut après un
demi-tour, sur les quatre héros.
