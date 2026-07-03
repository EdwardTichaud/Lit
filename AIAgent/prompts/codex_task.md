# Prompt Codex minimal

Lis d'abord :

`Codex/AGENTS.md`
`Codex/current_work.md`

Puis identifie dans `Codex/systems/` uniquement la documentation strictement
pertinente. Ne lis pas le reste.

Ensuite traite la tache suivante :

## Regles

- Reste strictement centre sur la tache demandee.
- Considere le contexte comme une ressource limitee.
- Lis uniquement les fichiers necessaires.
- Applique un patch minimal.
- Reutilise autant que possible les systemes existants.
- Ne fais pas de refactor global.
- Ne modifie pas des scenes, prefabs, ScriptableObjects ou autres assets Unity
  sans necessite claire.
- Si tu crees un nouveau systeme ou une nouvelle fonctionnalite, cree un dossier
  dedie dans l'emplacement le plus pertinent.
- N'elargis pas le perimetre si une amelioration connexe te vient a l'esprit.
- Si tu identifies d'autres problemes, signale-les a la fin sans les corriger.
- Si la tache s'avere beaucoup plus large que prevu, arrete-toi et propose un
  plan avant de continuer.

## Documentation

- Si la tache modifie durablement un systeme, son fonctionnement, son
  architecture ou le game design, mets a jour uniquement la documentation
  concernee dans `Codex/systems/`.
- Mets egalement a jour `Codex/current_work.md` pour refleter l'etat actuel du
  projet.
- Ne modifie pas les autres documents si ce n'est pas necessaire.

## Avant modification

- Identifie brievement les fichiers concernes.
- Resume l'approche ou la cause probable du probleme.

## Apres modification

- Resume les changements effectues.
- Indique les fichiers de documentation mis a jour, ou explique pourquoi aucune
  mise a jour n'etait necessaire.
- Liste les risques eventuels.
- Liste les tests Unity a effectuer.

## Tache

[TACHE]
