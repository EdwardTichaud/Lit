# Travail en cours

## Objectif actuel

Utiliser `AIAgent` comme espace documentaire leger pour preparer des prompts
Codex efficaces et economes en tokens.

## Etat actuel

`AIStudio_Legacy` a ete supprime. `AIAgent` contient un dossier `Codex/` avec
les regles de travail, l'etat courant et les fiches systeme utiles. Les anciennes
fiches systeme Markdown ont ete migrees depuis `AIStudio_Legacy/docs/systems/`.

Le nouveau flux ne depend plus d'une application Python, d'un environnement
virtuel ou d'un appel LLM local. Les prompts sont prepares manuellement depuis
`AIAgent/prompts/codex_task.md`.

Le combat tour par tour pilote maintenant la camera locale par phase via
`CombatCameraPresentationController`; pendant le combat, Opsive est suspendu
comme driver camera spatial puis restaure a la sortie.
Le ralenti de reaction ennemie entre rapidement, reste actif pendant l'action
ennemie, et evite une vitesse trop basse pour conserver un rendu dynamique.
Les profils de temps combat synchronisent aussi les mouvements scriptes de
presentation et declenchent un court hit-stop local aux impacts.

## Contraintes

- Garder `Codex/AGENTS.md` et `Codex/current_work.md` courts.
- Lire uniquement les fiches pertinentes de `Codex/systems/`.
- Ne pas recreer l'ancien pipeline AIStudio sans demande explicite.
- Ne pas stocker de secrets, caches ou environnements virtuels dans `AIAgent`.

## Prochaine utilisation

Pour une nouvelle tache, partir du modele `prompts/codex_task.md`, remplacer
`[TACHE]`, puis fournir le prompt a Codex depuis le contexte `AIAgent`.
