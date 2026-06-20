# Configuration de `Intro_Ragefort`

L'asset prêt à configurer est :

`Assets/StorySequenceAssets/Sequences/Chapter01/Intro_Ragefort.asset`

## Installation dans Maison

1. Ouvrir `Assets/Scenes/Maison.unity`.
2. Glisser `Assets/StorySequenceAssets/Prefabs/StorySequenceRig.prefab` dans la scène.
3. Sélectionner `StorySequenceRig`.
4. Dans `StorySequenceRunner`, assigner `Intro_Ragefort` au champ `Sequence`.
5. Laisser `Play On Start` actif et `Start Delay` à zéro.
6. Lancer `Lit > Story Sequences > Validate Open Scene`.

Le même rig sera exécuté si Maison est chargée depuis le menu principal ou si la scène est
lancée directement en Play Mode.

## ActorID de la squad

Les personnages créés par `SquadManager` sont résolus automatiquement depuis
`CharacterData.characterId`.

- `lucian`
- `link`
- `luna`
- `mia`

Il n'est pas nécessaire d'ajouter manuellement `StorySequenceActor` aux personnages de la squad.

Pour un PNJ placé directement dans une scène :

1. Ajouter `StorySequenceActor` sur sa racine.
2. Renseigner un `Actor Id` unique, stable et en minuscules.
3. Assigner le transform de tête dans `Face Anchor` si la résolution humanoïde automatique échoue.
4. Assigner l'Animator du personnage.

`actorId` désigne le locuteur. `listenerId` aide le cadrage caméra mais ne joue aucun dialogue.

## Spawn actuel de Maison

La scène contient actuellement les membres et points dans cet ordre :

1. Lucian → `LucianSpawnPoint`
2. Link → `MSP_1`
3. Luna → `MSP_2`
4. Mia → `MSP_3`

Modifier les transforms des points permet donc de placer et orienter les personnages sans toucher
à la séquence.

## Déroulement configuré

1. Écran noir immédiat.
2. Attente de la création du personnage local et de la squad.
3. Verrouillage du personnage et de la caméra.
4. Passage de toute la squad dans le cycle assis.
5. Révélation de la scène depuis le noir pendant quatre secondes.
6. Dialogue de Link.
7. Dialogue de Lucian.
8. Dialogue de Mia.
9. Restauration de la caméra UCC et du contrôle local.

Les lignes sont textuelles pour le moment. Assigner ultérieurement un `AudioClipSO` à chaque
`VoiceLineData` ajoutera la voix sans modifier `Intro_Ragefort`.

## Durées

En l'absence d'audio, le champ `Duration` de chaque étape Dialogue sert de durée minimale :

- Link : 3,5 secondes
- Lucian : 10 secondes
- Mia : 4 secondes

`Interact` termine immédiatement la réplique active. Les étapes d'assise et de fondu ne sont pas
skippables dans cette introduction.

## Test conseillé

Tester d'abord directement depuis Maison, puis depuis MainMenu. Pour accélérer les itérations,
réduire temporairement la durée du fondu dans l'étape `Révéler la scène depuis le noir`.
