# Snow Runtime Effects

Ce document décrit le système runtime qui fait scintiller la neige autour du joueur et qui ajoute les empreintes de pas sur les surfaces enneigées.

## Résumé

Le scintillement de neige ne vient pas d'un prefab placé en scène. Il est créé automatiquement en Play Mode par `SnowRuntimeEffects`.

Le système :

- trouve le personnage contrôlé localement ;
- suit sa position avec un émetteur de particules runtime ;
- raycast vers le sol sous le personnage ;
- lit la quantité de neige exposée par le matériau touché ;
- convertit cette quantité en émission, alpha et intensité de petites particules additives.

## Fichiers principaux

- `Assets/Scripts/SnowRuntimeEffects.cs`
  - `SnowRuntimeEffects` : bootstrap runtime et binding au personnage local.
  - `SnowSparkleController` : particules de scintillement.
  - `SnowFootprintEmitter` : empreintes de pas.
  - `SnowRuntimeUtility` : raycast sol, lecture des propriétés shader, création de materials runtime.
- `Assets/Shaders/ShaderGraph_MasterShader.shadergraph`
  - expose les propriétés de neige lues par le runtime.

## Démarrage runtime

`SnowRuntimeEffects` utilise `RuntimeInitializeOnLoadMethod`.

Au chargement d'une scène en Play Mode :

1. il cherche une instance existante de `SnowRuntimeEffects` ;
2. sinon il crée un GameObject caché `Snow Runtime Effects` ;
3. il ajoute ou récupère un `SnowSparkleController` ;
4. il conserve l'objet avec `DontDestroyOnLoad`.

Toutes les `0.25` seconde environ, il appelle `LocalPlayerUtils.GetControlledCharacter()`. Si le personnage contrôlé change, il donne son `Transform` au `SnowSparkleController` et ajoute un `SnowFootprintEmitter` sur ce personnage.

## Détection de surface enneigée

La fonction clé est `SnowRuntimeUtility.TrySampleSnowSurface`.

Elle effectue un `Physics.RaycastNonAlloc` vers le bas depuis le joueur. Le système :

- ignore les colliders appartenant à la hiérarchie du joueur ;
- ignore les surfaces trop verticales avec `minimumGroundNormalY` ;
- lit la quantité de neige sur le `Terrain` ou les `Renderer.sharedMaterials` associés au collider touché ;
- retourne `SnowSurfaceSample` avec `Point`, `Normal` et `SnowAmount`.

La quantité de neige est calculée depuis les propriétés matériau :

- `_SnowAmount`
- `_SnowTopThreshold`
- `_SnowBlendSoftness`

Si le matériau n'a pas `_SnowAmount`, il n'est pas considéré comme une surface de neige, sauf si un `fallbackSnowAmount` est configuré.

## Scintillement

`SnowSparkleController` crée un enfant runtime `Snow Sparkles` avec un `ParticleSystem`.

Paramètres importants :

- `shapeRadius = 3.5` : rayon de la zone de scintillement autour du joueur.
- `shapeHeight = 0.8` : hauteur de la boîte d'émission.
- `maxEmissionRate = 46` : émission maximum quand `SnowAmount` vaut `1`.
- `amountSmoothTime = 0.2` : lissage des transitions quand le joueur entre ou sort de la neige.

Chaque frame :

1. l'émetteur suit le joueur à `followHeight` au-dessus de sa position ;
2. le système sample la neige sous lui ;
3. `SnowAmount` est lissé avec `SmoothDamp` ;
4. l'émission passe de `0` à `maxEmissionRate` ;
5. l'alpha passe de `0.015` à `0.55` ;
6. l'intensité de couleur passe de `0.6` à `2.8`.

Le material des particules est créé en runtime via `CreateTransparentRuntimeMaterial` avec blending additif. Les particules sont des billboards blancs très petits, avec un gradient alpha qui apparaît puis disparaît.

## Empreintes de pas

`SnowFootprintEmitter` est ajouté au personnage contrôlé localement.

Il utilise la même détection de surface que les scintillements, puis place des quads elliptiques transparents sur le sol quand :

- le personnage est contrôlé localement ;
- il est grounded si `requireGrounded` est actif ;
- il a parcouru au moins `stepDistance` ;
- la surface contient au moins `minimumSnowAmount`.

Les empreintes utilisent un mesh ellipse et un material runtime transparent. Elles s'estompent pendant `footprintLifetime` et sont recyclées jusqu'à `maxFootprints`.

## Lien avec le MasterShader

Le `ShaderGraph_MasterShader` expose :

- `_SnowAmount`
- `_SnowThickness`
- `_SnowColor`
- `_SnowTopThreshold`
- `_SnowBlendSoftness`

Le shader applique la neige seulement sur les faces orientées vers le haut :

```hlsl
topMask = smoothstep(SnowTopThreshold, SnowTopThreshold + SnowBlendSoftness, NormalWS.y);
snowMask = saturate(SnowAmount) * topMask;
```

Ensuite :

- la couleur de surface lerp vers `_SnowColor` ;
- la position des vertices est poussée selon `_SnowThickness`.

Le runtime utilise la même logique de `topMask` pour que les particules et empreintes correspondent aux surfaces que le shader rend visuellement enneigées.

## Différence avec FX_Snow

`Assets/Prefabs/Castle/Balconies/Balcony_1.prefab` contient un GameObject `FX_Snow`.

Celui-ci est un `ParticleSystem` statique de neige locale, placé dans un prefab. Il sert plutôt à afficher une chute ou présence de neige dans un décor précis.

Il ne pilote pas le scintillement runtime autour du joueur et ne lit pas `_SnowAmount`.

## Conditions pour qu'une surface fasse briller la neige

Une surface doit :

- avoir un collider touchable par le raycast ;
- ne pas être dans la hiérarchie du personnage contrôlé ;
- avoir une normale assez horizontale ;
- utiliser un matériau qui expose `_SnowAmount` ;
- avoir `_SnowAmount > 0`.

Si les paillettes n'apparaissent pas, vérifier dans cet ordre :

1. le collider du sol ;
2. le layer inclus par `groundMask` ;
3. la normale de la surface ;
4. le shader/material et la présence de `_SnowAmount` ;
5. la valeur effective de `_SnowAmount` ;
6. le personnage local retourné par `LocalPlayerUtils.GetControlledCharacter()`.

## Points d'attention

- Le système est local au joueur contrôlé. Il ne crée pas des paillettes pour tous les personnages réseau.
- Les materials runtime utilisent `HideFlags.DontSave` ou `HideAndDontSave`, donc ils ne sont pas faits pour être édités en scène.
- Modifier les noms de propriétés shader casserait la lecture runtime.
- Les particules dépendent d'un raycast physique, pas uniquement du rendu visuel.
- Si un sol est visuellement blanc mais n'a pas `_SnowAmount`, il ne déclenchera pas ce système.
