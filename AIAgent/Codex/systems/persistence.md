# Persistance

## Rôle

Sauvegarder les personnages et le monde, reconstruire les objets runtime et
transférer l’état complet aux clients qui rejoignent tardivement.

## Classes principales

Emplacement canonique : `Assets/Persistence/Save/`.

- `CharacterStateStore` : JSON personnages, roster, inventaires et compatibilité.
  Il persiste aussi les PV restants des items defensifs combat entames par
  personnage.
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

Les sauvegardes personnage version 7 stockent
`CharacterSaveEntry.combatDefenseItemHitPoints` comme des piles `itemId` + PV
restants + quantite. Seuls les items encore portes et entames sont sauvegardes;
les items pleins sont deduits du nombre total porte.

`CharacterStateStore.SuppressNextAutomaticSave` permet d'ignorer une seule
sauvegarde automatique `OnDisable`/`OnApplicationQuit`. Le combat l'utilise
avant un retour `MainMenu` ou un rechargement checkpoint apres defaite, afin de
ne pas ecraser la sauvegarde active avec l'etat de mort juste avant le
chargement.

`CharacterStateStore.CaptureRuntimeState` / `RestoreRuntimeState` exposent aussi
une restauration en memoire, sans ecriture disque ni screenshot. Le retry de
combat s'en sert pour remettre inventaires, items combat, PV de bouclier,
flamme, Munin et donnees lisibles a leur etat pre-combat, pendant que le monde
persistant est restaure via un `WorldSnapshot` en memoire. Cette capture runtime
de retry peut conserver les issues de validation sans les log en erreurs console,
pour ne pas polluer l'entree combat avec des providers de scene incomplets.

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
- Les PV de bouclier sont propres au personnage. Ne pas les placer dans l'asset
  `Item`, sinon tous les personnages partageraient la meme usure.
