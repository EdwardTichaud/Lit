# Acceptation de robustesse — Bootstrap, MainMenu et District_1

État : **en cours, 25 octobre 2026**. Aucun feu vert global n'est donné.

Audit initial global sur les assets enregistrés : **27 scènes inspectées,
zéro problème d'authoring détecté**. Bootstrap, MainMenu et toutes les scènes
District_1 présentes dans Assets sont couvertes. Rapport conservé sous
`Docs/Validation/2026-10-25/BootstrapDistrictSavedSceneAudit.json`.
Le contrôle après l'essai Play avait relevé une entrée Bootstrap temporaire
ajoutée par NGO (`BootstrapDistrictAfterPlayAudit.json`). Son retrait exact est
terminé : le nouvel audit natif retrouve **27 scènes, zéro issue**, dans
`BootstrapDistrictAfterCleanupAudit.json`, avec la confirmation précise du
nettoyage dans `StartupFixtureCleanup.result`.
Un premier parcours réel en Play Mode est désormais réussi : démarrage menu,
nouvelle partie solo, portail vers District_1 et retour au menu. Cela ne vaut
pas validation des six cycles complets ni du multijoueur.

## Périmètre demandé

Précision humaine du 25 octobre : vérifier **Bootstrap, MainMenu et toutes les
scènes District_1**, avant de poursuivre le reste du jeu. Cela inclut les scènes
d'environnement, d'interactions, d'ennemis, de séquences, de lumière et de
raccourcis du manifeste, pas uniquement les scènes nommées Cycle.

Maison n'est examinée ici que lorsque le démarrage normal et les portails en
font une dépendance du trajet vers District_1 ; sa campagne et les zones futures
ne font pas partie de cette acceptation. Gustave est actuellement un indice,
pas un cycle achevé à inventer pour obtenir artificiellement une validation.
Sa proposition narrative reste à approuver.

Le catalogue actuel expose six cycles jouables : Luc, Nina, Belmont, Étienne,
Ardent et Clairmont. Clairmont constitue le dernier cycle actuellement construit
dans ce parcours. Aucun prérequis de complétion d'un autre cycle ne doit être
ajouté pour remplacer la chronologie assurée par les accès et triggers.

## Ce qu'un résultat vert doit prouver

| Porte d'acceptation | Preuve requise | État actuel |
| --- | --- | --- |
| Démarrage Bootstrap → MainMenu | Build ou Play réel, services uniques, menu contrôlable, aucun chargement infini | Play réel réussi le 25 : menu atteint ; commandes des handlers exercées, saisie manuelle non testée |
| Nouvelle partie et Continuer | Création d'une sauvegarde de test isolée ; personnage contrôlable ; données du bon slot | Nouvelle partie solo réelle réussie et 32 tests de sessions/transactions ; Continuer depuis disque reste à tester |
| Session privée | Création, code invalide, annulation, connexion, perte d'hôte ; menu toujours récupérable | Parcours avec vrais personnages manquant |
| Chargement District_1 | Toutes les scènes obligatoires du manifeste, spawn, caméra, input, navigation, absence d'erreur bloquante | Portail réel réussi le 25 ; scènes du manifeste chargées, navigation prête, personnage au sol et mode exploration ; rendu et chemins complets non prouvés |
| Luc | Deux ordres d'indices, portée, lumière, dialogue, dissolution ; après Belmont terminé aussi | Placement et tests de scène vérifiés le 24 ; partie réelle à accepter |
| Nina | Lettre/combat dans les deux ordres, Scar/Cicatrice uniques, souvenir facultatif sans verrou caméra résiduel | Timeline et tests vérifiés le 24 ; rendu et partie réelle à accepter |
| Belmont | Trois preuves ; Éloïse ; ShadowGuardian ; trois torches dans tout ordre ; séquence ; Ancient Flame ; Wall_2 | Contrats et tests partiels ; parcours et récupération après échec à accepter |
| Étienne | Entrée, preuves parallèles, frein, boss, souvenir relançable, fantôme, accès persistant | Contrats et tests partiels ; parcours et frein en réseau à accepter |
| Ardent | Enquête/réglages parallèles, groupe dans l'arène, fenêtres de vulnérabilité, votes, souvenir, Nora, sortie | Protocole partiel ; avatars, rassemblement et progression complète à accepter |
| Clairmont | Entrée, deux preuves, registre, Agnès, réplique finale et accès après déchargement | Définition inspectée ; parcours réel à accepter |
| Toutes les autres scènes District_1 | Levers/portails/ennemis/Flames accessibles ; pas de passage requis irréalisable ni de chute sans sortie | À parcourir et classer |
| Sauvegarde et réentrée | Rechargement avant/après chaque jalon et après décès ; récompenses uniques, accès conservés | Tests de service partiels, parcours complets manquants |
| Dev | Aucun événement de test écrit dans les sauvegardes réelles | Tests ciblés existants ; tous les départs à reprendre |
| Retour au menu | Contrôles libérés, gameplay déchargé, nouvelle session propre sans état résiduel | Réussi depuis District_1 au début d'une partie neuve ; autres grandes phases à tester |

Les vérifications solo doivent être complétées par un hôte et au moins un client
dans les scènes réelles. Les essais NGO isolés ne remplacent pas cette preuve.

## Constats actuels de configuration

- Les Build Settings commencent par Bootstrap puis MainMenu ; les scènes
  District_1 sont déclarées et le manifeste référence ses sous-scènes.
- Bootstrap possède `loadMenuAfterBootstrap = true`, mais aussi un démarrage
  direct éditeur activé. Ce dernier court-circuite volontairement MainMenu en
  Play depuis Bootstrap. Ne pas le confondre avec un test du menu ; ne pas
  écraser cette préférence de travail sans accord.
- Les Flames et torches de puzzle sont exemptes du verrou d'interaction lié à
  la lumière ; une zone sombre n'interdit donc pas par principe de rallumer sa
  source. Cela ne prouve pas que les ressources d'allumage suffisent au parcours.
- La configuration actuelle du code impose un coût minimal de deux charges aux
  Ancient Flames. L'ancien constat « Ancient Flame gratuite » ne doit plus être
  réutilisé comme preuve d'absence de blocage de ressources.

## Défaut reproduit et corrigé : tentative de cinématique obsolète

La coroutine hôte `DeathSequence` surveillait seulement le booléen partagé
`cinematicRunning`. Après une interruption, une nouvelle lecture peut commencer
avant la reprise de l'ancien énumérateur ; celui-ci surveillait alors la nouvelle
tentative et pouvait lui appliquer son ancien délai d'expiration.

Le test `OldHostSequenceCannotWaitOnOrCancelANewerAttempt` lance un vrai graphe
natif, interrompt sa première présentation, puis installe un nouveau token et
un nouveau graphe avant de reprendre l'ancienne coroutine. Avant correction :
**6/7 réussis, un échec reproduisant l'ancienne attente** dans
`Library/DistrictCycleValidation/Run-20261025-115159/DialogueTests.xml`.

La boucle et son annulation de fin vérifient maintenant le token capturé.
Après correction : **7/7 réussis**, aucun ignoré, dans
`Library/DistrictCycleValidation/Run-20261025-115316/DialogueTests.xml`.
Le test exige que l'ancienne attente se termine sans arrêter le nouveau graphe
ni produire une notification d'échec supplémentaire. Les sources testées sont
identifiées par `SourceHashes.csv`. Aucune scène ni sauvegarde réelle modifiée.

## Vérification des sessions et sauvegardes du menu

**32/32 tests réussis**, zéro ignoré, le 25 octobre, dans
`Library/DistrictCycleValidation/Run-20261025-115904/DialogueTests.xml`.
Classes exécutées : `MainMenuSessionTests` et `SaveTransactionTests`.
Elles couvrent les réservations distinctes de personnages, les états de salon,
les notifications de connexion répétées, les slots et fins de séquences, le
rejet de sauvegardes ambiguës, les transactions de trois fichiers, les fichiers
verrouillés et la récupération après publication interrompue. Leurs fichiers
sont créés dans des dossiers temporaires isolés, pas dans les sauvegardes Lit.

Cette passe ne charge pas le décor du menu et ne démontre ni navigation visuelle,
ni focus des boutons, ni démarrage réel du personnage. La matrice demeure ouverte
sur ces points ; aucun résultat de tests de modèle n'est présenté comme un parcours
complet Bootstrap → MainMenu → District_1.

## Reprise Belmont après échec — correction du 25 octobre

L'absence d'action de relance a été corrigée dans l'interaction existante des
`BrokenAnchorTorch`, sans changer les scènes ni ajouter un générateur. Après
échec de `anchor_release`, interagir avec une torche allumée peut demander une
nouvelle lecture. La liaison se résout depuis les `CycleFlameBinding` du même
contrôleur et de la même scène, pas depuis un singleton global. Un seul retour
local non modal indique l'action à effectuer après une interruption.

En réseau, le client ne rapporte aucun succès : un RPC demande une reprise et
l'hôte résout le véritable expéditeur, sa portée, son personnage vivant/actif,
son accès aux interactions et l'état de la séquence. Le contrôleur existant
refuse un double réarmement, une lecture en cours et une étape déjà terminée.
Les torches restent allumées, le boss reste vaincu et la réussite de la Timeline
reste indispensable à la progression.

**13/13 tests réussis**, zéro ignoré, dans
`Library/DistrictCycleValidation/Run-20261025-123358/DialogueTests.xml` ; le lanceur
éditeur corrigé est également compilé dans cette copie isolée. Les tests couvrent
l'interaction normale, le double appel, l'absence d'échec, le joueur mort/éloigné,
les composants désactivés, une torche éteinte, une lecture en cours, l'absence de
liaison, un souvenir déjà terminé et une autre séquence. Aucune progression n'est
synthétisée et le mode Dev n'écrit rien dans les règles persistantes.

La passe précédente `Run-20261025-122033` était également verte à 13/13. La
dernière relance compile aussi l'auditeur en lecture seule et conserve les mêmes
assertions après le garde-fou empêchant un faux message d'interruption pendant
la désactivation normale d'un cycle terminé.

La première passe était verte à 11/11. Le passage intermédiaire
`Run-20261025-121832` contient un échec de fixture parmi 13 tests : la simulation
Dev n'était pas initialisée avant d'y injecter une étape terminée. La fixture
initialise maintenant cet état via la requête publique, sans changer la règle
de production pour obtenir du vert.

**Limite :** ces tests ne qualifient pas encore l'interaction RPC avec les vrais
avatars, les cadrages et la relance dans la scène rendue Belmont. Cette case de
la matrice n'est donc pas entièrement verte.

## Suite dans l'éditeur principal — résultat à portée limitée

Le lanceur existant a exécuté 196 tests, zéro échec/ignoré, le 25 octobre.
Copie conservée : `Library/DistrictCycleValidation/MainCyclesSyncPreRefresh-20261025.xml`.
Cette exécution utilisait les assemblies importées à 11:55, antérieures aux
nouveaux scripts de reprise. Son mode synchrone excluait en outre les `[UnityTest]`
multi-frames du filtre, sans les compter comme ignorés. Elle ne remplace donc
pas une suite complète à jour et ne doit pas être additionnée aux anciennes
passes en prétendant une validation globale.

`CycleTestRunner` utilise désormais une scène vide temporaire additive comme
scène active de fixtures, puis la ferme en rétablissant la scène active initiale.
Il conserve le refus de commencer si une scène utilisateur est modifiée et ne
sauvegarde rien. L'exécution est asynchrone pour inclure les tests multi-frames.
Ces changements sont compilés dans la copie de validation puis ont été importés
par le projet principal. La suite asynchrone complète a ensuite terminé dans
l'éditeur principal le 25 octobre à 12 h 43 : **211 réussites, zéro échec,
zéro ignoré**. Le rapport contient bien les tests multi-frames, dont le passage
anticipé du dialogue et le graphe natif du souvenir de Nina, ainsi que les
13 tests de récupération Belmont. Preuves conservées dans
`Docs/Validation/2026-10-25/FullCycleSuite.xml` et `FullCycleSuite.result`.

Ce résultat ne prouve pas à lui seul la conservation des flags dirty de chaque
scène par toute la suite : le titre de la fenêtre Bootstrap affichait encore une
étoile ensuite. L'utilisateur a depuis indiqué avoir enregistré volontairement
ses scènes ; le prochain outil vérifie directement `Scene.isDirty` avant de
changer la disposition des scènes. Aucun enregistrement automatique de Bootstrap
n'est autorisé.

Le contrôle Windows a identifié Unity mais échoué en capture (`FrameArrived
timed out`, puis `window capture timed out`) et en activation
(`computer-use request timed out: activate_window`). Aucun clic aveugle, Play,
enregistrement ou fermeture de scène n'a été effectué. La demande de tests a
été consommée et terminée ; aucun fichier de demande de test n'est laissé en
attente. L'utilisateur a été sollicité pour Refresh et sauvegarde volontaire.

## Audit natif en lecture seule des scènes enregistrées

Le nouvel outil `Lit/Validation/Audit Saved Bootstrap MainMenu District_1` a été
exécuté dans le vrai éditeur principal le 25 octobre. Il ouvre uniquement des
scènes Preview, les ferme sans sauvegarder et compare ensuite la scène active,
l'ordre des scènes initialement ouvertes, leurs flags dirty et les SHA256 des
fichiers inspectés. **27 scènes, zéro issue, états de travail et fichiers inchangés.**

Il a vérifié Bootstrap (services uniques et références de démarrage), le
validateur d'authoring MainMenu existant, la déclaration des scènes de District_1
dans les Build Settings et le manifeste, les scripts manquants dans toutes les
hiérarchies, les identités/définitions des cycles et la couverture des liaisons
Timeline. Une comparaison avec les fichiers `District_1*.unity` d'Assets n'a
trouvé aucune scène District_1 supplémentaire hors de cette inspection.

Rapport brut : `Library/StartupDistrictValidation/SavedSceneAudit.json`, copié
dans `Docs/Validation/2026-10-25/BootstrapDistrictSavedSceneAudit.json` pour rester
disponible après nettoyage de Library. `fullPlaythroughValidated` est explicitement
**false**. Les références non couvertes par ces validateurs, le rendu, la portée
réelle des interactions, les chemins, ressources, contrôles et le réseau natif
de scènes demeurent des portes d'acceptation ouvertes.

## Deux risques supplémentaires à qualifier, pas à masquer

- **Arrivée tardive réseau dans District_1 :** l'essai réel du 24 octobre
  `Library/PersistenceContinuityValidation/NetworkRuns/9e10931ebdff44f693b7e21a76637e23`
  était en échec sur la synchronisation de scène NGO :
  `VigilAltar_Hub_Safeguard`, hash `1275473975`, puis erreurs de désérialisation.
  Les traces originales ont été relues. Aucun essai récent avec les scènes
  réelles ne prouve encore sa correction. Cela concerne l'accès réseau à
  District_1, même si l'objet identifié appartient à une sous-scène Maison
  conservée pour les arrivées tardives. Les dix processus de protocole isolé
  ont la gestion de scènes désactivée et ne peuvent pas valider ce problème.
- **Refus de restauration pendant un portail :** le refus de capture avant
  déchargement est désormais corrigé et testé dans les deux directions (voir
  plus bas). Les sorties après échec de `RestoreArchivedScenes` ou de préparation
  post-loading n'ont pas encore de preuve de récupération. Le refus de
  persistance doit rester strict ; il faut reproduire ces autres cas et revenir
  proprement au menu sans sauvegarder un monde partiel, ou préserver une zone
  utilisable lorsqu'aucun état incomplet n'a été appliqué.

## Priorités restantes

1. Les cas réseau isolés viennent d'être rejoués : **10 processus réussis**,
   hôte/client pour `normal`, `skip`, `interrupt`, `sequence` et
   `sequence_interrupt`, dans `Run-20261025-115533/Results.csv` sous le dossier
   local de validation. Le passage volontaire termine la première tentative
   sans répétition ; l'interruption technique nécessite une nouvelle tentative.
   Les deux pairs animent réellement la Timeline et valident une seule réussite.
   Ce résultat ne prouve ni les verrous des personnages, ni Cinemachine dans
   les scènes finales, ni le rassemblement d'Ardent.
2. Vérifier dans Belmont la récupération **en jeu** après échec de
   `anchor_release`, désormais reliée aux torches existantes. Les tests ciblés
   sont verts, mais le parcours rendu et la demande client réelle restent à accepter.
3. Le démarrage solo et le portail vers District_1 sont désormais réussis avec
   un profil isolé. Poursuivre par Continuer depuis disque, les refus de transition
   et les parcours de cycles, sans écraser les sauvegardes de l'utilisateur.
4. Classer chaque case de la matrice avec un rapport récent et sa portée exacte.
   Un succès de compilation, de manifeste ou de tests unitaires ne démontre pas
   un parcours jouable sans hardlock ou softlock.

## Parcours réel solo du 25 octobre

`BootstrapDistrictPlayProbe` a exécuté le vrai chargement dans l'éditeur principal,
avec une copie temporaire de Bootstrap et un dossier de sauvegarde absolu propre
à cet essai. Il refuse de commencer si une scène ouverte est modifiée ou non
enregistrée. Aucun original n'est sauvegardé ni annulé.

Résultat conservé : `Docs/Validation/2026-10-25/BootstrapDistrictSoloPlay.json`.
**11 contrôles réussis**, retour au menu et restauration du setup initial terminés.
Les SHA256 des fichiers de scènes d'origine sont identiques avant et après.
Les options de Play Mode et la scène de démarrage de l'éditeur ont été restaurées.
Cela ne garantit pas que les callbacks d'édition ne remarquent pas Bootstrap
dirty lorsqu'il est rouvert ; aucun flag dirty n'est effacé artificiellement.

Le parcours utilise les handlers publics du vrai menu pour choisir Solo, saisir
un nom de test et confirmer la nouvelle partie. Une fixture de placement UCC
approche ensuite le vrai portail sur un sol physique dégagé ; le portail conserve
ses validations de production. Maison, puis toutes les sous-scènes du manifeste
District_1 sont chargées, la navigation devient prête et le personnage est au sol
en mode exploration. Une caméra écran est active à chaque arrivée. Le retour au
menu sans sauvegarde de sortie libère bien le personnage contrôlé.

**Limites :** aucune marche manuelle ni appréciation visuelle, aucune victoire ou
énigme de cycle, aucune réentrée depuis une sauvegarde existante, aucun pair réseau.
La présence d'une caméra et l'état Ready du NavMesh ne prouvent ni le cadrage,
ni la qualité du rendu, ni tous les chemins. Le rapport conserve donc
`fullProductionReadiness=false`.

Le journal natif mentionne un script inconnu manquant lors du chargement de
`Temp/__Backupscenes/0.backup` avant le démarrage ; ce warning n'est pas expliqué
par cet essai et n'est pas masqué par un correctif arbitraire. Le contrôle natif
des 27 assets enregistrés réalisé après Play n'a relevé aucun MonoScript manquant,
mais a trouvé une entrée Bootstrap temporaire ajoutée par la confirmation NGO.
Son retrait exact est préparé, sans reconstruire les listes de scènes ni toucher
aux entrées utilisateur. Le nouveau lanceur inscrit désormais sa copie avant
Play et retire seulement cette entrée à la fin afin d'éviter ce dialogue.
La demande de nettoyage a terminé et a été consommée. Elle n'a relancé ni Play
ni sauvegarde de scène ; seule l'entrée du fixture terminé a été retirée.

Compilation du lanceur initial et de sa correction de nettoyage : succès dans
la copie code-only, journaux conservés dans
`Docs/Validation/2026-10-25/BootstrapDistrictPlayProbeCompile.log` et
`BootstrapDistrictPlayProbeCompileFinal.log`.
La vérification Windows a échoué en capture (`FrameArrived timed out`) puis en
activation (`activate_window timed out`). Aucun clic aveugle n'a été tenté.

## Départs refusés : reproduction, correction et nouvelle anomalie trouvée

Les deux coroutines de départ (portail vers une zone et retour Maison) ont été
exercées dans la copie code-only avec un vrai provider persistant qui lève une
exception. Avant le correctif : **0/2**, l'overlay restait opaque. Après :
**2/2**, avec capture réellement refusée, présentation et propriétaires de
zone conservés, overlay libéré et snapshot de caméra restauré. Rapports
`DepartureBeforeFix.xml` et `DepartureAfterFix.xml`.

Le code archive désormais avant d'arrêter le streaming ou de libérer la
présentation. En cas de refus, il reconstruit la navigation de la même zone,
ferme l'overlay et réarme son post-chargement, sans vider les focus de dialogue
ou combat, sans décharger le monde et sans écrire une sauvegarde partielle.
Les tests EditMode injectent l'état de départ de l'overlay : ils ne démontrent
pas eux-mêmes un fondu ni une navigation physique rendus.

La recette Play `recovery` a ensuite confirmé **dans les vraies scènes** les
deux refus exacts et la restitution du même avatar, de la caméra, du contrôle
et du NavMesh. Le nouvel essai après retrait du fixture a bien atteint
District_1. La seconde nouvelle tentative a chargé Maison, mais a révélé un
autre défaut : `ReturnToHub()` sans ID ne repositionnait pas le groupe et gardait
ses coordonnées District_1. Lucian était alors sous le sol de Maison et tombait.
La validation à l'étape 42 a été annulée, sans la déclarer réussie.

Preuves de ce constat : `DepartureLiveBeforeSpawnFix.json` et
`ReturnHubMissingSpawnBeforeFix.txt`. Le correctif de retour choisit désormais
le spawn de la scène Maison lorsque l'ID est absent/introuvable. Il est limité
au voyage et ne change pas la restauration des positions de sauvegarde.
La recette rendue complète doit encore être rejouée après cette modification.

Elle a depuis été **rejouée avec succès** : `DepartureLiveAfterFix.json`,
28 contrôles réussis, exactement deux refus d'identité attendus, zéro erreur
inattendue. Les nouvelles tentatives atteignent District_1 puis Maison ; Lucian
retrouve un sol valide, la navigation est prête, le mode exploration est actif,
puis le retour au menu et la restauration des scènes initiales terminent.
Les SHA256 des fichiers de scènes d'origine restent identiques. Le probe ne
valide ni la marche manuelle, ni les cycles complets, ni les erreurs de
restauration après déchargement. Sources de cet essai : `DepartureSourcesFinal.csv`.

Après ce parcours, le contrôle natif retrouve encore **27 scènes / zéro issue**
(`BootstrapDistrictAfterRecoveryAudit.json`). La suite complète a également
été rejouée après les corrections de départ et QTE : **211/211**, zéro ignoré,
dans `FullCycleSuiteAfterDepartureFix.xml` et son `.result`. Aucun fichier de
demande de test ou de Play ne reste en attente. Le titre de Bootstrap est de
nouveau marqué après cette suite : le correctif QTE est prouvé isolément, mais
la suite entière ne démontre toujours pas la conservation de tous les flags
dirty. Aucun enregistrement ni effacement de flag n'est fait pour le masquer.

Deux warnings restent visibles dans cet essai : arbre utilisant un ancien
shader Soft Occlusion, et tentative de création d'agent sans NavMesh à proximité
de l'initialisation du Scientifique. Ils ne sont pas comptés comme une preuve de
bon fonctionnement de tous les ennemis ; cette investigation reste ouverte.

## Recompilation et état modifié de Bootstrap

`QTEPanelController.OnValidate` appelait systématiquement `MarkSceneDirty`,
même lorsque références et valeurs sérialisées étaient identiques. La fixture
isolée reproduit ce comportement : la validation inchangée échoue tandis que
le remplissage de valeurs manquantes fonctionne. Le correctif compare l'état
sérialisé avant/après, et ne marque la scène que lors d'un changement réel.
Il ne retire aucun flag dirty d'une scène déjà modifiée.
Les deux tests QTE et les deux tests de départ sont maintenant réussis ensemble :
**4/4**, compilation du probe actualisé incluse, dans `DepartureAndQTEFinal.xml`.
Le rouge QTE précédent (une réussite et un échec sur validation inchangée) reste
conservé dans `QTEValidationBeforeFix.xml`. Cela ne constitue pas encore la preuve
que tous les composants de Bootstrap ont des validations Editor idempotentes.

L'éditeur a également crashé pendant son Refresh du 25 octobre à 13 h 20. La
pile conservée dans `EditorCrashInitializationExcerpt.txt` passe par
`System.Xml.Serialization` puis l'initialisation de Unity Version Control /
PlasticSCM. Cela localise l'exécution au moment du crash, sans démontrer sa
cause profonde. Aucun paquet n'a été désactivé ; aucun second éditeur n'a été
lancé sur le projet source. Le projet a été rouvert et l'audit natif a ensuite
terminé normalement.
