# Input, UCC et caméra

## Rôle

Centraliser les actions locales, les transmettre au bon personnage et adapter
la façade gameplay Lit à Opsive UCC et à sa caméra.

## Classes principales

- `PlayerInputs.inputactions` : source des bindings.
- `LocalPlayerInput` : capture Input System et singleton local.
- `LocalInputRouter` : événements, valeurs continues, debounce et consommation.
- `InputFocusStack` : priorité UI/cinématique sur le gameplay.
- `LitOpsiveLocomotionBridge` : mouvement, saut, vol, franchissement d'obstacles
  bas et verrous UCC.
- `LitUccCameraCharacterBinder` : liaison de la caméra au `LocalPlayerContext`.
- `NetworkCharacterInput` : relais des commandes du propriétaire vers le serveur.

## Flux principaux

`PlayerInputs` → `LocalPlayerInput` → `LocalInputRouter`.

Le flux se divise ensuite :

- solo vers `SquadManager` / `SquadCharacterController`;
- réseau vers `NetworkCharacterInput`;
- caméra vers les abonnés dédiés.

`SquadCharacterController` convertit l’input en espace monde, puis
`LitOpsiveLocomotionBridge` pilote UCC.

Le franchissement automatique d'obstacles reste dans `LitOpsiveLocomotionBridge` :
les obstacles sous le seuil d'ignorance ne déclenchent rien, les obstacles bas
franchissables lancent un court traversal scripté avec trigger Animator
configurable, et les obstacles plus hauts restent bloquants.

## Pièges observés

- Modifier l’asset `.inputactions`, jamais le wrapper C# généré.
- Un input consommable (`Interact`, `TriggerMunin`) ne doit être traité qu’une fois.
- `InputFocusStack` est une pile : seul le propriétaire au sommet détient le focus.
- Le debounce utilise `Time.unscaledTime`; son état statique doit être remis à zéro
  à chaque session Play.
- Les cinématiques utilisent des verrous externes UCC; toujours restaurer le
  contrôle lors d’un abort ou `OnDisable`.
