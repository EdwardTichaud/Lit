# Ideas

## LIT - Vision Narrative & Systemique (Mise a jour)

### 1. Concept General

Lit est un jeu d'exploration 3D a la troisieme personne centre sur :
- La lumiere comme outil de survie
- La manipulation du temps fige
- L'exploration d'un evenement historique mal compris
- La tension entre connaissance et danger

Contrainte transversale : le jeu doit supporter le multijoueur (jusqu'a 4 joueurs, un joueur par personnage).
Tous les systemes et la conception doivent en tenir compte.
Mode cible : multijoueur en ligne via Unity Netcode (host-client).

### Etat Multijoueur (implementation en cours)

- Netcode runtime : `NetcodeBootstrap` cree/assure un unique `NetworkManager` + `UnityTransport`, configure `EnableSceneManagement`, desactive `AutoCreatePlayerObject`, puis prepare les objets de scene et prefabs au chargement de scene.
- Demarrage test : `NetcodeLauncher` permet de demarrer un host (`F5`) ou un client (`F6`), et d'arreter (`F7`) (adresse/port configurables dans `NetcodeLauncher`).
- UI de session : `NetcodeLobbyUI` (auto-cree par `NetcodeBootstrap`) affiche un panneau Host/Join, genere un code et mappe le code -> port (base 7000, range 1000). Connexion directe UTP (pas de relay/UGS pour l'instant).
- UI multijoueur : tant que le panneau est visible, les controles joueur/camera sont bloques (`InputFocusStack` + lock `SquadManager`).
- Toggle UI : l'action d'input `Multi` affiche/masque le panneau multijoueur.
- Menu principal : au lancement, la scene `MainMenu` affiche un menu type BG3 (liste des parties a gauche, details a droite). Le host/join se fait depuis ce menu.
- Sauvegardes : `SaveSessionManager` gere des sessions + sauvegardes (`persistentDataPath/Saves/<sessionId>/<saveId>/CharacterState.json`) et expose les metadonnees (date/heure, temps de jeu, scene). Le menu recharge ces donnees et permet de hoster une sauvegarde.
- Connection approval : `NetcodeConnectionApproval` active `ConnectionApproval` et lit la payload (playerId) pour lier le client a son dernier personnage. La payload est definie par `NetcodeClientIdentity`.
- Identite client : `NetcodeClientIdentity` stocke un GUID dans `PlayerPrefs` et l'envoie en payload (connection data) via `NetcodeLauncher`.
- Attribution des joueurs : `NetcodePlayerSpawner` (serveur) assigne un personnage par client en utilisant `SquadManager.currentSquad` (fallback `Resources.FindObjectsOfTypeAll<CharacterData>()`). Si un personnage existe deja en scene (ex: host lance en cours de partie), il est reutilise et converti en objet reseau (pas de duplication). Sinon, un prefab reseau est instancie au point de spawn (`squadSpawnPoints` ou `squadSpawnOrigin`). Il spawn aussi `WorldInteractionService` une seule fois.
- Switch perso (1-3 joueurs) : en multijoueur, un joueur peut ouvrir le squad panel (toggle `LeftShoulder`) et selectionner un personnage libre pour demander un switch (RPC `WorldInteractionService.RequestCharacterSwitchServerRpc`). Le serveur valide que le perso n'est pas deja assigne et met a jour l'association client -> personnage.
- Squad panel (affichage constant) : les portraits de l'equipe restent visibles en permanence. Le panel est "actif" uniquement quand `LeftShoulder` est presse (selection/switch), sinon il reste visible mais inactif.
- Statut G/S : `G` = personnage groupe avec le personnage controle par le joueur (suivi actif). `S` = personnage independant (ne suit pas). Tout personnage controle par un joueur est toujours affiche en `S`.
- Marqueur joueur : `squadPanelCross` est le prefab utilise par `SquadUISettings` pour afficher un marqueur sur le perso assigne. La croix du joueur local est teintee en bleu.
- Controle : les inputs locaux sont routés via `LocalPlayerInput`/`LocalInputRouter`. `NetworkCharacterInput` envoie les inputs du joueur possede au serveur. Chaque joueur controle uniquement son personnage (ownership Netcode).
- Audio : l'`AudioListener` n'est actif que pour le personnage possede. Les voicelines baissent la musique via `AudioManager.BeginMusicDucking` (multiplicateur par defaut 0.5, configurable) puis restaurent le volume a la fin.
- Inventaire reseau : `NetworkInventory` est autoritaire serveur (use/break/drop/place) et synchronise l'etat (items + torche) via `NetworkList`/`NetworkVariable`. Les loot containers sont synchronises via `NetworkList`.
- Construction/craft : `BuilderController` est autoritaire serveur et publie les constructions via `NetworkList`; build/upgrade/craft/catalyseur passent par `ServerRpc` avec validation cote serveur.
- Interactions serveur : `Brasero`, `Lever` et `LootContainer` sont `NetworkBehaviour`; `ReturnHomeTrigger`, `HubCompanionSwapTrigger`, `LabyrinthStartTrigger` passent par `WorldInteractionService` (RPC serveur + `ClientRpc` cible).
- Assignations reseau : `WorldInteractionService` maintient une `NetworkList` clientId -> characterId pour identifier le perso controle (utile pour le switch et l'affichage des marqueurs).
- Persistance : `CharacterStateStore` sauvegarde/charge uniquement cote serveur quand Netcode est actif.
- Objets de scene : `NetcodeSceneObjectInstaller` ajoute/prepare les `NetworkObject` pour les `NetworkBehaviour` deja presents dans la scene. Les IDs reseau sont derives du chemin d'objet + scene (`NetcodeSceneIdUtility`) : renommer/reparentage change l'ID.
- Persos en scene : `NetcodeSceneObjectInstaller` ajoute aussi les composants Netcode (`NetworkObject`, `NetworkTransform`, `NetworkCharacterInput`, `NetworkInventory`, etc.) a tous les `SquadCharacterController` de la scene pour permettre le switch sans duplication.
- Prefabs dynamiques : `NetcodePrefabRegistry` enregistre des handlers (`INetworkPrefabInstanceHandler`) par hash stable (`NetcodeStableHash`). Les `NetworkObject` et composants reseau sont ajoutes en runtime aux instances (pas de modification des prefabs sur disque).
- Hashs de prefabs : les items utilisent `ItemIdUtils.GetItemId` (fallback nom d'asset), les personnages utilisent `CharacterData.UniqueId`/`characterId` (fallback nom). Garder ces IDs stables pour eviter des desync. Si un item n'a pas de prefab, un cube fallback est instancie.
- Notes/limites : les items/persos doivent etre charges a l'execution pour etre enregistres. Le mode actuel est host-client via `UnityTransport` (pas de relay/auth dedicatee a ce stade).
- Hierarchie avant Play : pas besoin d'ajouter `NetworkManager` ni `NetcodeBootstrap`. La scene doit au minimum contenir `SquadManager` (avec `currentSquad` ou des `CharacterData` accessibles via `Resources`) et idealement `squadSpawnPoints` ou `squadSpawnOrigin`.
- Hierarchie en Play : `NetcodeBootstrap`, `NetworkManager`, `EventSystem`, `LobbyCanvas` (UI), `LocalPlayerInput` sont crees automatiquement. Au demarrage du host, `WorldInteractionService` et les joueurs reseau sont instancies. Les objets avec `NetworkBehaviour` en scene reçoivent un `NetworkObject` runtime via `NetcodeSceneObjectInstaller`.
- Continuite joueur : chaque client envoie un `playerId` persistant (GUID en `PlayerPrefs`) via la payload d'approbation de connexion Netcode. Le serveur mappe `playerId -> characterId` (sauvegarde `CharacterStateStore`) et assigne ce personnage si disponible, sinon fallback vers le prochain perso libre. L'IP n'est pas utilisee (instable/NAT).

Le joueur controle un groupe de 4 explorateurs envoyes dans des labyrinthes instables afin de comprendre ce qui s'est reellement produit dans le passe et recuperer des reliques liees a ces evenements.

### 2. Contexte Narratif

Il y a 300 ans, une armee tenta d'assieger un immense chateau. Face au siege, les habitants du chateau declencherent un rituel de defense. Ce rituel tourna mal.

Consequences :
- Le chateau fut fige dans plusieurs strates temporelles
- Toute vie organique a l'interieur disparut
- Des echos, ombres et fragments temporels persisterent
- L'armee, temoin de l'anomalie, leva le siege et quitta les lieux

Les chroniques historiques parlent d'un siege interrompu par une catastrophe inexpliquee.

### 3. Le Village et les Explorateurs

Des siecles plus tard, des descendants de cette armee montent une expedition pour comprendre :
- Ce qui s'est reellement passe
- Si le rituel represente toujours une menace
- Ce que protegeait reellement le chateau

Les explorateurs ne sont pas des soldats, mais des enqueteurs formes a manipuler une flamme particuliere capable de survivre dans l'instabilite du chateau. Seuls les porteurs de cette flamme peuvent evoluer a l'interieur sans etre consumes.

### 4. Structure du Chateau (Premier Labyrinthe)

Le chateau est instable et existe simultanement dans plusieurs etats temporels. Il n'est pas vide : il est sature de memoire et de fragments d'existence.

On n'y trouve aucun vivant. Seulement :
- Des squelettes
- Des echos
- Des Ombres
- Des vestiges du rituel

### 5. Systeme des Braseros (Temps Global)

Des braseros sont dissemines dans le chateau. Chaque brasero allume ancre le chateau dans une epoque donnee.

Exemple :
- 0 brasero -> An 0 (avant le rituel)
- 1 brasero -> An 100 (siege)
- 2 brasero -> An 200 (rituel)
- 3 brasero -> An 300 (ruine)

Changer d'epoque modifie :
- La structure du chateau
- L'acces a certaines zones
- L'etat des ponts, escaliers, murs
- La disposition d'objets
- Les fragments narratifs disponibles

Les braseros permettent de reconstruire les strates du rituel.

### 6. Systeme des Orbes (Perception Locale)

Les orbes modifient temporairement la lueur de la torche. Ils n'alterent pas l'epoque globale, ils modifient la perception du joueur.

- Orbe Bleu — Lueur d'Ordre. Revele : passages caches, connexions invisibles, structures stabilisees. Usage : navigation et comprehension spatiale.
- Orbe Rouge — Lueur de Convoitise. Revele : tresors, ressources rares, zones optionnelles. Usage : prise de risque pour recompense.
- Orbe Violet — Lueur d'Echo. Revele : fragments du passe, squelettes animes par memoire, instants figes du rituel, dialogues narratifs.

Les morts ne donnent pas de solutions directes. Ils partagent :
- Des souvenirs fragmentes
- Des avertissements
- Des interpretations biaisees

Utiliser le violet consomme davantage de torche et peut augmenter la pression ambiante.

### 7. Les Ombres

Les Ombres ne sont pas des ennemis classiques. Elles sont :
- Des residus du rituel
- Des zones ou le temps s'est effondre
- Des fragments instables d'existence

Elles prosperent dans l'obscurite. La lumiere les repousse. Certaines couleurs peuvent les perturber. Elles representent la consequence persistante du rituel.

### 8. Gestion du Groupe

- 4 personnages jouables
- Switch instantane
- Mode groupe ou independant

Les personnages doivent cooperer pour resoudre :
- Enigmes spatiales
- Manipulations de leviers eloignes
- Changements d'epoque risques
- Traversees instables

Seuls les survivants rapportent les ressources.

### 9. Tension Centrale

Le joueur est constamment face a un dilemme : explorer davantage pour comprendre ou preserver son equipe.

Manipuler le temps (braseros) peut :
- Ouvrir de nouveaux chemins
- Bloquer des retours
- Separer le groupe
- Intensifier l'instabilite du chateau

La connaissance a un cout.

### 10. Themes

- Memoire fragmente
- Histoire mal comprise
- Heritage et responsabilite
- Lumiere contre oubli
- Manipulation du passe

## Idees

1. Enigme a deux leviers.
2. Les deux leviers doivent etre actives en meme temps par 2 personnages.
3. Un personnage reste bloque apres l'activation.
4. Il peut s'en sortir seulement s'il a la competence "sauf conduit" (se faufiler dans des trous etroits).
5. Le trou etroit doit etre detecte au prealable par un personnage avec la capacite "observateur" pour pouvoir etre utilise.
