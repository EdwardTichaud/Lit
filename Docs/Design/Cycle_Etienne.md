# Cycle Étienne — La dernière relève

## Parcours livré

Après la conclusion de Belmont, traverser Wall_2 : le pont mène au belvédère du
Puits de la Relève. Le puits octogonal fait environ 36 m de largeur et 22,4 m de
profondeur ; quatre volées d'escaliers fixes relient ses trois niveaux. Chaque palier
dispose d'une Flame commune allumée ; aucune nouvelle Ancient Flame n'est ajoutée.

1. Entrer dans le Puits : Lucian découvre le réseau.
2. Lire ou récupérer les trois preuves, dans n'importe quel ordre.
3. Actionner le frein au poste inférieur : Le Poids mort apparaît.
4. Vaincre ce boss de 300 PV. Pendant le combat, la touche d'interaction permet
   d'actionner le frein à moins de 2 m : immobilisation de 4 s, recharge partagée
   de 12 s. Le frein ne fait pas de dégâts.
5. Le souvenir « Tenir jusqu'au dernier » dure 30 s. En cas d'échec, revenir au
   frein pour le relancer ; aucune connaissance finale n'est accordée sur un échec.
6. Retourner volontairement auprès d'Étienne : les preuves et le souvenir permettent
   de l'apaiser. La sortie vers les Conduits Noyés s'ouvre durablement.
7. La réplique finale de Lucian clôt le cycle, après la dissolution.

Étienne est mort en tenant le mécanisme pour les autres. Son geste a réussi.
Les documents révèlent seulement la convergence des conduits vers les salles
inférieures proches de la statue, pas le rituel ni la fonction des Chanteurs.

## Organisation

- `Assets/Scenes/District_1/District_1_PuitsDeLaReleve_Environment.unity` : architecture,
  collisions, escaliers, Flames communes, contrepoids et sortie permanente.
- `Assets/Scenes/Cycles/Cycle_Etienne/District_1_Cycle_Etienne.unity` : quête, preuves,
  fantôme, boss, frein et souvenir.
- `Assets/Resources/Narrative/EtienneCycle.asset` : définition `district1.etienne`,
  catégorie principale, aucun prérequis venant d'un autre cycle.
- Les autres assets de la quête se trouvent dans `Assets/Scenes/Cycles/Cycle_Etienne`.
- La sortie consulte `district1.etienne.flooded_conduits_access` même après le
  déchargement de la quête. Les liaisons de cinématique entre scènes utilisent des
  identifiants stables et ne nécessitent pas de référence sérialisée inter-scène.

Les scènes et assets sont directement éditables : aucun générateur ne les reconstruit.
Les cycles sont indépendants. La chronologie est organisée par les accès et les
triggers de scène, pas par un prérequis `CycleCompleted`. Pour Étienne,
`Puits_EntryTrigger` valide `puits_entered` ; cette étape débloque les preuves.
Wall_2 conserve son fonctionnement spatial actuel, sans lien de dépendance entre
les définitions Belmont et Étienne. Un chargement isolé du Puits permet donc de
jouer Étienne sans avoir terminé Belmont.
Le décor est un blockout jouable et le souvenir une première mise en scène avec
les ressources existantes. Le blockout a été habillé avec les modules du pack
Dungeon_Environment : maçonnerie, arches, sols, poutres et mobilier de maintenance.
Leur finition artistique reste à faire.

## Décor et édition manuelle

Dans la scène d'environnement, `Puits_Octogonal` regroupe la maçonnerie, le
belvédère, la galerie intermédiaire, le poste inférieur, la descente fixe et le
réseau de Veillée. Le centre se trouve 24 m à l'ouest de l'ancrage d'entrée.
Les paliers sont à 0, -11,2 et -22,4 m relativement à cet ancrage.

Les instances du pack gardent leurs liens aux prefabs sources. Les collisions
des surfaces de circulation et des protections sont explicites ; le décor suspendu
ne doit pas gêner la navigation. Le frein reste proche du centre de l'arène et
dans le BattleWall. Les trois preuves, Étienne, le boss et les cadrages ont été
repositionnés sans changer les identifiants de progression.

`AncienBlockout_Inactif` conserve les anciens volumes déplacés, désactivés, pour
faciliter une récupération. Ne pas le réactiver en bloc : ses collisions
reviendraient dans les anciens emplacements. Les snapshots complets antérieurs à
l'habillage se trouvent localement dans `Library/PuitsBeforeDecoration` ; ce
dossier n'est pas versionné et sera perdu si Library est supprimé.

Le raccord et la scène Belmont ne sont pas reconstruits. Aucun générateur de
décor n'est conservé dans le projet. Après modification des escaliers, plateformes
ou collisions, rebâtir le NavMesh de District_1 et vérifier le parcours entier.

Le raccord du palier intermédiaire est élargi pour rejoindre la galerie sans
étranglement. Le blocage des Conduits Noyés est exclu de la géométrie statique du
NavMesh et utilise un obstacle avec carving : sa disparition libère un chemin
déjà construit, sans nécessiter de nouveau bake en jeu.

## Tester sans refaire Belmont

Dans le CycleController Étienne, activer le départ Dev et choisir :

| Étape de départ | Test |
|---|---|
| `relief_register_read` | Enquête : entrée déjà simulée |
| `brake_released` | Actionner le frein pour faire apparaître le boss |
| `dead_weight_defeated` | Combat, dégâts, verrouillage et frein |
| `last_relief_seen` | Boss déjà vaincu, lancement du souvenir |
| `etienne_spoken` | Apaisement volontaire et ouverture |

Charger les deux scènes additives avec les systèmes habituels de District_1 et
placer le joueur dans le Puits. Le départ Dev simule les étapes précédentes, y
compris les connaissances, sans modifier la sauvegarde ni décharger définitivement
la scène. Désactiver Dev pour tester le parcours persistant normal.

## Éditer le souvenir dans Timeline

`Timeline/TenirJusquAuDernier.playable` conserve son GUID et sa durée de 30 secondes.
La mise en scène est désormais portée par des pistes Unity Timeline natives :

- **Caméras** : quatre clips Cinemachine, coupes à 6, 14 et 24 secondes. Les
  caméras `Souvenir_Plan_0` à `Souvenir_Plan_3` sont dans `Cameras_Souvenir`,
  sous le Director du souvenir dans la scène du cycle.
- **Personnages** : clips Animation de traversée en coordonnées locales et
  pistes Activation. Les collègues apparaissent de 6 à 23 secondes ; le souvenir
  d'Étienne de 6 à 22 secondes.
- **Contrepoids** : clip Animation local, lié à l'environnement par
  `district1.etienne.counterweight`. Sa pose est restaurée après fin ou interruption.
- **Son** : le clip de rupture déjà assigné commence à 22 secondes.

Charger les deux scènes, sélectionner `TimelineBindingProfile_LastRelief`, puis
utiliser **Préparer l'aperçu Timeline (scènes chargées)** dans son inspecteur.
Le Director s'ouvre dans Timeline ; activer Preview et déplacer la tête de lecture.
**Terminer l'aperçu et restaurer** rétablit les poses et les liaisons initiales.
Ces liaisons d'aperçu sont temporaires et nettoyées avant sauvegarde ou Play Mode.
En jeu, la piste Cinemachine utilise le Brain de la caméra de gameplay via
`camera.cinemachine_brain` et la passerelle commune rend le contrôle après lecture.

Les anciens calculs temporels, caméra indépendante, pistes d'horloge et repères
de passage/cadrage sont remplacés par ces clips et caméras éditables. Les poses
actuelles ont été transférées sans reconstruire le décor ni déplacer le gameplay.
Des snapshots avant conversion sont conservés localement dans
`Library/EtienneBeforeTimeline` (perdus si Library est supprimé).
La progression `last_relief_seen` et les autres identifiants restent inchangés ;
une lecture interrompue ou une liaison requise manquante ne valide pas le souvenir.

## Vérifications

Le menu **Lit > Validation > Valider le cycle Étienne** réalise un audit en lecture
seule des références, identifiants réseau, preuves récupérables à 2 m, outlines sur
les renderers, trois Flames allumées, données du boss, sortie, Timeline et manifeste.

Historique de l'habillage (avant les éditions ultérieures) : compilation, 16 cas de test Étienne réussis,
audit des scènes, navigation continue du pont à l'arène par les quatre volées,
accès à la galerie de la deuxième preuve, à la note inférieure, à Étienne, au frein
et aux Conduits Noyés lorsque la sortie est ouverte. Les points de circulation
contrôlés n'intersectent aucun collider solide des autres scènes chargées.
L'influence des trois Flames couvre les preuves, Étienne et le frein. Les vues
de contrôle des trois niveaux ont été examinées avec un éclairage temporaire de
prévisualisation, non enregistré dans la scène ; elles ne valident pas l'éclairage
final en jeu.

Les tests couvrent les permutations des preuves, départs Dev sans écriture, données et mécanique
du boss, conservation du contrat BrokenAnchor, restauration après interruption du
souvenir et inclusion de l'activation de sortie dans les clés synchronisées.

Les tests de progression génériques couvrent aussi la restauration et l'absence
de récompense doublée. Des tests historiques Nina et Géant échouent actuellement
sur leurs anciens chemins d'assets absents ; ils n'ont pas été modifiés ici.

Restent à valider en session réelle : parcours complet, confort des cadrages,
lisibilité du frein en combat, sauvegarde/rechargement à chaque jalon, hôte/client,
arrivée tardive et déconnexion pendant la cinématique. Les contrôles automatisés
ne remplacent pas ces essais de gameplay et réseau.

Contrôle du 3 octobre après conversion Timeline : compilation et évaluation native
des pistes à 10 et 24 secondes réussies, déplacements locaux des collègues,
contrepoids à +2 m, sélection du deuxième plan Cinemachine et silhouettes masquées
après leurs clips. Les 17 cas Étienne donnent 16 réussites et un échec d'audit :
`Prefab_Ghost_Etienne_Belmont` est actuellement hors du rayon d'une Flame allumée.
Son placement de gameplay a été conservé ; rapprocher une Flame ou ajuster son
rayon si ce placement est souhaité. La restauration de Transform après interruption
et le contrat des pistes natives passent. Les essais réseau et la lecture complète
en situation réelle restent à faire.
