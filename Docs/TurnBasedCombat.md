# Systeme de combat tour par tour

## Objectif

Le combat est une contrainte d'exploration, pas un mode de jeu principal. Il reste solo, sans JcJ, et isole uniquement le joueur aggro dans une arene runtime.

## Points d'integration

- `Enemy` est le ScriptableObject de donnees d'un ennemi placable : prefab de monde, `CharacterData` optionnel, PV, degats et ennemis additionnels.
- `ItemSceneMarker` peut maintenant etre utilise en mode `Enemy` pour placer et baker un `Enemy` en scene, en ajoutant `EnemyInfo`, `CombatHealth` et `CombatAggroEnemy` sur l'instance.
- `CombatAggroEnemy` demarre un combat quand un joueur controle entre dans son trigger d'aggro.
- `CombatSessionManager` gere les sessions, les tours, le timer de 30 secondes, les PV ennemis, le retour du joueur et les RPC Netcode.
- Les PV joueur reutilisent `SquadCharacterController.CurrentHp/MaxHp/ApplyDamage`.
- L'inventaire existant reste utilise via `InventoryPanelController` et `NetworkInventory`. Un item ne peut etre applique en combat que pendant le tour joueur ; si l'usage reussit, `CombatSessionManager.NotifyInventoryItemUsed` termine le tour.
- `IustiaIdolPrayer` s'accroche aux `Item_Idole de Iustia` et signale les joueurs en priere au manager.
- `CharacterInteractionDetection` priorise `IustiaIdolPrayer` avant `InteractableItem` pour eviter qu'une idole soit traitee comme un loot quand on veut prier.
- `NetcodePrefabRegistry` ajoute `CombatSessionManager` au `WorldInteractionService` networke.

## Deroulement

1. Un `CombatAggroEnemy` aggro un joueur controle.
2. Le manager cree une arene runtime loin du monde d'exploration et y teleporte uniquement ce joueur.
3. Le mouvement du joueur est suspendu via `PushScriptedMovementSuppression`, cote serveur et cote client local.
4. Le combat alterne strictement ennemi puis joueur.
5. Chaque tour a un timer de 30 secondes. Le tour ennemi execute une attaque automatique apres un court delai, avec timer de secours.
6. Le joueur peut attaquer, passer, ou ouvrir son inventaire et utiliser un item.
7. Victoire si tous les ennemis runtime tombent a 0 PV. Defaite si le joueur tombe a 0 PV.
8. Le manager restaure la position d'exploration, libere la suppression de mouvement, detruit l'arene et notifie le HUD.

## Idoles de Iustia

Chaque joueur hors combat qui prie pres d'une idole ajoute 20% de reduction des degats recus par un joueur en combat. Le calcul est dynamique au moment de l'attaque ennemie.

Un cap configurable `maxPrayerDamageReduction` est defini dans `CombatSessionManager` a `0.8` par defaut. Cela laisse toujours au moins 20% des degats bruts passer, meme si le nombre de joueurs augmente plus tard.

## Limites actuelles

- L'arene est une instance runtime simple dans la scene courante, pas une scene additive dediee.
- Les ennemis sont des donnees runtime issues de `CombatAggroEnemy`; il n'y a pas encore d'animation d'ennemi dans l'arene.
- Le HUD est genere automatiquement si aucune UI dediee n'existe.
- La victoire peut desactiver l'objet ennemi source cote serveur, mais les prefabs non networkes devront etre ajustes si un visuel client permanent doit disparaitre partout.

## Test manuel recommande

1. Creer un asset `Enemy` via `Create > Scriptable Objects > Enemy`, renseigner son prefab de monde et ses stats.
2. Creer un marker via `Lit > Enemy > Scene Marker`, assigner l'asset `Enemy`, puis utiliser `Bake Replace Marker`.
3. Lancer une partie host ou solo, entrer dans le trigger et verifier la teleportation en arene.
4. Verifier que l'ennemi joue en premier, puis que le joueur peut attaquer avec Interagir.
5. Pendant le tour joueur, ouvrir l'inventaire, utiliser un item utilisable, et verifier que le tour passe a l'ennemi.
6. Essayer d'utiliser un item pendant le tour ennemi et verifier que l'usage est refuse.
7. Attendre 30 secondes pendant le tour joueur et verifier le passage automatique.
8. En multijoueur, faire prier un autre joueur sur une `Item_Idole de Iustia` et verifier la reduction affichee et appliquee.
9. Verifier qu'un autre joueur reste dans le monde et ne rejoint pas l'arene.
10. Terminer par victoire et defaite pour confirmer le retour a la position d'exploration.
