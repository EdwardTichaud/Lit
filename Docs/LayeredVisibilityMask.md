# Visibilité du joueur à travers plusieurs couches

Le masque traite tous les groupes compatibles qui occupent son disque écran devant le joueur ou la cible, sans s'arrêter à la première paroi. Les groupes configurés manuellement sont inclus, même sans collider ou hors des préparations automatiques Maison/District_1. Les requêtes physiques complémentaires agrandissent leurs tampons lorsqu'ils sont pleins ; les capacités initiales 128/64 ne tronquent plus les résultats.

## Ouverture commune

`LitTacticalMaskStack` maintient un fondu commun par personnage protégé. Toutes les couches vues dans le même disque reçoivent la même ouverture. Un nouvel obstacle ou LOD entrant derrière une paroi déjà découpée hérite immédiatement de cette ouverture, au lieu de cacher à nouveau le personnage pendant un second fondu. Le joueur et l'ennemi gardent deux états indépendants ; changer de cible réinitialise uniquement celui de l'ennemi.

La détection est également actualisée depuis la pose réellement affichée avant le rendu. L'arrivée/le retrait d'un groupe force une nouvelle recherche. Les délais de détection, de restitution et de fondu restent ceux du profil.

## Étages et sol

Les sols, marches et surfaces praticables des zones automatiques sont désormais préparés avec `Overhead Surface`. Ils ne sont sélectionnés que s'ils dépassent la hauteur protégée ; le shader ne découpe que leurs fragments au-dessus des pieds du personnage, plus `Mask Ground Clearance` (0,25 m par défaut). Ainsi, un étage supérieur peut s'ouvrir pour révéler le joueur, sans ouvrir de trou dans son sol d'appui. La protection est calculée séparément pour le joueur et l'ennemi.

Sur un groupe manuel de planchers, cocher `Overhead Surface`. Séparer les surfaces praticables des parois ordinaires dans les groupes authorés. Le protocole shader garde les mêmes propriétés : `_LitMaskEnabled = 1` pour les parois, `2` pour les surfaces protégées ; les composantes `w` des vecteurs caméra portent les deux hauteurs de protection, leurs `xyz` restant inchangés.

## Restitution

Un renderer référencé par plusieurs groupes ne reçoit qu'une écriture par rendu. Il ne peut donc pas restaurer le masque d'un autre groupe après l'avoir retiré. Les matériaux partagés, les colliders et les propriétés des autres effets restent inchangés. L3, Timeline, changement de mode, désactivation et déchargement conservent leurs règles de restitution.

## Vérification

Les tests couvrent les empilements sans collider, l'entrée d'une nouvelle couche dans une ouverture existante, les deux cibles, 150 colliders superposés, les sols protégés et les groupes ayant des renderers communs. Un test GPU D3D11 utilise la fonction HLSL de production : contrôle opaque, découpe de la première couche seule (joueur encore caché), puis découpe de trois murs et d'un plancher supérieur (joueur visible), conservation du sol et du décor arrière et restitution de toutes les parois. Les tests de shaders Maison/District_1 restent des régressions du pipeline natif.

Le masque ne peut pas découper un shader dépourvu de son contrat. Les diagnostics de compatibilité existants restent actifs ; aucun changement arbitraire de shader n'est appliqué à des matériaux inconnus. Les essais des niveaux réels sous HDRP/TAA restent distincts des contrôles isolés de géométrie et de pixels.

Validation de cette correction : compilation runtime/éditeur réussie ; 67 tests réussis dans le projet Unity isolé, dont lecture de pixels GPU et chargement/désactivation/réactivation/déchargement en Play d'une scène avec trois murs et un plancher supérieur. Les sources testées ont été comparées aux sources livrées. Le contrôle GPU constate successivement un mur opaque, un second obstacle après découpe du premier, le joueur visible après découpe de toute la pile, puis les murs restaurés ; le sol et le décor arrière restent visibles.
