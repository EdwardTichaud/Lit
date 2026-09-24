# Narration et connaissances

## Transition Ghost/Enemy et disparition apres dialogue

GhostController.SetGameplayMode applique Ghost, Introduction ou Enemy. Pour une
rencontre startAsGhost, EnemyController conserve l'etat autoritaire (local en
solo, NetworkVariable en reseau). CombatEnabled tient compte de cet etat :
perception offensive, verrouillage, degats et riposte restent bloques avant
Enemy. CharacterInfo.DataChanged reapplique le mode apres affectation tardive.
La fermeture reussie de l'introduction, validee par token, client initiateur et
duree minimale sur le serveur, ouvre le combat. Annulation/desactivation ou
deconnexion de l'initiateur rend le Ghost disponible; les anciens callbacks
sont ignores. Le dialogue reste diffuse aux clients de la session.

CycleDialogue.disappearAfterCompletion utilise le rewardFlag existant ou les
completedFlags; aucun nouveau format de sauvegarde. Le cycle transmet le jalon
au Ghost, qui attend disappearanceDelay en temps reel puis dissout son corps et
termine ses effets. Scar commence sa dissolution des la fermeture qui accorde
Cicatrice (disappearanceDelay=0), puis se desactive apres les effets. Son dialogue
reste affiche deux secondes hors fondus (durationSeconds=2). Zero conserve la
duree du cycle pour les autres dialogues. UI et validation serveur utilisent
la meme duree resolue. Le delai
zero ne saute pas la dissolution; restoreImmediately distingue le chargement.
Une premiere presentation avec jalon deja charge masque directement
le Ghost. Les activations de cycle et la proximite ne peuvent pas le reapparaitre.
Les dialogues Nina ne sont pas configures pour cette disparition.

## Rencontre Ghost dans EnemyController

enemyEncounterOptions.requiredKnowledge est une condition facultative de la
rencontre. Le scientifique n'en a pas : son interaction Ghost lance directement
l'introduction et le combat. `Existence des chimères` est révélée par le cycle
dès que sa défaite `encounter` est enregistrée, avant toute cinématique ou
présentation de fin. La connaissance suit le partage de session existant.

ScientistEncounterController est supprime. CharacterData.enemyEncounterOptions active la phase Ghost, la replique d introduction, sa duree et la distance d interaction. EnemyController implemente IGhostInteractionHandler et ICycleCinematicBlocker. Les controles serveur, la visibilite Ghost et la progression Nina sont conserves. Les dernieres paroles sont configurees dans enemyDeathOptions et bloquent la presentation de cycle pendant leur lecture.

## Liaison des rencontres et poses de cycle

CycleController accepte un encounterEnemy explicite pour les ennemis places en
scene sans SceneMarker. Avec un marker, RuntimeInstance est prioritaire sur la
copie baked. Le cycle observe CharacterInfo.HealthChanged et retrouve aussi un
ennemi deja mort lors du chargement. Nina utilise cette liaison directe.
CyclePoseBinding peut piloter un booleen Animator suivant la condition; Nina
utilise isDead. Les noms courts d'etats sont resolus avec le nom du layer.
La presentation Dead requiert les deux connaissances et reste independante de
la disponibilite de la Timeline.

## Rôle

Exécuter les séquences cinématiques, afficher les dialogues, gérer les
connaissances et résoudre les interactions narratives.

## Classes principales

- `StorySequenceAsset` / `StorySequenceRunner` : données et exécution des étapes.
- `StorySequenceSceneBindings` : acteurs, caméras, timelines et événements.
- `StorySequenceCameraDriver`, `StorySequenceDialoguePresenter`,
  `StorySequenceFadeController` : présentation.
- `StorySequenceCompletionStore` : progression `playOnce` dans le slot.
- `KnowledgeManager` / `KnowledgeSO` : connaissances débloquées.
- `GhostData` / `GhostController` : enquêtes et réactions conditionnelles.
- `ReadableContentRuntime` : contenu généré stable et sauvegardable.

## Flux principaux

- Une séquence attend le personnage local, verrouille l’input/UCC, pilote caméra,
  dialogues et étapes, puis restaure le gameplay.
- Les acteurs sont résolus par ID, squad ou `LocalPlayerContext`.
- Les connaissances débloquent des réactions de fantômes et des effets de scène.
- Un `Item` peut débloquer des connaissances à sa récupération
  (`knowledgeUnlockedOnPickup`) ou à sa consultation (`knowledgeUnlockedOnRead`).
  Les deux chemins passent par `KnowledgeReveal` : le serveur les valide et la
  notification est envoyée à toute la session.
- Une `KnowledgeSO` peut aussi porter un `CombatKnowledgeModifier` passif. Si
  son option combat est active, l'effet s'applique automatiquement au combat
  temps réel tant que la connaissance est débloquée; elle n'est ni équipée ni
  consommée.
- Quand une réaction de connaissance est disponible, le feedback du fantôme joue
  seulement la réponse de résolution, sans répéter la ligne d’apparition, la
  question par défaut ou l’option joueur.
- Les contenus lisibles générés sont capturés dans `CharacterStateStore`.
- Les séquences `playOnce` sont enregistrées dans les métadonnées du slot.
- `SceneMarker` est le point d'auteur unique pour les personnages, items et
  fantômes. Son Inspector permet `Bake in Scene` pour les items et fantômes :
  leur `WorldPrefab` devient alors un objet de scène déjà configuré, sans
  instanciation runtime. Le Bake Character conserve le marker de persistance et
  place l'acteur immédiatement en solo ; en réseau, cette copie est masquée et
  le spawn Netcode existant reste autoritaire. Après chaque Bake, l'Inspector
  vérifie la hiérarchie produite : en l'absence de `RuntimeOutlineTarget`, il
  propose de l'ajouter sur l'un des enfants qui porte un `Renderer`. Les anciens
  `ItemSceneMarker` se migrent depuis le menu `Lit/Scene Marker`.

## Cycles reutilisables et progression de partie

CycleDefinition contient metadonnees de journal (sans UI), prerequis et etapes
nommees. CycleProgressionService est installe sur le WorldRulesStateManager de
session par NetcodeBootstrap. Il possede les transitions autoritaires et les
notifications ; CycleController garde les liaisons et presentations de scene.
Les objectifs disponibles avancent en parallele ou selon des prerequis toutes/
au moins une, incluant un autre cycle termine. Les sources couvrent connaissance,
dialogue ferme, interaction, ennemi vaincu et sequence terminee. Un evenement
precoce n'est pas memorise ; connaissances et defaites sont des faits persistants.
Un evenement ne traverse pas deux etapes successives attendant la meme source.

Les cles .step.<id>, .defeat.<sourceId> et .completed reutilisent les variables
monde et snapshots existants. Le service diffuse les etats uniquement depuis le
serveur et actualise aussi les clients apres ClientMarkedReady. La progression
et les skills restent donc accessibles apres destruction du controleur de scene.
Les recompenses sont composees par CycleSharedSkills et ne modifient pas les
fiches source. La migration des anciens bits Nina est idempotente et silencieuse.

CycleInteraction fonctionne aussi sans Ghost via les interfaces de detection
et d'input locales existantes. Les rencontres et sequences sont des liaisons
multiples avec ID. Une sequence ne suspend que les ennemis explicitement lies,
sans parcourir ou interrompre les autres cycles du monde. Les callbacks tardifs
de playback ne peuvent pas liberer les references d'une nouvelle presentation.

Les etapes terminales definissent la fin partagee. Le serveur attend les effets
finaux et les clients connectes au plus completionPresentationTimeout (15 s),
puis annule les presentations restantes du cycle avant de solliciter GameFlow.
GameFlow protege la scene principale, joueurs et services de session, et ignore
les scenes terminees aux chargements suivants et dans sa validation NavMesh.
Nina conserve sa fin au bit 8 et les bits historiques 1/2/4/16. Ses six etapes
nommees sont dans la fiche ; aftermath_seen est facultative. La Timeline et le
profil du Director de la scene Nina sont encore absents : aucune completion
fictive n'est attribuee a cette sequence.

L'Inspecteur expose des categories repliables, des conditions selectionnees par
nom, un etat runtime lisible et les diagnostics de references/dependances.
Le modele vide et la migration Nina sont sous Lit/Narrative et Assets/Create.
Guide d'auteur et verification : Assets/Narrative/Cycles/README.md.

## Pièges observés

Le cycle Nina (contenu dans `Assets/Narrative/NinaCycle`) utilise CycleController
(`Assets/Narrative/Cycles`), un controleur serveur generique, et les
variables monde sauvegardees pour ses jalons. La lettre ne donne Dilemme
Edouard qu'a sa lecture. Existence des chimeres est revelee des la mort confirmee
du scientifique, via KnowledgeReveal, sans attendre la cinematique. Le chargement
d'un etat ScientistDefeated accorde aussi ce savoir s'il manque.
Nina devient Dead uniquement lorsque toutes les connaissances
du cycle (Existence + Dilemme) sont acquises, sans autre jalon requis. Parler a Nina
Dead active ensemble sang et Scar (bits NinaDeadSpoken=16 et NinaVisited=4).
Scar reste invisible au loin et apparait progressivement a proximite via le
GhostController standard. La portee utilise le collider comme l'interaction standard.
Ghost_Scar porte un Rigidbody dynamique avec gravite et rotation bloquee dans
la scene Nina : modele et zone d'interaction suivent ensemble les contacts du
monde. Le prefab Scar conserve une seule capsule corporelle solide, avec les
colliders de squelette/accessoires desactives et le root motion coupe. La
revelation ne pilote pas la gravite; l'activation narrative remet le corps en
simulation. Aucun repositionnement force au sol n'est ajoute.
Les anciennes sauvegardes avec un seul des deux bits restent compatibles.
DialoguePanelUI.TryShowTimedConversation distingue fin naturelle et annulation.
CycleInteraction relie chaque fantome a un dialogue de CycleDefinition.
GhostController gere seul revelation, outline et detection, sans adaptateur Nina. La scene
contient des emplacements explicites; les ressources artistiques restent a assigner.
La competence de Scar est composee a la lecture par SkillsManager depuis la variable
de monde, sans mutation de CharacterData ni duplication de recompense.
Le dialogue valide de Scar accorde Cicatrice a sa fermeture naturelle, apres le
fondu puis validation serveur de la portee et des prerequis. Une annulation ou
un dechargement ne donne pas la recompense. Le bit RewardGranted conserve le deblocage
au chargement et pour les clients tardifs, sans rejouer la notification.
SkillUnlockPanel utilise le panneau dedie de Bootstrap (identifiant distinct du
panneau des connaissances), avec titre/description et une file de notifications
de 4.5 s en temps reel. Son API TryShow accepte SkillSO et StatsSO; chaque source
l'appelle uniquement apres un nouvel apprentissage (Scar, LearnSkill du personnage).
Cicatrice est retiree des skills et de l'equipement initiaux de Lucian; aucun
equipement automatique n'est ajoute. Le format des sauvegardes reste identique.

La defaite du scientifique utilise ScientistEncounterController.PlayDeathPresentation :
Death + replique + AudioClipSO deathVoiceLine. RealTimeCombatSceneUiController
attend la fermeture du dialogue avant le panneau de victoire. La cinematique Nina
attend ensuite la fermeture du resultat pour ne pas remplacer les derniers mots.
La presentation est locale a chaque joueur, le resultat de combat reste inchange.

- `dialogueMaxDisplayDuration = 0` attend indéfiniment `Interact`.
- Chaque chemin de sortie d’une séquence doit libérer focus, caméra et verrou UCC.
- Les IDs narratifs sont persistants; les renommer nécessite une migration.
- `GhostData` est une donnée d’auteur; l’état compris/résolu appartient au runtime.
- Timeline est réservée aux chorégraphies complexes, pas aux dialogues ordinaires.
- La Timeline `GiantJuggernaut_Intro` est une presentation camera/animation :
  elle ne porte aucun `TimelinePlayerMoveTrack` et ne doit pas modifier la
  position de Lucian. Le verrou UCC de la sequence suffit a le maintenir en place.

## Transition fantome vers ennemi

Le Scientifique utilise GhostController avec GhostData_Scientist avant la
rencontre. Le routage optionnel IGhostInteractionHandler est commun aux
CycleInteraction et ScientistEncounterController : une seule source d'input et
de detection sur le fantome. Le serveur valide le joueur, la portee et la
revelation avant la transition. SetGhostMode(false) arrete les coroutines et
l'input du fantome, puis rend le corps visible pour le mode suivant. Il ne doit
plus disparaitre a distance pendant le combat. La replique d'introduction
existante precede l'activation du cerveau et de la navigation ennemis.
L'instance Nina herite des quatre composants CombatHealth/CharacterInfo/
EnemySkills/RealTimeCombatEnemy du prefab sans copie additionnelle.
