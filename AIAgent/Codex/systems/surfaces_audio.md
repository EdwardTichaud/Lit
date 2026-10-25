# Surfaces et audio contextuel

## Rôle

Décrire les surfaces du monde une seule fois par type afin que les systèmes de
feedback puissent adapter sons, impacts, VFX ou decals sans hardcoder les
matériaux dans le code.

## Classes principales

- `SurfaceDefinition` : ScriptableObject par type de surface, avec sons de pas,
  volume, variation de pitch et références réservées aux futurs impacts/VFX/decals.
- `SurfaceProvider` : composant posé sur un sol, un collider ou un parent de
  collider; référence une `SurfaceDefinition`.
- `SurfaceResolver` : résout une surface depuis un `RaycastHit`, d'abord sur le
  collider touché puis dans les parents, avec fallback fourni par l'appelant.
- `SurfaceFootstepPlayer` : raycast vers le bas depuis le personnage, résout la
  surface sous le joueur et joue un clip de pas aléatoire via `AudioManager`.

## Flux principal

`SurfaceFootstepPlayer` mesure la distance parcourue par le personnage. À chaque
pas, il raycast le sol, appelle `SurfaceResolver`, récupère la surface ou sa
surface par défaut, puis joue un clip aléatoire avec le volume et le pitch de
`SurfaceDefinition`.

## Pièges observés

- Ne pas créer une `SurfaceDefinition` par GameObject : créer un asset par type
  de surface (`Wood`, `Metal`, `Grass`, `Concrete`, etc.).
- Le fallback par défaut est porté par le système appelant, pas par un singleton
  global, afin d'éviter un état statique runtime.
- Les futurs impacts/VFX/decals doivent réutiliser `SurfaceResolver` plutôt que
  refaire une résolution parallèle.


## Musique synchronisee a l'intro video

Le lecteur UI peut rendre audibles les seuls canaux musique au repere media de
19 s. AudioManager.PlayMusicImmediately stoppe le titre precedent encore muet,
applique les volumes/pitch/loop du clip et demarre le nouveau titre directement.
IsMusicSource identifie ces canaux sans recherche par nom de GameObject.

GameFlow garde temporairement un override sur Music_Maison (clip du HubManifest).
Le ZoneRuntimeContext de Maison est configure avant liberation de cet override :
le garde existant de PlayMusic conserve le meme AudioSource deja en lecture.
Pas de reset du morceau a la fin de la video ni de dependance a la vitesse de
chargement. Les cycles de mute du VideoPanel retirent ces canaux de leur snapshot
lorsqu'ils sont rendus audibles, pour ne pas les remuter lors de la fermeture.
Le pre-roll et le contrat de temps sont decrits dans systems/runtime_session.md.

Validation : compilation native et suite Unity 27/27 passee. Le controle audio
cible le choix des canaux a restituer et leur etat mute au handoff ; l'ecoute
et la position reelle du morceau pendant un chargement complet restent a qualifier.
