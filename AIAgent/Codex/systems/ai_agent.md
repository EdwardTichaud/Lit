# AIAgent

## Role

AIAgent est le dossier de preparation et de capitalisation pour le travail Codex
sur le projet Lit. Il remplace l'ancien `AIStudio_Legacy` par une structure
documentaire simple, sans application locale.

## Quand lire cette fiche

Lire cette fiche uniquement pour les taches qui modifient :

- les regles de prompt ;
- la structure du dossier `AIAgent` ;
- le workflow de documentation Codex ;
- la maniere de stocker les travaux realises.

## Structure attendue

- `Codex/AGENTS.md` contient les regles de comportement globales.
- `Codex/current_work.md` contient le contexte temporaire actuel.
- `Codex/systems/` contient les fiches systeme durables.
- `prompts/` contient les modeles de prompts reutilisables.
- `work/` contient les travaux et notes conserves.

## Invariants

- AIAgent ne contient pas de `.venv`, cache, secret ou dependance installee.
- AIAgent ne genere pas de patch automatiquement.
- AIAgent ne modifie pas Unity directement.
- Les prompts doivent favoriser la lecture minimale de contexte.
- Les anciennes fiches systeme utiles restent dans `Codex/systems/`.

## Mise a jour

Quand une regle de travail durable change, mettre a jour `Codex/AGENTS.md`.
Quand l'etat du projet change, mettre a jour `Codex/current_work.md`.
Quand le fonctionnement d'un systeme change, mettre a jour uniquement la fiche
pertinente dans `Codex/systems/`.
