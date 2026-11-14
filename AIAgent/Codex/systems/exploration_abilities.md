# Compétences d'exploration

## Equipement

AbilityService reste l'autorite des deblocages, de la sauvegarde et des requetes
reseau. Aucun etat mutable n'est ajoute a CharacterData. AbilitySaveData conserve
ses entrees historiques playerId/abilityId ; plusieurs entrees pour une meme
identite portent maintenant plusieurs capacites. equipmentVersion=2 distingue
les anciennes sauvegardes : en solo, leurs capacites deja debloquees deviennent
equipables ensemble. Les sauvegardes v2 conservent les retraits volontaires.
JSON personnage et JSON inclus dans WorldSnapshot conservent toutes les entrees.

Solo : deblocage equipe automatiquement la nouvelle capacite ; le menu de pause
permet de cumuler/retirer des capacites a la Maison. Les verifications existantes
HasEquipped reconnaissent l'ensemble, pas seulement la premiere entree legacy.

Multi : l'hote repartit les capacites debloquees sans doublons, en ordre de
deblocage, entre les identites des clients presents ordonnes par clientId.
Quatre capacites donnent 4 en solo, 2/2 a deux, 2/1/1 a trois, 1/1/1/1 a quatre.
Avec moins de capacites debloquees, certains joueurs n'en ont pas encore.
Changement d'effectif ou nouveau deblocage : redistribution. A la Maison, choisir
une capacite d'un autre joueur echange une des siennes avec celle-ci. Les donnees
solo restent separees des entrees multijoueur. Ce partage n'est pas une refonte
des kits de combat : ceux-ci restent propres au personnage attribue en multi.

## Grimpeur

Grimpeur permet de s'accrocher au contact d'une prise, puis de se propulser
en sautant. Observateur peut repérer les prises. La pose utilise
`Mixamo_ClimbingIdle`, alignée
sur la face réelle du collider d'appui. Au moins quatre des cinq repères
répartis sur la hauteur de la capsule doivent rester proches de la surface
(le seuil de 80 % est une mesure de couverture, pas une aire de peau).
Le squelette ajuste le bassin, les mains et les pieds pendant la pose.
La capsule conserve ses collisions et suit la prise si elle se déplace
ou tourne ; le saut est dévié légèrement vers l'extérieur si nécessaire.
Les conditions de déblocage d'origine de Grimpeur sont conservées. L'ancien identifiant
`accrocheur` est reconnu à la lecture des sauvegardes et converti en
`grimpeur` pour les déblocages et les équipements.

La poutre du Seuil est dans `Reve_Limbes_Secrets`. Les repères
`Pose_suspendue` et `Prise` visent le flanc plat du socle du pilier couché,
avec un rayon d'accrochage de 0,75 m autour de la pose. Le scanner de
session suffit : aucun scanner local ni Observateur ne sont requis.
L'appui mobile est synchronisé avant le contrôle physique. Une fois tenu,
le balayage ignore le déplacement propre du support, mais la capsule
finale reste contrôlée contre celui-ci et contre le décor.

Le grappin et sa compétence Bondisseur ont été retirés. Les anciennes
entrées de Bondisseur sont ignorées à la restauration. L'accès à la statue
dans les Limbes utilise le portail existant après les cinq cycles, sans
exiger Observateur ni Grimpeur.

## Saut de lumière

Les anciens passages `LadderController` sont des sauts entre deux points
lumineux. Ils conservent le déplacement et les effets existants. Le prompt
indique la direction du saut ou la compétence manquante. Le passage exige
Saut de lumière **équipé par le personnage qui voyage** ; l'hôte valide
également la demande en multijoueur. Les règles de visibilité, de proximité
et de combat restent en place. La compétence est disponible dès le départ
pour garder le parcours existant accessible, et utilise la répartition et
la sauvegarde standard des compétences.
