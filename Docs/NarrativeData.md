# Lit - Données narratives

Ce document décrit les structures de données légères à utiliser pour soutenir la
vision actuelle sans créer de système lourd.

## Readable Items

Les `Item` ScriptableObjects restent la base des documents lisibles. Les nouveaux
champs de métadonnées narratives servent à classer un readable sans casser les
assets existants :

- âge associé ;
- quartier ;
- lignée ;
- courant religieux ;
- niveau de révélation ;
- tags narratifs.

Ces champs sont optionnels. Un vieux readable sans métadonnées reste valide.

## Registres

Créer des `RegistryEntry` pour les entrées importantes seulement. Il n'est pas
nécessaire de modéliser chaque habitant du château.

Types prioritaires :

- naissance ;
- décès ;
- déplacement ;
- habitation ;
- correction ;
- veille ;
- ration ;
- entretien.

Une entrée peut pointer vers un readable ou un objet associé quand cela aide le
level design.

## Lignées

Créer des `FamilyRecord` quand un personnage ou une famille sert une enquête.

Statuts utiles :

- normal ;
- rayé ;
- déplacé ;
- disparu ;
- non renseigné.

Les parents, enfants et conjoints peuvent être renseignés par identifiants texte
au début. Ne pas construire un arbre généalogique complet avant d'avoir un besoin
clair en gameplay.

## Objets transgénérationnels

Créer un `TransgenerationalObjectRecord` pour les objets qui reviennent à plusieurs
âges : pendentif, couverture, jouet, livre, bol, outil.

Un bon objet transmis doit pouvoir répondre à trois questions :

- qui l'a possédé ;
- où le joueur le retrouve ;
- ce que son état révèle sur le passage du temps.

## Transformations humaines

Les modifications humaines peuvent rester de simples tags sur un `TemporalObject`
ou dans une note de registre.

Tags recommandés :

- mur condamné ;
- chambre réaffectée ;
- lit ajouté ;
- fenêtre murée ;
- brasero déplacé ;
- passage fermé ;
- registre corrigé.

Ne créer une classe plus complexe que si plusieurs systèmes doivent réellement
interroger ces transformations.
