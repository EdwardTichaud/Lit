# Munin

Munin est l'orbe du joueur. Il reste enfant du personnage dans la hierarchie Unity, mais son deplacement visuel est pilote en world space par `MuninIndependentFollower` pour eviter qu'il tourne rigidement avec le joueur.

## Configuration

Sur l'objet `Munin`, garder `MuninController` pour les charges, reactions, VFX de vie et trajets vers torches/braseros. Ajouter `MuninIndependentFollower` pour le suivi autour du joueur.

Le prefab `Player_Model_Lucian` utilise maintenant :

- `MuninIndependentFollower` active.
- `FollowTarget` conserve sur Munin mais desactive, uniquement pour garder les anciens reglages consultables.
- `targetPlayer` assigne au root du personnage.
- `baseOffset` en world space, avec `ignoreTargetRotation`, `useWorldSpaceOffset` et `keepWorldRotation` actives.

Si Munin est instancie ailleurs, `targetPlayer` peut rester vide : le script cherchera d'abord un `SquadCharacterController` parent, puis le parent direct.

## Reglages importants

- `baseOffset` place Munin autour du joueur sans dependre directement de la rotation du joueur.
- `followSmoothTime` controle le delai de suivi principal. Monter la valeur rend Munin plus flottant.
- `maxDistanceFromTarget` evite que Munin reste trop loin apres un deplacement rapide.
- `minDistanceFromTarget` evite que Munin traverse le centre du joueur.
- `driftAmplitude`, `driftFrequency`, `driftSpeed` et `driftAxisMultiplier` reglent la derive organique via Perlin Noise.
- `spasmAmplitude` doit rester faible. Les spasmes servent a donner de la vie, pas a creer du jitter.
- `spasmCooldownMin` et `spasmCooldownMax` reglent la rarete des spasmes.

## Interactions

`MuninController.MoveToWorldAndBack` suspend automatiquement `MuninIndependentFollower` pendant les trajets vers une torche ou un brasero, puis le reactive au retour. Si un autre systeme doit piloter Munin temporairement, appeler `BeginExternalMotion(...)`, puis `EndExternalMotion()` quand le controle peut revenir au follow.

Etats utiles :

- `Following` : le follow independant pilote Munin.
- `MovingToTarget` : un autre script deplace Munin vers une cible.
- `Returning` : un autre script ramene Munin.
- `Disabled` : le follow reste coupe.
