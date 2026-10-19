# Qualification finale du combat

## Reference et regles

- Charge sans cible : dfba2a9d62c5430ca189107904d2a224 PASSE dans District puis
  Maison depuis Bootstrap/portail/retour. Vrai X : relachement pendant ralenti,
  puis maintien avec meme Basic/generation, cadrage actif, lancement unique apres
  seuil, cooldown et restitution complete sans engagement/lock. Compilation native
  et 119 hashes identiques. Preuves : Library/ProductionCombatMigration/Charge.
  Aucun runtime/asset auteur modifie ; suite elargie non rejouee dans ce lot
  (derniere 544 passes / un echec Etienne / un Explicit ignore). Projection,
  flottement, interruptions, ressenti et reseau restent non qualifies ici.
  Aucun gate global promu.
- Defenses de production : cas 5d4f296413df4de0b8e67f70d0555596 PASSE depuis
  Bootstrap, vraie rencontre et inputs B/Y, deux cycles avec mouvement roulade,
  garde tenue/relachee et commandes rendues ; fuite/reengagement/retour passes
  ensuite. Aucune suspension ennemie ni contre force. Fixture Flame explicite,
  83 hashes identiques. Preuves : Library/ProductionCombatMigration/Defenses.
  Pas de preuve de degats reduits par garde, esquive garantie, CounterSkill,
  ressenti ou reseau. Compilation native reussie ; suite elargie : 544 passes,
  un echec Etienne (Flame auteur), un Explicit non execute. Aucun gate global promu.
- Sprint/saut de production : cas 7e1e274b5f814e30bfae8f4651a7a91f PASSE dans
  District et Maison. RT route/applique avec tier 3,25 et vitesse physique accrue,
  relachement propre ; deux vrais sauts successifs par zone avec restitution
  effective de l'input, sans liberation forcee ni hauteur auteur changee. Le conflit
  InputValue/CallbackContext sur NetworkCharacterInput.OnJump a ete reproduit puis
  corrige par renommage OnRouted* des quatre callbacks concernes, RPC inchanges.
  Quatre regressions ajoutees et passees ; suite 544 passes / un echec Etienne /
  un Explicit non execute. 75 hashes compares. Preuves :
  Library/ProductionCombatMigration/Locomotion. Ni roulade/garde en engagement,
  charge, interruption de saut par attaque, ressenti visuel ou reseau qualifies.
- Inputs de production : cas 125c75c0dc9d43129a260098dd757546 PASSE avec Gamepad
  virtuel apres chaque arrivee du parcours Maison/District/Maison. Impulsion
  camera-relative mesuree dans RawMove/UCC/pose (1,03 m puis 0,87 m ; alignement
  >0,997), relachement propre, X unique et fin Basic sans repetition/charge.
  Ni appels directs de mouvement/attaque pour ces gestes, ni preference changee.
  Decalage .2/.5 des quatre prefabs corrige dans la copie seulement ; principal
  deja a .5, nouveau test par personnage. Suite elargie 540 passes / un echec
  Etienne / un Explicit non execute, 61 hashes compares. Ce n'est pas un essai
  des huit directions, defenses, charge, ressenti manuel ou reseau. Artefacts :
  Library/ProductionCombatMigration/Gamepad. Aucun gate global promu.
- Transitions solo : recette 6bf1904b99794059aa90056332b7b46b PASSEE. Portail
  auteur Maison admis via ServerTryUse, poses d'arrivee capturees avant decharge,
  District 1 complet, Basic/fin, ReturnToHub(Default), Maison complete et Basic/fin.
  Meme acteur/PV/progression/binding, un seul monde commun, NavMesh de la bonne
  zone, anciennes scenes dechargees et controles/interactions autorises. Fixture
  UCC d'approche du portail uniquement : ni input Interagir manuel, ni mouvement
  au stick, ni audio, ni reseau qualifies. Aucun changement runtime ou asset auteur.
  Compilation et regression elargie 536 passes / 1 echec Etienne / 1 Explicit
  non execute ; vingt-neuf hashes compares. Preuves :
  Library/ProductionCombatMigration/ZoneTransfer. Aucun gate global promu.
- Couverture Maison : recette 7a37b21178774e04a2d2920f775a5641 PASSEE depuis
  Bootstrap avec manifeste explicite, dix scenes, NavMesh Ready, Lucian seul au
  spawn auteur/grounded, Basic et fin de session, cycle natif du curseur et snapshot
  ecrit/lu. Baseline reproduite : LucianSpawnPoint etait un Transform non reconnu.
  Seul ajout auteur : ZoneSpawnPoint Default/slot 0 sur ce meme objet ; aucune
  pose, identite ou metadonnee changee. Suite elargie finale 536 passes / un echec
  Etienne / un Explicit ignore, dont sept nouveaux tests passes ; dix-neuf hashes
  compares. Pas de qualification des interactions Maison, transfert de zone ou
  reseau. Artefacts : Library/ProductionCombatMigration/ZoneCoverage.
- Recuperation physique : penetration des meshes Flame/WoodFire reproduite par
  diagnostic natif, puis contact incline bloquant identifie. Descente par capsule
  complete, ruee incluse, et sortie bornee dans le moteur existant implementees ;
  admission NavMesh toujours locale a 0,15 m. Regression ciblee 483/0/1, neuf cas
  physiques ajoutes, dix-neuf empreintes comparees. Recette lifecycle
  d14f503cc325488a82628d855da4fe58 PASSEE : deux impacts, fuite, reengagement,
  troisieme impact, seconde fuite, controles rendus et retour home/NavMesh/
  orientation auteur sans impact tardif, snapshot ecrit/lu. Flame allumee comme
  fixture explicite ; ni interaction/progression, ni fuite au stick, ni BattleWall,
  ni toutes zones/reseau valides. Aucun gate global promu. Echecs et XML conserves dans
  Library/ProductionCombatMigration/LandingRecovery. Aucun asset auteur modifie.
- Lifecycle production : retour a reculons reproduit puis corrige (dot -0.915
  -> 1), protection du coup hors leash ajoutee. Contrats de controles/interactions
  restitues deux fois, reengagement pendant retour et troisieme impact passes.
  Les recettes precedant la recuperation physique ci-dessus echouaient sur un
  collider Flame non navigable, Recovery/agent desactive persistants ; pas une
  tolerance a agrandir. Compilation et regression de ce lot precedent 474/0/1, dix-huit
  empreintes comparees incluant moteur, prefab et scene. Artefacts :
  Library/ProductionCombatMigration/Lifecycle ; CombatEscapeLifecycle*.log et
  CombatLandingLifecycleDiagnostics.log. Aucun asset auteur modifie dans ce lot.
- Vision/reprise de patterns : cause 6/30 reproduite par import natif avec/sans
  ligne vide avant vision. Retrait de cette seule ligne auteur, GUID/meta/valeurs
  preserves. Repositionnement NavMesh sous la portee minimale ajoute sans
  deplacement Transform ni ajout de skill. Recette 52de58fb32b245f6b2487b975f212307
  passee : vrai Juggernaut, aggro sans lock, approche 5.61 m a 3.62 m/s, reprise
  de distance et deux Assomoirs, PV 1000 -> 960. Flame existante allumee comme
  fixture explicite : allumage/progression non valides. Regression 471/0/1, dix
  empreintes comparees, manifeste d'un asset cible. Artefacts :
  Library/ProductionCombatMigration/VisionImport. Fuite/retour, autres rencontres
  et reseau restent distincts ; aucun gate global promu.
- Spawn nouveau jeu corrige, sans modifier de point auteur : le fallback de
  scene ne remplace pas une pose sauvegardee. Recette 39e49776f3f2450bbb3f034a7ded5ae9
  passee : spawn au sol, Basic, sauvegarde a une pose distincte, menu/recharge
  avec pose/PV/identites conserves. Compilation et regression 463/0/1, sept sources
  et metadonnee comparees. La rencontre reste en echec : vision chargee 6 m,
  YAML canonique 30 m, Lucian grounded a 7 m, echelles a 1 et aucun stop Brains.
  Aucun gate global valide sur ce seul cas. Artefacts :
  Library/ProductionCombatMigration/Spawn ; Logs/CombatSpawn*.log et
  CombatEncounterGateDiagnostics.log.
- Rencontre production : recette reelle ajoutee, aggro sans lock reproduite mais
  immobilite du kit Assomoir seul. Filtre de preference melee et reprise d'agent
  corriges ; compilation et regression 456/0/1, cinq empreintes comparees a la
  copie compilee. La recette complete reste en echec ; spawn initial a l'origine
  et placement/sol UCC a corriger/verifier. Aucun gate Play Mode ou zone passe
  artificiellement. Preuves : Library/ProductionCombatMigration/Encounter.
- Lot inventaire/cycles : audit complet sans ecriture proposee, baseline 166
  blocages avant nettoyage (dont 77 GUID distincts). Archives auteur examinees,
  aucun original correspondant trouve. Deux cycles natifs On et deux Off passent,
  apres un premier timeout Off sur Basic toujours ouvert. Nettoyage strict de
  70 documents de diagnostic orphelins dans 10 controllers livre ; regression
  453/0/1. Les preuves natives precedent ce nettoyage sans changement de gameplay.
  Artefacts : Library/ProductionCombatMigration/SessionCycles. Pas de nouvelle
  attestation de gate ni de declaration ReadyForProduction.
- Dernier lot controles/persistance : onze fichiers auteur livres, 26 sources
  comparees a la copie validee, 451 tests cibles passes / 0 echec / 1 Explicit
  non execute. Recette rendue 66701034d42a44f5b225c5bc644f902f passee : Bootstrap,
  District 1 complet, Basic, vrai retour menu/recharge, un seul Lucian, PV 993,
  progression preservee, ennemi fixture toujours mort sous son marker, NavMesh
  Ready. Nettoyage natif du curseur confirme. Preuves et echecs precedents :
  Library/ProductionCombatMigration/Controls et Logs/CombatControls*.log.
  Cela ne valide ni victoire jouee, ni toutes zones, ni client distant.
- Continuation du 2026-10-19 : onze fichiers auteur livres avec sauvegardes et
  gardes SHA-256 dans `Library/ProductionCombatMigration/CombatFeel/`. La suite
  combinee avant correction Bootstrap passe 418/0/1 ; la regression finale avec
  resolution de zone runtime passe 419/0/1 (un Explicit non execute).
  Une recette rendue a revele `zoneManifests` vide ; le vrai Bootstrap reference
  maintenant District 1. Nouvelle recette : 22 scenes, NavMesh Ready 2/2,
  Lucian seul, Basic terminee et snapshot ecrit/lu. Ce n'est ni un parcours solo
  complet, ni une restauration reelle de sauvegarde, ni une preuve reseau.
  Rotations et curseur monde, encore propres a l'arene a ce stade, sont maintenant
  dans les donnees communes du lot controles/persistance ci-dessus.
- Installation principale : `d0aff98072784154b05309f35428062c` ; 90 fichiers surveilles, GUID preserves, seconde application byte-stable.
- Export de cette passe : `Library/ProductionCombatMigration/Qualification/source-export.json`. Il contient revision Git, provenance, fichiers synchronises et SHA-256 ; une revision seule ne decrit pas le worktree modifie.
- Copie isolee : `Library/ActorAnimationSandbox`. Aucun Apply global, remplacement par une fixture ou restauration de sauvegarde principale. Les corrections ciblees livrees sont tracees par leurs manifestes et sauvegardes.
- Tests : `CombatMigrationValidation.RunEditBatch`, sans flags de migration, de staging ou de Play Mode. Ne pas utiliser `ProductionCombatValidation.RunBatch`, qui applique une migration.
- Les anciens totaux ne sont pas les resultats de cette passe. Une attestation n'est importee qu'apres execution reussie et verification des artefacts pour la bonne empreinte.
- Toute correction impose de renouveler les preuves invalidees. `ReadyForProduction` reste faux tant que les onze gates ne sont pas acceptees.

## Onze criteres

| Gate existante | Preuve attendue | Etat de cette passe |
|---|---|---|
| compilation | Compilation runtime et editeur sur sources finales | Copie isolee reussie, 75 fichiers source/asset/meta compares pour le lot sprint/saut ; gate globale non revendiquee |
| editMode | Suite combat elargie ; echecs et exclusions explicites | Suite elargie : 544 passes / 1 echec Etienne (couverture Flame auteur) / 1 Explicit non execute ; aucun echec masque |
| playMode | Parcours production, changement d'acteur, nettoyage mort/desactivation | Spawn/recharge, lifecycle Juggernaut, aller-retour solo, stick/X, sprint et deux sauts par destination passes ; defenses, charge, interruptions et autres parcours restants |
| zone | Chargement de toutes les zones, overrides, gates, placement NavMesh | District 1 : 22 scenes ; Maison : 10 scenes. Aller-retour solo par API passe, NavMesh courant et anciennes scenes dechargees ; interactions et rencontres restantes |
| save | Sauvegarde/recharge reelle, identites et progression preservees | Cas District 1 passe (PV, progression, ennemi fixture, unicite) ; autres cas restants |
| domainReloadOn | Entrees repetees, abonnements uniques, nettoyage statique | Deux cycles Bootstrap/menu/recharge/sortie passes ; couverture des inputs/abonnements non exhaustive |
| domainReloadOff | Memes controles sans Domain Reload | Deux cycles passes apres un premier timeout Basic intermittent encore ouvert |
| network | Vrai hote/client, autorite, familles, reactions, paliers et cinematiques | Non execute |
| allEnemyMappings | Tous patterns et mecanismes specialises, evenements auteur | Cas Juggernaut Assomoir seul passe ; autres patterns, paliers et rencontres non qualifies |
| inventoryCoverage | References dynamiques, Resources/Addressables et markers examines | Audit exhaustif execute, references manquantes/dynamiques encore ouvertes ; baseline conservee |
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
| Maison | Core + 9 scenes additives, exploration, interactions, transition | Bootstrap, dix scenes, NavMesh, spawn auteur, Basic/cycle curseur/snapshot et aller-retour par API passes ; interactions et ressenti restants |
| District 1 | Core + 21 scenes additives, NavMesh/spawns, combat, cycles et retour | Chargement, Basic, menu/recharge, repetitions, lifecycle Assomoir et aller-retour Maison passes ; autres familles et victoire jouee restantes |
| Juggernaut de production | Vision, poursuite, patterns, QTE/contre, fuite, mort | Vision/trois attaques, fuite/reengagement/retour final passes ; QTE/contre, autres patterns et mort jouee restants |
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

L'auteur s'est declare disponible pour le solo et hote-client. La configuration
du second poste reste a confirmer apres explication ; aucun client distant ni
groupe de quatre joueurs n'est suppose disponible ou deja valide.

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
