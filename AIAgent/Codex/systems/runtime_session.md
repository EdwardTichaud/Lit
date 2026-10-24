# Session runtime

## Synchronisation de pose UCC/Netcode

Les joueurs utilisent ServerUccNetworkTransform, toujours serveur autoritaire.
OnTransformUpdated reporte la pose NGO interpolee dans CharacterLocomotion et
SimulationManager via les API natives du moteur. Sans ce raccord, NGO recevait
la bonne cible mais UCC reimposait son cache de spawn a chaque simulation.
Le spawn et les teleports reseau declenchent le snap UCC complet une seule fois.
L'interpolation ordinaire ne stoppe pas les capacites et n'emet pas de snap de
camera/Animator. Le changement Grounded/Land est laisse au prochain pas physique.
Le serveur et le solo ne passent jamais dans ce chemin de reception.

Les quatre prefabs joueurs deja authors conservent fileIDs et reglages du
NetworkTransform ; seul son script devient ce derive. La fabrique dynamique
emploie le meme type, et l'authoring refuse d'ajouter une seconde autorite sur
un ancien composant. Aucun objet reseau n'est ajoute au personnage solo.
Qualification : 75 tests persistance passes dans la derniere execution et cinq
tests authoring passes auparavant (execution mixte par ailleurs en echec de
SetUp). Le replay franchit la distance cliente apres correction du cache, mais
le parcours complet avec portail/late join reste NON valide : le dernier essai
echoue sur le mappage d'un objet de Maison avant de rendre le controle client.

## Session locale d'un client synchronise

GameFlow observe les scenes Core et la scene active recues via NGO pour creer
le GameplaySessionRoot configure cote client, une seule fois, sans charger de
scene ni creer de personnage. La restauration de snapshot dispose ainsi des
managers locaux avant son application. La navigation et l'initialisation de
l'escouade cliente n'interviennent qu'APRES les variables de progression : un
cycle termine peut avoir retire volontairement sa scene du groupe de l'hote.
JoinSync applique le snapshot une seule fois, attend cette seconde phase puis
envoie client_ready et libere le controle. La phase locale utilise le budget
NavMesh existant plus une marge de 5 s ; le delai de transfert reste distinct.
L'hote conserve scene loading, placement et spawn ; aucun restore disque client.
Qualification native : 69 tests passes. Essais rendus 690c557883b94b62ba03b2269ab7e507
et 52e0fa372f9d44048b5198175d54928b : session cliente creee, snapshot applique
une fois, navigation Ready, depot stable retrouve et ack recu par l'hote.
Ces deux anciens essais ne qualifient pas les transitions ni le ramassage.
Le conflit de cache UCC/NGO a ensuite ete demontre et corrige par le raccord
decrit plus haut, sans changement d'autorite. Le dernier late join apres portail
(9e10931ebdff44f693b7e21a76637e23) echoue sur VigilAltar_Hub_Safeguard,
hash 1275473975 de Maison_Enigme_Vigil, puis NetworkObject.Deserialize.
Le mappage/despawn des objets NGO apres voyage reste a analyser ; aucun succes
global reseau n'est deduit des seules phases ou suites natives passees.

## Recette Charge Sans Cible

-combatMigrationCharge exige -combatMigrationGamepad et son aller-retour de zone.
Le meme validateur teste deux maintiens X distincts par destination : relachement
pendant le ralenti puis lancement complet. Les callbacks/erreurs/peripherique
sont nettoyes par leur propre cycle. Duree bornee a 40 s avec attente du vrai
cooldown, aucune modification du skill, du timer, de la progression ou du sol.
Cas dfba2a9d62c5430ca189107904d2a224 passe, 119 empreintes identiques ; resultat
et sources dans Library/ProductionCombatMigration/Charge. Le champ limitation
historique du JSON de base reste generique stick/Basic ; les checks supplementaires
et le rapport precisent la charge sans cible. Ce n'est pas une preuve de projection
ennemie, d'interruption, de sauvegarde/recharge ou de reseau.

## Recette Des Defenses De Production

CombatBootstrapPlayValidation accepte -combatMigrationDefenses avec
-combatMigrationEncounter, eventuellement -combatMigrationEncounterLifecycle.
Le Gamepad propre a la recette passe par les inputs reels, dans l'engagement
production ; ne pas ajouter -combatMigrationGamepad, reserve au transfert de zone.
Deux cycles roulade/garde avec restitution sont requis, sans suspension d'IA.
Les checks de base de rencontre sont journalises une seule fois pendant cette
observation. Cas 5d4f296413df4de0b8e67f70d0555596 passe avec lifecycle,
fixture Flame explicite, sources comparees dans
Library/ProductionCombatMigration/Defenses. Aucune qualification globale deduite.

## Installation explicite du combat commun (2026-10-17)

Extension du 2026-10-19 : camera de production dediee conservant sa base
d'exploration, promotion d'Ascension sans changement d'identite, trois Basics
aeriens et presentations de contact auteurs. Le dry-run inclut leurs fichiers
dans le manifeste et les sauvegardes ; aucune installation automatique au boot.
Les reglages de deplacement Burst/freinage sont ceux du FreeAttackProfile partage
par le monde, y compris pour l'execution serveur.

Bootstrap doit referencer District 1 dans zoneManifests, pas uniquement dans
editorStartManifest. Sinon StartOrLoadGame("District_1_Core") et le chargement
d'une sauvegarde de cette zone ne trouvent pas son manifeste et chargent Core
seul. La migration ajoute la reference canonique manquante, sans doublon ni
remplacement des autres zones. Aucun fallback vers le raccourci editeur n'est
introduit dans le runtime. Une recette rendue a reproduit l'echec Core seul,
puis valide les 22 scenes, NavMesh Ready, Lucian et une Basic apres correction.

CombatMigrationImplementationInstall orchestre les services communs, les quatre
kits, la conversion du vrai Juggernaut, l'authoring reseau et les adaptateurs de
rencontre. Avant ecriture : plan, sauvegarde des fichiers/metadonnees concernes
et refus des assets sales. Les profils Giant nouvellement crees sont egalement
dans le manifeste. Une seconde application doit etre sans ecriture et byte-stable.

Le mode planned de CombatNetworkSetup est reserve au preflight avant installation
des reglages de session ; Apply exige ensuite les vraies references configurees.
Aucun defaut temporaire n'est remplace par des valeurs runtime inventees.
Le rapport separe authoringInstalled et ReadyForProduction. Aucun de ces outils
ne lance de Play Mode ou ne fournit de preuve de sauvegarde/multijoueur.

## Spawns Et Couverture Des Zones

Maison_Core : le Transform auteur 0_SpawnPoints/LucianSpawnPoint ne portait aucun
ZoneSpawnPoint. La recette depuis Bootstrap reproduit un depart a l'origine de
squad (0,-0.71,3), pas au point auteur. Le meme objet porte maintenant le composant
ZoneSpawnPoint (Default, slot 0), sans changement de pose, parent ou GUID de scene.
La regle GameFlow deja presente ne l'utilise que pour une nouvelle partie sans
JSON personnage ni arrivee explicite ; les sauvegardes gardent leur pose.
Recette 7a37b21178774e04a2d2920f775a5641 passee : dix scenes, NavMesh Ready,
Lucian seul au sol (-0.47,-0.56,5.99), Basic/fin de session et cycle curseur natif,
snapshot monde ecrit/lu. Ni sauvegarde/recharge reelle, ni visite des interactions,
ni transfert Maison/District 1, ni reseau qualifies par cette seule recette.

CombatBootstrapPlayValidation accepte -combatMigrationZoneManifest suivi du
chemin Assets/.../*.asset explicite. Sans option, District 1 reste la recette
existante. Le JSON indique le manifeste teste ; une selection invalide echoue
sans fallback silencieux. Les fixtures Juggernaut et SaveRoundTrip restent
explicitement reservees a District 1, pas simulees pour une autre zone. Sept
tests couvrent selection, refus et composant sur le vrai Transform auteur.

Option -combatMigrationZoneRoundTrip, uniquement avec le manifeste Maison, sans
fixtures de rencontre, recharge ou repetition : apres la Basic initiale, place
Lucian sur sol libre pres du portail auteur et appelle son vrai ServerTryUse.
Les poses d'arrivee viennent du portail, sans ouverture anticipee de District 1
ni substitution de spawn. Verifie son chargement complet, le dechargement des
scenes Maison en solo, le nouvel identifiant de monde NavMesh, acteur/PV/
progression/binding uniques et commandes autorisees. Une Basic doit terminer en
moins de huit secondes avant ReturnToHub(Default), puis meme verification dans
Maison avec une nouvelle Basic. Cas 6bf1904b99794059aa90056332b7b46b passe. Les
90 s par phase bornent le chargement ; le watchdog global reste 180 s. Ce n'est
ni un test de mouvement au stick, ni une interaction clavier/manette manuelle,
ni une preuve audio, de sauvegarde/recharge ou de transition multijoueur.

Option supplementaire -combatMigrationGamepad : branche les commandes virtuelles
sur ce meme parcours apres chaque Basic d'arrivee. La premiere fixture generique
refusait le trajet avant toute pression ; elle utilise maintenant la capsule et
le GroundedRaycastHit du moteur. Cas natif courant 125c75c0dc9d43129a260098dd757546
passe pour mouvement physique camera-relatif et X, sans appels directs aux
executeurs pour ces deux gestes. Details et limites dans input_camera_ucc.md.
L'audit de sources a trouve un decalage .2/.5 de fenetre de contre reseau dans
les quatre prefabs de la copie, pas dans le principal : copie resynchronisee sans
retoucher le gameplay livre. Quatre regressions verifient maintenant leurs vraies
valeurs auteur (.5 contre, .5 roulade, .4 antispam). Suite elargie 540/1/1,
echec Etienne reserve a l'auteur, 61 hashes compares ; pas une preuve reseau.

Complement -combatMigrationLocomotion (requiert Gamepad) : RT avec le stick,
relachement, puis deux pressions A et atterrissages par destination. Utilise
tous les executeurs existants et conserve les bornes de phase. RT doit atteindre
routeur, bridge, tier de course et augmenter la vitesse physique ; les sauts
doivent rendre les verrous normalement. L'ecoute temporaire des erreurs de
runtime fait echouer cette phase ; elle est retiree a la fin/nettoyage. La baseline
a revele le conflit SendMessages/NetworkCharacterInput corrige par renommage des
delegates prives. Cas 7e1e274b5f814e30bfae8f4651a7a91f passe, 544/1/1 en suite
elargie, 75 empreintes comparees. Details/limites dans input_camera_ucc.md ; ni
defenses en engagement, ni charge, ni ressenti manuel ou reseau qualifies ici.

Pour le demarrage normal sans spawnId ni JSON personnage, GameFlow utilise
desormais le ZoneSpawnPoint existant de la scene primaire. L'existence du fichier
est capturee AVANT restauration/initialisation squad. Un fichier existant, meme
invalide ou non restaure, ne doit pas devenir silencieusement une nouvelle partie
placee au spawn. Les arrivees explicites restent prioritaires ; l'option editeur
de placement de scene garde son comportement. Aucun point de spawn n'est ajoute
ou deplace et aucune projection NavMesh ne remplace la pose UCC auteur.
La recette Bootstrap exige maintenant une arrivee proche du spawn ET grounded
avant sa Basic. SaveRoundTrip capture une pose distincte sur sol physique libre
et verifie son maintien apres menu/recharge : le joueur n'a pas besoin d'un
polygone NavMesh pour ce test. Cas rendu 39e49776f3f2450bbb3f034a7ded5ae9 passe,
sans generalisation aux autres zones ou au reseau.

Retour menu : UnloadSceneIfLoaded conserve la derniere scene chargee ; le
chargement Single suivant la retire. Unity refuse son dechargement additif, et
l'ancien chemin attendait indefiniment sa disparition. La recette avec une vraie
sauvegarde a reproduit ce defaut apres dechargement des additives, sans perte du
slot Library. Un test cible couvre cette attente impossible et le nettoyage repete.
Le cycle District 1 sauvegarde/menu/recharge passe apres correction des PV et
identites : un seul Lucian, ennemi fixture toujours mort sous son marker, NavMesh
de nouveau Ready. Les autres zones et le reseau restent a qualifier.

La liste runtime du manifest inclut Indoor_Lights comme sa reference SceneAsset :
21 scenes additives requises et le Core. L'eclairage se trouve a la racine Assets,
donc ZoneManifestBuildSettingsSync cherche dans Assets, pas uniquement Scenes.
Une ambiguite de nom refuse l'ajout automatique. Les entrees Build Settings
Volume_Castle et Crypt_Flammes, absentes et sans consommateur runtime, sont retirees.
La recette isolee peut demander -combatMigrationSaveRoundTrip pour verifier un
retour menu/rechargement reel (PV, deblocages existants et ennemi sous marker).
Ce controle utilise un slot Library jetable ; il ne constitue pas une preuve
de victoire via le gameplay ni une validation manuelle du combat.

Pour qualifier uniquement la persistance avec les regles actuelles, l'option
supplementaire -combatMigrationPersistenceOnly requiert SaveRoundTrip et refuse
les autres recettes de combat/repetition. Elle garde le mode Exploration,
sans lancer de Basic ni creer la fixture du curseur de combat. Chargement,
spawn/sol, PV, progression, identite de l'ennemi et vrai retour menu/recharge
restent verifies ; aucune preuve d'attaque, de pickup ou de transfert interzone
n'est deduite de cette variante. Les anciennes recettes de combat restent
distinctes et ne doivent pas imposer une attaque en exploration pour qualifier
une sauvegarde.

## Repetition isolee de migration

Option -combatMigrationEncounter : observation bornee du vrai Juggernaut de
District 1 apres le chargement Bootstrap et une Basic. Seul Lucian est place
par l'adaptateur UCC sur un point du NavMesh courant devant l'ennemi, avec chemin
complet et ligne physique libre. Le choix de cette position de fixture sur la
surface n'altere pas les tolerances de validation du spawn ennemi. Pas de cible
override, de notification de degat ou d'aggro forcee. Le succes exige aggro sans
lock, facade engagee, mouvement reel, deux generations d'impact distinctes,
recuperation et perte de PV. Un doublon d'impact fait echouer la recette.
Option supplementaire -combatMigrationEncounterLifecycle : apres deux impacts,
place Lucian hors leash via UCC, observe la restitution du mode exploration et
des autorisations UCC/interactions, puis le fait reapprocher pendant le retour.
Une nouvelle attaque doit venir de la perception, sans lock ou aggro forcee.
Une seconde sortie attend le vrai retour home et l'absence d'impacts tardifs.
Chaque phase est bornee a 30 s. Ce n'est pas un parcours de fuite pilote au stick,
une tentative d'interaction narrative ni une preuve de collision BattleWall.
Recette d14f503cc325488a82628d855da4fe58 passee apres correction de la descente
par capsule et sortie physique bornee du support non navigable : controles,
facing, reengagement, troisieme impact puis retour final home/NavMesh/orientation
auteur et aucun impact tardif. Aucun warp, collider ignore, changement de
tolerance ou reconstruction NavMesh. Le choix de fixture utilise maintenant les
hauteurs et le masque de vision auteur, avec verification de la perception
reelle apres placement UCC. Les echecs anterieurs (meshes deja penetrants, puis
contact incline bloquant) restent dans LandingRecovery. Regression 483/0/1,
sources/assets/metas comparees a la copie validee ; pas une qualification globale.
Option distincte -combatMigrationEncounterLitFixture : si necessaire, allume
une seule Flame auteur existante couvrant un collider de l'ennemi, puis la remet
eteinte au nettoyage. Aucun rayon/placement ou gate n'est change. Ce fixture est
signale dans le JSON et ne prouve ni interaction d'allumage ni progression.
Les essais en echec restent distincts : un premier montre l'aggro mais aucune
action, puis le filtre des kits aeriens est corrige ; la recette complete apres
correction reste en echec hors portee. Le controle du spawn/sol est maintenant
complete par le cas nouveau jeu et sauvegarde decrit plus haut.
L'ecart 6/30 apres correction du spawn venait de l'import de la section vision,
ignoree apres une ligne vide dans l'asset. Suppression de cette seule ligne et
correction du repositionnement sous la portee minimale : recette bornee
52de58fb32b245f6b2487b975f212307 passee, aggro sans lock, approche et deux impacts
reels (1000 -> 960 PV). La regle de Flame reste testee avec un allumage de fixture
signale, pas avec une interaction/progression simulee comme reussie.

CombatBootstrapPlayValidation accepte -combatMigrationRepeatPlay : deux entrees
et sorties reelles de Play Mode dans un meme editeur isole, avec nouveaux slots
Library par cycle. Domain Reload est force On, ou Off avec l'option supplementaire
-combatMigrationDomainReloadOff. A chaque sortie native : aucun acteur joueur ni
singleton combat/navigation/temps ne doit survivre. Les options initiales sont
restaurees a la fin ou apres echec ; watchdog de sortie de 45 s. Le JSON par cycle
reste distinct du resume, sans fabriquer de gate de qualification.
Le timeout Basic de 8 s est conserve ; son diagnostic inclut temps de jeu,
frames, etats Animator courant/suivant, echelles et parametres. Premier essai Off
expire, puis deux cycles Off passent : anomalie intermittente encore ouverte.

CombatMigrationStaging.RunBatch exige une copie sans racine Git et l'option
explicite -applyStagedCombatAssets. Il applique les ecritures auteur, repete Apply
puis Dry Run et conserve leur resultat dans Library/ProductionCombatMigration.
Ce resultat ne vaut pas autorisation de recopier les prefabs en production.

CombatBootstrapPlayValidation ouvre le vrai Bootstrap et emprunte
GameFlowService.StartOrLoadGame vers District 1. Les seules modifications de
Bootstrap sont des overrides de scene non sauvegardes. Une session de sauvegarde
neuve utilise un dossier absolu dans Library, jamais un slot utilisateur.
La recette exige un rendu compatible HDRP et des frames d'affichage : le mode
-nographics est refuse avant toute creation de slot. L'entree de menu isolee
demande confirmation et ferme son editeur de test a la fin. Un lancement batch
ne contourne pas les attentes reelles de presentation du chargement.
La recette controle chargement obligatoire, navigation, acteur solo, bindings,
une Basic et une ecriture/lecture de snapshot. L'option SaveRoundTrip ajoute
menu/rechargement reel, PV, progression et mort d'un ennemi sous son marker,
ainsi que l'unicite de Lucian et le nettoyage natif du curseur. La mort utilise
une fixture ForceDefeat, pas une victoire jouee. Ni le ressenti, ni les autres
zones, ni le multijoueur ne sont deduits de cette recette.

## Admission du combat reseau

NetcodeBootstrap installe CombatCommandAuthority sur le NetworkManager existant.
Les adaptateurs des acteurs sont auteurs avant spawn via CombatNetworkSetup,
jamais ajoutes comme NetworkBehaviour a un objet deja spawne.
Apres chargement/validation du monde et placement, GameFlowService readmet les
acteurs de ses scenes de destination et les personnages effectivement assignes.
Un ennemi d'une ancienne zone ou du hub conserve en arriere-plan n'est pas readmis
par simple proximite ou parce qu'il est encore charge.
La bascule auteur des prefabs de session reste distincte des preuves de livraison.

## Migration combat (en integration)

GameplaySessionRoot reste le proprietaire des services de partie ;
CombatWorldRuntime configure explicitement le combat sur BattleManager.
L'arene de laboratoire n'est jamais instanciee par GameFlowService.
ProductionCombatMigration est un outil auteur manuel et reexecutable,
avec preflight et manifeste d'ecritures ; pas de migration automatique au boot.
Son succes auteur ne valide ni tous les ennemis ni le reseau.

## Rôle

Gérer les sessions et slots de sauvegarde, le passage menu/jeu et l’isolation
de l’état entre deux parties.

## Classes principales

Emplacement canonique des scripts de session de sauvegarde :
`Assets/Persistence/Save/Session/`.

- `SaveSessionManager` : sessions, slots, métadonnées, chemin du slot actif.
- `MainMenuController` : création/sélection d’une partie et chargement de scène.
- `GameplayRuntimeReset` : nettoyage centralisé des singletons et caches runtime.
- `LoadingScreenService` : transition de chargement de scène.

## Flux principaux

GameFlowService demarre uniquement si Bootstrap est presente parmi les scenes
de la hierarchie, meme lorsqu'une autre scene est active. Sans Bootstrap,
l'initialisation automatique ne cree aucun ApplicationRoot et ne redirige pas
vers le menu. Un GameFlowService place dans une scene autonome se desactive
avant de publier son singleton ou de s'abonner aux chargements de scenes.
Pour tester une scene seule, lancer Play avec Bootstrap absente ; pour lancer
le jeu complet ou Editor Test Startup, garder Bootstrap chargee.

1. Le menu crée ou sélectionne un slot dans `SaveSessionManager`.
2. Le lancement appelle `GameplayRuntimeReset.PrepareForGameplayStart`.
3. La scène de jeu est chargée et les stores utilisent le chemin du slot actif.
4. Le retour au menu déclenche `ResetForMenuScene` et arrête le suivi du temps.

## Pièges observés

- Plusieurs managers sont `DontDestroyOnLoad`; le reset explicite est nécessaire.
- Un singleton peut partager son GameObject avec d’autres composants : ne pas
  détruire aveuglément l’objet complet.
- Les statiques doivent être réinitialisés même si Domain Reload est désactivé.
- Une scène lancée directement peut ne pas avoir de slot actif; les fallbacks
  globaux sont généralement désactivés pour éviter les fuites entre parties.

## MainMenu et salon privé

MainMenu_Load presente les sessions en haut a gauche et les sauvegardes en bas.
Le survol ne reconstruit pas les sauvegardes : seule la validation d'une session
change leur contenu. MainMenuNavigation limite alors le focus aux sauvegardes ;
Retour revient aux sessions en conservant le choix. La souris synchronise ce
focus pour permettre l'alternance souris/clavier/manette. Chaque sauvegarde
affiche son nom et sa date ; l'apercu et le district issu de sceneName sont a droite.
Les deux listes utilisent ContentSizeFitter avec defilement vertical ; les
anciennes entrees sont desactivees avant destruction pour eviter un focus obsolete.
MainMenuLoadBrowserTests couvre le survol, le retour, l'isolation des sessions,
le district et les references de scene. Compilation verifiee ; tests Unity et
validation visuelle du parcours encore a executer.

`PrivateSessionService`, installé par `NetcodeBootstrap`, possède les tentatives
Relay et le salon indépendamment de la scène de menu. Le transport reste Relay,
les commandes/états de salon passent par les messages NGO `lit.private.*.v1`.
L'hôte valide quatre réservations uniques et les confirmations Prêt. Le spawner
attend le gameplay sans délai de salon et consomme les réservations avant tout
spawn, y compris lors d'une arrivée tardive. Un changement de personnage en jeu
met également à jour la réservation. Aucune migration d'hôte : fermeture et
retour au menu orchestré via `GameFlowService`.

Les tentatives sont identifiées et annulables : 30 s pour Relay/authentification,
15 s pour connexion/état initial et 120 s pour chargement/synchronisation. Les
résultats tardifs ne peuvent plus démarrer NGO après une annulation. Les
opérations Unity déjà engagées sont terminées avant le retour au menu.
`PrivateSessionRoster` dans Resources référence les quatre personnages initiaux ;
une sauvegarde filtre cette liste selon son squad. Garder ce catalogue aligné
avec les personnages du GameplaySessionRoot.

`MainMenuNavigation` possède la navigation directionnelle en menu et le panneau
réseau ; les clics souris restent gérés par UGUI. La navigation standard de
l'EventSystem est suspendue puis restaurée pour éviter un double Submit. Les
objets 3D décoratifs, même s'ils portent une action de curseur, sont exclus :
seuls les contrôles sous un Canvas peuvent recevoir le focus clavier/manette.
Une préférence d'entrée mémorisée repasse en automatique dès qu'une action vient
de l'autre périphérique, afin qu'une manette connectée ne bloque pas le clavier
(ni l'inverse).
Les confirmations conservent leur propre contrôleur ; une suppression sélectionne
Annuler par défaut. Les transitions passent par UIManager. La manette navigue exclusivement de bouton en bouton avec un cadre lumineux
et un fond de sélection, sans pointeur ni flamme mobile. La souris conserve
son pointeur. Les confirmations affichent le même highlight ; le clavier virtuel
est piloté par MainMenuNavigation pour éviter les doubles validations. Le bouton OK
ferme le clavier et sélectionne Confirmer si la saisie est valide ; un nouvel
appui est nécessaire pour soumettre le formulaire.
L’ancienne préférence GamepadPointer est ignorée et retirée des réglages.
Les réglages utilisent les canaux AudioManager existants et des PlayerPrefs.

Les métadonnées sont remplacées atomiquement par `SaveMetadataWriter`. Une
création initiale échouée est retirée, et une tentative réseau échouée réutilise
sa partie préparée. La reprise solo passe uniquement par Charger ; le bouton
Continuer et la sélection automatique de la dernière sauvegarde sont supprimés. Le cache de miniatures conserve quatre PNG (dimensions et poids
bornés). Le décor suit CatalogChanged au lieu d'explorer le disque toutes les
deux secondes. MainMenuBuildValidator contrôle les références au build.

Validation locale : compilation C# runtime et Editor avec le compilateur et les
références Unity du projet ; tests indépendants des règles de salon et des
écritures atomiques. Les tests NUnit sont dans MainMenuSessionTests. À vérifier
en Play Mode/build : navigation/modalités, configurations d'écran, annulation
Relay, salon longue durée, 2–4 joueurs, refus du cinquième, late join et perte
hôte. Pour plusieurs exécutables sur un PC, utiliser des profils distincts
`-lit-profile=host`, `-lit-profile=client1` (identité locale et authentification
Unity). Ces identités sont des identifiants de continuité, pas une preuve
cryptographique d'identité. Les connexions exigent la même version du jeu et le
protocole `lit-private-1`.

## Scenes de cycles termines

GameFlowService.TryUnloadCompletedCycleScene recoit les demandes apres attente
ou annulation des presentations finales du cycle. Il verifie l'autorite, le
jalon de fin, le nom de la scene dediee, l'absence de transition et de joueurs/
services dans la scene. Les operations Netcode occupees sont retentees, et la
scene retiree quitte loadedZoneSceneNames. Les chargements de manifeste ignorent
les cycles deja termines via CycleProgressionService.ShouldSkipScene, y compris
pour la validation des scenes necessaires au NavMesh. La scene principale n'est
jamais retiree par ce chemin. La progression reste dans le service de session.
