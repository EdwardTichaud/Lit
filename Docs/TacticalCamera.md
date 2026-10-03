# Caméra tactique UCC — comparaison développeur

## Utilisation

La Main Camera du prefab `Assets/Core/System/GameplaySessionRoot.prefab` porte maintenant `LitGameplayCameraModeController`. Le mode initial reste **ThirdPerson**. En Play, sélectionner la Main Camera du Bootstrap puis utiliser les boutons **Third-person** / **Tactical** de ce composant. L'inspecteur affiche le mode demandé, le mode effectif et l'autorité cinématique. `SetMode(GameplayCameraMode)` est également disponible pour les outils développeur.

Le profil partagé est `Assets/CombatRealTime/Camera/TacticalDefault.asset`. Tous les réglages de cadrage, amortissement, commandes, limites et délais de masquage y sont exposés. Le défilement aux bords est désactivé. Aucune préférence joueur n'est sauvegardée.

UCC reste le seul pilote de la caméra physique. La vue Adventure sérialisée n'est pas remplacée : les extensions s'installent après son initialisation. Le lock conserve ses règles de combat mais n'impose ni orientation ni secousse à la vue tactique. Une demande de bascule pendant une cinématique est différée ; le Brain et les pistes Cinemachine gardent leur relais actuel.

## Commandes

| Contexte | Commandes |
| --- | --- |
| Clavier/souris | Flèches : pan ; milieu maintenu : glissement ; droit maintenu : orbite ; molette : zoom ; C : recentrage |
| Manette, suivi | Stick gauche : personnage ; stick droit : orbite ; maintien L3 0,6 s : inspection |
| Manette, inspection | Stick gauche : pan ; stick droit : orbite ; gâchettes : zoom ; maintien L3 : sortie ; maintien R3 : sortie et recentrage |

L'action dédiée `Camera/TacticalInspection` remplace l'ancien `ToggleFreeCamera` dans l'asset d'entrées et son wrapper généré. Son binding L3 utilise un maintien de 0,6 s. La vue third-person ignore cette action et conserve le clic L3 de locomotion ; celui-ci est ignoré uniquement en mode tactique. L'inspection n'active que l'ActionMap Camera, utilise également le contexte de suppression de gameplay existant, et bloque les overrides de déplacement UCC. Les gâchettes et le D-pad ne pilotent pas la caméra hors inspection. La perte de focus, les menus, un changement de personnage, une cinématique ou une désactivation quittent l'inspection.

Les entrées souris sont consommées une fois par frame. Le survol UI et un champ de texte actif bloquent les commandes clavier/souris de caméra. Le curseur utilise une propriété temporaire partagée avec `MainMenuPointerCursor` : les menus restent prioritaires, et l'état précédent est restitué à la sortie.

## Zone intérieure configurée

Déposer `Assets/CombatRealTime/Camera/TacticalCameraTestRoom.prefab` dans une zone de test vide, son origine au niveau des pieds du personnage. Il contient un sol de 12 × 10 m, une porte au sud, des murs, un toit et trois marches. Les six groupes murs/toit référencent explicitement leurs renderers ; chaque groupe est masqué séparément. Le volume de limites du pivot est déjà configuré. Le prefab n'ajoute aucune caméra, aucun joueur ni ancien contrôleur CRPG et ne modifie pas les scènes de production.

Pour une pièce existante : placer `LitCameraOcclusionGroup` sur un parent des colliders des obstacles, puis renseigner explicitement les renderers à masquer. Les requêtes ignorent les triggers ; les obstacles doivent avoir des colliders non-trigger sur une couche physique interrogeable. Les colliders et matériaux restent inchangés ; `forceRenderingOff` est temporaire et son état initial est mémorisé. Un renderer doit appartenir à un seul groupe d'occlusion. Les volumes `LitTacticalBounds` utilisent un BoxCollider trigger ; ils s'appliquent uniquement quand le personnage est dedans. Éviter de superposer les volumes.

Les gizmos sélectionnés montrent les limites et les groupes. Activer `Show Diagnostics` du profil pour voir le pivot et la ligne de caméra. Les collisions rétractent immédiatement la caméra, filtrent le retour et vérifient également le trajet de pan et de transition. La hauteur du pivot libre ne change que si le sondage proche trouve un sol dans la marge autorisée ; sinon elle est conservée.

## Validation

`LitTacticalCameraTests` couvre la résolution des quatre couples mode/lock, la limite et son ralentissement, le contexte exclusif d'inspection, la restitution des renderers (y compris initialement cachés), la priorité/restauration du curseur, le Bootstrap et le prefab intérieur. Les lancer dans le Test Runner, onglet EditMode.

Validation Play à effectuer : démarrage third-person avec Brain désactivé ; bascules à l'arrêt/en mouvement/en lock ; commandes aux trois fréquences 30/60/120 FPS ; perte/reprise du focus ; murs, porte, marches et plusieurs étages ; masquage puis mode/Timeline/déchargement ; inspection puis menus/roues de skills ; retour de cinématiques dans chaque mode ; changement de personnage/respawn/téléportation ; deux clients réseau. Le composant, la vue, les états de pivot et les renderers masqués ne contiennent aucun état réseau ni écriture persistante.

La compilation statique n'est pas une validation visuelle du ressenti, des niveaux authorés ou d'une manette réelle. Ces essais restent nécessaires avant de choisir le mode final.
