# Navigation NavMesh

## Autorite runtime

`NavMeshWorldService`, installe sur `GameplaySessionRoot`, possede le cycle de vie du NavMesh de zone. Il invalide les donnees precedentes lors d'un changement de zone, charge le `NavMeshData` du `ZoneManifest` lorsqu'il est compatible, puis utilise le bake runtime comme secours.

Les etats sont `Unloaded`, `Loading`, `Building`, `Validating`, `Ready`, `Failed` et `Invalidating`. Aucun agent ne doit etre active avant `Ready`. Une projection n'est acceptee que si ses ecarts horizontal et vertical restent dans la tolerance locale; aucun `Warp` de rattrapage n'est effectue.

## Integration

`SquadAIManager` conserve la formation et le suivi de squad. Ses anciennes API NavMesh deleguent au service central lorsqu'il existe. `SceneMarker` et `EnemyNavigationController` attendent `WorldReady` et refusent les donnees residuelles d'une ancienne zone.

Le menu `Lit > Navigation > Bake Zone NavMesh` produit un asset optionnel a partir des scenes actuellement chargees et l'inscrit dans le `ZoneManifest`. En l'absence d'asset, le service reconstruit le monde apres la synchronisation physique des scenes obligatoires.

Le bake editeur charge maintenant la scene principale et toutes les scenes du
manifest avant de construire l'asset. Il refuse le fichier si un marker gameplay
ne possede pas de polygone a moins de 0,15 m. En runtime, `GameFlowService` logue
le couple `scenes attendues/chargees` avant d'autoriser le build et bloque la zone
si une scene obligatoire manque.

La surface runtime est placee a l'origine monde, meme si `GameplaySessionRoot`
est auteurise avec un offset de district. Le `NavMeshData` pre-bake est ainsi
charge avec exactement le meme repere que celui utilise lors de sa generation;
il ne peut plus reapparaitre sous le sol par translation du root persistant.

## Regles d'action

Pendant une attaque, une cinematique, un QTE ou une action physique, l'agent est suspendu mais le NavMesh n'est pas reconstruit. Il ne reprend qu'apres restitution complete et validation locale du sol.

## Validation et sonde de sol

SceneMarker reprend son attente lorsque le service de monde apparait apres lui,
et annule cette attente a la desactivation. EnemyNavigationController suspend
son agent des que le monde quitte Ready. Le prefab scientifique demarre avec
l'agent desactive. Son marker auteur a -98,16 m correspond au NavMesh pre-bake
a environ 8 mm pres ; le configurateur ne reecrit plus sa position en dur.
La capsule scientifique a ses pieds sur ActorRoot. La sphere de recherche du
sol demarre avec son bord inferieur au-dessus des pieds ; les contacts initiaux
sans surface valide et les colliders de personnages sont exclus.
