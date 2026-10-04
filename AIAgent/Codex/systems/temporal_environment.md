# Temps et environnement

## Rôle

Fournir l’âge temporel canonique, adapter les objets visibles et piloter
l’environnement HDRP du joueur local.

## Classes principales

- `AgeManager` : année globale calculée depuis les Ancient Flames.
- `TemporalAge` / `TemporalAgeUtility` : représentation et conversions.
- `TemporalZone` / `TemporalObject` : âge local et objets affectés.
- `TimePeriodVisibility` : filtrage des objets selon le temps.
- `LocalVolumeAnchor` : ancre les Volumes HDRP locaux sur le personnage contrôlé,
  plutôt que sur la caméra à la troisième personne.
- `LocalVolumeSunController` : lie une racine de lumière directionnelle au
  Volume local et l'active uniquement lorsque le personnage est dans cette
  zone.
- `Flame` : source d’état utilisée notamment par `AgeManager`; affiche en
  runtime une sphère transparente sans collider calée sur sa zone
  `LitInfluenceSource` quand l'influence est visible.
- `FlameGuidanceArcRenderer` : feedback local auto-installé qui affiche des arcs
  discrets depuis une Ancient Flame proche vers les Flames éteintes les plus proches.

## Flux principaux

- `AgeManager` collecte les Ancient Flames, compte celles allumées et calcule
  l’année globale par pas de 111 ans.
- `AncientFlameCompassUI` est un affichage client-local posé dans la scène. Il
  lit le personnage local, tourne son cadran selon la caméra pour afficher les
  points cardinaux et oriente sa flèche vers l’Ancient Flame active la plus
  proche.
- `FlameGuidanceArcRenderer` lit le personnage local; près d’une Ancient Flame,
  il trace un arc or vers l’Ancient Flame éteinte la plus proche et un arc blanc
  vers la Flame commune éteinte la plus proche.
- Un changement actualise visibilité temporelle, affichages et propriété shader.
- Une `TemporalZone` peut appliquer explicitement un autre âge à ses objets.

## Pièges observés

- Le gameplay global doit lire `AgeManager` pour éviter des calculs divergents.
- Une zone temporelle locale ne remplace pas implicitement l’âge global.
- Les profils HDRP sont uniquement portés par les `Volume` configurés dans les
  scènes Core et leurs scènes additives. `ZoneManifest` ne pilote aucun profil
  visuel runtime.
- Dans `District_1_Core`, les Volumes locaux sont évalués depuis le personnage.
  Leur `BoxCollider` définit la zone pleine et `blendDistance` la bande de fondu
  autour de cette limite. Une caméra qui traverse seule une limite ne modifie pas
  l'environnement du joueur.
- Dans `District_1_Core`, chaque `LocalVolumeSunController` est directement
  porté par son GameObject HDRP `Volume`. `Castle_Volume` pilote `Moon Light`
  et `Oldbarn_Volume` pilote `Sun Light`. Les deux lumières commencent inactives
  et ne sont activées qu'après l'évaluation de la position du personnage. Une
  référence de Volume vide signifie « utiliser ce GameObject ». L'intensité et
  la couleur restent entièrement réglées par les composants Unity de la lumière.
- Les objets interactifs peuvent être exclus par `TimePeriodVisibility`.

## Direction validée : chaleur et givre du château

Le château reste sous un froid glacial : les matériaux utilisent leur état
`Ice` par défaut. Une source de chaleur applique localement et progressivement
un état `Normal`, puis le givre revient lorsque la source s'éloigne. Cette
influence visuelle ne doit jamais être confondue avec la visibilité HDRP d'un
Volume.

La torche du joueur est une source mobile de chaleur : elle révèle les
interactions et active les objets qui exigent une influence suffisante. Sa
réserve décroît seulement lorsqu'elle est allumée; combustible et braseros la
rechargent. Une réserve vide éteint la torche, mais ne bloque jamais le
déplacement ou l'inventaire du joueur. Les Flames et Ancient Flames s’allument
désormais directement avec Interact, à proximité : Munin n’est plus requis.

Munin demeure un élément de lore, sans présence ni mécanique jouable actuelle.
La direction retenue pour une future réintroduction est une fusion de Lucian et
Munin, librement inspirée de *Shaman King*, afin de déverrouiller des actions
spécifiques. Aucun comportement de cette future fusion n’est implémenté.

Le rendu repose sur une ambiance bleu nuit très faible, un faisceau lunaire
directionnel localisé, un brouillard volumétrique et des sources pratiques.
Les flammes chaudes guident l'exploration; le cyan reste réservé aux éléments
surnaturels et aux mécaniques de puzzle.

`Lit_Volume_Castle` porte l'étalonnage local : exposition fixe à `-0,35 EV`,
brume volumétrique bleu nuit (28 m de libre parcours moyen, anisotropie `0,45`,
distance maximale 62 m)
et contraste modéré. `Castle_Volume` n'active sa `Moon Light` qu'à l'intérieur
de sa zone; elle éclaire à `1,1 lux`, projette des ombres douces et renforce la
brume sans devenir une lumière d'ambiance uniforme. Les intensités des sources
pratiques restent les réglages auteurs qui guident l'exploration.

`Lit_ContrastLight` est le prefab HDRP commun des sources locales. Son composant
`LitContrastLight` offre les rôles Faisceau lunaire, Flamme et Magie/cristal, et
applique leurs couleurs, unités, portées, ombres souples et contribution
volumétrique depuis l'Inspector. Il ne pilote ni l'exposition ni le bloom du
Volume. Dans le château, le fill indirect est réduit (`0,28`), les réflexions
restent limitées (`0,65`) et les probes à `0,75` : le fond peut ainsi s'effacer,
pendant que les faisceaux de lune et sources pratiques créent le contraste.
Le bloom du profil est réservé aux émissions qui franchissent son seuil : torche,
braises, magie et cristaux de glace. Les matériaux d'armes doivent rester sous
ce seuil.

Les Flames communes utilisent une source locale chaude, courte et ombrée. Les
Ancient Flames combinent un coeur chaud et une aura cyan verticale de puissance
égale ; l'aura reste sans ombre pour éviter une seconde source de fill. La torche
joueur garde une portée de 5 m, car sa portée HDRP pilote aussi son rayon
d'influence de gameplay. `LitAtmosphereParticles` référence des systèmes de
particules sérialisés : la poussière froide du `Castle_Volume`, les braises des
Flames et le mélange froid/chaud des Ancient Flames. Il ne crée aucun GameObject
ni matériau à l'exécution. Les Flames relaient `Flame.StateChanged` à leurs
particules ; la torche révèle seulement la poussière globale. Les matériaux HDRP
dédiés conservent la texture alpha, le seuil de coupe et le fondu transparent
afin d'éviter les quads opaques. L'outil `Lit/Lighting/Rebuild Castle Atmosphere`
reconstruit les références si elles sont invalides.

Le Shader Graph `LitIceFrostedEdges_v3` rend le corps de glace sombre et mat,
mais garde les arêtes géométriques et texturées plus lisses afin d'y concentrer
les reflets bleus. Dans l'influence d'une flamme, il révèle les textures de
l'état normal, les réchauffe légèrement et augmente leur aspect humide. Le
milieu du fondu d'influence devient une bordure de fonte bruitée et animée,
avec gouttelettes et une très faible émission orange si l'émission du matériau
est activée.
