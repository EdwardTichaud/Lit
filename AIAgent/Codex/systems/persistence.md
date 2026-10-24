# Persistance

## Rôle

Sauvegarder les personnages et le monde, reconstruire les objets runtime et
transférer l’état complet aux clients qui rejoignent tardivement.

## Classes principales

Emplacement canonique : `Assets/Persistence/Save/`.

- `CharacterStateStore` : JSON personnages, roster, inventaires et compatibilité.
  Il persiste aussi les PV restants des items defensifs combat entames par
  personnage.
- `WorldSaveAdapter` : lecture/écriture du snapshot binaire du slot actif.
- `WorldStateManager` : capture et application ordonnée d’un `WorldSnapshot`.
- `PersistentNetworkObject` : identité stable et agrégation des providers.
- `IPersistentStateProvider` : payload spécifique d’un composant.
- `NetworkObjectRegistry` : résolution par `persistentId`.
- `SpawnManager` : IDs et reconstruction des objets runtime.
- `JoinSyncSystem` : transfert de snapshot et blocage du late join.

## Flux principaux

CharacterStateStore prepare le JSON personnage, le binaire monde et les
metadonnees sans remplacer les fichiers, puis les publie ensemble via
SaveMetadataWriter.WriteBatchAtomic. TryPrepareWorldSnapshot et
TryPrepareSaveMetadata ne publient aucun nouveau payload. Les fichiers gardent
leurs noms et formats existants ; la capture d'ecran reste hors transaction.

Les completions StorySequence suivent maintenant ce meme commit : modifications
en memoire uniquement, hydration depuis le slot selectionne et publication avec
les autres metadonnees. Retour menu/nouvelle session invalident le cache, sans
ecrire les changements abandonnes. La creation d'un nouveau slot manuel reprend
les completions courantes ; ce slot ne devient chargeable qu'avec les donnees
personnage. Aucun ancien slot jouable n'est modifie a la fin d'une sequence.
Qualification native : story-persistence-tests.xml, 63 tests passes, aucun
echec/ignore, dont cinq regressions narratives et controle des scenes de build.
Cette suite ne qualifie ni le rendu narratif ni la synchronisation hote/client.

Identites de prefab reseau : EnsurePersistentObject conserve le hash d'un
NetworkObject runtime deja present au lieu de le remplacer par le nom du clone.
Le late join reel a identifie le depot orbegold Light_Blue(Clone), hash
3532671608 (hash du nom), alors que la fabrique enregistre item:orbegold:loot.
Deux regressions couvrent item simple/depot, reconstruction et renommage.
Les hashes d'objets deja spawnes ne sont plus modifies par cet installateur.
Suite native network-prefab-hash-tests.xml : 65 passes, zero echec/ignore,
dont les deux nouveaux cas. Replay hote/client encore a qualifier.

Le replay du 24 octobre a ensuite revele un deuxieme ecart de fabrique : le
serveur ajoute PersistentContainerState au depot, mais le handler NGO client
ne l'ajoutait pas. Son snapshot etait refuse pour provider container absent,
malgre un objet et une identite correctement synchronises. CreateItemInstance
et son fallback installent maintenant ce provider avec l'infrastructure loot,
avant tout snapshot, sans allouer d'identite runtime cote client. Les objets
simples ne recoivent pas ce provider. Suite native
network-loot-provider-verified-tests.xml : 69 passes, zero echec/ignore,
dont les trois nouvelles regressions de fabrique. Replay reseau a terminer.

Acteurs combat inactifs : NGO peut appeler InitializeVariables avant Awake lors
d'une synchronisation de scene/late join. Les NetworkList controlStates et
cooldownStates de CombatNetworkActorRuntime sont donc creees a la declaration,
pas dans Awake, qui ne doit surtout pas remplacer une liste deja deserialisee.
Le NetworkBehaviour de la version NGO embarquee initialise et dispose aussi ses
variables dans OnDestroy si elles ne l'ont jamais ete. Pas de nouvelle variable,
permission ni format reseau. Le run 6f61adffd24f422d9ffab93adb0054c9 a reproduit
controlStates null pendant la synchronisation native de District ; regression
inactive avant/apres Awake validee dans inactive-network-actor-tests.xml :
75 passes, zero echec/ignore. Replay hote/client de ce correctif en cours ;
ces tests seuls ne qualifient pas une synchronisation native de zone complete.
Le replay 9e10931ebdff44f693b7e21a76637e23 depasse l'ancien blocage de liste,
mais echoue sur la synchronisation de VigilAltar_Hub_Safeguard de Maison
(hash 1275473975, scene non retrouvee puis NetworkObject.Deserialize). Le run
est arrete proprement EN ECHEC, aucune prise RPC ni transition cliente qualifiee.

Chaque publication prepare des copies neuves et des sauvegardes des anciens
fichiers dans un dossier voisin .lit-save-stage-<id>. Le journal
.lit-save-journal est durable avant le premier remplacement ; un marqueur de
commit est durable seulement apres le dernier. Ecritures temporaires avec
Flush(true), puis Replace/Move. Un verrou de slot empeche deux transactions de
se chevaucher. Avant commit, la recuperation restaure tous les fichiers anciens
et retire ceux qui n'existaient pas ; apres commit, elle garde tous les nouveaux.
Le journal/les backups restent disponibles si une cible verrouillee empeche la
recuperation. Le nettoyage interrompu d'un commit ou rollback termine est
rejouable sans reinterpreter la decision. Une corruption de journal ou un backup
manquant refuse la lecture ; aucune reparation d'identite n'est improvisee.

Les lecteurs personnage/monde et le catalogue recuperent un journal en attente
avant de consulter l'existence ou le contenu de leur fichier. Les metadonnees
illisibles restent non chargeables. Il s'agit d'une transaction recuperable
pour le flux autoritaire du jeu, pas d'une promesse de coherence de lecteurs
externes ouvrant directement les fichiers pendant leur remplacement. Les
anciennes API d'ecriture d'un seul fichier restent disponibles ; elles ne
constituent pas a elles seules une sauvegarde complete de partie.
CharacterStateStore refuse une capture monde absente/incomplete et ne publie
LastSaveSucceeded qu'apres le commit commun.

Les fichiers temporaires atomiques utilisent un nom court independant de la
cible, ajuste au budget de chemin Windows. CreateNew reserve exclusivement ce
nom ; une collision est retentee sans effacer le fichier d'un autre ecrivain.
Le format du journal, les noms de staging et le flush durable restent inchanges.
Le parcours Play a reproduit un ancien chemin temporaire de 270 caracteres
alors que la cible finale etait valide ; les tests couvrent publication,
remplacement et recuperation dans des slots aux chemins de 180/200 caracteres.
Validation native : 52 tests passes dans long-path-and-loot-tests.xml, dont les
quatre cas de chemins Windows et le contrat d'import du slot UI.

GameFlowService ne detruit plus la session si la sauvegarde demandee au retour
menu echoue : il rend l'exploration et annonce l'echec. Les clients quittent sans
ecrire le disque de l'hote. Avant capture/depart, les demandes PostLoading deja
lancees sont terminees sous overlay, puis leur archive est restauree si necessaire.
Arreter une coroutine ne suffit pas a annuler une demande native retenue a 90 %.
Ces nouveaux raccords attendent encore leur qualification Play et reseau.

WorldSaveAdapter controle lui-meme l'autorite : seul le solo ou le serveur d'une
session a l'ecoute peut ecrire/restaurer une sauvegarde disque. Les clients
recoivent toujours le monde par JoinSyncSystem. Une validation d'identite en
echec ne publie plus HostWorldRestoreCompleted ni LastRestoreSucceeded.

SnapshotSerializer refuse les donnees vides, versions inconnues, comptes ou
longueurs negatifs/hors payload, types inconnus et octets surnumeraires avant
reconstruction. Le format v3 ajoute SceneArchives ; v1 et v2 restent lisibles
avec une archive initialement vide. Les exceptions de lecture attendues
remontent comme un echec dans WorldSaveAdapter.

WorldStateManager conserve maintenant une archive par sous-scene. GameFlow
capture les scenes de depart avant de les decharger, puis restaure celles
reellement rechargees avant le NavMesh et le placement des joueurs. Le hub
reste en memoire en reseau : une scene deja chargee ne recoit pas un ancien
etat d'archive. PostLoading et retrait explicite d'un cycle passent aussi par
ce raccord. La capture de sauvegarde exporte les archives des zones absentes
avec les objets actuellement charges ; la lecture ne charge pas leurs scenes.

Une restauration selective conserve le pipeline existant transforms/providers/
references, mais limite le retrait des objets runtime a ses scenes et ne
reapplique pas variables globales/capacites/escouade. Les objets runtime sans
parent rejoignent leur scene sauvegardee, sauf personnages et singletons.
Le provider d'un SceneMarker garde son etat en attente si l'acteur attend
encore la navigation. Un changement de partie efface toutes les archives.
Les providers actives sur une racine inactive restent captures/appliques :
le filtre porte sur enabled, et non isActiveAndEnabled. Les providers
explicitement desactives restent ignores. Coffres, passages secrets, puzzles
et flammes resolvent leurs references meme si Awake n'a pas encore ete appele.
Cette distinction a ete reproduite par les tests de scene fermee/recreee :
le filtre ancien perdait le payload d'une racine inactive sans signaler d'echec.

Les definitions d'items propres aux sous-scenes sont ajoutees a ItemRegistry
pendant PrepareSceneContainers, depuis representedItem et storedItems, y compris
sur les racines inactives. Pas de nouveau scan global ni de rebinding d'un ID deja
resolu. Le catalogue initial de Bootstrap n'est pas suppose exhaustif. Le retour
dans District apres recharge disque a reproduit des definitions jon_collar et
jon_grave_marker introuvables alors que leurs assets etaient references dans la
scene. PersistentContainerState resout maintenant toutes les definitions avant
de modifier le conteneur : un manque signale reste un echec mais ne vide plus
son contenu auteur. Les 57 tests natifs passent, dont les trois nouvelles
regressions definition locale inactive, ID deja lie et contenu conserve en cas
de definition absente. Recette solo 3cfb072ef8f84a1d929fa407c8ef0edb passee :
84 assertions, zero erreur capturee, deux voyages Maison/District/Maison et une
vraie recharge disque entre les deux. Depot 3/reprise 1/reste 2, identites/PV/
inventaire/pose et etat d'ennemi archive retrouves. Defaite d'ennemi et approche
UCC sont des fixtures explicites ; ni victoire manuelle, ni pickup auteur, ni
recompense, ni reseau qualifies par cette preuve. Refus de sauvegarde de sortie
et recuperation transactionnelle valides avec verrou des metadonnees du nouveau slot.

InteractableItem enregistre l'identite d'un objet de scene vide avant son
retrait : cette trace desactive l'exemplaire recree par la scene. Les objets
runtime consommes sont absents de la liste runtime de la scene suivante.
Ce raccord explicite concerne le retrait des objets ramassables ; les autres
systemes detruisant definitivement des objets de scene restent a auditer.
Une capture de provider echouee bloque l'ecriture du monde et le depart de
zone plutot que de remplacer l'etat conserve par un payload incomplet.

JoinSyncSystem differe les demandes pendant la transition de l'hote et les
reprend ensuite ; le client attend la scene active de l'hote et les sous-scenes
du snapshot. GameFlow active la synchronisation NGO de scene active. Les
archives sont transportees comme donnees : seul l'hote restaure une zone au
retour. Ces raccords demandent encore une recette avec un vrai hote/client,
notamment late join, transfert interrompu et deconnexion pendant transition.

L'export late join valide la structure complete du snapshot, archives comprises,
avant envoi. Les demandes recues pendant une restauration de l'hote sont mises
en attente et remettent le client non pret. La comparaison avec la sauvegarde
historique controle les identites encore presentes, mais autorise les objets
consommes depuis cette sauvegarde et les nouveaux objets runtime. Une identite
historique presente ne peut changer ni de type ni de prefab, meme archivee.
La verification de restauration disque immediate reste stricte : un objet
attendu absent est toujours un echec, contrairement a l'export du monde vivant.

Les depots solo issus de WorldPickupUtility recoivent une identite runtime
allouee par SpawnManager et un PersistentContainerState. Reconfigurer le meme
depot conserve son identite ; deux depots issus du meme prefab sont distincts.
BuildingInfoInteractable utilise uniquement pour l'information d'un objet
recuperable ne le classe pas comme batiment et ne supprime plus son provider
de contenu. Seul un vrai batiment legacy, lorsque son systeme est actif, possede
ce payload a la place du conteneur autonome. Meme regle a la preparation de
scene et a la reconstruction ; le chemin reseau conserve son allocation propre.

Regression native Unity : 46 tests passes, sans echec ni ignore, rapport
Library/PersistenceContinuityValidation/loot-presentation-tests.xml. Cela valide
les payloads/identites et transactions isoles, pas encore les actions d'inventaire
en jeu ou le parcours rendu Maison/District, ni un vrai hote/client.

Un parcours Play reel de depot/reprise partielle a ensuite verifie les quantites
3 puis 2 via les API d'inventaire/ramassage. Sa fermeture a revele qu'une entree
sans scene valide pouvait rester dans le registre : CapturePersistentObjects
tentait alors de consulter une archive avec une cle nulle. Le registre refuse
desormais les prefabs/assets hors scene et retire les entrees devenues invalides
de ses caches. La consultation d'archive tolere aussi un nom absent sans changer
le payload de capture existant. Regression sur un vrai prefab asset ajoutee :
47 tests natifs passes, zero echec/ignore (registry-isolation-current-tests.xml).
La nouvelle recette Play de fermeture/aller-retour reste a verifier.

GameFlow distingue l'absence de JSON personnage d'une restauration existante
avant de choisir le placement initial de zone. Une nouvelle partie sans arrivee
explicite utilise son ZoneSpawnPoint ; une sauvegarde existante conserve sa pose.
Un echec de restauration ne doit pas etre masque par ce placement de nouvelle
partie. Le test de menu/recharge utilise une pose differente du spawn pour ne pas
confondre restauration reelle et remise au depart.
Un snapshot monde present mais refuse interrompt aussi ApplyLoadedData au lieu
de valider une restauration partielle via les donnees de compatibilite du JSON.
L'ancien chemin sans fichier monde reste lisible pour les sauvegardes legacy.

Version 9 du JSON personnage : healthInitialized/currentHp conservent les PV du
joueur. A la capture, le controller actif est prioritaire ; un personnage non
instancie conserve son payload charge precedent. Au chargement solo, les PV sont
restaures apres BindCharacterData. Au spawn reseau, seul le serveur les restaure,
apres l'identite du NetworkObject. Le maximum reste celui de l'acteur, les PV
sont bornes et zero reste zero. Un ancien JSON sans healthInitialized garde
l'initialisation historique, sans etre interprete comme un personnage mort.

Spawn solo : SquadManager appelle EnsureRuntimeCharacterIdentity apres le bind,
comme le chemin NetcodeCharacterIdentity deja utilise en reseau. Le clone prend
runtime:character:<id> / character:<id>, sans modifier le prefab ni inventer un
nouveau GUID. Sans ce raccord, le prefab pouvait exporter son GlobalObjectId nul
comme objet de scene introuvable au rechargement. La capture JSON des PV et la
restauration du snapshot monde restent deux controles distincts.

Quand le snapshot a deja reconstruit Lucian avant StartRoutine, SquadManager
adopte l'acteur exact du registre par ID et prefab attendus ; aucun second spawn
ni recherche par nom/proximite. PersistentSceneMarkerCharacterState resout son
marker lors de Capture/Apply, meme avant Awake sur une racine inactive. Il peut
etre sur l'acteur baked : pas de RequireComponent(SceneMarker), qui ajoutait un
marker vide sur cet acteur et masquait son vrai parent. Le marker auteur reste
le proprietaire du clone et de son etat de mort.

Sauvegarde :

`CharacterStateStore.Save` → preparation JSON + monde/providers + metadonnees →
`SaveMetadataWriter.WriteBatchAtomic` → journal → remplacements → commit.

Chargement :

lecture du JSON → restauration des données de squad → lecture du snapshot →
résolution des objets de scène → spawn/destruction runtime → transforms →
providers → références finales.

Un `SceneMarker` peut utiliser un personnage déjà baked comme enfant du marker
en solo, ou instancier son personnage runtime lorsqu'aucun bake n'est présent.
En session réseau, la copie baked est masquée et le spawn serveur existant est
conservé. Dans tous les cas, le marker applique sa pose en coordonnées monde.
La vie et l'état actif d'un ennemi restent sauvegardés, mais sa position et sa
rotation sont réinitialisées au marker à chaque nouvelle session. Hors session réseau, aucun
`NetworkTransform` n'est créé ou activé pour ce clone. En host/client, le serveur
applique le parentage via `NetworkObject.TrySetParent` et synchronise le clone en
espace local. La position persistée d'un ennemi n'est jamais appliquée : cela
évite qu'une ancienne position invalide replace l'acteur à l'origine du monde
ou sur un autre niveau du décor.

Les sauvegardes personnage version 7 stockent
`CharacterSaveEntry.combatDefenseItemHitPoints` comme des piles `itemId` + PV
restants + quantite. Seuls les items encore portes et entames sont sauvegardes;
les items pleins sont deduits du nombre total porte.

`CharacterStateStore.SuppressNextAutomaticSave` permet d'ignorer une seule
sauvegarde automatique `OnDisable`/`OnApplicationQuit`. Le combat l'utilise
avant un retour `MainMenu` ou un rechargement checkpoint apres defaite, afin de
ne pas ecraser la sauvegarde active avec l'etat de mort juste avant le
chargement.

`CharacterStateStore.CaptureRuntimeState` / `RestoreRuntimeState` exposent aussi
une restauration en memoire, sans ecriture disque ni screenshot. Le retry de
combat s'en sert pour remettre inventaires, items combat, PV de bouclier,
flamme, Munin et donnees lisibles a leur etat pre-combat, pendant que le monde
persistant est restaure via un `WorldSnapshot` en memoire. Cette capture runtime
de retry peut conserver les issues de validation sans les log en erreurs console,
pour ne pas polluer l'entree combat avec des providers de scene incomplets.

`CharacterSaveEntry` reste le format de persistance des inventaires, de la
flamme, des trois objets combat, des boucliers et de Munin. Au chargement,
`SquadManager` hydrate d'abord `CharacterRuntimeState`; un controller instancie
consomme ensuite cet état. A la sauvegarde, le controller actif est prioritaire,
puis le cache runtime couvre les membres non instanciés. `CharacterData` ne
porte aucune donnée mutable de partie.

Late join :

client demande le snapshot → serveur sérialise et segmente → client reconstruit
le monde → restaure son personnage local → envoie `client_ready`.

## Pièges observés

- `persistentId`, `runtimePrefabId` et `ProviderId` sont des contrats de migration.
- Une collision d’ID invalide la résolution; ne pas générer d’IDs ad hoc.
- L’ordre des phases d’application est intentionnel.
- Le snapshot doit correspondre à la scène active.
- Les clients sont bloqués par `JoinSyncSystem.IsGameplayBlocked` pendant la sync.
- Les snapshots Building legacy sont conservés mais ignorés si le système est désactivé.
- `EditorAutoSave` sauvegarde scènes/assets côté Editor; la persistance runtime
  reste isolée dans `Application.persistentDataPath` et ses écritures/lectures
  runtime doivent être ignorées hors Play Mode.
- Les PV de bouclier sont propres au personnage. Ne pas les placer dans l'asset
  `Item`, sinon tous les personnages partageraient la meme usure.

Le parcours MainMenu écrit les métadonnées via SaveMetadataWriter (temporaire,
flush, remplacement atomique) et remonte les erreurs. Les entrées dont les
métadonnées sont invalides restent supprimables mais ne sont pas chargeables.
JoinSyncSystem borne à deux les nouvelles tentatives après un premier transfert
échoué ; PrivateSessionService assure le délai global d'entrée réseau et le
retour au menu. Le salon ne déclenche pas de demande de snapshot de gameplay.

## Identite des flammes du corridor

La flamme historique garde scene-flame:Maison:0DE640398. Sa copie distincte
dans District_1_Corridor_Flammes utilise desormais
scene-flame:District_1_Corridor_Flammes:4389905704937368489. Le format de sauvegarde
est inchange ; un ancien etat partage ne permettait pas de distinguer les deux.
