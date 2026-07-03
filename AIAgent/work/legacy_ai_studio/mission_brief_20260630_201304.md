# Mission Codex

## Demande utilisateur

Identifier un éventuel systeme existant d'affichage/masquage des renderer lorsque la camera ne les affiches pas. Si un systeme existe déjà, le supprimer.
Créer un tout nouveau systeme qui permer d'optimiser au maximum les renderer/particlesSystem/systemes interactifs,etc... afin d'éviter les chutes de FPS; Faire quelque chose de professionnel, qui s'adapte vite et qui est facile à configurer.

## Messages utilisateur

- Identifier un éventuel systeme existant d'affichage/masquage des renderer lorsque la camera ne les affiches pas. Si un systeme existe déjà, le supprimer.
- Créer un tout nouveau systeme qui permer d'optimiser au maximum les renderer/particlesSystem/systemes interactifs,etc... afin d'éviter les chutes de FPS; Faire quelque chose de professionnel, qui s'adapte vite et qui est facile à configurer.

## Documentation selectionnee

- `architecture.md` (score 38)
- `systems/input_camera_ucc.md` (score 30)
- `systems/netcode.md` (score 28)
- `systems/combat.md` (score 20)
- `systems/surfaces_audio.md` (score 18)
- `systems/squad_characters.md` (score 16)

## Fichiers Unity probables

- `Assets\Legacy\BattleManager_SymphonieImport\Scripts\Scripts\MonoBehavioursUsed\BattleTransitionManager.cs` (score 126)
- `Assets\Legacy\BattleManager_SymphonieImport\Scripts\Scripts\MonoBehavioursUsed\NewBattleManager.cs` (score 122)
- `Assets\Legacy\BattleManager_SymphonieImport\Scripts\Scripts\MonoBehavioursUsed\CharacterUnit.cs` (score 116)
- `Assets\Scripts\KnowledgeManager.cs` (score 110)
- `Assets\Legacy\BattleManager_SymphonieImport\Scripts\Scripts\MonoBehavioursUsed\TimelineManager.cs` (score 110)
- `Assets\Legacy\BattleManager_SymphonieImport\Scripts\Scripts\MonoBehavioursUsed\NewBattleManager.Turns.cs` (score 108)
- `Assets\Legacy\BattleManager_SymphonieImport\Scripts\Scripts\MonoBehavioursUsed\BattleCameraManager.cs` (score 106)
- `Assets\Legacy\BattleManager_SymphonieImport\Scripts\Scripts\MonoBehavioursUsed\RhythmQTEManager.cs` (score 104)
- `Assets\Legacy\BattleManager_SymphonieImport\Scripts\Scripts\MonoBehavioursUsed\CinemachineBlendSwitcher.cs` (score 104)
- `Assets\Legacy\CameraSystem_Legacy\Scripts\CameraController.cs` (score 102)
- `Assets\Legacy\BattleManager_SymphonieImport\Scripts\Scripts\MonoBehavioursUsed\BattleCameraDamageFilter.cs` (score 102)
- `Assets\Combat\CombatSessionManager.cs` (score 98)
- `Assets\Legacy\BattleManager_SymphonieImport\Scripts\Scripts\MonoBehavioursUsed\InputsManager.cs` (score 96)
- `Assets\Camera\VisibilityOptimization\CameraVisibilityProtection.cs` (score 94)
- `Assets\UI\MainMenu\MainMenuController.cs` (score 92)

## Diagnostic API

- architect_mission | modele: gpt-5.4 | input: 5668 | output: 1924 | cout estime: $0.086060 | duree: 24.3 s

## Prompt Codex

## Analyse courte

- Objectif compris
- Systemes concernes
  - optimisation/visibilite camera des `Renderer` / `ParticleSystem` / composants interactifs
  - camera et contexte joueur local (`LocalPlayerContext`)
  - potentiellement netcode si des objets reseau sont actives/desactives
  - recherche/suppression d’un systeme existant probable dans `Assets/Camera/VisibilityOptimization/CameraVisibilityProtection.cs`
- Risques principaux
  - casser des presentations locales ou des objets gameplay si on desactive trop agressivement des GameObjects
  - introduire un second chemin de decision camera/local player au lieu d’utiliser les systemes existants
  - effets de bord multijoueur si un systeme local masque un objet qui doit rester simule/replique
  - toucher des prefabs/scenes inutilement alors qu’un systeme configurable par composant suffit
  - conflit avec un eventuel systeme legacy de visibilite deja en place

## Prompt Codex

```text
Objectif
Identifier s’il existe deja un systeme d’affichage/masquage des renderers hors camera. Si oui, le supprimer proprement.
Puis implementer un nouveau systeme professionnel, configurable et minimal pour optimiser localement ce qui est hors vue camera : renderers, particle systems, et certains systemes interactifs/updates couteux, afin de reduire les chutes de FPS sans casser le gameplay.

Contexte
Projet Unity 6 avec Opsive UCC, NGO et HDRP.
Le systeme doit respecter le contexte joueur/camera existant. Ne jamais contourner LocalPlayerContext. Ne pas creer un second chemin d’input. En multijoueur, garder l’autorite serveur : l’optimisation doit etre une optimisation locale de presentation/processing, pas une mutation gameplay autoritaire cote client.
Le besoin utilisateur demande aussi d’identifier et supprimer un systeme existant s’il y en a un.

IMPORTANT
1. Avant toute modification, lire `AIStudio/docs/AGENTS.md`.
2. Lire ensuite uniquement ces documents :
   - `AIStudio/docs/architecture.md`
   - `AIStudio/docs/systems/input_camera_ucc.md`
   - `AIStudio/docs/systems/netcode.md`
   - `AIStudio/docs/systems/combat.md`
   - `AIStudio/docs/systems/surfaces_audio.md`
   - `AIStudio/docs/systems/squad_characters.md`

Fichiers Unity probables a lire en priorite
- `Assets/Camera/VisibilityOptimization/CameraVisibilityProtection.cs`
- `Assets/Combat/CombatSessionManager.cs`
- `Assets/Legacy/CameraSystem_Legacy/Scripts/CameraController.cs`
- si references trouvees par recherche, seulement les fichiers qui consomment le systeme de visibilite existant

Mots-cles a rechercher
- `CameraVisibilityProtection`
- `OnBecameVisible`
- `OnBecameInvisible`
- `isVisible`
- `CullingGroup`
- `Renderer.enabled`
- `ParticleSystem`
- `LocalPlayerContext`
- `Camera.main`
- `CameraController`
- `VisibilityOptimization`
- `SetActive(`
- `enabled = false`
- `disable when not visible`
- `frustum`

Travail demande

Phase 1 — Audit minimal de l’existant
- Identifier tout systeme deja present qui masque/affiche des renderers ou des objets selon la camera.
- Verifier en priorite `Assets/Camera/VisibilityOptimization/CameraVisibilityProtection.cs`.
- Trouver ses points d’entree/usages/references.
- Determiner s’il est vraiment actif en runtime ou obsolete.
- Si un systeme existant fait deja ce role, le supprimer proprement :
  - enlever son code
  - nettoyer seulement les references code evidentes
  - ne pas modifier scenes/prefabs/ScriptableObjects sauf necessite demontree
  - si suppression trop risquee sans toucher aux scenes/prefabs, preferer le rendre inutilise/obsolete de maniere minimale et documenter clairement

Phase 2 — Nouveau systeme
Concevoir et implementer un systeme simple, pro, rapide a adopter et facile a configurer, avec patch minimal.
Objectif prioritaire : optimisation locale de presentation, pas framework geant.

Strategie proposee
Implementer un systeme par composant avec une logique centrale simple et sure :
1. Un composant de configuration par objet, par exemple type `VisibilityOptimizedObject` / `VisibilityOptimizationTarget`.
2. Une evaluation de visibilite locale basee sur la camera gameplay active / camera liee au joueur local.
3. Des actions configurables par cible :
   - activer/desactiver une liste de `Renderer`
   - stop/play ou pause/resume de `ParticleSystem`
   - activer/desactiver une liste de `Behaviour` couteux non critiques visuellement
4. Garder une separation stricte entre :
   - optimisation de presentation locale sure
   - gameplay/reseau : ne pas desactiver arbitrairement des composants qui portent de la logique autoritaire
5. Fallback prudent :
   - si la camera locale active n’est pas resolue de facon sure, ne rien casser
6. API/inspecteur facile a configurer :
   - listes explicites de composants a piloter
   - options claires de comportement `WhenVisible` / `WhenNotVisible`
   - eventuellement mode auto pour recuperer les `Renderer` enfants si c’est peu risqué
7. Eviter de desactiver tout le GameObject racine par defaut.

Contraintes de conception
- Favoriser un patch minimal, lisible et sur.
- Ne pas refactorer un systeme voisin sans necessite demontree.
- Ne pas modifier scenes, prefabs ou ScriptableObjects sans necessite demontree.
- Ne jamais contourner `LocalPlayerContext`.
- Ne jamais creer un second chemin d’input.
- En multijoueur, respecter l’autorite serveur et tester host/client.
- Verifier les risques Unity classiques :
  - statics
  - singletons
  - events non desabonnes
  - coroutine double
  - ordre `Awake` / `OnEnable` / `Start`
  - Domain Reload desactive
- Preserver les changements Git existants.

Implementation attendue
- Si `CameraVisibilityProtection.cs` est l’ancien systeme, le remplacer/supprimer proprement.
- Creer le nouveau systeme dans un emplacement coherent (probablement sous `Assets/Camera/VisibilityOptimization/`).
- Preferer une implementation locale et configurable plutot qu’un systeme global intrusif.
- Si utile, fournir un composant helper pour resoudre la camera gameplay locale de facon compatible avec l’architecture existante, sans bypass de `LocalPlayerContext`.
- Documenter dans le code les limites importantes :
  - ne pas brancher de logique reseau/autoritaire dans les behaviours desactives localement
  - ne pas desactiver des colliders/NetworkObject/composants de simulation critiques sans validation

Ce qu’il faut livrer
1. Résumé d’audit :
   - systeme existant trouve ou non
   - ou il est branche
   - decision de suppression/remplacement
2. Patch code minimal
3. Explication concise de l’architecture du nouveau systeme
4. Liste des risques restants / hypothèses

Tests a effectuer
Donner des tests manuels precis, sans promettre de les avoir executes si ce n’est pas possible :
- Solo :
  - un objet avec renderer seul hors champ => rendu coupe puis restaure quand il revient dans le champ
  - un objet avec particules => comportement correct hors champ et retour sans etat casse
  - un objet interactif avec behaviours explicitement configures => suspension/reprise correcte sans casser interaction
- Camera/UCC :
  - verifier compatibilite avec la camera gameplay active et les changements de cible via `LocalPlayerContext`
- Multijoueur host/client :
  - verifier que l’optimisation reste locale visuelle
  - verifier qu’aucun objet reseau ou logique serveur n’est desactive de facon gameplay-breaking
- Cas limites :
  - objet spawn/despawn runtime
  - activation/desactivation repetee
  - changement de scene
  - Domain Reload desactive

Risques a surveiller
- `Renderer.isVisible` peut dependre de n’importe quelle camera ; si tu l’utilises, justifie pourquoi c’est acceptable ou evite-le au profit d’un calcul cible plus robuste.
- Desactiver un `Behaviour` arbitraire peut casser de la logique ; rendre cela opt-in, jamais automatique pour tout.
- Les `ParticleSystem` peuvent perdre un etat si stop mal gere ; choisir une strategie prudente et configurable.
- Attention aux cameras de combat/presentation si elles existent en parallele.

Consignes finales
- Patch minimal uniquement.
- Preserver les changements Git existants.
- N’ouvre pas tout le projet : pars des fichiers probables et mots-cles fournis.
- Mettre a jour la documentation seulement si une connaissance durable est creee ou si une documentation devient inexacte.
```
