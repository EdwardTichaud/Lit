# Lit - Idées actives

Ce document ne garde que les pistes compatibles avec la direction actuelle. Les
pistes contradictoires ont été retirées pour éviter de mélanger les priorités de
production.

Références principales :

- [Lore.md](Lore.md) pour la bible narrative ;
- [Gameplay.md](Gameplay.md) pour les piliers jouables ;
- [NarrativeData.md](NarrativeData.md) pour les données de registres, lignées et
  objets transmis.

## Priorité de production

`Lit` est un jeu d'exploration narrative et d'archéologie temporelle.

Les priorités actives sont :

- lire les strates temporelles du château ;
- comprendre les transformations des lieux ;
- reconstruire des lignées à partir de registres et d'objets ;
- suivre les traces humaines ordinaires, avec des affrontements rares quand ils
  servent l'exploration ;
- maintenir un scope réaliste pour un solo dev.

## Tranches de contenu utiles

### Chambre de Maëlle

Objectif : introduire chaleur, famille, absence et stabilité domestique.

Contenus utiles :

- lit réparé plusieurs fois ;
- couvertures reprises ;
- bol ou pendentif transmis ;
- liste de couchage ;
- note domestique courte ;
- brasero ou trace de chauffe qui explique pourquoi la chambre a mieux résisté.

### Couloir de la Veillée

Objectif : introduire communauté, organisation sociale, registres et premiers
trous administratifs.

Contenus utiles :

- portes de chambres numérotées ;
- registre d'affectation ;
- registre de décès ou de déplacement ;
- nom rayé sans explication ;
- chambre réattribuée trop vite ;
- objet qui relie deux occupants éloignés dans le temps.

### Première zone de comparaison temporelle

Objectif : montrer clairement le trio `TemporalZone` / brasero / torche.

Contenus utiles :

- état dominant contrôlé par un brasero ;
- torche révélant l'âge précédent ou suivant ;
- mur condamné visible à un âge mais pas à l'autre ;
- registre qui donne une raison administrative ;
- objet transmis qui confirme l'histoire humaine.

## Système temporel

Les âges internes actifs sont :

- Age000 ;
- Age111 ;
- Age222 ;
- Age333 ;
- Age444 ;
- Age555 ;
- Age666.

Les braseros stabilisent l'âge dominant d'une zone. La torche lit localement
l'âge précédent, courant ou suivant. Le joueur ne voyage pas dans le temps : il
lit les strates du château.

Les chiffres sont des repères de production. En jeu, ils peuvent être remplacés
par des noms de périodes, des états sociaux ou des changements architecturaux.

## Données narratives

Les prochains contenus doivent privilégier des données simples :

- `TemporalReadableMetadata` pour relier un texte à un âge, un quartier, une
  lignée, un courant religieux et des tags narratifs ;
- `LineageRecord` / `FamilyRecord` pour préparer les familles ;
- `RegistryEntry` pour les naissances, décès, déplacements et affectations ;
- `TransgenerationalObjectRecord` pour suivre les objets transmis ;
- `HumanModificationTag` pour nommer les transformations humaines visibles.

Règle de production : un petit registre clair, une chambre transformée et un
objet récurrent valent mieux qu'une grande explication frontale.

## Systèmes existants à garder stables

Le projet contient déjà du Netcode, des readables, des visions de torche, des
braseros et des systèmes de persistance. Les nouveaux contenus doivent s'appuyer
sur ces bases quand c'est raisonnable.

Le combat tour par tour est aussi conservé pour le moment. Sa documentation de
référence reste [TurnBasedCombat.md](TurnBasedCombat.md). Il doit rester un
système secondaire, utile pour la tension et les conséquences locales, sans
déplacer le centre du jeu hors de l'archéologie temporelle.

Principes :

- ne pas supprimer un composant référencé par une scène sans migration Unity ;
- éviter les nouveaux frameworks ;
- préférer des MonoBehaviours et ScriptableObjects lisibles ;
- garder les nouveaux champs optionnels pour ne pas casser les assets existants ;
- documenter les ponts temporaires directement dans les docs de production.

## Direction d'écriture

Les textes doivent rester :

- administratifs ;
- intimes ;
- fragmentaires ;
- quotidiens ;
- indirects.

Le courant secret de Vérité ne doit pas être nommé tôt. Il doit émerger par
contradictions : noms grattés, chambres réattribuées, registres incompatibles,
objets trouvés au mauvais endroit, notes prudentes.

## Notes de scope

À éviter comme priorités de production :

- combat central ou rencontres nombreuses ;
- crafting massif ;
- arbres de pouvoirs complexes ;
- exposition encyclopédique ;
- puzzles abstraits déconnectés des lieux ;
- dépendances lourdes pour des données qui peuvent rester simples.
