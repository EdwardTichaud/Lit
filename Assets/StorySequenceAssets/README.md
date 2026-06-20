# Story Sequence Assets

Ce dossier centralise le système de séquences narratives de Lit.

## Structure

- `Scripts/` : runtime, données et composants de scène.
- `Editor/` : création du rig, génération d'assets de départ et validation.
- `Sequences/` : assets `StorySequenceAsset`.
- `CameraProfiles/` : cadrages réutilisables.
- `Timelines/` : Timelines réservées aux chorégraphies complexes.
- `Prefabs/` : UI ou rigs personnalisés si les versions runtime ne suffisent plus.

## Première installation

1. Ouvrir la scène cible.
2. Exécuter `Lit > Story Sequences > Create Runtime Rig In Scene`.
3. Exécuter `Lit > Story Sequences > Create Starter Assets`.
4. Assigner une séquence au `StorySequenceRunner`.
5. Ajouter `StorySequenceActor` aux PNJ et leur donner un `actorId` stable.
6. Utiliser `lucian` ou `player` pour le personnage local créé par `SquadManager`.
7. Exécuter `Lit > Story Sequences > Validate Open Scene`.

## Règles de production

- Une réplique vocale reste un `VoiceLineData`.
- Une conversation est un `StorySequenceAsset`.
- Les plans récurrents utilisent un `StorySequenceCameraProfile`.
- Un plan exceptionnel utilise un `StorySequenceCameraPoint` dans la scène.
- Timeline sert aux déplacements et animations coordonnés, pas au séquençage ordinaire des dialogues.
- `Interact` termine uniquement l'étape courante si elle est marquée `skippable`.
- Une étape `Dialogue` affiche le texte de son `VoiceLineData` dans le `DialoguePanel`.
- `Dialogue Max Display Duration = 0` attend indéfiniment `Interact`. Une valeur positive impose un délai maximal.

`playOnce` est enregistré dans le `meta.json` du slot actif via `SaveSessionManager`.

- Une nouvelle partie démarre avec une progression vide.
- Un nouveau slot créé depuis la partie courante hérite des séquences terminées.
- Charger un ancien slot restaure l'état correspondant à ce slot.
- Sans sauvegarde active (test direct d'une scène), l'état reste transitoire et est remis à zéro
  au prochain Play Mode.

Les anciennes clés `PlayerPrefs` ne sont plus utilisées.
