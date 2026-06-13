# Visibility Optimization

Ce document décrit le système global d'optimisation de visibilité ajouté pour limiter le rendu et certains coûts runtime sans casser la logique de jeu.

## Problèmes observés

Audit rapide de `Assets/Scenes/Maison.unity` par lecture YAML hors Unity :

- environ 3714 renderers dans la scène ;
- seulement 10 `LODGroup` détectés ;
- environ 3704 renderers sans `LODGroup` ;
- 49 lights ;
- environ 103 déclarations `Update`, `LateUpdate` ou `FixedUpdate` dans les scripts du projet.

Le système remplace l'ancien culling décor par une optimisation opt-in par objet, avec une séparation claire entre rendu, lights et éventuelles pauses de scripts compatibles. L'obstruction caméra/XRay reste traitée comme une protection prioritaire.

## Architecture

### `VisibilityOptimizationManager`

Manager global à placer une fois dans la scène. Il évalue les `OptimizableObject` par lots selon :

- distance caméra ;
- distance joueur ;
- frustum caméra ;
- catégorie ;
- hystérésis pour éviter le clignotement ;
- protection active du système de visibilité caméra-joueur.

Il ne fait pas de `SetActive(false)` sur les objets et ne désactive pas les colliders.

### `OptimizableObject`

Composant à ajouter sur les racines d'objets que l'on veut optimiser. Il peut contrôler séparément :

- `Renderer` / `SkinnedMeshRenderer` ;
- `Light` ;
- comportements explicitement compatibles avec `IPausableWhenInvisible` ou `IVisibilityUpdateRateTarget`.

Les catégories disponibles sont :

- `StaticMesh`
- `DynamicObject`
- `Light`
- `NPC`
- `Decoration`
- `Interactive`
- `Critical`

`Critical` et `Never Cull` restaurent tout et excluent l'objet du culling.

### Interfaces de pause

Un script ne peut être pausé ou ralenti que s'il implémente explicitement :

- `IPausableWhenInvisible`
- `IVisibilityUpdateRateTarget`

Le manager ne désactive pas les `MonoBehaviour` arbitrairement.

### Protection caméra-joueur

`XRayVisibilityController` reste prioritaire. Quand il détecte un mur/obstacle entre la caméra et le joueur, il signale ses renderers à `CameraVisibilityProtection`.

Tant qu'un renderer est protégé :

- `VisibilityOptimizationManager` ne le coupe pas ;
- les colliders restent actifs ;
- les scripts restent actifs ;
- le lighting et l'occlusion physique ne sont pas supprimés.

Pour verrouiller ce comportement sur un objet, ajouter `CameraVisibilityObstacle`.

### `PlayerVisibilityFader`

Composant optionnel, non installé automatiquement, qui peut appliquer un fade visuel via `MaterialPropertyBlock` sur plusieurs obstacles détectés par `SphereCastNonAlloc`. Il sert de base robuste si l'on veut remplacer ou compléter le rendu XRay actuel, mais il ne doit pas être activé sans test shader.

## Lights

`RoomLightZoneController` pilote les lights d'une pièce/zone sans raycast permanent par light :

- activation par caméra/joueur dans ou près des bounds de zone ;
- capture et restauration des états runtime ;
- limitation optionnelle des ranges ;
- ombres désactivées sur lights non importantes ;
- résolution d'ombre HDRP réduite et restaurable.

Stratégie recommandée :

- lights statiques : baking ;
- lights qui doivent influencer des objets dynamiques : mixed ;
- realtime seulement pour torches, braseros, effets importants ;
- ranges courts ;
- shadow casting uniquement sur les lights importantes ;
- shadow resolution 256/512 pour les lights secondaires ;
- zones/rooms plutôt que raycasts permanents.

## Menus Unity

Menus ajoutés :

- `Tools/Lit/Visibility Optimization/Audit Active Scene`
- `Tools/Lit/Visibility Optimization/Install Manager In Active Scene`
- `Tools/Lit/Visibility Optimization/Add OptimizableObject To Selection`
- `Tools/Lit/Visibility Optimization/Mark Selection As Camera Visibility Obstacles`

L'ajout d'`OptimizableObject` via le menu refuse les roots qui portent des comportements critiques connus : managers, joueur, fantômes, interactions, triggers importants, netcode/persistence, etc.

## Intégration recommandée

1. Installer `VisibilityOptimizationManager` dans la scène via le menu.
2. Lancer `Audit Active Scene`.
3. Ajouter `OptimizableObject` seulement sur des roots de décoration ou static mesh simples.
4. Vérifier que les murs utilisés par le XRay ont `CameraVisibilityObstacle` si nécessaire.
5. Ajouter `RoomLightZoneController` sur des zones/pièces avec un collider trigger ou des bounds clairs.
6. Tester d'abord avec logs/gizmos sur une petite zone.
7. Élargir progressivement aux grands groupes de décor.

## Garde-fous

- Pas de `SetActive(false)`.
- Pas de colliders désactivés.
- Pas de `MonoBehaviour.enabled = false` automatique.
- Pas de désactivation d'un renderer protégé par la visibilité caméra-joueur.
- Les objets interactifs proches du joueur restent visibles via `playerKeepAliveDistance`.
- `Critical` et `Never Cull` excluent complètement un objet.
- Les modifications de lights faites par `RoomLightZoneController` sont restaurées au disable.

## Tests manuels

- Entrer dans `Maison` et vérifier déplacement joueur, interactions, sauvegarde et triggers.
- Passer derrière plusieurs murs : le XRay doit continuer à rendre le joueur lisible.
- Vérifier qu'un mur rendu transparent visuellement conserve collision et ombres.
- Activer les gizmos du manager et des `OptimizableObject`.
- Tourner la caméra rapidement pour vérifier l'absence de pop-in violent.
- Profiler avant/après : main thread, render thread, realtime lights, shadow casters, set pass calls.
