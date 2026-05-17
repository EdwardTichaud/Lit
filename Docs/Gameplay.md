# Lit - Gameplay

Ce document fixe la direction gameplay actuelle et sert de référence de production.
Tout système qui ne nourrit pas directement l'archéologie temporelle, les registres,
les lignées ou la lecture environnementale doit rester secondaire.

## Vision actuelle

`Lit` est un jeu d'exploration narrative et d'archéologie temporelle.

Le joueur explore un château suspendu dans une fracture du temps pour comprendre :

- comment une civilisation a vécu ;
- comment ses quartiers, chambres et registres ont changé ;
- comment les lignées se sont déplacées, croisées puis effacées ;
- pourquoi le rituel de l'an 666 a laissé le château dans cet état.

Le coeur du jeu n'est plus le combat, le puzzle abstrait ou un système de pouvoirs
complexe. Le plaisir vient de l'observation, de la comparaison de strates, de la
déduction et de la mémoire spatiale.

Le combat tour par tour reste conservé pour l'instant comme contrainte locale
d'exploration, documentée dans [TurnBasedCombat.md](TurnBasedCombat.md). Il ne
doit simplement pas prendre le pas sur l'enquête temporelle et humaine.

## Boucle principale

1. Entrer dans une zone du château.
2. Observer son état dominant.
3. Changer l'âge dominant avec un brasero quand la zone le permet.
4. Utiliser la torche temporelle pour révéler localement l'âge précédent ou suivant.
5. Comparer les traces : objets, murs, lits, portes, registres, réparations, noms.
6. Relier ces indices à des lignées, des chambres, des courants religieux ou des
   transformations humaines.
7. Consolider une compréhension narrative ou spatiale qui ouvre la suite.

Cette boucle doit rester lisible pour un solo dev : peu de systèmes lourds, beaucoup
de réutilisation de composants simples, et des données faciles à maintenir.

## Système temporel

### Braseros - âge dominant de zone

Les braseros ne sont plus seulement un compteur global de "temps du château".
La direction canonique est qu'un brasero stabilise l'âge dominant d'une zone.

Repères internes recommandés :

- Age000 ;
- Age111 ;
- Age222 ;
- Age333 ;
- Age444 ;
- Age555 ;
- Age666.

Chaque activation fait avancer ou reculer la zone d'un pas de 111 ans selon la
logique de level design. Les chiffres sont des repères internes : le joueur peut
voir des noms de périodes, des états sociaux ou des changements architecturaux
plutôt que des dates brutes.

Le système global `BraseroTimeManager` existe encore et pilote déjà des années
par nombre de braseros allumés. Il reste utile pour les scènes existantes.
Les nouveaux contenus doivent privilégier la notion d'âge temporel 0..666 et
utiliser les ponts de conversion plutôt que dupliquer le comportement.

### Torche temporelle - lecture locale

La torche ne fait pas voyager le joueur. Elle lit localement une strate voisine.

Si une zone est stabilisée à Age333, la torche peut révéler :

- Age222 ;
- Age333 ;
- Age444.

Elle sert à comprendre les transitions : réparation, condamnation, réaffectation,
mur bouché, fenêtre murée, lit ajouté, brasero déplacé, registre corrigé.

Les visions de torche par couleur restent une couche de perception secondaire.
Elles peuvent servir le langage visuel et religieux, mais ne doivent pas devenir
quatre arbres de pouvoirs à étendre sans limite.

### Objets temporels

Un objet temporel peut :

- apparaître ou disparaître selon l'âge ;
- changer de mesh, matériau ou racine visuelle ;
- activer/désactiver ses colliders ou comportements ;
- documenter une modification humaine.

La règle de scope : préférer plusieurs états simples et explicites à un automate
complexe.

## Piliers de gameplay

### 1. Registres

Les registres deviennent le coeur de l'enquête narrative.

Types utiles :

- naissances ;
- décès ;
- déplacements ;
- affectations de chambres ;
- corrections administratives ;
- listes de veille, rationnement ou entretien.

Certains noms sont rayés, déplacés, absents ou réattribués. La résolution vient
souvent de la comparaison entre un registre officiel, un objet trouvé et une
modification de chambre.

### 2. Lignées

Le joueur reconstruit des familles en reliant :

- noms ;
- parents, enfants, conjoints ;
- chambres occupées ;
- décès et causes de décès ;
- objets transmis ;
- traces de rayure, déplacement ou disparition.

Les lignées ne doivent pas devenir un arbre généalogique exhaustif obligatoire.
Elles servent à rendre le château humain et à donner du poids aux absences.

### 3. Objets transgénérationnels

Certains objets traversent plusieurs âges :

- pendentif ;
- couverture ;
- jouet ;
- bol ;
- livre ;
- outil de veille.

Le joueur peut retrouver le même objet chez plusieurs descendants ou dans plusieurs
états d'une chambre. Ces objets sont de bons fils rouges parce qu'ils évitent le
lore abstrait : un bol fêlé transmis pendant quatre générations raconte plus qu'un
discours.

### 4. Transformations humaines

Le château doit évoluer par usages humains autant que par rituel.

Exemples :

- mur condamné ;
- chambre réaffectée ;
- lit ajouté ;
- fenêtre murée ;
- passage fermé ;
- brasero déplacé ;
- registre corrigé ;
- dortoir agrandi ;
- espace de prière transformé en salle de veille.

Ces transformations doivent être lisibles dans l'environnement et dans les données
narratives.

## Combat conservé

Le système de combat existant reste dans le projet pour le moment. Sa place de
design est celle d'une tension ponctuelle : un obstacle ou une conséquence locale
qui peut coûter des ressources, isoler un personnage ou renforcer la pression
d'exploration.

Contraintes :

- ne pas en faire la boucle principale ;
- éviter d'ajouter un arbre de progression lourd ;
- garder les affrontements rares et lisibles ;
- maintenir la compatibilité avec le HUD et les managers existants ;
- relier les rencontres au lieu, aux registres ou aux transformations plutôt qu'à
  une logique de grind.

## Début du jeu

### Chambre de Maëlle

Le joueur commence dans une ancienne chambre des Espérants.

La pièce a résisté parce qu'elle a été occupée, chauffée, entretenue et répétée
pendant des siècles. Elle doit introduire la chaleur, la famille, la routine et
l'absence, pas une grande révélation.

Objets et textes à privilégier :

- couvertures reprises ;
- bols simples ;
- lits réparés ;
- traces de cire ;
- tours de brasero ;
- listes de couchage ;
- petites notes domestiques.

### Couloir de la Veillée

Après la chambre, le Couloir de la Veillée introduit la communauté :

- portes et chambres ;
- registres ;
- rotations ;
- premiers noms rayés ;
- premières réaffectations ;
- premier doute sur la cohérence administrative du château.

## Progression émotionnelle

La progression remonte depuis l'âge 666 vers l'origine.

Le joueur commence par les conséquences finales, puis comprend progressivement :

- ce qui s'est passé ;
- pourquoi les habitants ont continué ;
- comment les règles existaient depuis l'origine ;
- pourquoi personne n'avait besoin d'être un grand manipulateur actif pour que le
  système survive.

Le twist principal doit rester froid et progressif : en rallumant des braseros,
en reconnectant des circulations et en restaurant des cohérences, le joueur peut
réactiver involontairement certaines conditions du rituel interrompu. Il ne crée
pas la catastrophe ; il réaligne un système suspendu depuis 666 ans.

## Direction d'écriture

Les textes doivent souvent être :

- administratifs ;
- intimes ;
- fragmentaires ;
- quotidiens ;
- indirects.

À éviter :

- exposition massive ;
- héros prophétique ;
- grands discours ;
- phrase trop écrite ;
- révélation frontale trop tôt.

Le courant secret de Vérité ne doit pas être nommé tôt dans les textes destinés
au joueur. Il doit apparaître par contradictions : chambre réattribuée, nom gratté,
registre incohérent, objet qui ne devrait pas être là.

## Ligne directrice

`Lit` doit être un jeu d'archéologie temporelle et humaine : explorer, lire les
strates, reconstruire les lignées, comprendre les transformations du château et
ressentir l'absence d'une civilisation disparue.
