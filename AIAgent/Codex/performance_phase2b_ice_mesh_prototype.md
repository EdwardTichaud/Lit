# Phase 2B — Prototype contrôlé glace et maillages hors budget

## Statut

Phase 2B terminée le 18 juillet 2026.

- Aucun asset historique supprimé, déplacé ou écrasé.
- Aucune référence de production modifiée par le prototype.
- Toutes les variantes utilisent de nouveaux GUID.
- La scène `Maison`, le réseau, les colliders, la sauvegarde et la simulation
  autoritaire n'ont pas été modifiés.
- La phase 3 n'a pas été commencée.

Conclusion : conserver les changements de la phase 2A. Préparer, dans un jalon
ultérieur séparé, une migration conditionnelle de la roche A vers son mesh
source et du grand `Model` vers la variante décimée monolithique. Ne pas migrer
la roche B ni la variante découpée dans leur état actuel.

## Protocole

Mesures effectuées dans un Windows Development Player :

| Paramètre | Valeur |
|---|---|
| Unity | 6000.4.9f1 |
| API | DirectX 12 |
| Résolution | 1920 × 1080 |
| Profil | Balanced fixe, adaptation désactivée |
| CPU | Intel Core i7-8700 |
| GPU | NVIDIA GeForce RTX 2070 |
| Préchauffage | 5 secondes |
| Mesure | 15 secondes |
| Répétitions | 3 par variante |
| Seed | 1337 |
| VSync / limite FPS | désactivées |

Les scènes de comparaison fixent la caméra, l'exposition HDRP, le ciel, les
lumières, la résolution et le seed. Quatre captures ont été réalisées pour
chaque variante : proche, lointaine, angle rasant et transition.

`Balanced` est le seul profil concerné à ce stade : le projet ne possède pas
encore de service de profils qui sélectionne une variante de qualité du shader
de glace pour `Performant` ou `HighFidelity`. Les trois utilisent donc encore
le même chemin de glace. Les normales, la rugosité et les reflets ont été jugés
dans le rendu composite sous lumière et exposition fixes ; aucun remplacement
de shader par une vue debug n'a été utilisé, car il aurait changé le chemin
mesuré. Il faudra ajouter les captures par profil lorsque ces variantes de
shader existeront réellement.

Le p95 du Render Thread n'était pas exposé par `ProfilerRecorder` dans ce
Player. `Draw Calls Count` n'était pas disponible non plus ; `Batches Count` a
été utilisé comme compteur de secours lorsque disponible. Les résultats CPU et
GPU ne sont jamais additionnés.

## Validation indépendante de la phase 2A

La comparaison utilise deux tableaux physiques distincts : 32 influences pour
le chemin legacy et 4 pour le chemin optimisé. Le test contient 64 renderers,
chacun exposé à 8 influences actives. Les premières mesures où les deux chemins
recevaient encore un tableau de 32 éléments ont été rejetées et ne participent
pas aux résultats ci-dessous.

| Mesure | Legacy | Optimisé | Écart médian |
|---|---:|---:|---:|
| Frame p95 | 104,62 ms | 95,60 ms | -8,62 % |
| GPU p95 | 101,96 ms | 93,75 ms | -8,05 % |
| GC moyen | 3 176 o/frame | 1 602 o/frame | -49,56 % |
| Renderers examinés p50 | 64 | 0 | -100 % hors transition |
| Influences retenues par renderer | 8 | 4 | -50 % |
| Influences écartées par renderer | 0 | 4 | attendu |

Séries frame p95 :

- legacy : 104,62 / 101,04 / 108,41 ms ;
- optimisé : 95,60 / 118,92 / 21,70 ms.

Séries GPU p95 :

- legacy : 103,07 / 98,13 / 101,96 ms ;
- optimisé : 93,75 / 114,45 / 16,21 ms.

La réduction du travail fonctionnel est confirmée : une fois les transitions
terminées, le p50 passe de 64 renderers parcourus et réécrits à zéro. La limite
de quatre influences est également observée : 8 candidates, 4 retenues et 4
écartées par renderer. Aucune allocation n'est créée par les tableaux
d'influences, mais le scénario complet conserve des allocations de diagnostic
propres au Development Player.

Le rendu est pratiquement identique : MAE moyen de 0,316 niveau RGB sur 255 et
0,001 % des pixels au-dessus du seuil diagnostique. La variance du chemin
optimisé est toutefois trop élevée pour considérer le gain temporel de 8 %
comme une baseline statistique définitive. La décision de conserver la phase 2A
repose sur la baisse déterministe du travail, la parité visuelle et l'absence de
régression médiane, avec une nouvelle mesure à prévoir dans `Maison`.

## Audit des trois assets hors budget

| Asset | Sommets | Triangles | Fichier | Mémoire Editor | Buffer GPU estimé | Build packé |
|---|---:|---:|---:|---:|---:|---:|
| `Model` généré | 5 999 997 | 1 999 999 | 912,00 Mo | 912,00 Mo | 456,00 Mo | 456,00 Mo |
| roche A générée | 481 068 | 160 356 | 88,52 Mo | 88,52 Mo | 44,26 Mo | 44,26 Mo |
| roche B générée | 481 068 | 160 356 | 88,52 Mo | 88,52 Mo | 44,26 Mo | 44,26 Mo |

Les trois assets représentent 1 089 045 230 octets, soit environ 1,014 Gio et
48 % des 2 271 026 108 octets de meshes de glace générés audités.

La taille d'artefact importé séparée n'est pas exposée de manière stable par
l'API Unity utilisée et reste explicitement à `-1` dans le rapport JSON ; elle
n'est pas remplacée artificiellement par la taille du fichier source. La taille
dans `Assets`, la mémoire runtime Editor, l'estimation des buffers GPU et la
contribution packée réelle du BuildReport sont fournies séparément.

Ils sont tous lisibles, en index 32 bits, avec normales, tangentes et couleurs
de sommets barycentriques. Les roches conservent UV0, UV1 et quatre submeshes ;
`Model` possède UV0 et un submesh. Aucun ne contient de blendshape.

DirectX 12 signale à chaque chargement des versions générées que son upload
buffer de 16 Mio est trop petit : 431 999 784 octets demandés pour `Model` et
42 333 984 octets pour chaque roche. Cet avertissement disparaît avec les
variantes légères correspondantes.

### Dépendances et inclusion Player

Chaque mesh historique est référencé par :

- son prefab de glace de production ;
- `Assets/Environment/Prefabs_Ice/LitIcePrefabCatalog.asset` ;
- une occurrence directe dans `Assets/Scenes/Maison/Maison.unity` ;
- les prefabs de test Phase 2B, isolés du build de production.

Occurrences directes dans `Maison` :

| Asset | Ligne | Type de référence |
|---|---:|---|
| `Model` | 425637 | override `m_Mesh` d'une instance de prefab |
| roche A | 1682916 | `MeshFilter.m_Mesh` |
| roche B | 1824436 | `MeshFilter.m_Mesh` |

Les trois noms sont présents dans le `sharedassets0.assets` du Player baseline
de `Maison` de la phase 1B : ils sont donc réellement inclus dans un Player de
production. Aucun chargement dynamique C# supplémentaire n'a été trouvé. Le
catalogue est utilisé par les générateurs Editor, pas par le runtime. Aucun des
trois GUID n'est directement affecté à un `MeshCollider` ; les colliders de
gameplay sont distincts et doivent rester inchangés pendant une future migration.

## Comparaison des géométries

Les nombres ci-dessous sont les médianes des trois p95. Les séries complètes
sont conservées dans les rapports bruts.

| Variante | Sommets | Tris | Renderers | Frame p95 | GPU p95 | Build packé | Décision |
|---|---:|---:|---:|---:|---:|---:|---|
| roche A générée | 481 068 | 160 356 | 1 | 133,75 ms | 127,76 ms | 44,26 Mo | référence |
| roche A source | 82 269 | 160 356 | 1 | 121,59 ms | 113,22 ms | 6,53 Mo | candidate conditionnelle |
| roche B générée | 481 068 | 160 356 | 1 | 143,40 ms | 141,27 ms | 44,26 Mo | référence |
| roche B source | 82 256 | 160 356 | 1 | 153,17 ms | 152,36 ms | 6,53 Mo | attente |
| `Model` généré | 5 999 997 | 1 999 999 | 1 | 567,86 ms | 546,15 ms | 456,00 Mo | référence hors budget |
| `Model` source | 1 207 434 | 1 999 999 | 1 | 206,71 ms | 160,13 ms | 62,64 Mo | rejeté comme solution finale |
| `Model` décimé 9 % | 142 976 | 179 999 | 1 | 97,44 ms | 75,41 ms | 6,74 Mo | candidate conditionnelle |
| `Model` décimé 9 % / 4 morceaux | 144 525 | 179 999 | 4 | 158,79 ms | 145,31 ms | 5,71 Mo | rejeté pour cet usage |

Mesures runtime secondaires, médianes des trois passages :

| Variante | Mémoire utilisée | Mémoire graphique | Chargement scène | SetPass p50 | Renderers visibles |
|---|---:|---:|---:|---:|---:|
| roche A générée | 1 203,7 Mio | 973,0 Mio | 2 406 ms | 42 | 1 |
| roche A source | 1 132,2 Mio | 937,0 Mio | 2 629 ms | 42 | 1 |
| roche B générée | 1 203,6 Mio | 973,0 Mio | 2 394 ms | 42 | 1 |
| roche B source | 1 132,2 Mio | 937,0 Mio | 2 443 ms | 42 | 1 |
| `Model` généré | 1 980,7 Mio | 1 357,4 Mio | 2 732 ms | 43 | 1 |
| `Model` source | 1 171,0 Mio | 982,3 Mio | 2 238 ms | 43 | 1 |
| `Model` décimé | 1 118,4 Mio | 929,0 Mio | 2 472 ms | 43 | 1 |
| `Model` découpé | 1 117,2 Mio | 928,0 Mio | 2 426 ms | 43 | 4 |

Le compteur direct de draw calls et son fallback `Batches Count` sont tous deux
indisponibles dans ce Player ; aucun nombre inventé n'est rapporté. SetPass est
stable dans chaque groupe. Le prototype garde volontairement tous les morceaux
dans le champ : il valide le coût sans attribuer un gain hypothétique au
culling. La variante découpée n'a donc démontré aucun bénéfice de culling ; ce
point ne pourrait être reconsidéré que dans une scène spatiale représentative.

### Roche A

Le mesh source conserve les triangles, les quatre submeshes, l'ordre des
matériaux, UV0/UV1, les normales et les tangentes. Il réduit les sommets de
82,9 % et la contribution packée de 85,2 %, sans ajouter de renderer. La
comparaison visuelle est négligeable : MAE moyen 0,466/255 et 0,001 % de pixels
au-dessus du seuil.

Les médianes frame et GPU sont meilleures, mais les séries frame sont très
variables (93,31–143,93 ms pour le généré et 58,99–145,90 ms pour le source).
La recommandation est donc une candidate de production conditionnelle, pas un
remplacement automatique : validation artistique puis mesure dans `Maison`
avant modification de référence.

### Roche B

Le mesh source présente les mêmes gains déterministes de mémoire et de build,
et une différence visuelle faible (MAE 0,737/255 ; 0,023 % au-dessus du seuil).
En revanche, les trois répétitions sont stables et montrent une régression :
+6,81 % sur la frame p95 et +7,85 % sur le GPU p95. Cette variante reste en
attente. Aucune suppression historique n'est autorisée pour la roche B.

### Grand `Model`

Le mesh source simple retire la duplication barycentrique, mais conserve près
de deux millions de triangles et 1,2 million de sommets Unity. Il reste très
au-dessus de la limite de 150 000 sommets et n'est pas une solution finale.

La variante décimée monolithique respecte le budget avec 142 976 sommets,
réduit les triangles de 91 %, la contribution packée de 98,5 %, la frame p95
de 82,8 % et le GPU p95 de 86,2 % par rapport au mesh généré. Elle garde un seul
renderer. C'est la seule candidate recommandée pour une future intégration.

La décimation modifie cependant les détails fins et le givre : environ 10,158 %
des pixels dépassent le seuil diagnostique sur les quatre vues. Une validation
artistique explicite est obligatoire.

La variante divisée en quatre morceaux ne montre pas de couture évidente dans
les vues contrôlées, mais elle ajoute trois renderers. Face à la variante
décimée monolithique, sa médiane frame est 63 % plus élevée et sa médiane GPU
93 % plus élevée. Le bénéfice théorique de culling n'est pas démontré dans ce
scénario ; elle est rejetée pour l'usage actuel.

## Données barycentriques et shader

Les variantes sources et décimées n'ont pas les couleurs barycentriques du mesh
généré. Le prototype force donc `_EdgeBakedBoost = 0` et utilise le chemin de
givre restant. Aucun rebake barycentrique lourd n'a été relancé. Une tentative
de rebake récursif depuis un mesh déjà généré est correctement rejetée et ne
produit aucun asset.

Pour une intégration future du grand `Model`, il faudra choisir entre :

1. accepter visuellement ce fallback léger ;
2. ajouter une donnée de bord légère au mesh décimé ;
3. améliorer le fallback shader sans recréer une duplication barycentrique.

## Stratégie de remplacement proposée

Cette stratégie n'a pas été exécutée pendant la phase 2B.

### Jalon A — roche A

1. Dupliquer le prefab de glace de la roche A avec un nouveau GUID.
2. Remplacer uniquement son `MeshFilter` par le mesh source existant.
3. Conserver exactement les quatre matériaux, leur ordre, le transform et les
   colliders existants.
4. Mettre à jour l'entrée correspondante du catalogue Editor.
5. Remplacer l'unique référence dans une copie de `Maison` ou derrière un flag.
6. Exécuter captures, Player `Maison`, test réseau et validation colliders.
7. Promouvoir seulement si le GPU ne régresse pas et si l'art valide le rendu.

Rollback : restaurer le GUID du mesh historique dans le prefab et l'unique
référence de `Maison`, puis restaurer l'entrée du catalogue. Aucun asset source
n'est déplacé ou renommé.

### Jalon B — grand `Model`

1. Importer la variante décimée monolithique comme asset intégré définitif avec
   un nouveau GUID, hors du dossier de prototype.
2. Créer un prefab de migration séparé ; ne pas écraser `Model_Ice.prefab`.
3. Conserver transform, matériau, identité persistante et colliders existants.
4. Appliquer le fallback de givre validé artistiquement.
5. Remplacer l'unique référence de `Maison` derrière le flag de migration.
6. Refaire les mesures dans le parcours réel, les captures par profil et les
   tests réseau/sauvegarde.
7. Promouvoir uniquement après validation visuelle et respect des budgets.

Rollback : remettre le prefab historique et son GUID dans `Maison` et dans le
catalogue. Le mesh historique reste intact jusqu'à la fin d'un jalon complet.

## Liste exacte des suppressions futures possibles

Aucune suppression n'est réalisée maintenant.

Après migration approuvée de la roche A et du grand `Model`, après un jalon
complet avec rollback testé et après vérification qu'aucune référence ne reste,
les seuls assets historiques candidats à la suppression sont :

- `Assets/Environment/Prefabs_Ice/Model/Mesh_IceEdges_Model.asset`
- `Assets/Environment/Prefabs_Ice/Model/Mesh_IceEdges_Model.asset.meta`
- `Assets/Environment/Prefabs_Ice/SM_MERGED_BP_PathRocky_01_2_LOD0/Mesh_IceEdges_SM_MERGED_BP_PathRocky_01_2_LOD0.asset`
- `Assets/Environment/Prefabs_Ice/SM_MERGED_BP_PathRocky_01_2_LOD0/Mesh_IceEdges_SM_MERGED_BP_PathRocky_01_2_LOD0.asset.meta`

La roche B n'est pas dans cette liste, car son remplacement est refusé par la
mesure GPU actuelle. Son asset et son `.meta` doivent rester.

Les prototypes 10 % créés lors de l'exploration mais non retenus peuvent être
nettoyés séparément, sans impact sur la production :

- `Assets/Performance/Phase2B/Geometry/Model_Decimated_10pct.fbx`
- `Assets/Performance/Phase2B/Geometry/Model_Decimated_10pct.fbx.meta`
- `Assets/Performance/Phase2B/Geometry/Model_DecimatedSplit_10pct.fbx`
- `Assets/Performance/Phase2B/Geometry/Model_DecimatedSplit_10pct.fbx.meta`

## Validation exécutée

- compilation Unity : réussie ;
- Windows Development Player DX12 : réussi, 10 scènes de prototype ;
- matrice géométrique : 24 exécutions valides, 3 par variante ;
- validation phase 2A corrigée : 6 exécutions valides ;
- tests EditMode `Lit.Performance.Tests` : 19/19 réussis ;
- garde de rebake récursif : réussie, aucun asset produit ;
- audit bloquant : 1 092 meshes, 1 089 conformes, 3 violations historiques
  attendues ;
- références de production modifiées par le prototype : aucune.

Les avertissements de build restants concernent la résolution de types
`System.Numerics` dans le package Code Coverage et les trois gros buffers DX12.
Ils n'ont pas empêché la compilation ni les mesures. Le fichier de résultats
agrégés versionné est
`Assets/Performance/Phase2B/Reports/phase2b-results-summary.json`.

## Artefacts de vérification

- inventaire : `Assets/Performance/Phase2B/Reports/prototype-inventory.json` ;
- audit mémoire : `Assets/Performance/Phase2B/Reports/oversize-mesh-audit.json` ;
- garde récursive :
  `Assets/Performance/Phase2B/Reports/recursive-rebake-validation.json` ;
- résumé agrégé :
  `Assets/Performance/Phase2B/Reports/phase2b-results-summary.json` ;
- rapports bruts : `.codex-temp/Phase2B/FinalRuns` et
  `.codex-temp/Phase2B/Phase2AValidated` ;
- captures et différences : `.codex-temp/Phase2B/VisualReview` ;
- résultats de tests : `.codex-temp/Phase2B/phase2b-editmode-results.xml`.
