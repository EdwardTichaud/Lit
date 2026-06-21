# Règles de travail Codex

## Objectif

Préserver l'architecture du projet, minimiser les régressions et transformer les découvertes importantes en documentation durable afin que les futures tâches bénéficient du travail déjà effectué.

---

# Avant toute modification

1. Lire `Codex/architecture.md`.

2. Identifier puis lire uniquement les fiches pertinentes dans `Codex/systems/`.

3. Lire `Codex/current_work.md`.

4. Lire `Codex/known_bugs.md`.

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

* `Assets/Scripts/Menu/SaveSessionManager.cs`
* `Assets/Scripts/GameplayRuntimeReset.cs`
* `Assets/Scripts/CharacterStateStore.cs`

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

* `Assets/Scripts/Netcode/Persistence/WorldStateManager.cs`
* `Assets/Scripts/Netcode/Persistence/PersistentNetworkObject.cs`
* `Assets/Scripts/Netcode/Persistence/JoinSyncSystem.cs`

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
* Tester le flux nominal.
* Tester le teardown et le rechargement de scène.
* Pour les systèmes partagés : vérifier solo puis host/client.
* Pour la persistance : vérifier nouvelle partie, chargement et late join.
* Pour les bugs runtime Unity : préciser les tests manuels à effectuer.
* Documenter dans `known_bugs.md` tout défaut confirmé non corrigé.

---

# Format de réponse attendu

## Avant modification

* Cause probable.
* Fichiers concernés.
* Plan minimal de correction.
* Niveau de confiance (faible / moyen / élevé).

## Après modification

* Résumé du patch.
* Risques identifiés.
* Vérifications effectuées.
* Tests manuels recommandés.
* Documentation mise à jour et justification.
