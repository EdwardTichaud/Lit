# Cycles narratifs cooperatifs

## Modules

CycleDefinition configure les objectifs et textes. CycleProgressionService,
installe avec WorldRulesStateManager par NetcodeBootstrap, valide et replique la
progression de partie. CycleController lie les acteurs de sa scene et orchestre
les presentations ; il ne possede plus l'etat durable. GhostController et
TimelineManager gardent leurs effets. GameFlowService possede le dechargement.

## Creer un cycle

1. Menu Assets > Create > Lit > Narrative > Modele de cycle vide. Ranger la fiche
   sous Resources/Narrative et conserver son ID apres publication.
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
Les champs numeriques historiques sont reserves a la compatibilite de Nina.

## Progression et persistance

Etats : indisponible, disponible, en cours au premier jalon, termine. Les
 evenements recus avant les prerequis sont oublies. Les connaissances acquises
et morts enregistrees restent des faits persistants reevalues a l'activation.
Un clic ne traverse pas plusieurs etapes successives attendant le meme evenement.
Le dialogue est valide apres fermeture naturelle, controle du token, de la
portee et de sa duree. Interaction correspond a l'ouverture validee ou a l'action
sans dialogue. Le serveur reste seul autorise a valider en reseau.

Les cles narrative.<cycleId>.step.<id>, .defeat.<sourceId> et .completed utilisent
WorldVariableSnapshot sans nouveau format binaire. Le service de session emet
les etats et JoinSync conserve les variables apres dechargement. Un envoi au
signal ClientMarkedReady actualise l'etat apres le snapshot initial.
CycleSharedSkills compose les skills depuis les jalons sauvegardes, sans modifier
les fiches ni equiper de force. Une restauration ne rejoue pas la notification.

## Fin et nettoyage

Le serveur attend les dialogues et dissolutions des clients connectes, 15 s au
maximum par defaut. Il annule ensuite uniquement les presentations du cycle et
libere ses verrous avant de demander le dechargement a GameFlowService. Une
deconnexion ne bloque plus l'attente. Les operations reseau concurrentes sont
retentees. La scene principale et les scenes contenant joueurs/services sont
protegees. GameFlow ignore les scenes terminees aux prochains chargements et
dans sa validation des scenes requises pour le NavMesh.

## Nina

Etapes : chimeras_known, edouard_understood, scientist_defeated, nina_spoken,
scar_reward et aftermath_seen (facultative). Le bit 8 conserve la fin ; 4/16 la
visite de Nina, 1 la mort du scientifique et 2 la sequence. La migration des
sauvegardes est automatique et idempotente, sans reecrire les bits existants.
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
Library/CycleTests.xml. Completer par un parcours Play Mode solo/hote-client :
interaction simultanee, deconnexion, client tardif, expiration de presentation,
retour de zone et Domain Reload active/desactive. Une compilation seule ne
valide pas ces parcours.
