# Netcode

## Callbacks Routes Et SendMessages

NetworkCharacterInput recoit Jump, LocomotionMode, TriggerMunin et ToggleTorch
depuis LocalInputRouter avec InputAction.CallbackContext. Ses methodes privees
portent le prefixe OnRouted, pas On<Action> : sur le meme acteur, PlayerInput en
SendMessages envoie InputValue et tentait auparavant de les appeler, meme hors
reseau (MissingMethodException reproduite sur un vrai saut). Abonnement et
desabonnement emploient les memes nouveaux delegates ; corps, controles owner,
RPC et validation serveur restent inchanges. Aucun nouveau recepteur brut ni
deuxieme envoi de commande n'est ajoute. Audit des UnityEvents auteur : aucune
reference serialisee a ces methodes privees trouvee. Quatre regressions de noms/
signatures et une recette solo RT/A repetee passent ; ceci ne qualifie pas un
client distant ou les reactions en ligne.

## Flux participants raccordes (2026-10-17)

CombatNetworkActorRuntime integre maintenant les ticks/restitutions de palier,
reaction, cinematique et clash dans son cycle existant. Les reservations sont
nettoyees au retrait d'acteur et a l'invalidation du monde. Un QTE de palier est
execute par le moteur existant du joueur source, jamais par substitution de l'hote.

Les pools de lecture sont references explicitement sur chaque prefab joueur,
avec les definitions Counter/Light indexees par GUID. Les evenements et signaux
de contact rejoignent le rig actif de cet acteur ; seul le serveur applique les
impacts. Camera et inputs sont limites au participant proprietaire. Les snapshots
de garde, roulade et flottement ne conduisent aucun moteur physique client.

Les deux adaptateurs ennemis ouvrent l'opportunite chez la victime engagee.
B/Y transportent leur horodatage dans l'horloge serveur ; le contact ferme toute
possibilite de contre retroactif. La protection B reste limitee a l'action ennemie.
Le dernier engagement remet la clarte a zero, pas chaque changement de cible.
Les contacts optionnels sont revalides par le volume spatial reel ; les victimes
obligatoires/cinematiques ne sont jamais substituees.

Les anciens paragraphes indiquant ces flux incomplets decrivent le checkpoint
precedent. Leur integration actuelle reste a tester sur un vrai host/client.

## Extension de l'executant, sans recette reseau (2026-10-17)

Le catalogue admet maintenant les attaques ordinaires a evenement natif, Rupture,
Ascension et l'impact au sol. Les evenements sont toujours valides par clip, etat,
ticket et session ; aucun impact client ni degat de secours. La Fleche applique
son stun au contact auteur, pas a la fin du projectile visuel.
Le serveur observe le maintien X apres une Basic confirmee, attend sa fin,
publie la preparation puis lance la charge avec son horloge. La continuation
n'utilise pas un faux numero de commande client. Camera de preparation locale
uniquement au proprietaire. Le lock declare est valide par acteur et capture pour
Rupture, independamment des changements de lock ulterieurs.
Le snapshot d'action transmet victime et direction capturees aux presentations.
CanReplace valide la prochaine action avant de restituer la precedente. Les
fenetres externes suivent le profil auteur ; annulations et watchdog de progression
liberent uniquement les ressources de la session fautive.
La roulade serveur appelle CombatMobilityController.TryDodgeActor et le moteur
PlayerScriptedDodgeController existant ; son invulnerabilite est propre a l'acteur.
La garde distante utilise TryBeginGuardForActor et sa generation de presentation.
Projection/flottement utilisent ICombatChargedTarget et les moteurs ennemis
existants. Mort, sortie de monde et despawn liberent les maintiens et defenses.
EnemyController choisit sa vision parmi les joueurs assignes au serveur, sans
attribuer une source inconnue a l'hote. Les feedbacks ennemis sont confirmes par
RPC depuis le receveur autoritaire, sans RPC de montants de degats entrants.
Reactions/QTE/cinematiques distants et clash multipaire restent incomplets.
Couverture du catalogue et compilation ne prouvent pas leur fonctionnement NGO.

## Auteur reseau et isolation des participants (2026-10-17)

CombatNetworkSetup configure avant spawn les dependances du spawner joueur :
NetworkTransform, identite, contexte local, input, inventaire et persistance.
Les composants existants sont conserves ; autorites multiples ou mouvement non
serveur sont refuses plutot que remplaces silencieusement.
Les ennemis utilisent NetworkTransform et NetworkAnimator serveur existants,
avec binding explicite vers leur Animator auteur et restitution de l'espace de
transformation lors du parentage au marker. Aucun nouveau moteur n'est ajoute.
Le catalogue et l'inventaire sont calcules une seule fois par passe auteur.

ContainsParticipant/IsCinematicParticipant consultent uniquement les reservations
cinematiques, pas les tickets ordinaires d'attaque. Un autre combat ne perd plus
ses impacts parce que le manager local affiche une cinematique.
Ces garanties de code/auteur restent distinctes d'une session NGO hote/client.

## Executant combat par acteur (2026-10-16)

CombatNetworkActorRuntime est un NetworkBehaviour auteur sur l'ActorRoot avant
spawn. CombatNetworkSkillCatalog derive des IDs deterministes des GUID d'assets,
controle les collisions d'IDs et conserve l'equipement propre a chaque acteur.
CombatNetworkSetup propose/applique explicitement le catalogue et les composants
des prefabs accessibles, avec preflight, manifeste et liste des familles refusees.

Le serveur execute les BasicSkills au sol et les supports/controles pris en
charge sans reaffecter RealTimeCombatManager a un acteur distant. Animation Events
locaux serveur, portee/contact/obstruction et tickets identifient les impacts.
PV, effets persistants, cooldowns et menaces du proprietaire sont repliques ;
les observateurs ne peuvent ni appliquer les degats ni prendre la camera locale.
Les RPC historiques de support deleguent/refusent leur ancien chemin quand
l'adaptateur est installe. Le cooldown affiche suit la confirmation serveur.

Continuation du 17 octobre : chemins concrets ajoutes pour Basic aerienne,
dashs de contact, buffer Basic borne, Juste Retour, Sommation et Pas de recul.
Le clip Basic aerien auteur possede desormais son evenement d'impact unique.
Les raccords de charge, projection/zone et recuperation sont maintenant implementes
(voir la section ci-dessus). Reactions/QTE, cinematiques et clash multiacteur
restent incomplets ; aucune recette reseau reelle n'a ete executee.
Replication complete des animations ennemies et test host/client reel requis.
Ne pas activer la bascule globale sous pretexte que la compilation est reussie.

## Rôle

Créer l’infrastructure NGO, connecter les joueurs, attribuer les personnages et
faire respecter l’autorité serveur.

## Classes principales

- `NetcodeBootstrap` : crée `NetworkManager`, transport et services persistants.
- `NetcodeLauncher` / `NetcodeConnectionApproval` : host/client et validation.
- `NetcodePlayerSpawner` : attribution, spawn et ownership des personnages.
- `WorldInteractionService` : assignations client-personnage et interactions monde.
- `NetworkCharacterInput` : commandes du propriétaire vers le serveur.
- `NetworkInventory` : réplication d’inventaire.
- `NetcodePrefabRegistry` : mapping des prefabs réseau.
  Ses caches de session sont aussi vides au retour complet en edition, pour
  liberer les references de prefabs sans Domain Reload. Ils restent disponibles
  pendant l'arret reseau et sont reconstruits a la session suivante.

## Flux principaux

1. `NetcodeBootstrap` s’installe avant les scènes.
2. Le launcher démarre host ou client avec Unity Transport.
3. Le serveur choisit un `CharacterData`, crée ou réutilise son instance et
   donne l’ownership au client.
4. `WorldInteractionService` publie l’assignation.
5. `NetcodeLocalPlayer` / les utilitaires mettent à jour `LocalPlayerContext`.
6. Les inputs du propriétaire sont envoyés au serveur, qui pilote le gameplay.
7. Pendant une exploration réseau, la Maison reste chargée en arrière-plan : un
   joueur qui rejoint tardivement y apparaît, puis rejoint le groupe par portail.
   Les destinations de portail réservent toujours l'index 0 au personnage
   principal. Elles sont memorisees avant le dechargement de la scene source
   puis appliquees apres le chargement de la destination; `ZoneSpawnPoint`
   reste reserve au spawn initial de Maison.

## Pièges observés

- Le package NGO embarque un garde-fou dans `NetworkObject.OnDestroy` lorsque
  le SpawnManager subsiste sans son dictionnaire runtime pendant le teardown
  editeur. Le retrait du suivi de scene ne depend plus de NetworkConfig et
  tolere une collection deja absente. Conserver ces correctifs lors d'une mise
  a jour du package; le controle d'autorite reste actif pour les objets spawnes.
- `AutoSpawnPlayerPrefabClientSide` est désactivé : le spawner projet est maître.
- L’ownership NGO et l’assignation métier sont deux informations distinctes.
- Les mutations monde, inventaire et combat sont autoritaires serveur.
- Les objets de scène sont préparés par `NetcodeSceneObjectInstaller` à chaque
  chargement.
- Les objets sous `NetworkObject` ne doivent pas etre reparentes par des helpers
  `DontDestroyOnLoad` avant que le host/server n'ecoute; les objets reseau de
  scene restent dans leur hierarchie Netcode.
- Les systèmes de snapshot/persistance réseau vivent sous
  `Assets/Persistence/Save/World/`; `NetcodeBootstrap` les crée toujours au
  runtime quand l’option dédiée est active.
- Tester les changements en host et client distant, pas seulement en host local.

## Temps de combat partage

`TimeManager` refuse les handles globaux/locaux de ralenti et de pause des que
NGO ecoute (host, serveur ou client, meme avant l'arrivee d'un second joueur).
L'echelle globale/base effective et l'echelle geree des domaines restent a 1 ;
le pas physique initial est conserve. Les handles solo existants sont retires
a la transition, sans resurrection a la deconnexion. Hors session a l'ecoute,
composition, pauses et expiration solo restent inchangees. Les pauses intrinseques
de gameplay des acteurs (ex. dormance) ne sont pas des effets temporels et restent.
Le menu peut toujours gerer son interface/input ; sa demande de pause globale ne
doit pas arreter la simulation partagee. Aucune pause reseau consensuelle ajoutee.

Pour les contres avec `EnemyReactionTiming`, la largeur en secondes de clip est
`counterWindowSeconds / reactionTimeScale` en ligne, bornee au debut de l'attaque
et excluant toujours l'impact : .2 a .4 donne .5, ou moins si l'impact est precoce.
Une configuration solo sans ralenti (echelle 1) conserve .2. Les trois lectures
(pression, resolution differee, disponibilite) utilisent la meme regle. Les
fenetres reelles B, contres sans contexte et QTE de palier ne sont pas allongees.
Les etats/generations/contacts continuent d'invalider les contres normalement.

Tests cibles : `MultiplayerCombatTimeTests` et regression `FairCounterTimingTests`.
La fixture Edit Mode simule uniquement l'etat NGO a l'ecoute, sans transport ;
compilation Unity, execution NUnit et parcours reels host/client restent requis.

## Rencontre scientifique et migration RPC

ScientistEncounterController conserve un etat local en solo sans ecriture de
NetworkVariable. En ligne, seul le serveur apres spawn effectue les transitions.
La desactivation/despawn annule l'attente et retire les abonnements. Les RPC
migres utilisent Rpc(SendTo.Server) avec InvokePermission explicite ; les controles
metier du serveur sont conserves. Host et clients doivent utiliser le meme build.
