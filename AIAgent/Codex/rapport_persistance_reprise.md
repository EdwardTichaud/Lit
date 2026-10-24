# Persistance — rapport de reprise du 24 octobre 2026

## Etat reel

Le travail n'est pas termine dans son ensemble. Le parcours solo rendu passe
dans le perimetre de la recette ; le parcours multijoueur complet reste en echec.
Aucun processus de validation suivi n'est encore actif. Aucun commit cree.

## Corrections conservees

- Archives de sous-scenes : capture avant depart, restauration selective au retour,
  preservation des zones absentes, identites stables et quantites des depots.
- Fabriques d'items : definitions disponibles avant restauration, provider
  PersistentContainerState present sur les depots recus par NGO, hashes de
  prefabs enregistres conserves. Pas d'allocation d'identite stable par client.
- Sauvegarde transactionnelle personnage/monde/metadonnees : journal durable,
  recuperation et sortie refusee si le commit echoue, sans perdre la partie active.
- Sequences narratives : completions en memoire, engagees avec la sauvegarde.
  Quitter sans sauvegarder puis recharger suit bien le dernier etat sauvegarde.
- GameFlow/JoinSync client en deux temps : services avant snapshot ; navigation et
  escouade apres restauration de progression ; application initiale unique.
- ServerUccNetworkTransform : synchronise le cache moteur/interpolation UCC avec
  la pose recue de NGO. Autorite serveur conservee. Snap Animator/camera/abilities
  complet au spawn/teleport seulement, pas a chaque interpolation. Quatre prefabs
  joueurs et leur fabrique/authoring alignes, sans deux NetworkTransform empiles.
- CombatNetworkActorRuntime : les deux NetworkList sont creees des la declaration,
  avant Awake, car NGO synchronise aussi les acteurs inactifs. Awake conserve les
  instances deja enregistrees/deserialisees. Permissions et format inchanges.

## Tests et limites

- Compilation Unity et inactive-network-actor-tests.xml : **75 passes, 0 echec,
  0 ignore**, dont la regression InitializeVariables avant Awake puis identite
  des listes apres Awake.
- ucc-network-pose-continuity-tests.xml : execution precedente isolee, **74 passes**.
- Cinq tests d'authoring passes dans ucc-network-pose-tests.xml. Cette execution
  mixte n'est PAS verte : 42 SetUp refusent une scene temporaire dirty laissee par
  le groupe precedent. Protection utilisateur conservee, pas de bypass.
- Solo rendu 3cfb072ef8f84a1d929fa407c8ef0edb : **84 assertions**, zero erreur runtime.
  Bootstrap/menu, vrais portails Maison/District/Maison, depot 3/reprise 1/reste 2,
  inventaire, identites, PV, pose et ennemi archive, vraie recharge disque,
  echec transactionnel de sortie puis recuperation et abandon sans sauvegarde.
  Approche UCC et defaite d'ennemi sont des fixtures explicites ; pas de preuve
  de victoire manuelle, recompense auteur, camera ou saut/landing.
- Diagnostic 2b9ea330f79344a4a1060429268b7bf5 : pose serveur et cible NGO cliente
  correctes, mais cache UCC/Transform/Rigidbody client restaient au spawn.
  Apres correctif, le replay c14c8ca6148f48d480133874859c29e1 franchit la distance.
- Essai 6f61adffd24f422d9ffab93adb0054c9 : erreur produit controlStates null sur
  acteur inactif pendant le late join. C'est l'origine du correctif des listes.
- Dernier essai **9e10931ebdff44f693b7e21a76637e23 : ECHEC**.
  L'hote passe le portail auteur Maison vers District et le gate reel d'influence
  de son depot. Le client echoue pendant la synchronisation native : scene non
  retrouvee, soft synchronization failure pour hash 1275473975, identifie cote
  hote comme VigilAltar_Hub_Safeguard dans Maison_Enigme_Vigil, puis exceptions
  NetworkObject.Deserialize / SceneEventData.SynchronizeSceneNetworkObjects.
  Warning de serialisation ShadowGuardian et erreur UnityEditor.Search conserves.
  Annulation gracieuse apres ce defaut conclusif, sortie normale des deux editeurs.
  **functionalGatePassed=false et fullProductionReadiness=false.**
  Ni prise RPC ni aller-retour avec client actif qualifies dans ce run.
- Les recettes utilisent deux processus rendus et le vrai NGO loopback, avec
  fixtures explicites d'approche UCC/NGO et Flame.SetLit(true). Pas de preuve
  d'input d'allumage, de Relay ni de rejoin. Aucun gate de gameplay affaibli.
- Build de developpement toujours non qualifie : des validateurs independants
  signalent des contrats d'animation/temps/acceptance deja existants. Ne pas
  contourner ces garde-fous ni modifier les assets proteges pour obtenir du vert.

## A reprendre, par priorite

1. Examiner le cycle de vie et le mappage des scenes/objets NGO Maison au late join
   apres portail : comparer handles, despawn/dechargement et installation runtime
   des identites. L'objet et son hash sont identifies ; la cause exacte du mauvais
   rattachement n'est pas encore demontree. Ne pas randomiser son hash ou enregistrer
   arbitrairement un objet de scene en prefab pour masquer l'erreur.
2. Resoudre le warning de serialisation ShadowGuardian avec son exception complete.
   Rejouer ensuite le late join District avec les vrais objets, sans erreurs.
3. Qualifier depot stable 3, prise de 1 par ServerRpc, inventaire du seul client
   credite, reste 2 partage et commit uniquement hote ; portails aller/retour avec
   client actif, acteurs uniques et quantite archivee restauree.
4. Reconnexion avec meme identite, recharge disque hote puis rejoin, deconnexion
   pendant transition et ordre snapshot/deltas d'inventaire. Verifier aussi le
   traitement d'un refus client_ready, pas seulement sa reussite.
5. Securiser tout refus de restauration partielle : conserver une zone/sol
   utilisable, et refuser sauvegarde/export/late join d'un monde incomplet.
6. Completer les preuves d'inputs manuels, camera, saut/landing, objets auteurs,
   recompenses, suppressions definitives et narration rendue abandon/recharge.
7. Traiter separement UnityEditor.Search et les blockers de build deja signales.

## Nettoyage et conservation

L'utilisateur a explicitement autorise la suppression apres validation de :

- C:\Users\pierr\git\Lit\Library\MovingLegsValidationProject
- C:\Users\pierr\git\Lit\Library\PersistenceNetworkClientProject

Ils representent environ **79.04 Gio de fichiers**, pas une mesure d'espace
physique recuperable. Racines resolues et reparse points controles ; les editeurs
et leurs import workers sont sortis. La tentative de suppression a ete **bloquee
par la politique de l'environnement avant execution**. Les deux dossiers existent
donc encore ; aucun espace ne doit etre annonce comme libere.

Pour nettoyage manuel, fermer tout Unity qui utiliserait une de ces copies,
puis supprimer uniquement ces DEUX dossiers dans l'Explorateur Windows.
Ne supprimer ni le projet principal Lit, ni Library/PersistenceContinuityValidation.

Recettes utiles preservees dans Tools/Validation/PersistenceContinuity (hors Assets).
Archive durable : Tools/Validation/PersistenceContinuity/Evidence-2026-10-24.zip,
409 entrees, 29 783 701 octets ; contient tous les NetworkRuns, EditorRuns, XML,
logs selectionnes et sources de recettes, y compris les echecs.
SHA256 : 3DA6B1AA6056EF51E0AD9F60343CF5AEB3DEA9271F636BBCB26711257B4A150D.
Les originaux restent dans Library/PersistenceContinuityValidation, hors copies.
Les projets de test complets ne sont pas archives. Aucun nouveau projet cree ;
ne pas en recreer sans accord et controle de place libre.

Documentation technique : systems/persistence.md et systems/runtime_session.md.
