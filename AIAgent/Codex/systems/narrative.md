# Narration et connaissances

## Rencontre Ghost dans EnemyController

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
  le spawn Netcode existant reste autoritaire. Les anciens
  `ItemSceneMarker` se migrent depuis le menu `Lit/Scene Marker`.

## Cycles reutilisables

CycleDefinition configure un ID persistant, plusieurs dialogues (conditions,
textes, effets a l'ouverture/a la fermeture, skills) et un encounter/cinematique
facultatif. CycleController lie les acteurs de scene, activations et poses Animator
aux conditions, sans noms de personnages ni recompenses codes en dur.
CycleInteraction porte seulement le cycle et l'ID du dialogue ; GhostController
conserve le comportement visuel et l'interaction communs a tous les fantomes.
Le serveur valide ouverture puis fermeture avec token, duree, portee et prerequis.
Annulation/despawn nettoient les demandes en attente. CycleSharedSkills compose
les recompenses persistees de toutes les definitions sous Resources/Narrative,
sans modifier les personnages ni notifier a nouveau au chargement.
Le schema de sauvegarde reste un entier par ID ; la migration Nina conserve tous
les GUID et la cle narrative.district1.nina. Le guide d'auteur est dans
`Assets/Narrative/Cycles/README.md`. Les inspecteurs signalent les IDs et bits en
collision ainsi que les ressources requises manquantes.

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
