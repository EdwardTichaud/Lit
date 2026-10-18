# Performance editeur et jeu

## Priorite actuelle

La performance est prioritaire depuis le 17 octobre 2026, en edition, Play et
Player final. Le contrat historique est `../performance_contract.md` ; ses
mesures de juillet ne sont pas une baseline du projet courant.

## Premiere passe du 17 octobre

- `LitInfluenceSource` utilise des listes reutilisees pour retrouver receivers
  et renderers autour des colliders. Les recherches parents/enfants gardent
  `includeInactive=true` et la meme deduplication. Les references temporaires
  sont liberees apres decouverte, avant les callbacks receivers.
- `FlameInfluenceMaterialRuntime` lit les materiaux partages dans une liste
  reutilisee, sans instancier de materiaux ni conserver leur signature en cache.
  Les changements de materiaux restent donc visibles au scan suivant.
- Guidage et boussole copient le registre actif de `Flame` plutot que de lancer
  des recherches globales periodiques. Les regles de selection et les listes
  explicites d'`AgeManager` restent prioritaires. Une initialisation unique
  reconstruit le registre apres reset statique, y compris sans Scene Reload.
- `FlickeringLight` n'anime en edition que si `Preview In Edit Mode` est active.
  En jeu, le mouvement reste anime ; une Light desactivee n'est pas deplacee.
- Les controles de parametres Animator du pont UCC et de la facade squad
  reutilisent `AnimatorParameterLookup`. Le cache est propre au composant et
  invalide sur changement d'Animator, de controller/base override, du nombre de
  parametres, d'authoring ou de session (reload desactive compris).
  Si la base d'un override est modifiee en place, ce controller repasse en
  lecture directe pour respecter les mises a jour natives differees par Rebind.
- `LoadingScreenService.StopLoadingOrb` desactive aussi sa camera HDRP. Masquer
  uniquement la RawImage et arreter les particules laissait cette camera rendre
  sa texture pendant le jeu. Le prochain affichage la reactive normalement.
- Le fallback d'influence d'`InteractableItem`, observe dans LateUpdate, copie
  aussi le registre actif au lieu de lancer une recherche globale par objet.
- `LitEditorMemoryBudget` limite a un worker les futurs imports sur les machines
  de 16 Go ou moins. Reglage local, reversible par le menu
  `Lit/Performance/Limit Import Workers On 16 GB Machines`. La limite precedente
  est conservee ; aucun worker supplementaire n'est lance au repos et aucun
  reglage Player n'est modifie. La reserve est limitee a un worker, minimum
  impose nativement par Unity 6.6 ; le delai des workers excedentaires est
  plafonne a 30 secondes. Les valeurs precedentes sont restaurees au retrait
  de la limite, y compris le nombre configure et le nombre actif. Les changements
  de preferences utilisent RefreshSettings, comme l'inspecteur Unity.
  Le dernier worker reste reserve ; aucun gain de sa memoire au repos n'est
  revendique. La politique ne s'applique pas aux builds batch ni aux workers.

## Mesures reproductibles

`Lit/Performance/Check Influence CPU And Allocations` cree une scene de preview
jetable : 65 renderers et 65 receivers, dont parents, descendants inactifs et
receiver desactive. Les ensembles trouves sont compares au chemin Unity
original. Trois passes de 2 000 decouvertes sont chronometrees apres warmup.

Sur i7-8700 / Unity 6000.6.4f1, projet de validation isole :

| Mesure ciblee | Avant | Apres |
| --- | ---: | ---: |
| Allocations par passe de 2 000 decouvertes | 8 000 | 0 |
| Temps passe 1 | 30,998 ms | 24,567 ms |
| Temps passe 2 | 33,460 ms | 23,815 ms |
| Temps passe 3 | 42,551 ms | 25,227 ms |
| Mediane des trois passes | 33,460 ms | 24,567 ms |

Gain median cible : environ 27 %. Cela ne mesure ni le FPS du jeu complet,
ni le rendu, ni toutes les allocations du scan d'influence.

Microbenchmark Animator apres correction : 100 recherches dans un controller
de 80 parametres, reference allouant 16 100 objets contre 0 pour le cache sur
chacune des trois repetitions. Le temps varie fortement sur la machine chargee
(reference 4,039 / 15,609 / 9,796 ms ; cache 0,338 / 0,720 / 0,308 ms) : retenir
la suppression des allocations et non une garantie de gain FPS. Rapport
`Library/LitPerformance/influence-final.json`.

Le compteur est `ProfilerRecorder("GC.Alloc")` : on compte les evenements,
pas leur duree ni leurs octets. Une allocation de calibration valide le compteur.
Une saturation ou un compteur absent produit `-1`, jamais un faux zero.
`GC.GetAllocatedBytesForCurrentThread` renvoie zero sur cet editeur et n'est pas
utilise.

Rapports locaux : `Library/LitPerformance/influence-before.json` et
`influence-after.json`. Compilation runtime/editeur :
`Logs/PerformanceInfluenceAfter.log`.

## Captures de contexte

`Lit/Performance/Capture Current Context (15 Seconds)` active temporairement
le Profiler CPU, sans Deep Profile, dans le contexte courant Edition ou Play.
Le rapport est ecrit dans `Library/LitPerformance/`. Les reglages du Profiler
sont restaures a la fin ou a l'annulation. Un changement Edition/Play ou une
compilation annule la capture. Aucune scene n'est chargee par cette commande.

Le rapport exporte uniquement les frames encore presentes dans le buffer du
Profiler, leur contexte, les couts CPU inclusifs/exclusifs et les allocations
attribuees au parent direct disponible. Les couts inclusifs se recouvrent.
Une absence de GPU/Render Thread n'est jamais estimee. Les captures Play
demandent maintenant les temps GPU natifs (nombre de frames disponibles et
percentiles ; -1 si indisponibles). Le module Memory du Profiler est coupe
pendant la capture temporelle pour reduire son cout ; GPU/Memory/CPU et les
autres reglages modifies sont restaures a la fin ou a l'annulation.
Le temps de frame principal inclut les attentes, ce n'est pas du CPU occupe.
Focus applicatif, VSync, limite FPS et resolution sont exportes. Ce diagnostic dans
l'editeur ne remplace pas les trois passes Player du contrat.
En Play, le profilage de la boucle editeur est desactive pour isoler les frames
de jeu ; en Edition, il est active. Les reglages precedents sont restaures.

`Capture Bootstrap In Current Editor` exige Bootstrap deja ouvert en Edition,
capture chargement puis Play et revient en Edition sans fermer l'editeur. Il
conserve VSync/limite FPS de cette session. Un fichier ponctuel
`Library/LitPerformance/capture.request` contenant `current` ou `bootstrap` est
consomme au prochain reload seulement ; aucune boucle de polling n'est ajoutee.
`memory` produit uniquement l'inventaire explicite des textures, meshes et clips
audio deja charges. Le menu `Capture Loaded Asset Memory` ne charge ni ne
reimporte d'assets. Les tailles Unity estimees ne sont pas la RAM du processus
ni la residence GPU. Une capture Bootstrap reussie produit aussi cet inventaire
apres les mesures temporelles, avant de sortir du Play.
Dans l'editeur principal, un second inventaire ponctuel suit le retour en Edition
pour distinguer ressources de jeu et ressources conservees au repos.

`LitPerformanceCapture.RunBatchPlay` est reserve a la copie jetable
`Library/ActorAnimationSandbox`, avec rendu actif. Il emprunte le vrai flux
Bootstrap, attend un personnage et dix secondes de stabilite des scenes,
puis capture le CPU. Il echoue explicitement sans rendu, sans rapport valide
ou au bout de quatre minutes. Il ne sauvegarde pas les scenes.

## Validation et suite

`LitPerformanceTests` couvre l'aperçu explicite des lumieres, le retour a leur
position auteur et l'activation/desactivation/destruction/reconstruction du
registre des Flames pendant un vrai passage Play. Un reset de registre sans
nouvel OnEnable reproduit le cas Scene Reload desactive ; ce n'est pas une
campagne complete de toutes les combinaisons de reload.
La suite etendue ajoute les remplacements de controller/Animator et le cycle
arret/reprise de la camera d'orbe. Les quatre tests performance passent, dont
la preservation des deplacements manuels de lumiere sans preview. Le rapport
`final-regression-tests.xml` (17 octobre, 15:43 UTC) reste rouge : 4 passes / 5,
la fixture de locomotion existante n'est pas une preuve valide apres migration.
Elle retire des providers requis avant leurs dependants. Les essais de remise
en etat atteignent ensuite un moteur actif mais non inscrit a la simulation
en scene isolee (indice -1), ainsi que des erreurs de teardown. Ces essais
sont conserves dans les rapports diagnostiques ; les modifications exploratoires
de `PlayerInPlaceRuntimeTests.cs` ont ete retirees pour ne pas livrer une
reparation de fixture non validee. Le fichier preexistant est preserve.
Ne pas annoncer cinq tests reussis ni une validation marche/course automatisee.
Le vrai Bootstrap source confirme par inventaire un moteur actif, au sol,
inscrit avec l'indice 0 : ne pas transposer l'echec de fixture au jeu reel.
Execution du 17 octobre : 2 tests reussis, 0 echec, rapport
`Library/LitPerformance/regression-tests.xml`, journal
`Logs/PerformanceRegressionTests.log`.

La priorite signalee par l'auteur est une regression depuis la migration combat :
Bootstrap tres lent, Play injouable et machine lente hors Play. Premiere lecture
machine : 15,93 Go de RAM, 0,88 Go disponible, ~38 Go engages, forte pagination
(54 211 pages entrantes/s sur un echantillon). C et D gardent chacun ~319 Go libres.
La copie de validation ajoutait plusieurs processus ; apres leur fermeture,
la RAM disponible est remontee a 7,15 Go. Ne pas attribuer toute la saturation
au jeu seul. L'editeur principal avait ~8,2 Go prives, ses deux workers d'import
~2,2 Go chacun. Ces mesures de processus ne sont pas une mesure de memoire Player.
La capture Bootstrap suivante limite les workers d'import de la copie jetable
a 1 pour limiter la charge de diagnostic concurrente.

Premiers diagnostics rendus du 17 octobre : Bootstrap atteint la zone en
65,383 s, dont 34,981 s de prechargement Core et seulement 0,520 s de preparation
NavMesh (asset pre-bake, 2/2 ancres). Une repetition chauffe donne 49,344 s :
ce n'est pas un gain d'optimisation mesure.

La capture CPU suivante attribue 21,086 Mio / 440 frames (~49 Kio/frame) au
`LitOpsiveLocomotionBridge.Update` : `HasAnimatorParameter` recreait tous les
objets de `animator.parameters` a chaque ecriture. Elle montre aussi 880 passages
HDRP / 440 frames, compatibles avec la camera d'orbe jamais arretee.
Les couts de `InteractableItem.LateUpdate` justifient le retrait de son scan global.
Ces observations ciblent les corrections ; les percentiles de cette passe sont
invalides pour une baseline : l'export du diagnostic de chargement a pollue
l'echantillon suivant (301 Mio attribues au runner). L'exporteur cache maintenant
les noms de marqueurs et laisse une fenetre de stabilisation avant la capture.
Rapports diagnostiques conserves sous `Library/LitPerformance/loading-20261017-144634-230.json`
et `play-20261017-144659-796.json`. Les mesures sont dans un second editeur avec
le projet principal ouvert ; la concurrence memoire interdit une garantie Player.

Premiere capture dans l'editeur principal, sans second editeur (17 octobre) :
demarrage 97,845 s contre un ancien journal source a 126,187 s. Les etats de cache
different : aucun pourcentage de gain de chargement n'est revendique. La passe
Play `play-20261017-151746-118.json` contient seulement 15 frames en 15 secondes,
avec de fortes attentes GPU/presentation et Idle. Un seul ExecuteRenderGraph
par frame est observe, et le pont UCC alloue 6 960 octets au total (~464/frame).
Les freezes globaux ne sont donc pas resolus par ces correctifs cibles.

Inventaire au retour en Edition `memory-20261017-152140-970.json` : 4,48 Go
alloues Unity, 9,94 Go reserves, 0,12 Go Mono utilises. Les objets Texture charges
representent 3,16 Go estimes, alors que Texture.currentTextureMemory indique
1,51 Go. Ne pas additionner ces compteurs qui se recouvrent. Mip Streaming
desactive. L'editeur monte a ~15 Go prives pendant le chargement ; son nettoyage
de sortie libere 258 332 objets inutilises (6,25 -> 3,73 Go, 17,84 secondes).
La forte reserve native et la concurrence de l'import sont distinctes des
donnees effectivement utilisees. Un second passage et l'inventaire Play sont
necessaires avant de choisir une modification des ressources ou du rendu.

Repetition source a chaud, meme qualite Performant, 1920x1080 : demarrage
25,286 s, puis 416 frames / 15 secondes (`play-20261017-152443-021.json`).
Temps de frame du thread principal, attentes incluses : p50 15,481 ms,
p95 61,836 ms, p99 405,545 ms. Focus applicatif absent, VSync 1, profilage actif :
diagnostic uniquement, pas une baseline du contrat. Les a-coups restent bloquants.
Le pont UCC conserve ~464 octets/frame ; 416 passages HDRP / 416 frames.
Le meme pont allouait ~49 Kio/frame dans la premiere passe diagnostique.

L'inventaire Play `memory-20261017-152443-403.json` rapporte 7,72 Go alloues Unity,
9,97 Go reserves et 0,107 Go Mono utilises. Texture.currentTextureMemory = 3,43 Go ;
tailles estimees des objets Texture = 7,84 Go (compteurs qui se recouvrent).
Le plus gros RenderTexture est l'atlas de reflexions HDRP 8192x8192 (~358 Mo).
La texture legacy `fe_village_base` pese ~157 Mo estimes, les meshes ~1,10 Go.
Un echantillon Windows pendant la passe indique 5,43 Go graphiques dedies et
0,34 Go partages pour Unity ; il ne prouve pas une saturation de VRAM.
Ne pas attribuer les attentes de presentation au seul manque de VRAM.
L'analyse suivante doit separer chargement froid, reserve native/import,
presentation/VSync et cout GPU reel avant de modifier la qualite visuelle.

Capture source avec GPU demande, module Memory coupe et focus present :
`play-20261017-155056-274.json`, 230 frames / 15 s, VSync 1. p50/p95/p99 du
thread principal : 15,967 / 187,475 / 538,356 ms ; GPU natif disponible sur
230 frames : 16,582 / 182,151 / 567,465 ms. Ces temps incluent la cadence et
les attentes ; ils ne justifient pas a eux seuls une baisse de qualite.
Une collecte GC de 331 ms est egalement presente. Les a-coups restent reels
dans cette configuration de diagnostic ; aucun budget stable n'est valide.
Apres recompilation, le chargement prend 104,553 s, dont une frame de 49,347 s.
Le passage a chaud a 25 s ne doit donc pas masquer le probleme du premier acces.
Le personnage controle est actif, grounded et inscrit au simulateur (indice 0).
Inventaires associes : `memory-20261017-155056-458.json` en Play (8,95 Go alloues,
12,12 Go reserves) et `memory-20261017-155100-670.json` au retour en Edition
(7,35 Go alloues, 12,12 Go reserves). L'instrumentation conserve aussi ses
buffers natifs : son cout memoire doit etre mesure avant d'attribuer cette
retention au jeu. L'inventaire lit maintenant les compteurs Profiler/Gfx
disponibles sans recorder permanent ; -1 indique un compteur absent.

Le compteur natif confirme ensuite 2,753 Go utilises par le Profiler lui-meme
(`memory-20261017-160102-142.json`). Cette retention appartient au diagnostic,
pas au jeu. Historique archive sans perte avant liberation :
`history-20261017-160305-284.data` (718 298 008 octets). Apres ClearAllFrames,
Profiler Used Memory = 162 Mo et Unity alloue 3,56 Go, contre 6,10 Go avant
(`memory-20261017-160432-961.json`). La reserve native reste a 12,09 Go : ne pas
la confondre avec la memoire utilisee. Le menu explicite
`Archive And Clear Profiler History` refuse un Play/enregistrement actif,
sauvegarde d'abord un fichier non vide, puis libere les frames et refait
l'inventaire. Requete ponctuelle equivalente : `archive-profiler`.
Les captures ulterieures doivent soustraire ou archiver cet historique avant
de comparer la memoire au repos. Aucune baseline existante n'a ete ecrasee.

Piste suivante pour l'Edition : mesurer les dependances chargees par le prefab
GameplaySessionRoot simplement reference dans Bootstrap. La reference Unity
LazyLoadReference peut differer leur chargement dans l'editeur sans deplacer
l'asset ; elle ne differe pas les dependances dans un Player. Cette piste reste
non implementee et doit d'abord prouver compatibilite de serialisation et gain
sur une copie isolee. Documentation Unity 6000.6 :
https://docs.unity3d.com/6000.6/Documentation/ScriptReference/LazyLoadReference_1.html

## Preparation du Player autonome

Prototype LazyLoadReference teste uniquement dans la copie jetable : ancienne
reference Bootstrap et roundtrip de scene valides (1 test passe,
`lazy-reference-tests.xml`). Delta observe sans rendu : 456 -> 460 textures,
1 078 797 900 -> 1 089 466 846 octets Unity lors de l'acces. Ce faible delta ne
prouve pas un gain significatif en edition avec rendu ; changement non livre.
Graphe textuel indicatif `bootstrap-reference-graph.json`, packs locaux inclus :
27 grosses textures deja chargees sont accessibles depuis GameplaySessionRoot
(~572 Mo estimes, 691 dependances). L'orbe de chargement reference aussi cinq
textures connues (~101 Mo).
Les branches peuvent partager leurs dependances ; ce graphe n'est pas une
preuve du chargement effectif ni une attribution exhaustive.

Comparaison hors Unity avec Git HEAD (`bootstrap-reference-comparison.json`) :
GameplaySessionRoot passe de 600 a 691 dependances accessibles ; poids des seules
textures connues dans l'inventaire : 526,7 -> 571,6 Mo. Bootstrap complet :
922 -> 1 013 dependances, 734,2 -> 779,1 Mo connus. Ce n'est ni le delta memoire
total ni une mesure de chargement : les packs ignores n'ont pas d'historique
Git et leurs donnees actuelles sont reutilisees dans les deux graphes.

Les vrais jalons SceneTransition du source ciblent surtout les salles : sur
104,553 s, Prechargement termine (District_1_Rooms_Environment) prend 49,347 s,
exactement la frame maximale, contre 7,295 s lors du passage a chaud a 25,286 s.
Le Core prend respectivement 5,867 s et moins d'une seconde a chaud. Les jalons
"Initialisation Netcode debut" incluent le temps d'activation qui les precede ;
ils ne mesurent pas a eux seuls le cout de l'initialisation Netcode.

Le projet jetable a ete resynchronise depuis les fichiers source modifies ou
nouveaux (958 fichiers), les ProjectSettings et le Bootstrap/source de session.
Un harness de mesure Development Player est en preparation dans cette copie,
pas encore livre : vrai flux Bootstrap -> District 1, spawn auteur, 10 secondes
de warmup focalise, 60 secondes de frames valides, 1080p Balanced/VSync 0, sortie
JSON CPU/GPU/GC/memoire et refus des changements de contexte ou mouvements.
Company/Product et racine de sauvegardes sont isoles. Aucun Player n'est encore
valide ; ne pas annoncer de baseline autonome.

Le harness de diagnostic verifie Debug.isDebugBuild a l'execution. Son ancien
garde UNITY_EDITOR/DEBUG excluait le code des vrais defines Player (DEBUG et
UNITY_INCLUDE_INSTRUMENTATION absents, DEVELOPMENT_BUILD present mais deprecie
dans cette version). Garde retire dans la copie ; precompilation sans symbole
UNITY_EDITOR reussie. Le build courant est garde actif pendant les imports.

Version suivante preparee hors Assets dans
`Library/LitPerformance/Staged/LitStandaloneBenchmark.cs`, precompilee mais non
executee : seed fixe, buildGUID/API/clock/camera, refus de toute perte de focus
ou erreur de demarrage, scenario de trajet plat NavMesh proche du spawn par
gamepad virtuel. Le moteur UCC reste seul executant. Le pilotage lit l'intention
reelle du bridge ; il ne sonde pas GetWorldSpaceInput avec des axes fictifs,
car cette methode modifie la reference de mouvement conservee. Deux longueurs
de trajet et au moins 10 m/95 % de frames au sol sont demandes. Combat, blocage
ou sortie du couloir refusent la mesure. Ce scenario court ne couvre pas les
portes, changements de salle, portails, VFX de combat ni multijoueur.
La premiere preparation a atteint 13,5 Go de memoire privee et une forte
pagination. Son editeur de diagnostic (42320) et son worker ont ete arretes
apres verification d'identite pour liberer cette memoire ; SourceAssetDB et
ArtifactDB avaient enregistre les imports acheves. Journal conserve dans
PerformancePlayerBuildAttempt14-MemoryCheckpoint.log. Apres arret, 9,48 Go de
RAM disponibles. Le build reprend dans la meme copie ; le cache est a verifier.
Reprise confirmee : imports acheves reutilises, ouverture de Bootstrap en
3,134 s dans l'editeur de diagnostic, puis reimport des shaders pour la nouvelle
liste DX12 seule. Les gates techniques passent. Code Player compile ; les types
LitStandaloneBenchmark et LitStandaloneTraversalDriver sont presents dans
Library/Bee/PlayerScriptAssemblies/Assembly-CSharp.dll (20:59:44 local).
La compilation des variantes HDRP DX12 continue ; aucun Player lance.
Avant cette reprise, le harness renforce a ete copie dans Assets de la copie,
precompile avec succes et le builder de diagnostic libere les objets importes
inutilises avant d'ouvrir Bootstrap. Aucune variante de gameplay source changee.
Les premiers outils non executes restent des diagnostics historiques ; aucune
baseline validee n'en resulte. Repartir de sauvegardes temporaires vierges entre
passages pour eviter une variation de progression du monde.

L'audit Time prenait une comparaison `==` pour une ecriture ; la regex
distingue maintenant lectures et affectations. Le teardown des tests temps
laisse TimeManager restaurer son propre etat. 14 tests passes, 0 echec :
`time-authority-tests.xml` ; aucun controle runtime de temps affaibli.

Les autres blocages concernent la qualification preexistante : empreintes
historiques InPlace, dialogue Belmont absent de la definition, callbacks de
prefabs importes/hors build, acceptation visuelle et ancien EnemyController
remplace par Brains AI. Les adaptations exploratoires de ces controles et du
dialogue ont ete retirees du projet principal. Elles restent uniquement dans
la copie jetable pour produire un executable de diagnostic. Aucun clip,
manifeste, profil accepte ou contenu narratif source n'est modifie. Le Player
de diagnostic ne constitue donc pas une preuve de livraison conforme.

La copie contenait aussi des metadonnees divergentes : les 11 264 fichiers meta
source ont ete compares et 258 resynchronises (essentiellement dossiers,
plus brasero, profils globaux et police). Le GUID du nouveau test Time avait
33 caracteres ; il est corrige a 32 dans les deux projets. Les erreurs de
prefab manquant dans les tentatives precedentes appartenaient a cette copie,
pas au Bootstrap source. Ne pas les attribuer a la regression du jeu.

Les tentatives invalides sont conservees dans `Logs/PerformancePlayerBuildAttempt*.log`.
La compilation du Player reste en cours de validation, notamment ses autres
gates, sa compilation runtime et ses shaders.

Le code Player compile dans la copie ; la premiere compilation des shaders
ne fait pas partie d'un temps de chargement du jeu. Cette premiere compilation
a ete interrompue pour verifier la fidelite de la copie. Attention : le premier
inventaire (20 321 fichiers, 40 assets differents) utilisait rg sans --no-ignore
et omettait donc les packs locaux ignores par Git. Le deplacement de 8 003
fichiers vers `Library/LitPerformance/CloneResync-20261017-192241` incluait
des fichiers encore utilises dans le source ; leur absence dans l'inventaire
ne prouvait pas leur suppression. Ces fichiers sont retablis depuis l'archive.
Le build suivant a signale UnityGLTF manquant dans l'outil de conversion Editor,
consequence de cette omission de Sketchfab dans la copie, pas erreur source.
La synchronisation complete doit inclure --no-ignore sur les deux arborescences.
7 963 fichiers de packs locaux ont ete retablis depuis l'archive. L'inventaire
reel compte 46 746 fichiers source Assets. Synchronisation terminee : 18 709
fichiers supplementaires/differents copies, aucun autre fichier ancien archive.
Manifeste `CloneResync-20261017-193144/manifest.json`, avec les exceptions de
diagnostic explicites. La copie contient les 46 746 fichiers source plus les
outils de mesure ; nouveau build de diagnostic lance sur cet etat.
Le manifeste `CloneResync-20261017-192624/manifest.json` ne certifie que le sous-
ensemble inventorie a cette etape et n'est pas une preuve exhaustive.
Le nouveau build utilise DX12 uniquement, selon le contexte de mesure cible.
Aucun resultat de la premiere copie n'est une baseline Player admissible.
Audit Packages : 9 118 fichiers source controles, seule absence = 6 340 fichiers
du dossier orphelin com.unity.ai.assistant (sans package.json, absent du manifeste,
du lock et des assemblies actives du source). Aucune divergence dans les
packages actifs UCC, Netcode, config HDRP, manifeste ou lock.
Le worker source AssetImportWorkerHW0 (PID 23204, enfant de l'editeur 21700)
restait bloque depuis plusieurs heures dans Prepare/ReloadAssembly, avec
`ADTM: Control thread waiting for realtime thread timed out` dans son journal.
Unity le considerait Preparing, donc le delai de worker inactif ne suffisait
pas. Apres verification de son identite et archivage de son journal, ce seul
worker a ete arrete ; l'editeur principal reste vivant et repondant. Unity
signale l'interruption du transport attendue. Cela libere environ 2,3 Gio
de memoire privee ; la recuperation au prochain vrai import reste a verifier.
Unity a depuis cree AssetImportWorkerHW3 (PID 8036), annonce pret : reprise du
pool observee. Il conserve aussi environ 2,3 Gio ; l'arret automatique au repos
n'est pas encore observe. Ne pas compter la liberation ponctuelle de l'ancien
worker comme un gain de memoire permanent.
L'essai standby 0 n'a pas libere ce dernier worker : le code officiel Unity
impose un minimum natif de 1. Le helper est corrige a 1 et applique les preferences
par RefreshSettings, anciennes valeurs 2 / 60 s conservees. L'inventaire inclut maintenant les AnimationClip,
sans charger de nouvelles ressources ; pas encore de capture avec ce champ.

Restent a mesurer sur le contexte courant : fluidite de la Scene View, entree
et sortie Play, exploration representative, combat/VFX et Player autonome.
Le journal precedent indiquait 37,09 s de retour en edition puis 11,17 s de
nettoyage d'assets ; ces durees sont des indices a attribuer, pas des gains
revendiques. Aucune garantie globale de 60 FPS n'est encore etablie.

Le 18/10, la reprise du build DX12 est constatee terminee sans executable :
le journal se termine par perte de connexion FXC sur HDRP/Lit Forward.
Le code Player compile ne constitue donc pas une validation du Player.
`LitLightweightPlayCapture` propose une observation Bootstrap de 15 secondes,
avec ProfilerRecorder de capacite 1 et sans historique du Profiler natif.
Elle conserve les reglages du jeu, restaure l'activation du Profiler apres Play,
et exporte focus par echantillon, frames sautees, compteurs indisponibles et
memoire allouee. Le GPU n'est pas mesure. Les echantillons Editor.update
incomplets ne sont pas une baseline Player. Requete unique `bootstrap-light`
via le dispatcher existant ; aucun polling de fichiers au repos.

Premier flux Unity valide : `light-20261018-143907-096.json`, 897 echantillons
en 15,007 s, 0 frame sautee, focus present sur tous les echantillons.
1080p, VSync 1, timeScale 1 : frame p50 16,711 / p95 16,867 / p99 19,427 ms,
maximum 54,535 ms. Repos uniquement, pas de comparaison avant/apres controlee.
Entree Play 28,17 s ; SceneTransition 24,174 s (max 4,037 s), total demande
jusqu'au jeu pret environ 51,58 s. Retour Edit 1,13 s. Unity alloue 5,60 Gio
en Play, puis annonce 2,98 Go apres son nettoyage d'assets. La memoire privee
du processus atteint 13,37 Gio, ce qui ne mesure pas la RAM physique residente.
Le faible espace RAM libre et les avertissements natifs d'eviction persistent.
Le journal DX12 demande aussi un upload de 64 Mio pour un buffer de 16 Mio ;
aucun reglage global modifie sur cette seule observation.
Repetition `light-20261018-144157-676.json` : 897 frames, 0 sautee, focus 100 %,
p95 19,124 / p99 25,070 ms, demande->pret 40,107 s ; entree Play 27,58 s,
retour 0,65 s. Inventaire apres tous les echantillons
`memory-20261018-144157-776.json` : allocations 5 988 737 509 octets,
reserve 8 899 371 008, Profiler 7 272 055. 298 clips estimes a 115 067 103
octets, meshes 1 103 867 192, textures 7 712 111 904 (estimation d'objets,
non additionnable aux autres compteurs et pas une mesure de residence GPU).
Ces essais de repos ne qualifient toujours pas exploration/combat/Player.
Leur profil qualite n'etait pas exporte : ne pas les comparer directement au
Player Balanced. Le collecteur leger exporte maintenant qualite/API/GPU et
interrompt l'observation si la qualite change. Compilation externe reussie ;
nouveau flux Unity avec ces champs encore a executer.

Le registre Netcode conserve autrement ses dictionnaires de sources jusqu'au
SubsystemRegistration suivant. Un hook reserve a l'editeur les vide desormais
sur EnteredEditMode, apres l'arret reseau et la destruction des scenes. Il ne
vide rien sur ExitingPlayMode. Le delta de memoire physique reste a mesurer ;
la suppression de ces references ne garantit pas a elle seule le dechargement
des prefabs encore references par les assets de Bootstrap.

Reprise du build le 18/10 a 16:45, PID 26968, editeur batch sans graphics,
Player toujours DX12, cache de shaders reutilise. Manifeste de resynchronisation
`CloneResync-20261018-164433/manifest.json` : 46 748 fichiers source, 11 copies,
4 fichiers de resultats de tests propres au clone archives. Le hook de cleanup
Editor ajoute ensuite au source est exclu du code Player par UNITY_EDITOR.
Journal `Logs/PerformancePlayerBuildAttempt15-NoGraphics.log` ; executable et
resultat de build doivent toujours etre verifies avant toute mesure Player.

La reprise 15 est terminee en echec apres 1386,5 s, cette fois sans crash FXC :
4 erreurs de shaders Vefects dans resources.assets, `Undefined punctual shadow
filter algorithm`. Les quatre shaders declarent encore SHADOW_LOW/MEDIUM/HIGH.
Leur unique pragma de qualite est remplace par les deux groupes HDRP actuels,
PUNCTUAL_SHADOW et DIRECTIONAL_SHADOW, avec les trois niveaux conserves. Les
calculs de surface et les proprietes de materiaux ne changent pas. La compilation
Player de ces variantes reste a valider. Ces fichiers de packs sont ignores par
Git : correctif reproductible `../patches/vefects-hdrp-shadow-keywords.patch`
(appliquer avec `git apply --unidiff-zero`), originaux et hashes dans
`Library/LitPerformance/VefectsShadowFix-20261018/audit.json`.

Suppression de la dependance directe com.unity.ai.inference 2.6.1 : recherche
sans exclusions Git dans Assets/Packages, aucune utilisation dans le code actif,
aucun modele ONNX/Sentis, aucun package dependant et aucune reference aux 1358
GUIDs du package dans les assets serialises controles. Les seuls usages de
types sont dans l'ancien assistant sans package.json, non charge. Preuve locale
`inference-dependency-audit.json`. Le build 15 compilait bien les kernels et
shaders Hidden/Sentis inutilises. App UI 2.1.13 est conserve explicitement
(configuration projet existante), toutes les autres versions du lock restent
identiques. Aucun gain d'entree Play/FPS n'est encore attribue a ce retrait.
Compilation Unity source reussie apres resolution. Test cible
`network-cache-tests.xml` du 18/10 a 17:18 : 1 passe / 0 echec dans la copie,
sans enregistrer la scene source. Il verifie la retention pendant ExitingPlayMode,
la liberation sur EnteredEditMode et l'invalidation d'initialisation pour la
session suivante ; pas un benchmark RAM. Build 16 lance a 17:19, PID 11968,
journal distinct, sur les quatre shaders corriges et sans inference.
Manifestes/lock, registre Netcode et HDRP Balanced verifies identiques entre
source et copie avant compilation.

Build 16 reussi : 0 erreur, 350,319 s, 4 582 886 955 octets ; quatre shaders
Vefects corriges valides par ce build DX12 strict. Le premier lancement
`Player/Runs/20261018-172821-idle/result.json` est invalide : seul District_1_Core
etait charge, navigation en echec et acteur non au sol. Aucun chiffre FPS tire
de cet essai. Bootstrap a un registre runtime de zones vide ; son manifeste de
test est un champ reserve a l'editeur. Le builder de diagnostic enregistre
maintenant ce meme manifeste District_1 dans la copie, afin que l'API par nom
de scene charge aussi les 21 scenes associees. Bootstrap source est conserve.
Nouveau build encore a lancer. Le rapport de diagnostic contient desormais les
etats du pont UCC, du simulateur et de la navigation meme en cas d'echec.

`Pulse` ne porte plus ExecuteAlways : son animation d'echelle est reservee au
Play, mathematiques runtime inchangees. L'ancienne execution en edition pouvait
modifier l'echelle serialisee de l'interface. Aucun gain CPU revendique : deux
releves Source seul donnent 10,109 puis 10,563 secondes CPU pour ~10 secondes
reelles. Le test local Scene View sans Always Refresh donne 12,813 s CPU / 10 s,
pas de gain demontre ; reglage precedent restaure a true et outil temporaire
retire. Preuves `editor-pulse-before.json`, `editor-pulse-after.json`,
`editor-on-demand-after.json`. Ne pas attribuer ce cout a Pulse seul.

Deux essais d'attribution CPU Edit ont retourne 0 frame disponible, cible Editor.
L'export conserve maintenant le contexte/les modules jusqu'a sa fin, puis les
restaure, et initialise la fenetre Profiler pour la commande current. Ce dernier
flux Edit reste a valider. Aucun rapport de cout invente pour les essais vides.
La cible etait bien Editor, plage disponible 0..0. Les changements de
rafraichissement de Scene View et leur menu temporaire sont retires.

Capture Source suivante `light-20261018-160129-511.json` : Performant, 1080p,
VSync 1, 893 frames / 15 s, 0 sautee, toutes avec focus. p95 16,877 / p99
22,525 ms ; demande->pret 47,769 s, entree Play 27,61 s, SceneTransition
21,153 s, retour Edit 0,69 s. Aucun gain de demarrage attribue au retrait IA.
Inventaires `memory-20261018-160129-620.json` en Play : 5,755 Gio alloues ;
`memory-20261018-160130-693.json` en Edit : 5,246 Gio, reserve 8,288 Gio,
dont Profiler ~156,8 Mio issu des diagnostics. Pas de liberation memoire
globale demontree par le seul cleanup du registre. L'inventaire post-Edit est
desormais demande une seule fois par capture observee via SessionState.
Builds 17 et 18 reussis, respectivement 229,614 et 238,801 s, 0 erreur : registre
clone complete avec le manifeste de test existant et spawn explicite District_1,
aucun changement Bootstrap source. Les 22 scenes attendues sont chargees.

Player 18, `Player/Runs/20261018-183303-idle/result.json` : mesure complete et
valide, 60,006 s / 8200 frames, DX12 Balanced 1920x1080, VSync 0, focus continu,
acteur conduit et au sol, navigation Ready, aucune erreur. Frame p50 7,137,
p95 9,083, p99 9,855, maximum 15,864 ms. Compteur natif
`Render/CPU Render Thread Frame Time`, unite TimeNanoseconds : p95 3,393 ms.
GPU 5947 echantillons : p95 8,422 ms. Main Thread p95 9,058 ms inclut attentes.
Chargement 11,152 s apres Awake, hors demarrage natif du processus ; allocations
GC totales 80 144 312 octets (~9,8 Ko/frame), memoire allouee Unity 710 Mo,
reserve 1,393 Go (pas une mesure de RAM physique/VRAM). Build GUID
`b8ab42616944460596ecd2c53912a0dd`. Ces reglages different de l'Editor Performant
VSync 1 : aucune comparaison avant/apres de FPS revendiquee.

Build 17 `20261018-181543-traversal` : 9417 frames / 60 s, 257,726 m, 44 trajets,
100 % au sol, navigation Ready, focus continu, aucune erreur. Frame p95 7,757 ms,
GPU p95 6,389 ms ; rapport invalide uniquement parce que l'ancien compteur
Render Thread etait indisponible. Ce compteur est corrige dans le build 18.
Le premier repos build 18 `20261018-182902-idle` est interrompu par perte de
focus, donc exclu des baselines. Aucun de ces parcours ne qualifie combat,
portes, portails ou multijoueur ; adaptations de build toujours diagnostiques.

Le code officiel UnityCsReference `Modules/ProfilerEditor/ProfilerWindow/
ProfilerWindow.cs` confirme que OnFocus reapplique m_Recording au driver.
L'outil de capture synchronise desormais SetRecordingEnabled pour les fenetres
Profiler existantes et restaure leurs etats apres export/annulation. Reflection
Editor uniquement, refus explicite si cette API interne est absente. Compilation
Unity reussie, mais nouvelle capture Edit toujours vide : fin avec recording,
editor et CPU a true, range 0..3, aucune nouvelle frame. La synchronisation de
fenetre n'est donc pas une resolution demontree. Essai distinct `entry` limite
a l'entree Play, sans changer les options de reload existantes.

Player 18 `20261018-184104-traversal/result.json` : complete et valide,
60 s / 8731 frames, 258,027 m, 44 trajets, 100 % au sol, focus continu, erreurs
vides. Memes reglages Balanced 1080p DX12 sans VSync. Frame p50 6,678,
p95 8,717, p99 11,279, maximum 34,974 ms ; Render p95 3,050 ms ; GPU 8646
echantillons, p95 6,234 ms. Chargement apres Awake 13,431 s. Validation limitee
au trajet local de diagnostic, sans combat, portes/portails ou multijoueur.

Essai entree source `entry-20261018-164605-516.json` : entree Play 27,57 s,
retour Edit normal 1,04 s. Seulement 3 frames natives, EditorLoop inclusif
~910 ms : la longue attente synchrone manque a la capture, ne pas attribuer
27 s a ses marqueurs ni lire ses percentiles comme baseline. Recuperation du
buffer `retained-20261018-164733-424.json` : 10 frames de contextes melanges,
EditorLoop ~7,27 s et Loading.LockPersistentManager ~5,11 s inclusifs ; cette
recuperation ne comble pas la couverture. Outils opt-in, etats Profiler restaures,
aucune scene source enregistree. Diagnostic entree/RAM encore ouvert.

## Edition : attribution du 18/10 au soir

Le pre-roll de la capture `entry-20261018-165302-990.json` dure 25,341 s
malgre une consigne de 2 s ; ensuite demande->Play actif 2,710 s (logger
EnteredPlayMode 2,63 s), setter Play 0,025 s, setup Profiler 0,054 s. L'attente
de ~25 s peut donc se produire avant la demande de Play ; aucune amelioration
de demarrage n'est demontree par ce deplacement. Les commandes de mesure
attendent desormais trois updates idle pour Bootstrap leger / attribution Edit,
au lieu de demarrer dans le callback de fin de refresh.

`edit-20261018-170254-119.json` : 283 frames obtenues en 5 s avec
QueuePlayerLoopUpdate explicite. Ce mode force updates et rendu : il sert a
attribuer du travail, pas a mesurer le repos. Attente GPU D3D12 2651 ms self,
OnGUI 451 ms self, CamerasOverlay.Update 89 ms self. Les 23 callbacks update
retenus parmi les 100 couts les plus importants totalisent 430 ms self ; tous
les outils de migration ne sont donc pas gratuits, mais l'affichage domine.

Registre local confirme avant modification : InteractionMode=1 (No Throttling),
ApplicationIdleTime=0. Reglage ponctuel via API Preferences native : mode 2
(Monitor Refresh Rate), 16 ms. Original sauvegarde dans
`Library/LitPerformance/interaction-mode-original.json`, menu de restauration
`Lit/Performance/Restore Interaction Mode From Performance Snapshot`. Preference
globale pour cet utilisateur Unity, pas un changement de qualite du projet ni
du Player ; aucune reapplication automatique au reload. Reference du mecanisme :
https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/PreferencesWindow/PreferencesSettingsProviders.cs

CPU reel source seul, Bootstrap Edit, Profiler arrete/historique archive dans
les deux cas : `editor-after-diagnostic-cleanup.json`, 8,328 s CPU / 10,018 s ;
`editor-monitor-refresh-after.json`, 6,078 s CPU / 10,016 s, environ -27 % sur
ces premiers releves. Le cout residuel reste significatif ; pas de comparaison
avec la capture forcee. Le flux Play complet doit encore etre controle.

Le menu manuel `Release Unused Editor Assets` utilise
EditorUtility.UnloadUnusedAssetsImmediate(includeMonoReferencesAsRoots:true)
pour conserver les references des scripts et scenes. Mesures
`memory-20261018-165909-872.json` -> `...985.json` : allocation ~2,819 -> 2,752 Go,
66 textures et 59 meshes retires, gain ~64 Mo, aucun gain global majeur.
Cette mesure suit un Play abrege : ne pas comparer ses ~2,75 Go aux ~5,25 Gio
apres le monde complet. Reserve toujours ~8,90 Go, private process ~7,95 Go.
Aucune liberation automatique n'est ajoutee.

L'ouverture d'une fenetre Profiler peut activer son enregistrement avant Start.
L'outil conserve maintenant l'etat natif AVANT cette ouverture et synchronise
la restauration des fenetres ; les diagnostics precedents pouvaient laisser
l'enregistrement actif. Arret explicite et archives de 174 793 924 octets puis
historique de la capture forcee, avant mesures CPU. Ne pas attribuer leur cout
au runtime du jeu. Compilation Unity des outils reussie.

Controle source complet apres reglage, `light-20261018-171000-426.json` :
899 frames / 15 s, Performant 1080p VSync 1, focus continu, 0 sautee, p50
16,696 / p95 17,226 / p99 18,022 ms. Aucun message d'erreur dans le segment
de journal correspondant. Inventaire Play `memory-20261018-171000-509.json` :
acteur conduit et au sol, simulation index 0. Demande->pret 19,409 s, entree
Play 2,55 s, transition District complet 17,232 s (frame max 3201,6 ms), retour
Edit 0,80 s. Le protocole differe des premieres mesures : lancement apres
trois updates idle plutot que directement apres reload. La baisse de ~47,8 a
19,4 s n'est donc pas un gain runtime controle attribuable au mode d'interaction.
Le reglage ne modifie pas le Player et les performances steady-state en Play
restent satisfaisantes sur ce point de depart. Combat toujours non qualifie.
Apres retour Edit, allocation 3,458 Go, reserve 8,904 Go ; la reserve native et
la pression memoire globale restent a surveiller, sans promesse de liberation.
