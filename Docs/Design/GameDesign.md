# Lit — Game design et narration interactive

Ce document rassemble la direction gameplay, les priorités de production et le
modèle de données narratives. Le lore de référence est
[Lore.md](Lore.md).

## Vision

`Lit` est désormais pensé autour de deux expériences complémentaires :

- une campagne solo entièrement narrative ;
- un mode coopératif fortement rejouable.

Ces deux expériences utilisent le même château, le même lore et les mêmes
mécaniques de base. Le mode coopératif n'est pas une campagne différente : c'est
une autre manière de parcourir le château.

Le cœur du jeu reste l'exploration, la compréhension des événements passés, la
résolution des mémoires des fantômes et la progression jusqu'à l'an `000`.

Le plaisir recherché vient de :

- l'observation ;
- la comparaison de traces entre époques ;
- la déduction ;
- la mémoire spatiale ;
- la compréhension progressive de vies humaines ;
- l'optimisation collective du parcours en coopératif.

## Structure narrative

Dans la campagne solo, le joueur contrôle Lucian. Lucian fait partie des
Explorateurs, un groupe de demi-dieux incarnant chacun un concept. Lucian incarne
l'Espoir.

Le jeu débute dans un hub où Lucian échange avec les autres Explorateurs. Ce hub
sert à :

- présenter les personnages ;
- développer progressivement leurs relations ;
- introduire la mission.

L'histoire principale reste celle du château. Les Explorateurs ne deviennent
réellement importants que progressivement : ils encadrent le point de vue du
joueur, mais ne doivent pas prendre le pas trop tôt sur les familles, les
registres, les fantômes et les événements du château.

## Progression temporelle

L'exploration commence dans la strate `666`, puis progresse par paliers de `111`
ans :

```text
666 -> 555 -> 444 -> 333 -> 222 -> 111 -> 000
```

L'objectif principal est d'atteindre la strate `000`. Ces années ne constituent
pas un voyage dans la chronologie historique : les Explorateurs parcourent les
strates nées de la fracture, plusieurs siècles après le siège.

L'implémentation actuelle distingue :

- `AgeManager`, source runtime globale calculant l'année depuis les Ancient
  Flames allumées ;
- `TemporalAge`, grille de production `Age000`, `Age111`, `Age222`, `Age333`,
  `Age444`, `Age555`, `Age666` ;
- `TemporalZone`, couche locale explicite disponible pour le level design.

Tous les systèmes temporels utilisent des pas de `111` ans. Les nouveaux
contenus doivent lire `AgeManager` comme source runtime et éviter de supposer
qu'une Flame pilote automatiquement une `TemporalZone` : l'échelle est commune,
mais la zone locale reste une couche explicitement configurée.

## Boucle principale

1. Entrer dans une zone ou un district du château.
2. Observer l'état temporel présenté.
3. Utiliser les portails, les Flames communes et les changements d'époque pour
   rendre le monde navigable et interactif.
4. Découvrir des registres, documents, objets ou lieux significatifs.
5. Acquérir des connaissances persistantes.
6. Relier personnes, familles, chambres, quartiers et événements.
7. Utiliser ces connaissances auprès d'un fantôme ou d'une interaction.
8. Résoudre une mémoire humaine et ouvrir une nouvelle piste.
9. Allumer une Ancient Flame pour avancer vers l'an `000`.

Une enquête réussie doit produire une compréhension, pas seulement une clé
abstraite.

## Piliers

### Château, familles et registres

Les familles restent l'unité émotionnelle principale du récit. Une lignée n'a pas
besoin d'être exhaustive : elle doit contenir uniquement les personnes et
relations utiles à une histoire jouable.

Les premiers axes canoniques sont :

- Belmont : gardiens des Veillées, usure d'un héritage dont le sens disparaît ;
- Ardent : fonction narrative à définir, distincte de celle des Belmont.

Les registres contiennent les naissances, décès, changements de quartier,
affectations de chambres et événements importants. Le gameplay vient du
croisement entre :

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

### Connaissances

Les connaissances remplacent complètement les réponses libres.

Le joueur découvre des registres, lit des documents, retrouve des objets et
observe des lieux. Lorsqu'une source significative est découverte, la
connaissance correspondante est automatiquement acquise. Le joueur n'est pas
obligé de lire intégralement chaque document pour que le système comprenne qu'il
possède l'information.

Les connaissances servent ensuite à résoudre les fantômes, débloquer certaines
interactions narratives et matérialiser la mémoire de l'enquête.

Le système officiel est exclusivement fondé sur `KnowledgeSO`,
`KnowledgeManager`, `KnowledgeRequirement` et les réactions configurées dans
`GhostData`.

### Fantômes

Chaque fantôme représente une mémoire incomplète. Il ne demande jamais une
réponse libre : il attend une information.

Lorsque Lucian possède la bonne connaissance, le fantôme comprend enfin ce qui
lui est arrivé, puis disparaît ou s'apaise selon la mise en scène. Les fantômes
constituent le cœur de la progression narrative.

Une enquête de fantôme doit rester fixe et lisible dans la première version :

- question ou souvenir incomplet ;
- deux à cinq preuves pertinentes ;
- connaissance requise ;
- apaisement et conséquence narrative ou spatiale.

### Flames communes

Les Flames communes servent uniquement à rendre le monde interactif. Elles
permettent par exemple :

- d'ouvrir des portes ;
- d'activer des mécanismes ;
- de rendre certains objets utilisables ;
- de rendre des fantômes interactifs.

Chaque époque possède son propre état des Flames communes. Une Flame allumée en
`666` n'est pas automatiquement allumée en `555`.

### Ancient Flames

Les Ancient Flames sont des mécanismes du rituel interrompu. Elles modifient la
strate temporelle globale ; chaque Ancient Flame fait avancer la progression :

```text
666 -> 555 -> 444 -> 333 -> 222 -> 111 -> 000
```

Elles ne servent jamais directement à révéler des objets. Seules les Flames
marquées `ancientFlame` participent au calcul de `AgeManager`.

### Conséquences temporelles

Les actions effectuées dans une strate peuvent produire des conséquences dans une
autre. Exemple canonique :

1. Une Flame inaccessible en `555` devient accessible uniquement en `444`.
2. Le joueur l'allume en `444`.
3. En revenant ensuite en `555`, cette Flame est déjà allumée.
4. Cette conséquence permet d'interagir avec des objets auparavant inaccessibles.

Le gameplay temporel repose donc sur des conséquences entre strates, pas sur un
simple changement cosmétique de décor. Le lien qui produit ces conséquences reste
volontairement inexpliqué : il ne doit pas être présenté comme une réécriture du
passé.

### Portails

Les portails relient les districts et les zones importantes du château. Leur
intérêt principal vient de leur combinaison avec les époques.

Selon l'année :

- une salle existe ;
- une salle est détruite ;
- une porte est fermée ;
- un couloir est effondré.

Les portails deviennent donc des outils de navigation temporelle. Ils ne doivent
pas seulement réduire les trajets : ils doivent permettre de penser le château
comme un espace transformé par le temps.

### Combat

Le combat ne cherche pas à rivaliser avec les JRPG modernes. Il doit rester :

- simple ;
- nerveux ;
- spectaculaire.

Les combats sont réguliers et participent à la boucle d'exploration : défendre
une trace, sécuriser un trajet, alimenter une épreuve, délivrer un Savoir
narratif ou modifier la circulation d'une zone. Ils ne doivent ni exiger de
farming, ni devenir du remplissage aléatoire.

Les ennemis apparaissent notamment lorsque :

- le joueur se trouve hors des zones éclairées ;
- le joueur choisit volontairement d'éteindre une Flame.

Le joueur prend donc un risque calculé.

### Progression du joueur et boss

Le joueur devient plus puissant grâce à certains équipements, certains objets et
quelques récompenses de boss. Le jeu ne doit jamais devenir un jeu de farming.

Les boss servent à conclure certaines quêtes ou certains événements importants.
Ils peuvent protéger une connaissance ou un objet important. Ils représentent les
grands moments du jeu.

### Coopération

Le mode coopératif conserve exactement le même objectif : atteindre l'an `000`.
En revanche, les joueurs cherchent à optimiser leur progression.

Ils rejouent afin de :

- essayer d'autres compositions ;
- obtenir de meilleurs objets ;
- optimiser leur parcours ;
- aller plus vite.

La rejouabilité vient principalement de la coopération, des rôles complémentaires
et des décisions de parcours, pas d'une génération procédurale de campagne.

### Personnages jouables et spécialisations

Le jeu ne doit pas proposer uniquement quatre personnages. L'objectif est de
viser six à huit personnages jouables afin de créer davantage de compositions
d'équipe, de synergies et de rejouabilité.

Spécialisations validées :

- Manipulateur de Flames : peut allumer ou éteindre les Flames à une distance
  beaucoup plus importante, atteindre certaines Flames autrement et éviter
  certains détours.
- Observateur des anomalies temporelles : peut voir certaines anomalies
  invisibles pour les autres personnages. Ces anomalies représentent des
  incohérences entre plusieurs époques. Ce ne sont pas des raccourcis ; elles
  servent surtout à fournir davantage d'informations aux joueurs.
- Médium : dispose d'environ `33 %` de chance de comprendre immédiatement le
  besoin d'un fantôme, même sans posséder encore la connaissance normalement
  requise. Cette spécialisation introduit une légère part d'aléatoire et
  récompense certaines compositions d'équipe.

## Philosophie générale

Le jeu ne repose pas sur :

- le grind ;
- les arbres de compétences ;
- la génération procédurale ;
- les statistiques complexes.

Le cœur de `Lit` reste :

- l'exploration ;
- les enquêtes ;
- les familles ;
- les registres ;
- les fantômes ;
- la temporalité.

Les combats servent à créer de la tension et à transformer les parcours. Les
spécialisations servent à renouveler les parties multijoueur. Les connaissances
servent à raconter l'histoire. Les époques servent à transformer la manière
d'explorer le château.

La coopération ne consiste pas simplement à jouer à plusieurs : elle doit offrir
une expérience différente grâce aux rôles complémentaires, aux compositions
d'équipe et à l'optimisation du parcours jusqu'à l'an `000`.

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

`Item.knowledgeUnlockedOnRead` peut débloquer une connaissance. Côté design, la
connaissance doit être acquise dès que la source pertinente est découverte ou
ouverte, sans exiger que le joueur lise intégralement le texte affiché. Un ancien
readable sans métadonnées doit rester valide.

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

## Fantômes validés

### Luc

Luc est le premier fantôme rencontré. Sa fonction est de servir de tutoriel.

Luc cherche Jon, son chat. Le joueur trouve son collier à clochette, puis une
petite tombe portant une clochette semblable. En revenant voir Luc, il comprend
enfin ce qui est arrivé à Jon, puis disparaît.

Cette quête est obligatoire. Sa récompense est la disparition d'un mur permettant
de continuer l'aventure.

### Ulrich

Ulrich est le gardien d'une partie extérieure du château. Il a vu arriver l'armée
lors du siège de l'an `666` et ignore être mort.

Le joueur retrouve son cadavre ainsi que son journal. Le journal débloque une
connaissance. En revenant voir Ulrich, il comprend enfin sa mort, puis disparaît.

### Marcellin

Marcellin est un ancien horloger rencontré très tôt dans l'aventure, en année
`666`. Sa quête est entièrement optionnelle.

Le joueur retrouve progressivement plusieurs pièces de son horloge dans
différentes époques. Une fois toutes les pièces retrouvées, Lucian revient en
`666` et répare l'horloge.

Marcellin refuse cette réparation, car il considère que ce travail aurait dû être
le sien. Il devient alors un boss. Après sa défaite, il accepte enfin sa mort.

La récompense est une montre à gousset permettant de ralentir temporairement le
temps pendant les combats. Cette quête récompense les joueurs qui pensent à
revenir sur une ancienne quête plusieurs heures plus tard.

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

### Luc, première enquête de fantôme

Objectif : démontrer la boucle complète sans exposition massive et valider le
tutoriel obligatoire.

Le joueur doit :

1. rencontrer une mémoire incomplète ;
2. comprendre que Luc cherche Jon ;
3. récupérer le collier de Jon ;
4. consulter la petite tombe de Jon ;
5. revenir voir Luc ;
6. faire disparaître le mur qui bloque la progression.

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

1. La campagne solo narrative autour de Lucian et du château.
2. La progression temporelle `666 -> 555 -> 444 -> 333 -> 222 -> 111 -> 000`.
3. Une enquête de fantôme obligatoire résolue par connaissances.
4. Une conséquence temporelle lisible entre deux époques.
5. Des Flames communes utiles aux interactions, séparées des Ancient Flames.
6. Des portails dont l'intérêt dépend des époques.
7. Une famille complète et jouable.
8. Un registre utile à plusieurs indices.
9. Un objet transgénérationnel.
10. Les premières spécialisations coopératives validées.

## Hors priorité

- crafting massif ;
- construction et amélioration de bâtiments ;
- arbres de pouvoirs ;
- arbre généalogique générique ;
- génération procédurale d'enquêtes ;
- campagne coopérative séparée ;
- farming d'équipement ou d'expérience ;
- statistiques complexes ;
- réponses en texte libre ;
- encyclopédie exhaustive du château ;
- détails définitifs du rituel avant validation narrative.
