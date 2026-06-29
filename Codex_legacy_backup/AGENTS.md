# Rôle de Codex

Codex ne possède plus sa propre base documentaire.

La source de vérité est maintenant :

../AIStudio/docs/

Codex doit suivre les briefs générés par AIStudio, notamment :

../AIStudio/logs/latest_mission_brief.md

Règles :

- Ne pas improviser une architecture.
- Lire d'abord le brief AIStudio.
- Utiliser la documentation AIStudio si le brief y renvoie.
- Modifier le projet Unity uniquement selon le plan validé.
- Garder les patches minimaux.
- Ne pas refactorer sans justification explicite.
- Après modification, résumer :
  - fichiers modifiés ;
  - risques ;
  - tests à faire.