# Maison — visibilité tactique à travers le décor

`Maison_Core` contient `Maison_TacticalVisibility` (`LitMaisonVisibilityZone`). Au chargement de `Maison_Environment_Near_1` à `_4`, ce composant prépare les murs, arches, colonnes, toits et rochers nommés comme tels. Il exclut les sols, chemins, escaliers praticables, objets animés et les autres scènes. Les LOD sont tous préparés, même lorsqu'ils ne sont pas actuellement affichés.

En caméra tactique **IgnoreObstacles**, tant que le personnage est dans l'emprise du décor préparé, un masque circulaire révèle le joueur et la cible verrouillée. La caméra conserve son zoom et sa pose UCC. Le masque se désactive en L3, hors zone, en third-person et pendant les Timelines ; le clipping adaptatif reste désactivé.

## Rendu et détection

- Le shader HDRP/Lit du pack MedievalRuins ne possède pas le contrat de découpe. Une variante dédiée (`LitMaisonHDRPLit.shader`) conserve ses propriétés et passes natives HDRP 17.6, avec un hook `LitMaisonSurface.hlsl` ajoutant uniquement le masque. Les textures, couleurs, normales, propriétés métalliques et lissages sont copiés depuis chaque matériau original.
- Les shaders Master et Ice déjà compatibles gardent leur propre shader. Leurs passes de découpe sont activées dans des copies locales, jamais dans les assets partagés. Les colliders restent inchangés.
- La détection complète les raycasts par l'intersection du cercle écran avec les bounds des renderers : pas de dépendance à un collider sur chaque élément décoratif. Elle englobe le bord du cercle et les parois contenant la caméra. Le fragment shader ne coupe que la géométrie devant le personnage, pas le décor derrière lui.
- Le centre du cercle utilise les poses affichées de la caméra et du personnage, pas leur mélange avec une pose physique antérieure. Rayon, bord adouci et fondu utilisent les réglages existants du profil tactique.
- La découpe native s'applique aux passes raster couleur/profondeur/normales/mouvement, sans changer les ombres, le baking ou le ray tracing. La variante doit être revue lors d'une mise à jour majeure de HDRP.

## Diagnostic développeur

En Play, sélectionner `Maison_TacticalVisibility` pour lire le nombre de renderers préparés et incompatibles. La MainCamera affiche `Maison visibility mask: Active (camera distance preserved)` lorsque le masque est actif. Un matériau inconnu n'est pas remplacé arbitrairement : son renderer reste inchangé et un avertissement agrégé apparaît au chargement de sa scène.

Le matériau `Resources/MaisonVisibilityNativeLit` maintient une référence explicite au shader et à sa variante opaque normale/cutout pour les builds. Si de nouveaux types de matériaux HDRP/Lit sont introduits dans Maison, vérifier aussi leurs variantes dans un build joueur.

À la désactivation ou au déchargement, les groupes temporaires et leurs blocs de propriétés sont restitués, puis les références originales de matériaux sont rétablies. Les matériaux remplacés par un autre système entre-temps ne sont pas écrasés. Aucun état de masque n'est répliqué.

## Vérification

Les tests `LitMaisonVisibilityTests` couvrent le filtrage des architectures, les éléments sans collider, les ratios d'écran, la caméra dans une paroi, les obstacles derrière le personnage, la non-mutation des matériaux sources et la conservation des propriétés HDRP/Lit. Le test natif compile toutes les passes raster disponibles en D3D11.

Essai visuel requis dans la vraie scène : murs et colonnes de la vidéo, changement de LOD, orbite, zoom 1–20 m, joueur et ennemi occultés, L3, Timeline et chargement/déchargement des scènes. Les tests isolés de shader et de géométrie ne constituent pas une validation visuelle du niveau sous HDRP/TAA.
