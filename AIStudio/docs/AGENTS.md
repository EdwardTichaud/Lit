# Règles de travail Codex

## Objectif

Préserver l'architecture du projet, minimiser les régressions et transformer les découvertes importantes en documentation durable afin que les futures tâches bénéficient du travail déjà effectué.

---

# Avant toute modification

1. Lire `AIStudio/docs/architecture.md`.

2. Identifier puis lire uniquement les fiches pertinentes dans `AIStudio/docs/systems/`.

3. Lire `AIStudio/docs/current_work.md`.

4. Lire `AIStudio/docs/known_bugs.md`.

5. Vérifier `git status` et préserver tous les changements existants.

6. Identifier le flux complet avant de modifier un composant isolé.

7. Confirmer si le comportement attendu concerne :

   * solo ;
   * multijoueur ;
   * ou les deux.

8. Pour tout bug Unity, vérifier en priorité :

   * état statique (`static`)
   * singleton
   * événement (`event`) non désabonné
   * ScriptableObject utilisé comme état runtime
   * coroutine
   * ordre `Awake` / `OnEnable` / `Start`
   * Play Mode avec Domain Reload désactivé

9. Expliquer la cause probable avant toute correction.

10. Rechercher la solution la plus simple avant toute modification importante.

---

# Fichiers à consulter en priorité

## Session et sauvegardes

* `Assets/Persistence/Save/Session/SaveSessionManager.cs`
* `Assets/Persistence/Save/Session/GameplayRuntimeReset.cs`
* `Assets/Persistence/Save/Character/CharacterStateStore.cs`

## Squad et personnage

* `Assets/Scripts/SquadManager.cs`
* `Assets/Scripts/SquadCharacterController.cs`
* tous les fichiers `partial` associés

## Input et contrôle local

* `Assets/Scripts/Netcode/LocalPlayerInput.cs`
* `Assets/Scripts/Netcode/LocalInputRouter.cs`
* `Assets/Scripts/Netcode/LocalPlayerContext.cs`

## UCC et caméra

* `Assets/Scripts/OpsiveIntegration/LitOpsiveLocomotionBridge.cs`
* `Assets/Scripts/OpsiveIntegration/LitUccCameraCharacterBinder.cs`

## Interactions

* `Assets/Scripts/CharacterInteractionDetection.cs`
* `Assets/Scripts/Movement/SquadCharacterController.Interactions.cs`
* `Assets/Scripts/RuntimeOutlineSelectionManager.cs`

## Réseau

* `Assets/Scripts/Netcode/NetcodeBootstrap.cs`
* `Assets/Scripts/Netcode/NetcodePlayerSpawner.cs`
* `Assets/Scripts/Netcode/NetworkCharacterInput.cs`
* `Assets/Scripts/Netcode/WorldInteractionService.cs`

## Persistance monde

* `Assets/Persistence/Save/World/WorldStateManager.cs`
* `Assets/Persistence/Save/World/PersistentNetworkObject.cs`
* `Assets/Persistence/Save/World/JoinSyncSystem.cs`

## Narration

* `Assets/StorySequenceAssets/README.md`
* `Assets/StorySequenceAssets/Scripts/StorySequenceRunner.cs`
* `Assets/Scripts/KnowledgeManager.cs`

---

# Règles de modification

* Privilégier les patches minimaux.
* Ne pas refactorer un système voisin sans nécessité démontrée.
* Ne pas modifier les assets source (`CharacterData`, `Item`, etc.) à l'exécution.
* Utiliser les clones ou états runtime prévus.
* Ne jamais contourner `LocalPlayerContext`.
* Ne jamais créer un second chemin d'input.
* Utiliser exclusivement `LocalPlayerInput` et `LocalInputRouter`.
* Ne pas écrire directement un état réseau depuis un client lorsque le serveur est autoritaire.
* Préserver les identifiants stables de sauvegarde et réseau.
* Ne pas supprimer le système Building legacy sans stratégie de migration.
* Pour les PlayerInputs, modifier `Assets/PlayerInputs.inputactions` et jamais `Assets/PlayerInputs.cs`.
* Tout état statique runtime doit fonctionner avec ou sans Domain Reload.
* Ne pas modifier scènes, prefabs ou ScriptableObjects sans nécessité démontrée.
* Ne pas introduire de dépendance cachée à l'ordre d'exécution Unity.

---

# Documentation et capitalisation

Le projet doit s'enrichir progressivement de ses découvertes.

Avant de terminer une tâche :

1. Identifier les connaissances durables découvertes pendant l'analyse ou la correction.
2. Identifier les documentations devenues inexactes.
3. Mettre à jour uniquement les documents concernés.
4. Expliquer quelles documentations ont été modifiées et pourquoi.

## Répartition de la documentation

### architecture.md

Y placer :

* décisions d'architecture durables ;
* contraintes globales du projet ;
* invariants importants.

### systems/*.md

Y placer :

* fonctionnement réel des systèmes ;
* dépendances importantes ;
* pièges techniques ;
* comportements non évidents ;
* enseignements issus de bugs complexes.

### current_work.md

Y placer uniquement :

* contexte de travail actuel ;
* objectifs en cours ;
* prochaines étapes ;
* hypothèses temporaires.

Ne pas utiliser ce fichier comme historique permanent.

### known_bugs.md

Y placer uniquement :

* bugs confirmés non corrigés ;
* limitations connues ;
* comportements anormaux reproduits.

## Ne jamais documenter

* l'historique des tâches ;
* les commits ;
* les modifications triviales ;
* les changements évidents directement visibles dans le code.

Documenter uniquement les informations susceptibles d'être utiles lors de futures tâches.

Toute correction ayant nécessité une analyse significative doit être considérée comme candidate à la documentation.

---

# Revue obligatoire après modification

1. Relire tous les fichiers modifiés.
2. Chercher les régressions possibles.
3. Vérifier la cohérence avec l'architecture existante.
4. Vérifier qu'une solution plus simple n'a pas été ignorée.
5. Signaler explicitement les hypothèses non vérifiées.

## Checklist Unity

* Aucun event statique non désabonné.
* Aucun singleton conservant un état invalide.
* Aucun ScriptableObject utilisé comme stockage runtime involontaire.
* Aucun listener enregistré plusieurs fois.
* Aucune coroutine susceptible d'être lancée en double.
* Aucun champ `[SerializeField]` renommé ou supprimé sans analyse.
* Aucun problème potentiel lié à `Awake`, `OnEnable`, `Start`, `OnDisable` ou `OnDestroy`.
* Compatibilité Domain Reload activé et désactivé.

---

# Validation attendue

* Compiler les assemblages Runtime et Editor concernés.
* Corriger toute erreur de compilation introduite.
* Tester le flux concerné.

---

# Règles spécifiques AIStudio

Quand AIStudio travaille sur son propre dépôt :

1. La mission suit toujours ce pipeline :

   * Mission Manager
   * Mission Planner
   * Collecte automatique du contexte
   * LLM
   * Prompt Codex

2. AIStudio ne sert qu'à produire un prompt Codex.

3. Le LLM ne doit jamais être appelé tant que la collecte du contexte n’est pas terminée.

4. Le contexte construit est la seule entrée du LLM.

5. AIStudio ne doit jamais générer ni appliquer de patch.

6. AIStudio ne doit jamais modifier directement les fichiers Unity Lit.

7. Toute modification du projet doit être faite ensuite par Codex à partir du prompt généré.
