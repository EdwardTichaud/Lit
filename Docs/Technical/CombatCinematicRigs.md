# Rigs de cinematiques de combat

Les Light Skills et Counter Skills jouent maintenant des prefabs `CombatCinematicRig` pooles. Un rig contient son propre `PlayableDirector`, `SignalReceiver`, `LitTimelineCinemachineBridge` et ses `CinemachineCamera`.

## Installation initiale

1. Dans Unity, lancer `Lit > Combat > Cinematics > Build Combat Cinematic Rigs`.
2. Lancer `Lit > Combat > Cinematics > Validate`.
3. Tester Devastation de lumiere puis Riposte temporelle depuis le combat.

Le service est ajoute sur `BattleManager` et les prefabs sont assignes aux deux ScriptableObjects. Les anciennes references de camera restent sans effet tant qu'un rig est assigne ; elles servent uniquement de compatibilite si un prefab de rig est retire.

## Auteur de plans

Chaque `CinemachineShot` de la Timeline utilise une cle `exposedName`. Le `CombatCinematicRig` associe explicitement cette cle a une de ses `CinemachineCamera`. La piste Cinemachine realise les coupes et blends ; les scripts de rig peuvent seulement mettre a jour la position et le look-at de leurs propres cameras.

Un rig ne doit jamais etre parente au joueur. Au lancement, il est pose a la position du joueur et oriente vers la cible ; les cameras suivent ensuite les anchors runtime du contexte. Une seule cinematique de combat peut etre active a la fois.
