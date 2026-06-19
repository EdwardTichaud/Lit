# Lit — Game design et narration interactive

Ce document rassemble la direction gameplay, les priorités de production et le
modèle de données narratives. Le lore de référence est
[Lore.md](Lore.md).

## Vision

`Lit` est un jeu d'exploration narrative et d'archéologie temporelle. Le joueur
reconstitue la vie du château par ses familles, ses lieux, ses registres, ses
objets transmis et ses mémoires incomplètes.

Le cœur du jeu n'est pas le combat, le puzzle abstrait ou un arbre de pouvoirs.
Le plaisir recherché vient de :

- l'observation ;
- la comparaison de traces ;
- la déduction ;
- la mémoire spatiale ;
- la compréhension progressive de vies humaines.

## Boucle principale

1. Entrer dans une zone du château.
2. Observer les traces visibles et l'état temporel présenté.
3. Révéler ou comparer d'autres traces grâce aux systèmes disponibles.
4. Lire des registres et retrouver des objets.
5. Relier personnes, familles, chambres, quartiers et événements.
6. Acquérir des connaissances persistantes.
7. Utiliser ces connaissances auprès d'un fantôme ou d'une interaction.
8. Résoudre une histoire humaine et ouvrir une nouvelle piste.

Une enquête réussie doit produire une compréhension, pas seulement une clé
abstraite.

## Piliers

### Familles et lignées

Les familles sont l'unité principale du récit. Une lignée n'a pas besoin d'être
exhaustive : elle doit contenir uniquement les personnes et relations utiles à
une histoire jouable.

Les premiers axes canoniques sont :

- Belmont : gardiens des Veillées, usure d'un héritage dont le sens disparaît ;
- Ardent : fonction narrative à définir, distincte de celle des Belmont.

### Registres

Les registres contiennent les naissances, décès, changements de quartier,
affectations de chambres et événements importants.

Le gameplay vient du croisement entre :

- une entrée officielle ;
- un lieu ;
- un objet ;
- une autre époque ou trace ;
- une mémoire de fantôme.

Il n'est pas nécessaire de modéliser chaque habitant du château.

### Objets transgénérationnels

Un objet transmis doit relier plusieurs personnes ou périodes. Exemples :
pendentif, couverture, jouet, livre, bol ou outil de Veillée.

Un bon objet répond à trois questions :

- qui l'a possédé ;
- où et dans quel état le joueur le retrouve ;
- ce que son parcours révèle de la famille.

### Fantômes et connaissances

Les fantômes sont des mémoires incomplètes. Ils posent une question, rejouent un
regret ou restent attachés à un événement.

Le joueur ne saisit pas une réponse libre. Il découvre des `KnowledgeSO`, puis le
fantôme réagit aux connaissances disponibles via un `KnowledgeRequirement`.

Une enquête de fantôme doit rester fixe et lisible dans la première version :

- question ou souvenir incomplet ;
- deux à cinq preuves pertinentes ;
- connaissance requise ;
- apaisement et conséquence narrative.

Les réponses libres textuelles ont été retirées du projet. Le système officiel
est exclusivement fondé sur `KnowledgeSO`, `KnowledgeManager`,
`KnowledgeRequirement` et les réactions configurées dans `GhostData`.

### Lecture temporelle

Les strates temporelles servent à comparer les transformations humaines du
château : chambre réaffectée, mur condamné, lit ajouté, passage fermé, flame
déplacé ou registre corrigé.

Le joueur ne doit pas avoir l'impression de voyager librement dans le temps. Il
lit des états du château.

L'implémentation actuelle distingue :

- `AgeManager`, source runtime globale calculant `666 - 111` ans par Flame
  ancien allumé ;
- `TemporalAge`, grille de production `Age000`, `Age111`, `Age222`, `Age333`,
  `Age444`, `Age555`, `Age666` ;
- `TemporalZone`, couche locale explicite disponible pour le level design.

Tous les systèmes temporels utilisent désormais des pas de 111 ans. Les nouveaux
contenus doivent lire `AgeManager` comme source runtime et éviter de supposer
qu'un Flame pilote automatiquement un `TemporalZone` : l'échelle est commune,
mais la zone locale reste une couche explicitement configurée.

### Révélation par lumière

La lumière et l'âge sont deux systèmes séparés.

`FlameLightReceiver`, `DissolveRevealSystem` et `DissolveRevealTarget` contrôlent
la visibilité d'objets via `_DissolveAmount`. Ils ne doivent pas modifier l'âge.

Munin, les flammes et les Flames classiques restent des sources de lumière ou
d'interaction. Seuls les Flames marqués `ancientFlame` participent au calcul
de `AgeManager`.

### Charges de Munin

La lumière est une récompense de compréhension, pas une ressource récupérable
en déplaçant l'obscurité.

Boucle cible :

Explorer → trouver des preuves → débloquer des connaissances → résoudre ou
apaiser une mémoire → recharger Munin → explorer plus loin.

Équilibrage de base :

- maximum initial : 10 charges ;
- allumage d'une `Flame` légère : 1 charge ;
- allumage d'une grande `Flame` ou `AncientFlame` : 2 charges ;
- extinction : aucune charge rendue ;
- Éclat de Mémoire : +1 ;
- Mémoire Apaisée : +3 ;
- Autel de Veillée : recharge complète, avec usage rare ou cooldown.

L'extinction peut toujours replonger une zone dans le noir et déclencher des
conséquences narratives ou des Ombres. Elle ne doit jamais devenir une recharge
d'urgence.

Les récompenses doivent venir d'un fait compris : exploration, objet important,
connaissance, enquête familiale, lignée complétée, fantôme apaisé ou lieu de
Veillée. Les `KnowledgeRequirement` permettent de configurer ces liens sans
coder les récompenses dans chaque système narratif.

### Combat

Le combat tour par tour est conservé comme tension ponctuelle :

- rencontres rares ;
- coût ou conséquence locale ;
- aucun grind ;
- aucun arbre de progression lourd ;
- lien avec un lieu, une famille ou une enquête quand c'est possible.

Il ne doit pas déplacer le centre du jeu hors de l'archéologie humaine.

## Données narratives

### Readable Items

Les `Item` ScriptableObjects restent la base des documents lisibles. Les
métadonnées optionnelles peuvent relier un readable à :

- un âge ;
- un quartier ;
- une famille ou lignée ;
- un courant ;
- un niveau de révélation ;
- des tags narratifs.

`Item.knowledgeUnlockedOnRead` peut débloquer une connaissance. Un ancien readable
sans métadonnées doit rester valide.

### KnowledgeSO

Un `KnowledgeSO` représente un fait que le joueur a réellement appris.

Champs structurants :

- `knowledgeId` stable ;
- titre et description ;
- catégorie ;
- type de source ;
- tags ;
- liens vers personne, famille, chambre, quartier, objet, readable ou âge.

La possession des connaissances reste centralisée dans `KnowledgeManager` et
persistée par `PersistentKnowledgeState`.

### KnowledgeRequirement

Une interaction peut demander :

- toutes les connaissances d'une liste ;
- au moins une connaissance d'une liste ;
- une ou plusieurs catégories ;
- un ou plusieurs tags ;
- un nombre minimal de connaissances d'une catégorie ou d'un tag.

Cette combinaison suffit pour représenter une compréhension implicite sans créer
un second moteur de déduction.

### GhostData

`GhostData` porte la question et les réactions possibles. Chaque
`GhostKnowledgeReaction` contient un `KnowledgeRequirement` et peut débloquer une
connaissance ou déclencher un effet de scène.

L'état « mémoire apaisée » est persisté par `PersistentGhostState`. Il ne doit pas
être confondu avec les faits connus du joueur.

### Registres et familles

Créer des `RegistryEntry` uniquement pour les entrées utiles à une enquête.
Créer des `FamilyRecord` ou `LineageRecord` uniquement pour les familles
effectivement jouées.

Les identifiants texte sont acceptables tant que le contenu reste limité. Un
graphe généalogique générique n'est pas une priorité.

### Objets transmis

Créer un `TransgenerationalObjectRecord` lorsqu'un objet revient dans plusieurs
traces et que sa transmission fait partie de l'enquête.

### Transformations humaines

Préférer un tag ou un état simple sur `TemporalObject` avant de créer une nouvelle
classe. Exemples :

- mur condamné ;
- chambre réaffectée ;
- lit ajouté ;
- fenêtre murée ;
- flame déplacé ;
- passage fermé ;
- registre corrigé.

## Première tranche de contenu

### Une chambre familiale

Objectif : introduire chaleur, absence et continuité domestique.

Contenu recommandé :

- lit réparé ;
- couverture reprise ;
- objet transmis ;
- liste de couchage ;
- note domestique ;
- trace de Veillée ou d'entretien.

Le nom « chambre de Maëlle » existe déjà dans certains contenus, mais l'identité
exacte de cette Maëlle et son lien avec une famille canonique doivent être
clarifiés avant d'en faire un pilier du lore.

### Un couloir de Veillée

Objectif : introduire la communauté, les registres et une première incohérence.

Contenu recommandé :

- chambres numérotées ;
- registre d'affectation ;
- changement de quartier ;
- nom rayé ou corrigé ;
- objet reliant deux générations ;
- trace des gardiens de la Veillée, idéalement liée aux Belmont.

### Une première enquête de fantôme

Objectif : démontrer la boucle complète sans exposition massive.

Le joueur doit :

1. rencontrer une mémoire incomplète ;
2. identifier une personne ou une famille ;
3. consulter un registre ;
4. retrouver un objet ou une chambre ;
5. acquérir la connaissance pertinente ;
6. apaiser le fantôme.

## Direction d'écriture

Les textes doivent être :

- administratifs ;
- intimes ;
- fragmentaires ;
- quotidiens ;
- indirects.

À éviter :

- exposition encyclopédique ;
- prophéties détaillant toute la vérité ;
- grand manipulateur omniscient ;
- héros prophétique ;
- révélation frontale du rituel ;
- puzzle sans lien avec une personne ou un lieu.

## Priorités

1. Une famille complète et jouable.
2. Un registre utile à plusieurs indices.
3. Un objet transgénérationnel.
4. Une enquête de fantôme résolue par connaissances.
5. Une comparaison temporelle lisible.
6. La validation en jeu de la progression temporelle par pas de 111 ans.

## Hors priorité

- combat central ;
- crafting massif ;
- construction et amélioration de bâtiments ;
- arbres de pouvoirs ;
- arbre généalogique générique ;
- génération procédurale d'enquêtes ;
- réponses en texte libre ;
- encyclopédie exhaustive du château ;
- détails définitifs du rituel avant validation narrative.
