# Narration et connaissances

## Direction : labyrinthes de fracture (2026-11-01)

Audit de realisation : Docs/Design/Premier_Labyrinthe_Audit.md. Scene de test
independante choisie, pas de raccord campagne. FractureAttempt modele les faits
de tentative hors services persistants. Graybox solo creee et importee,
13 tests natifs de modele/raccords passes ; parcours physique UCC et interactions
au gamepad simule passes. Preview capturee/inspectee, controle humain restant.
Scene, controles provisoires et limites : systems/fracture_labyrinth.md.
Cela ne qualifie pas le jeu complet ni NGO. Le RPC actuel de revelation n'annonce pas le revelateur au HUD :
cette partie du contrat doit etre raccordee, pas supposee livree.

Plan de prototype : Docs/Design/Premier_Labyrinthe_Plan.md. Il sépare les
contraintes actées du contenu proposé, décrit Savoir/préparatifs/parcours
solo-multi et les jalons d'acceptation. Ce plan n'est pas une livraison runtime.

Lore.md et GameDesign.md définissent des anomalies rejouables issues de la
fracture existante : la manifestation du lieu/gardien peut se reformer sans
annuler les résolutions narratives ni les Savoirs persistants. Époque commune
à toute l'équipe, aucun retour arrière global de campagne. Boss : puissance
latente accumulée sans lumière, mais activation toujours conditionnée à une
influence de Flame. La lumière arrête la croissance sans effacer automatiquement
l'acquis ; préparatifs facultatifs contre coût de temps. Cela ne modifie pas
le rôle des Ancient Flames. Découverte initiale puis reprises chronométrées,
temps labyrinthe+boss, contenu essentiel solo, score collectif et victoire
toujours récompensée. Économie XP/loot et paramètres de croissance à arbitrer.
Direction documentaire uniquement : aucun système runtime d'expédition, de
reformation, de chronométrage ou de croissance du boss n'est livré ici.

## Marge avant dechargement de Luc

`LucCycle.completionUnloadDelay` vaut5 secondes et se regle dans son Inspector,
rubrique Fin et dechargement. Le compteur en secondes reelles commence apres
dialogue, dissolution et confirmations des clients ; il se reinitialise si la
presentation redevient bloquante ou si le cycle n'est plus termine. Les autres
definitions gardent0 par defaut. Le dechargement utilise toujours GameFlowService
et ses controles de scene additive/autorite/capture de l'etat, sans detruire le
joueur ni les services de session. Le timeout de presentation reste distinct.
Validation native `c4739d26a4f6404694f04303e55ffcd3` :33 controles,zero erreur,
restauration terminee ; scene presente a la fin du dialogue puis dechargee apres
presentation et marge, controles/collier/recompense conserves. Cela ne qualifie
pas encore un nouveau parcours complet avec sauvegarde/reprise.
Tests cibles62/62 passes (`Logs/LucCompletionMarginTargetedTests.xml`). L'echec
Clairmont de la premiere suite elargie est corrige le30 octobre : l'ancien test
ne creait pas identite, variables persistantes et manager de savoirs necessaires
au passage migre. Le montage isole utilise maintenant le vrai adaptateur et une
reussite de jet deja enregistree pour tester livraison du savoir puis permissions
aux deux extremites ; jet frais/echec/non-repetition gardent leurs tests dedies.
Aucun asset ni comportement runtime modifie. Tests passages35/35 puis suite
elargie143/143 (`Logs/ClairmontCycleBroadRegressionTests.xml`).

## District_2 — Stillhaven, continuite de Moonrise (2026-10-28)

Stillhaven est le nom public du deuxieme district, avec le cycle Mirage.
Les scenes District_2_Courts* et leurs IDs restent inchanges. Entree libre
depuis District_1_SecretNotes, retour vers Moonrise et sortie vers les Limbes
conditionnee par la conclusion locale ; ni Clairmont ni les vingt notes ne
bloquent l'entree. Les sauvegardes affichent District 1 — Moonrise et
District 2 — Stillhaven ; arrivee et chargement identifient aussi Stillhaven.

Le manifeste District_2 est maintenant dans zoneManifests du GameFlowService
de Bootstrap : sans cet enregistrement, un voyage par portail pouvait marcher
alors qu'une reprise de sauvegarde ne resolvait pas le groupe de scenes.
L'outil auteur enregistre egalement ce manifeste lors d'une nouvelle creation.
La verification native controle l'unicite de l'inscription et les Build Settings.
La recette DistrictConnection inclut sauvegarde production dans District_2,
retour au menu, StartOrLoadGame sans destination imposee et verification des
cycles, passages, faits Read et souvenirs apres rechargement depuis disque.
Validation native : 90 controles auteur ; Play
1bdb8179ca59482785e018130819f4ee passe/restaure avec 223 assertions et aucune
nouvelle erreur runtime. Reprise du groupe complet et progression preservee.
Approches/dialogues/degats sont pilotes ; ne valide ni parcours manuel, ni
reseau, ni redemarrage du processus. Les prefabs D1 manquants preexistants
restent signales separement (66 messages). Rapports : Library/District2Integration.
Regression native : 53/53 tests passes, aucun echec/ignore.

## Passages auteurs et cycle des notes (2026-10-27)

SecretPassageCatalog sous Resources/Narrative relie vingt identites de notes au
cycle transversal SecretNotesCycle. Dix reservations futures n'ont ni definition
physique ni contenu invente. Dix passages sont installes : deux D1, six Cours et
deux Limbes. Un seul D1 etait ancien ; l'utilisateur a valide la creation du
second, de l'entree du quartier au couloir ouest, avec note dans les Limbes.

SecretPassageNotes recoit uniquement l'ouverture reussie d'un livre/parchemin
du catalogue. Le chemin serveur verifie lecteur vivant et possession ou note
active a portee, puis soumet un Read au CycleProgressionService de session.
Le RPC ne prend aucun acteur/compteur client. Les faits Read existants sont
idempotents, sauvegardes et repliques comme les autres cycles. Observateur ne
passe jamais par ce chemin. Le titre du groupe de connaissances montre x/20
notes lues, sans assimiler le nombre de connaissances au nombre de lectures.
Lire une note deja connue par Observateur peut donc encore avancer le cycle.

RestoreDetectedState signale a KnowledgePassageDiscovery l'ancien etat connu.
Son import autoritaire accorde uniquement le savoir du passage, sans jet ni
fait Read. Les identifiants et hierarchies anciens ne changent pas. Les nouveaux
passages utilisent deux extremites et une seule definition ; les grilles de
presentation ne portent pas de collision solide bloquant le moteur.
Leurs dialogues playOnce sont propres a chaque passage, sans voyage camera.

Scenes District_2_Courts_Secrets, Reve_Limbes_Secrets et District_1_SecretNotes
ajoutees aux manifestes. Notes placees hors de leur propre passage. Un recoin
facultatif accessible par Sauf-Conduit porte un episode familial supplementaire.
Sources auteur explicites : SecretPassagesAuthoring, jamais appelees au Play.
Regression native finale : 44/44 tests passes, aucun echec/ignore ; audit des
neuf passages : 77 controles. Recette Play auteur
fcf774f6b1a7493887585fbcc850a9d0 passee/restauree, 174 assertions, zero erreur
ou warning : huit lectures de notes, huit nouveaux passages en aller-retour UCC,
portail reel Cours -> Limbes, maintien des faits et du playOnce, episode facultatif.
Les resultats Observateur etaient des fixtures persistees ; le vrai d6 est teste
par la suite unitaire. Approches et dialogues pilotes, pas de partie manuelle.
L'import legacy attend la fin du chargement/transition pour ne pas etre efface
par la restauration ulterieure des savoirs. Prefabs D1 manquants preexistants,
recette reseau et save froide toujours ouverts.

Acces D1 -> Cours valide libre des sa decouverte : PortalController permanent
dans District_1_SecretNotes, sans CycleController, condition Clairmont, competence
ou nombre de notes. Quatre destinations Cours explicites. Le retour Cours -> D1
et la sortie Cours -> Limbes sont preserves ; cette derniere attend la conclusion
locale comme auparavant. CompleteDistrictConnection est un outil auteur unique,
jamais automatique. Emplacements verifies sur les collisions du District complet,
sans remplacer ses prefabs manquants. Audit actualise : 87 controles. Circuit
Play d9b24819aa944d898a704580f5deb0aa passe/restaure, 210 assertions : acces libre
avec Clairmont incomplet, dix lectures et dix passages en aller-retour UCC,
retours de campagne conserves. Les 66 erreurs d'assets D1 connues restent dans
preexistingDistrictAssetErrors ; aucune erreur nouvelle/inattendue, mais pas de
certification D1 globale. Le retour repete a aussi necessite la correction du
registre des acteurs conserves inactifs (voir persistence.md), regression 45/45.

## Cours interieures — contenu local approuve (2026-10-27)

Famille Mirage : Alix, sa mere Agathe et son pere Abel (noms valides le
2026-10-28 ; Abel remplace le role de grand-pere). Repartition 2 D1 + 6 Cours + 2 Limbes + 10
futurs valides. Scenes natives sous Scenes/District_2/Courts, sans remplacement
du prototype WhiteCastle. Cinq CycleDefinitions independantes dans Resources :
family, visits, reserve, margins, finale. Pas de CycleCompleted entre definitions.
Les objets de scene et preuves restent dans le Core apres completion des cycles.
Neuf documents donnent leur savoir uniquement via la lecture existante ; aucun
deblocage au ramassage. Six StorySequenceRunners playOnce utilisent les outils
existants. Leur contenu ne revele pas les secrets reserves aux Limbes.

CourtsProgression projette les etats persistants sur les deux obstacles et la
sortie finale. L'acces principal n'exige ni Observateur, ni Sauf-Conduit, ni cycle
des vingt notes. CourtsGargoyleGate retient les decisions/degats jusqu'a l'intro ;
le seul cerveau est Brains et la condition Flame reste standard. SceneMarker
conserve l'acteur et sa defaite. Le declencheur de souvenir reutilise
LimbesMemorySequence.primarySceneName ; defaut Reve_Limbes preserve.
Authoring explicite uniquement via CourtsAuthoring ; aucune generation au Play.
Verification auteur native : 57 controles passes. Recette Play locale
96bc5b9f00714d93aec44e0af735a0f1 passee/restauree, zero erreur : neuf vraies
ouvertures de documents, souvenirs, actions, cinq cycles, defaite Brains,
conclusion et retention apres rechargement. Approches/avance de texte/degats
sont des fixtures, pas une partie manuelle ni une preuve d'equilibrage.
Passages et notes transversales ont maintenant leur recette dediee ci-dessus.
Les raccords complets D1 et le multijoueur ne sont pas encore qualifies.
Le rendu initial sans Volume HDRP etait surexpose. InstallLighting reutilise
uniquement le Volume local et la lumiere diffuse des Limbes, dans l'Environment
des Cours, sans copier leurs effets/decors. Seconde recette
fc7429f15af74b898a9eadb49aba4bcc passee/restauree : 105 assertions, zero erreur
ou warning runtime, capture de la vraie camera examinee. Finition artistique
et parcours continus restent ouverts : il s'agit d'un blockout fonctionnel.

## Journal classe par cycles — chantier zone 2 (2026-10-27)

KnowledgeCycleGrouping projette uniquement les connaissances debloquees en
groupes de cycles. Les etapes de connaissance, recompenses Knowledge, prerequis
et conditions de dialogue fournissent les liens existants. KnowledgeSO.cycles
permet un lien explicite supplementaire, notamment pour les futures notes des
passages. Une meme connaissance peut apparaitre sous plusieurs cycles, toujours
avec la meme reference ; aucun savoir ni jalon n'est accorde par ce classement.
Les connaissances non liees restent dans Autres connaissances, en fin de liste.
Seuls les groupes non vides sont affiches, titres/entrees tries alphabetiquement.

KnowledgePanelController reutilise les slots et ajoute des titres TMP non
interactifs, pools et abonnes aux changements du manager et du service de cycles
pendant son activation. Aucun etat UI supplementaire dans les sauvegardes ; aucun
ID existant change. Compilation runtime/Editor et 9 tests natifs passes, dont
un test du panneau reel en Play (classes, descriptions, refresh, reouverture,
retrait). Pas de validation visuelle du vrai menu Bootstrap par cette fixture.

SecretPassageAttemptLedger porte la regle d6 2+ sans interface ni ecriture de
connaissance. L'appelant autoritaire devra verifier proximite, capacite equipee
et identite stable du personnage. Chaque couple passage/personnage conserve le
seul resultat succes/echec dans WorldRulesStateManager, donc le snapshot monde
habituel. Aucun ID client ou objet Unity transitoire ne doit servir d'identite.
Connaissance deja acquise : aucun jet. Succes deja tire : livraison du savoir
reessayable sans nouveau jet. Seize tests natifs couvrent seuils, repetitions,
restauration, independance, absence de capacite et identites non ambigues.
Rapport commun : Library/Zone2FoundationTests.xml, 25 passes, zero echec/ignore.
KnowledgePassageDiscovery raccorde ce registre aux instances de TrouEtroit qui
portent ce composant. SecretPassageDefinition lie identite stable, connaissance
et note ; HasReadLinkedNote verifie lecture sans attribution au seul ramassage.
Le serveur derive le personnage depuis son acteur controle, jamais un ID client
declare. WorldInteractionService garde ses controles de proximite et de capacite.
Un accuse cible memorise localement la tentative pour supprimer les requetes
repetitives ; lire la note reste possible apres un echec. Les savoirs passent par
KnowledgeReveal/KnowledgeSynchronizationService, pas un nouvel inventaire.

Les nouvelles instances reconnaissent aussi un personnage deja present dans le
volume, aux deux extremites ; outline limite a sa proximite. La presentation
locale utilise un StorySequenceRunner playOnce auteur, sans texte de secours.
Sortie du volume/desactivation annule la lecture possedee ; seules ses fins
reussies passent dans le store existant. Sauf-Conduit demeure une condition
individuelle de traversee. Les instances sans cet adaptateur gardent le contrat
historique jusqu'a une migration explicitement choisie.

Essai Play ff6068aeabc44800995baf13b2a4bacc reussi/restaure avec vrai Lucian/UCC,
echec conserve, savoir via fixture KnowledgeReveal, reconnaissance automatique,
replique unique, refus puis traversee apres equipement individuel, et retour menu.
Trente-sept tests cibles passes, aucun echec/ignore ; outline de proximite et
accuse propre a chaque personnage inclus. Rapport :
Library/Zone2DiscoveryVerifiedTests.xml. La regression de scenes Clairmont signale des prefabs manquants
dans les scenes Nef/AbilityShortcuts non modifiees. Aucun feu vert global D1.
Pas de recette client/cooperation, de vingt notes auteur, de save froide ni de
zone 2 complete : histoire familiale et repartition validees, construction en cours.

## Extension narrative des Limbes (2026-10-27)

Reve_Limbes_Progression porte trois branches 3D permanentes, les presentations,
le pont d'arene, la Gargouille angelique et le portail de victoire. Creation,
Captivite et Invocation ont chacune une scene additive de cycle, trois preuves
recuperables et un souvenir au sommet. Les cinq cycles d'exploration, Seuil et
Chambre inclus, sont requis pour le pont final. L'ancien detour Chambre compte
donc maintenant pour la fin de cette zone. Aucun cycle n'a de prerequis sur un
autre : LimbesZoneProgression derive l'acces depuis CycleProgressionService.

LimbesMemorySequence utilise StorySequenceRunner et son store playOnce existant.
Le jalon NamedFact est rapporte uniquement par le CycleController autoritaire
apres une presentation reussie ; une annulation ne le valide pas. Le sixieme
cycle Ange attend l'introduction, la sante morte de l'acteur Brains puis la
sequence de conclusion. Les objets de parcours sont permanents quand les
scenes de cycles terminees se dechargent. Les neuf items sont ajoutes aux
catalogues de GameplaySessionRoot pour la resolution apres dechargement.

LimbesAngelBoss derive du shell BossEncounterBehaviour, accepte l'acteur
IEnemyEncounterActor/Brains existant et fournit une politique de rencontre.
Pas de second EnemyController. Sante/degats/Flame/presentation reutilisent les
services existants ; le comportement dedie possede le mouvement du boss.
Phases de vol/projectiles puis descente/melee. Les tirs sont simules par
l'autorite, les clients n'appliquent pas leurs collisions. La sortie finale
lit la completion du cycle ; les portails de retraite sont conserves.

Revelations autorisees : Azazel, ames captives, statue et rite. Le secret
Lucian-Munin et son sacrifice ne sont pas devoiles. Installation native explicite
par LimbesExpansionAuthoring, jamais en Play ni au chargement de l'editeur.
LimbesExpansionVerification controle references/sols/NavMesh ; ne pas assimiler
ces controles au parcours UCC, au rendu, a la sauvegarde froide ou au reseau.
Qualification native : 43 controles auteur passes. Parcours automatise complet
0b3f9181064946948db4c514563d0564 reussi : treize preuves, trois montees UCC,
six cycles/sequences, boss, sortie et conservation apres rechargement de zone.
Controle cible e83f82523fb84de88e49bf0bba7b7f69 reussi, terrasse et boss examines
dans les captures. Les deux essais restaurent l'editeur sans erreur runtime
observee. Approches, avance des dialogues et survie au boss sont des fixtures ;
pas une recette manuelle continue, une sauvegarde froide ni un test reseau.
Direction et limites dans Docs/Design/Reve_Limbes.md.

## Audit du contenu sauvegarde des Limbes (2026-10-27)

Menu Tools/Lit/Limbes/Audit saved zone completion : LimbesCompletionAudit ouvre
les scenes du manifeste en preview, les ferme sans sauvegarder et rapporte les
cycles lies, StorySequenceRunner, bindings Timeline, IEnemyEncounterActor et scripts
manquants. Controle aussi les scenes activees dans Build Settings et les seuils
de cinq cycles/six sequences. Sortie Library/ReveLimbesCompletion/audit.json.
Il compte les liaisons auteur, pas leur accessibilite ou leur declenchement reel.
Un acteur genere uniquement au runtime n'est pas compte. Le vol, les attaques, la
fin de zone et la persistance demandent toujours une verification Play Mode.

## Premiere arrivee dans les Limbes (2026-10-27)

Reve_Limbes contient un rig Limbes_First_Arrival avec le StorySequenceRunner
existant, trois StorySequenceCameraPoint et leurs cibles. Aucun nouvel objet
Camera ni personnage n'est cree. Les points/cibles enfants sont editables dans
la hierarchie ; ils surplombent le parcours a 34-42 m, avec trois angles distincts.
Sequence : StorySequence/LimbesArrival/Limbes_First_Arrival.asset, identifiant
stable limbes.first_arrival, playOnce actif, 3 x 3,5 s de panorama (transitions
incluses), puis evenement limbes.camera.gameplay, attente de 0,35 s et deux
dialogues de Lucian. Les textes demandes sont conserves exactement dans deux
VoiceLineData, sans doublage ajoute et avec avance manuelle par Interagir.

L'evenement de scene lie explicitement StorySequenceCameraDriver.EndCinematic
avec snapBackToGameplay=true. Les deux dialogues ont focusCameraOnSpeaker=false :
ils ne reprennent donc pas une camera cinematographique apres le retour a la
vue normale. Le verrou d'entree est conserve pendant le dialogue puis restitue
par le runner standard. Aucun fondu noir ou changement de musique ajoute.

StorySequenceOnZoneArrival attend l'environnement et l'eclairage des Limbes,
le Core actif, la fin de la transition/loading, Lucian local et l'exploration
libre. Il ne charge aucune scene et ne teleporte personne. Les completions du
store existant empechent le rejeu apres fin reussie ; elles suivent la sauvegarde.
Desactivation/changement de zone annule sa presentation et restitue le controle.
Une sequence interrompue n'est pas artificiellement terminee. Le demarrage
direct fonctionne lorsque le groupement est charge ; Core seul ne lance pas
un panorama dans le vide. Presentation locale, aucune nouvelle synchro Netcode.

Compilation runtime/Editor et tests compiles avec le compilateur Unity, references
locales YAML controlees (aucun doublon ni lien local manquant). Tests natifs
auteur demandes dans Library/LimbesArrival, pas encore executes sur cette revision.
Rendu Play et retour reel UCC a verifier ; la compilation et les controles de
references ne prouvent pas l'absence d'obstacle visuel.

## Retrouvailles des compagnons dans Maison (2026-10-26)

Maison_Core contient Maison_Companions_Reunion : son composant
MaisonCompanionReunion instancie Vesta, Soren et Aurell aux MaisonWaitingPoint
2, 3 et 4. Les references et la distance d'approche (3 m) sont editables sur
ce groupe. Les presentations sont des prefabs visuels dedies dans
StorySequence/MaisonReunion/Presentations : meshes, squelette et Animator sans
root motion, sans controleur joueur/UCC, reseau, collisions ou persistance.
Leur presence narrative ne recrute personne et ne modifie pas la squad solo.
Elles appartiennent au Core, sont recreees lors de son rechargement et ne sont
pas des objets persistants ; EnsurePresentations est idempotent.

Lucian declenche une conversation de groupe en approchant un des trois acteurs,
seulement dans le Core actif, apres loading, transitions, menus et sequences
existantes (notamment la rencontre Munin). La difference de hauteur maximale
est 1,5 m. Tous les interlocuteurs doivent etre presents. Le binding Lucian
utilise explicitement l'acteur local ; les trois autres sont lies directement,
sans dependance a un scan global ou a l'ordre de la squad.

Maison_Companions_Reunion.asset porte 17 repliques texte, avancees avec Interagir,
les portraits CharacterData et les plans/dialogue/restauration de controle du
StorySequenceRunner existant. Les textes sont des VoiceLineData separes dans
MaisonReunion/Dialogues. Pas de doublage ajoute. [Munin] reutilise le nom choisi.
Le dialogue montre l'amitie ancienne et les rencontres recentes avec les anges
gardiens ; leur attirance pour leurs hotes reste une question, pas une explication.

Mise en scene : 17 dialogues conserves et trois etapes CameraShot ajoutees
(ouverture, retour au groupe avant la reassurance d'Aurell, conclusion).
Neuf profils dedies dans MaisonReunion/CameraProfiles : groupe, duo de
taquinerie, portraits moyens de Vesta/Soren, portraits rapproches des trois
compagnons et de Lucian, et contrechamp de Lucian. Les repliques 1, 3 et 16
conservent le plan precedent, notamment pour la reaction a une replique hors
champ. Transitions de 0,75 a 1,15 s ; la conclusion laisse 0,8 s apres le
recentrage avant de rendre le controle. Aucun fade noir supplementaire.

Les profils peuvent activer useConversationAxis (orientation du plan selon
les positions reelles locuteur/interlocuteur) et centerCameraBetweenSubjects
(origine du cadrage au milieu des acteurs). Ces options sont desactivees par
defaut : les autres sequences conservent leur TransformPoint auteur, echelle
comprise. Le contrechamp Lucian inverse le decalage lateral pour conserver
le cote de l'axe d'un duo. Au declenchement, seuls les trois PNJ visuels se
tournent horizontalement vers Lucian ; ni son moteur UCC ni sa pose ne sont
modifies. Le driver existant assure transitions douces et restitution gameplay.
Pas de correction d'obstacles ajoutee : les cadrages restent a verifier avec
les murs/meubles reels et plusieurs positions d'approche.

sequenceId stable : maison.companions.first_reunion, playOnce actif. Seule une
fin reussie marque la completion via StorySequenceCompletionStore existant.
Abandon, annulation et recharge suivent la sauvegarde, pas un PlayerPrefs global.
Un retour au hub conserve les compagnons mais ne rejoue pas la conversation
deja terminee. Presentation locale, pas de nouvelle logique de recrutement ou
de dialogue synchronise ajoutee pour le multijoueur.

Installation native executee dans le projet existant via demande explicite,
sans copie de projet ; l'installateur refuse un Core ouvert dirty ou du contenu
deja installe. Compilation runtime/Editor (avec nouveaux tests) reussie par le
compilateur Unity, y compris la revision des cadrages et leurs tests (compatibilite
legacy, axe/contrechamp, centrage et rythme auteur). Tests cibles natifs et rendu
Play encore a qualifier sur cette revision ; compilation seule ne vaut pas
validation visuelle. Aucune copie de projet creee.
Le controle existant StorySequence/VideoPanel a passe 34/35 : seul le test video
VideoLeaseRestoresOnlyUiModulesItDisabledAndHidesTheQuestion echoue sur une scene
temporaire sans nom non sauvegardee, avant ses assertions. Aucun code video modifie.

## Audits auteur de zone

Etienne conserve ses trois Flames identifiees dans la scene du cycle, pas dans
son environnement. L'audit controle toujours leurs IDs, lumiere et etat initial.
Une interaction peut aussi relever d'une Flame d'une autre scene obligatoire
du meme ZoneManifest : CycleZoneFlameScope ouvre ces scenes en lecture seule,
ignore les sources inactives et ferme uniquement celles qu'il a ouvertes.
L'audit n'accepte pas une Flame d'une zone etrangere deja ouverte dans l'editeur.
Le Ghost d'Etienne reste actuellement hors couverture reelle. Une Flame commune
locale initialement eteinte est approuvee ; l'auteur se charge de son placement.
Aucune scene Etienne modifiee automatiquement ; couverture a revalider ensuite.
Le ruban d'Ardent est couvert par un rayon canonique de 11,25 m sur sa Flame
locale ; lumiere et influence suivent le reglage central existant.
Les VFX de Scar gardent leur renderer auteur ; seules les proprietes exposees
par le graphe sont renseignees, sans recherche d'un objet nomme Skinned Renderer.

## Transition Ghost/Enemy et disparition apres dialogue

GhostController.SetGameplayMode applique Ghost, Introduction ou Enemy. Pour une
rencontre startAsGhost, EnemyController conserve l'etat autoritaire (local en
solo, NetworkVariable en reseau). CombatEnabled tient compte de cet etat :
perception offensive, verrouillage, degats et riposte restent bloques avant
Enemy. CharacterInfo.DataChanged reapplique le mode apres affectation tardive.
La fermeture reussie de l'introduction, validee par token, client initiateur et
duree minimale sur le serveur, ouvre le combat. Annulation/desactivation ou
deconnexion de l'initiateur rend le Ghost disponible; les anciens callbacks
sont ignores. Le dialogue reste diffuse aux clients de la session.

CycleDialogue.disappearAfterCompletion utilise le rewardFlag existant ou les
completedFlags; aucun nouveau format de sauvegarde. Le cycle transmet le jalon
au Ghost, qui attend disappearanceDelay en temps reel puis dissout son corps et
termine ses effets. Scar commence sa dissolution des la fermeture qui accorde
Cicatrice (disappearanceDelay=0), puis se desactive apres les effets. Son dialogue
reste affiche deux secondes hors fondus (durationSeconds=2). Zero conserve la
duree du cycle pour les autres dialogues. UI et validation serveur utilisent
la meme duree resolue. Le delai
zero ne saute pas la dissolution; restoreImmediately distingue le chargement.
Une premiere presentation avec jalon deja charge masque directement
le Ghost. Les activations de cycle et la proximite ne peuvent pas le reapparaitre.
Les dialogues Nina ne sont pas configures pour cette disparition.

## Rencontre Ghost dans EnemyController

enemyEncounterOptions.requiredKnowledge est une condition facultative de la
rencontre. Le scientifique n'en a pas : son interaction Ghost lance directement
l'introduction et le combat. `Existence des chimères` est révélée par le cycle
dès que sa défaite `encounter` est enregistrée, avant toute cinématique ou
présentation de fin. La connaissance suit le partage de session existant.

ScientistEncounterController est supprime. CharacterData.enemyEncounterOptions active la phase Ghost, la replique d introduction, sa duree et la distance d interaction. EnemyController implemente IGhostInteractionHandler et ICycleCinematicBlocker. Les controles serveur, la visibilite Ghost et la progression Nina sont conserves. Les dernieres paroles sont configurees dans enemyDeathOptions et bloquent la presentation de cycle pendant leur lecture.

## Liaison des rencontres et poses de cycle

CycleController accepte un encounterEnemy explicite pour les ennemis places en
scene sans SceneMarker. Avec un marker, RuntimeInstance est prioritaire sur la
copie baked. Le cycle observe CharacterInfo.HealthChanged et retrouve aussi un
ennemi deja mort lors du chargement. Nina utilise cette liaison directe.
CyclePoseBinding peut piloter un booleen Animator suivant la condition; Nina
utilise isDead. Les noms courts d'etats sont resolus avec le nom du layer.
La presentation Dead requiert les deux connaissances et reste independante de
la disponibilite de la Timeline.

## Rôle

Exécuter les séquences cinématiques, afficher les dialogues, gérer les
connaissances et résoudre les interactions narratives.

## Classes principales

- `StorySequenceAsset` / `StorySequenceRunner` : données et exécution des étapes.
- `StorySequenceSceneBindings` : acteurs, caméras, timelines et événements.
- `StorySequenceCameraDriver`, `StorySequenceDialoguePresenter`,
  `StorySequenceFadeController` : présentation.
- `StorySequenceCompletionStore` : progression `playOnce` dans le slot.
- `KnowledgeManager` / `KnowledgeSO` : connaissances débloquées.
- `GhostData` / `GhostController` : enquêtes et réactions conditionnelles.
- `ReadableContentRuntime` : contenu généré stable et sauvegardable.

## Flux principaux

- Une séquence attend le personnage local, verrouille l’input/UCC, pilote caméra,
  dialogues et étapes, puis restaure le gameplay.
- Les acteurs sont résolus par ID, squad ou `LocalPlayerContext`.
- Les connaissances débloquent des réactions de fantômes et des effets de scène.
- Un `Item` peut débloquer des connaissances à sa récupération
  (`knowledgeUnlockedOnPickup`) ou à sa consultation (`knowledgeUnlockedOnRead`).
  Les deux chemins passent par `KnowledgeReveal` : le serveur les valide et la
  notification est envoyée à toute la session.
- Une `KnowledgeSO` peut aussi porter un `CombatKnowledgeModifier` passif. Si
  son option combat est active, l'effet s'applique automatiquement au combat
  temps réel tant que la connaissance est débloquée; elle n'est ni équipée ni
  consommée.
- Quand une réaction de connaissance est disponible, le feedback du fantôme joue
  seulement la réponse de résolution, sans répéter la ligne d’apparition, la
  question par défaut ou l’option joueur.
- Les contenus lisibles générés sont capturés dans `CharacterStateStore`.
- Les séquences `playOnce` sont enregistrées dans les métadonnées du slot.
  Leur completion et leur reset restent en memoire pendant la partie : ils ne
  modifient plus immediatement le slot. TryPrepareSaveMetadata les inclut dans
  la transaction de sauvegarde personnage/monde/meta. Quitter sans sauvegarder
  puis recharger restitue donc les sequences du dernier commit. Un nouveau
  slot manuel de la meme session reprend les completions runtime, sans modifier
  l'ancien slot. Le reset de session efface aussi les completions transitoires
  sans slot (notamment cote client). Le format des metadonnees est inchange.
- `SceneMarker` est le point d'auteur unique pour les personnages, items et
  fantômes. Son Inspector permet `Bake in Scene` pour les items et fantômes :
  leur `WorldPrefab` devient alors un objet de scène déjà configuré, sans
  instanciation runtime. Le Bake Character conserve le marker de persistance et
  place l'acteur immédiatement en solo ; en réseau, cette copie est masquée et
  le spawn Netcode existant reste autoritaire. Après chaque Bake, l'Inspector
  vérifie la hiérarchie produite : en l'absence de `RuntimeOutlineTarget`, il
  propose de l'ajouter sur l'un des enfants qui porte un `Renderer`. Les anciens
  `ItemSceneMarker` se migrent depuis le menu `Lit/Scene Marker`.

## Cycles reutilisables et progression de partie

CycleDefinition contient metadonnees de journal (sans UI), prerequis et etapes
nommees. CycleProgressionService est installe sur le WorldRulesStateManager de
session par NetcodeBootstrap. Il possede les transitions autoritaires et les
notifications ; CycleController garde les liaisons et presentations de scene.
Les objectifs disponibles avancent en parallele ou selon des prerequis toutes/
au moins une, incluant un autre cycle termine. Les sources couvrent connaissance,
dialogue ferme, interaction, ennemi vaincu et sequence terminee. Un evenement
precoce n'est pas memorise ; connaissances et defaites sont des faits persistants.
Un evenement ne traverse pas deux etapes successives attendant la meme source.

Les cles .step.<id>, .defeat.<sourceId> et .completed reutilisent les variables
monde et snapshots existants. Le service diffuse les etats uniquement depuis le
serveur et actualise aussi les clients apres ClientMarkedReady. La progression
et les skills restent donc accessibles apres destruction du controleur de scene.
Les recompenses sont composees par CycleSharedSkills et ne modifient pas les
fiches source. La migration des anciens bits Nina est idempotente et silencieuse.

CycleInteraction fonctionne aussi sans Ghost via les interfaces de detection
et d'input locales existantes. Les rencontres et sequences sont des liaisons
multiples avec ID. Une sequence ne suspend que les ennemis explicitement lies,
sans parcourir ou interrompre les autres cycles du monde. Les callbacks tardifs
de playback ne peuvent pas liberer les references d'une nouvelle presentation.

Les etapes terminales definissent la fin partagee. Le serveur attend les effets
finaux et les clients connectes au plus completionPresentationTimeout (15 s),
puis annule les presentations restantes du cycle avant de solliciter GameFlow.
GameFlow protege la scene principale, joueurs et services de session, et ignore
les scenes terminees aux chargements suivants et dans sa validation NavMesh.
Nina conserve sa fin au bit 8 et les bits historiques 1/2/4/16. Ses six etapes
nommees sont dans la fiche ; aftermath_seen est facultative. La Timeline et le
profil du Director de la scene Nina sont encore absents : aucune completion
fictive n'est attribuee a cette sequence.

L'Inspecteur expose des categories repliables, des conditions selectionnees par
nom, un etat runtime lisible et les diagnostics de references/dependances.
Le modele vide et la migration Nina sont sous Lit/Narrative et Assets/Create.
Guide d'auteur et verification : Assets/Narrative/Cycles/README.md.

## Pièges observés

Le cycle Nina (contenu dans `Assets/Narrative/NinaCycle`) utilise CycleController
(`Assets/Narrative/Cycles`), un controleur serveur generique, et les
variables monde sauvegardees pour ses jalons. La lettre ne donne Dilemme
Edouard qu'a sa lecture. Existence des chimeres est revelee des la mort confirmee
du scientifique, via KnowledgeReveal, sans attendre la cinematique. Le chargement
d'un etat ScientistDefeated accorde aussi ce savoir s'il manque.
Nina devient Dead uniquement lorsque toutes les connaissances
du cycle (Existence + Dilemme) sont acquises, sans autre jalon requis. Parler a Nina
Dead active ensemble sang et Scar (bits NinaDeadSpoken=16 et NinaVisited=4).
Scar reste invisible au loin et apparait progressivement a proximite via le
GhostController standard. La portee utilise le collider comme l'interaction standard.
Ghost_Scar porte un Rigidbody dynamique avec gravite et rotation bloquee dans
la scene Nina : modele et zone d'interaction suivent ensemble les contacts du
monde. Le prefab Scar conserve une seule capsule corporelle solide, avec les
colliders de squelette/accessoires desactives et le root motion coupe. La
revelation ne pilote pas la gravite; l'activation narrative remet le corps en
simulation. Aucun repositionnement force au sol n'est ajoute.
Les anciennes sauvegardes avec un seul des deux bits restent compatibles.
DialoguePanelUI.TryShowTimedConversation distingue fin naturelle et annulation.
CycleInteraction relie chaque fantome a un dialogue de CycleDefinition.
GhostController gere seul revelation, outline et detection, sans adaptateur Nina. La scene
contient des emplacements explicites; les ressources artistiques restent a assigner.
La competence de Scar est composee a la lecture par SkillsManager depuis la variable
de monde, sans mutation de CharacterData ni duplication de recompense.
Le dialogue valide de Scar accorde Cicatrice a sa fermeture naturelle, apres le
fondu puis validation serveur de la portee et des prerequis. Une annulation ou
un dechargement ne donne pas la recompense. Le bit RewardGranted conserve le deblocage
au chargement et pour les clients tardifs, sans rejouer la notification.
SkillUnlockPanel utilise le panneau dedie de Bootstrap (identifiant distinct du
panneau des connaissances), avec titre/description et une file de notifications
de 4.5 s en temps reel. Son API TryShow accepte SkillSO et StatsSO; chaque source
l'appelle uniquement apres un nouvel apprentissage (Scar, LearnSkill du personnage).
Cicatrice est retiree des skills et de l'equipement initiaux de Lucian; aucun
equipement automatique n'est ajoute. Le format des sauvegardes reste identique.

La defaite du scientifique utilise ScientistEncounterController.PlayDeathPresentation :
Death + replique + AudioClipSO deathVoiceLine. RealTimeCombatSceneUiController
attend la fermeture du dialogue avant le panneau de victoire. La cinematique Nina
attend ensuite la fermeture du resultat pour ne pas remplacer les derniers mots.
La presentation est locale a chaque joueur, le resultat de combat reste inchange.

- `dialogueMaxDisplayDuration = 0` attend indéfiniment `Interact`.
- Chaque chemin de sortie d’une séquence doit libérer focus, caméra et verrou UCC.
- Les IDs narratifs sont persistants; les renommer nécessite une migration.
- `GhostData` est une donnée d’auteur; l’état compris/résolu appartient au runtime.
- Timeline est réservée aux chorégraphies complexes, pas aux dialogues ordinaires.
- La Timeline `GiantJuggernaut_Intro` est une presentation camera/animation :
  elle ne porte aucun `TimelinePlayerMoveTrack` et ne doit pas modifier la
  position de Lucian. Le verrou UCC de la sequence suffit a le maintenir en place.

## Transition fantome vers ennemi

Le Scientifique utilise GhostController avec GhostData_Scientist avant la
rencontre. Le routage optionnel IGhostInteractionHandler est commun aux
CycleInteraction et ScientistEncounterController : une seule source d'input et
de detection sur le fantome. Le serveur valide le joueur, la portee et la
revelation avant la transition. SetGhostMode(false) arrete les coroutines et
l'input du fantome, puis rend le corps visible pour le mode suivant. Il ne doit
plus disparaitre a distance pendant le combat. La replique d'introduction
existante precede l'activation du cerveau et de la navigation ennemis.
L'instance Nina herite des quatre composants CombatHealth/CharacterInfo/
EnemySkills/RealTimeCombatEnemy du prefab sans copie additionnelle.
## Socle de cycles nommes

CycleProgressionService centralise les etapes nommees, les migrations et les recompenses. Les faits comprennent connaissance, dialogue ferme, interaction, ennemi vaincu, lecture, collecte, entree de zone et fait nomme. Un fait est persiste avant ses prerequis puis reconcilie lorsque les services disponibles peuvent terminer les etapes. Chaque CycleStep peut accorder des CycleReward idempotentes de type Skill, Knowledge, Item, WorldVariable ou Activation. Le validateur global Lit/Narrative/Validate All Cycles controle les definitions et leurs scenes avant build.

Nina est le premier cycle sans flags actifs : ses anciens bits restent uniquement une source de migration versionnee. La defaite du scientifique accorde Existence des chimeres et Scar accorde Cicatrice par les recompenses de leurs etapes. Nina's Blood et Scar utilisent l'etape nina_spoken.

## Cycle Luc : etapes nommees (2026-09-27)

Luc suit le meme socle que Nina, sans flags historiques car son ancien flux n'en persistait pas. Les connaissances du collier et de la tombe reconciliant les deux premieres etapes, l'interaction luc puis sa fermeture valident la conclusion. La connaissance jonLocation est une recompense idempotente de luc_farewell; Ghost_Luc est relie a CycleInteraction et CycleController par Lit/Narrative/Configure Luc Cycle. La scene est referencee sous District_1_Cycle_Luc dans le manifest District 1.

## Rencontre Munin apres la video de nouvelle partie

Asset : Narrative/StorySequence/Sequences/1_Munin_Meeting.asset, rig homonyme
sous Prefabs reference par MainMenu. Nouvelle partie queue ce rig ; GameFlow
consomme une fois la demande, prepare Maison/navigation/personnages et place
l'escouade avant de l'instancier sous GameplaySessionRoot. PlayAfterLoading
attend la disparition du loading. Annulation/retour menu nettoient l'attente et
la demande ; Charger et les portails n'en creent pas. Presentation locale au
createur seulement ; pas de nouveau protocole reseau narratif.

Quatre etapes : question de Lucian -> ChooseMuninName -> presentation avec le
nom choisi -> invitation au depart. Les trois VoiceLineData dans
VoiceLines/1_Munin_Meeting sont textuelles, sans voix ajoutee. Les dialogues
attendent Interact et leur fermeture precede l'etape suivante ; pas de camera
ou d'acteur Munin invente. La camera de jeu actuelle est conservee.

ChooseMuninName est ajoute a la fin de l'enum (10) sans renumeroter les anciens
types. Le composant StorySequenceNamePrompt du rig reference explicitement
UI_Overlay_VirtualKeyboard. Il reprend ses touches/visuels avec des Buttons,
desactive les pilotes du menu sur la copie et gere la saisie sous focus/mode UI.
Les pressions ne traversent pas vers le dialogue suivant. Clavier physique et
souris disponibles ; manette : stick/croix pour selectionner, A pour une touche,
B pour effacer, Start pour valider. Entree/OK valident ; Echap choisit le defaut.
Vide/espaces seuls donnent Munin ; nom borne a 24 caracteres, lettres/chiffres,
espaces, apostrophes et tirets (pas de balises de texte). Annulation/desactivation
ferment l'UI sans enregistrer de brouillon et restaurent curseur, selection,
navigation EventSystem, focus et mode d'input. Echec de saisie : sequence avortee,
pas de completion PlayOnce.

StorySequenceNames stocke story.munin.name via WorldRulesStateManager.SetString.
Le snapshot binaire existant conserve cette valeur ; aucune nouvelle ecriture
immediate ni changement de format. Sauver puis recharger restitue le nom ; une
nouvelle partie repart sans nom choisi et utilise le defaut. La balise [Munin]
est resolue a l'affichage par StorySequenceDialoguePresenter et DialoguePanelUI,
y compris les cues ; aucune mutation des VoiceLineData source. Les textes
overhead utilisant un autre afficheur ne passent pas par ce chemin.

StorySequenceAsset.TryValidate partage les controles structurels entre lecteur
et inspecteur : vide, etapes nulles/inconnues, dialogue sans contenu ou sans
issue, Timeline sans profil, camera sans point/acteur, etc. Le lecteur verifie
le composant de saisie et son clavier avant toute sequence qui le requiert.
Les bindings dynamiques restent a qualifier dans la scene. L'inspecteur offre
une liste numerotee reordonnable avec champs par type, ajouts Wait neufs (1 s),
et diagnostic ; outils auteur diriges vers Narrative/StorySequence.

Validation : compilation runtime/Editor et import natif passes ; 19/19 tests
StorySequenceAuthoringTests passes dans Unity, zero echec/ignore (XML dans
Library/IntroValidation). Tests de normalisation/defaut, vrai serialiseur binaire
du monde, reset/recharge du nom, token sans mutation source, ordre auteur des
etapes et construction des 39 touches dans une scene de preview, sans sauvegarde
utilisateur modifiee. Ces tests EditMode ne qualifient pas les inputs en jeu.
A qualifier en Play depuis Bootstrap : intro complete, fermeture question,
saisie nom/validation vide, reprise des deux repliques sans double validation,
restitution UCC/camera, sauvegarde/recharge du nom, seconde nouvelle partie,
annulation du rig pendant la saisie, manette/souris/clavier, Domain Reload On/Off.
