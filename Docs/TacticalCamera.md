# Caméra tactique UCC — comparaison développeur

## Utilisation

Stabilisation de la visee : la rotation visant le joueur est calculee une fois
par pas UCC depuis la position finale apres collision, puis utilise la meme
fraction d'interpolation que la position. Le second filtre LateUpdate et son
seuil de saut de 15 degres sont retires. `renderedAimSharpness` est conserve
pour la compatibilite des profils, mais n'est plus utilise ni affiche.
Le suivi amorti, les collisions, L3 et les Timelines restent en place.
Verifier visuellement marche/course, orbite, obstacles et combat a 30/60/120 FPS.
Compilation runtime/editor sans erreur. Les tests de stabilite sont executes
dans un projet Unity isole ; cela ne valide pas le ressenti visuel en Play.

La Main Camera du prefab `Assets/Core/System/GameplaySessionRoot.prefab` porte maintenant `LitGameplayCameraModeController`. Le mode initial reste **ThirdPerson**. En Play, sélectionner la Main Camera du Bootstrap puis utiliser les boutons **Third-person** / **Tactical** de ce composant. L'inspecteur affiche le mode demandé, le mode effectif et l'autorité cinématique. `SetMode(GameplayCameraMode)` est également disponible pour les outils développeur.

Le profil partagé est `Assets/CombatRealTime/Camera/TacticalDefault.asset`. Tous les réglages de cadrage, amortissement, commandes, limites et délais de masquage y sont exposés. Le défilement aux bords est désactivé. Aucune préférence joueur n'est sauvegardée.

UCC reste le seul pilote de la caméra physique. La vue Adventure sérialisée n'est pas remplacée : les extensions s'installent après son initialisation. Le lock conserve ses règles de combat mais n'impose ni orientation ni secousse à la vue tactique. Une demande de bascule pendant une cinématique est différée ; le Brain et les pistes Cinemachine gardent leur relais actuel.

## Commandes

| Contexte | Commandes |
| --- | --- |
| Clavier/souris | Droit maintenu : orbite ; molette : zoom ; C : recentrage. Flèches et glissement milieu : uniquement en caméra libre |
| Manette, suivi | Stick gauche : personnage ; RT : sprint ; stick droit : orbite ; LB : éloigner ; RB : rapprocher ; LT : roue de compétences ; croix bas : verrouiller/changer de cible ; clic L3 : caméra libre hors combat |
| Manette, inspection libre | Stick gauche : pan ; stick droit : orbite ; LB/RB : zoom ; clic L3 : retour au suivi ; maintien R3 : sortie et recentrage |

La caméra suit et vise le joueur par défaut, y compris lorsqu'une collision modifie sa position réelle. Un pan ne détache jamais ce suivi. L'action dédiée `Camera/TacticalInspection` remplace l'ancien `ToggleFreeCamera` dans l'asset d'entrées et son wrapper généré. Son binding L3 utilise maintenant un simple clic, sans maintien. La vue third-person ignore cette action et conserve le clic L3 de locomotion ; celui-ci est ignoré uniquement en mode tactique. Le second clic L3 réactive le suivi et recentre progressivement. L'inspection n'active que l'ActionMap Camera, utilise également le contexte de suppression de gameplay existant, et bloque les overrides de déplacement UCC. Les gâchettes pilotent le zoom en suivi et en inspection ; le D-pad conserve ses commandes de gameplay hors inspection. La perte de focus, les menus, un changement de personnage, une cinématique ou une désactivation quittent l'inspection.

Le pivot libre reste limité à 20 m horizontalement (`Maximum Pan Radius`). Une seconde limite de 10 m borne la position de caméra, zoom et hauteur compris (`Maximum Free Camera Distance`). Ces deux limites restent réglables dans le profil. Les volumes de scène ne contraignent que le pivot libre : ils ne peuvent plus retenir la caméra de suivi lorsque le personnage se déplace.

Le contrôle du trajet glisse sur les obstacles et autorise le mouvement tangent ou sortant lorsqu'une surface est déjà au contact. Si un coin empêche tout progrès, la caméra se rétracte vers le dernier ancrage accessible à `Collision Recovery Speed` avant de retrouver sa distance normale. Les colliders ne sont jamais désactivés pour débloquer le mouvement.

Les entrées souris sont consommées une fois par frame. Le survol UI et un champ de texte actif bloquent les commandes clavier/souris de caméra. Le curseur utilise une propriété temporaire partagée avec `MainMenuPointerCursor` : les menus restent prioritaires, et l'état précédent est restitué à la sortie.

## Zone intérieure configurée

Déposer `Assets/CombatRealTime/Camera/TacticalCameraTestRoom.prefab` dans une zone de test vide, son origine au niveau des pieds du personnage. Il contient un sol de 12 × 10 m, une porte au sud, des murs, un toit et trois marches. Les six groupes murs/toit référencent explicitement leurs renderers ; le matériau `TacticalTestRoom.mat` utilise le Shader Graph Lit HDRP compatible `LitTacticalMaskLit.shadergraph`. Le volume de limites du pivot est déjà configuré. Le prefab n'ajoute aucune caméra, aucun joueur ni ancien contrôleur CRPG et ne modifie pas les scènes de production.

Pour une pièce existante : placer `LitCameraOcclusionGroup` sur un parent des colliders des obstacles, puis renseigner explicitement **tous** les renderers de ces obstacles. Les requêtes ignorent les triggers et utilisent les mêmes couches UCC que les collisions caméra ; les obstacles doivent avoir des colliders non-trigger sur ces couches. Un renderer doit appartenir à un seul groupe d'occlusion. Les volumes `LitTacticalBounds` utilisent un BoxCollider trigger ; ils s'appliquent uniquement quand le personnage est dedans. Éviter de superposer les volumes.

## Glissement ou masque localisé HDRP

Dans `TacticalDefault.asset`, régler **Obstacle Mode** :

- **Sliding** (défaut) : collisions, glissement et rétraction habituels. Aucun renderer n'est caché.
- **VisibilityMask** : en suivi, conserver le cadrage à travers les groupes compatibles et ouvrir un cercle autour du joueur, plus un second autour de l'ennemi verrouillé si son groupe l'occulte. Les obstacles non configurés ou incompatibles restent solides pour la caméra.

Le choix peut changer en Play sans réinitialiser yaw, zoom ou personnage. L'inspecteur de la MainCamera affiche le choix demandé et effectif. L3/caméra libre impose **Sliding** et retire immédiatement la découpe ; revenir au suivi restitue le choix du profil. Une Timeline, la sortie du mode tactique, une désactivation ou le déchargement des groupes restitue les propriétés. Le relais UCC/Cinemachine n'est pas modifié.

Les réglages initiaux sont `Mask Radius = 0.16` (fraction de hauteur de l'écran), `Mask Feather = 0.025`, `Mask Fade Time = 0.12 s`, détection `0.05 s`, délai de restitution `0.15 s`. Le cercle reste rond quel que soit le format d'image. Les ouvertures se combinent par union, ne découpent que la géométrie devant la cible, et sont désactivées pour une cible derrière la caméra. L'ancrage du joueur est celui du suivi UCC, sans os animé.

### Préparer les shaders

La fonction partagée est `Assets/CombatRealTime/Camera/LitTacticalVisibilityMask.hlsl`, Custom Function **File**, nom `LitTacticalMask`, précision **Float**. Les graphs `ShaderGraph_MasterShader` et `ShaderGraph_LitIceFrostedEdges_v3` sont raccordés ; le masque est désactivé par défaut et les effets décor/glace restent présents. Le graph de référence `LitTacticalMaskLit` fournit un exemple minimal.

Pour un autre graph HDRP Lit, reproduire les propriétés et le raccord de ce graph : position **Absolute World** → `PositionAbsolute`, alpha habituel → `OriginalAlpha`, sortie `Alpha` → bloc Alpha, Alpha Clipping activé. Le graphe de glace opaque utilisait jusque-là un alpha ignoré : son nouvel alpha de base est 1 pour préserver cette apparence. La découpe supplémentaire utilise `clip` indépendant du seuil existant. Ne pas raccorder uniquement la couleur : profondeur, normales et motion vectors doivent évaluer le même Alpha. Les passes d'ombre et de lumière précalculée conservent la géométrie d'origine ; les ouvertures ne s'appliquent pas au ray tracing.

Propriétés réservées : `_LitMaskVersion` (Float, défaut 1), `_LitMaskEnabled` (Float, défaut 0), `_LitMaskTarget0`, `_LitMaskTarget1`, `_LitMaskSettings`, `_LitMaskCameraPosition`, `_LitMaskCameraForward` (Vector4, défaut zéro). Ce contrat doit représenter une **découpe réellement raccordée**, pas seulement des propriétés ajoutées. Le diagnostic de chaque groupe vérifie tous ses renderers et tous leurs slots de matériau ; une référence absente ou un seul matériau incompatible conserve le glissement et produit un avertissement unique.

La détection part de la pose souhaitée, avant rétraction, et inclut les colliders contenant la caméra. Les propriétés sont appliquées via `MaterialPropertyBlock` par slot, uniquement pendant le rendu de cette caméra, puis restituées. Les autres propriétés, dont celles des effets de glace, sont préservées ; aucun matériau partagé, collider, couche physique ou `forceRenderingOff` n'est modifié par le chemin tactique. Le masque est local au client et ne contient aucune donnée réseau. Ne pas utiliser des matériaux compatibles seulement sur une partie d'un groupe.

Les gizmos sélectionnés montrent les limites et les groupes. Activer `Show Diagnostics` du profil pour voir le pivot et la ligne de caméra. Les collisions rétractent immédiatement la caméra, filtrent le retour et vérifient également le trajet de pan et de transition. La hauteur du pivot libre ne change que si le sondage proche trouve un sol dans la marge autorisée ; sinon elle est conservée.

## Validation

### Correction du léger tremblement de visée

Le cadrage final ne doit pas utiliser `ViewType.GetAnchorPosition()` : cette méthode UCC lit la position physique du personnage, tandis que la caméra a déjà été interpolée pour l'affichage. Les quatre personnages ont l'interpolation UCC activée ; mélanger ces deux instants produit une erreur de visée en dents de scie à chaque étape physique.

La vue tactique utilise désormais un ancrage stable calculé depuis la racine : position physique pour le suivi/collisions, position affichée pour `LateRotate`. Les offsets horizontaux suivent l'orbite nominale et non la rotation de caméra déjà corrigée, ce qui évite une boucle de rétroaction. Aucun os animé n'entre dans ce calcul. Aucun second pilote de Transform ni nouveau lissage des commandes n'a été ajouté ; UCC conserve son interpolation et les tests de collision.

Les tests à 30/60/120 FPS reproduisent le mélange physique/affichage de l'ancien calcul, vérifient qu'il générait une erreur angulaire, puis contrôlent que la nouvelle visée reste stable aux frontières des étapes fixes. Un test couvre aussi la stabilité avec offsets non nuls pendant la rotation du personnage. La compilation et les tests de calcul ne remplacent pas une vérification visuelle en gameplay : marche/course, rotations, lock, murs, zoom et reprise Timeline restent à contrôler en Play.

### Zoom manette et roue de compétences

En suivi comme en inspection tactique : LB/L1 eloigne, RB/R1 rapproche. La distance demandée est comprise entre 1 et 10 m dans le profil par défaut ; les collisions peuvent toujours rétracter davantage la caméra pour éviter un mur. Le zoom reste amorti et continu pendant le maintien. Les deux shoulders simultanes s'annulent. Les menus/roues et Timelines suspendent le zoom.

La roue de competences s'ouvre avec LT/L2 maintenu et se ferme au relachement ; le clavier conserve son raccourci. Les deux assets d'input utilisent ce binding. RT/R2 pilote le sprint dans les deux vues, sans sprint automatique au stick tactique. Croix bas acquiert le verrou puis change de cible ; le callback d'exploration est filtre en combat pour eviter une double action. L'inspection, les menus et le retour de contexte annulent/restaurent le sprint via le routeur existant. La limite de distance reelle au joueur en inspection libre reste de 10 m.

Deux essais Play avec gamepad virtuel dans Iluvilirae_Test sont passes : sprint RT/relachement, zoom RB sans roue, absence de sprint automatique au stick, verrou croix bas, roue LT, attaques et restitution apres cinematics/pause. Compilation runtime/editeur reussie. Le ressenti du zoom et du sprint et les scenes de gameplay restent a confirmer sur manette physique.

### Déplacement relatif à la vue tactique

Le stick gauche et le clavier utilisent la base horizontale de la caméra affichée, jamais l'orientation du personnage ou la direction de l'ennemi. La base est capturée après le cadrage UCC ; les mises à jour physiques ne la remplacent pas par une pose de simulation non affichée. Tourner la caméra pendant un déplacement maintenu change la direction demandée sans mémorisation d'angle de caméra fixe. La magnitude analogique est conservée et les diagonales restent limitées à 1.

La règle s'applique uniquement lorsque la vue UCC tactique est réellement active et liée à ce personnage, hors contrôle cinématique. Une caméra de rendu momentanément indisponible conserve la dernière base valide ; avant la première base valide, le déplacement demandé est nul. Le changement de personnage efface cette base. En lock, le personnage garde son regard vers la cible, sans correction de rayon autour de l'ennemi ; les animations latérales/arrière utilisent le déplacement réel exprimé dans son repère local. L'inspection L3 et les blocages d'entrée existants restent inchangés.

`LitTacticalMovementType` est installé au runtime pour l'exploration tactique. Le bridge fournit une intention de déplacement monde et de regard ; le MovementType les convertit dans le repère de simulation du moteur UCC, qui applique lui-même la rotation. Le type antérieur est restitué à la sortie du mode ; le lock conserve son type UCC spécifique et utilise le même calcul tactique. Les commandes `MoveWorld` des scripts ne sont pas reconverties depuis la caméra.

### Stabilité du personnage en déplacement tactique

Le suivi caméra étant stabilisé, la rotation du personnage ne doit plus appeler `SetRotation` ou `SetPositionAndRotation` à chaque image de déplacement/pivot tactique : ces commandes immédiates réinitialisent l'interpolation UCC et notifient aussi la caméra. Exploration, pivot au sol et regard de lock transmettent désormais une intention à la simulation UCC. La conversion du déplacement utilise la rotation du moteur plutôt que le Transform interpolé, et compense la rotation que le moteur applique à cette étape pour préserver la direction écran et la magnitude analogique. Les repositionnements explicites, les esquives dédiées et le chemin third-person restent inchangés.

Vérification : compilation runtime/éditeur réussie ; 6 tests de calcul moteur et 14 tests de stabilité caméra réussis dans un projet Unity isolé (20 au total). Contrôle visuel Play encore nécessaire : marche/course, virages et pivot à l'arrêt, déplacements latéraux en lock, bascules de mode et retour de Timeline.

Validation automatisée : les 9 cas de `LitTacticalMovementTests` passent dans le projet Unity isolé (quatre angles caméra, quatre orientations personnage et huit directions, pitch 25–90°, magnitude analogique et rotation avec input maintenu). Le test du type de mouvement intégré est compilé avec le projet. Essais visuels gameplay à effectuer : exploration et lock, changements de cible/mode/personnage, stick partiel et course, L3/menu/Timeline, pentes et obstacles. Aucun essai Play de la scène gameplay n'a été effectué pour cette correction.

### Stabilité du suivi et entrée en combat

La vue tactique conserve le yaw d'orbite, le zoom et le FOV choisis à l'entrée en combat : le lock reste une sélection de combat, pas un ordre de recadrage. Les notifications d'aggro répétées du même ennemi ne retirent/réappliquent plus le lock et ne remplacent pas un déverrouillage manuel. Une perte de lock ne termine plus une suspension cinématique.

Le pivot avance selon les étapes de simulation UCC, même si plusieurs étapes physiques partagent une frame affichée. Les deltas d'entrée s'accumulent jusqu'à leur consommation unique ; les changements de contexte/focus, binds et téléportations les annulent. L'orientation finale vise l'ancrage affiché/interpolé du joueur depuis la pose réelle de caméra, sans avancer une seconde fois le suivi dans LateRotate. L'orbite libre conserve l'interpolation UCC et reste limitée en distance.

Les colliders enfants d'un `BattleWallContainment` sont exclus des seules requêtes caméra tactiques (y compris sol et masque), sans modifier couche, collider ou matériaux : ils continuent de retenir les personnages. Le third-person et les autres obstacles restent inchangés. La rétraction est immédiate ; le retour attend `Collision Clear Hold Time` (0,08 s par défaut), puis utilise `Collision Return Time`. Cette hystérésis évite les petites oscillations de distance au contact d'un mur. Une pose immédiate ne balaie pas depuis une ancienne position précédant une téléportation.

`LitTacticalStabilityTests` couvre l'accumulation/consommation des entrées, leur restitution, les étapes fixes multiples, les appels immédiats et le retour après collision à 30/60/120 étapes par seconde. Le test de filtre de barrière dans `LitTacticalCameraTests` vérifie aussi l'obstacle ordinaire, la cinématique et le maintien du collider solide. Avec `Show Diagnostics`, le menu contextuel **Diagnostics: Dump Tactical Motion** de la MainCamera affiche les 32 derniers échantillons physique/affichage ; le stockage ne génère pas d'allocation par frame.

Vérifications de cette correction : compilation C# runtime et éditeur sans erreur ; 8 tests de `LitTacticalStabilityTests` exécutés et réussis dans le projet Unity isolé. Le test intégré de barrière est compilé, mais n'a pas été exécuté dans le projet gameplay. Aucun essai visuel Play de la scène de combat n'a été effectué pendant cette correction.

Essai Play spécifique requis : déclencher un combat à l'arrêt puis en courant, recevoir plusieurs dégâts du même ennemi, enlever manuellement le lock, passer près de la BattleWall et d'un mur normal, essayer L3 et une Timeline. Vérifier la conservation de l'orbite/zoom/FOV, l'absence de tremblement et la priorité cinématique. Les tests automatisés de logique ne prouvent pas le ressenti visuel dans la scène gameplay.

`LitTacticalCameraTests` couvre la résolution mode/lock et obstacles/suivi/libre/cinématique, le défaut Sliding, la projection d'une cible derrière la caméra, la compatibilité de tous les slots et le diagnostic unique, le contrat des trois graphs, la restitution des blocs renderer et par slot (en préservant les autres effets), les colliders/matériaux, les limites, le contexte exclusif d'inspection, le curseur, le Bootstrap et la pièce compatible. Les lancer dans le Test Runner, onglet EditMode.

Vérifications effectuées pour le choix d'obstacles : compilation C# runtime et éditeur sans erreur (avertissements existants hors caméra) ; dans un projet Unity 6000.4.9f1/HDRP 17.4 isolé, 14 tests EditMode ciblés passent et les 66 variantes de passes des trois graphs compilent en D3D11, avec et sans `_ALPHATEST_ON`. Une erreur latente de nom du slot Alpha Clip Threshold du graph glace, révélée par l'activation du clipping, a été corrigée. Ces vérifications n'ont pas lancé la scène gameplay et ne remplacent pas les essais visuels ci-dessous.

Validation Play à effectuer : démarrage third-person avec Brain désactivé ; bascules à l'arrêt/en mouvement/en lock ; commandes aux trois fréquences 30/60/120 FPS ; perte/reprise du focus ; murs, porte, marches et plusieurs étages ; masquage puis mode/Timeline/déchargement ; inspection puis menus/roues de skills ; retour de cinématiques dans chaque mode ; changement de personnage/respawn/téléportation ; deux clients réseau. Le composant, la vue, les états de pivot et les renderers masqués ne contiennent aucun état réseau ni écriture persistante.

Comparer Sliding et VisibilityMask contre un mur, dans la porte et sous le toit ; vérifier les deux ouvertures en lock, leur union, le décor derrière la cible, L3, les changements d'option en Play et les Timelines. Vérifier HDRP/TAA à plusieurs résolutions et formats, avec deux clients : absence de profondeur invisible, de scintillement excessif et d'effet sur l'autre client. La compilation statique n'est pas une validation visuelle du ressenti, des niveaux authorés ou d'une manette réelle. Ces essais restent nécessaires avant de choisir le mode final.
