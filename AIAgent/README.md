# AIAgent

AIAgent remplace `AIStudio_Legacy`.

Ce dossier ne contient plus d'application Python ni de pipeline LLM local. Il sert
a stocker les regles de prompt, le contexte projet utile a Codex et les travaux
conserves sans consommer inutilement du contexte.

## Structure

- `Codex/AGENTS.md` : regles de travail a lire au debut de chaque tache.
- `Codex/current_work.md` : etat actuel du projet, a garder court.
- `Codex/systems/` : documentation durable par systeme. Lire uniquement les
  fiches strictement pertinentes a la tache.
- `prompts/codex_task.md` : modele de prompt economique a reutiliser.
- `work/` : espace de stockage pour les travaux, notes et sorties utiles.

Les chemins `Codex/...` utilises dans les prompts sont relatifs au dossier
`AIAgent`.

## Principes

- Garder les prompts courts et centres sur une seule tache.
- Ne charger que le contexte indispensable.
- Capitaliser les connaissances durables dans `Codex/systems/`.
- Garder `Codex/current_work.md` temporaire et lisible.
- Ne pas stocker de secrets, caches, environnements virtuels ou sorties Unity
  generees dans ce dossier.
