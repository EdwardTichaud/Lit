# Camera Obstruction

Ce système garde le comportement de la caméra CRPG existante et évite de déplacer la caméra quand un mur passe entre elle et le joueur. L'obstruction est traitée visuellement : les renderers touchés sont atténués si possible, et une vignette HDRP assombrit les bords de l'écran.

## Composants

- `CameraController` reste le pilote du rig, du zoom, de la rotation, du combat et des caméras fixes.
- `CameraLineOfSightObstructionDetector` fait un Raycast ou SphereCast entre la caméra et le personnage contrôlé.
- `CameraObstacleFader` applique les valeurs de fade aux renderers obstruants avec `MaterialPropertyBlock`.
- `CameraObstructionVignetteController` crée un `Volume` HDRP runtime dédié à la vignette d'obstruction et un overlay UI de secours si le post-process n'est pas visible.

`CameraController` installe automatiquement ces composants au runtime si `obstructionVisibilityEnabled` et `autoCreateObstructionVisibilityComponents` sont actifs.

## Comportement

La caméra ne se rapproche pas et ne recule pas à cause des murs. Le champ `allowLegacyObstacleRepositioning` garde l'ancien solver physique disponible, mais il est désactivé par défaut pour ce comportement type Baldur's Gate 3.

Quand un obstacle est détecté :

- les renderers entre la caméra et le joueur reçoivent `_CameraObstructionFade` et `_CameraObstructionAlpha`;
- la vignette HDRP et son overlay de secours augmentent progressivement;
- le centre clair de la vignette suit la position écran du personnage;
- si aucun material ne supporte les propriétés d'obstruction, le fallback peut utiliser `Renderer.forceRenderingOff` temporairement pour laisser le joueur visible.

Quand la vue est dégagée, le fader remet les propriétés à l'état normal et restaure `forceRenderingOff`.

## Configuration Unity

Sur le rig caméra :

- `obstacleLayerMask` doit inclure les murs et gros obstacles. Dans `Maison.unity`, il est configuré sur `Default`, `Ground`, `Stairs` et le layer projet `CameraObstruction`.
- Le layer du joueur doit être exclu ou laissé ignoré par `ignoreTargetColliders`.
- Les petits props décoratifs peuvent être filtrés avec `minimumRendererBoundsSize`.
- Les objets non obstruants peuvent être mis hors du mask ou recevoir le tag `CameraNonObstructing`.
- Les nouveaux murs problématiques peuvent être laissés en `Default` ou déplacés vers le layer `CameraObstruction`.
- `detectionRadius` peut rester autour de `0.18`; augmenter dans les couloirs instables.
- `checkInterval` peut rester autour de `0.03` pour limiter les casts.
- `obstacleGraceTime` évite le clignotement de vignette.
- `useTargetRendererBounds` vise la hauteur réelle du personnage pour centrer la vignette sur son corps. `targetBoundsHeightFactor` ajuste cette hauteur si un modèle a un pivot atypique.
- `centerOnTarget`, `centerFollowSharpness` et `viewportCenterPadding` contrôlent le suivi du centre de vignette sur la position écran du personnage.

## Matériaux Compatibles

La voie propre est un shader HDRP/ShaderGraph qui lit :

- `_CameraObstructionFade` : `0` normal, `1` obstruant;
- `_CameraObstructionAlpha` : `1` opaque, `0` invisible.

Pour un rendu plus propre, utiliser ces valeurs pour un dither ou une transparence contrôlée. Le système ne modifie pas les materials de façon permanente et n'utilise pas `renderer.material`, donc il ne crée pas de fuite de materials instanciés.

Si un material ne supporte pas ces propriétés, le fallback `hideRendererFallback` masque temporairement le renderer obstruant. Ce fallback est utile pour prototyper mais il peut cacher un gros mesh si le mur fait partie d'un bloc combiné.

## Limites

- Les murs combinés en très grands meshes peuvent disparaître en bloc avec le fallback opaque.
- Les shaders opaques standards ne deviendront pas transparents sans propriété ShaderGraph dédiée.
- La vignette HDRP a son propre `Volume` runtime pour ne pas écraser la vignette de vitesse quand aucune obstruction n'est active.
- Si le Volume HDRP ne se voit pas selon la configuration de caméra/post-process, `createScreenOverlayFallback` affiche une vignette UI plein écran non interactive.
- Le système cible le personnage contrôlé local. Si une cinématique doit suivre une autre cible, assigner explicitement `playerTargetTransform`.

## Tests Manuels

- Passer derrière un mur entre caméra et joueur : la caméra ne doit pas changer de distance.
- Vérifier que le joueur redevient visible par fade shader ou fallback.
- Vérifier que le centre clair de la vignette reste sur le personnage quand il est décentré à l'écran.
- Sortir de l'obstruction : le renderer revient normal et la vignette disparaît progressivement.
- Tester un couloir avec plusieurs murs pour vérifier l'absence de clignotement.
- Tester rotation, zoom, caméra fixe et caméra combat.
