# Capture Deep Profile courte — diagnostic, sans correction

## Portee et validite

Deux Players Development ont ete captures sur la machine de reference, avec
VSync desactive : une premiere passe de 12 s et une seconde de 4 s avec
`Profiler.enableAllocationCallstacks` active. La seconde est la source de ce
rapport. Elle contient 11 frames de fenetre de mesure et 25 frames dans le
flux Profiler, dont du chargement et du prechauffage.

Le Deep Profile a fortement perturbe le jeu : 130 a 1 179 ms/frame dans cette
seconde passe. Ces valeurs ne sont **ni** une baseline, **ni** les hitches
normaux du jeu. Elles servent seulement a identifier les chemins executes.

## Allocations de la capture avec call stacks

Les chiffres suivants sont normalises sur les 25 frames presentes dans le
flux Profiler. Ils ne sont pas comparables directement aux ~266 Ko/frame de
la passe Player non profilee : l'activation de call stacks ajoute une tres
grande surcharge.

| Source | Ko/frame | Echantillons/frame | Interpretation |
| --- | ---: | ---: | --- |
| `GC.Alloc` adresses non resolues | 3 235,3 | 48 655,1 | Profiler/Mono non symbolise ; non attribuable a une methode avec cette capture. |
| `Main Thread > GC.Alloc`, pile indisponible | 2 045,9 | 11 936,8 | Allocation Main Thread non symbolisee ; non exploitable seule. |
| `PreloadManager > GC.Alloc`, pile indisponible | 119,6 | 1 561,1 | Chargement Unity, hors regime stable. |
| `Object.Instantiate...` | 29,5 | 35,3 | Chargement/instanciation, hors regime stable. |
| `UltimateCharacterLocomotion.AwakeInternal` | 5,2 | 122,0 | Initialisation Opsive, hors regime stable. |
| `TextMeshProUGUI.Rebuild` via `TMP_Text.ParseInputText` | 4,6 | 1,3 | Reconstruction UI, echantillon de chargement. |

Les derniers chemins disposent de piles symbolisees dans
`DeepProfile/Analysis/gc-callstacks-report.json`. Les entrees de pipeline HDRP
sont volontairement absentes du tableau : plusieurs piles se recouvrent et ne
doivent pas etre additionnees.

## Marqueurs CPU Main Thread

Les temps sont inclusifs et se recouvrent entre parent et enfant ; ils ne
doivent donc pas etre additionnes. Les marqueurs de chargement et le cout du
Deep Profile sont exclus de toute conclusion sur le framerate final.

| Marqueur ou chaine | Observation | Confiance |
| --- | --- | --- |
| `LitInfluenceParticleSystemController.Update` | 24 appels, 6 350 ms inclusifs dans la capture perturbee. | Moyenne pour le cout absolu, elevee pour l'existence du chemin chaud. |
| `RefreshCommonLights` | 15 appels ; parcours global des `ParticleSystem` et `Light`. | Elevee. |
| `TryResolveCommonLightRoot` / `IsCommonLightTagged` | 44 776 / 370 816 appels ; remonte les parents et lit `GameObject.tag`. | Elevee. |
| Chaines `System.String` et `OutStringMarshaller` | ~918 000 appels chacun, sous le chemin precedent. | Elevee comme symptome de l'acces tag ; cout absolu perturbe. |
| `CombatHudController.ResolveScenePanelsIfNeeded` / `FindSceneGameObjectByName` | 5 / 21 appels, observes pendant la phase chargee. | Faible pour le regime stable ; a recontroler hors chargement. |

Les gros marqueurs Unity (`Application.WaitForAsyncOperationToComplete`,
`Loading.AwakeFromLoad`, `Shader.CreateGPUProgram`, HDRP `Submit`) sont des
couts de chargement ou d'instrumentation. Ils n'expliquent pas les 15 hitches
>50 ms de la passe Player non profilee.

## Attribution des hitches

- **Hitches de la capture Deep Profile : confiance elevee.** Ils sont domines
  par chargement de scene, `Shader.CreateGPUProgram`, initialisation graphique
  et l'enregistrement de call stacks (`Profiler.Callstack`).
- **Hitches >50 ms du Player non profile : non attribues.** La courte capture
  Deep Profile ne contient aucune frame representative : toutes ses frames
  depassent 100 ms a cause de l'outil. Il serait incorrect de les attribuer
  au rendu, a HDRP, aux portails, a la visibilite ou a la glace.
- **Cause candidate du cout CPU stable : confiance moyenne.** La frequence des
  parcours globaux de `LitInfluenceParticleSystemController` et le volume de
  resolutions de tags sont directement observes ; leur cout exact hors Deep
  Profile doit etre confirme par un marqueur leger avant toute modification.

## Une seule correction recommandee, apres validation

Instrumenter legerement puis remplacer **uniquement si confirme** le scan
periodique global de `LitInfluenceParticleSystemController.RefreshCommonLights`
par un registre d'evenements/cache de racines deja connues, sans changer la
presentation ni les regles de lumiere. Impact attendu : baisse du CPU et des
allocations de chaines dues aux recherches et tags repetes. Risque : moyen,
car ce controleur pilote des particules et lumieres ; aucune implementation
n'est autorisee avant une mesure non intrusive qui isole ce cout.

## Fichiers et artefacts

Code de diagnostic modifie :

- `Assets/Performance/Runtime/MaisonPerformanceBaselineRunner.cs`
- `Assets/Performance/Editor/PerformanceGcAllocationReport.cs`

Livrable ajoute :

- `AIAgent/Codex/performance_deep_profile_diagnosis.md`

Artefacts non suivis :

- `.codex-temp/PerformanceStabilization/DeepProfile/Runs/deep-stable-01.*`
- `.codex-temp/PerformanceStabilization/DeepProfile/Runs/deep-callstacks-01.*`
- `.codex-temp/PerformanceStabilization/DeepProfile/Analysis/gc-callstacks-report.json`

Aucune scene, prefab, asset de production, logique de jeu, reseau, sauvegarde,
visibilite, portail, streaming ou qualite graphique n'a ete modifie.
