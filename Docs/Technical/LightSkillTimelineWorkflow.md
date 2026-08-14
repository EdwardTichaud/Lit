# Atelier Timeline des Light Skills

`AnimationLab` est le plateau de travail des attaques chorégraphiées. Il n'est pas une scène de gameplay et ne doit pas être ajouté aux scènes de build.

## Parcours de travail

1. Une fois, lancer `Lit > Combat > Light Skills > Create Or Refresh Animation Lab` ; cela crée la scène et le prefab Furie.
2. Ouvrir `Assets/Scenes/Workshop/AnimationLab.unity` et sélectionner `LightSkill_1_Furie_AuthoringRig`, puis ouvrir la fenêtre Timeline.
3. Modifier les animations, les plans Cinemachine et les marqueurs de la Furie.
4. Lancer `Lit > Combat > Light Skills > Apply Preview Bindings`, puis `Validate Selected Authoring Rig`.
5. Tester ensuite la compétence dans le combat réel : le `LightSkillCombatController` applique les bindings runtime.
6. Ajouter un signal seulement lorsqu'une nouvelle action gameplay est nécessaire ; son nom suit `LightSkill_<SkillId>_<Action>`.

Les acteurs de preview sont toujours enfants d'un `*_Anchor` fixe. Placer l'Anchor dans le plateau ; laisser le modèle animé à la position locale `(0, 0, 0)`. Cela empêche les clips Generic qui animent la racine de déplacer l'acteur dans le monde à la première frame.

Pour une nouvelle attaque, lancer `Create New Authoring Rig From Furie Template`, renseigner son identifiant, puis commencer dans le prefab généré sous `Assets/CombatRealTime/LightSkills/Authored/`.

## Contrat de pistes

Les pistes requises sont `Player.Animator`, `Enemy.Animator`, `Cinemachine` et `Signals`.

- Les deux pistes Animation sont liées au joueur local et à l'ennemi verrouillé à l'exécution.
- `Cinemachine` reçoit la caméra de la compétence.
- `Signals` reçoit le `SignalReceiver` de la compétence.
- Les sons de cette Light Skill restent déclenchés par ses signaux et son `LightSkillSO`; ne pas ajouter de piste Audio runtime sans une cible explicitement gérée.

Les `StorySequence` et les `TimelineBindingProfile` restent réservés aux séquences narratives : ils ne font pas partie de ce flux.
