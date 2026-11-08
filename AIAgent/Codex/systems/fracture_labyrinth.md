# Labyrinthe de fracture — scène de test et intégration District 1

## Intégration continue dans District 1 (2026-11-02)

`District_1_Fracture_Environment.unity` est une extension physique chargée par
le manifeste District 1, pas une zone de portail. Accès sur le côté ouest du
couloir du belvédère, à environ 10 m du spawn (-92,98 ; -93,84 ; 61,02). Une portion du
garde-corps est ouverte, le sol raccordé et le NavMesh du quartier rebâti dans
un nouvel asset, avec obstacles sculptant les portes mobiles. Aucun autre
quartier ni point de spawn n'est déplacé.
L'accès est décalé au nord du passage secret entrée–couloir ouest existant,
pour ne pas superposer les deux entrées ; son ancien raccord exploratoire est refermé.

La scène dédiée de test est conservée. Dans la copie intégrée, le mode
`districtIntegration` retire la responsabilité du personnage, de la caméra,
des inputs et des managers à FractureGraybox : elle utilise le Lucian local
de la session et ne détruit jamais cet acteur à la désactivation. Pas de
caméra, soleil, exposition, spawn ou Flames de travail du prototype dans la
copie intégrée. Le HUD est limité à l'emprise du labyrinthe.

`FractureLightEntrance` réutilise le filtre d'influence Flame/torche commun,
donc la portée réelle de la TorchLight. Dans le noir, une paroi solide et
texturée camoufle le seuil ; sous lumière, elle laisse place à l'encadrement
et au passage. Un joueur revenant de l'intérieur peut ressortir même sans
lumière : ne pas l'enfermer si sa torche s'éteint.

Les indices, cases, état des fouilles, démarrage et fin sont encodés dans
`world.district1.fracture.archives.v1` de WorldRulesStateManager, déjà inclus
dans le snapshot monde. Le retour au quartier ou le rechargement ne repose
pas sur un nouveau fichier de sauvegarde. La résolution de campagne n'est
pas réinitialisée par le pupitre ; la reformation explicite reste au test.
WorldInteractionService route les actions client au serveur (identifiant
de station stable, acteur issu de la connexion, portée/lumière/sol vérifiés),
puis diffuse l'état et le nom du révélateur ; demande de snapshot au join.
Pas de nouveau NetworkObject de scène ni de changement d'époque.

Installation native `district-install` : copie dédiée, ajout au manifeste et
Build Settings, ouverture ciblée du garde-corps et nouvelle navigation.
Refuse les scènes sales et conserve les autres références du manifeste.
Validation campagne : `Library/StartupDistrictValidation/PlayRuns/c54d6cdc9d9042f09720c9f15d80dc42/result.json`
est Passed/restored, 36 contrôles et aucune erreur runtime. Bootstrap réel,
sauvegarde isolée, Maison → District, entrée sombre puis torche, parcours UCC des
trois indices et des cases, porte ouverte, retour à pied au couloir d'origine,
même acteur/caméra, capture et écriture monde/personnage, retour menu contrôlés.
Les captures entrée éclairée et Galeries ont été relues ; le bandeau IMGUI a
ensuite été décalé pour ne pas recouvrir la boussole du quartier.

Limites : approche initiale bornée de fixture, alignements de rotation UCC aux
pupitres (sans déplacement de position), confirmations automatiques des fenêtres
de dialogue/Savoir. Le trajet entre les points et les appuis A sont physiques,
sans appel direct aux exécuteurs des énigmes. Les essais précédents annulés ou
échoués de conduite du test ne valent pas validation. Pas de recette humaine,
caméra/focus, host/client, client tardif ou recharge froide de sauvegarde.
Le garde client attend la fin de JoinSync avant de demander l'état actuel, afin
qu'un ancien snapshot de connexion ne remplace pas les dernières découvertes.
Ce garde client et le décalage de bandeau ont été recompilés après le parcours
solo, sans modifier son chemin de gameplay ; 15/15 EditMode passés ensuite
(dernier EditTests.xml du 2026-11-02, 17:41:25 UTC).

## Périmètre actuel (2026-11-01)

Scène : `Assets/Gameplay/FractureLabyrinth/Prototype/Fracture_01_Graybox.unity`.
Révision demandée : deux énigmes seulement. Remplace le parcours pièce/réparation,
réseau et préparatifs ; ces anciens postes sont retirés du montage actif.
La campagne, le prefab Lucian et les réglages de caméra partagés sont inchangés.
Le plan initial et son audit restent des références de production, pas une
description de cette nouvelle version simplifiée.

## Parcours

1. La porte de départ ferme le passage vers la Cour. Interagir au premier
   pupitre commence la tentative et ouvre cette porte. Elle reste ouverte après
   la fin pour permettre le retour au pupitre ; une nouvelle session Play la
   retrouve fermée. Ce n'est pas une troisième énigme.
2. Fouiller six bibliothèques aux Archives. Trois contiennent un indice numéroté.
   Configuration auteur : A → 3 = O, C → 1 = Y, E → 2 = — ; B/D/F sont vides.
   La découverte peut se faire dans n'importe quel ordre. Répéter une fouille ne
   duplique pas l'indice ; une bibliothèque vide le signale explicitement.
3. Le carnet HUD ne montre que les indices trouvés, classés par numéro. Le
   troisième révèle le KnowledgeSO « Les symboles numérotés des Archives » via
   KnowledgeReveal ; chaque nouvel indice annonce localement son révélateur.
4. Aux Galeries, régler les cases 1, 2 et 3 puis utiliser « Essayer la combinaison ».
   Interagir fait défiler O → Y → — → O. La solution auteur est Y / — / O.
   Les trois indices doivent avoir été trouvés ; une erreur ne consomme ni ne
   réinitialise rien. La bonne combinaison ouvre la porte physique des Galeries.
5. Traverser et utiliser le poste de fin. Aucun boss, score ou gain simulé.

Les anciens chemins vers les Machines et le seuil sont fermés dans ce montage.
La porte dispose de murs latéraux et la sortie n'accepte la fin qu'après validation.
Il n'y a plus d'énigme, pièce ou préparatif supplémentaire dans le parcours actif.

## État et limites

`FractureArchivePuzzle` (dans FractureAttempt.cs) contient les trois faits
découverts, les trois entrées et l'ouverture. Pas d'état statique. Une reprise au
pupitre remet la porte et les entrées à zéro mais garde les indices, comme un
Savoir connu. Une nouvelle session Play repart sans indice ; aucune sauvegarde
campagne n'est écrite. FractureAttempt conserve les primitives de l'ancien plan,
mais elles ne conditionnent plus ce parcours de deux énigmes.

Le prototype refuse une session réseau/campagne active. Le carnet et les indices
sont actuellement locaux ; ne pas les présenter comme déjà répliqués. Intention
multi : fouilles parallèles et communication avec un joueur à la porte, partage
immédiat des indices. L'autorité et la réplication de ces faits partiels restent à
raccorder avant de qualifier 2/3/4 joueurs. Le solo n'exige qu'un seul acteur.

## Aides et commandes

Panneau non modal de proximité (5,5 m, réglable via `puzzleHelpDistance`, désactivable
via `showPuzzleHelp`). Rubriques « À faire maintenant », « À quoi ça sert »,
« À savoir / ensuite ». Il explique la règle sans révéler un indice non fouillé.
Bandeau permanent : carnet, combinaison courante et prochaine étape.
Pas de focus, pause, nouvelle écoute brute d'Interagir ou blocage de locomotion.

Caméra locale copiée de GameplaySessionRoot ; UCC seul propriétaire de la pose,
LitUccCameraCharacterBinder lié au Lucian local. Orbite stick droit, zoom LB/RB,
inspection/suivi L3, souris habituelle et C selon les réglages existants.
R3 reste réservé à la torche. Désactivation : suspendre/détacher avant destruction
de Lucian ; réactivation : reprendre le binding du nouvel acteur.
Flames de travail déjà allumées, non Ancient, colliders triggers uniquement.
Sélection, portée, visibilité et action Interagir passent par les services communs.

## Authoring et validation

Habillage : `FractureDungeonArt` remplace les rendus primitifs par des instances
de ModularDungeonCollection dans cette seule scène : Floor_Flagstone_01,
Door_02, SM_Shelf_01, Pillar_01_2H et les deux meshes de levier. Les murs sont
désormais des volumes fermés dédiés, avec la texture de pierre du pack.
Les colliders de gameplay sont conservés invisibles ; ceux des instances de
décor sont désactivés pour ne pas doubler les collisions ni bloquer la sélection.
Les portes restent enfants de leur objet de gameplay et disparaissent avec
celui-ci lors de l'ouverture. Les anciennes paires de faces planes sont retirées.
Chaque mur possède six faces : côtés, dessus/dessous et extrémités. Son volume
correspond exactement à son collider existant (20 cm pour les murs périphériques,
30 cm pour les cloisons intérieures). Meshes persistés et partagés par dimensions
dans SolidWall_*.asset, matériau local SolidWall_Stone.mat copié du matériau
pierre du pack avec ses maps couleur/normale/AO/rugosité. UV calculés par face
avec une répétition pour 4 m sur les deux axes, y compris les tranches : pas
d'étirement selon la longueur ou l'épaisseur. Aucun changement des assets source.
Révision volumique : compilation et 14/14 EditMode passés (24 sommets, 12 triangles,
huit coins, normales sortantes, volume conforme au collider et densité UV des
six faces). Parcours physique complet passé le 2026-11-01 à 18:28:17 UTC ; capture
Galeries inspectée, dessus et extrémités fermés visibles. Aucun changement de
locomotion ou d'énigme ; recette humaine/manette physique toujours distincte.
Les étagères génériques et leurs livres colorés sont masqués, pas superposés.
Les tailles sont ajustées à l'enveloppe existante, sans modifier le pack source.
Menu `Lit/Fractures/Install Modular Dungeon Art`, commande `install-art` dans
Create.request. Installation idempotente par enfant ModularDungeonCollection_Visual,
refus si scène sale. `inspect-art` écrit dimensions et shaders dans
DungeonArt-assets.txt. Orientation des étagères vers le côté de fouille (180°).
Validation de cet habillage : compilation, 14/14 EditMode, parcours complet
à la manette simulée (18:21:26 UTC) et preview Play (18:21:52 UTC) passés.
Captures entrée/Archives/Galeries relues : matériaux non roses, étagères de face,
commandes et carnet lisibles. Les tests vérifient origine des meshes, shaders,
colliders de décor désactivés et habillage de chaque poste. Pas une recette
artistique finale ni un essai humain/multijoueur.

`Lit/Fractures/Install Archive Gallery Puzzles` migre seulement cette graybox,
refuse une scène sale et ne modifie pas la campagne. Marqueur de montage
Puzzles_ArchivesGaleries pour éviter une seconde construction.
`Create Solo Graybox` construit aussi cette version pour une scène neuve.
Profil KnowledgeSO du seul prototype réutilisé avec titre/description actualisés.

Rapports sous Library/FractureLabyrinthValidation :
- EditTests.xml : modèle, ordre, déduplication, reset, pas de fuite vers une
  nouvelle session, montage des six bibliothèques et trois cases.
- WalkTests.xml / Walk.trace : parcours réel UCC avec manette simulée ; bibliothèque
  vide, répétition, indices découverts hors ordre, refus d'un code erroné,
  combinaison correcte, traversée physique et fin.
- PuzzleHelp-archives.png et PuzzleHelp-gallery.png : captures de ce parcours.
- PreviewTests.xml et CameraTests.xml : validations de présentation/caméra,
  dont vérifier la date car des rapports précèdent cette révision.

Révision validée dans Unity : compilation, 14/14 EditMode et 1/1 parcours physique
passés ; dernière régression avec porte de départ le 2026-11-01, fin 18:05:13 UTC.
Assertions : porte initialement fermée avec collider solide, ouverte par le
pupitre et restant ouverte à la fin. Captures Archives/Galeries inspectées :
étagères, carnet et explications lisibles. Le parcours physique couvre la
bibliothèque vide, la répétition, l'ordre de découverte 3/1/2, le refus d'un
mauvais code et le passage après ouverture. Reset/reprise et nouvelle session
sont couverts par le modèle, pas par deux parcours humains successifs.
Les preuves des anciennes énigmes ne qualifient pas les nouvelles.
Aucun combat, boss ou multijoueur validé.
Les tests automatiques ne remplacent pas un essai humain clavier/manette.

Pont Editor : Refresh.request pour importer, Create.request = install-puzzles
pour migrer, Tests.request = edit / walk / preview / camera pour lancer les tests,
hors Play et compilation. Le framework restaure le contexte de scène et refuse
les scènes utilisateur sales. Ne pas modifier les sources pendant un test Play.
