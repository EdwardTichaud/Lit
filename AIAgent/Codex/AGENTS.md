# Regles de travail Codex

## Demarrage de chaque tache

1. Lis d'abord `Codex/AGENTS.md`.
2. Lis ensuite `Codex/current_work.md`.
3. Identifie dans `Codex/systems/` uniquement la documentation strictement
   pertinente pour la tache.
4. Ne lis pas le reste.
5. Traite ensuite la tache demandee.

## Regles de contexte

- Reste strictement centre sur la tache demandee.
- Considere le contexte comme une ressource limitee.
- Lis uniquement les fichiers necessaires.
- Applique un patch minimal.
- Reutilise autant que possible les systemes existants.
- Ne fais pas de refactor global.
- N'elargis pas le perimetre si une amelioration connexe te vient a l'esprit.
- Si tu identifies d'autres problemes, signale-les a la fin sans les corriger.
- Si la tache s'avere beaucoup plus large que prevu, arrete-toi et propose un
  plan avant de continuer.

## Regles Unity

- Ne modifie pas des scenes, prefabs, ScriptableObjects ou autres assets Unity
  sans necessite claire.
- Si tu crees un nouveau systeme ou une nouvelle fonctionnalite, cree un dossier
  dedie dans l'emplacement le plus pertinent.
- Preserve les systemes existants et leurs invariants.
- Evite les dependances cachees a l'ordre d'execution Unity.
- Verifie les risques lies a `Awake`, `OnEnable`, `Start`, `OnDisable` et
  `OnDestroy` quand la tache touche le runtime.
- Prends en compte Domain Reload active et desactive quand un etat statique,
  singleton, event ou cache runtime est concerne.

## Documentation

- Si la tache modifie durablement un systeme, son fonctionnement, son
  architecture ou le game design, mets a jour uniquement la documentation
  concernee dans `Codex/systems/`.
- Mets egalement a jour `Codex/current_work.md` pour refleter l'etat actuel du
  projet.
- Ne modifie pas les autres documents si ce n'est pas necessaire.
- Ne documente pas les changements triviaux directement visibles dans le code.
- Ne transforme pas `Codex/current_work.md` en historique permanent.

## Avant modification

- Identifie brievement les fichiers concernes.
- Resume l'approche ou la cause probable du probleme.
- Verifie l'etat Git et preserve les changements existants.

## Apres modification

- Resume les changements effectues.
- Indique les fichiers de documentation mis a jour, ou explique pourquoi aucune
  mise a jour n'etait necessaire.
- Liste les risques eventuels.
- Liste les tests Unity a effectuer.

## Validation minimale

- Relis les fichiers modifies.
- Verifie qu'aucune solution plus simple n'a ete ignoree.
- Signale les hypotheses non verifiees.
- Si du code Unity a ete modifie, prevois au minimum une compilation Unity et un
  test du flux concerne.
