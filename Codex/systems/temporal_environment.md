# Temps et environnement

## Rôle

Fournir l’âge temporel canonique, adapter les objets visibles et piloter
l’environnement HDRP du joueur local.

## Classes principales

- `AgeManager` : année globale calculée depuis les Ancient Flames.
- `TemporalAge` / `TemporalAgeUtility` : représentation et conversions.
- `TemporalZone` / `TemporalObject` : âge local et objets affectés.
- `TimePeriodVisibility` : filtrage des objets selon le temps.
- `EnvironmentManager` / `EnvironmentZone` : profils HDRP et blending local.
- `Flame` : source d’état utilisée notamment par `AgeManager`.
- `FlameGuidanceArcRenderer` : feedback local auto-installé qui affiche des arcs
  discrets depuis une Ancient Flame proche vers les Flames éteintes les plus proches.

## Flux principaux

- `AgeManager` collecte les Ancient Flames, compte celles allumées et calcule
  l’année globale par pas de 111 ans.
- `AncientFlameCompassUI` est un affichage client-local auto-installé qui lit
  le personnage local et pointe vers l’Ancient Flame active la plus proche, en
  filtrant optionnellement les Ancient Flames déjà allumées.
- `FlameGuidanceArcRenderer` lit le personnage local; près d’une Ancient Flame,
  il trace un arc or vers l’Ancient Flame éteinte la plus proche et un arc blanc
  vers la Flame commune éteinte la plus proche.
- Un changement actualise visibilité temporelle, affichages et propriété shader.
- Une `TemporalZone` peut appliquer explicitement un autre âge à ses objets.
- `EnvironmentManager` suit `LocalPlayerContext`, évalue les zones autour du
  personnage et mélange leurs profils vers des Volumes HDRP globaux runtime.

## Pièges observés

- Le gameplay global doit lire `AgeManager` pour éviter des calculs divergents.
- Une zone temporelle locale ne remplace pas implicitement l’âge global.
- `EnvironmentManager` est volontairement client-local et ne doit pas être synchronisé.
- Les profils HDRP source sont lus, pas modifiés; le manager travaille sur des
  profils runtime.
- Les objets interactifs peuvent être exclus par `TimePeriodVisibility`.
