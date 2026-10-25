# Cycles narratifs cooperatifs

## Modules

CycleDefinition configure les objectifs et textes. CycleProgressionService,
installe avec WorldRulesStateManager par NetcodeBootstrap, valide et replique la
progression de partie. CycleController lie les acteurs de sa scene et orchestre
les presentations ; il ne possede plus l'etat durable. GhostController et
TimelineManager gardent leurs effets. GameFlowService possede le dechargement.

## Creer un cycle

1. Menu Assets > Create > Lit > Narrative > Modele de cycle vide. Ranger la fiche
   sous Resources/Narrative et conserver son ID apres publication. Si la fiche
   reste dans le dossier de sa scene, la referencer dans un CycleCatalog situe
   sous Resources/Narrative (comme Luc et Nina). Le catalogue garantit la
   restauration et les recompenses meme lorsque la scene est deja dechargee.
2. Renseigner titre, description et categorie principale/annexe. Ces metadonnees
   preparent le journal ; aucune interface de journal n'est creee ici.
3. Ajouter des etapes avec ID stable et libelle : connaissance, dialogue termine,
   ennemi vaincu, interaction ou sequence terminee. La source est l'ID du dialogue
   ou de la liaison de scene ; une connaissance utilise directement son asset.
4. Choisir les prerequis par nom : toutes ou au moins une des conditions.
   Les objectifs peuvent avancer en parallele ou demander un autre cycle termine.
5. Cocher terminal sur les objectifs de fin : ils sont tous necessaires. Les
   etapes facultatives ne sont pas terminales. Configurer les skills sur les
   etapes, pas sur les anciens champs de recompense du dialogue.
6. Placer CycleController/NetworkObject dans une scene additive dediee. Assigner
   fiche, interactions, rencontres et sequences. Chaque sequence exige son
   Director et son profil. Lier explicitement les ennemis a suspendre.
7. Pour un Ghost, ajouter CycleInteraction a son objet. Pour un PNJ ou objet,
   ajouter CycleInteraction et un collider, regler point/distance. Son ID est
   celui du dialogue ou de l'etape Interaction. Ne pas doubler une interaction
   existante prioritaire sur le meme objet. Les interfaces de detection et
   d'input existantes routent les interactions sans Ghost.
8. Assigner cycleSceneName, ajouter la scene au manifeste et aux Build Settings.
   Ne pas y mettre joueurs, services persistants ou terrain permanent.

L'Inspecteur propose categories repliables et tooltips. En Play Mode, le
controleur affiche l'etat partage et la raison d'attente de chaque objectif.
Les nouveaux cycles n'utilisent aucun flag. Une migration versionnee est reservee aux contenus publies.

## Progression et persistance

Etats : indisponible, disponible, en cours au premier jalon, termine. Les
 evenements valides par les adaptateurs de scene restent des faits persistants,
reevalues lorsque les prerequis sont remplis. Une meme source partagee par
plusieurs etapes peut donc les satisfaire successivement : utiliser des IDs de
source distincts lorsque plusieurs actions du joueur sont necessaires.
Le dialogue est valide apres fermeture naturelle, controle du token, de la
portee et de sa duree. Interaction correspond a l'ouverture validee ou a l'action
sans dialogue. Le serveur reste seul autorise a valider en reseau.

Les cles narrative.<cycleId>.step.<id>, .fact.<type>.<source>, .reward.<step>.<index>
et .completed utilisent
WorldVariableSnapshot sans nouveau format binaire. Le service de session emet
les etats et JoinSync conserve les variables apres dechargement. Un envoi au
signal ClientMarkedReady actualise l'etat apres le snapshot initial.
CycleSharedSkills compose les skills depuis les jalons sauvegardes, sans modifier
les fiches ni equiper de force. Une restauration ne rejoue pas la notification.

## Fin et nettoyage

### Repliques automatiques

Configurer `autoDialogues` dans le groupe « Sources de progression » du
CycleController. L'ID de liaison est l'ID d'etape ; son `sourceId` peut etre
different. La replique utilise une session transitoire, sans nouveau format
de sauvegarde, isolee dans CycleController.AutoDialogues et CycleAutoDialogueSession.

Une interface deja occupee n'est jamais remplacee. La progression attend une
lecture terminee ou passee volontairement : chaque spectateur connecte au lancement
doit confirmer. Interagir/Valider/Annuler pour passer une replique affichee compte
comme une lecture, et ne la redemarre pas. La fermeture est immediate ; si le joueur
passe avant la duree minimale, seule la confirmation attend la fin de cette duree,
afin de respecter la protection de l'hote contre les confirmations precoces.
Un delai seul, sans lecture ni passage volontaire, ne valide rien.
Remplacement, annulation technique ou interruption de scene annulent la tentative,
sans jalon ni recompense ; une nouvelle tentative est possible apres une seconde.
Le delai de securite est la duree de la replique plus au moins 30 secondes.
Une deconnexion retire le spectateur, mais il faut au moins une lecture confirmee
pour valider. Une arrivee tardive ne change pas l'audience ; elle recoit la
progression partagee. Les anciens tokens et doublons sont ignores.

La desactivation et le dechargement annulent seulement la presentation possedee
par ce controleur. L'inspecteur Play Mode affiche les confirmations de fin.

### Dechargement

Le serveur attend les dialogues et dissolutions des clients connectes, 15 s au
maximum par defaut. Il annule ensuite uniquement les presentations du cycle et
libere ses verrous avant de demander le dechargement a GameFlowService. Une
deconnexion ne bloque plus l'attente. Les operations reseau concurrentes sont
retentees. La scene principale et les scenes contenant joueurs/services sont
protegees. GameFlow ignore les scenes terminees aux prochains chargements et
dans sa validation des scenes requises pour le NavMesh.

## Nina

Etapes : chimeras_known, edouard_understood, scientist_defeated, nina_spoken,
scar_reward et aftermath_seen (facultative). Les etapes terminales remplacent
les flags actifs. La migration versionnee lit une seule fois les anciens bits 1,
2, 4/16/20 et 8, les conserve pour diagnostic et ne les reevalue plus.
Le menu Lit > Narrative > Migrer Nina vers les etapes nommees et le generateur
utilisent la meme configuration ; une fiche deja migree n'est pas remplacee.
Nina devient Dead selon les deux connaissances. Scar parle deux secondes hors
fondus, accorde Cicatrice, se dissout puis laisse decharger la scene.
Le scientifique n'a aucune condition d'introduction : sa défaite débloque
immédiatement ExistenceDesChimeres.
La sequence cinematic manque de Timeline et de profil de liaison dans la scene
Nina : son etape facultative reste non validee.

## Validation

Menu Lit > Narrative > Executer les tests des cycles ; resultat dans
Library/CycleTests.xml ; l'etat et le resultat sont dans Library/CycleTests.result.
Ce lanceur couvre les fondations et les tests Luc, Nina, Belmont, Etienne, Ardent et
Clairmont. Les scenes modifiees doivent etre enregistrees avant son lancement ;
aucun import force ni reconstruction n'est effectue. Completer par un parcours Play Mode solo/hote-client :
interaction simultanee, deconnexion, client tardif, expiration de presentation,
retour de zone et Domain Reload active/desactive. Une compilation seule ne
valide pas ces parcours.

Regression du protocole des repliques en vrais processus separes :
`Tools/Validation/CycleNetworkSmoke/Run.ps1`. La procedure ne touche pas aux
scenes du jeu et isole les sauvegardes. Elle couvre sept scenarios de
presentation/transport ; elle ne remplace pas les parcours des scenes et boss.
Consulter son README avant utilisation.

Les modes `timeline`, `timeline_pause`, `timeline_stop`, `timeline_external_stop`,
`timeline_service` et `timeline_missing` de ce meme outil verifient la lecture
native d'une animation en executable, sans personnage ni sauvegarde. Le
gestionnaire pilote ses sequences en une passe avec Hold temporaire : il
constate la fin sur le temps reel du Director, laisse presenter la derniere
image puis restaure le wrap mode d'origine. Il ne deduit jamais une reussite
de la duree ecoulee ni de l'evenement stopped, dont Unity peut remettre le
temps a zero. Les arrets explicites restent des interruptions.

Le contrat de couverture des bindings est commun a l'editeur et au runtime.
Une piste active non liee fait echouer la lecture ; un binding facultatif
doit etre declare explicitement. Les bindings natifs compatibles du Director
conservent leurs callbacks de presentation et de restauration.
