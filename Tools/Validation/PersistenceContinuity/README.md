# Recettes de continuite et preuves — 24 octobre 2026

Ces scripts sont conserves hors `Assets` : ils ne sont ni compiles ni lances
par le jeu. Ils ne creent pas de projet. La suppression des deux copies est
autorisee par l'utilisateur, mais BLOQUEE automatiquement par l'environnement :
elles existent encore. Le rapport donne leurs deux chemins pour nettoyage manuel.
Ne pas les recreer ou copier le projet sans accord et controle de place libre.

## Resultats qualifies

- `inactive-network-actor-tests.xml` : 75 passes, 0 echec, 0 ignore.
- `ucc-network-pose-continuity-tests.xml` : 74 passes dans l'execution precedente.
- Cinq tests d'authoring passes dans `ucc-network-pose-tests.xml`, MAIS cette
  execution mixte est en echec (42 SetUp refusent une scene temporaire dirty).
  Ne pas additionner ces executions en pretendant une suite globale verte.
- Solo rendu `3cfb072ef8f84a1d929fa407c8ef0edb` : 84 assertions, 0 erreur runtime,
  portails, depot/retrait partiel, sauvegarde/recharge et echec transactionnel.
- Reseau `9e10931ebdff44f693b7e21a76637e23` : ECHEC, annule proprement apres
  erreur native de synchronisation cliente. L'autel de Maison, hash 1275473975,
  n'est pas retrouve dans la scene attendue. Puis NetworkObject.Deserialize
  echoue. Aucune validation du ramassage ou de l'aller-retour reseau dans ce run.
- `fullProductionReadiness` et `functionalGatePassed` restent faux dans ce run.
  L'erreur UnityEditor.Search est egalement conservee, jamais filtree.

## Conservation

`Evidence-2026-10-24.zip` contient les NetworkRuns (reussites partielles ET
echecs), EditorRuns, resultats XML, logs natifs selectionnes et recettes.
Les originaux restent dans `Library/PersistenceContinuityValidation`, qui
n'est PAS un des deux dossiers de projet supprimes. L'archive permet de garder
les preuves meme si le cache Library est nettoye plus tard.

## Reprise ulterieure

Lire `AIAgent/Codex/rapport_persistance_reprise.md` et les sources avant usage.
Les chemins/profils et le nom des copies sont propres a la recette historique.
`SyncProductionDependencies.ps1` n'est pas un createur de projet ; il exige une
copie isolee existante. Ne jamais l'executer pendant que son Unity tourne.
Ne jamais installer ces probes dans le projet utilisateur ouvert.

`PersistenceEditorPlayProbe` lance les vrais Bootstrap/menu et sort normalement.
Le reseau requiert deux editeurs rendus, pas `-batchmode`/`-nographics` :
`-executeMethod PersistenceEditorPlayProbe.Run -persistence-validation`
`-persistence-network-roundtrip -persistence-network-role=host` (ou `client`)
`-persistence-network-run=<dossier absolu partage unique>`.
Chaque processus utilise des saves isoles ; attendre la sortie avant toute
copie. Un fichier `cancel-requested.txt` dans le dossier partage annule l'essai
proprement, en ECHEC. Aucun gate de production ou de build ne doit etre contourne.

Fixtures explicites : approche physique via API UCC/NGO et Flame.SetLit(true).
Ces recettes ne prouvent ni les inputs manuels, ni la camera, ni le saut/landing,
ni Relay, ni la reconnexion, ni le rechargement disque d'un hote avec ses clients.
