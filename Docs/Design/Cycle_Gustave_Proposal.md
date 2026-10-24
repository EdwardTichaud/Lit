# Gustave — « Ce que la lumière garde »

Statut : **proposition à valider, pas du lore canonique ni un cycle implémenté**.
Décision humaine du 24 octobre 2026 : proposer un cycle complet plutôt que conserver uniquement la note.

## Point de départ vérifié

`District_1_Cycle_Gustave` contient actuellement une note : « Tout semble étrange ici, à commencer par cette lumière… ». Elle ne possède ni définition de cycle ni contrôleur de quête. Le nom de l'asset ne prouve pas à lui seul l'identité de son auteur, son époque, son métier ou sa parenté.

Le canon établit que la lumière rend accessibles des mémoires de la fracture, que les habitants ont réellement vécu dans le château et que les cycles peuvent être facultatifs. Il ne fixe pas encore le destin de Gustave.

## Proposition de jeu

Un **petit cycle facultatif d'observation**, environ 8–12 minutes lors d'une première découverte. Pas de nouveau boss, pas de nouvelle Ancient Flame, pas de collecte de nombreux objets. Deux preuves conservables et deux observations de lumière remplacent le parcours systématique « trois documents → fantôme → boss ».

Le joueur trouve la note dans une chambre apparemment abandonnée. Deux petits espaces voisins, déjà intégrés au décor permanent, gardent les traces d'usages domestiques différents. En les éclairant, il observe des fragments incomplets : une table préparée, une place de travail, un objet remis à sa place. Le registre des affectations permet de rapprocher ces traces des personnes qui ont occupé les lieux.

L'enjeu n'est pas de découvrir un laboratoire secret : **comprendre qu'un lieu aujourd'hui incohérent a été habité, réutilisé et aimé**. La lumière ne remonte pas le temps et ne garantit pas une reconstitution exacte. Les apparitions sont des fragments de fracture.

### Place proposée pour Gustave — à approuver

Gustave serait un habitant qui avait entrepris de consigner ce qu'il voyait, sans connaître l'origine du château. Son métier, son courant, ses dates et sa famille resteraient indéfinis. Deux rapprochements d'écriture et d'objet permettraient d'attribuer la note à Gustave ; cette attribution ne doit pas être affirmée avant que ces preuves soient écrites et validées.

**Pas de nouveau fantôme obligatoire.** Sa présence peut rester portée par ses annotations et un bref fragment de mémoire. On ne décide donc pas implicitement de sa mort, de son salut ou de sa parenté. Si un fantôme est souhaité, son regret et son destin devront faire l'objet d'une validation narrative distincte.

## Parcours concret

1. Lire ou récupérer la note existante, à portée de 2 mètres. Lucian constate que quelqu'un s'interrogeait déjà sur la lumière.
2. Explorer deux petites pièces dans n'importe quel ordre. La torche et/ou les Flames communes existantes permettent de révéler chaque fragment. Une simple traversée accidentelle ne valide pas l'observation : interaction volontaire sur la trace visible.
3. Lire le registre local d'affectation, deuxième et dernière preuve collectable. On peut le trouver avant les fragments ; aucun ordre artificiel n'est imposé.
4. Revenir à la table de départ et examiner ensemble les traces. Une courte présentation, relançable si interrompue, rapproche les usages du lieu et les annotations de Gustave. Le joueur comprend une histoire locale, pas le rituel.
5. Lucian conclut, puis une petite alcôve d'archives propre à cette enquête devient accessible. La résolution est persistante et le cycle peut être déchargé sans refermer l'accès.

L'alcôve n'est ni un verrou de campagne ni une nouvelle zone entière. Sa récompense doit être utile mais facultative : un petit lot de ressources déjà utilisées par l'inventaire et une connaissance de lieu consultable. **Objets précis et quantités à choisir après audit de l'économie**, sans inventer une nouvelle monnaie ou une Ability obligatoire.

## Définition proposée

ID futur : `district1.gustave`. Catégorie facultative selon l'enum existant. Aucun prérequis à un autre cycle ; accès organisé physiquement.

| Jalon proposé | Condition |
| --- | --- |
| `light_note_read` | Lecture effective de la note existante |
| `first_room_observed` | Observation volontaire de la première trace visible |
| `second_room_observed` | Observation volontaire de la seconde trace visible |
| `room_register_read` | Lecture du registre ; indépendante des observations |
| `gustave_trace_understood` | Présentation réussie après les quatre découvertes ; récompense et accès, unique terminal |

Les quatre découvertes restent parallèles ; une preuve lue tôt reste acquise. Si le projet exige un trigger d'entrée, il ne doit pas invalider une lecture antérieure. Réutiliser les connaissances et jalons existants, pas un deuxième inventaire d'indices.

Activation proposée : `district1.gustave.archive_alcove_access`, consultée par l'obstacle permanent de l'environnement. Préserver l'ID de l'item et les identifiants de scène existants.

## Solo et coopération

- Les joueurs peuvent se répartir les deux pièces ; les découvertes sont partagées, avec un résumé bref pour les absents.
- Pas de plaques simultanées, de spécialisation imposée ou de deuxième joueur requis.
- Un joueur peut éclairer pendant qu'un autre observe, mais une personne seule peut tout faire.
- La petite présentation utilise le système de rassemblement existant si elle prend la caméra du groupe ; pas de prise de caméra surprise sur un joueur éloigné.
- Aucun butin exclusivement attribué au premier joueur ayant ramassé la preuve.

## Réalisation envisagée après validation

Réutiliser `CycleDefinition`, `CycleController`, `KnowledgeSO`, les readables, `CycleSequenceBinding` et les participants Timeline existants. Les pièces et Flames communes restent dans l'environnement ; seuls les traces révélées, les interactions et l'obstacle spécifiquement piloté par cette quête appartiennent au cycle.

Avant d'implémenter, vérifier que les interactions de traces peuvent être conditionnées par la visibilité lumineuse sans ajouter de logique Gustave à un gestionnaire générique. En cas de manque réel, proposer une petite liaison réutilisable plutôt qu'un nouveau système de lumière.

## Acceptation

- Tous les ordres de découverte, preuve déjà possédée, sauvegarde/rechargement et arrivées tardives.
- Source éteinte : pas d'observation d'une trace invisible ; indice lisible expliquant le rôle de la lumière.
- Source rallumée : trace réobservable sans récompense dupliquée.
- Interruption de présentation : contrôle restauré, pas de validation finale, possibilité de reprendre.
- Résolution et déchargement : alcôve toujours ouverte ; progression principale inchangée.
- Aucun déplacement du décor ou des autres cycles sans accord.

## Décisions encore à valider

Le format facultatif sans boss ; l'identité proposée de Gustave et l'attribution de la note ; les deux pièces utilisables ; les objets exacts des traces ; la récompense issue de l'économie actuelle. Rien de ce document ne doit être ajouté au canon comme un fait déjà approuvé.
