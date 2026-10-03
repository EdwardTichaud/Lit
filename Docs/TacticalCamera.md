# Caméra tactique UCC — comparaison développeur

## Utilisation

La Main Camera du prefab `Assets/Core/System/GameplaySessionRoot.prefab` porte maintenant `LitGameplayCameraModeController`. Le mode initial reste **ThirdPerson**. En Play, sélectionner la Main Camera du Bootstrap puis utiliser les boutons **Third-person** / **Tactical** de ce composant. L'inspecteur affiche le mode demandé, le mode effectif et l'autorité cinématique. `SetMode(GameplayCameraMode)` est également disponible pour les outils développeur.

Le profil partagé est `Assets/CombatRealTime/Camera/TacticalDefault.asset`. Tous les réglages de cadrage, amortissement, commandes, limites et délais de masquage y sont exposés. Le défilement aux bords est désactivé. Aucune préférence joueur n'est sauvegardée.

UCC reste le seul pilote de la caméra physique. La vue Adventure sérialisée n'est pas remplacée : les extensions s'installent après son initialisation. Le lock conserve ses règles de combat mais n'impose ni orientation ni secousse à la vue tactique. Une demande de bascule pendant une cinématique est différée ; le Brain et les pistes Cinemachine gardent leur relais actuel.

## Commandes

| Contexte | Commandes |
| --- | --- |
| Clavier/souris | Droit maintenu : orbite ; molette : zoom ; C : recentrage. Flèches et glissement milieu : uniquement en caméra libre |
| Manette, suivi | Stick gauche : personnage ; stick droit : orbite ; clic L3 : caméra libre |
| Manette, inspection libre | Stick gauche : pan ; stick droit : orbite ; gâchettes : zoom ; clic L3 : retour au suivi ; maintien R3 : sortie et recentrage |

La caméra suit et vise le joueur par défaut, y compris lorsqu'une collision modifie sa position réelle. Un pan ne détache jamais ce suivi. L'action dédiée `Camera/TacticalInspection` remplace l'ancien `ToggleFreeCamera` dans l'asset d'entrées et son wrapper généré. Son binding L3 utilise maintenant un simple clic, sans maintien. La vue third-person ignore cette action et conserve le clic L3 de locomotion ; celui-ci est ignoré uniquement en mode tactique. Le second clic L3 réactive le suivi et recentre progressivement. L'inspection n'active que l'ActionMap Camera, utilise également le contexte de suppression de gameplay existant, et bloque les overrides de déplacement UCC. Les gâchettes et le D-pad ne pilotent pas la caméra hors inspection. La perte de focus, les menus, un changement de personnage, une cinématique ou une désactivation quittent l'inspection.

Le pivot libre reste limité à 20 m horizontalement (`Maximum Pan Radius`). Une seconde limite de 24 m borne la position de caméra, zoom et hauteur compris (`Maximum Free Camera Distance`). Ces deux limites restent réglables dans le profil. Les volumes de scène ne contraignent que le pivot libre : ils ne peuvent plus retenir la caméra de suivi lorsque le personnage se déplace.

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

`LitTacticalCameraTests` couvre la résolution mode/lock et obstacles/suivi/libre/cinématique, le défaut Sliding, la projection d'une cible derrière la caméra, la compatibilité de tous les slots et le diagnostic unique, le contrat des trois graphs, la restitution des blocs renderer et par slot (en préservant les autres effets), les colliders/matériaux, les limites, le contexte exclusif d'inspection, le curseur, le Bootstrap et la pièce compatible. Les lancer dans le Test Runner, onglet EditMode.

Vérifications effectuées pour le choix d'obstacles : compilation C# runtime et éditeur sans erreur (avertissements existants hors caméra) ; dans un projet Unity 6000.4.9f1/HDRP 17.4 isolé, 14 tests EditMode ciblés passent et les 66 variantes de passes des trois graphs compilent en D3D11, avec et sans `_ALPHATEST_ON`. Une erreur latente de nom du slot Alpha Clip Threshold du graph glace, révélée par l'activation du clipping, a été corrigée. Ces vérifications n'ont pas lancé la scène gameplay et ne remplacent pas les essais visuels ci-dessous.

Validation Play à effectuer : démarrage third-person avec Brain désactivé ; bascules à l'arrêt/en mouvement/en lock ; commandes aux trois fréquences 30/60/120 FPS ; perte/reprise du focus ; murs, porte, marches et plusieurs étages ; masquage puis mode/Timeline/déchargement ; inspection puis menus/roues de skills ; retour de cinématiques dans chaque mode ; changement de personnage/respawn/téléportation ; deux clients réseau. Le composant, la vue, les états de pivot et les renderers masqués ne contiennent aucun état réseau ni écriture persistante.

Comparer Sliding et VisibilityMask contre un mur, dans la porte et sous le toit ; vérifier les deux ouvertures en lock, leur union, le décor derrière la cible, L3, les changements d'option en Play et les Timelines. Vérifier HDRP/TAA à plusieurs résolutions et formats, avec deux clients : absence de profondeur invisible, de scintillement excessif et d'effet sur l'autre client. La compilation statique n'est pas une validation visuelle du ressenti, des niveaux authorés ou d'une manette réelle. Ces essais restent nécessaires avant de choisir le mode final.
