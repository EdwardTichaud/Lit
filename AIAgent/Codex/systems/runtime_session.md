# Session runtime

## Installation explicite du combat commun (2026-10-17)

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

## Coherence des scenes de District 1

La liste runtime du manifest inclut Indoor_Lights comme sa reference SceneAsset :
21 scenes additives requises et le Core. L'eclairage se trouve a la racine Assets,
donc ZoneManifestBuildSettingsSync cherche dans Assets, pas uniquement Scenes.
Une ambiguite de nom refuse l'ajout automatique. Les entrees Build Settings
Volume_Castle et Crypt_Flammes, absentes et sans consommateur runtime, sont retirees.
La recette isolee peut demander -combatMigrationSaveRoundTrip pour verifier un
retour menu/rechargement reel (PV, deblocages existants et ennemi sous marker).
Ce controle utilise un slot Library jetable ; il ne constitue pas une preuve
de victoire via le gameplay ni une validation manuelle du combat.

## Repetition isolee de migration

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
une Basic et une ecriture/lecture de snapshot ; elle ne valide ni une restauration
complete de sauvegarde ni le ressenti, les autres zones ou le multijoueur.

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
