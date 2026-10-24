# Audit de robustesse des cycles de District_1

État du chantier : en cours, reprise le 24 octobre 2026. Ce document ne constitue pas une
validation de parcours multijoueur.

Mise à jour : Luc réactivé/replacé avec contrôles natifs ; souvenir Nina créé
sur décision humaine ; proposition Gustave rédigée, encore à valider.
Suite principale sélectionnée : 192/192 puis 193/193 après l'ajout Nina,
puis **194/194** avec vérification native du défilement/nettoyage des citations.
Voir `District_1_Cycles_Resume_2026-10-24.md` pour les preuves et limites.
Les paragraphes datés du 21 octobre ci-dessous restent un historique, pas
l'indication que ces trois actions sont toujours en attente.

## Contrats conservés

- Cycles indépendants ; la chronologie dépend des accès physiques et triggers.
- Environnement permanent séparé des acteurs et obstacles pilotés par les cycles.
- Identifiants de sauvegarde et placements manuels conservés.
- Progression et récompenses autoritaires, répliquées ; simulation Dev sans écriture.
- Timeline éditable et validation uniquement après une fin réussie.

## Corrections réalisées

- Catalogue `District1CycleCatalog` sous Resources/Narrative : référence Luc et
  Nina, dont les fiches restent dans leurs dossiers de scènes. La progression,
  les récompenses de compétences et le filtrage des scènes terminées utilisent
  le même chargement, même sans scène du cycle chargée.
- La validation de projet inspecte toutes les fiches, et signale celles absentes
  du chargement runtime. L'inspecteur utilise la même règle.
- Chemins des outils et tests Luc/Nina remis en accord avec les assets actuels.
- Récompenses d'objets : le reliquat d'un dépôt partiel au coffre est conservé
  dans les variables monde existantes, au lieu de redonner la quantité totale.
  Test ajouté : coffre plein, dépôts fractionnés, restauration du snapshot,
  disparition de la dette après livraison complète.
- Les trois comportements de boss appellent explicitement le nettoyage hérité
  lors de leur désactivation ; aucune règle de combat n'est modifiée.
- Le lanceur des tests inclut désormais Luc, Nina, Étienne, Ardent et Clairmont,
  sans import forcé d'asset. Il refuse de lancer la suite si une scène est sale,
  pour ne pas provoquer de choix d'enregistrement pendant un test automatique.
- Documentation et tooltip alignés sur la conservation effective des faits :
  une source partagée peut satisfaire plusieurs étapes successives. Employer
  des sources différentes si plusieurs actions distinctes sont requises.

## Inventaire à compléter

Six définitions présentes : Luc, Nina, Belmont, Étienne, Ardent, Clairmont.
`District_1_Cycle_Gustave` contient actuellement une note récupérable, sans
CycleController ni CycleDefinition : c'est un contenu d'enquête, pas encore un
cycle complet au même contrat. Ne pas lui inventer une conclusion silencieusement.

Belmont possède désormais des tests dédiés aux six ordres d'allumage :
progression Dev parallèle et synchronisation réelle des trois PV avec les
torches, sans double comptage du callback projectile. L'audit détaillé de scène
et le parcours jusqu'à la première Ancient Flame restent à compléter.

La première suite élargie a donné 106 réussites et trois échecs : deux fixtures
de raccourcis incompatibles avec la scène sans titre de NUnit, et Étienne hors
couverture lumineuse. Les fixtures ouvrent désormais la scène Maison existante
sans l'enregistrer, plutôt que créer une scène sans titre supplémentaire.
Un brasero commun `Flame_Etienne`, initialement éteint, est ajouté à proximité
du fantôme avec l'ID `district1.puits.etienne`, sans déplacer les acteurs ni
étendre démesurément les rayons existants.

Revalidation ciblée dans Unity : 16/16 contrôles réussis (12 permutations
Belmont santé/progression Dev, audit de scène Étienne, permissions des deux
raccourcis et validation globale du projet). Rapport :
`Library/DistrictCycleValidation/FocusedChecks.txt`. Ces contrôles ne ferment
ni n'enregistrent Bootstrap, encore modifiée dans l'éditeur.

La réplique `lucian_after_shadow` de Belmont est désormais également déclarée
dans sa définition, avec le texte et les cinq secondes déjà configurés en
scène. La validation Maison inspecte les références de script sérialisées,
sans charger tous les modèles et matériaux du projet pour chercher un coffre.

## Vérifications

Compilation de contrôle runtime et éditeur réussie après les corrections de
catalogue et de récompenses ; compilation native Unity également effectuée.
Les résultats de la suite courante sont dans `Library/CycleTests.result`, avec
le détail NUnit dans `Library/CycleTests.xml`. Consulter leurs dates : les
anciens résultats ne prouvent pas l'état courant. La suite complète doit
être rejouée sur une session d'éditeur avec les scènes enregistrées ; le
dernier passage complet précède les trois corrections vérifiées séparément.

Restent à vérifier : parcours complets solo, hôte/client et arrivée tardive,
interruptions des souvenirs, restauration à chaque jalon, navigation et accès
persistants après déchargement. Aucun parcours réel de groupe n'est déclaré
réussi à ce stade.

## Répliques automatiques : deuxième passage

Le délai fixe de `CompleteAutoDialogue` est supprimé. La présentation est
séparée dans `CycleController.AutoDialogues` et la barrière d'accusés de fin
dans `CycleAutoDialogueSession`, indépendante de Unity et des sauvegardes.
Le serveur fige l'audience, vérifie les tokens et la durée minimale, puis
attend les fermetures naturelles. L'interface occupée est respectée ; un refus
ou une interruption ne valide rien et permet une nouvelle tentative.
Les départs sont retirés de l'audience, les arrivées tardives ne la modifient
pas. Il faut au moins une fermeture naturelle : une salle vidée de tous ses
spectateurs ne termine pas une réplique invisible.

La désactivation annule les sessions, invalide les callbacks et ne ferme que
le dialogue possédé par le cycle. Une attente de confirmation bloque la
séquence suivante et le déchargement. L'ID d'étape et le `sourceId` sont
maintenant distincts et testés comme tels. L'inspecteur expose enfin les
liaisons de Flames et de répliques, et ne signale plus ces sources valides
comme des interactions manquantes.

Compilation native Unity runtime/éditeur réussie. Vérifications synchrones
dans Unity : **141/141 assertions réussies**, avec SetUp/TearDown par cas et
capture des erreurs Unity. Elles comprennent 13 tests de barrière et 6 tests
des vrais énumérateurs du contrôleur, en plus des cycles précédents.
Rapport : `Library/DistrictCycleValidation/InPlaceChecks.txt`. Ce passage en
place préserve les scènes ouvertes, sans enregistrer Bootstrap. Il ne remplace
pas un parcours Play Mode ni une session hôte/client.

## Régression réelle du protocole réseau

Procédure conservée sous `Tools/Validation/CycleNetworkSmoke/Run.ps1`, hors
des assets du jeu. Elle crée une copie code/UI sans décor sous
`Library/DistrictCycleRuntimeProject`, construit un exécutable Mono séparé,
puis démarre de vrais processus UTP/NGO sur la boucle locale. Le nom de produit
et le dossier de sauvegarde sont isolés. Les scripts runtime testés sont copiés
sans modification, avec vérification des hashes avant et après l'essai.
Le marqueur du projet et les gardes du simulateur empêchent son installation
dans l'application Lit ou l'exécution de son builder dans le projet principal.

Exécution de référence : `Library/DistrictCycleValidation/Run-20261020-225721`.
**7 scénarios réussis, 16 résultats de processus réussis** :

- Hôte/client normal : véritable fermeture du panneau, puis progression répliquée.
- Interface client occupée : conversation antérieure préservée, aucun jalon anticipé.
- Interruption : première tentative annulée ; seconde présentation nécessaire.
- Déconnexion d'un client : aucun achèvement avant la fin naturelle restante.
- Arrivée tardive : audience initiale inchangée, progression reçue sans rejouer la réplique.
- Serveur dédié et départ des deux clients : aucune validation d'une réplique non terminée.
- Désactivation/réactivation du contrôleur : panneaux annulés, aucun jalon perdu ou anticipé,
  puis nouvelle présentation réussie sur hôte et client.

Rapports : `Results.csv`, `SourceHashes.csv`, journaux et résultats de chaque
processus dans ce répertoire. Les premiers essais de mise au point hors de
ce répertoire ne sont pas les résultats de référence.

Un test NUnit natif supplémentaire a validé la restauration des dissolutions :
`Library/DistrictCycleValidation/DissolveLifecycle.xml`, **1/1 réussi**. Le suivi
des présentations est remis à zéro à la désactivation ; un mur déjà effacé
ne rejoue pas son effet après rétablissement du contrôleur.

Ces essais vérifient le protocole, le vrai cycle de vie du panneau et les
messages réseau, pas le rendu HDRP, les collisions, les boss dans leur arène
ou les caméras des Timelines des scènes narratives. Ces parcours restent requis.

## Audit de couverture des liaisons Timeline — 20 octobre 2026

Le contrôle précédent ne vérifiait que les entrées présentes dans les profils :
une piste active sans aucune entrée pouvait donc passer sans diagnostic.
L'inspecteur CycleController vérifie désormais chaque séquence, et le menu
`Lit > Narrative > Audit District_1 Timeline Bindings` inspecte les scènes
enregistrées dans des scènes de prévisualisation séparées, sans sauvegarde ni
remplacement des scènes ouvertes. Le contrôle des scènes chargées ne réclame
plus les cibles de profils appartenant aux autres scènes additives absentes.

Les pistes muettes, les pistes Audio vides, les bindings facultatifs explicites
et les liaisons natives compatibles du Director restent autorisés. Ce contrôle
de couverture ne prouve pas la disponibilité des cibles dynamiques à l'exécution.

Huit assertions NUnit exécutées dans Unity en place ont réussi (hors Play Mode,
pas une nouvelle exécution complète du runner NUnit) :
`Library/DistrictCycleValidation/TimelineBindingAudit.txt`.
Le même rapport contient l'inspection effective des scènes sauvegardées :

- Belmont : `anchor_release` possède une ActivationTrack de 4 secondes sans
  binding natif ni entrée de profil. La Timeline peut actuellement réussir sans
  jouer cette piste. **La mise en scène reste à réparer** ; le contrôle ne la
  répare pas automatiquement et aucun placement de Belmont n'a été modifié.
- Nina : Director sans Timeline/profil, conformément au point déjà signalé.
- Étienne et Ardent : pas de défaut de couverture signalé par ce contrôle.
  Le rendu, les cibles runtime, les interruptions et la caméra restent à tester.

Le petit runner temporaire d'inspection a été retiré après l'essai. Le menu
d'audit et les tests de non-régression restent disponibles dans le projet.

### Propriété de la disparition de l'Ancre

Un second conflit a été vérifié dans le code courant : `EnemyController.ActorAwake`
ajoute automatiquement `EnemyDeathPresentation`, qui peut lancer son propre
fondu et désactiver le boss pendant l'attente ou la lecture de la séquence de
quête. `BossDefinitionSO.UseExternalDeathPresentation` permet désormais de
confier explicitement cette disparition à la quête/Timeline. L'option est
désactivée par défaut et activée uniquement dans `BossDefinition_BrokenAnchor`.
Le composant de mort standard continue d'exister, mais ne démarre pas sa
coroutine pour ce boss. Les PV, la résolution du combat et la persistance ne
sont pas modifiés par cette option.

Quatre assertions NUnit exécutées en place dans Unity ont réussi : Ancre
externe et conservée à 0 PV, Poids mort et Faux Chœur toujours standards,
option désactivée par défaut pour les futurs boss. Rapport :
`Library/DistrictCycleValidation/BelmontReleaseInspection.txt`.

L'inspection native de la scène sauvegardée révèle également que les deux
meshes visibles de l'Ancre (`Brazier`, `WoodFire`) utilisent des matériaux
Autodesk Interactive HDRP sans `_DissolveStrength` ni `_DissolveAmount`.
Le simple fondu `CycleDissolveBinding` ne pouvait donc pas les dissoudre : il
attendait puis désactivait l'objet. La réparation complète de `anchor_release`
doit fournir une présentation locale compatible, sans modifier les matériaux
partagés du pack, et conserver la restauration après interruption. Elle reste
à réaliser et à tester visuellement ; la Timeline n'a pas encore été réparée.

## Décisions de contenu demandées

### Relance NUnit après enregistrement des scènes — 20 octobre 2026

La protection des scènes modifiées a d'abord refusé l'exécution, sans rien
enregistrer ni abandonner. Après l'enregistrement par l'utilisateur, le runner
NUnit Edit Mode du projet principal a réellement terminé : **142 tests réussis,
0 échec, 0 ignoré** (`Library/CycleTests.xml` et `Library/CycleTests.result`,
début à 21:09:44 UTC). Ce résultat remplace le précédent statut « NOT RUN »
pour cette relance. Il ne remplace pas les parcours Play Mode, les contrôles
visuels HDRP et les essais des cinématiques dans leurs scènes.

Le diagnostic de refus du runner précise désormais toutes les scènes modifiées
pour permettre de décider explicitement lesquelles enregistrer.

- Gustave : la scène ne contient qu'une note. Confirmer s'il faut concevoir un
  véritable cycle ou conserver cet élément comme indice de lore.
- Nina : `aftermath_seen` est facultative mais son Director n'a ni Timeline ni
  profil. Confirmer la création d'un souvenir ou le nettoyage de cette
  configuration, sans casser les identifiants historiques de sauvegarde.

## Présentation Belmont : réparation préparée, application en attente

La présentation préparée utilise six secondes : convergence des trois torches
(0–3 s), dissolution de l'Ancre (2–4 s), éclat temporaire près de l'Ancient Flame
(4–6 s), deux plans Cinemachine et une piste Audio vide clairement identifiée.
La Flame réelle reste pilotée par `lower_flames_restored`, sans allumage forcé.

`TimelineLightLinks` suit les Transform existants, avec une intensité animée par
Timeline. `TimelineDissolveTarget` conserve les matériaux et PropertyBlocks
d'origine et utilise des copies locales pour les meshes incompatibles. Un
callback Timeline optionnel reçoit le résultat de lecture ; les anciens
participants gardent leur contrat. Une notification de résultat partagé du
cycle restaure aussi l'effet si un autre spectateur échoue après la fin locale.

NUnit natif Unity dans le projet isolé : **4/4 réussis**, rapport
`Library/DistrictCycleValidation/TimelinePresentationNative.xml`. Vérifie les
PropertyBlocks conservés, l'absence de mutation du matériau source, le retour
après arrêt/échec, le maintien invisible après réussite locale et le retour
après échec partagé. Ce n'est pas un essai réseau de la cinématique réelle.

La première application native a rencontré le parent `BrokenAnchor_Spawn`,
distinct du GameObject porteur du boss, et s'est arrêtée avant sauvegarde de
la scène. La recherche est corrigée par ascendance. Timeline et profil ont été
remis depuis le snapshot d'origine pour ne pas laisser leurs liaisons
intermédiaires dans la scène jouable ; les nouveaux clips/matériaux sont
conservés pour la reprise, sans duplication de GUID.

**Scène finale non appliquée/non vérifiée.** L'import principal attend un
rafraîchissement de l'éditeur, demandé à l'utilisateur. La reprise temporaire
est commandée par `Library/DistrictCycleValidation/BelmontReleaseAuthorV2.request`.
Elle sauvegarde les sources avant intervention, refuse une scène déjà modifiée,
vérifie tous les Transform existants et n'enregistre que Belmont et ses assets
de présentation. Retirer le script temporaire après application et validation.

Compilation du code éditeur préparé : contrôle Roslyn réussi avec les références
Unity du projet (`Library/DistrictCycleValidation/ReleaseEditorCompile.rsp`).
Régression réseau supplémentaire des répliques : deux scénarios, quatre processus
réussis, `Library/DistrictCycleValidation/Run-20261020-233035/Results.csv`.
Le hook de restauration à la désactivation a été ajouté après cette copie ;
les résultats réseau ne couvrent pas ce hook ni les nouvelles pistes visuelles.

### Mise à jour : application native réussie

L'import principal a finalement repris. L'opération ciblée V2 est terminée :
`Library/DistrictCycleValidation/BelmontReleaseAuthor.result` confirme la
Timeline de 6 secondes, les 4 pistes liées, les 2 plans et **312 Transform
existants inchangés**, parents inclus. Snapshot préintervention :
`Library/DistrictCycleValidation/BelmontBeforeRelease-20261020-213549`.
Les quatre assertions ont aussi réussi en place dans le projet principal
(`TimelinePresentationChecks.txt`). Le statut d'attente décrit plus haut est
donc historique pour cette application, pas le statut courant de la scène.

La disparition de `BrokenAnchor_Spawn` utilise maintenant la condition de
masquage de son activation après `lower_flames_restored`, sans second fondu
concurrent. Les trois Animators possèdent aussi leurs liaisons natives dans
le Director pour la prévisualisation de l'éditeur ; les IDs stables restent
la liaison runtime. Les identifiants de quête, l'Ancient Flame réelle et les
conditions des deux murs sont conservés. Les normalisations non demandées
des trois FlickeringLight ont été remises depuis le snapshot ; les trois
near planes des lumières retrouvent leur valeur initiale de 0,02.

Le script temporaire d'authoring a été retiré. Un contrôle de lecture de la scène
sauvegardée est ajouté aux tests (`BelmontReleaseSceneTests`) ; sa première
exécution est encore à confirmer après import. Le petit vérificateur temporaire
ne sauvegarde aucune scène et doit être retiré une fois son rapport obtenu.
La cinématique réelle en jeu, ses cadrages HDRP, la restauration caméra et les
cas réseau complets restent à vérifier : les tests de composants ne les prouvent
pas.

## Consolidation du 21 octobre 2026

### Scène Belmont relue après sauvegarde

`Library/DistrictCycleValidation/BelmontReleaseVerify.txt` confirme la réussite
des assertions sur la scène réellement sauvegardée : clips et durée, quatre
sorties liées, deux plans résolus, bridge caméra, suppression du second fondu
concurrent et masquage après `lower_flames_restored`. Le petit vérificateur
temporaire a été retiré. La couverture des Timelines de District_1 ne signale
plus que la configuration optionnelle incomplète de Nina.

### Rassemblement Ardent : validations côté hôte

`CycleGroupSequenceGate` exige désormais la portée d'interaction pour ouvrir
une demande, puis le rayon de rassemblement pour confirmer une demande déjà
ouverte. L'annulation distante est refusée ; un retrait non confirmé ne peut
plus réinitialiser le compte à rebours. La liste des joueurs vivants est capturée
dès la première confirmation : une arrivée avant l'Update suivant invalide aussi
les votes. Une arrivée remet les confirmations à zéro, une déconnexion conserve
celles des joueurs restants, et un simple changement d'ordre ne relance rien.
La Flame doit toujours être active au lancement. Les valeurs de rayon/durée non
finies sont refusées ; le nouvel état de rassemblement est remis à zéro au spawn.
Ces données restent temporaires et n'ajoutent aucun format de sauvegarde.

### Gestionnaire Timeline commun : contrat et nettoyage

Le contrat de couverture des pistes est partagé entre éditeur et runtime dans
`TimelineBindingContract`. Une piste active sans cible native compatible ni
binding déclaré ne peut plus réussir silencieusement. Les pistes facultatives
explicites et les pistes Audio vides restent autorisées. Les participants des
bindings natifs sont enregistrés, y compris sur un GameObject. Leur disparition,
le déchargement de scène et la perte du service entraînent une fin en échec et
la restauration des participants restants, sans appel sur un composant détruit.

### Défaut de fin naturelle reproduit et corrigé en exécutable

L'essai `Run-20261021-182204` a reproduit un vrai défaut : sous
`DirectorWrapMode.None`, Unity émet `stopped` après avoir remis le temps à zéro.
Le gestionnaire classait donc une fin normale comme `Stopped`, ce qui empêchait
la validation du souvenir. Les seuls tests d'arrêt explicite ne détectaient pas
ce problème.

La lecture narrative gérée utilise maintenant Hold temporairement, observe la
fin réelle du graphe, laisse présenter la dernière image, puis termine une
seule fois et restaure le wrap mode d'origine. Elle ne transforme jamais la
durée écoulée en réussite. Un arrêt explicite reste une interruption, même
lorsqu'il provient directement du Director.

### Preuves actuelles et limites

- NUnit natif combiné : **31/31 réussis**, zéro ignoré, dans
  `Library/DistrictCycleValidation/TimelineAndGroupNative.xml` : 14 cas du
  rassemblement, 8 du contrat de liaisons, 4 de dissolution/restauration et
  5 du gestionnaire. Ce sont des tests Edit Mode du code réellement copié,
  pas un parcours des personnages du jeu.
- Exécutable réel : **8 processus réussis** dans
  `Library/DistrictCycleValidation/Run-20261021-182941/Results.csv` et hashes
  des sources dans `SourceHashes.csv`. Six cas Timeline : fin naturelle,
  pause plus longue que la durée, arrêt par handle, arrêt direct du Director,
  perte du gestionnaire, piste sans cible. Deux processus hôte/client
  vérifient aussi la régression du dialogue. Les champs d'animation avancent
  réellement ; la notification est unique et le wrap mode est restauré.
- La relance complète dans l'éditeur principal est encore refusée par la
  protection de Bootstrap modifiée (`Library/CycleTests.result`). Aucune
  sauvegarde forcée ni abandon des modifications n'a été effectué. Le succès
  historique des 142 tests ne doit pas être présenté comme une relance récente.
- Le rendu HDRP, les cadrages et le retour de la caméra UCC dans les scènes
  complètes, les parcours de combat/navigation et les sessions multijoueurs
  complètes des cycles restent requis. Les deux décisions Nina/Gustave restent
  en attente ; la consolidation de tout District_1 n'est pas terminée.

### Navigation sauvegardée et recalage ciblé d'Étienne — 21 octobre 2026

Le manifeste utilise le dataset global
`District_1_Clairmont_NavMeshData.asset` (GUID
`de9c13f624b83ea42b77285ed67ec11e`, bake `20261019183706`). Son nom ne
signifie pas qu'il ne couvre que Clairmont. Les requêtes natives ont enregistré
uniquement ce dataset à l'origine du monde, comme le service runtime ; aucune
reconstruction de navigation ni sauvegarde de profil joueur n'a été effectuée.

Étienne et `Flame_Etienne` étaient restés dans l'ancien repère alors que le
Puits avait été déplacé et tourné. Après vérification des scènes non modifiées
et sauvegarde de sécurité, seuls leurs poses ont été recalées dans le repère
actuel de l'environnement : respectivement (-88.8, -116.24, 100.57) et
(-88.8, -116.24, 97.52). Les identifiants des objets et les transforms locaux
des autres objets sont conservés ; le fichier du décor est inchangé octet pour
octet. Rapport : `Library/DistrictCycleValidation/EtienneNavigationRebase.txt`.

Une nouvelle exportation des scènes effectivement sauvegardées, après import,
puis l'audit natif `NavigationAudit-20261021-192230.json` confirment la couverture
des deux positions et deux chemins complets depuis Le Poids mort. Le contrôle
porte sur 45 points : 27 chemins complets et 10 partiels. La racine de la petite
tombe de Jon reste à inspecter séparément. Les chemins partiels ne constituent
pas à eux seuls une preuve d'impasse jouable : les liaisons runtime, obstacles
pilotés et collisions du personnage ne sont pas simulés par cet audit isolé.

L'outil réutilisable est sous `Tools/Validation/DistrictNavigation`, hors des
scripts de production. Le correcteur ponctuel a été supprimé ; l'exporteur
temporaire est retiré d'Assets et conservé avec les outils de validation.
Restent nécessaires les parcours physiques, le rendu et les validations réseau
des scènes complètes. Ce contrôle n'atteste pas la complétion globale du chantier.

### Entrées physiques : respect du volume réel

`CycleZoneTrigger` utilisait `BoxCollider.bounds.Contains`, donc la boîte
englobante alignée sur le monde : un trigger tourné pouvait valider une entrée
à côté de son volume réel. Le contrôle s'effectue maintenant dans le repère
local du BoxCollider, avec son centre, sa taille et la transformation de ses
parents. Les colliders/composants désactivés, hiérarchies inactives, transforms
aplatis et coordonnées non finies sont refusés. L'autorité hôte, la validation
du personnage vivant et les prérequis de progression restent inchangés.

Preuve : **11/11 tests Edit Mode natifs réussis**, zéro ignoré, dans
`Library/DistrictCycleValidation/ZoneTriggerNative.xml`. Le test tourné reproduit
explicitement un point accepté par l'ancienne boîte englobante et rejeté par
le volume réel. Les hashes du script et des tests exécutés sont conservés dans
`ZoneTriggerSourceHashes.csv`. Le lanceur global inclut cette fixture ; ce
passage isolé ne prétend pas valider les parcours réseau complets.

Inspection Luc : le root du cycle est actuellement inactif. La racine de la
tombe est à l'origine, mais son modèle est décalé dans un enfant du prefab
(`Tombstone_3_Unity`). Son absence de collider sérialisé implique un fallback
runtime ; le point racine exporté ne prouve donc pas un mauvais placement du
visuel. Aucun déplacement ni réactivation de Luc n'a été fait. Confirmer
l'intention de désactivation, puis vérifier la portée réelle en jeu avant de
modifier cet agencement manuel.

### Séquences réseau : annulation distante reproduite en exécutable

Le nouvel essai `sequence_interrupt` de l'outil `CycleNetworkSmoke` a reproduit
un défaut absent des tests locaux du gestionnaire : l'échec d'un spectateur
annulait le handle chez l'hôte et remettait son champ `playback` à null, alors
que la coroutine suspendue le consultait encore au frame suivant. Le rapport
`Run-20261021-193316` conserve ce cas négatif ; la fin normale hôte/client
réussissait, mais l'interruption produisait une NullReferenceException.

Chaque coroutine conserve maintenant son propre handle et son numéro de
génération. Une annulation partagée invalide la génération : la coroutine sort
sans consulter un champ effacé et sans renvoyer un ancien résultat réseau.
Une régression Edit Mode du contrôleur réel couvre cette reprise après
`CancelPlayback`, en complément des essais en processus séparés. Les nouvelles
fixtures n'utilisent ni personnages ni porte de rassemblement ; elles ne
prouvent pas les verrous UCC ou les votes d'Ardent. Leur relance corrigée doit
être lue dans les rapports avant de déclarer ces cas validés.

Relance après correction : **six processus réussis**, hôte et client pour
`sequence`, `sequence_interrupt` et la régression de dialogue `normal`, dans
`Library/DistrictCycleValidation/Run-20261021-194130/Results.csv`. Le cas
interrompu montre sur chaque pair deux lectures réellement animées, une seule
notification d'échec, la présentation restaurée avant relance, puis une seule
réussite avec jalon répliqué. Les sources contrôlées figurent dans
`SourceHashes.csv`. Le passage intermédiaire `Run-20261021-193743` avait déjà
supprimé l'exception de production ; son dernier échec venait d'une assertion
de fixture encore appliquée après la seconde réussite côté client. Cette
assertion est désormais limitée à l'intervalle entre échec et relance.

Suite native complémentaire : **43/43 réussis**, zéro ignoré, dans
`CycleSequenceAndZoneNativeRetry.xml` ; hashes des huit sources principales et
fixtures dans `CycleSequenceAndZoneSourceHashes.csv`. Le nouveau test de
coroutine démarre effectivement le graphe, efface le champ par l'annulation
commune puis reprend l'énumérateur sans exception. La première tentative
`CycleSequenceAndZoneNative.xml` conservait un échec de fixture : en Edit Mode,
Awake du service n'avait pas été invoqué, donc le graphe n'était pas lancé.
L'initialisation explicite du service de test corrige cela sans changer le
code de production.

L'utilisateur a demandé la réactivation et le replacement des cycles concernés.
La lecture des scènes sauvegardées confirme que seule la racine Luc est
inactive ; Nina, Gustave, Belmont, Étienne, Ardent et Clairmont sont actives.
Une inspection native en lecture seule de la tombe et des Flames voisines est
préparée (`LucPlacementAuditSession`, temporaire), en attente du rafraîchissement
de l'éditeur principal. Le contrôle Windows a échoué sur capture puis activation
de sa fenêtre ; aucune scène manuellement modifiée n'a été sauvegardée et Luc
n'a pas encore été déplacé ni réactivé.
