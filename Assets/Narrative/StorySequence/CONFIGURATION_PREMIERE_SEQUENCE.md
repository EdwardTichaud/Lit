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
4. Placement immédiat de toute la squad dans la boucle `Sitting_Idle`, sans jouer `Sit_Down`.
5. Révélation de la scène depuis le noir pendant quatre secondes.
6. Dialogue de Link.
7. Dialogue de Lucian.
8. Dialogue de Mia.
9. Restauration de la caméra UCC et du contrôle local.

Les lignes sont textuelles pour le moment. Assigner ultérieurement un `AudioClipSO` à chaque
`VoiceLineData` ajoutera la voix sans modifier `Intro_Ragefort`.

## Durées

Chaque étape `Dialogue` affiche le texte de son `VoiceLineData` dans le `DialoguePanel`.

- `Dialogue Max Display Duration = 0` : la réplique reste affichée jusqu'à `Interact`.
- Valeur supérieure à `0` : la réplique avance automatiquement après ce délai maximal.
- `Skippable` doit rester actif pour permettre à `Interact` de terminer immédiatement la réplique.

Le champ générique `Duration` n'est plus utilisé par les étapes `Dialogue`. Les étapes d'assise et
de fondu ne sont pas skippables dans cette introduction.

## Test conseillé

Tester d'abord directement depuis Maison, puis depuis MainMenu. Pour accélérer les itérations,
réduire temporairement la durée du fondu dans l'étape `Révéler la scène depuis le noir`.

`Play Once` dépend du slot de sauvegarde actif. Un lancement direct de Maison sans sauvegarde active
utilise seulement une progression transitoire, réinitialisée à chaque nouvelle session de Play Mode.

Pour réinitialiser les tests :

- sélectionner la séquence puis cliquer `Reset Play Once Completion`;
- ou utiliser `Lit > Story Sequences > Reset All Sequence Completions`.

Hors Play Mode, l'outil propose de réinitialiser la séquence dans tous les slots existants.
