# Squad et personnages

## Rôle

Maintenir le roster, les instances de personnages, le personnage contrôlé, les
groupes, le follow et les données runtime de chaque membre.

## Classes principales

- `SquadManager` : roster, spawn, sélection, groupes, verrouillage global.
- `SquadCharacterController` : façade gameplay du personnage, inventaire, santé,
  mouvement, interactions, assise et crochetage.
- `SquadAIManager` / `SquadFollowerAgent` : formation et déplacement des followers.
- `CharacterData` : données d’auteur; clonées au runtime par `SquadManager`.
- `LocalPlayerContext` : référence canonique du personnage local.

## Flux principaux

- Solo : `SquadManager` spawn le roster et choisit `currentCharacter`.
- Multijoueur : `NetcodePlayerSpawner` fournit les instances; `SquadManager`
  rafraîchit sa liste à partir du réseau.
- Les changements de contrôle mettent à jour `LocalPlayerContext`.
- Les consommateurs locaux (caméra, environnement, narration) écoutent
  `LocalCharacterChanged`.
- `SquadAIManager` pilote les membres groupés et suspend le follow pendant une
  `StorySequence`.
- Munin reste enfant logique du personnage pour les systèmes qui le résolvent
  via le character root, mais son suivi visuel est piloté en position monde par
  `MuninController`. L'avance de mouvement utilise le delta de position du
  Transform si la vitesse Rigidbody du personnage ne reflète pas le déplacement
  réel UCC.

## Pièges observés

- Ne jamais modifier directement les assets `CharacterData`; utiliser
  `GetRuntimeCharacter`.
- `SquadManager.SetInputLocked` est compté : chaque verrou doit être libéré.
- Le personnage local n’est pas toujours `SquadManager.currentCharacter` en
  multijoueur; utiliser `LocalPlayerContext`.
- `SquadCharacterController` est réparti sur plusieurs fichiers `partial`.
