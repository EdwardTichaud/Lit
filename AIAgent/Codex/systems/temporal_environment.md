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
