# Premier labyrinthe de fracture — objectif et plan de production

Date : 2026-11-01.
Statut : plan de prototype. Les décisions de direction sont validées ; le lieu,
le contenu proposé et ses paramètres ne deviennent pas canoniques par ce document.

Références : [GameDesign](GameDesign.md#expéditions-dans-les-labyrinthes-de-fracture),
[Lore](Lore.md#les-labyrinthes-de-fracture),
[suivi narratif et connaissances](../../AIAgent/Codex/systems/narrative.md).

## 1. Objectif de la livraison

Créer un premier labyrinthe auteur, jouable intégralement avec Lucian seul ou
en coopération jusqu'à quatre joueurs, permettant de choisir entre atteindre
rapidement un boss puissant et consacrer du temps à préparer son affrontement.

La livraison doit démontrer une boucle complète :

**Explorer → combattre/résoudre → comprendre et partager un Savoir → ouvrir
l'accès → choisir les préparatifs → éclairer et affronter le boss → recevoir
une récompense → pouvoir recommencer.**

Le prototype doit prouver trois choses avant extension :

1. La connaissance du lieu modifie réellement les choix de parcours.
2. Jouer à plusieurs permet des stratégies différentes, pas seulement davantage
   de dégâts ou quatre tâches indépendantes.
3. Aller vite et préparer le combat sont deux options défendables ; aucune
   n'écrase systématiquement l'autre.

Ne pas considérer la livraison comme terminée sur la seule compilation ou
sur des tests de composants : le parcours complet doit avoir été joué.

## 2. Contraintes déjà actées

- Les labyrinthes sont des manifestations d'anomalies de l'espace-temps issues
  de la fracture existante du Château. Leur reformation justifie les reprises.
- La reformation ne remet à zéro ni la campagne, ni les recrutements, ni les
  Savoirs, ni les résolutions narratives.
- L'équipe partage une époque unique, même lorsque ses membres se séparent.
- Le boss accumule une puissance latente dans l'obscurité. La lumière arrête
  cette croissance sans effacer automatiquement la puissance acquise.
- L'accumulation n'est pas son activation : une influence de Flame reste
  nécessaire pour rendre une Gargouille ennemie active et affrontable.
- Le boss est battable sans préparatif, y compris seul et au plafond de puissance
  prévu pour le prototype. Les préparatifs n'éliminent pas le combat.
- Les combats sont réguliers et ont une fonction identifiable ; aucun farming
  ni tirage aléatoire n'est nécessaire à la progression.
- Un Savoir est partagé immédiatement et annoncé avec le nom de son révélateur.
- En solo, Lucian reste le seul acteur physique. Les supports déjà recrutés
  restent accessibles selon la progression ; aucun recrutement n'est offert
  implicitement par le labyrinthe.
- Aucune énigme essentielle n'exige des présences simultanées ou une spécialisation.
- La première découverte n'exige pas un bon score. Les reprises chronométrées
  mesurent le labyrinthe ET le combat, jusqu'à la défaite effective du boss.
- La victoire donne une récompense garantie ; la performance donne un bonus
  collectif, sans réserver de contenu essentiel aux meilleurs temps.

## 3. Périmètre du premier prototype

Livrer un secteur interconnecté comprenant :

- une entrée de fracture et une sortie utilisables dans les deux modes ;
- un espace central servant de repère, trois espaces d'exploration et une arène ;
- un Savoir transversal avec au moins trois applications locales ;
- une pièce mécanique de tentative nécessaire à l'accès, acquise de façon garantie ;
- deux préparatifs facultatifs indépendants ;
- deux raccourcis réversibles côté circulation, déverrouillés pour la tentative ;
- des rencontres de combat placées pour protéger une pièce, une installation
  ou un trajet, sans respawn exploitable pendant la tentative ;
- un gardien, une croissance de puissance lisible et plafonnée ;
- un bilan collectif et une reformation de tentative sans perte de progression.

Hors périmètre : génération procédurale, nouveau district complet, plusieurs
boss, nouveaux personnages, nouvel arbre de progression, économie infinie de
loot/XP, cinématiques de production et refonte générale du combat ou du réseau.

Le nom du lieu, son emplacement dans la campagne et l'identité du gardien sont
à choisir parmi les éléments compatibles du projet. Réutiliser un rig de boss
existant ne signifie pas faire revivre un personnage narratif déjà résolu.

## 4. Proposition de parcours concret — contenu à valider en graybox

### Architecture

Utiliser provisoirement les noms fonctionnels suivants :

```text
Entrée ── Cour centrale ── Galerie ── Seuil ── Arène
              │              │          │
           Archives ── Salle des machines
              └──── raccourci vers la Cour
```

Ce schéma décrit les connexions, pas des couloirs imposés. La galerie peut être
en surplomb de la cour ; des vues sur l'arène et des conduites visibles relient
visuellement les espaces. Le trajet Archives–Machines et le retour vers la cour
créent une boucle. L'échelle et les distances sont à mesurer en jeu.

### Situation initiale

Depuis la cour, le joueur voit le seuil fermé, une commande à symboles et une
conduite menant vers l'arène encore sombre. Un indice compréhensible signale
que le gardien se renforce dans l'obscurité. Les directions sont reconnaissables
par leur architecture, leurs sons et leurs repères, pas seulement par des marqueurs.

Le joueur choisit d'abord entre chercher le sens des commandes aux archives,
sécuriser la pièce dans la galerie ou reconnaître la salle des machines.
Trouver le mécanisme avant le Savoir doit permettre d'en mémoriser la fonction
et de formuler une question, pas seulement d'afficher « clé manquante ».

### Objectif principal

Dans la galerie, une rencontre protège une pièce de réparation garantie.
La récupérer puis réparer la commande du seuil est obligatoire. L'ouverture
demande aussi de comprendre et manipuler le réseau décrit par le Savoir.
Une fois ces conditions réunies, le boss est accessible : aucun préparatif
facultatif n'est nécessaire pour ouvrir son arène.

La pièce reste un état collectif de tentative : une déconnexion ou la mort
de son porteur ne doit pas rendre la partie insoluble. La représentation en
inventaire doit être décidée après examen du système existant, sans imposer
un nouvel inventaire partagé global.

### Savoir proposé : « Lire le réseau »

| Aspect | Proposition du prototype |
|---|---|
| Acquisition | Aux archives, comparer un schéma incomplet à des conduites observables pour reconstituer la fonction de trois symboles. |
| Épreuve | Observation et déduction ; pas un code aléatoire à deviner ni une longue lecture chronométrée. |
| Apprentissage réel | Distinguer une arrivée, une dérivation et une coupure ; suivre un circuit malgré ses passages entre salles. |
| Usage principal | Interpréter et régler la commande réparée du seuil. |
| Réutilisations | Identifier la dérivation d'une protection du boss et comprendre le mécanisme ouvrant un raccourci. |
| Solo | Revenir à une commande repérée plus tôt ou rejoindre les machines par la boucle, plutôt que refaire toute la branche. |
| Multi | Un joueur aux commandes peut agir dès la révélation faite aux archives par un autre ; le second décrit ce qu'il voit et l'équipe choisit le circuit. |
| Persistance | Le Savoir reste acquis ; les commandes et réparations de la tentative se reforment. Un usage ailleurs dans le Château est une extension, pas une dépendance de cette livraison. |

Le partage automatique donne accès à l'interprétation, pas à la résolution
automatique. Les symboles doivent être observables et les actions locales rester
nécessaires. Si le Savoir est déjà possédé, ne pas imposer de refaire l'épreuve
des archives : ce gain de compréhension fait partie de la rejouabilité.

### Préparatif A : dériver la protection

Dans la salle des machines, lire le circuit puis effectuer une manipulation
persistante pour la tentative. L'installation est protégée par une rencontre
courte. Réussir retire une séquence de protection du boss ou en réduit la
fréquence, sans désactiver ses attaques fondamentales.

Le joueur choisit de payer ce détour pour obtenir davantage de fenêtres offensives.
Un circuit allumé, un changement visible dans l'arène et un état HUD confirment
l'effet, y compris pour les joueurs restés ailleurs.

### Préparatif B : interrompre les renforts

Une alcôve accessible depuis la galerie contient le relais d'appel de renforts.
Sécuriser son accès et neutraliser le relais empêche une arrivée de renforts
identifiable pendant le boss. Le détour ajoute du risque et du temps, mais
réduit la dispersion de l'attention pendant l'affrontement.

Les préparatifs A et B sont indépendants ; aucun n'est la clé de l'autre.
Le choix porte sur ce que l'équipe redoute ou maîtrise, pas sur une réduction
automatique identique des PV.

### Raccourcis et retours

La sécurisation de la galerie ouvre un retour direct vers la cour. Le réseau
compris permet d'ouvrir la liaison Archives–Machines sans refaire la galerie.
Le déverrouillage bénéficie immédiatement à toute l'équipe et fonctionne dans
les deux sens lorsque la géométrie le permet. Aucun joueur séparé ne doit être
enfermé par l'action d'un autre sans issue de secours.

## 5. Lumière, engagement et croissance du boss

Contrat proposé pour le prototype, à confirmer avant implémentation :

1. Le lancement volontaire de la tentative, validé par l'équipe, initialise le
   temps de performance et l'accumulation. Les chargements et la préparation
   hors tentative n'augmentent pas la puissance.
2. La croissance utilise un nombre limité de paliers lisibles, avec plafond.
   Elle ne dépend pas de l'horloge réelle pendant que le jeu est fermé.
3. Une fois le seuil ouvert, un dispositif local de Flame permet l'engagement.
   L'apport de lumière au gardien déclenche le combat : il n'existe pas de
   bouton gratuit permettant de figer sa puissance puis de repartir tout préparer.
4. L'engagement est une décision d'équipe annoncée avant activation, pas une
   conséquence accidentelle d'un projectile lumineux ou du passage d'une torche.
   Il n'exige pas de téléportation ni quatre interrupteurs simultanés.
5. La Flame d'arène reste alimentée jusqu'à la résolution de la rencontre.
   L'éteindre pour suspendre le boss ne fait pas partie de ce premier prototype.
6. Les préparatifs acquis restent effectifs au palier de puissance atteint.
   La lumière ne remet pas le palier à zéro. Le score continue pendant le combat.

Présenter les paliers par des manifestations du gardien et un état partagé,
pas seulement un pourcentage de PV supplémentaire. Chaque attaque doit rester
lisible et esquivable. Pas de vitesse d'animation arbitrairement illisible au
dernier palier. Les valeurs et modificateurs restent à régler sur le vrai combat.

En découverte, ne pas classer ni diminuer la récompense de victoire à cause du
temps ; utiliser une croissance plafonnée et accueillante. Le détail de ce
profil est une hypothèse à tester, pas une dispense de la règle de lumière.

Aucune Ancient Flame n'est ajoutée ou réaffectée au dispositif local.

## 6. Parcours et décisions à 1, 2, 3 et 4 joueurs

| Joueurs | Parcours à vérifier | Décision intéressante |
|---|---|---|
| 1 | Lucian découvre le Savoir, sécurise la pièce, ouvre le seuil puis choisit zéro, un ou deux préparatifs avec les raccourcis. | Compenser ses difficultés face au boss ou accepter son état actuel pour éviter un détour. |
| 2 | Un joueur aux archives pendant que l'autre sécurise la galerie ; entraide possible aux machines. | Gagner du temps séparément ou sécuriser ensemble un combat avant de se répartir. |
| 3 | Deux joueurs sécurisent une branche pendant qu'un autre comprend le réseau ; après ouverture, répartition entre les deux préparatifs. | Réaffecter de l'aide à celui qui rencontre une difficulté plutôt que laisser un joueur attendre. |
| 4 | Deux binômes, groupe complet ou exploration distribuée ; révélation des symboles utilisable à distance et raccourcis partagés. | Mettre plus de monde sur le chemin critique ou investir dans la préparation, sans supposer que quatre solos sont toujours optimaux. |

Un joueur seul doit pouvoir combattre, manipuler et rejoindre l'arène sans
support spécifique imposé. Les mêmes obstacles restent présents en multi ;
ne pas les remplacer par des portes exigeant un nombre donné de joueurs.
Les combats et les modifications croisées doivent rendre l'entraide utile.
Le joueur qui révèle le Savoir ou prépare le boss reçoit le même résultat
collectif que celui qui porte le dernier coup.

## 7. Chronomètre, récompenses et reformation

Séparer trois états : progression persistante de campagne, état partagé de
tentative et présentation locale. Réutiliser les services existants après audit,
sans supposer que le trigger d'entrée actuel fournit déjà un système d'expédition.

- La victoire est validée une seule fois par tentative et arrête le chrono.
- Le bilan indique temps total, préparatifs réalisés, puissance à l'engagement
  et bonus de performance ; il ne récompense pas les dégâts individuels.
- La récompense de victoire est garantie, même au-delà du dernier palier de temps.
- Les paliers de bonus diffèrent selon le nombre de participants et sont fixés
  après mesures, pas en divisant simplement le temps solo par quatre.
- L'XP et le loot réutilisent les destinataires et pipelines existants s'ils
  conviennent. Tant que l'économie n'est pas validée, utiliser une configuration
  de test explicitement séparée, pas créer des gains de campagne non maîtrisés.
- Une nouvelle tentative réinitialise accès, pièce, combats, préparatifs et
  puissance du gardien, mais pas les Savoirs, recrutements ou faits narratifs.
- La reformation est volontaire après résolution/abandon ; elle ne relance
  pas une partie automatiquement et ne reconstitue pas le labyrinthe au milieu
  d'une tentative d'autres joueurs.

À trancher au jalon de cadrage : abandon, défaite collective, reconnexion,
arrivée tardive, pauses et reprise de sauvegarde. En multi, aucun menu local
ne doit arrêter seul les horloges partagées. Un changement d'effectif ne doit
ni réinitialiser le chrono ni permettre de changer opportunément de barème.
Ne pas promettre une reprise au milieu du labyrinthe sans persister tout son état.

## 8. Ordre de réalisation et critères de sortie

### Jalon 0 — Audit et contrat de tentative

Identifier l'emplacement, la strate, le boss compatible, les services de session,
Savoirs, interactions, combat, récompenses, réseau et sauvegarde réutilisables.
Décider les questions de cycle de vie listées ci-dessus et documenter les points
non supportés. Aucune nouvelle fonctionnalité ne doit être déclarée existante
sur la seule présence d'une classe portant un nom similaire.

**Sortie :** parcours et dépendances dessinés, contenu validé, contrat d'entrée/
fin/reprise défini, liste de réutilisation et écarts techniques explicites.

### Jalon 1 — Graybox et parcours solo sans score

Construire uniquement la géométrie utile, les collisions et les connexions.
Poser pièce, commande, Savoir et raccourcis avec interactions provisoires.
Réutiliser locomotion, sauts et interactions sans refonte de leurs systèmes.

**Sortie :** Lucian peut entrer, comprendre, ouvrir le seuil, visiter les deux
préparatifs et sortir. Les trajets sont lisibles et sans blocage ; aucune
énigme n'exige un deuxième acteur. Mesurer les retours avant d'ajouter du décor.

### Jalon 2 — Combats, boss et lumière

Raccorder les rencontres, les deux effets de préparation et la croissance
plafonnée du gardien. Ajouter les signaux nécessaires pour comprendre les choix.

**Sortie :** victoires possibles avec zéro, un et deux préparatifs ; vérifier
aussi le plafond de puissance sans préparatif. Pas de combat supprimé par un
réglage de lumière ni de progression dépendant d'un loot aléatoire.

### Jalon 3 — État partagé et coopération

Faire du serveur l'autorité de tentative, des horloges, de la puissance, des
interactions validées, de la mort du boss et de la récompense. Réutiliser
l'autorité existante des Savoirs et sa notification du révélateur.
Conserver une exécution locale directe en solo et éviter deux règles de gameplay.

**Sortie :** host/client puis équipes de trois et quatre ; découverte à distance,
activation simultanée d'un mécanisme, synchronisation des préparatifs et absence
de doublon d'objets/récompenses. Les clients voient le même palier et la même époque.
Tester les cas de cycle de vie retenus au jalon 0, y compris la déconnexion du
joueur ayant acquis la pièce et celle de l'hôte selon le support réel du projet.

### Jalon 4 — Bilan, reformation et persistance

Ajouter chrono, bilan, configuration des récompenses et relance de tentative.
Comparer découverte sans Savoir et reprise avec Savoir déjà acquis.

**Sortie :** deux tentatives successives sans état résiduel ; une seule récompense
par victoire ; aucune perte de progression persistante et aucun replay forcé
d'un événement narratif déjà résolu. Vérifier Domain Reload activé et désactivé.

### Jalon 5 — Équilibrage et acceptation jouée

Faire jouer les parcours direct, préparation A, préparation B et préparation
complète à 1/2/3/4 joueurs. Relever temps d'exploration, retours, combats,
préparatifs, combat final, défaites et périodes d'attente entre joueurs.

**Sortie :** aucun parcours ne domine dans toutes les compositions et tous les
niveaux de maîtrise ; un novice peut terminer sans exigence de score ; un joueur
qui comprend mieux le lieu prend des décisions différentes lors de la reprise.
Les seuils chiffrés de temps et récompense ne sont fixés qu'après cette mesure.
Si le rush domine, revoir coût/effet des préparatifs avant d'allonger artificiellement
le trajet principal. Si tout préparer domine, réévaluer les bénéfices cumulés.

## 9. Vérification finale et preuve de livraison

- Compilation Unity et tests ciblés sur états, autorité, déduplication et resets.
- Parcours humain complet au clavier/manette en solo, puis host/client et à 3/4.
- Boss au plafond, avec et sans préparatifs ; collision/retour de contrôle après combat.
- Appui simultané, Savoir déjà connu, pièce acquise puis déconnexion, échec et relance.
- Horloges et récompenses identiques pour tous ; aucune pause locale exploitable.
- Époque commune et absence de téléportation/regroupement forcé par un changement d'âge.
- Rapport indiquant version testée, conditions, temps observés, résultats et limites.

Une compilation réussie, des tests automatiques ou une victoire obtenue avec des
états préremplis ne prouvent pas seuls que cette boucle est jouable ou équilibrée.

## 10. Définition de « terminé »

Le premier labyrinthe est terminé lorsqu'une équipe de un à quatre joueurs peut
faire une tentative complète, comprendre les conséquences de ses choix,
vaincre le gardien, recevoir un résultat collectif fiable et recommencer sans
perdre sa progression ; les preuves de validation et les limites restantes
sont consignées. Il est recommandé de n'étendre la production à plusieurs
labyrinthes qu'après acceptation de cette première boucle.
