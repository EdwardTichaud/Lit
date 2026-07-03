# Persistance

## Rôle

Sauvegarder les personnages et le monde, reconstruire les objets runtime et
transférer l’état complet aux clients qui rejoignent tardivement.

## Classes principales

Emplacement canonique : `Assets/Persistence/Save/`.

- `CharacterStateStore` : JSON personnages, roster, inventaires et compatibilité.
- `WorldSaveAdapter` : lecture/écriture du snapshot binaire du slot actif.
- `WorldStateManager` : capture et application ordonnée d’un `WorldSnapshot`.
- `PersistentNetworkObject` : identité stable et agrégation des providers.
- `IPersistentStateProvider` : payload spécifique d’un composant.
- `NetworkObjectRegistry` : résolution par `persistentId`.
- `SpawnManager` : IDs et reconstruction des objets runtime.
- `JoinSyncSystem` : transfert de snapshot et blocage du late join.

## Flux principaux

Sauvegarde :

`CharacterStateStore.Save` → JSON → `WorldSaveAdapter.SaveWorldSnapshot` →
`WorldStateManager.CaptureSnapshot` → providers → fichier binaire.

Chargement :

lecture du JSON → restauration des données de squad → lecture du snapshot →
résolution des objets de scène → spawn/destruction runtime → transforms →
providers → références finales.

Late join :

client demande le snapshot → serveur sérialise et segmente → client reconstruit
le monde → restaure son personnage local → envoie `client_ready`.

## Pièges observés

- `persistentId`, `runtimePrefabId` et `ProviderId` sont des contrats de migration.
- Une collision d’ID invalide la résolution; ne pas générer d’IDs ad hoc.
- L’ordre des phases d’application est intentionnel.
- Le snapshot doit correspondre à la scène active.
- Les clients sont bloqués par `JoinSyncSystem.IsGameplayBlocked` pendant la sync.
- Les snapshots Building legacy sont conservés mais ignorés si le système est désactivé.
- `EditorAutoSave` sauvegarde scènes/assets côté Editor; la persistance runtime
  reste isolée dans `Application.persistentDataPath` et ses écritures/lectures
  runtime doivent être ignorées hors Play Mode.
