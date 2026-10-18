# Qualification finale du combat

## Reference et regles

- Installation principale : `d0aff98072784154b05309f35428062c` ; 90 fichiers surveilles, GUID preserves, seconde application byte-stable.
- Export de cette passe : `Library/ProductionCombatMigration/Qualification/source-export.json`. Il contient revision Git, provenance, fichiers synchronises et SHA-256 ; une revision seule ne decrit pas le worktree modifie.
- Copie isolee : `Library/ActorAnimationSandbox`. Aucun Apply, remplacement d'asset ou restauration de sauvegarde principale pendant la qualification.
- Tests : `CombatMigrationValidation.RunEditBatch`, sans flags de migration, de staging ou de Play Mode. Ne pas utiliser `ProductionCombatValidation.RunBatch`, qui applique une migration.
- Les anciens totaux ne sont pas les resultats de cette passe. Une attestation n'est importee qu'apres execution reussie et verification des artefacts pour la bonne empreinte.
- Toute correction impose de renouveler les preuves invalidees. `ReadyForProduction` reste faux tant que les onze gates ne sont pas acceptees.

## Onze criteres

| Gate existante | Preuve attendue | Etat de cette passe |
|---|---|---|
| compilation | Compilation runtime et editeur sur sources finales | En cours |
| editMode | Suite combat elargie ; echecs et exclusions explicites | En cours |
| playMode | Parcours production, changement d'acteur, nettoyage mort/desactivation | Non execute |
| zone | Chargement de toutes les zones, overrides, gates, placement NavMesh | Non execute |
| save | Sauvegarde/recharge reelle, identites et progression preservees | Non execute |
| domainReloadOn | Entrees repetees, abonnements uniques, nettoyage statique | Non execute |
| domainReloadOff | Memes controles sans Domain Reload | Non execute |
| network | Vrai hote/client, autorite, familles, reactions, paliers et cinematiques | Non execute |
| allEnemyMappings | Tous patterns et mecanismes specialises, evenements auteur | Non execute |
| inventoryCoverage | References dynamiques, Resources/Addressables et markers examines | Audit exhaustif a executer |
| idempotence | Second Apply et dry-run sans ecriture ; GUID/identites preserves | Installation bornee prouvee ; audit complet restant |

## Recettes solo, par lots courts

| Lot | Manipulation | Resultat requis |
|---|---|---|
| S1 | Bootstrap, menu normal, District 1 | Lucian seul, chargement complet, NavMesh pret, camera/deplacement/interactions fonctionnels |
| S2 | Frapper dans le vide puis toucher sans lock ; aggro visuelle | Aucun combat sur coup manque ; engagement au contact/vision sans lock automatique, introduction unique |
| S3 | Attaquer en mouvement et en l'air ; sprint/roulades/charge | Direction coherente, buffers et cooldowns conserves, aucune immobilisation apres interruption |
| S4 | Eclair/Bond, Rupture, Ascension et attaque de zone | Portee et collisions respectees ; Rupture exige le lock ; chute/restauration sures |
| S5 | Garde, contre, clash, LightSkill, paliers multi-QTE | Contacts uniques, contre pre-impact, resultat/fin propres, pas de ressources residuelles |
| S6 | Fuir, retour ennemi, reengagement ; plusieurs menaces | Combat maintenu tant qu'une menace existe, interactions/camera/musique restituees apres la derniere |
| S7 | Sauver, recharger, changer de zone, retour menu | PV/progression/ennemis vaincus et identites coherents ; aucun kit artificiellement debloque |
| S8 | Refaire entree/sortie avec Domain Reload On puis Off | Aucun double callback, singleton perime ou verrou persistant |

Utiliser un slot de test distinct. Ne pas ecraser la sauvegarde habituelle. Tester les kits de Vesta, Aurell et Soren avec une progression de test documentee, sans changer leurs deblocages de production.

## Matrice zones et rencontres

| Sujet | Couverture requise | Etat |
|---|---|---|
| Maison | Core + 9 scenes additives, exploration, interactions, transition | Non execute |
| District 1 | Core + 21 scenes additives, NavMesh/spawns, combat, cycles et retour | Non execute |
| Juggernaut de production | Vision, poursuite, patterns, QTE/contre, fuite, mort | Non execute |
| ScientifiqueFou | Introduction narrative et participant reel, combat, mort | Non execute |
| DeadWeight | Frein d'urgence et gate auteur | Non execute |
| BrokenAnchor | Torches et engagement, transitions/mort | Non execute |
| FalseChoir | Relais, exposition, filtres de degats et fin | Non execute |
| Veilleur | Gates specialisees, combat et mort | Non execute |
| giant_juggernaut | Identite distincte et vrai skill Jump equipe | Non execute |
| giantjuggernaut | Identite distincte et vrai skill Assomoir equipe | Non execute |
| Interaction Etienne | Couverture Flame locale | Placement reserve a l'auteur ; ne pas declarer reussie sans verification |

L'audit doit ajouter tout sujet supplementaire decouvert ; cette matrice n'autorise pas a ignorer une rencontre ou un marker inconnu.

## Recettes reseau

L'auteur dispose d'un second poste/joueur pour hote-client. Quatre joueurs restent a organiser et ne sont pas supposes disponibles.

| Lot | Scenario | Resultat requis |
|---|---|---|
| N1 | Hote + client distant, chaque kit et famille d'action | Actions/mouvements/contacts uniques ; serveur autoritaire et victimes correctes |
| N2 | Portee/cooldown/cible invalides, generation perimee | Refus sans effet non autorise ni doublon |
| N3 | Contre, roulade, paliers et cinematiques du client | Fenetre contre 0,5 s animation, participant reel, UI/camera locales, aucun ralenti reseau |
| N4 | Combats simultanes, cinematiques concurrentes, exploration ailleurs | Aucun detournement des commandes/camera des non-participants |
| N5 | Arrivee tardive, mort, deconnexion, changement de zone | Presentation coherente et ressources/reservations liberees |
| N6 | Quatre joueurs et kits distincts | Memes invariants, sans substitution de l'hote |

Conserver les logs hote et client distincts. Un double de transport ou deux acteurs locaux ne remplacent pas un vrai client.

## Releve de resultat

Pour chaque lot : date, reference/empreinte, configuration, actions effectuees, attendu/observe, erreurs Console, artefacts et statut (`reussi`, `echoue`, `bloque`, `non execute`). Une observation utilisateur est identifiee comme telle, jamais convertie en preuve automatisee.

Importer seulement les attestations conformes via `ProductionCombatEvidenceImport`, avec artefacts hashes et couverture des sujets imposes par le rapport exhaustif. Ne pas creer de seconde API de qualification ni modifier les gates pour accelerer la livraison.
