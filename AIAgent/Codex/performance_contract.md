# Contrat de performance — Windows PC

## Statut

Contrat de travail version 1, créé le 18 juillet 2026. La machine ci-dessous
est la référence provisoire utilisée pour la baseline ; elle doit être validée
comme configuration cible minimale avant de déclarer une garantie commerciale.

## Référence matérielle provisoire

| Élément | Référence |
|---|---|
| Système | Windows 10 x64 |
| CPU | Intel Core i7-8700 |
| RAM | 16 Go |
| GPU | NVIDIA GeForce RTX 2070, 8 Go VRAM |
| API principale | DirectX 12 |
| API de repli | DirectX 11, à qualifier séparément |
| Résolution | 1920 × 1080 |
| Affichage | fenêtré pour les benchmarks automatisés ; plein écran exclusif à qualifier avant release |
| Profil | Balanced fixe, VSync désactivée, limite de framerate désactivée |

## Scénarios homologués

Un résultat n'est valable que pour son scénario exact. Les scénarios à rendre
automatiques sont :

1. `MaisonIdle` : caméra et jeu au repos dans `Maison` ; disponible maintenant.
2. `MaisonTraversal` : déplacement continu incluant portes et changements de
   salle ; à créer avant baseline officielle.
3. `CombatVfx` : combat, VFX, UI et caméra ; à créer avant baseline officielle.
4. `PortalsAndLighting` : portails actifs et éclairage dynamique ; à créer avant
   baseline officielle.
5. `LoadUnload` : chargement, activation et déchargement représentatifs ; à
   créer seulement lorsque ce flux existe réellement.

`MaisonIdle` est un diagnostic de stabilité minimal, pas une preuve que le jeu
complet tient 60 FPS.

## Budgets en jeu interactif

| Mesure | Information | Avertissement | Blocage |
|---|---:|---:|---:|
| Frame time p95 | 13,0 ms | 14,0 ms | 16,67 ms |
| Frame time p99 | 16,67 ms | 20,0 ms | 25,0 ms |
| CPU Main Thread p95 | 11,0 ms | 12,5 ms | 13,5 ms |
| GPU p95 | 11,0 ms | 12,5 ms | 14,0 ms |
| Frame isolée | 25 ms | 50 ms | 100 ms sans cause approuvée |
| GC stable | 0 o/frame | > 1 Ko/frame | collecte perceptible ou allocation récurrente non documentée |
| Chargement interactif | 4 ms de travail propre/frame | 6 ms | frame totale > 33 ms ou chargement synchrone non justifié |

Les métriques Main Thread et Render Thread sont diagnostiques et ne sont jamais
additionnées. Une absence de compteur Render Thread ou GPU est un résultat
incomplet : elle doit être signalée, pas remplacée par une estimation.

Les budgets mémoire absolus seront gelés après trois baselines homologuées :
mémoire processus stable, pic de chargement, mémoire graphique, textures,
meshes/buffers et croissance après cycles de chargement. Avant cela, une hausse
de plus de 5 % sur le même scénario est un avertissement et doit être expliquée.

## Protocole de mesure

- Player Windows Development, commit identifié, machine au repos et sans charge
  GPU concurrente connue ;
- même résolution, API, profil, seed, caméra et scénario ;
- 10 secondes de warmup minimum ;
- 60 secondes de mesure pour une baseline, au moins trois passages ;
- VSync et limite de framerate désactivées ;
- frames de chargement, pause, perte de focus, changement de résolution et
  changement de profil exclues et comptabilisées ;
- exporter médiane, p95, p99, maximum, nombre de frames >16,67/>25/>50/>100 ms,
  CPU, GPU, GC, mémoire, draw calls/SetPass/triangles lorsque disponibles.

Une baseline n'est jamais remplacée silencieusement. Toute nouvelle baseline
compare les distributions, conserve les exécutions invalides et référence le
commit ainsi que le matériel.

## Règles de décision

- Une optimisation est acceptée seulement si elle améliore ou préserve les
  budgets mesurés, le rendu et le comportement gameplay/réseau/sauvegarde.
- Une régression confirmée supérieure à 5 % ou un dépassement de budget bloque
  la modification jusqu'à justification ou rollback.
- Les assets, visibilité, lumières, portails, streaming et qualité adaptative
  sont des leviers : aucun n'est un chantier automatique.
- Les allocations récurrentes et les hitches sans cause sont prioritaires sur
  les optimisations théoriques.
