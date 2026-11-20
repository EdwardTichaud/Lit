# Nettoyage des anciennes compensations — Lucian

45 scripts obsolètes supprimés : solveurs de buste, Moving Legs, contacts/pieds compensatoires, expériences et installateurs associés. Les données de supports qui leur appartenaient ont été supprimées via Unity après retrait des composants sérialisés. L’IK UCC standard est conservé. Les ressources fournisseur ne sont pas supprimées.

Le chemin de locomotion fondé sur le stick et sa conversion fixe est supprimé. Le bridge utilise la vitesse obtenue par UCC. Les réglages physiques, le saut, la traversée et les sessions cinématiques restent sous leurs autorités existantes. Les menus de dialogue utilisent désormais l’installation et la validation uniques. Les autres protagonistes ne sont pas recalibrés.

Vérifications du nettoyage :

- Compilation de contrôle runtime et Editor : aucune erreur.
- Références sérialisées aux composants supprimés dans les prefabs, scènes, controllers et assets examinés : zéro.
- Validation native du contrat Lucian : PASS (22:36:44 UTC).
- Test ajouté : aucune référence de composant manquante dans les trois prefabs de Lucian.
- Derniers tests Editor et Play Mode après nettoyage : en attente de la sauvegarde de Bootstrap demandée par Unity. Les résultats antérieurs au nettoyage ne constituent pas cette validation.

La validation visuelle complète en jeu reste nécessaire ; ce nettoyage ne certifie pas le ressenti de toutes les animations.

Le 20 novembre 2026, la suite Editor a ete relancee apres retrait de la cadence automatique : 56 tests passent, y compris le controle des composants manquants. Les essais visuels et Play Mode complets restent distincts. Voir ../../../../2026-11-20/ManualLocomotionSpeeds/Readme.md.
