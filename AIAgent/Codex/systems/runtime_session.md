# Session runtime

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
confirmations conservent leur propre contrôleur ; une suppression sélectionne
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
