# Narration et connaissances

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

## Pièges observés

Le cycle Nina (`Assets/Narrative/NinaCycle`) utilise un controleur serveur et les
variables monde sauvegardees pour ses quatre jalons. La lettre ne donne Dilemme
Edouard qu'a sa lecture. Nina change immediatement de pose avec ce savoir; la
visite qui active Scar exige aussi la cinematique terminee et Existence des chimeres.
DialoguePanelUI.TryShowTimedConversation distingue fin naturelle et annulation.
NinaGhostInteraction adapte uniquement les deux fantomes de ce cycle. La scene
contient des emplacements explicites; les ressources artistiques restent a assigner.
La competence de Scar est composee a la lecture par SkillsManager depuis la variable
de monde, sans mutation de CharacterData ni duplication de recompense.

- `dialogueMaxDisplayDuration = 0` attend indéfiniment `Interact`.
- Chaque chemin de sortie d’une séquence doit libérer focus, caméra et verrou UCC.
- Les IDs narratifs sont persistants; les renommer nécessite une migration.
- `GhostData` est une donnée d’auteur; l’état compris/résolu appartient au runtime.
- Timeline est réservée aux chorégraphies complexes, pas aux dialogues ordinaires.
- La Timeline `GiantJuggernaut_Intro` est une presentation camera/animation :
  elle ne porte aucun `TimelinePlayerMoveTrack` et ne doit pas modifier la
  position de Lucian. Le verrou UCC de la sequence suffit a le maintenir en place.
