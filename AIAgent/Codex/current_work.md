## Intro video et rencontre Munin (2026-10-25)

Nouvelle partie : Resources/Videos/Lit_Intro -> VideoPlayer auteur Bootstrap
(UI_Overlay/VideoPanel/VideoPanel_Root) -> Maison preparee -> 1_Munin_Meeting.
Le menu reference le prefab dedie ; GameFlow consomme la demande une fois et
instancie le rig apres placement, puis PlayAfterLoading attend la fin du loading.
Reprises et portails ne relancent pas la rencontre ; retour/annulation nettoient
les demandes. Presentation locale au createur, sans nouvelle synchro reseau.

Video : BootstrapVideoPanel pilote le lecteur auteur et la RawImage ajoutee a
VideoPanel_Root, avec FitInParent et fond noir sur VideoPanel. UiPanel cache au
boot ; aucun VideoPlayer/Canvas genere pour l'intro. NewGameIntroPlayer est un
adaptateur Resources vers ce panneau partage. Lease par appelant, arret/erreur/
annulation avec nettoyage des callbacks, focus et audio du menu. Copie identique
StreamingAssets retiree ; Resources est la source livree. Compilation native
runtime/Editor et 27/27 tests Unity passes : clip Resources avec audio, lecteur
auteur unique, references UI et masquage/proportions verifies en preview. Le
decodage/rendu, le son joue et le raccord complet restent a qualifier en Play.

Timing intro : loading opaque au moins 2,5 s avant lecture ; video preparee et
piste Music_Maison prechargee pendant cet ecran. Le repere 19 s utilise seulement
VideoPlayer.time (pas la duree du chargement ni Time.time). Musique lancee une
fois via AudioManager persistant, au volume musique habituel ; les autres sons
restent muets pendant le film. La piste continue durant le chargement apres les
28 s de video ; le ZoneRuntimeContext de Maison prend la suite sans redemarrage.
Override d'intro nettoye a la prise de relais, annulation ou retour menu.
Compilation runtime/Editor native et 27/27 tests Unity passes (zero echec/ignore),
dont seuil media, pause, saut de frame, seconde lecture et restitution selective
des canaux musique. Precision audible et raccord complet en Play restent a qualifier.

Rencontre configuree : Lucian demande « Qui es-tu ? Comment t'appelles-tu ? »,
puis ChooseMuninName ouvre les touches du clavier virtuel existant. Validation
vide : Munin. Apres validation, Lucian reprend avec le nom choisi puis « Allez,
il est temps d'y aller ». Dialogues texte sans doublage audio ajoute.

Le nom est une variable monde story.munin.name, dans les snapshots existants,
reinitialisee/restauree avec la partie. Balise [Munin] disponible dans les textes
StorySequence et DialoguePanelUI sans modifier leurs assets VoiceLineData.
Saisie clavier physique, souris ou manette ; focus/mode UI et curseur restitues
sur fermeture/annulation. Un nom non valide n'est pas enregistre a l'abandon.

Inspecteur : etapes reordonnables, champs par type, validation avant lancement.
Compilation C# runtime/Editor et import natif passes ; 27/27 tests Unity auteur,
nom, snapshot binaire, clavier et panneau video passes (zero echec/ignore).
Rendu, inputs et parcours complet Bootstrap -> intro -> rencontre ->
gameplay a qualifier en Play Mode. Details : systems/narrative.md.

## CounterHit sans reaction (2026-10-25)

HitIndex 0 accepte : pas de Hit_0, pas de changement d'etat ; degats/VFX/SFX
conserves. Une Timeline avec uniquement des indices 0 ne prend pas l'autorite
Animator des reactions. Indices negatifs et etats positifs absents refuses.
Validation ciblee : commande CounterHitSignals validate-flow, independante de
la configuration auteur de la Timeline : 28 assertions passees dans Unity.
Compilation runtime/Editor reussie ; rendu en jeu a verifier. Aucun marqueur
ou asset Timeline modifie.

## Nettoyage controllers et avatars (2026-10-25)

Graphes retenus : Lucian_ImpactLab, Juggernaut/BrainsProduction/Production,
Juggernaut_v2/StoneGolem_Humanoid (dernier controller auteur) et Werewolf natif.
References scene/prefabs et installateurs adaptees ; 15 etats compatibles
manquants conserves. Overrides requis et Player_Model partage restent des
dependances, pas des copies de production a supprimer. Timeline et signaux
CounterHit conserves. Consolidation du doublon exact TwinSword_Attack15 avec
conservation du GUID historique. Aucun autre personnage migre.
Archive : D:/LitAssetArchive/2026-10-25-animation-cleanup-101902 ; premier import
Generic du Golem dans l'archive voisine 2026-10-25-animation-cleanup-101544.
Suppression finale terminee Unity ferme : quatre assets et leurs metadonnees,
archives verifies SHA-256 et aucun GUID reference par les assets conserves.
Rapport Library/CharacterAnimationCleanup/report.json ; controle du rendu
apres reouverture encore requis.
Verification Unity du nettoyage : avatar Golem valide Humanoid et assets
retires absents. Test CounterHit actualise pour accepter le nombre de contacts
auteur (six actuellement) : tous valides avec effets resolus. Test strict bloque
ensuite car Enemy.Animator est maintenant demutee dans la Timeline auteur ;
aucune modification automatique de cette piste. Les 25 assertions precedentes
ne constituent donc pas une validation de cette nouvelle configuration.
Le plan de schema commun ci-dessous reste non applique ; ses inventaires sont
anterieurs au nettoyage. Rendu du Golem Humanoid a qualifier dans le jeu.

## Contrat Animator commun — prevalidation, migration non appliquee (2026-10-25)

Reference choisie explicitement : Player_Model.controller. Meme structure et
memes chemins d'etats pour les personnages ; seules les motions adaptees au
rig different. Choix confirme pour une action sans clip : etat conserve mais
action indisponible et signalee, sans clip de remplacement.
Audit lecture seule : Player_Model 90 etats/11 Blend Trees/88 transitions ;
Lucian_ImpactLab actif 96 etats ; Juggernaut v2 9, Production 10, StoneGolem 28,
Werewolf 7. Les six etats actifs supplementaires de Lucian doivent etre
preserves : ChargedPreparation, ChargedAscension, ChargedFloat, ImpactDeLumiere,
AirBasicSkill_2 et AirBasicSkill_3. Hit_n doit aussi entrer dans le schema
commun, pas seulement dans les controllers ennemis comme actuellement.
Le declenchement exclusivement par nom implique de migrer les pilotes des
88 transitions automatiques et leurs consommateurs, pas seulement les assets.
Perimetre transversal au-dela d'un patch local : plan requis par AGENTS avant
remplacement. Aucun controller, prefab ni code runtime modifie pour cette
demande a ce stade. Inventaire : Library/SharedAnimatorContract/Inventory.json.
Plan et criteres de migration dans systems/combat.md. Les travaux precedents
et les modifications auteur restent en place.

## Impacts de contre multi-ennemis (2026-10-25)

CombatLab_CounterSkill_2 migree vers quatre marqueurs
CombatLab_CounterHit_Damage_Animation_VFX_SFX : Damage float, Hit Index,
Blend Seconds, Vfx, Sfx. Horaires conserves ; 25 degats de base au total
initialises en 6/6/7/6, editables par marqueur. Enemy.Animator conservee mais
muette pour laisser jouer Hit_n propre a chaque ennemi. Alias natifs Hit_1
dans les controllers actifs. Les anciens Hit_2/Hit_3 Generic du Golem ne sont
pas transferes au controller Humanoid ; leur disponibilite doit etre verifiee. Sans changer
leurs motions existantes. Modification auteur precedente des clips de la
Timeline preservee ; rig et installateur ne recreent plus les anciens impacts.

Clock Timeline unique pour les reactions malgre pause globale ; autorite
Animator et root-motion restituee a la sortie. Ancien impact du clip joueur
ignore sur ce mode, dedoublonnage session/graphe, cible capturee, clarte unique,
degats reseau reserves au serveur. Details dans systems/combat.md.
Choix explicite CounterSkillSO.Timeline egalement respecte cote reseau.
Compilation Unity et 25 assertions EditMode de graphe passees ; configuration
repetee : zero difference sur sept assets, references clips auteur preservees.
Rapports Library/CounterHitSignals*. Rendu Play Mode et host/client non valides.

## Retrait des adaptations Execution / Executed1 (2026-10-25)

Retrait demande uniquement pour ce lot ; calibration du regard et RetouchedWalks
conservees. Copies et outils specifiques retires, sources fournisseur conservees.
StoneGolem utilise desormais l'avatar Humanoid de l'atelier sur le modele
principal Assets/StoneGolem/Mesh/StoneGolem.fbx (GUID conserve). Les mesures
historiques Generic ci-dessous ne qualifient pas ce nouvel import Humanoid.

## Calibration du regard, premier lot laboratoire (2026-10-24)

Werewolf reste Generic avec ses animations natives, selon choix utilisateur ;
aucune course native disponible. StoneGolem reste egalement Generic.
Onze copies de laboratoire preparees : Idle/marche/course Lucian et Juggernaut,
Idle/marche Werewolf, Idle/marche/course StoneGolem. Les deux derniers ne sont
pas modifies dans leurs poses. Sur les Humanoid, seuls les muscles Head/Neck
Turn sont ajustes ; Root Rotation reste a zero offset, garde du torse preservee.
Les appuis TwinBlades vont deja droit (marche environ 2 degres, course 0) : une
rotation globale de 40-60 degres serait contre-productive. Le regard est mesure
sur le plan horizontal, sans compter l'inclinaison verticale de la tete.

Extension directionnelle : 32 references Humanoid marche/course, quatre deja
alignees conservees, deux copies reutilisees, 26 copies nouvelles. Regard moyen
corrige a moins de 0,1 degre, oscillation auteur jusqu'a 3,8 degres conservee.
256 fondus voisins verifies : regard au plus 4,2 degres, appuis et root preserves.
Overriders resolus et couche de base seule pour ne pas confondre les etats
homonymes de la torche. 32 captures supplementaires ; regeneration idempotente
par comparaison des courbes auteur, sans comparer les caches natifs de Unity.
SaveAssetIfDirty cible les copies ; aucune sauvegarde globale des assets dirty.

Play Mode du lecteur sur les quatre vrais rigs dans le mini-projet isole :
23 717 assertions, 657 frames par mode, Domain Reload actif puis desactive,
pause, demandes rapides, desactivation/reactivation. Aucun test Play Mode
n'a detourne l'editeur principal. Ces tests ne qualifient pas UCC/NavMesh.
80 fondus Idle/marche/course mesures ; phases propres a chaque avatar,
conservation du clip sortant pendant le fondu.

Audit semelles des quatre marches incertaines ajoute, avec/sans IK native :
aucune proposition de phase assez fiable. Foot IK est actuellement desactive
sur les etats locomotion des deux controllers. Walk_L Juggernaut : pied droit
sans contact dans la tolerance de hauteur ; ne pas traiter comme simple cycle
offset. Experience RootT.y en memoire non retenue : hauteur de semelle forcee
mais oscillation bassin doublee sur certains cas (7,6 -> 15,6 cm), corrections
jusqu'a 9,7 cm. Les extractions verticales Unity alternatives ne corrigent pas
globalement ces cas. Aucun clip de hauteur experimental enregistre.
Captures sur sol de reference dans Library/ActorFacingCalibration/GroundingCaptures ;
rendu de phase zero initialise par un delta positif minuscule. Tentative de
capture CPU invalide retiree. Les mesures ne valent pas validation gameplay.

Deux overrides de previsualisation non installes prepares (Lucian/Juggernaut),
autres slots effectifs/overrides torche conserves, base graphs/prefabs inchanges.
Idle gameplay effectif du Juggernaut calibre a part, GUID et evenements source
conserves. Generics natifs inchanges. Choix artistique confirme : retoucher les
copies des quatre marches en conservant leur style. Quatre copies preparees
dans FacingCalibration/RetouchedWalks : Lucian Walk_BR, Juggernaut Walk_B/L/FL.
Retouche hors runtime des muscles des jambes sur chaque avatar reel ; aucune
installation en production ni modification des vitesses physiques. Walk_L
necessite une posture de bassin abaissee de 3,5 cm constants pour la portee des
jambes : oscillation auteur conservee, pas de suivi vertical force du sol.
Les trois autres gardent la trajectoire du bassin. Premier controle : appuis
verticaux a moins de 0,2 mm dans les fenetres basses identifiees, haut du corps
inchange, ecart horizontal cheville au plus 6,1 mm. Ce dernier chiffre mesure
la retouche, PAS le glissement au sol en locomotion. Captures avant/apres sous
Library/ActorFacingCalibration/RetouchedWalkCaptures. Compilation Unity,
boucles, evenements et seconde execution passes (zero clip reecrit).
Echantillonnage intermediaire : erreur verticale maximale 3 mm pour Walk_L,
moins de 0,4 mm pour les trois autres. Racine physique immobile, orientation
des pieds et du haut du corps conservee. Rapports durables dans RetouchedWalks.
Reste : qualification en mouvement avec UCC/NavMesh, contacts horizontaux,
raccords et composition avec les copies de regard avant integration.

Points ouverts : quatre correlations directionnelles sous 0,75 ; reference
historique Idle_LucianAligned manquante sans entree/default entrant trouve,
non reparee par supposition. Catalogue conservateur graph/skills/overrides
inventorie (157 entrees avant resolution des overrides), pas une preuve que
tous ces clips sont actifs. Sorties Juggernaut : sans IK bloque a 5-6 cm ; avec
IK existante, Strike/Followup candidats, Sweep reste hors seuil. Aucun raccord
retime active. Suite : recuperations rendues, contacts ambigus, pilotes physiques.

Prevalidation et reimport des onze clips passes dans Unity principal 6000.6.4f1,
ainsi que dans le mini-projet sur les quatre rigs. Regard Humanoid borne a
2,55 degres ; pieds/bassin preserves a moins de 0,001 mm. Compilation runtime
et Editor passe. Source/metadonnees, evenements, durees et extraction preserves.
Nouveaux outils/scene sous Characters/AnimationPresentation/FacingCalibration.
Rapports : Library/ActorFacingCalibration ; manifeste durable FirstLotManifest.
Le laboratoire ne contient ni gameplay, ni compensation LucianCombatTorsoFacing.
Aucune reference ou autorite de production n'est migree. Les poses identiques
aux sources ne prouvent pas une absence de glissement physique. Validation
rendue, raccords avec UCC/NavMesh dans Iluvilirae_Test, autres directions et
actions restent necessaires avant integration. Voir README du premier lot.

## Origine du desaxage TwinBlades / StoneGolem (2026-10-24)

Diagnostic termine, sans migration du jeu : le decalage est reproduit sans
scripts dans les poses TwinBlades source et conserve par Based Upon Original,
Offset 0, rotation bakee. Torse Idle : Lucian 58,52 degres / copie Humanoid de
StoneGolem 58,47 ; marche avant 35,56 / 35,82. Root physique : zero rotation.
La pose existe deja sur le rig fournisseur Generic ; ce n'est pas une rotation
ajoutee uniquement par le retargeting StoneGolem. 66 cas Unity a 121 points,
avatars cibles valides, comparaisons Root on/off, Body Orientation et Offset
sur copies isolees. Body Orientation ramene Idle vers -1 degre mais decale
encore la course : aucune correction globale de tous les clips n'est validee.
LucianCombatTorsoFacing est aussi present dans le prefab Lucian actuel ; sa
compensation doit etre distinguee des poses brutes lors d'une future correction.
StoneGolem original reste Generic ; huit sources FBX/metadonnees inchangees.
Preuves : Library/TwinBladesRotationAudit/Rapport.md, verification.json et
Library/RotationResults/report.json dans ce mini-projet. Aucun clip/controller,
prefab, vitesse ni script runtime modifie pour cet audit. Retargeting rendu,
appuis et raccords a valider avant une future calibration de production.

## Continuite Scenes Et Sauvegardes (2026-10-24)

Etat : SOLO rendu qualifie dans le perimetre de recette ; RESEAU COMPLET NON
VALIDE. Aucun essai suivi encore actif. Aucun nouveau projet cree ce tour.

Corrections conservees :
- Archives additives selectives, identites et quantites de depots ; definitions
  d'items resolues avant restauration. Sauvegarde transactionnelle recuperable.
- Completions narratives publiees avec la sauvegarde, pas lors d'un simple visionnage.
- Creation de session cliente avant snapshot, navigation/escouade apres progression
  restauree ; une seule application initiale avant client_ready.
- Hash de prefab enregistre conserve et provider de container installe par les
  fabriques NGO, sans creation d'identite stable sur client.
- ServerUccNetworkTransform conserve l'autorite serveur et aligne le cache UCC
  client sur la pose NGO recue ; snap complet uniquement au spawn/teleport.
  Quatre prefabs joueurs et fabrique/authoring alignes, sans double composant.
- CombatNetworkActorRuntime : NetworkList creees a la declaration, utilisables
  par NGO avant Awake sur acteur inactif ; Awake ne les recree jamais.

Preuves :
- inactive-network-actor-tests.xml : 75 passes, zero echec/ignore, compilation Unity.
- Execution precedente isolee ucc-network-pose-continuity-tests.xml : 74 passes.
  Cinq tests authoring passes dans une execution mixte par ailleurs EN ECHEC
  (42 SetUp refusent la scene dirty). Ne pas presenter cette execution verte.
- Solo 3cfb072ef8f84a1d929fa407c8ef0edb : 84 assertions, zero erreur runtime,
  portails, depot 3/prise 1/reste 2, vraie recharge et echec de sortie transactionnel.
  Approches UCC/defaite sont des fixtures ; pas une preuve d'inputs/combat manuels.
- Diagnostic pose 2b9ea330f79344a4a1060429268b7bf5 : cible NGO correcte mais
  ancienne pose reimposee par UCC. Apres correctif, client atteint le depot.
- Dernier run 9e10931ebdff44f693b7e21a76637e23 EN ECHEC : portail Maison
  et influence reelle du depot passent hote ; late join echoue sur l'objet
  VigilAltar_Hub_Safeguard (hash 1275473975, Maison_Enigme_Vigil), puis
  NetworkObject.Deserialize. Annulation gracieuse, deux editeurs sortis.
  Le warning de serialisation ShadowGuardian et UnityEditor.Search sont conserves.

A reprendre : mappage/despawn des objets de scenes NGO au late join apres portail,
puis vrai pickup RPC/commit et portails avec client actif ; reconnexion/recharge
disque hote ; refus de restauration partielle sans perte du sol ni publication
d'un monde incomplet ; gameplay auteur et mouvement/camera/saut manuels.
Ne pas bypasser les validateurs de build existants, ni modifier l'autorite.

Nettoyage autorise par l'utilisateur pour DEUX copies seulement :
Library/MovingLegsValidationProject (60.46 Gio de fichiers) et
Library/PersistenceNetworkClientProject (18.58 Gio). Suppression automatique
BLOQUEE par l'environnement ; les deux existent encore, aucun espace annonce
comme libere. Racines/reparse points verifies, aucun processus ne les utilise.
Rapports/recettes preserves hors copies ; archive Evidence-2026-10-24.zip
(409 entrees, SHA256 dans rapport), Tools/Validation/PersistenceContinuity.
Originaux Library/PersistenceContinuityValidation conserves. Ne pas recreer
de copie sans accord. Rapport courant : rapport_persistance_reprise.md.

## Interactions Depuis Le Personnage (2026-10-19)

La selection locale ne depend plus du frustum, de la position ou du masque de
la camera. Portee et orientation restent liees au joueur ; obstruction depuis
son origine d'interaction, buffer reutilise et au plus trois rayons par candidat.
Migration des deux champs serialises, sans modification de scene/prefab.
Tests ajoutes : camera eloignee/retournee, renderer masque, mur, portee et dos.
Compilation runtime et Editor (tests inclus) reussie avec le compilateur et les
references Unity du projet, sorties isolees dans Library/PortalInteractionValidation.
Tests physiques et parcours Play non executes ; verifier en Maison le portail
avec camera eloignee puis un obstacle entre joueur et portail.

## Portails Sans Influence Lumineuse (2026-10-19)

`PortalController` rejoint les exceptions du filtre d'influence partage.
Distance, visibilite, destination et activation restent inchangees ; aucun
prefab ni scene modifie. Deux cas de non-regression couvrent les prefabs de
transition et de teleportation locale sans Flame active.
Verification du diff effectuee ; compilation et tests Unity non executes dans
ce lot. A valider en Play : portail utilisable hors influence, controles de
distance et de visibilite conserves.

## Integration Du Combat Valide Au Jeu (2026-10-19)

Correction demandee apres essais auteur : exploration et combat redeviennent
distincts. Retrait de la reactivation individuelle BasicAttack/SelectSkillKit en
exploration ; admission des attaques libres uniquement en InputMode.Combat et
roue refusee en exploration. Sans lock reste autorise en combat. Les recettes et
preuves historiques d'attaques/charge en exploration ci-dessous correspondent
a l'ancien contrat et ne qualifient plus cette regle. Compilation isolee reussie,
55 tests FreeAttackTests passes, dont cinq cas de modes/refus sans effet. Pas
de Play Mode ni de reprise de la campagne generale mise en pause.

Objectif actif : porter completement le combat valide du laboratoire dans la
partie normale, sans remplacer les rencontres par les fixtures de test.

Lot charge en exploration : -combatMigrationCharge complete la recette Gamepad
d'aller-retour Maison/District/Maison, sans override d'asset ou appel direct de
charge. Cas dfba2a9d62c5430ca189107904d2a224 PASSE : dans chaque destination,
relachement pendant la fin Basic ralentie sans lancement, puis maintien avec
generation/clip continus, ralenti et cadrage observes, lancement unique apres le
seuil auteur, cooldown consomme. X reste tenu brievement apres lancement sans
repetition. Vitesse Basic/camera/sol/commandes restitues, aucun engagement ou lock
sur ces coups dans le vide ; transfert/PV/progression et snapshot passes.
Compilation native reussie, 119 empreintes principal/copie identiques. Preuves :
Library/ProductionCombatMigration/Charge. Aucun runtime ni asset auteur modifie ;
suite elargie non rejouee dans ce lot (derniere : 544 passes, un echec Etienne,
un Explicit ignore). Projection ciblee/flottement, interruptions, ressenti et
reseau restent distincts ; ReadyForProduction faux.

Lot defenses : recette optionnelle -combatMigrationDefenses sur la vraie rencontre
depuis Bootstrap, Gamepad prive sans appel direct de roulade/garde. Cas
5d4f296413df4de0b8e67f70d0555596 PASSE : deux B avec vrai mouvement UCC
(2,87 et 3,49 m), deux Y hors fenetre de contre avec garde tenue puis relachee,
restitution action/input/sol a chaque cycle. Ennemi non suspendu, aucun lock
camera ou contre force ; fuite, reengagement, troisieme impact, retour home et
snapshot passent ensuite. Flame existante allumee comme fixture explicite.
83 empreintes principal/copie identiques ; preuves dans
Library/ProductionCombatMigration/Defenses. Ce lot ne prouve ni reduction d'un
impact garde, ni esquive garantie, contre cinematographique, ressenti ou reseau.
Compilation native reussie ; regression elargie : 544 passes, un echec de
couverture Flame Etienne reservee a l'auteur, un Explicit non execute.
ReadyForProduction reste faux.

Lot sprint/saut : recette -combatMigrationLocomotion ajoutee au meme parcours
Gamepad. Baseline : vrai saut, mais MissingMethodException sur le callback OnJump
de NetworkCharacterInput (InputValue contre CallbackContext prive). Les quatre
callbacks reseau Jump/LocomotionMode/TriggerMunin/ToggleTorch sont renommes OnRouted*,
sans changer leurs corps, RPC ou autorite ; aucune reference UnityEvent auteur
trouvee. Le controle d'atterrissage attend la restitution effective dans sa borne
de cinq secondes, sans forcer la liberation d'un verrou pendant le fondu auteur.
Cas 7e1e274b5f814e30bfae8f4651a7a91f PASSE : RT route/applique, tier course 3,25,
vitesse physique accrue dans District et Maison, relachement propre ; deux sauts
successifs par zone, montee/air/sol observes et commandes rendues, aucune exception
de runtime dans cette phase. Hauteur/vitesse auteur et bindings inchanges.
Regression : 544 passes, un echec Etienne Flame auteur, un Explicit ignore ; quatre
nouveaux tests de collisions de noms passent. 75 hashes source/copie identiques.
Preuves : Library/ProductionCombatMigration/Locomotion. Roulade/garde en engagement,
charge, familles de skills, rencontres et reseau restent a qualifier ; aucune
qualification globale ni de ressenti visuel deduite. ReadyForProduction faux.

Lot commandes natives : -combatMigrationGamepad ajoute a la recette de transfert
existante, uniquement dans la copie isolee. Cas 125c75c0dc9d43129a260098dd757546
PASSE sur sources synchronisees : impulsion de stick -> routeur -> UCC -> vrai
deplacement (District 1,03 m ; Maison 0,87 m), alignements camera >0,997 ; pression
X -> une Basic, fin propre sans doublon, repetition ou charge. Selection d'un
trajet sur capsule/sol UCC reels ; un premier refus de fixture generique est garde.
Controle de provenance : les quatre prefabs de la copie avaient encore .2 au
lieu du .5 deja auteur pour le contre reseau dans le principal. Synchronisation
principal -> copie uniquement, aucune modification du gameplay principal ; quatre
tests de valeurs auteur ajoutes. Suite elargie : 540 passes, un echec Etienne
Flame auteur, un Explicit non execute. Soixante et une empreintes identiques.
Preuves : Library/ProductionCombatMigration/Gamepad. Reste notamment sprint,
saut/roulade/charge et familles de skills en production, rencontres et reseau ;
pas de ressenti manuel ou huit directions qualifies par ces deux impulsions.
ReadyForProduction reste faux.

Lot transitions : recette optionnelle -combatMigrationZoneRoundTrip ajoutee au
validateur Bootstrap existant. Cas 6bf1904b99794059aa90056332b7b46b PASSE : vrai
portail Maison admis via ServerTryUse, District 1 complet, Basic et fin propre,
ReturnToHub(Default), Maison complete et nouvelle Basic terminee. Meme acteur/
binding/PV/progression conserves, monde combat unique, NavMesh Ready portant la
bonne zone, anciennes scenes dechargees en solo, controles/interactions autorises.
Placement UCC de Lucian pres du portail uniquement comme fixture ; pas une preuve
d'input Interagir manuel, de mouvement au stick, d'audio ou de reseau. Aucun
runtime ou asset auteur modifie dans ce lot. Regression elargie : 536 passes,
un echec Etienne (couverture Flame auteur), un Explicit non execute. Vingt-neuf
empreintes sources/assets/metas identiques a la copie validee.
Preuves : Library/ProductionCombatMigration/ZoneTransfer. ReadyForProduction faux.

Lot couverture des zones : recette Bootstrap parametrable par manifeste, sans
fallback silencieux ni seconde infrastructure de qualification. Maison reproduit
un spawn a l'origine squad car LucianSpawnPoint etait un simple Transform. Ajout
de ZoneSpawnPoint Default/slot 0 SUR cet objet, sans deplacer pose/parent/GUID.
Recette 7a37b21178774e04a2d2920f775a5641 PASSEE : dix scenes, NavMesh Ready,
Lucian seul au spawn auteur, grounded, Basic/fin propre, curseur natif et snapshot
ecrit/lu. Interactions de Maison et reseau restent non qualifies ; transfert solo
par API couvert par le lot ci-dessus.
Suite elargie finale : 536 passes, un echec reel Etienne (couverture Flame reservee
a l'auteur), un Explicit ignore ; les sept nouveaux tests passent, aucune
assertion retiree. Dix-neuf empreintes sources/assets/metas identiques a la copie
validee. Backup scene/meta, manifeste cible et preuves dans
Library/ProductionCombatMigration/ZoneCoverage. ReadyForProduction reste faux.

Lot recuperation physique : sortie bornee d'un support non navigable
implementee dans le moteur production existant, sans changer collider, tolerance
NavMesh ni asset auteur. Balayages planaire/vertical separes, gravite conservee,
destination locale libre et connectee, nettoyage par generation/cinematique/mort.
Compilation et regression : 483 passes / 0 echec / 1 Explicit non execute,
dont neuf nouveaux cas de geometrie, mur, etage et proprietaire. Premier essai
physique reel garde en echec (timeout de sortie). Essai suivant : deux impacts,
fuite et retour oriente passes, mais reengagement non obtenu ; la fixture
employait un rayon generique a 1,2 m au lieu du regard auteur. La recette utilise
maintenant eyeHeight/targetHeight/obstructionMask et verifie la perception exacte
apres placement UCC. Le diagnostic suivant confirme une penetration deja acquise
de Flame/MeshCollider (0,026 m) et WoodFire/MeshCollider (0,237 m) : le rayon
central d'atterrissage ne protegeait pas le volume entier. La descente, ruee incluse,
utilise maintenant le balayage complet de capsule, sans snap vers le rayon plus
bas. Une face inclinee bloquante permet la sortie physique, pas le rattachement
NavMesh ; le test de support decale couvre descente normale et ruee, deux angles.
Recette rendue d14f503cc325488a82628d855da4fe58 PASSEE : Bootstrap/District 1,
deux attaques, fuite et restitution des controles, reengagement pendant retour,
troisieme impact, seconde fuite, retour home avec NavMesh valide et orientation
auteur, aucun impact tardif. Snapshot monde ecrit/lu. Dix-neuf empreintes
sources/assets/metas identiques a la copie validee. Allumage de Flame fixture
toujours explicite ; pas une preuve d'interaction narrative, fuite au stick,
BattleWall, autres rencontres ou reseau. ReadyForProduction reste faux.
Artefacts : Library/ProductionCombatMigration/LandingRecovery.

Controle supplementaire de rencontre reelle : la recette Bootstrap accepte
-combatMigrationEncounter. Avant correctif, le vrai Juggernaut a detecte Lucian
et engage la facade sans lock, puis reste 65 s sans mouvement ni impact malgre
un agent valide. Son kit auteur contient uniquement Assomoir ; preferMeleeApproach
pouvait exclure tout son kit apres le choix aleatoire d'alternative aerienne.
Le filtre conserve maintenant un kit uniquement aerien ; un kit mixte garde sa
preference melee. Approche et retour liberent aussi l'arret de l'agent avant
SetDestination. Aucun skill ajoute, aucune identite ou regle de Flame remplacee.
La recette peut explicitement allumer une Flame EXISTANTE couvrant l'ennemi
dans la copie jetable, puis restaurer son etat : ce fixture ne valide pas son
interaction ni la progression. Aucun placement de Flame, notamment chez Etienne.
Ecart de vision resolu par preuve d'import natif : une ligne vide precedant
vision dans Juggernaut.asset faisait ignorer cette section (6 m par defaut au
lieu des 30 auteur). ForceImport seul ne corrigeait rien ; deux copies temporaires
avec/sans ligne vide reproduisent 6/30. Suppression de cette seule ligne, GUID et
valeurs inchanges, sauvegarde/metadonnee conservees. Aucun autre cas analogue
dans les 19 CharacterData scannes ; pas de nettoyage general des fichiers Unity.
La vision correcte a revele un second blocage apres le premier Assomoir : trop
pres pour sa portee minimale, l'agent visait encore le joueur avec un arret plus
grand que sa distance. Il peut maintenant reprendre sa distance par chemin
NavMesh complet, seulement si tous les patterns sont trop proches ; marge de
portee interieure, aucune translation directe, aucune interruption de coup.
Recette 52de58fb32b245f6b2487b975f212307 passee : vrai Juggernaut, vision 30 m,
aggro sans lock, vitesse 3.62 m/s, approche 5.61 m, recul de positionnement,
deux generations d'impact et PV Lucian 1000 -> 960, recuperation observee.
Fixture de Flame existante toujours explicite : pas une preuve d'allumage ni de
progression. Fuite/retour/reengagement, autres rencontres et reseau restent a
valider. Preuves : Library/ProductionCombatMigration/VisionImport et
Logs/CombatVisionImportDiagnostic.log, CombatEncounterRangeRetreat.log.
Controle lifecycle ajoute via -combatMigrationEncounterLifecycle : deux sorties
de zone placees par UCC et reapproche pendant le retour, sans appel de fin/aggro
force. Baseline : retour a reculons confirme (dot -0.915). Corrige par rotation
vers home avec le profil existant. La sortie de leash ne peut plus annuler une
attaque deja engagee sur une cible vivante ; fin normale puis abandon du combo.
Essai suivant : controles/autorisation d'interaction restitues deux fois, facing
retour dot 1, reengagement pendant retour et troisieme impact (PV 940) passes.
Avant le lot recuperation ci-dessus, recette totale en echec : Recovery avec agent
desactive persiste, aucune suspension Flame/cinematique. Diagnostic suivant
reproduit et precise : motion=True, airborne=False, endRequested/abort=True,
vitesse verticale 0 ; sol = collider Flame dans District_1_Corridor_Flammes,
point (-4.47,-93.93,44.21), root a +0.03. Aucun polygone a 0.15 m ; plus proche
(-3.80,-94.24,44.21). L'ennemi est pose SUR la Flame non navigable, et le moteur
n'a pas de sortie physique avant restitution NavMesh. Ce n'est pas un simple
manque de tolerance : ne pas activer l'agent, ignorer le collider, warper ou
rebaker pour masquer le cas. Correctifs et preuves actuels de recuperation
physique dans le lot ci-dessus ; rattachement seulement apres vrai retour au
sol local valide.
Artefacts : Library/ProductionCombatMigration/Lifecycle ; journaux baseline,
fixed et CombatLandingLifecycleDiagnostics.log. Derniere recette passee dans
LandingRecovery ci-dessus ; ces echecs restent conserves separement.
Spawn initial corrige : GameFlow capture l'existence du JSON personnage avant
restauration et utilise le ZoneSpawnPoint de la scene primaire pour une nouvelle
partie sans spawnId. Un fichier existant, meme en echec de restauration, n'active
pas ce fallback ; arrivee explicite et option editeur conservent leurs contrats.
Recette 39e49776f3f2450bbb3f034a7ded5ae9 passee : Lucian au spawn auteur
(-89.88,-93.94,51.88), sol physique confirme, Basic, sauvegarde a une pose distincte,
menu/recharge avec cette pose conservee (-91.87,-93.94,52.91), PV 993, acteur unique,
progression et mort fixture sous marker conservees, navigation Ready. Premier
essai de fixture sauvegarde refusait un polygone NavMesh non requis pour UCC ;
remplace par sol physique et espace capsule libre, echec garde separement.
Preuves : Library/ProductionCombatMigration/Spawn. Ce cas ne valide pas a lui seul
la rencontre, toutes les zones, une victoire jouee ou le reseau.
Regression avant recuperation : compilation reussie, 474 passes / 0 echec / 1 Explicit
non execute ; trois nouveaux cas de protection du coup hors leash. Dix-huit
fichiers (sources, assets, metas, prefab et scene du cas physique) compares par
SHA-256 a la copie compilee dans Lifecycle. Aucun asset auteur modifie dans ce
lot. Le correctif anterieur d'une ligne vide reste manifeste dans VisionImport.
Pas de preuve reseau ni de qualification globale deduite de ces resultats.
Echecs conserves dans Library/ProductionCombatMigration/Encounter ; journaux
Logs/CombatProductionEncounter*.log. ReadyForProduction reste faux.

ProductionCombatMigration promeut les reglages d'Ascension vers le SkillSO
canonique (GUID conserve), les trois Basics aeriens et les controllers existants.
Profil GameplayCombatCamera dedie : seule la section actionFraming vient du lab,
les reglages d'exploration restent ceux de la camera de production.
Audio de contact auteur pour les quatre joueurs ; reaction additive du vrai
Juggernaut apres controle des colliders de son torse. Pas de copie de la lumiere
de laboratoire ni de remplacement de ses patterns, donnees ou identite.
Reseau : BasicTail utilise la session Basic autoritaire et une presentation
repliquee par epoque/generation ; Burst et freinage Rupture viennent du monde
serveur. Point de contact confirme transmis aux feedbacks, audio leger/lourd
sans lecture double, reaction de torse apres impact confirme seulement.
Bootstrap reference maintenant explicitement District 1 dans zoneManifests :
21 additives + Core, NavMesh pre-bake Ready 2/2 anchors, Lucian seul et monde
commun sans arene. Historique et preuves initiales dans
Library/ProductionCombatMigration/CombatFeel et le rapport detaille.
Raccords supplementaires implementes : rotations dans PlayerSettings et profils
ennemis ; curseur prefab +3 Y dans CombatWorldRuntime, nettoyage natif a la
desactivation/destruction ; buffer mobilite et reactions dans leurs composants
communs. Les surcharges de l'arene sont retirees apres migration en deux phases.
Installation isolee 6ac70eb06a8f447982566ddf9fb84be5 idempotente ; onze fichiers
auteur livres avec sauvegarde et metadata preservee. Le vrai cycle sauvegarde/menu/recharge
a revele puis permis de corriger : dechargement impossible de la derniere scene,
PV joueur absents du JSON, identite solo invalide et double spawn apres snapshot,
provider d'ennemi ajoutant un marker vide au lieu de lire son vrai parent.
GameFlow garde la derniere scene jusqu'au Single du menu. JSON v9 conserve les PV
avec compatibilite ancienne ; SquadManager reutilise l'identite et l'acteur du
registre. Provider de marker utilisable sur acteur inactif, sans Awake obligatoire.
Recette rendue 66701034d42a44f5b225c5bc644f902f passee : un seul Lucian apres
recharge, PV 993 conserves, progression inchangee, ennemi fixture toujours mort
sous son vrai marker et navigation de nouveau Ready. Pas une victoire jouee ni
une preuve de toutes les zones/reseau. Sources et onze fichiers auteur
compares par SHA-256. Preuves dans
Library/ProductionCombatMigration/Controls et Logs/CombatControls*.log, y compris
les echecs precedents conserves separement.
Retour auteur confirme dans le lab : ouvertures/reprises du Juggernaut et
distinction des coups satisfaisantes ; camera large et ChargedSkill preserves.
Reste : parcours complet des zones/rencontres, autres cycles de sauvegarde,
qualification exhaustive des abonnements/inputs et vrai host/client ;
ne pas declarer ReadyForProduction ni reprendre les chiffres historiques comme
preuves des sources courantes. Flame d'Etienne toujours reservee a l'auteur.

Lot suivant : audit integral en lecture seule, zero ecriture auteur proposee,
19 sujets ennemis, 34 scenes et 8383 sujets d'inventaire. Baseline avant nettoyage :
166 blocages, dont 150 references GUID (77 distincts), trois chargements dynamiques,
un registre reseau et les gates/provenance de copie non attestes. Ne pas confondre
l'absence de Git dans la copie avec un defaut du jeu. Les deux ZIP indiques par
l'auteur dans D:/LitAssetArchive ont ete lus : 8987 metas valides, aucun GUID
recherche ni clip/profil auteur correspondant ; aucune restauration hasardeuse.
Deux cycles natifs Bootstrap/Basic/menu/recharge/sortie passent avec Domain Reload
On, puis deux avec Off. Options editeur isole restaurees. Un premier essai Off a
expire sur une Basic au-dela de 8 s ; non reproduit avec diagnostics, NON corrige
par allongement du delai et toujours ouvert. Ne pas transformer ces repetitions
bornees en preuve de tous les callbacks/manettes ou des onze gates.
Nettoyage explicite du diagnostic supprime en Git 0b219dcd : 70 documents orphelins
dans 10 controllers, aucun lien de comportement actif, clip, transition ou event
retire. Plan SHA-256, sauvegardes et metadata preservee ; livres au projet principal.
Regression apres nettoyage : 453 passes / 0 echec / 1 Explicit non execute.
L'audit classe desormais les seules icones moteur GUID d comme ressources integrees
quand AssetDatabase resout exactement Library/unity editor resources ; les autres
GUID restent controles. Preuves : Library/ProductionCombatMigration/SessionCycles
et Logs/CombatBootstrapRepeat*.log, CombatSessionCyclesRegression.log.

## Presence Du Combat (2026-10-19)

Objectif valide par l'auteur le 2026-10-19 apres l'echange manuel demande.
Perimetre reste
Lucian/Juggernaut dans Iluvilirae_Test, sans nouvelle mecanique ni migration.
CombatContactPose installe explicitement sur ChargedEnemy_Lab : reaction additive
breve du torse apres un contact confirme, attenuee pendant un coup engage,
sans interruption, changement de degats ou mouvement de racine. Hurt, mort,
cinematiques et flottement d'Ascension gardent leur presentation. Aucun collider
sur les os modifies du prefab actuel. Charge et regles d'annulation inchangees.
Retour auteur : premiere passe bien recue, mais conserver la distance d'exploration
large facon BG3 et rapprocher seulement selon les actions. Le cadrage permanent
7,5 m / 38 / 58 est remplace par la base 10 m / 50 / 60 avec zoom relatif lisse
dans UCC : Basic 0,88, skill 0,78, preparation/frappe ennemie proche 0,92.
Retour vers la distance manuelle apres l'action, aucune modification du yaw/pitch.
Zoom manuel prioritaire 1,5 s ; pas de cumul avec le cadrage de charge valide.
Limite de rapprochement par projection des corps dans le champ. TacticalDefault
reste inchange, fonctionnalite desactivee par defaut hors du profil laboratoire.
Lumiere CombatFeelFill de scene pour rendre l'armure lisible, sans flash/shake.
Installation explicite idempotente : scene, prefab et profil identiques en octets
au second passage. Assets controles puis rapatries avec leurs metadonnees.
CombatFeelFinalRendered.log passe en Play Mode isole avec HDRP : trois contacts
reels de Basic (5/10/15 PV), un impact chacun, reaction visible mesuree, racine
immobile et retour a la pose normale. Captures inspectees dans la copie isolee,
Library/CombatFeel/ContactVisual. Premiere recette ScreenCapture sans Game View
ne produisait pas d'image ; remplacee par une requete de rendu HDRP effective.
Ne pas assimiler ces trois contacts a une validation du ressenti general.
CombatFeelRegressionTests.xml : 84/84 Edit Mode, incluant les nouveaux tests de
presentation, FreeAttackTests, continuite du contact et protections de charge.
Compilation runtime/outils reussie dans la copie isolee ; pas de campagne reseau.
CombatContactAudio remplace la lecture existante du son de contact sur Lucian lab,
sans couche supplementaire : registre leger vers 1,05, troisieme Basic vers 0,91,
variation deterministe par generation sans consommer le Random de l'IA. La charge
utilise le registre lourd ; voix, musiques et cinematiques gardent leur lecture.
CombatFeelAudioPlay.log passe : trois sons pour trois impacts reels, chacun unique.
CombatActionCameraFinalPlay.log passe : rapprochement pendant les Basics, retour
au large, distance de zoom manuel preservee, commande moteur zoom 10 -> 12 et
orbite +12 degres prioritaires pendant un coup. En batch sans focus, la premiere
injection avant Update etait nettoyee normalement par Application.isFocused ;
la recette finale injecte au consommateur UCC. Ce n'est pas un test de manette
physique ni un contournement du focus dans le runtime.
CombatActionCameraAerial.log passe avec rendu HDRP : debut, montee, sommet,
Basic aerien, descente, sol ; tete et pieds des deux avatars dans le champ avec
marge de 2 %, contact unique, navigation et commandes restituees. Captures lues.
Le prefab Lucian conserve le GUID de script genere dans le projet principal ;
copie isolee alignee dessus, pas d'ecrasement de ses metadonnees originales.
CombatActionFeelTests.xml : 93/93 sur le perimetre contacts/charge/continuite/
attaque libre/nouveau cadrage. Suite avec les anciens tests tactiques : 186/190,
quatre echecs dans LitTacticalCameraTests, non masques ni modifies pour verdir :
Bootstrap attend initialMode=0 mais le prefab existant porte 1 ; fixture BattleWall
inactive non trouvee par GetComponentInParent ; Dispose de PlayerInputs utilise
Destroy en Edit Mode ; MovementType sans personnage initialise leve NullReference.
Ces cas ne passent pas par le nouveau calcul de rapprochement. Ils restent a
traiter, sans annoncer une qualification globale. Mathematique camera, moteur,
stabilite, mouvement et near clip existants passent dans cette meme suite.
Recuperations du profil Clash du lab mesurees sur l'avatar reel, puis prolongees
sans retimer les impacts : 1,05 / 0,985 / 1,017 s au lieu de 0,2 s. Sorties vers
1,783 / 1,885 / 1,783 s du clip ; score d'ecart pieds/hauteur bassin vers Idle
0,389 -> 0,044, 0,546 -> 0,058, 0,352 -> 0,045. Ce score mesure un raccord de pose,
pas une distance de glissement observee. Aucun IK ou playback experimental active.
L'attente utilise le follow-through auteur plutot qu'un retour tronque en Idle.
La recuperation plus complete a revele une course : le trigger Attack pouvait
etre perdu pendant le fondu precedent vers Idle. CommitFluidAttack utilise
desormais directement l'etat du profil, comme le chemin production existant.
Pas de seconde autorite Animator, pas de changement d'impacts ni de contre.
CombatFollowThroughTests.xml : 161/161, incluant continuite Brains, clash, contre,
paliers, mort, generations, charge et idempotence de la recuperation mesuree.
CombatExchangeStateContinuity.log passe : 30 s apres vraie stabilisation de la
simulation, trois reprises calmes sans faux depart. CombatExchangeFinalRendered.log
passe sur les memes sources : 484 echantillons, 16 actions ennemies de trois types,
18 lancements joueur, 8 contacts joueur et 15 baisses de PV observees ; trois
reprises calmes sans faux depart. Captures HDRP sans nombres inspectees dans
Library/ActorAnimationSandbox/Library/CombatFeel/Exchange. Les PV de cette fixture
seule sont portes a 1000 pour observer le rythme, sans immunite ni asset modifie.
Les premiers essais ont ete refuses car le cout d'entree Play Mode etait compte
dans les 30 s ; la recette attend maintenant 0,5 s de simulation avant de compter.
La recette headless avec intention continue produit les trois coups du combo ;
la recette rendue finale n'a produit que les deux premiers, sous pression.
Retour manuel final : "oui c'est vraiment bien" sur les ouvertures, reprises
et distinction leger/puissant, sans s'appuyer sur les nombres de degats.
Conserver cette base : vue large, rapprochements ponctuels et ChargedSkill valide.
Aucune retouche speculative supplementaire. Cette acceptation du ressenti au
laboratoire ne constitue pas une qualification production ou reseau, ni une
certification des appuis de chaque clip. Les quatre echecs de l'ancienne suite
tactique restent ouverts.

## ChargedSkill Sur La Fin Du Basic (2026-10-19)

Ressenti BasicTail valide explicitement par l'auteur ; conserver ses valeurs.
Protection renforcee : TryPlay refuse les remplacements ordinaires pendant la
charge, InterruptBasicSkillForDamage refuse son annulation directe, et le nettoyage
preserveChargedHold conserve la Basic bufferisee porteuse de la charge. Mort,
cinematique, relachement de X et nettoyage force restent possibles ; les PV sont
toujours retires. ChargedArmorFinalTests.xml : 27/27 sur les sources finales,
y compris conservation de la destination bufferisee avec un manager reel de fixture.
ChargedTailArmorPlay.log passe : perte de 7 PV pendant la charge, interruption
directe/animation concurrente/mobilite refusees, generation preservee, Ascension
unique puis retour sol, vitesse/input/camera restitues. Compilation reussie.
Valeurs et animations validees inchangees ; pas de campagne reseau ni de nouveaux
essais de ressenti manuel dans cette passe.

Mode BasicTail ajoute au ChargedSkill lab seulement : meme session et clip Basic,
ralenti 0,1 apres 70 % et apres tous les evenements non cosmetiques, fondu 0,08 s,
pose finale tenue sans boucle. Relachement : reprise normale jusqu'a la fin du
geste. A 2 s reelles depuis la pression, lancement normal d'Ascension valide avant
remplacement, sans Idle intermediaire. Parametre Animator reserve aux trois
Basics terrestres ; pas d'ecriture du temps global ou Animator.speed. Verrous UCC,
camera et vitesse restitues par generation ; input bufferise rattache a son vrai
Basic de destination. Production et reseau gardent leur preparation existante.
ChargedBasicTailInstall.log : compilation/installation reussies ; premiere suite
ChargedBasicTailTests.xml : 5/5. Suite finale ChargedBasicTailFinalTests.xml :
26/26 (charge, maintien, nettoyage/generation, buffer et continuite des maps).
ChargedBasicTailPlayContinuity.log : six cas passent, relachement des trois coups
puis trois lancements uniques a 2 s, restitution vitesse/input/navigation.
Cette recette a refuse son dernier cas car elle verifiait le gel avant le tick
Animator correspondant ; controle corrige apres stabilisation de 0,05 s, puis
nouvelle recette terminale distincte. Les essais refuses ne sont pas des PASS.
ChargedBasicTailFinalPose.log passe : pose terminale tenue plus de 1,2 s sans
recuperation intempestive, puis relachement, camera/input/vitesse restitues ;
sortie Unity 0. Les six autres cas gardent leur preuve precedente sur le meme
runtime. Ressenti manuel, interruptions en rencontre complete et reseau non
rejoues dans cette passe. Aucun processus de validation laisse actif.
La fixture attend les inputs et le retour UCC au sol apres son placement.
Le premier contact a revele une vraie coupure : SetBaseMode(Combat, preserve=true)
desactivait les maps si le mode etait deja Combat, annulant X tenu. La reapplication
Combat conserve maintenant les actions quand explicitement demandee et sans
contexte superieur ; menus et cinematiques restent prioritaires. Regression ajoutee
aux tests de continuite, ainsi que nettoyage idempotent et destination bufferisee.

## Combo Aerien Lucian (2026-10-19)

Deux BasicSkills aeriennes ajoutees dans Iluvilirae_Test : coups 2/3 de 10/15 PV,
clips dedies Twinblades_Air_Attack_1_Inplace / _2_Inplace du pack (corps entier,
plus de coups au sol adaptes). Remplacement des copies sans changer leurs GUID.
Impacts/VFX recales selon leur phase normalisee. Liste de
Lucian_ImpactLab completee (trois coups), deux etats Animator installes ; aucune
configuration de production modifiee. Pressions distinctes et buffer existants,
aucun nouveau maintien de gravite ni prolongation du flottement charge.
LucianAirComboSetup preserve les assets deja crees et est raccorde a l'upgrade
du laboratoire. Les anciens LucianAirComboTests.xml (4/4) et LucianAirComboPlay.log
validaient les swings adaptes, pas le remplacement Twinblades. Les nouvelles
preuves restent distinctes. Le ressenti visuel manuel et l'enchainement complet
par pressions en conditions de combat restent a essayer.
LucianTwinbladesAirInstall.log : compilation et remplacement reussis, GUID
preserves. LucianTwinbladesAirTests.xml : 6/6, dont comparaison des poses
Humanoid avec les deux sources Twinblades, impact unique et idempotence.
Les sources et les deux clips livres ont les memes empreintes que la validation.
LucianTwinbladesAirPlay.log : deux cas passes avec les nouveaux clips, etats 2/3,
un impact chacun apres Ascension, retour sol et commandes/NavMesh restitues.
Sortie 0 ; aucun processus de validation laisse actif.

## Allegement des assets telecharges (2026-10-18)

Premiere passe Fab : 7 558 assets et leurs meta retires d'Assets apres audit
Unity, controle des GUID et archive SHA-256 verifiee sur D:. Gain source :
21 853 578 821 octets. Archive et manifeste de restauration :
D:/LitAssetArchive/2026-10-18-fab/. Tous les chemins Assets conserves existent ;
aucune reference GUID restante vers les candidats retires. Huit tests des
outils passent. Reouverture Unity batch reussie : audit post-nettoyage a zero
candidat restant, aucun chemin Assets conserve manquant apres refresh.
Seconde passe sur 14 perimetres : 1 429 assets et meta supplementaires retires,
6 171 847 403 octets. Total : 8 987 assets, 28 025 426 224 octets. Seconde
archive : D:/LitAssetArchive/2026-10-18-other-packs/. Controle GUID cumule passe,
aucun chemin conserve manquant ; audit Unity final a zero candidat restant.
Dix tests Unity cibles et treize tests des outils passent. Pour restaurer les
deux passes : other-packs puis fab. Details : systems/assets_cleanup.md.
Pas de recette visuelle complete ni de suppression manuelle de Library.

## Objectif combat physique et reactif (2026-10-18)

Verification avec rendu du ChargedSkill en cours, pas seulement -nographics.
RunMotionVisualBatch capture camera gameplay/depart/montee/sommet/Basic/chute/sol.
ChargedImpulseMotionVisual.log puis VisualSync.log refuses : sommet Lucian trop
bas (2,49 m puis 2,34 m de gain mesure), ennemi 3 m. Attendre seulement le timer
ou la fin de l'ability ne suffisait pas. Correctif candidat : phase de montee
attend l'ability UCC au pas physique ; en mode Impulse, intention de deplacement
calculee depuis la position simulee vers la hauteur courante de la courbe,
bornee par la vitesse de lancement. Rattrapage limite a 0,15 s, collisions et
plafonds prioritaires. Aucun SetPosition ni teleportation.
ChargedImpulseMotionVisualHeight.log passe : Lucian Y=2,90 / ennemi Y=3,00 au
sommet, gain mesure 2,879 / 3,000 m, ecart 0,10 m. Basic aerienne puis retour
au sol, ressources restituees. Six captures fraiches inspectables sous
Library/CombatFeel/ChargedVisual ; sommet et descente inspectes. Camera gameplay
non modifiee, controle fonctionnel pas une preuve de ressenti manuel.
Regression de cette derniere correction passee : ChargedImpulseHeightSafety.log,
plafond 0,897 / 0,925 m de montee, annulation 1,520 / 1,644 m, deux retours au sol,
input/NavMesh restitues, impacts uniques, sortie 0. Aucun processus laisse actif.
Les anciens 8 tests ne constituent pas la preuve de ce raccord ; les deux nouvelles
recettes avec rendu et de securite sont ses preuves ciblees. Le ressenti manuel
du ChargedSkill reste attendu depuis plusieurs tours ; ne pas retoucher les valeurs
ou les autres gestes deja acceptes sans ce retour. Objectif global non atteint.

Diagnostic de l'echec de deconnexion : dans le package Input System installe,
InputManager.defaultUpdateType retourne Editor hors Play Mode ; InputActionState
n'evalue pas les actions de jeu dans cette phase. Le test actuel change la valeur
brute de X mais ne verifie pas l'entree en Performed avant d'attendre canceled.
La fixture doit effectuer explicitement un update Dynamic et verifier cette phase,
sans retirer l'assertion d'annulation. Aucun defaut de charge runtime etabli par
cet echec. Ecriture redevenue possible : fixture corrigee via le switch du package
runPlayerUpdatesInEditMode, sauvegarde/restauration en finally et verification de
Performed avant deconnexion. Un simple update Dynamic sans ce switch est lui-meme
ignore hors Play Mode (ShouldRunUpdate) ; premier reessai garde donc 7/8.
ChargedInputLifecycleFinalTests.xml : 8/8 passes, zero echec, compilation reussie.
L'action est verifiee Performed avant le retrait, canceled et lecture sure du
controle retire restent exiges ; switch restaure en finally. Aucun changement
runtime des inputs ou de ressenti. Aucun processus de test laisse actif.

Retour auteur : Rupture est bien, conserver. Nouvelle priorite explicite :
ameliorer fortement la montee et la retombee des deux acteurs du ChargedSkill.
Prototype Impulse isole dans Skill_AscensionLumineuse_Lab.asset, reference par
Lucian_ImpactLab ; SO canonique, Lucian de production et catalogue reseau non
remplaces. InstallImpulsePrototype cree uniquement cette copie et son lien.
Montee partagee UCC/ennemis : h=H*t*(2-t), 3 m en 0,36 s, depart energique,
arrivee progressive au sommet. Flottement/combo manuel 1,2 s conserve, aucun
second coup automatique. Chute cible acceleree (2,5 m/s initiaux, 42 m/s2,
plafond 18 m/s), Lucian reprend la gravite UCC avec impulsion initiale de chute.
Feedback du coup de projection active : impact/audio existants, hit-stop court,
aucun kick camera ni shockwave. Les assets non prototypes gardent la trajectoire
ConstantVelocity et les reglages existants. Ancienne coroutine d'ascension
verifie sa generation avant d'arreter l'ability de Lucian.
Tests mathematiques 30/60/120 passes ; premier groupe ChargedSkillTests 7/8,
echec du test de deconnexion (callback canceled non observe), sans changement
runtime d'input. Tentative de correction de fixture retiree car overload Input
indisponible dans ce package. Compilation et recette courte de mouvement passent :
ChargedImpulseMotionPlayFinal.log, montee reelle Lucian 2,650 m / ennemi 2,999 m,
contact de lancement unique, Basic aerienne demarree depuis le flottement,
deux acteurs au sol et navigation/commandes restituees (1,606 s apres sommet).
L'ecart de hauteur au sommet est 0,349 m : ne pas annoncer des poses identiques
ni une hauteur physique exacte pour UCC. Ancien groupe Edit Mode : 7/8, meme
echec DeviceRemovalDoesNotReadDisconnectedChargeControls (callback canceled),
ChargedImpulseArcFinalTests.xml ; tests des trajectoires et isolation laboratoire
passes, aucune assertion retiree. Ce defaut de fixture est maintenant corrige :
8/8 dans ChargedInputLifecycleFinalTests.xml ci-dessus, pas une suite globale.
SO canonique et Lucian de production : diff nul. Source et copie testee identiques
sur le runtime modifie et le nouvel asset. Recette courte supplementaire
RunMotionSafetyBatch passee : ChargedImpulseMotionSafety.log. Plafond bas :
montee limitee Lucian 0,896 m / ennemi 0,925 m, fin naturelle puis retour au sol.
Annulation pendant ascension : maxima 1,232 / 1,678 m, retour au sol et reprise
NavMesh/input ; un seul impact dans les deux cas, aucun asset auteur sauve.
Rendu/manette, interruptions repetees, plafonds asymetriques et reseau restent
non qualifies. Aucun processus de test laisse actif.

Relecture du contact Bond : fixture corrigee, pas de changement gameplay.
L'etourdissement remplace hurtRemaining ; le verrou de decision fixe une seule
fois ne maintenait donc pas l'ennemi immobile entre les essais. RunSkillFeelBatch
maintient maintenant ce verrou de fixture et mesure sa derive, refusee au-dela
de 3 cm. CombatSkillFeelStationaryContact.log passe : derive cible 0,000 m,
Eclair +3,943 m derriere l'origine, Bond devant a 1,231 m, un impact de 10 PV
chacun, ressources restituees, aucune exception de fermeture, sortie 0.
Les assertions de contact ne sont pas assouplies. L'ancien echec de Bond n'est
pas une preuve de bug contre une cible immobile ; pas de homing/impact de secours
ajoute. Cibles mobiles volontairement evasives, murs et ressenti Rupture encore
a qualifier. Retour manuel deja positif Eclair/Bond, reglages conserves.

Continuite de sortie : correctif cible de FinishCombatExit pour la reference
d'interface conservee apres destruction d'un composant Unity. Exists distingue
l'objet encore present de la cible vivante : ne pas acceder a Root ni restituer
une suspension d'un composant detruit, mais nettoyer les morts/inactifs encore
presents. Aucun changement des priorites d'action, degats ou reglages de ressenti.
Trois regressions ContactContinuity ajoutees (objet/composant detruit, mort
inactif encore suspendu). Compilation et 64 tests passes :
Library/CombatExitContinuityTests.xml (ContactContinuity, EngagementLedger,
FreeAttack). Empreintes source/copie identiques pour les trois fichiers.
Relecture CombatSkillFeelExitReplayFinal.log : aucune MissingReferenceException
de fermeture, mais recette globale REFUSEE car Bond manque son contact (4,109 m
parcourus, arret a 0,891 m devant la cible, 0 degat). Eclair reste valide a
+3,950 m, 10 PV uniques. Ne pas presenter cette relecture comme un nouveau
succes des deux skills. Un lancement trop tot pendant la fermeture precedente
a ete refuse par Unity (projet occupe), puis relance apres fin du processus.
Retour manuel auteur recu : Eclair et Bond sont tous deux meilleurs ; conserver
leurs reglages. La fiabilite du contact manque dans la recette doit etre
distinguee de la correction de fermeture, sans fabriquer de degats de secours.

Rupture : recette baseline sur vrais acteurs ajoutee (RunRuptureFeelBatch),
sans ecriture auteur. Logs/CombatRuptureFeelBaseline.log : impact unique 15 PV,
recul 7,411 m, montee 1,049 m. Atterrissage vers 1,35 s, puis environ 1,3 m
de glissement au sol pendant la fin du clip protege (2,63 s).
Correction candidate reservee au laboratoire : retreatLandingDeceleration=60
dans le profil de session et l'installateur. Apres decollage observe ET retour
au sol, la presentation freine la composante planaire de l'elan de recul via
UCC. Aucune modification du saut, de la gravite, de la duree protegee, du lock
obligatoire, du timing d'impact ou des degats. Generation invalidee = freinage
inactif ; reseau et production restent a 0.
Compilation et recette corrigee passent : CombatRuptureFeelBrakingFinal.log,
recul total 4,479 m, montee 1,083 m, glissement apres atterrissage 0,133 m,
un impact de 15 PV, fin normale et commandes/temps restitues. Premier essai
de freinage refuse : detection d'un faux retour au sol pendant le decollage,
avant la vraie montee. Le runtime ET la fixture exigent une elevation montante
de 0,1 m avant de reconnaitre l'atterrissage ; freinage au pas physique, pas
a chaque image. Les deux essais ne constituent pas une mesure de ressenti
controlee a FPS identiques. Exceptions de fermeture deja signalees toujours
presentes avant le correctif de sortie ci-dessus ; interruptions, murs, reseau
et ressenti de Rupture a la manette non qualifies.

Priorite auteur precisee : deplacements glissants/artificiels et impacts peu
puissants ou mal synchronises (choix 1 et 2). Premiere passe Eclair/Bond :
profil ContactDashMotion.Burst active seulement dans Iluvilirae_Test et son
installateur. Trajectoire temporelle a acceleration/franchissement/freinage
courts, sans recalcul de convergence vers la cible ; plafonds de vitesse,
sol, obstacles et moteur UCC conserves. Feedback au point horizontal reel
de contact, hauteur joueur + 1,5 m, burst non parente a la victime.
Production et executeur reseau gardent l'approche precedente.
Compilation et 50 tests FreeAttack passes (CombatSkillBurstTests.xml).
Recette courte RunSkillFeelBatch executee dans la copie isolee, vrais acteurs :
Eclair parcourt 8,943 m et finit a +3,943 m de l'origine cible ; Bond parcourt
3,524 m et reste devant elle. Un impact de 10 PV chacun, aucun lock manuel,
collisions/mouvement/verrous liberes et echelle 1 verifies avant fermeture.
Log : Logs/CombatSkillFeelPlay.log. Sortie code 0 MAIS deux MissingReferenceException
a la destruction de JuggernautV2Brain, depuis FinishCombatExit appele par Arena
et CombatWorldRuntime.OnDisable : historique avant le correctif de sortie
ci-dessus. Retour manuel auteur positif sur les deux dashs ; pas de preuve reseau.

Objectif utilisateur actif : ressenti proche de God of War nordique. Priorite
confirmee : reactivite et poids des frappes, sans changer les boutons ni retirer
les combos aeriens. Aucun objectif de performance ou de migration ci-dessous
n'est considere termine par cette passe.

Le runtime d'attaque libre utilise la file unique de la session de presentation,
comme le laboratoire : premiere Basic immediate, une seule pression suivante
en reserve, lancement au timing auteur. Suppression du detour par la coroutine
de combo historique pour ce flux. Revalidation sol/air et notification du vrai
demarrage communes au jeu et au laboratoire. Aucune ouverture d'annulation avancee.
Entree Animator en secondes fixes pour toutes les actions (plus de duree
proportionnelle au clip), redemarrage explicite a zero, validateur adapte.

Poids prototype uniquement Iluvilirae_Test : hit-stop Basic 40/50/70 ms,
profils sources et copies Clash coherents, installateur conserve les valeurs.
Production, camera, degats, fenetres de contre et regles reseau inchanges.
Compilation Unity et quatre tests cibles reussis :
Logs/CombatFeelResponsivenessFinal.log, Library/CombatFeelResponsivenessFinal.xml.
Premier essai 2/4 : assertion erronee sur la derniere requete apres refus d'une
troisieme pression, et fixture historique de pause sans TimeManager. Corriges
sans affaiblir les verifications de transition/nettoyage. Source/copie testee :
aucune difference sur les assets modifies. Aucun processus de test laisse actif.
Ressenti manette dans Iluvilirae_Test, campagne reseau/Domain Reload et
qualification migration encore non valides. Ancien PID de qualification 26692
absent ; son log termine pendant import, aucun nouveau resultat revendique.
Retour utilisateur : pas de changement marquant, mais BasicSkills relativement
satisfaisantes. Ne pas continuer a durcir leur hit-stop. Prochaine priorite
explicitement choisie : deplacements et impacts des skills, plutot que camera
ou rythme ennemi.

Recette ajoutee a FreeAttackPlayValidation.RunBasicFeelBatch : vrais acteurs,
presses X virtuelles et visee au stick, contacts, echelles observees puis coup
dans le vide, sans sauvegarde auteur. Compilation reussie ; recette NON validee.
Logs/CombatFeelLaboratoryAimedCombo.log et
Library/CombatFeel/laboratory-combo-diagnostic.json : 3 departs, premiere action
a +1 frame, 2 contacts de 5 et 10 PV, echelles 0,2 / 0,1 ; troisieme coup manque
et echelle 1. Le diagnostic sans visee montre le decalage lateral du combo et
la victime hors arc, pas un event d'impact absent. Etat final au sol/commandes
et essai a vide non atteints ; ne pas les annoncer passes.
La fixture utilise CharacterInfo.SetHealth, pas l'ancien cache currentHealth,
et place Lucian selon son yaw UCC reel, pas un axe monde suppose. Les premiers
essais invalides sont conserves dans Logs/CombatFeelLaboratory*.log.
Aucun changement supplementaire des degats, portees ou animations gameplay.
Aucun processus de ces recettes laisse actif. Reste a distinguer visee physique,
orientation/trajectoire auteur du troisieme coup avant de qualifier ce scenario.

## Priorite performance editeur et jeu (2026-10-18)

Objectif toujours actif : Bootstrap lent depuis migration combat, Play et
edition a fluidifier. Les travaux combat ci-dessous sont preserves.

Corrections validees : cache Animator UCC/facade, camera HDRP d'orbe arretee
hors chargement, buffers d'influence/materiaux, registre Flame reutilise et
apercu des lumieres explicite en edition. Politique locale reversible : un
worker d'import sur 16 Go, reserve minimale native de un. Quatre tests
performance et 14 tests d'autorite temporelle passent. Aucun clip ou profil
d'acceptation modifie. La fixture de locomotion historique reste incompatible
avec la migration ; ses reparations exploratoires ont ete retirees.

Mesures legeres du 18/10, source seul : deux fois 897 frames / 15 s,
1080p VSync 1, focus 100 %, aucune frame sautee. Au repos : p95 16,867 puis
19,124 ms, p99 19,427 puis 25,070 ms. Pas de preuve avant/apres controlee,
ni qualification exploration/combat/Player. Demande->jeu pret 51,577 puis
40,107 s, dont entree Play 28,17 puis 27,58 s. Retour Edit 1,13 puis 0,65 s.
Les anciennes captures avec historique Profiler sont des diagnostics pollues,
pas des baselines : jusqu'a 2,753 Go retenus par cet outil lui-meme.

Inventaire apres mesure : Unity alloue ~5,58 Gio, reserve ~8,29 Gio,
Profiler ~7 Mo, 298 clips ~115 Mo, meshes estimes ~1,10 Go. Forte pression
RAM/pagination persistante sur la machine 16 Go ; disque libre suffisant.
Suite : memoire native/textures, entree Play, exploration/combat et Player.
Cleanup Editor du registre Netcode apres EnteredEditMode uniquement :
compilation Unity et test cible reussis (1 passe, 0 echec). Aucun gain memoire
chiffre encore revendique ; vrai flux Bootstrap suivant encore a verifier.

Build 16 DX12 reussi (0 erreur) apres correction des quatre shaders Vefects
aux anciens mots-cles d'ombres ; correctif reproductible sous Codex/patches.
Premier essai Player invalide : seul Core etait charge, pas le manifeste de
test reserve a l'editeur. Aucun FPS revendique. Builder adapte uniquement dans
la copie pour enregistrer ce meme manifeste et charger le monde complet.
Builds 17 et 18 termines avec 0 erreur. Player 18 valide, monde complet
(22 scenes), DX12 Balanced 1080p sans VSync, repos 60 s / 8200 frames :
p95 9,083 ms, p99 9,855 ms, maximum 15,864 ms, focus continu, 0 erreur.
Render p95 3,393 ms, GPU p95 8,422 ms ; chargement mesure apres Awake 11,152 s.
Pas de comparaison directe avec Editor Performant/VSync ni qualification combat.
Parcours build 18 valide : 258 m / 44 trajets / 60 s, acteur au sol 100 %,
8731 frames, p95 8,717 / p99 11,279 ms, Render p95 3,050 / GPU 6,234 ms.
Focus continu, aucune erreur. Ce parcours local ne qualifie pas le combat.
Dependance IA inference inutilisee retiree, App UI 2.1.13 conserve ; aucune autre
version de package changee. Compilation Unity apres retrait reussie.
Capture legere source 18:01 : 893 frames / 15 s, focus 100 %, 0 sautee,
profil Performant, p95 16,877 / p99 22,525 ms. Entree Play toujours 27,61 s,
transition 21,153 s, demande->pret 47,769 s. Pas de gain de demarrage attribue
au retrait IA. Allocations apres retour Edit ~5,25 Gio, dont ~157 Mo Profiler.
Pulse est desormais anime uniquement en Play (evite l'echelle changeant en
edition), sans gain CPU mesure. Test Scene View Always Refresh off non concluant,
reglage precedent restaure. Cout au repos Source seul ~1 coeur CPU a attribuer.
Capture d'attribution Edit obtenue en forcant la boucle explicitement :
283 frames / 5 s, attente GPU 2,65 s, UI/affichage dominants. Ce flux force
des updates/rendus : diagnostic uniquement, pas une baseline de repos.
Interaction Mode global de cet utilisateur etait No Throttling / 0 ms ; passe
explicitement a Monitor Refresh Rate / 16 ms, sauvegarde originale en Library
et menu de restauration. CPU source au repos observe 8,328 -> 6,078 s CPU / 10 s
(~27 % de moins, un releve par etat). Player inchange ; Play source revalide :
899 frames / 15 s, 1080p Performant VSync 1, focus 100 %, 0 sautee, p95
17,226 / p99 18,022 ms, aucun message d'erreur. Acteur conduit, au sol, index 0.
Les ~25 s d'attente se reproduisent avant meme la demande de Play : essai
entry-20261018-165302-990.json, pre-roll 25,341 s puis demande->Play 2,710 s.
Ne pas revendiquer un demarrage 10x plus rapide : l'attente est deplacee.
Liberation manuelle des assets inutilises : ~64 Mo seulement, pas de politique
automatique ajoutee. Profiler arrete et historique archive ; ouverture de sa
fenetre pouvait elle-meme activer l'enregistrement, restauration corrigee.
Capture Bootstrap legere suivante attend trois updates idle apres refresh :
light-20261018-171000-426.json, demande->pret 19,409 s, entree Play 2,55 s,
SceneTransition 17,232 s. La baisse vs ~47,8 s comprend le changement de
protocole ; ne pas attribuer tout l'ecart au reglage de rafraichissement.
Utilisateur autorise la poursuite et demande des estimations revisees a chaque
etape ; controle courant termine, retour utilisateur demande (optionnel).
Suite : combat et transitions representatifs ; ne pas ouvrir un Player ni
recompiler pendant son retest si l'utilisateur reprend Unity.
Ne pas refaire de tweaks
CPU aveugles ; prioriser les mesures Player monde complet et l'entree Play.
Copie resynchronisee 46 748 fichiers, exceptions de diagnostic documentees :
ce build ne vaut pas validation release.
Rapports, preuves, limites et fichiers de mesure : `systems/performance.md`.

## Migration combat : integration finale et installation (2026-10-17)

Raccords restants implementes : paliers par participant serveur et transport QTE
proprietaire, Counter/Light/Skill cinematographiques avec pools acteur, reactions
B/Y horodatees, protection d'esquive par action et clash multipaire. Les moteurs
de mouvement existants restent seuls executants. Garde, roulade et flottement
ont une replication cosmetique sans second moteur. Les evenements de contact
et de palier quittent la facade singleton au profit du participant reel.
Les adaptateurs de rencontres conservent leurs mecaniques ; deux profils Giant
distincts sont derives des vrais kits Jump et Assomoir, avec provenance explicite.

Compilation runtime/editeur combinee reussie et catalogue borne sans refus :
CombatMigrationFinalImplementationCompile.log, run 59dfca280fd24d6484451d4d80713c70.
Sources des smoke tests reseau compilees, pas executees. Aucune nouvelle recette
Play Mode/reseau ni campagne de tests. Installation isolee reussie : run
925f647c3b9347369a968b5a1b23dcd8, 90 fichiers surveilles, GUID conserves,
seconde application byte-stable, aucun manque d'implementation dans le manifeste.
Compilation de livraison reussie : CombatMigrationDeliveryCompile.log,
catalogue borne 0 ecriture proposee / 0 blocker / 0 skill refuse.
Les profils incoherents de DeadWeight, FalseChoir et Veilleur sont egalement
remplaces dans le plan par leurs propres profils Assomoir equipe, sans modifier
leurs mecaniques specialisees.

Installation principale confirmee : run d0aff98072784154b05309f35428062c,
Library/ProductionCombatMigration/implementation-install.json : installed,
authoringInstalled=true, idempotent=true, 90 fichiers surveilles, aucun manque
d'implementation dans le manifeste borne. Backups conserves, GUID verifies et
seconde application byte-stable. Compilation principale reussie dans Logs/Editor.log.
Le helper statique CombatClashAuthoring est regroupe dans ProductionCombatMigration.cs :
l'ancien index Unity excluait son nouveau fichier autonome malgre le refresh.
La requete v2 a ete consommee ; aucune installation en attente.

Les controles d'installation sont limites au manifeste approuve ; les audits
exhaustifs et onze gates de qualification ne sont ni executes ni rendus verts.
ReadyForProduction reste faux. Flame d'Etienne : placement reserve a l'auteur.

### Checkpoint Precedent (Remplace Pour Les Raccords Ci-Dessus)

Passe implementation sans campagne Play Mode/reseau, demandee par l'auteur :
catalogue/executant etendus a Fleche, Rupture avec lock confirme, charge serveur,
Ascension et impact au sol. Le maintien X est une intention serveur, pas une
preparation speculative. Les sessions externes exposent leurs fenetres auteur ;
remplacement valide avant annulation, roulade serveur via moteur existant,
garde distante via presentation acteur et nettoyage au changement de monde.
Projection/flottement partages avec EnemyController via ICombatChargedTarget,
sans ajouter de moteur physique. Rupture : clip auteur rendu non bouclant.
Vision des ennemis historiques sur joueurs assignes serveur ; feedbacks d'impacts
ennemis repliques. Isolation des buffers/reconciliations du joueur local renforcee.
Ces chemins ne constituent pas une recette multijoueur validee.

Authoring isole intermediaire : compilation et catalogue sans famille refusee,
Apply repete puis dry-run sans ecriture (CombatImplementationAuthoring.log).
Compilation courante reussie sans lancer de tests : CombatImplementationCurrentCompile.log,
avec controle catalogue 0 blocker / 0 skill refuse.
CombatMigrationImplementationInstall installe explicitement les services communs,
les quatre kits et le vrai Juggernaut, avec backups et manifeste ; ce n'est pas
un installateur declarant toute la migration terminee. Source Unity inaccessible
par le controle de fenetre pendant MainMenu : aucune fermeture forcee. Requete
explicite d'installation sous Library en attente de l'editeur ; installation
principale non confirmee tant que son rapport manque. La requete a depuis ete
consommee (run 8d9ffc8b6db241099e936e46f3677245) : preflight interrompu car
les sources changeaient pendant l'audit ; aucun succes d'installation deduit.
Nouvelle application requise apres stabilisation de tous les raccords.
MainMenuMissingPrefabRepair est desormais manuel, pas lance a chaque compilation.
Reactions/QTE/cinematiques distants, clash multipaire et raccord complet des autres
rencontres restent des travaux d'implementation, pas de simples tests a deleguer.

Continuation en cours : impact Basic aerienne unique a 0,533333 s, texte Nina
repare dans les tests et binding VFX conditionne aux proprietes du graphe.
Audit Etienne : trois Flames auteur conservees dans sa scene de cycle, avec
controle de couverture de la zone additive complete. Le Ghost est effectivement
hors de toute influence. Flame locale approuvee ; placement repris par l'auteur.
Recherche automatique arretee a sa demande, aucune scene Etienne modifiee.
Ardent : rayon canonique d'une seule Flame a 11,25 m pour couvrir le ruban.
La scene Indoor_Lights existe a la racine Assets ; resolution de scenes corrigee.
Cache runtime du manifest realigne sur ses 21 references additives, Core en plus.
Deux entrees Build Settings vers des scenes absentes/non consommees retirees.

Conversion isolee du vrai prefab Juggernaut appliquee et repetee : identites,
quatre attaques et sources conservees. Audit post-conversion corrige pour lire
la provenance originale persistante, sans reutiliser le controller genere comme
source. Nettoyage isole effectue : 70 sous-assets de diagnostic orphelins retires.
Derniere suite terminee : 387 passes, 1 echec auteur Etienne, 1 controle manuel
ignore. Staging actualise et recette Bootstrap avec rendu restent en cours ;
aucune nouvelle preuve reseau ni bascule globale dans le projet principal.

L'audit des ressources a ete corrige : nombreuses absences propres a la copie
isolee, pas au projet source. Originaux recopies pour validation ; chemins Nina
et scene de cycle corriges sans changement de GUID, progression ou deblocage.
Le binder VFX du Veilleur existe dans le package actif. Le diagnostic Animator
Lucian supprime volontairement n'est pas restaure ; outil de nettoyage precis.

Continuation runtime : restitution differee apres derniere menace/cinematique,
nettoyage d'un lock mort, protections cinematiques limitees aux participants,
contacts ennemis auteur multivictimes et degats captures par generation.
Chemins reseau precedents : Basic aerienne, dashs, buffer Basic borne, Sommation,
Juste Retour et Pas de recul. Voir les ajouts de cette passe ci-dessus.
L'impact Basic aerien est auteur ; aucun degat de secours ajoute.
Reactions/QTE/cinematiques distants et clash multiacteur restent des blocages de livraison.

Une passe auteur isolee a valide Apply puis zero ecriture au second Apply/dry-run.
Elle precede les derniers changements : pas une preuve finale de production.
Aucune bascule globale des prefabs de production rapatriee.
Bootstrap sous Null graphics n'a pas termine le chargement (HDRP/frame rendue) :
recette non validee, pas une panne NavMesh demontree. Outil rendu isole disponible.
ReadyForProduction=false ; ne pas annoncer la migration complete.
Details et preuves : combat_migration_report.md.
Contacts multivictimes/generations, engagements et participants passent.
Les anciennes suites et leurs echecs restent conserves dans les rapports ;
seule la suite courante ci-dessus decrit l'etat auteur en cours.

## Point de reference precedent (2026-10-16)

Socle commun renforce : engagements par acteur, source des impacts explicite,
paliers lies a l'auteur du coup, contrats de rencontre preserves et conversion
Brains des patterns auteur sans remplacement par une identite de laboratoire.
Les adaptateurs reseau executent maintenant une tranche concrete : Basics au
sol, soins/passifs, Rempart/Intercession et Revelation/Entrave. Catalogue GUID,
validation serveur, PV/effets/cooldowns et menaces du proprietaire repliques.
Admission de monde explicite apres placement ; aucun rebind du manager local
vers un joueur distant. Les anciens RPC de support ne contournent pas un nouvel
executant installe. Charge reseau non migree : pas de preparation speculative.

Outils auteur explicites : ProductionCombatMigration, CombatNetworkSetup et
ProductionBrainsEnemyConversion. ReadyForProduction depend maintenant de preuves
liees aux sources/assets, de l'inventaire et de l'idempotence, pas du succes Apply.
La bascule globale des prefabs de production reste NON rapatriee. Les familles
reseau non migrees, rencontres specifiques et references narratives bloquees
restent enumerees dans combat_migration_report.md / Migration/Network/INTEGRATION.md.

Compilation Unity reussie dans Library/ActorAnimationSandbox. Selection ciblee :
248 tests passes, 0 echec, 1 verification auteur manuelle ignoree explicitement.
Le test de croix directionnelle auparavant exclu est corrige et reintegre.
La suite elargie initiale a aussi revele des references narratives absentes a leurs
anciens chemins et des attentes texte/rendu hors recette ciblee : non declares fixes.
Preuve courante : Logs/CombatMigrationContinuationFinal.log et
Library/ProductionCombatMigration/continuation-edit-results.xml dans la copie isolee.
Play de regression de charge reussi (maintien, degats, relachement/priorite/mort).
Audit auteur read-only reussi : 3 ecritures de socle proposees, 11 reseau, aucun
blocage de preflight reseau mais 10 skills reseau encore non pris en charge.
L'inventaire et les preuves manquantes maintiennent ReadyForProduction=false.
Bootstrap/District 1, toutes les zones, sauvegardes reelles, ressenti manuel et
host/client restent non valides.
Ne pas annoncer cette continuation comme une migration complete du jeu.

## Charge : pose finale de BasicSkill_1 (2026-10-16)

Dans Iluvilirae_Test, ChargedWindup conserve maintenant la pose finale exacte
de ClashBasic_0 (BasicSkill_1), via un clip statique InPlace sans Animation Events.
L'installateur regenere cette copie dediee sans modifier la Basic partagee.
Fin de Basic sous maintien X : transfert direct vers la session de preparation,
sans fondu intermediaire de locomotion. Animator.speed reste intact ; cadrage
camera, protection, seuil de charge et nettoyages existants sont conserves.
Compilation et recette Play ciblee reussies dans la copie isolee :
Library/ActorAnimationSandbox/Logs/ChargedFinalPose.log. Toutes les courbes
de preparation correspondent a la fin de BasicSkill_1, sans event ; maintien
X reel, degats pendant Basic et preparation, pose maintenue, relachement,
remplacement prioritaire et mort valides. Clip genere rapatrie au laboratoire.
Ressenti manuel et reseau non testes.

## Preparation du ChargedSkill protegee (2026-10-16)

Correction apres retour en jeu : la protection couvre aussi la Basic initiale
pendant le maintien X valide, et l'intervalle avant la preparation. Le premier
correctif et son test ne couvraient que la preparation, laissant la charge
annulable avant celle-ci. IsChargingSkill unifie cette autorisation par acteur,
input physique et generation ; sans maintien valide, une Basic reste normale.
La session de preparation possede un token explicite nettoye a chaque sortie.
Degats non mortels : PV retires normalement, sans Hurt ni annulation du
maintien X. Saut, roulade, garde, attaques ordinaires et ouverture de roue ne
peuvent pas remplacer le chargement. Les BasicSkills sans charge restent inchangees.
Relachement volontaire de X, mort, contexte prioritaire, fin de combat et
desactivation conservent leurs sorties et leurs nettoyages UCC/camera.
Recette ciblee : ChargedSkillPlayValidation.RunPreparationArmorBatch dans une
copie isolee d'Iluvilirae_Test. Compilation et Play reussis :
Library/ActorAnimationSandbox/Logs/ChargedPreparationArmor.log. PV reels retires,
session et maintien conserves, Hurt/mobilite/garde/roue refuses ; relachement X,
remplacement externe prioritaire et mort sans handle UCC/camera residuel.
La recette allonge seulement la fenetre de charge en memoire dans la copie de
test ; aucun asset auteur modifie. Reseau et ressenti manuel non executes.
Recette renforcee : degats reels pendant la Basic initiale puis preparation,
verification differee de la pose Animator, refus des commandes et nettoyages.
Les premiers essais ChargedHoldArmor.log / ChargedHoldArmorFinal.log echouent
sur la Basic initiale : maintien enregistre actif mais lecture brute du bouton
fausse hors phase d'input. La protection consomme maintenant l'etat valide
de l'input directement lie a l'acteur ; lecture physique et relachement restent
dans la boucle/callbacks d'input. ChargedHoldArmorVerified.log confirme compilation
et Play reussis : degats pendant Basic puis preparation, pose Animator conservee
apres un delai, annulations ordinaires refusees, relachement/priorite/mort propres.
Les annulations anormales loggent leur motif via les diagnostics existants,
sans journaliser chaque relachement normal de X. Pas de test reseau dans cette passe.

## Saut de rupture : evasion de melee (2026-10-16)

SkillSO configure en LockedTarget : lock manuel obligatoire et lancement
horizontal 0-3 m ; impact, 15 degats, stun et intensite de l'impulsion preserves.
Sans lock, refus explicite avant animation/cout, sans acquisition temporaire.
Les autres skills gardent leurs contrats. L'event applique une seule impulsion par generation vers
l'arriere de la direction capturee, meme si la cible evite ensuite le contact.
Les degats restent spatiaux et les nettoyages d'action/UCC restent inchanges.
Recette ciblee RuptureArmorPlayValidation dans Iluvilirae_Test isole : refus
hors melee/sans cible, impact/stun, recul sur contact et coup manque, restitution
et mort prioritaire. Compilation et Play reussis :
Library/ActorAnimationSandbox/Logs/RuptureEscapeValidationFinal.log (avant
l'exigence de lock). La recette actualisee exige verrouillage, refuse sans
lock/hors melee, et conserve les tests de recul et nettoyage.
Compilation et Play actualises reussis : RuptureLockedValidation.log dans le
meme dossier. Un ennemi a 2 m sans lock est refuse sans cout ; le meme ennemi
verrouille autorise le skill, et la portee de 3 m reste obligatoire.
Le premier parcours s'est arrete au rejeu (orientation de fixture apres reprise
de mouvement) ; la reprise alignee face a la cible valide aussi le coup manque.
Aucun reglage gameplay n'a ete assoupli pour le test. Reseau et campagne de
collisions multi-geometries non executes dans cette passe.

## Roue directionnelle et camera (2026-10-15)

LT ne declenche plus OpenPalette dans PlayerInputs.inputactions et son wrapper.
CombatWheel conserve Camera et l'autorisation de camera, en plus de ses entrees
directionnelles, mouvement/sprint et retour B. Aucun asset de scene modifie.
Validation ciblee via DirectionalSkillKitValidation.RunWheelCameraBatch dans
Iluvilirae_Test isole : bindings/modalite puis LT, croix, orbite et retour B.
Compilation et assertions reussies. Play automatise reussi : LT ignore, croix
ouvre, stick droit route vers Camera et calcul UCC d'orbite valide, B ferme.
L'editeur cache n'a pas le focus : le test exerce explicitement le calcul
d'orbite existant sans retirer la protection focus du runtime ; le mouvement
affiche dans une fenetre au premier plan reste a confirmer manuellement.
Preuve : Library/ActorAnimationSandbox/Logs/WheelCameraValidationFinal.log.

## Recette des attaques sans lock (2026-10-15)

78 tests Edit Mode cibles reussis : selection spatiale, cone/angle maximum,
portees inclusives, hauteur, morts, departage stable, contact/generation,
continuite, contres, clash et donnees de projection. XML :
Library/ActorAnimationSandbox/Logs/FreeAttackAcceptanceTestsFinal.xml.
Un ancien test ChargedSkillTests.DeviceRemovalDoesNotReadDisconnectedChargeControls
echoue sur le callback canceled en Edit Mode ; il est signale a part, non corrige
et exclu du filtre final. Ce resultat ne valide pas la deconnexion physique.

Play automatise dans Iluvilirae_Test isole : 24 frappes completes sans lock
(8 orientations a l'arret, 8 intentions camera-relatives en course, 8 en vol),
deux ennemis sans changement de cible capturee, contact unique, cible mobile
manquee, garde Legacy absorbant 100% avec engagement et esquive sans engagement.
Captures FreeAttack_Mode0/1/2.png inspectees ; elles ne valident pas le ressenti.
Le parcours complet a atteint sa limite au clash. Une reprise avec diagnostics
montre un impact ennemi avant l'ouverture de Lucian : pas de clash retroactif.
La recette clash a pas fixe 1/60, lancement a .0333 s de preparation, reussit :
deux fenetres acceptees, volumes a .623 m (rayons sommes 1.10 m), aucun PV retire,
recul et restitution des ressources. Aucun timing ni volume gameplay modifie.
La reprise physique valide aussi Ascension sans lock (un impact, deux acteurs
en vol), Basic aerienne manuelle puis atterrissage et rattachement NavMesh.
Les deux dashs libres atteignent environ 9.96 m sur sol degage dans la recette
de base. Sur la plateforme isolee, ils restent au sol avant le vide (.26/.47 m)
et le mur (.73/.89 m). Ce test confirme l'arret sur cette fixture, pas une
calibration de la marge au bord pour toutes les geometries ou BattleWall.
Preuves dans Library/ActorAnimationSandbox/Logs/ :
- FreeAttackAcceptancePlayFinal.log : base, 24 directions, cibles, garde/esquive ;
- FreeAttackAcceptancePhysical.log : projection, reprise aerienne, mur/vide ;
- FreeAttackAcceptanceClash.log et FreeAttackAcceptanceClashReport.txt : clash.
Les rapports des parcours precedents restent en echec, sans etre reecrits pour
masquer leurs interruptions. Bilan agrege : scenarios ci-dessus reussis ; pas
de declaration de reussite d'un parcours complet unique. La compilation des
derniers validateurs est confirmee par la derniere execution Unity.
Restent manuels/non executes : ressenti gamepad, campagne 30/120 FPS, tous les
cas BattleWall/pente, reseau et regression complete des QTE visibles. Les tests
cibles de contre passent sans changement de leurs regles.
Aucun asset de production migre, aucun reglage gameplay assoupli pour la recette.

## Continuite hors lock (2026-10-15)

Iluvilirae_Test prepare ses acteurs sans demarrer de combat ni verrouiller.
TryEngageFromPlayerContact valide l'identite de session consommee avant une
entree commune avec la perception ennemie. Contact garde/absorbe accepte meme
a 0 PV retires ; esquive, clash et protection cinematographique refusent.
Le chemin Legacy fournit explicitement l'acceptation du contact, distincte des
degats. Les impacts de zone et Juste Retour rejoignent aussi cette resolution.
EncounterAggroStarted ouvre EN GARDE une seule fois par engagement ; musique,
interactions et BattleWall suivent l'entree sans changer le lock, les cooldowns
ou la session courante. Les comportements de production restent hors opt-in.

Le passage Exploration -> Combat preserve les actions deja actives et les
valeurs de mouvement/camera. Les autres transitions gardent leur nettoyage.
Projectiles visuels et impacts differes verifient leur generation ; les effets
de projectile sont nettoyes avec leur session. Aucun nouveau moteur joueur.
Compilation Unity et 56 tests Edit Mode reussis (continuite des actions activees,
priorites UI/dialogue/cinematique, sessions, ciblage et contres). Courte validation
Play Mode automatisee reussie : premier contact sans lock, session conservee,
introduction unique, lock/unlock sans redirection, coup dans le vide, dashs,
obstacle et evenement perime refuse. Resultats dans
Library/ActorAnimationSandbox/Logs/ContactContinuityTests.xml et
ContactContinuityPlay.log. Aucun changement des fenetres de contre ni des QTE.
Restent non executes : garde/absorption Legacy en situation complete, maintien
physique de X pendant le premier contact, ressenti course/roulade, paliers et
host/client. Pas de migration de production.

## Attaquer sans lock (2026-10-15)

Opt-in explicite sur LucianBrainsCombatArena.freeAttacks dans Iluvilirae_Test,
inactif dans les autres scenes. Profil de scene : cone 60 degres,
correction maximum 25, rotation UCC bornee, volume de contact sphere dedie,
dash libre 10 m. SkillSO.targetingMode est la source unique du contrat :
directionnel, zone lanceur, ennemi facultatif/obligatoire, allie selectionne,
soi ou zone alliee. Non configure = refus explicite, jamais de deduction runtime.
Le profil ne conserve aucune association par skill. Les metadonnees partagees
ne changent pas le comportement de production, qui garde son ancien chemin.

TryResolveSkillTargeting valide les branches attaque, soin, protection, controle
et cinematique avant effets/couts. Le contexte capture aussi l'allie et l'origine
de zone. Lock invalide = refus cible, sans substitution ; sans lock, acquisition
temporaire devant le lanceur. Une cinematique ordinaire recoit explicitement sa
victime, valide son rig avant lancement et ne fabrique jamais de lock camera.
CounterSkills, LightSkills et paliers conservent leurs entrees specialisees.
Migration/validation : Lit > Combat Lab > Migrate/Validate Skill Targeting.
27 assets accessibles migres et valides dans Unity ; portees et effets conserves.
Compilation et 50 tests Edit Mode reussis (contrats, acquisition, absence de cible,
lock invalide sans substitution, generations, plages et regression des contres).
Resultats : Library/ActorAnimationSandbox/Logs/SkillTargetingTests.xml.
Courte validation Play Mode automatisee reussie dans la copie isolee : acquisition
obligatoire sans lock, contrat absent refuse, Basic au contact unique, Eclair
cible traversant a +4 m, X en Exploration sans engagement, Eclair/Bond libres
a 9.96 m, mur a 1.35 m et interruption sans bypass residuel. Log :
Library/ActorAnimationSandbox/Logs/SkillTargetingPlay.log.
Validation artistique/manuelle, cinematics ciblees completes et host/client
non executes ; aucune migration de scene de production.

PlayerAttackContext capture direction camera-relative, cible facultative,
position de depart et generation au demarrage reel, y compris apres buffer.
Impacts sans dependance au lock, contact unique, arbitrage clash preserve,
aucun degat de secours. Eclair/Bond gardent leur route ciblee ou parcourent
10 m sans cible, avec sol/obstacles/BattleWall et nettoyage UCC existants.
Les collisions ignorees restent limitees a la cible capturee d'Eclair.
Hors engagement, seul X et le selecteur de kit sont accessibles en Exploration ;
dialogues, menus, traversees et cinematics conservent leurs verrous.
Premiere suite : 12 tests Edit Mode reussis. Courte execution Play Mode
automatisee reussie : BasicSkill sans lock (5 PV uniques), Eclair cible sans
lock (10 PV uniques, sortie a +4 m), X en Exploration sans engagement,
Eclair/Bond libres a 9.96 m, mur limitant le trajet a 1.35 m et interruption
sans ressource residuelle. Les premiers essais fournissaient un facing oppose
a l'ennemi : protocole corrige, aucune extension du cone pour les faire toucher.
Logs : Library/ActorAnimationSandbox/Logs/FreeAttackPlayFinal.log.
Regression finale : compilation et 38 tests Edit Mode reussis (21 attaques
libres/directions, 17 contres), resultat FreeAttackRegression.xml dans le meme
dossier de logs. Ressenti manuel, tests
reseau, vide/BattleWall et scenarios aeriens complets non executes dans cette
passe. Les helpers de collision/sol et les autorites existantes sont reutilises.

## Contres alignes sur l'impact (2026-10-15)

Iluvilirae_Test fournit un EnemyReactionTiming (Animator, clip, etat, impact,
generation) au controleur de reactions existant. Y est valide dans les .2 s
de clip precedant l'impact, borne finale exclue. B garde .5 s reelle et expire
independamment. Aucun clip ni instant d'impact n'est modifie. Production et
QTE de paliers conservent leurs contrats.

La pression Y est horodatee ; une projection bornee a la frame depuis la
derniere phase Animator reelle couvre l'input precedant son evaluation. La
demande eligible est resolue avant contact/clash, sans annulation de degats
deja appliques. Un echec de lancement conserve le fallback garde si Y est tenu.
L'antispam .4 s reelle est conserve. Validation auteur du profil/Animation
Event et adaptation des installateurs et du smoke test existant.
Validation : compilation et 17 tests Edit Mode reussis (limites, 30/60/120 FPS,
ralenti, transitions, generations, expiration B et antispam). Courte execution
Play Mode automatisee dans une copie isolee d'Iluvilirae_Test : Frappe,
Balayage a .4 et seconde frappe contres avec succes ; B expire sans fermer Y,
pause sans progression/input, aucun degat tardif ni verrou cinematographique.
Resultats : Library/ActorAnimationSandbox/Logs/FairCounterTestsFinal.xml et
FairCounterPlayFinal.log. Une exception d'indexation UnityEditor.Search au
demarrage du sandbox n'a pas empeche les trois essais. Ressenti manuel non
evalue ; aucune migration de production, aucun asset auteur reecrit.

## Diagnostic CharacterEffect (2026-10-15)

Le rechargement de l'instance et les essais de skinning/offscreen n'ont pas
resolu l'emission concentree au-dessus de Lucian. Aucune cause de migration
Unity n'est confirmee. Lit/VFX/Audit Character Effects (Read Only) inspecte
les effets charges, references, formats de sommets et donnees reelles du
GraphicsBuffer (triangles distincts, indices invalides, bounds attendus).
Rapport : Library/CharacterEffectDiagnostics/report.txt. Le diagnostic ne
recree aucun effet et ne modifie aucun prefab/Graph ; compilation Editor
reussie. Premiere execution en mode edition reussie, mais aucun CharacterEffect
non persistant n'est charge dans la scene actuellement ouverte (AnimationLab).
Relancer le menu dans la scene ou l'effet est visible pour mesurer les
instances concernees. Aucun correctif VFX fonctionnel applique.

## Ascension : charge visible et descente securisee (2026-10-14)

La preparation Idle est remplacee par ChargedWindup : pose d'anticipation
InPlace issue du Basic 3, boucle douce sans impact ni evenement. Reglages camera
sur ChargedSkillSO : FOV -10 degres, decalage arriere .6 m, fondu .25 s reel.
LitCameraDirector compose cet effet au rendu puis restitue la pose source :
aucun second pilote, aucune modification persistante du cadrage UCC. Session
de preparation proprietaire, restitution au relachement/lancement/interruption.

Les suspensions aeriennes utilisent des handles independants avec echeance
locale. Un oubli de nettoyage expire, une double liberation est sans effet.
Ascension limite son hold a la duree configuree ; les Basics ont leur propre
borne clip +1 s, reduite au temps restant du flottement apres Ascension.
Une Basic aerienne tardive ne prolonge donc jamais cette suspension ; son
animation peut continuer pendant la chute. Rejouer Ascension remet le clip
a zero, meme lors d'une restitution apres interruption du meme etat.
Traverser une cinematique pendant le flottement ne doit plus
memoriser la gravite temporairement coupee comme valeur a restaurer.
Validation laboratoire : ChargedChargeSafetyVerified.log, dix cycles,
interruption, Basic aerien tardif et descente, expiration/double liberation des
holds. Trois tests Edit Mode passent (ChargedChargeSafetyTests.xml), dont le
cadrage rendu/restauration. La deconnexion de la manette de test a revele une
lecture de control retire ; garde device.added ajoutee. Son nouveau test et
la recompilation finale sont interrompus a la demande utilisateur, non valides.
Les processus de test isoles sont arretes ; session Unity utilisateur intacte.
Pas de migration production.

## Ascension : projection seule et poses de flottement (2026-10-14)

Le coup aerien automatique est retire : un seul ChargedSkillContact(0), 10 PV.
ChargedLaunch remplace le montage a deux frappes. La fin physique de montee
rend les commandes, puis maintient les acteurs 1.2 s pour les Basics manuels.
AerialDamage et le chemin de second impact ont ete retires du code et de l'asset.

ChargedFloat de Lucian utilise une boucle aerienne verticale InPlace ;
ChargedFloatHit de la victime utilise une boucle douce autour de sa pose Hit.
Les deux clips sont sans events et configurables sur ChargedSkillSO. Lucian
n'a aucune session/verrou supplementaire pendant sa pose passive : Basic aerien
prioritaire, puis reprise du float s'il reste du temps. Fondu vers Falling a
expiration, sans interrompre une action plus prioritaire. Enemy controller
dedie ChargedEnemy_Lab, production intacte.

Play PASS : ChargedFloatingFinal.log (Library/ActorAnimationSandbox/Logs) :
un contact, absence de frappe automatique, poses Player/Enemy, Basic prioritaire
et reprise apres Basic termine, dix cycles, atterrissage et interruption.
Le test de reprise utilise un hold prolonge en memoire uniquement ; l'asset
livre conserve 1.2 s. Capture ChargedFloating.png : pose verticale verifiee.
Deux tests Edit Mode passent : ChargedFloatingTests.xml (memes logs), avec
controle du contact unique et des deux boucles sans Animation Events.

## Ascension lumineuse chargee (2026-10-14)

CharacterData.chargedSkill optionnel et ChargedSkillSO ajoutes. Lucian_ImpactLab
possede Ascension lumineuse : X lance un Basic immediat, maintien >2 s reel,
fin complete du Basic puis preparation InPlace, attaque 10+15 PV, montee 3 m,
fenetre aerienne 1.2 s et cooldown 8 s. Aucune repetition automatique des Basics.
Autres personnages sans chargedSkill ; aucun skill de roue remplace.

Scene Iluvilirae_Test conserve Lucian et un seul ennemi : ChargedEnemy_Lab est
une copie du Juggernaut_v2 reel de la scene, avec Rigidbody cinematique pour la
reaction. Les prefabs de production ne sont pas modifies. Montage temporaire
des clips humanoides dedies ; deux contacts auteur et aucun impact de secours.

Le maintien aerien UCC utilise une consigne verticale dans UpdateDesiredMovement,
pas une compensation de vitesse dans Update/LateUpdate (qui amplifiait les forces).
NavMesh reste suspendu en vol ; retour local avec baseOffset et sol physique,
sans warp. Nettoyages idempotents et generation des sessions conserves.

Compilation et Play : ChargedPlayFinal2.log dans Library/ActorAnimationSandbox/Logs.
Scenario : pression courte, Basic immediat, charge2s, contacts10+15, hauteur bornee,
Basic aerien manuel, atterrissage, cooldown, dix cycles, contacts perimes et
interruption en montee. Deux tests Edit Mode passent : ChargedSkillTests.xml.
Reseau volontairement refuse pour cette nouvelle projection ; host/client,
revue visuelle artistique, obstacles/plafonds, LightSkill et QTE concurrents
restent a valider separement. Aucun resultat non execute n'est revendique.

## Laboratoire Lucian seul (2026-10-14)

Iluvilirae_Test demarre avec Lucian et un seul ennemi. Le spawn de l'allie
de test est desactive. Les assets et modes de test Aurell, Soren et Vesta
restent disponibles, sans modifier les references de production.

## Vesta jouable (2026-10-13)

Vesta.asset canonical conserve GUID, characterId et portrait ; prefab et
controller propres crees sur la base de Lucian. BasicSkills/locomotion communes,
quatre soins canoniques equipes, pas de Devastation copiee implicitement.
Iluvilirae_Test selectionne Vesta et fournit un allie de soins ; enum des
personnages conserve ses anciens indices. Aucun changement de roster campagne.
Installation idempotente passe (deux configurations). Compilation et Play
passent : VestaPlayFinal.log dans Library/ActorAnimationSandbox/Logs.
Checks : pile UCC jouable, kit4, soins 20% groupe/50% soi, cooldowns,
regeneration sur allie, remplacement par siphon 1 PV/s et nettoyage.
29 tests Edit Mode catalogue/progression passent : VestaKitTests.xml (meme dossier).
Validation host/client et rendu du modele definitif restent hors de ce lot.

## Jambes independantes du torse (2026-10-13)

PlayerMovingLegsOverlay ajoute une couche Moving Legs masquee aux jambes :
marche/course selon la vitesse planaire UCC et locomotion directionnelle en
combat, meme pendant une garde ou une action autorisant le deplacement.
Le torse, les events de combat et le moteur physique restent inchanges.
La couche s'efface a l'arret et reste inactive pendant les verrous de mouvement,
le vol, les cinematiques et la mort. Controllers Player_Model, Soren_Model et
copie Lucian_ImpactLab raccordes ; aucun nouvel etat global de session.
Validation isolee : compilation Unity et controle de poses passes (jambes
animees, torse en garde et racine inchanges) via MovingLegsPoseValidation.
Rapport : Library/MovingLegsValidationProject/Logs/MovingLegsPose3.log.
Le parcours avec stick simule n'a pas pu etre valide
(absence de deplacement des la precondition, avant l'action testee).
Controle visuel/manette requis dans Iluvilirae_Test : garde en mouvement,
fleche, marche/course, arret et preservation des verrous Eclair/Rupture.

## Impact de lumiere : prototype Lucian (2026-10-13)

Quatrieme slot de kit : onde rayon 5 m, 20/40 degats sol/air, recul 1,5/3 m,
cooldown 12 s. Descente physique UCC, impact unique au contact sol ; remplacement
explicitement limite a Rupture. Copies de prefab/controller/CharacterData dans
Iluvilirae_Test, sans remplacement des assets joueur de production.
Clip provisoire dedie derive de Rupture, sans anciens events d'impact.
Compilation et Play Mode passent : GroundImpactPlayUcc2.log dans
Library/ActorAnimationSandbox/Logs. Degats 20/40 uniques, deux ennemis touches,
cible hors rayon epargnee, recul physique, atterrissage, remplacement depuis
un vrai rebond de Rupture et nettoyage des verrous apres interruption verifies.
Le decollage de Rupture utilise une courte ability UCC concurrente (0,08 s),
dans UpdatePosition avant collisions : aucune pose/teleportation manuelle.
Les impulsions sont rattachees a la session de presentation ; le handler UCC
reste actif pendant leur verrou d'input. Recul differe par arbitrage raccorde
au callback d'impact confirme. Reseau non disponible pour ce prototype.
29 tests Edit Mode de catalogue/progression passent : GroundImpactKitTests.xml.

## Recrutement solo et supports (2026-10-13)

Flèche de lumière : vol visuel parabolique, hauteur proportionnelle à la
distance (0,2 m/m, plafond 4 m), orientation suivant la tangente vers le haut
puis vers le bas. Les autres projectiles gardent leur trajet droit par défaut.
Durée du vol, timing d'impact, dégâts, portée et stun inchangés.
Compilation, 29 tests Edit Mode et Play passent : paraboles à 3/15/30 m,
extrémités et tangentes, trajet droit par défaut, montée/descente du projectile
réel, dégâts uniques et stun 2 s. Logs ArrowParabolaPlay.log et
ArrowParabolaTests.xml dans Library/ActorAnimationSandbox/Logs.
Rendu et ressenti manette restent à confirmer dans l'Editor interactif.

Flèche de lumière : distance minimale supprimée (0 m au lieu de 10 m).
Portée maximale 30 m, dégâts, cooldown et stun 2 s inchangés.
Compilation/Play validés : tir réel à 3 m, dégâts uniques et stun 2 s ; bornes
0/30 m acceptées et 30,01 m refusée par le contrôle de portée.
Log : Library/ActorAnimationSandbox/Logs/ArrowNoMinimumRange.log.

Entrave renforce son ralentissement de navigation de 40% à 60% (vitesse x0,4).
Durée 5 s, rayon 4 m, portée 20 m et cooldown 25 s inchangés. Asset, description
et générateur Soren cohérents ; ralentissement non cumulable et animations
d'attaque inchangés.
Compilation et Play Soren passent : multiplicateur x0,4, vitesse NavMesh
3,6 → 1,44, frontière de zone, non-cumul, expiration et régressions du kit.
Log : Library/ActorAnimationSandbox/Logs/Entrave60Percent.log.

Flèche de lumière conserve son stun existant de 2 secondes à l'impact.
Compilation/Play revalidés depuis le kit Soren emprunté par Lucian : aucun
stun avant impact, dégâts uniques, immobilisation 2 s et récupération.
LucianBorrowedArrowStunValidation ; log ArrowStunLucian2.log dans
Library/ActorAnimationSandbox/Logs. Aucun réglage runtime changé inutilement.

Saut de rupture assomme désormais l'ennemi 1 seconde à son impact confirmé,
via le stun existant de SkillSO, sans nouvelle logique ni effet au lancement.
Compilation et Play validés avec IA active : impact unique, immobilisation
pendant 1 seconde puis récupération, verrous de Lucian et recul préservés.
Log : Library/ActorAnimationSandbox/Logs/RuptureStun2.log.

Saut de rupture conserve son animation malgré les dégâts : réaction Hurt refusée
uniquement pendant ce skill, sans réduction des dégâts ni invulnérabilité.
Son verrou de déplacement UCC couvre maintenant toute l'animation, dès le
lancement, sans supprimer le saut/recul physique. Nettoyage lié à la génération
d'action, sans libérer les verrous des autres systèmes. Compilation et Play
passent avec stick maintenu : déplacement latéral bloqué pendant le clip,
recul physique conservé, déplacement repris ensuite et verrou libéré à la mort.
Log : Library/ActorAnimationSandbox/Logs/RuptureMovementFinal.log.
Mobilité non annulable, récupération à normalizedTime=1 ; mort et nettoyages
techniques restent prioritaires. Compilation et Play ciblé réussis : trois
impacts réels (PV retirés), clip complet, mobilité refusée, Hurt à nouveau
disponible après le clip et mort prioritaire. Log :
Library/ActorAnimationSandbox/Logs/RuptureArmor2.log.

Éclair applique un stun de 1 seconde à son impact confirmé. Sa traversée
conserve le verrou planaire et son orientation jusqu’à la fin du clip ; les
collisions cible sont rétablies dès la fin du trajet. Mobilité non annulable,
récupération à normalizedTime=1, nettoyage par la session d’action existante.
Compilation et Play ciblé réussis : impact réel, stun 1 s, orientation conservée,
stick bloqué jusqu’à la fin du clip puis repris, interruption sans verrou résiduel.
Régression de mobilité réussie (14 cas) : portées, Bond offensif, murs, BattleWall,
vide, interruption, cible volumineuse et restauration des collisions.
Logs EclairInertia.log / EclairMobilityRegression.log dans
Library/ActorAnimationSandbox/Logs. Une MissingReferenceException distincte
dans EndCombat à la fermeture du laboratoire est consignée, sans correction ici.

La roue directionnelle utilise désormais deux étapes : une direction choisit
le personnage, puis gauche/haut/bas/droite lancent ses slots stables 0/1/2/3.
Haut puis haut lance Éclair. B ferme toutes les roues sans skill ni esquive ;
changer de kit nécessite B puis une direction. Sauvegardes inchangées. Le maintien
ne rebascule pas lors du changement d’ActionMap. CombatWheel laisse Move et
sprint disponibles, y compris un stick déjà maintenu ; les boutons de face
ne lancent plus de skills ; les modals/cinématiques bloquent la locomotion.
Compilation et Play Iluvilirae_Test passent : déplacement réel roue ouverte,
stick maintenu à l’ouverture, haut puis haut = Éclair, B annule les quatre
kits, X/Y/A inactifs, skills droite de Soren/Aurell et restitution après modal/combat.
Avec et sans Domain Reload. Logs : Library/ActorAnimationSandbox/Logs/
TwoStepSkillWheel.log, TwoStepSkillWheelAllKits.log et TwoStepSkillWheelDomainReload.log.
25 tests Edit Mode passent (TwoStepSkillWheelTests.xml) : directions/slots,
binding B exclusif, recrutement, verrouillages, migration et persistance.
Ressenti manette physique à confirmer.

Iluvilirae_Test démarre désormais avec Lucian et SoloTeamTestProfile activé :
les quatre kits et leurs quinze skills sont disponibles dès le lancement,
sans allié physique de protection. Le slot droite de Lucian reste vide. Ce profil
de laboratoire ne s’applique pas lorsqu’une sauvegarde de campagne est active.
Compilation et Play du profil réellement chargé passent : quatre directions,
quinze skills, effets empruntés et annulation/restitution d’input.
Log : Library/ActorAnimationSandbox/Logs/IluviliraeFullKits.log.

La session solo commence avec Lucian uniquement. SoloTeamProgression conserve
les recrues et déblocages par kit ; RecruitMember donne le slot X et les
autres slots nécessitent UnlockSkill, raccordables via SoloTeamStoryEvent.
La croix directionnelle intégrée choisit le personnage puis le skill ; B annule.
L’UI affiche les supports sans PV, annonce leur arrivée et ne permet aucun swap.
Le spawn, la restauration et les followers solo gardent uniquement Lucian.
Sauvegarde CharacterSaveData v8 avec migration conservatrice des anciens IDs.
Profil de test explicite séparé des sauvegardes actives ; Vesta sans prefab,
portrait provisoire. Compilation Unity et 21 tests Edit Mode passent, dont
migration par GUID, sauvegarde, recrutement unique et refus de spawn/swap.
Play Iluvilirae_Test passe avec et sans Domain Reload : recrutement, refus
des skills verrouillés, quatre kits, effets empruntés et restitution d’input.
Logs : Library/ActorAnimationSandbox/Logs/SoloTeamFinalTests.xml,
SoloTeamPlay.log et SoloTeamPlayNoReload.log. Les séquences de recrutement,
la présentation HUD sur manette physique, le parcours campagne et host/client
restent à vérifier/configurer ; aucun placement narratif n’a été inventé.

## Kits directionnels solo (2026-10-13)

Haut Lucian, droite Vesta, bas Soren, gauche Aurell ; dans la roue les quatre
directions lancent les skills, B ferme, R3 tap/hold verrouille/deverrouille en combat
libre. Catalogue de quinze skills : Lucian Bond offensif/Eclair/Rupture/vide,
autres kits a quatre. Regeneration sur Lucian lui-meme autorisee en solo,
reserve d'Aurell empruntee et Pas de recul sur le vrai Animator Lucian.
Roue exclusive CombatWheel + System, sans fallback des boutons de face et
avec restitution apres modal, fin de combat, mort, disable ou reset d'asset.
Play Iluvilirae_Test passe avec et sans Domain Reload et 11 tests Edit Mode
passent. Sources validees integrees au projet ; ressenti manette/scenes de jeu
a confirmer. Logs : Library/LucianCombatValidation/Logs/DirectionalSkillKit*.
Aucun equipier cree, aucun droit reseau ajoute : repartition multi a
traiter separement. Sources SkillKits/, Input, SkillWheel et profil coordinateur.

## Fusion automatique de combat (2026-10-13)

Fusion avec l'ange gardien du seul personnage incarne a l'entree du combat,
defusion a sa sortie et sur desactivation/destruction du manager. Presentation
seule restauree si SpiritBond absent, sans Munin jouable/equipier invisible.
LightSkill restaure la fusion persistante ; pas de verrou moteur/camera et
evenements tardifs ignores. Tests Play Iluvilirae_Test passes avec et sans
Domain Reload : entree/sortie, LightSkill interrompu, evenements tardifs,
idempotence, acteur isole, desactivation/reactivation et defaite. Les scripts
d'auteur Soren/GLTF sont exclus uniquement du sandbox pendant ces tests, puis
restaures. L'assise automatique est neutralisee dans le test seulement : son
verrou independant ne doit pas etre libere par la fusion. Logs :
Library/LucianCombatValidation/Logs/CombatSpiritFusionPlay*.log.
Regression visuelle en scene de jeu et replication host/client non validees.
La repartition future des kits est quatre en solo, deux/deux a deux joueurs,
deux/un/un a trois. La selection solo/R3 est branchee par le raccord Kits
directionnels ci-dessus ; la repartition reseau reste separee.

## Soren : quatre skills de Lucidite (2026-10-13)

Livraison limitee a Iluvilirae_Test : Soren.asset/prefab/controller distincts,
modele et BasicSkills de Lucian temporaires. Kit : Fleche existante preservee,
Revelation 25m/6s/+20%/20s, Entrave 20m/4m/5s/-60% navigation/25s, Pas de recul
5m/0,25s/8s sans invulnerabilite. Selecteur character Lucian/Aurell/Soren,
Soren selectionne. Aucun loadout ni controller de production modifie.
Revelation avant defenses/paliers, une seule application par impact ; ticks
passifs exclus. Entrave affecte uniquement les adaptateurs de navigation,
pas les animations/actions physiques. Pas de recul utilise le slot planaire
UCC avec controles de trajet d'Eclair et nettoyage par session.
Configuration : Lit > Combat Lab > Configure Soren Lucidity Skills.
Compilation et installation idempotente passent (SorenAuthoring3.log).
Play Mode avec Domain Reload passe : SorenFinalArrowValidation.log (meme dossier).
Controle du kit, Fleche preservee, bonus par origine sans doublon, exclusion
passive, portees, expiration/proprietaire/generation des marques, zones fixes
sans cumul, sol invalide, navigation seule, huit directions, distance physique
5 m, cooldowns, aucune invulnerabilite, mur/vide/interruption/nettoyage.
Fleche jouee sur le vrai ennemi : un impact de 10 PV, stun immediat et retour
apres 2 s. Sans Domain Reload passe egalement : SorenFinalNoDomainReload.log.
Prefab final
contient presentation explicite et relais reseau ; references rafraichies au
lancement et mouvement annule a la desactivation/despawn de son proprietaire.
Relais serveur ajoute ; host/client a deux joueurs non encore valide.
Regression Aurell passe avec le nouveau selecteur : SorenAurellRegression.log.
Regression Siphon vital passe : SorenLifeStealRegression.log ; transfert reel,
portees, emplacement passif et nettoyages preserves avec l'origine Passive.
Lore et fiche combat mis a jour ; pas de migration de production.

## Aurell : quatre skills de Justice (2026-10-13)

Livraison limitee a Iluvilirae_Test : modele de Lucian temporaire avec
CharacterData/prefab et kit propres. Intercession 75%/4s/25s, reduction 50% ;
Rempart mobile 30%/6m, concentration unique ; Sommation 15m/6s/30s ;
Juste Retour 0-3m/10s, reserve seule plafonnee a 50% PV max.
Garde par victime, partage precompute sans recursion, protections sur attaques,
pieges et chutes, pas sur morts scriptées ou SetHealth. Evenements de Juste
Retour lies a sa generation d'action. Configuration : Lit > Combat Lab >
Configure Aurell Justice Skills. Validation : AurellSkillSetup.RunBatch et
AurellPlayValidation.RunBatch. Compilation et installation idempotente valides.
Play Mode passe avec Domain Reload : transfert, cumul, garde et arrondi final,
reserve, exclusivite passive, impact auteur unique, event tardif, verrous,
danger UCC, expiration/rupture de lien, cooldowns, Sommation et disengagement.
Rapport : Library/ActorAnimationSandbox/Logs/AurellFinalDomainReload.log.
Sans Domain Reload passe egalement : AurellFinalNoDomainReload.log dans le meme
dossier. Prefab final contient le relais NetworkCharacterInput ; host/client
non encore valide. Aucune migration de production. Lore et fiche combat a jour.
Les credits de reserve sont normalises apres l'arrondi final des parts de PV,
sans credit fictif pour un petit impact dont aucun PV n'a ete evite. Le ciblage
de soi est reserve a Intercession (allowSelf explicite), pas a Regeneration.
Regression Regeneration passe : AurellRegenerationRegressionFinal.log.
Regression Siphon vital passe : AurellLifeStealRegression.log. Aucun heal/passif
de Vesta modifie par les nouveaux comportements de protection.

## Lore : Vesta et Lucian (2026-10-13)

Docs/Design/Lore.md precise les identites et leur traduction jouable : Lucian
represente l'Espoir et la Lumiere, avance malgre les difficultes et porte
l'offensive; Vesta, membre de l'equipe de quatre, represente l'Altruisme et la
prudence, soigne et prend soin de la famille et des proches. Revitalisation,
Soins d'urgence, Regeneration et Siphon vital sont son kit canonique.
Leurs effets/cooldowns et le slot passif unique sont rappeles dans le lore.
Aucun CharacterData Vesta n'a ete trouve : attribution documentee, pas de
renommage des membres existants ni de migration de loadout en production.
Les skills restent accessibles dans le laboratoire pour les tests. Aucun code
runtime modifie; branchement du CharacterData Vesta a faire lorsqu'il existera.

## Siphon vital (2026-10-13)

Skill_SiphonVital dans Resources/CombatSupportSkills : passif sans cooldown,
1 PV vole par seconde de jeu sur la cible ennemie explicitement lockee a
0-10 m (3D, inclus). Soigne uniquement les PV effectivement retires, plafonne
aux PV manquants du lanceur; ne donne aucune Clarte et n'interrompt pas les
coups par un Hit cosmétique. Les validations de degats, garde, mort et paliers
de l'ennemi sont conservees. Aux PV pleins, le siphon inflige toujours son tick.

Reutilise PlayerRegenerationEffect : un seul passif maintenu par lanceur, donc
remplace Regeneration et inversement. Unlock seul ne coupe pas le lien. Hors
portee ou sous autorite cinematique, ticks suspendus et timer reajuste sans
rattrapage. Reprise a portee apres une seconde; mort/despawn/desactivation
nettoient effet et affichage. Indicateur Siphon vital sur la cible et nombres
de degat/soin effectifs. Les ticks serveur et feedbacks utilisent le relais
reseau existant; les PV ennemis passent par leur resolution habituelle.

Disponible dans Iluvilirae_Test; aucune migration des loadouts de production.
Validation preparee : LifeStealPlayValidation.RunBatch (ICombatTarget de test
dans la scene reelle pour isoler timings et bornes). Host/client et validation
sur les ennemis reels restent requis avant migration.
Compilation runtime et Unity valides. LifeStealPlayValidation passe avec
un ICombatTarget controle dans la scene reelle : lancement 0/10 inclus,
refus au-dela de 10, tick 1 PV/s, suspension/reprise sans backlog, aucun soin
si impact refuse, plafond et mort, remplacement du passif de regeneration et
nettoyage a desactivation. Rapport :
Library/ActorAnimationSandbox/Logs/LifeStealPlay4.log. Les essais precedents
etaient perturbes par le Juggernaut reel, maintenant suspendu apres rebind
du combat de test. Le combat reel/host-client reste a rejouer.

## Regeneration d'allie (2026-10-13)

Skill_Regeneration : 1 PV par seconde de jeu, aucun cooldown, un effet maintenu
par lanceur via PlayerRegenerationEffect. Choix explicite d'un allie vivant
(pas soi-meme), portee d'application 30 m configurable; la distance ulterieure
ne coupe pas le lien. SetSupportTarget/TryCycleSupportTarget ne modifient pas
le lock ennemi. Roue ouverte sur ce skill, SwitchEnemyLock (changement de cible)
fait defiler les allies; ConfirmPalette applique/transfere. Sans cible valide,
aucun cast et aucun choix automatique. Le skill est disponible dans le labo;
il faut un autre personnage joueur present pour l'utiliser.

Transfert valide uniquement : retrait de l'ancien indicateur et nouveau delai
de 1 s avant soin. Recast identique refuse sans reset de timer ni cumul. PV
plafonnes, effet conserve a PV pleins et apres EndCombat. Mort/desactivation/
destruction du lanceur ou du beneficiaire ferment effet et presentation.
Indication persistante au-dessus de l'allie et popup vert +1 PV lors des soins
effectifs; aucun popup de soin a PV pleins. Les ticks suivent Time.time, donc
pause/ralentissement global, pas un timer UI non-scale.

En reseau, demande avec NetworkObjectReference, validation skill/cible/portee
cote serveur; replication du lien, des PV et feedbacks, nettoyage au despawn.
Aucun nouveau NetworkBehaviour ajoute aux prefabs. Validation dediee :
RegenerationPlayValidation.RunBatch dans copie isolee d'Iluvilirae_Test.
Le host/client reste a valider avant migration production.
Compilation runtime validee. RegenerationPlayValidation passe dans le labo :
choix explicite, rejet soi-meme/sans cible, 1 PV par seconde, cooldown nul,
spam sans cumul, transfert et arret ancien beneficiaire, lock ennemi conserve,
persistance EndCombat, plafond PV, mort cible et nettoyage a desactivation.
Rapport : Library/ActorAnimationSandbox/Logs/RegenerationPlay.log. Le routage
manuel du bouton de selection et l'apparence restent a verifier visuellement.

## Soins de soutien (2026-10-12)

Revitalisation et Soins d'urgence sont des SkillSO dans
Assets/Resources/CombatSupportSkills : respectivement 20% des PV maximum de
tous les personnages joueurs vivants dans une sphere de 30 m (lanceur inclus)
avec cooldown 120 s, et 50% des PV maximum du lanceur avec cooldown 30 s.
Soins instantanes sans animation pour cette premiere passe, sans cible ennemie
requise. Plafond aux PV maximum, arrondi au PV superieur, aucune reanimation;
aucun cooldown si personne n'est soigne. Actions engagees, verrous externes,
pause et cinematiques ne sont pas contournes. Les cooldowns existants par acteur
sont reutilises et survivent a EndCombat.

Le laboratoire ajoute les deux skills disponibles, sans modifier les loadouts
de production. En reseau, NetworkCharacterInput valide le skill connu dans le
CharacterData du lanceur, l'etat, les portees et le cooldown cote serveur, puis
diffuse les PV soignes et confirme au seul proprietaire du lanceur. Aucun
montant de soin ni cooldown n'est fourni par le client. Ajouter les assets aux
combatSkills du futur healer est requis pour autoriser ses casts reseau.
Validation preparee : HealingSkillPlayValidation.RunBatch dans copie isolee
d'Iluvilirae_Test. Host/client reste a tester avant migration production.
Compilation runtime et import/compilation Unity (RPC inclus) valides. Play Mode
du laboratoire passe : rayon 30 m inclus et exclusion a 30,01 m, 20/50% des PV
maximum, plafond, morts/ennemis/inactifs exclus, cooldowns 120/30 independants,
absence de consommation sans soin, utilisation sans lock et protection des
verrous externes. Rapport : Library/ActorAnimationSandbox/Logs/HealingSkillPlay.log.

## Fleche de lumiere : controle immediat (2026-10-12)

Skill_2_Fleche de lumiere conserve animation, degats et VFX : cooldown 10 s
par lanceur au demarrage accepte, et stun immediat 2 s sur un impact infligeant
des degats. Aucun report a la fin du coup : l'attaque et son mouvement sont
annules. Mort, suspension cinematique et paliers QTE restent prioritaires.
Les stuns repetes ne prolongent pas la duree. Le cooldown suit le temps global
de jeu et survit au delock/fin de combat, sans modifier les autres skills (0 par
defaut). Le stun suit le temps de combat de l'ennemi.
Iluvilirae_Test ajoute la fleche aux skills disponibles; aucun loadout de
production ni personnage de perturbateur non encore identifie n'est migre.
Validation dediee : ArrowStunPlayValidation.RunBatch dans une copie isolee.
Compilation runtime validee. Play Mode Iluvilirae_Test valide : impact du clip
reel, stun 2 s, cooldown propre a l'acteur, refus pendant cooldown, nouvelle
utilisation apres expiration, conservation apres fin/reprise de combat,
attaque/avancee annulees et priorites cinematique/mort. Rapport :
Library/ActorAnimationSandbox/Logs/ArrowStunPlay3.log.
La reference Animator de la fleche est corrigee pour correspondre exactement
au state existant (Fl\xEAche, pas Fl\xE8che). Le chemin EnemyController compile
mais reste a rejouer en production; validation runtime effectuee sur Brains AI.

## Eclair traversant et Bond offensif (2026-10-12)

Eclair conserve son asset/GUID : lancement horizontal 0-10 m, trajectoire droite
jusqu'a 4 m au-dela de la position de cible capturee au lancement. Bond offensif
est une copie de SkillSO, lancee a 2-30 m, approche et frappe sans rebond.
La portee de lancement explicite est independante de la portee d'impact ; les
autres skills conservent leurs regles. Bond est ajoute uniquement au laboratoire.

Les deux skills utilisent Eclair_Contact InPlace, remplace dans le slot Animator
Skill_1_Eclair par un override limite a la session. OpenSkillContactWindow a
0,08 s autorise un impact physique unique (contact actuel ou balayage du chemin
reellement parcouru), avec la resolution/feedback habituels. Le lancement
materialise le graphe override et attend sa nouvelle progression : une ancienne
animation terminee ne peut plus finaliser immediatement le redemarrage.

UCC reste proprietaire du mouvement. Les exclusions de collision sont limitees
au couple joueur/cible, reference-counted et restaurent les exclusions precedentes.
Le decor, BattleWall et les autres acteurs restent solides ; le sol est verifie
avec marge de capsule. Les contacts de sol initiaux aux normales horizontales
artificielles ne bloquent plus le dash. Interruption/desactivation nettoient les
ressources sans liberer les verrous d'un autre proprietaire.

Configuration idempotente : Lit > Combat Lab > Configure Eclair And Bond Offensif.
Les donnees CharacterData et scenes de production ne sont pas migrees.
Validation : 8 tests EditMode et 14 scenarios Play Mode passes, avec bornes de
lancement, approche a 30 m, traversee a 2/5/10 m, mur, BattleWall, vide, cible
elargie, annulation/redemarrage et scopes imbriques/desactivation. PV retires
une seule fois et collisions restaurees. TargetMobilityFinalPlay.log et
TargetMobilityTests.xml dans Library/ActorAnimationSandbox/Logs (copie Unity
isolee). CounterSkill/LightSkill et mort en situation reelle restent a rejouer.

## Prototype clash Iluvilirae_Test (2026-10-11)

CombatLabClashController arbitre les impacts instantanes en LateUpdate, apres
les deux Animators. Volumes spheriques dedies balayes en mouvement relatif,
fenetres OpenAttackClashWindow/CloseAttackClashWindow, identite acteur/generation.
BasicSkills sol contre Attack et Attack_Followup uniquement. Copies auteur dans
Iluvilirae/Clash, overrides Animator locaux ; aucun clip production modifie.
Clash : annulation des deux actions, combo maintenu desarme, recul UCC/NavMesh
0,6 m / 0,18 s et recuperation 0,15 s. Aucun degat/resistance/charge, pas de
camera/ralenti/QTE. Etincelles laboratoire et son SFX_Combat_GuardedImpact.
Installation idempotente : Lit > Combat Lab > Configure Attack Clash.
Validation : 7 tests EditMode passes (balayage, ordre d'evaluation, generations
perimees/neutralisees), puis 3 clashes reels successifs en Play Mode dans une
copie isolee d'Iluvilirae_Test. PV et resistance inchanges, recul des deux acteurs,
session joueur et slot UCC liberes ; double nettoyage preserve un verrou externe.
Rapports : Library/ActorAnimationSandbox/Logs/ClashTests.xml et ClashPlay6.log.
Le test a revele les rayons Brains coplanaires manquant le sol : le clash utilise
une sonde locale avec origine surelevee, agent sur NavMesh et hors off-mesh link.
Centres des volumes calibres a 0,8 m devant les roots pour eviter leur depassement
a courte portee. Obstacles, BattleWall, tous les clips/angles et interruptions
cinematiques restent a verifier visuellement avant toute migration production.

## Audio et epee CounterSkill du laboratoire (2026-10-11)

Ajout de quatre signaux CombatLab_CounterHitVfx aux memes contacts. Prefab
dedie : burst bref de 24 etincelles, materiau HDRP existant M_Particles,
sans shockwave ; duree non scalee et destruction automatique. Le rig place
l'effet au lock point ennemi en XZ, a la hauteur joueur + 1,5 m en Y.
Instances nettoyees sur remise en pool/desactivation, aucun degat/son/camera
additionnel produit par ces signaux. Prefab remplacable via le SignalReceiver.
Compilation et Play Mode cible passes dans la copie Iluvilirae_Test : quatre
bursts observes, hauteur joueur + 1,5 m verifiee et raccord camera preserve.
Rapport : Library/ActorAnimationSandbox/Logs/CounterHitVfxPlay.log.
L'apparence et l'intensite du nouvel effet restent a valider visuellement.

CombatLab_CounterSkill_2 ajoute quatre signaux de slash aux contacts auteur
(0,3667 / 0,7667 / 1,2833 / 1,85 s), une voix a 0,05 s et des signaux
d'affichage/masquage de l'epee au debut/a la derniere frame. Son de contre,
impact, shake et KnockedOut existants preserves. CounterSkill_1 (AudioClipSO)
reference la voix Lloyd "That was way too easy" ; SFX_Counter_SwordSlash
reference Sword_Swing_Fast_Light_01, non metallique. Lecture non loop/non scaled.
CounterAudioSetup configure idempotemment Timeline et receiver du rig via
Lit > Combat Lab > Configure Counter Audio And Sword. AnimationEvents route
PlaySfx/PlayVoice vers AudioManager, et Show/HideTimelineSword vers la
presentation de Lucian. Une desactivation du rig masque l'epee de sa session
sans modifier celle d'une action plus recente. Aucun asset production migre.
Compilation Unity runtime/Editor et configuration deux fois passees dans le
sandbox : 4 slashs, 1 voix, 2 signaux epee et bindings persistants verifies.
Mixage audible et visibilite en Play Mode Iluvilirae_Test restent a confirmer.

## Anti-spam contre (2026-10-11)

Les pressions de contre passent par un delai minimal configurable de 0,4 s
reelles dans CombatHealthThresholdController (Counter Retry Delay Seconds).
Une tentative hors fenetre consomme aussi ce delai ; les pressions refusees
ne le prolongent pas et une nouvelle attaque ne le remet pas a zero.
Une pression pendant le verrou est consommee avant le fallback BeginGuard :
le blocage ne peut plus etre relance par spam. Maintien et relachement de
garde restent disponibles ; roulade et QTE visibles de paliers independants.
SetGuardHeld du laboratoire utilise le meme routage, sur front de pression.
Compilation runtime passee ; 3 tests Edit Mode cibles passes dans le sandbox
(delai, spam consomme avant BeginGuard, nettoyage et independance de B).
Rapport actualise : Library/ActorAnimationSandbox/Logs/CounterGuardRetryTests.xml.
Ressenti en
combat dans Iluvilirae_Test restant a confirmer ; aucun asset production modifie.

## Raccord camera CounterSkill (2026-10-11)

CombatLab_CounterRig configure entree/sortie a 0,25 s reelles. Le raccord
LitCameraDirector interpole position, rotation et FOV uniquement pendant le
rendu, avant les matrices HDRP (beginContextRendering), puis restaure la pose
source. La destination reste celle calculee par Cinemachine/UCC a chaque image.
La premiere image lance le timer : un pre-roll couteux ne consomme pas le fondu.
La Timeline, ses plans internes et les timings de combat restent inchanges.
Generation camera : une ancienne fin de session ne restitue pas une nouvelle.
Les rigs existants gardent 0 s ; les bakers Skill/LightSkill exportent les deux
reglages auteur. AnimationLab propose le meme raccord lors de la lecture native
de CombatLab_CounterSkill_2 ; composant preview transitoire, aucune camera
d'AnimationLab exportee comme MainCamera.
Compilation runtime et Editor dans le sandbox passee. Play cible PASSE dans la
copie Iluvilirae_Test : vrai CounterSkill, entree/sortie, samples pose/FOV,
restauration pose brute, horloge reelle pendant pause et generation perimee.
Rapport : Library/ActorAnimationSandbox/Logs/CounterCameraHandoff.log.
Le ressenti HDRP, la lecture native dans AnimationLab et les raccords entre
deux contres restent a confirmer visuellement. Pas de migration production.

## Axe visuel de Lucian sous lock (2026-10-10)

LucianCombatTorsoFacing est installe uniquement par Iluvilirae_Test, avec
alignPlayerCombatTorso desactivable sur LucianBrainsCombatArena. La direction
visible est mesuree entre les bras, pas depuis les axes locaux du bassin.
Correction de presentation repartie Spine/Chest, bornee a 65 degres et entree
sur 80 ms locales. Bassin, jambes, pieds et moteur physique sont conserves.
Seulement CombatIdle/CombatLocomotion sous lock ; sprint libre, proprietaire
d'action, esquive, saut, exploration et session cinematique exclus. Aucun clip,
controller, prefab ou vitesse physique de production modifie. Nettoyage de
pose conditionnel : ne restaure pas une ancienne pose sur un Animator frais.
Rapports du controle cible : Library/LucianFacingValidation.
Compilation runtime/Editor passee ; 17 cycles verifies sur le rig reel avec
bas du corps/root inchanges, puis Play cible passe (garde alignee, unlock et
action exclus, controller preserve). Captures HDRP avant/apres ; raccords et
manette physique restent a confirmer visuellement dans le laboratoire.

## Rotations rapides du laboratoire (2026-10-10)

Retour de roulade tactique : ConfigureTurnRates active aussi la compensation
du blend yaw UCC pour le facing sous lock. Le facteur moteur 0,14 ne ralentit
plus une seconde fois le taux configure. Le yaw reste borne et simule par UCC ;
aucun snap Transform ni changement d'orientation pendant la roulade. Hors
laboratoire, sans cet opt-in, le facing tactique precedent reste intact.
Compilation passee ; sortie de roulade a confirmer visuellement.

Iluvilirae_Test expose sur LucianBrainsCombatArena les vitesses de rotation :
Lucian marche 720, sprint 1080, virage important et facing combat 1440 degres/s ;
Juggernaut 1080 degres/s. ConfigureTurnRates modifie seulement la copie privee
des reglages du bridge apres binding CharacterData. Le profil ennemi est clone
pour le test puis detruit avec l'encounter ; aucun asset production modifie.
Les verrous directionnels d'attaque/roulade sont conserves. Compilation C#
passee ; ressenti et demi-tours a confirmer en Play Mode.

## Mobilite apres interruption (2026-10-10)

PlayerScriptedJumpController libere sa presentation si Jump_Start est remplace
avant impulsion, indisponible/fige pendant 1 s locale, ou si l'Animator est
desactive. Une impulsion sans decollage est recuperee apres 0,5 s locale au
sol. OnDisable efface aussi l'etat actif et les triggers ; aucune vitesse UCC
ni chute physique n'est annulee. Le handoff PlayerAction surveille maintenant
les remplacements/desactivations Animator pour ne pas ecraser la nouvelle pose.
Compilation runtime/editor passee. Test Play cible PASSE dans la copie de
Iluvilirae_Test : saut interrompu -> roulade, roulade interrompue -> nouvelle
roulade/saut, verrou externe conserve, desactivation du saut nettoyee.
Rapport : Library/ActorAnimationSandbox/Logs/MobilityRecovery.log.
Le cas specifique d'interruption pendant le handoff reste a tester ; aucune
migration de scene/prefab de production effectuee.

## Curseur prefab monde dans Iluvilirae_Test (2026-10-10)

Le laboratoire reference CombatLab_LockCursor.prefab (mesh losange HDRP Unlit,
sans Canvas ni collider). CombatLockIndicator instancie ce prefab et le suit
a ActorRoot.position + (0, 3, 0), avec orientation camera. Prefab et offset
editables sur LucianBrainsCombatArena ; aucune migration des prefabs/scenes
de production. Le fallback UI existant est conserve hors laboratoire.
Compilation C# verifiee ; rendu du prefab a confirmer en Play Mode.

## Curseur sur cible generique (2026-10-10)

Correction : BeginExternalEncounter et ToggleExternalLock activent aussi le
CombatLockIndicator sur ICombatTarget.Root (Juggernaut du laboratoire).
EndCombat le masque avant restitution. Le premier branchement concernait
seulement EnemyController et ne couvrait pas le laboratoire.

## Sprint et focus Input System (2026-10-10)

Deux filtres identifies : LocalPlayerInput.Update effacait le sprint parce
que GamepadInputContextStack.IsGameplayInputSuppressed considere Combat bloque.
Le sprint autorise explicitement Gameplay et Combat, mais pas UI/Placement/
Cinematic. Le veto Application.isFocused, concurrent du routage Input System,
est retire. Focus UI, modes et synchronisation restent prioritaires ; les
appareils deconnectes/desactives sont exclus de la lecture.
La trace initiale ForceZeroInput provenait du placement du test lui-meme,
pas d'un verrou de combat orphelin. Validation Play ciblee PASSEE dans la copie
Iluvilirae_Test : trigger tenu, bridge sprint=true, SpeedChange UCC actif,
vitesse physique non nulle, mouvement/facing camera-relative et lock conserve.
Compilation Unity passee. Rapport :
Library/ActorAnimationSandbox/Logs/CombatSprintVerified.log.

## Regression du repere tactique (2026-10-10)

Retablissement du deplacement camera-relative en camera tactique, meme sous
lock : la conversion radiale ajoutee par la passe course ne doit pas remplacer
ce contrat. Le facing tactique repasse par MovementType/UCC, sans SetRotation
par frame. FreeSprint alimente le regard par la direction de mouvement.
La validation visuelle des tremblements reste a effectuer ; aucun test long
automatique relance.

## Curseur de cible lockee (2026-10-10)

CombatLockIndicator est active par SetLockPresentation, en plus du contour.
Sans visuel auteur, une fleche screen-space non interactive suit le sommet du
modele. Elle est masquee hors champ, au delock/desactivation, et detruite avec
l'acteur. Aucun changement du ciblage, de la camera ou du mouvement.

## Course libre et roulades directionnelles (2026-10-10)

Le bridge centralise TargetStrafe / FreeSprint / DirectionalEvasion. RT + stick
en lock utilise le repere camera et tourne via UCC vers le deplacement ; sans
sprint, retour progressif au face-a-face. La camera reste verrouillee.
Les chemins tactiques concurrents sont exclus sous lock, et le client reseau
convertit l'intention une fois. Les roulades explicites utilisent le clip avant
et leur direction capturee jusqu'a la fin effective de la session. Les anciens
reglages d'alignement de roulade sont retires. Vitesses et autorites physiques
existantes conservees.

Compilation Unity runtime/editor reussie dans la copie de validation.
La validation generale avait ete arretee a la demande du developpeur.
Le blocage du sprint a ensuite ete isole et corrige (filtre Combat), avec un
test Play cible passe. Les huit directions et roulades completes restent a
valider ; traces temporaires retirees, aucun test en attente.
Ne pas presenter District 1, reseau host/client, murs ou BattleWall comme valides
sans execution. Test disponible via Lit > Combat > Validate Free Sprint in
Iluvilirae ; rapport Library/CombatFreeSprint.result.

## Confirmation de la roue de competences (2026-10-10)

SouthButton confirme le slot actuellement selectionne/survole dans la roue
ouverte, plutot qu'un index memorise par l'input. Le stick droit conserve sa
navigation ; le survol souris selectionne aussi un slot equipe. Roue fermee,
desactivee ou slot vide : aucun lancement. Les controles TryUseSkill restent
inchanges. Compilation Unity et validation Play ciblee dans Iluvilirae_Test
passees : bon skill survole, un lancement par appui, aucun lancement par maintien,
slot vide ou roue fermee. Rapport : Library/SkillWheelSouthValidation.log.

## Appuis et presentation Lucian/Juggernaut_v2 (2026-10-10)

Les 22 copies gameplay du Juggernaut dont la source est identifiee sont
reconstruites depuis les originaux : conservation du bassin/RootT/RootQ
corporels, des evenements, durees et GUID ; retrait du seul mouvement physique
mesure. MakeInPlace ne doit plus aplatir ces courbes. Le prefab Juggernaut_v2
reconcilie opt-in la hauteur NavMesh avec le sol physique via agent.baseOffset.
Les vitesses, impacts et fenetres gameplay restent inchanges.

Les profils de cadence mesuree, l'adaptateur AnimatorMonitor et l'alignement
des phases sont experimentes uniquement dans Library/ActorAnimationSandbox,
sur Iluvilirae_Test. Ils ne sont pas actives dans les assets joueur du jeu.
Les marches directionnelles et les trois raccords Strike/Sweep/Followup
necessitent encore un travail d'animation ; aucune validation d'appui ni
activation automatique des raccords. L'IK natif est audite, pas generalise.
Le validateur avant build refuse une livraison sans profils acceptes.

Video 12-38-46 : essai cible de raccords ennemis ajoute, sans activation
globale. Dans Iluvilirae_Test : Lit/Animation Presentation/Try Enemy Recovery
Preview in Iluvilirae. Controller copie et profil runtime ; sortie du Play
restaure les references originales, y compris sans rechargement de scene.
L'IK Humanoid natif ramene l'ecart de pose final Strike/Followup de 5,3 a
3,0 cm ; Sweep reste a 4,1 cm et n'est pas active. Recuperation retimee seule,
apres son marqueur, sans changer impact, duree gameplay ou moteur physique.
Attention : l'IK de Locomotion fait aussi partie de cet essai ; ses appuis
ne sont pas acceptes. Controle Play cible passe dans le projet isole ; rendu
HDRP capture. Glissement cumule et raccord complet a confirmer visuellement.
Rapport du lot : Library/AnimationReview-20261010-123846/Rapport.md.

Rapports, manifeste, mesures et captures : Library/ActorAnimationValidation.
Les 26 tests ActorAnimationPresentationTests passent (poses, evenements,
trajectoires physiques, reconstruction repetee, cadence restreinte).
Les controles Play avec et sans Domain Reload passent ; l'acceptation visuelle
des vrais modeles, pentes, collisions et host/client reste distincte.
Details et limites dans systems/combat.md et systems/input_camera_ucc.md.

## Regle de travail sur le combat

Commandes manette du 2026-10-10 : LB eloigne / RB rapproche la camera, RT = sprint,
LT maintenu = roue de combat, croix bas = verrouiller/changer de cible.
Inversion du zoom : asset et chemin tactique inverses, wrapper regenere par
Unity et compilation reussie. Sens du zoom inverse a confirmer en Play ; les
essais listes ci-dessous precedent cette inversion.
Le sprint automatique au stick tactique est retire. Deux essais Play dans
Iluvilirae_Test reussis (roue LT, attaques, cinematics, pause/restauration).
L'essai etendu verifie RT/sprint et relachement, RB/zoom sans roue, stick plein
sans sprint automatique, croix bas/verrou et restitution. Wrapper regenere
par Unity. Logs : Library/LucianCombatValidation/Logs/shoulder-trigger-play*.log.
Ressenti sur manette physique et regression dans les scenes de gameplay a confirmer.

Toute evolution ou correction du combat doit d'abord etre integree et validee
dans `Iluvilirae_Test`, avec les tests pertinents et un essai jouable dans Unity.
La migration dans le jeu intervient ensuite, apres validation du laboratoire,
avec une verification dans les scenes de gameplay. Regle detaillee dans
`systems/combat.md`.

## Animations de mort du laboratoire (2026-10-10)

LockDeathAnimation conserve maintenant l'avancement du clip quand la scene
ou le gestionnaire redemande le meme etat : la mort de Lucian n'est plus
relancee chaque frame. La mort Brains AI interrompt l'action et attend la fin
effective de Death avant desactivation, meme si le delai auteur est plus court.
Une mort pendant une cinematique attend la restitution de l'Animator puis
demarre sa chute avec un nouveau delai complet. Les prefabs et clips existants
sont conserves. Documentation : systems/combat.md. Validation dediee :
LucianDeathPlayValidation, avec vrais acteurs du laboratoire et captures neutres.
Compilation Unity et parcours Play Mode passes : mort pendant attaque/blessure,
avancement continu du clip et des os de Lucian/Juggernaut, immobilite de l'ennemi,
disparition apres clip complet, mort pendant cinematique puis restitution,
redemarrage sans verrou de mort residuel. Les 78 tests cibles de regression
passent aussi. Rapports : Library/DeathValidation (tests.xml, play.log, frames).
Le rendu HDRP et le ressenti sur manette physique restent a verifier en jeu.

## Poursuite Brains AI apres reperage (2026-10-10)

LitBrainsEnemy conserve sa cible connue a 360 degres et derriere les obstacles,
dans la portee existante de CharacterData.vision. Le premier reperage et tout
changement de cible exigent toujours cone et ligne de vue. Mort, disparition,
inactivite et sortie de portee liberent la cible ; desactivation/reprise efface
le suivi. Pause, Knocked Out et cinematique le conservent.

Attaque et impact exigent la ligne de vue avec les couches de la vision, avec
ou sans profil. Un impact bloque reste consomme. Aucun changement des ennemis
historiques, des sauvegardes ou des reglages des profils. Documentation dans
systems/combat.md ; tests de regression dans LitBrainsPursuitTests.

Validation : compilation Unity et 78 tests cibles passes ; parcours Play Mode
dans le projet isole avec les vrais Lucian/UCC et Juggernaut_v2 : approche par
derriere sans acquisition, reperage frontal, poursuite dans l'angle mort,
contournement NavMesh d'un mur sans attaque a travers, puis perte hors portee.
Le parcours de regression contre/Knocked Out passe aussi : pause/reprise avec
gamepad simule, signaux auteur, impacts, restitution et desactivation/reprise.
Controle visuel des silhouettes en poursuite et du contournement via captures
avec materiaux neutres ; ces captures ne valident pas le rendu HDRP.
Rapports dans Library/BrainsPursuitValidation (final-tests.xml, counter-play.log,
pursuit-visual.log et frames). Le rendu HDRP et le ressenti
sur manette physique restent a verifier dans l'editeur principal.

## AnimationEvents centralise (2026-10-09)

AnimationEvents remplace PlayerCombatAnimationEvents en conservant son GUID.
Les clips conservent leurs callbacks ; les signaux CameraShake/KnockedOut et
LightSkill passent aussi par ce recepteur. Pas, SpiritBond et leviers deleguent
aux services existants, dont les anciens noms de reception sont retires pour
empecher les doubles appels. Les personnages et PlayableDirector du contenu
jeu ont le composant ; RequireComponent, assistants et ObjectFactory couvrent
les nouvelles creations. Les cibles suivent les bindings courants ou les
references explicites de l'Inspector. AnimationLab reste sans gameplay en
aperçu, avec Preview Only sur ses recepteurs. Validation avant build et menus
Lit/Animation Events ajoutes. Trois reactions orphelines sans SignalAsset de
l'ancien prefab AnimationLab ont ete retirees.

Validation : audit des references de 1 378 assets source sans recepteur
manquant sur les objets serialises ; validation native des prefabs/scenes
presents dans le projet isole ; compilation Unity et 70 tests cibles passes.
Parcours Play Mode avec vrais prefabs/UCC et gamepad simule passe : quatre
CameraShake aux temps auteur, contre, Knocked Out, VFX unique, degats sans
Hurt pendant le stun, expiration et reprise apres cinematique.
Rapports : Library/AnimationEventsMigrationValidation (final.xml, play.log,
audit.json, native-final.log). La passe elargie a aussi signale d'anciens tests
reposant sur des champs/contrats precedents (reactions privees, Animator racine,
presentation Lucian et masque de torche) ; ils ne sont pas corriges ici.
Le rendu HDRP et le ressenti sur manette physique restent a valider en jeu.
## Outlines interactifs et camera tactique (2026-10-04)

`RuntimeOutlineInspector` adapte maintenant le contour des interactifs a la
distance entre la camera principale et la cible selectionnee. Par defaut, il
passe progressivement de 4 px a 3 m a 1,5 px a 16 m, ce qui garde les objets
proches lisibles sans epaissir l'image en camera tactique. Les quatre valeurs
sont reglables sur `GameplaySessionRoot > OutlineManager`.

## Flames et atmosphère du château (2026-10-04)

Les Flames fixes, Ancient Flames et la torche Lucian utilisent `LitContrastLight`.
Les Flames sont chaudes, courtes et ombrées ; les Ancient Flames combinent un
coeur chaud et une aura cyan verticale. La torche reste à 5 m, identique à son
rayon d'influence. Le `Castle_Volume` porte maintenant la poussière glacée
avec des systèmes de particules sérialisés, et les Flames génèrent des braises
uniquement lorsqu’elles sont allumées. La brume du château est
resserrée à 62 m avec un libre parcours moyen de 28 m. Les contrôles de volume,
densité et taille des particules sont exposés dans l'Inspector. Valider en Play
les activations Flame/Ancient Flame, le rendu de poussière et la torche après
équiper/ranger.

## Munin retiré du gameplay (2026-10-04)

Munin n’est plus présent sur les prefabs joueur et son interface est retirée des
scènes de démarrage. Les Flames et Ancient Flames s’allument avec `Interact`, à
proximité, en consommant respectivement 10 ou 20 points de torche. La torche
commence à 1 000 points et consomme 1 point par seconde lorsqu’elle est sortie.
En exploration, le maintien de R3 multiplie sa portée par 2,5 et consomme 20
points par seconde ; combat, relâchement, rangement ou réserve vide restaurent
immédiatement le comportement normal.
Munin reste un élément de lore : une future
fusion de Lucian et Munin, inspirée de *Shaman King*, pourra servir d’action
spécifique, sans mécanique implémentée pour le moment.
## Eclairage de contraste du château (2026-10-04)

`Lit_ContrastLight` est un prefab HDRP réutilisable avec les préréglages
Faisceau lunaire, Flamme et Magie/cristal. Chaque instance reste réglable dans
l'Inspector (couleur, unité/intensité, portée, ombres et volumétrie) et ne
modifie jamais l'exposition ni le bloom global. `Lit_Volume_Castle` réduit le
fill indirect à 0,28, conserve les réflexions à 0,65 et les probes à 0,75 ; son
bloom reste réservé aux sources très lumineuses. Compilation Unity réussie ; valider
en Play : château sombre, faisceau lunaire lisible, flamme avec ombre douce,
arrière-plan presque noir et épée sans blanchiment.
## Verification Nina apres socle de cycles (2026-09-27)

La scene Nina avait encore deux activations dependantes du flag historique 20, alors que le cycle migre ne l'ecrit plus. Elles utilisent maintenant l'etape nina_spoken : apres les deux connaissances, Nina devient Dead; apres son dialogue, Nina's Blood et Scar deviennent visibles. Les tests de scene et de recompense ont ete mis a jour pour refuser ce retour aux flags. Unity batch reste indisponible : valider dans l'editeur le parcours scientifique, lettre, Nina, sang, Scar et rechargement d'une sauvegarde historique.

## Socle de cycles narratifs (2026-09-26)

Nina utilise desormais les etapes nommees comme source de verite. Les anciens bits sont conserves dans narrative.district1.nina uniquement pour une migration versionnee executee une fois : scientifique (1), Nina (20), Scar (8) et aftermath (2). La defaite du scientifique accorde Existence des chimeres via la recompense de l'etape; Scar accorde Cicatrice de la meme maniere. CycleProgressionService persiste les faits generiques avant les prerequis, reconcilie les etapes et les recompenses lorsque les services deviennent disponibles, et reconnait les etapes terminales lors du saut de scene. CycleReward couvre Skill, Knowledge, Item, WorldVariable et Activation. Les items attendent dans la boite Maison si le coffre est absent ou plein. Les dialogues reseau demandent maintenant une validation du serveur avant affichage local. Lit/Narrative/Validate All Cycles est execute avant build; Lit/Narrative/New Cycle cree une definition sous Resources/Narrative.

Validation Unity batch lancee avec 6000.4.9f1 : l'editeur a quitte immediatement avec son code interne 1 avant compilation (aucun diagnostic C# dans le log). Retester dans l'editeur : migration 0/1/2/4/8/16/20/31, recompenses, coffre Maison, dialogues host/client et validation globale.

## Cycle Luc migre vers les etapes nommees (2026-09-27)

Luc n'avait aucun flag historique : les connaissances existantes du collier et de la tombe alimentent des etapes Knowledge, puis le dialogue luc valide interaction et fermeture. La connaissance Le destin de Jon est maintenant une recompense de l'etape terminale et ne peut etre accordee qu'une fois. L'outil Lit/Narrative/Configure Luc Cycle relie Ghost_Luc au CycleController dans District_1_Cycle_Luc et corrige son nom dans le manifest. Compilation Unity et execution LucCycleTests restent a faire apres import des nouveaux scripts.
## Continuite des actions joueur (2026-09-20)

- PlayerActionPresentationController identifie ses sessions par acteur/generation et centralise Completed, Interrupted, Failed, OwnerDisabled et Death. Les nettoyages sont invalides avant leur execution : un ancien callback ne termine pas la session suivante.
- Les verrous UCC externes utilisent des handles idempotents. Migration des cinematiques, paliers, recul, planar motion, sequences narratives, transitions d'age et echelles ; plus de liberation externe anonyme.
- Roulade, invulnerabilite et lunge sont rattaches a la generation. Les reactions aux degats passent par la presentation au lieu d'un CrossFade concurrent. Une anomalie desarme les BasicSkills maintenues ; garde et buffer de mobilite conservent leurs regles existantes.
- Les Animation Events joueur verifient etat/clip et refusent les fondus ambigus vers le meme etat. Les paliers et cinematiques gardent leurs autorites specialisees. Menu Lit > Combat > Validate Player Action Event Blends pour les evenements trop proches de l'entree.
- Validation : compilation runtime/editeur et 13 tests EditMode reussis. Tests Play Mode et combat District 1 encore a confirmer ; ne pas considerer la validation visuelle comme acquise.
- Deux fiches Markdown avaient des octets Windows-1252 melanges a UTF8 : normalisation des seules sequences invalides, notes preexistantes conservees.

## Lumières directionnelles par volume local District 1 (2026-09-20)

`LocalVolumeSunController` est porté par chaque Volume HDRP concerné :
`Castle_Volume` pilote `Moon Light`, `Oldbarn_Volume` pilote `Sun Light`. Les
lumières démarrent inactives, puis sont activées seulement lorsque le personnage
est dans le Volume associé. Intensité et couleur restent entièrement modifiables
depuis les composants Unity de la lumière, y compris en Play. Compilation runtime
à refaire ; à vérifier en Play : Castle, Oldbarn, frontière et chargement de scène.
## Volumes locaux District 1 (2026-09-20)

District_1_Core ancre l'evaluation des Volumes HDRP sur le personnage controle
via LocalVolumeAnchor : une camera troisieme personne ne peut plus activer un
volume avant le joueur. Le volume local Oldbarn dispose d'un blendDistance de
2 m, au lieu d'une transition instantanee. Les volumes actifs restent locaux ;
leurs BoxCollider definissent les limites de zone. Compilation runtime reussie.
A verifier en Play : suivre le personnage et la camera independamment a la
frontiere Forest/Oldbarn, puis ajuster les BoxCollider si un perimetre de
design doit changer.
## UI monde d'interaction commune (2026-09-19)

Les sept producteurs de libelles monde utilisent UI_World_InteractionBox via
WorldInteractionUiSettings (asset Resources). Style depuis le prefab, libelles et
conditions geres par chaque interactif ; anciennes references conservees pour
reprendre les textes. Fallbacks TMP proceduraux supprimes. Compilation C# runtime
et editeur reussie ; WorldInteractionUiTests compile, non execute. Validation
Play restante : ladders, ghosts, steles, triggers, disparition et changement de scene.
## Outlines dans l'Inspector (2026-09-19)

RuntimeOutlineInspector est configure sur GameplaySessionRoot/OutlineManager,
persistant dans DontDestroyOnLoad. Reglages : affichage, couleur/opacite,
epaisseur ; diagnostic lecture seule : cible, owner, contours et suspensions.
Copie runtime du materiau pour ne pas modifier l'asset pendant le Play.
Compilation C# runtime/editeur reussie. Validation Unity Play a effectuer :
selection d'un interactif, changement couleur/epaisseur, suspension combat,
changement de scene et retour au menu. Masque sensible a la transparence : seuil
0.1 reglable, texture/UV/opacite transmis par cible, alpha particules, canal rouge
pour GlitteringStars. RuntimeOutlineAlphaTests ajoute et compile, non execute.
Rendu GPU et compilation shader a valider dans Unity sur les feuilles des ladders.
## Continuite apres combat (2026-09-19)

Suppression de StopResidualPlayerMovementAfterCombat : il repositionnait le joueur,
arretait toutes les capacites puis coupait le vol au prochain relachement du stick.
La sortie annule les actions de combat, restitue la locomotion animee au sol et
reconcilie les commandes maintenues. Un resultat masque libere son mode UI.
CombatExit journalise les blocages restants. Compilation C# runtime/editeur reussie ;
tests CombatExplorationRecoveryTests compiles, non executes dans Unity.
Validation Play encore necessaire : fuite en mouvement/esquive, victoire puis
ladders, Ghost_Scientifique fou, inventaire, torche et Munin ; repeter deux combats.
## Navigation du MainMenu : cibles UI uniquement (2026-09-19)

MainMenuNavigation ignorait les objets decoratifs 3D porteurs de MenuCursorAction. Leur focus pouvait etre invisible et bloquer le parcours clavier/manette. Seuls les controles places sous un Canvas sont maintenant eligibles. MainMenuInputRecovery remet aussi la preference de controle en automatique lorsque le joueur agit avec l'autre peripherique, pour ne jamais bloquer clavier ou manette. Compilation C# runtime et editeur reussie ; test Unity MainMenuLoadBrowserTests a executer.

## Volumes HDRP portés par les scènes Core (2026-09-20)

`ZoneManifest` et `ZoneRuntimeContext` ne pilotent plus de profil HDRP.
`EnvironmentManager`, `EnvironmentZone` et leur état runtime ont été retirés,
y compris leurs composants de Bootstrap et Outdoor. Les Volumes restent des
composants HDRP configurés dans les scènes Core et additives, sous la gestion
des composants `Zone` déjà présents. Compilation runtime à refaire ; vérifier
les chargements Maison/District 1 et les transitions entre volumes locaux.
## VFX ecran du MainMenu : layer et taille (2026-09-19)

Le layer historique `VisualEffect` n'existe plus : les VFX ecran utilisent
`VisualEffect_ScreenSpace`. Le survol du menu selectionne ce layer par masque,
le rend uniquement avec sa camera VFX et l'exclut de la camera de decor. La
taille du VFX reste entierement definie par son Transform et son ParticleSystem
dans l'Inspector. Compilation C# runtime reussie ; la scene MainMenu etait
ouverte par Unity pendant la correction, donc sa reserialisation et le test Play
restent a confirmer.
## Brasero de Maelle : influence garantie (2026-09-19)

Le brasero de Maelle ignore la neutralisation par ennemi : cette regle pouvait rendre une flamme allumee incapable d'activer Necklace et la porte. Les deux cibles sont dans son rayon effectif de 6 m. FlameInteractionReachTests couvre cette configuration. Compilation C# runtime/editeur a refaire, puis tester dans Unity : allumer avec Munin, s'approcher de Necklace et de la porte, avec un ennemi proche puis absent.

## Porte de Maelle : contour et selection (2026-09-19)

`Moon_Room_Door_Maelle` possede maintenant un `RuntimeOutlineTarget` sur son
renderer. La selection locale respecte la priorite des interactables avant leur
distance : Necklace reste donc la cible tant qu'il existe, puis la porte est
detectee, affiche son contour et s'ouvre avec Interact. Compilation C# runtime
et editeur reussie ; FlameInteractionReachTests couvre la presence du contour,
mais le test Unity et la validation en Play restent a executer.
## MainMenu_Load : sessions puis sauvegardes (2026-09-13)

Choix explicite de session, navigation limitee a sa liste de sauvegardes puis
Retour aux sessions en conservant la selection. Survol sans reconstruction,
focus souris/clavier/manette synchronise. Scene : deux listes defilantes a gauche,
apercu et district a droite, textes lisibles et references RawImage explicites.
Compilation C# runtime/editeur reussie avec Unity 6000.4.9f1 ; cinq tests ajoutes,
non executes. A valider en Play : navigation clavier/manette/souris, listes longues,
annulation du chargement, suppression et sessions vides, apercu absent, 16:9/21:9.
Les changements MainMenu preexistants ont ete conserves.

## Brasero de Maelle et interactions (2026-09-13)

InteractableItem verifie directement la portee effective des flammes, comme Door,
en secours des notifications physiques. La portee accepte les MeshColliders non
convexes via les bounds. Les flammes desactivees ne fournissent plus cette
influence directe ; les ennemis desactives ou sans combat ne la bloquent plus.
Compilation C# runtime et editeur reussie avec Unity 6000.4.9f1.
FlameInteractionReachTests ajoute (non execute). A retester en Play : allumer le
brasero de Maelle avec Munin, recuperer Necklace, ouvrir la porte ; verifier aussi
la perte d'influence et le blocage bleu par un ennemi hostile vivant a moins de 8 m.
Le symptome initial n'a pas ete reproduit en Play pendant cette correction.

## Fermeture inventaire et champs obsoletes (2026-09-13)

SetReadableActionInputsVisible(false) utilise uniquement la reference deja
resolue a l'ouverture : aucune recherche GameObject.Find pendant OnDisable ou
apres destruction des inputs. Une desactivation ne reparent plus ces objets.
GlobalAgeZone.useAgeManager et GhostController.reactionChoiceWidth supprimes,
sans consommateurs C# ; AgeManager et le panneau de choix restent responsables.
Compilation C# runtime/editeur avec Unity 6000.4.9f1 reussie, avertissements
cibles absents. Trois tests InventoryReadableLifecycleTests ajoutes, non executes.
A valider dans Unity : ouvrir/fermer livre et parchemin, changement de scene et
Play/Stop, notamment avec panneau jamais ouvert ou deja detruit.

## Fin des cycles partagee par la partie

CycleDefinition configure completionFlags et cycleSceneName. Nina termine au
bit 8 (Cicatrice). Le serveur attend dialogues et dissolutions des clients
connectes avant de decharger la scene via Netcode. En solo, meme attente locale.
GameFlow ignore les scenes terminees lors des prochains chargements et de la
validation NavMesh. Jalons, skills et snapshot d'arrivee tardive conserves.
Tests ajoutes pour les jalons et l'attente des presentations. Compilation C#
runtime/editeur avec Unity 6000.4.9f1 reussie ; tests Unity non executes.
A verifier en Play Mode : disparition Scar puis dechargement, hote/client,
arrivee tardive, deconnexion pendant attente, sauvegarde/rechargement et retour
de zone. Scene active principale protegee contre le dechargement.

## Scar : dialogue de deux secondes puis dissolution

Le dialogue de Scar reste affiche deux secondes hors fondus, puis sa fermeture
naturelle accorde Cicatrice et lance la dissolution sans attente supplementaire.
Scar se desactive apres ses effets. La duree propre au dialogue est partagee
par UI et validation serveur; les autres dialogues gardent la duree du cycle.
Les conditions narratives et le jalon sauvegarde restent inchanges.
Definition, generateur et tests adaptes. Compilation C# runtime/editeur avec
les references Unity 6000.4.9f1 reussie; tests Unity non executes, sequence
visuelle et reseau a confirmer en Play Mode.

## Scientifique : connaissance requise avant combat

La rencontre exige Knowledge_ExistenceDesChimeres via
CharacterData.enemyEncounterOptions.requiredKnowledge. Controle local et
autoritaire avant introduction, puis a sa fermeture. Sans connaissance, le
scientifique reste Ghost; l'obtention seule ne declenche pas le combat.
Generateur et tests adaptes. Compilation C# runtime/editeur verifiee; parcours
avec/sans connaissance et reseau a valider dans Unity.

L'utilisateur confirme que le probleme de locomotion joueur est regle apres
reserialisation des fiches.

## Locomotion/saut : fiches joueur reserialisees avec Unity

Cause constatee : la fiche Lucian importee par Unity contenait les valeurs par
defaut (driveLitLocomotionAnimatorParameters=false, speedParam=Speed), malgre
les bonnes valeurs dans le YAML. Le cache seul ne corrigeait donc pas la panne.
Les blocs playerSettings complets des quatre fiches ont ete reinjectes via
SerializedObject, sauvegardes et reimportes avec Unity. Reglages et references
auteur verifies (830 entrees), autres champs existants preserves. Lucian :
LitSpeed, pilotage actif, jumpHeight=100. Copies Unity controlees apres reimport.

Trois verifications de cache executees dans Unity : succes. Compilation C#
runtime/editeur reussie; test FirstWalkAndRunDriveGameplayAnimator ajoute mais
non execute. Retour visuel utilisateur marche/course/saut encore attendu;
reseau et Domain Reload restent a verifier. Probes temporaires retires apres
reparation. Resultats de diagnostic dans Library/PlayerSettingsProbe.

## Modules joueur : migration appliquee, validation Unity restante

Reglages centralises dans CharacterData.playerSettings, copies runtime par module, reservations de mouvement avec proprietaire, evenements de clips routes vers presentation et mobilite, branches ennemies retirees de PlayerAnimationController. Debugger facial remplace par commandes editeur. Quatre fiches/prefabs joueurs et AnimationLab migres; outils adaptes. Compilation C# runtime/editeur reussie; tests compiles et references/YAML controles. Execution Unity, reseau et Domain Reload restent requis. Le prefab Lucian deja ouvert dans Unity doit etre recharge apres import (ancienne instance signalant un script manquant lors de l auto-enregistrement). Guide : Assets/Characters/PlayerModules/README.md; arbitrages : AIAgent/Codex/player_method_migration.md.

## References internes EnemyController

Les references aux scripts (sante, contrats, controleur, temps local, contour et indicateur) sont privees et non serialisees. Elles sont resolues automatiquement. Les references Unity configurables restent visibles; les categories sans champs sont masquees. Compilation C# runtime/editeur verifiee; affichage Unity et parcours combat restent a valider.

## Vision et perception

VisionField est supprime. CharacterData.vision centralise portee, angle, hauteurs et masque d obstacles. EnemyController porte l origine du regard et les methodes de detection; la surcharge temporaire de portee reste locale a l instance. Le verrouillage joueur reutilise CharacterData.TryEvaluateVision. Les quatre prefabs et leurs fiches conservent leurs valeurs distinctes. Compilation C# runtime/editeur et ressources verifiees; tests de portee, angle, obstacles, triggers et copies runtime prepares. Execution Unity et parcours de detection/verrouillage restent a valider.

## Options de mort des ennemis

ScientistEncounterController supprime; EnemyController gere l interaction Ghost et la presentation de mort. Les options sont dans CharacterData; prefab scientifique, interface, disparition et tests migres. Compilation C# runtime/editeur verifiee; import Unity/ILPP et tests en jeu restent a executer.

## Cycle Nina : mort du scientifique et pose Dead

Le cycle reference directement EnemyController lorsque la rencontre n'a plus de
SceneMarker. Les rencontres avec marker suivent en priorite RuntimeInstance.
La pose Nina pilote le booleen isDead et utilise le chemin complet de l'etat
Animator. Les deux connaissances restent requises; aucune dependance a la
cinematique n'est ajoutee. Regression couverte par des tests de liaison, mort,
rechargement et presentation. Compilation C# runtime/editeur et YAML verifies;
execution des tests Unity et parcours en jeu encore a valider.

## Unification des ennemis — validation Unity restante

CharacterInfo reprend la sante de CombatHealth et copie les donnees CharacterData
par instance. EnemyController partial regroupe le combat, les decisions, la
navigation, la physique et les animations ennemies. Les anciens composants et
l’executeur historique sont supprimes. Les fonctions d’animation du joueur sont
separees. Les reglages sont dans CharacterData/EnemySettings et le profil existant,
avec inspecteurs par categories et tooltips. Prefabs et references de scenes
migres; GiantJuggernaut a une fiche distincte pour ses reglages differents.

Compilation C# runtime/editeur reussie avec Unity 6000.4.9f1; tests compiles et
controles YAML/references effectues. Import Unity/ILPP, execution des tests et
parcours solo/hote/client restent a valider : ne pas considerer le lot clos.
Voir enemy_method_migration.md et Assets/CombatRealTime/Enemies/Controller/README.md.

## Scientifique : fantome puis ennemi

Le prefab utilise GhostController/GhostData_Scientist pour la proximite et
l'interaction initiales. ScientistEncounterController implemente le handler
commun IGhostInteractionHandler et ne lit plus lui-meme les inputs. L'interaction
lance la replique puis active l'ennemi ; GhostController libere son affichage et
reste desactive pendant le combat. Les quatre copies de composants ajoutees
sur l'instance Nina sont remplacees par les composants herites du prefab.
Compilation C# runtime/editeur et controle YAML/references valides. Tests ajoutes,
execution Unity et parcours solo/hote/client a verifier dans l'editeur.

## Standardisation des cycles

CycleController, CycleDefinition, CycleInteraction et CycleSharedSkills remplacent
les scripts runtime Nina sous Assets/Narrative/Cycles. Dialogues, conditions,
recompenses, activations et poses sont configurables pour d'autres cycles.
GhostController absorbe la revelation/outline auparavant deleguee a l'adaptateur
Nina. Scene, definition Nina, generateur et tests migres en conservant GUID,
cle de sauvegarde et jalons. Cicatrice reste accordee a la fermeture du dialogue.
Guide d'auteur et inspecteurs generiques ajoutes ; tests de cycles independants,
conditions et validation des fermetures reseau ajoutes. Compilation C# runtime
et editeur reussie avec Unity 6000.4.9f1 ; YAML de la scene/definition valide et
references verifiees. L'editeur ouvert n'a pas repris l'import durant cette passe :
import Unity/ILPP, execution des tests et parcours solo/reseau restent a valider.

## Physique de Scar

Ghost_Scar recoit un Rigidbody dynamique avec gravite, rotation bloquee et
collision continue. Sa capsule corporelle porte le personnage; les colliders
de squelette/accessoires et le root motion du prefab Scar sont desactives.
Le modele et l'interaction suivent la meme racine physique. Test de chute sur
sol ajoute; compilation editeur verifiee. Execution Unity du test et validation
en jeu (apparition, sol/pente, interaction, solo et hote/client) restent a faire.

## Apprentissage Cicatrice et SkillUnlockPanel

Parler a Scar accorde Cicatrice a la fermeture naturelle du dialogue, apres le
fondu, une seule fois pour le groupe. Une annulation ne donne pas la competence.
Le SkillUnlockPanel cree dans Bootstrap affiche les nouveaux
apprentissages avec la presentation des connaissances. Nom de Cicatrice corrige;
skill retire de la liste et de l'equipement initiaux de Lucian. Etat sauvegarde
et autorite serveur conserves. Tests de regression ajoutes (recompense unique,
snapshot, interaction invalide, donnees initiales et texte du panneau).
Compilation C# runtime/editeur avec Unity 6000.4.9f1 verifiee. Execution des tests
Unity, rendu du panneau et parcours solo/hote/client restent a valider dans
l'editeur deja ouvert sur le projet.

## Ennemis morts

La locomotion ignore toute poursuite/rotation et ne remplace plus la pose de mort.
EnemyDeathPresentation, ajoute automatiquement aux RealTimeCombatEnemy, attend
l'animation (minimum 3 s) et le dialogue de mort du scientifique avant la
dissolution Ghost sur 2.8 s et la desactivation de la racine. Un materiau Ghost
temporaire couvre les shaders sans dissolution, sans modifier les assets source.
Compilation runtime/editeur validee. Test de regression cible/orientation ajoute;
execution Unity et rendu sur scientifique/Juggernaut, sauvegarde et host/client
restent a verifier.

## Stabilisation des erreurs Unity

Les huit lots ont recu les corrections de code/assets detaillees dans
[stabilization_report.md](stabilization_report.md). Import Unity/ILPP reussi et
96 tests EditMode distincts passes. Le marker scientifique a -98,16 m est valide sur le
NavMesh a 8 mm pres ; correction de la sonde de sol et de la capsule, sans teleport.
Etat solo scientifique separe du reseau, references UI explicites, doublon de
flamme corrige, API migrees. Sept champs de compatibilite restent signales.
Validation visuelle/GPU, sauvegarde en jeu et host/client/late join restantes.

## Nettoyage Netcode et Animator Nina

NetworkObject.OnDestroy tolere un SpawnManager sans collections runtime;
le retrait du suivi de scene ne dereference plus NetworkConfig au nettoyage.
Trois cas de regression editeur ajoutes et passes dans Unity en batch EditMode.
Compilation NGO/runtime et editeur validee. Les cycles Play/Stop avec/sans
Domain Reload restent a verifier dans Unity.
La transition Idle -> Dead de Nina_Controller utilise maintenant le bool isDead
existant au lieu d'une condition numerique avec un parametre vide.

## Cycle Nina

La victoire du scientifique joue Death, sa replique "Qu'est ce que... j'ai fait..."
et deathVoiceLine (Forgive me). Le panneau de victoire attend la fermeture du
dialogue; la suite du cycle attend la fermeture du panneau. Voix et dialogue
sont nettoyes en cas d'interruption. AudioClipSO cree et assigne au prefab.
Compilation runtime/editeur validee; test des references ajoute. Enchainement
visuel/audio et verification host/client restent a effectuer dans Unity.

Scene District_1_Enigme_Ghost_Nina et donnees creees, ajoutees au manifest District_1.
Mort scientifique -> Existence des chimeres immediatement; lecture Item_Edward
-> Dilemme Edouard.
La revelation d'Existence ne depend plus de la Timeline. Une sauvegarde avec le
scientifique deja mort recupere aussi ce savoir s'il manque.
Toutes les connaissances du cycle (Existence + Dilemme)
sont l'unique condition de Nina Dead. Parler a Nina Dead active ensemble sang et
Scar (bits NinaDeadSpoken=16 et NinaVisited=4). Scar conserve la revelation Ghost
standard : invisible au loin, apparition progressive a proximite. Son dialogue
termine naturellement accorde Cicatrice. CinematicCompleted n'est pas requis.
La portee utilise le collider de l'interaction. Les anciens bits 4/16 restent compatibles.
Compilation runtime/editeur reussie avec le compilateur Unity; 15 cas NUnit de
progression executes sous Mono hors editeur et passes.
Flux visuel Play Mode et validation multijoueur restent a effectuer.
Progression serveur via variables monde persistantes, replication active NGO.
SkillsManager compose les recompenses avec les skills auteur sans modifier CharacterData.
Modeles, clips, Timeline, placement final et SkillSO Cicatrice restent explicitement a assigner.
Voir Assets/Narrative/NinaCycle/README.md. Validation multijoueur et visuelle restante.
Compilation C# runtime/editeur validee avec les references et le compilateur Unity.
Tests NUnit de prerequisites et de lecture ajoutes, non executes dans Unity.

Correctif en attente d'import Unity : le parchemin Édouard reçoit un collider
racine afin que son world-info ne puisse plus échouer, Nina délègue sa
disponibilité/outline au cycle narratif, et le Scientifique utilise une porte
réseau `Interact → réplique groupe → combat`. Le configurateur éditeur recale
son marker sur le NavMesh et configure aussi Flame_Base_5. Au moment de la
dernière tentative, Unity est bloqué par une OutOfMemoryException d'AssetImport;
la compilation et le bake doivent être rejoués après récupération de l'éditeur.

## Interactions et outlines

`LitUccStateMotionAbility` est un hook UCC automatique et concurrent. Il reste
actif au repos, mais n'est plus considéré comme une ability bloquante par
`LitUccInteractionBridge`; autrement la détection Lit est annulée avant même la
sélection et aucun outline ne peut apparaître.

Les notes, parchemins et livres monde sont maintenant lus avant tout ramassage.
La lecture révèle leurs knowledges ; A / SouthButton prend ensuite le document,
tandis que B / EastButton ferme la lecture et le laisse en monde. Le panneau
auteur `ReadableActionInputs` (enfants `A` et `B`) est reutilise, jamais cree
au runtime. Les stèles restent des lectures fixes.

Tous les Ghost suivent maintenant la revelation de proximite de Ghost_Luc : hors
rayon, aucun rendu, outline ou interaction; l'approche restaure progressivement
leur presentation. Nina et Scar gardent leurs preconditions narratives sans
contourner ce contrat.

`RuntimeOutlineTarget` est strictement local : il ne modifie plus les layers de
ses enfants, afin qu'un target corps n'entoure jamais cheveux, accessoires ou VFX.
La sélection d'outline est désormais entièrement passive : chaque cible est
posée par l'auteur sur le renderer voulu et aucun script ne peut en ajouter
automatiquement au runtime ou durant une configuration éditeur.

## EnemyAttack : impact instantane unifie

EnemyAttack(SkillSO) remplace les evenements ennemis separes de contact et de
VFX. EnemySkills echantillonne les colliders uniquement a cet instant, deduplique
les victimes et refuse un second impact du meme ActionSequenceId. La resolution
centrale preserve esquive ciblee, invulnerabilite, reduction de garde et affichage
des PV effectivement perdus. SkillSO expose les feedbacks degat/garde/contre;
ses VfxCues existants sont conserves. Aucun effet camera ajoute.
Les quatre attaques Juggernaut sont migrees, Assomoir a 1.70 s explicitement.
GiantJuggernaut Jump conserve sa condition de cible au sol dans enemyImpact.
Les impacts cinematiques Light/Counter/Skill restent distincts et inchanges.
Compilation runtime et editeur reussie; tests auteur ajoutes/actualises.
Execution des tests Unity et validation en combat non realisees dans cette passe.

## Affichage des degats recus

SquadCharacterController.RecordDamageApplied affiche le montant effectivement
subi avec CombatDamageWorldFeedback, y compris les coups UCC hors gestionnaire
de combat. Les appels Lit vers UCC rapportent une seule fois leur resultat mesure;
le callback UCC ne duplique pas ce feedback. Le gestionnaire conserve seulement
l'affichage de son fallback CombatHealth. Aucune sante disponible = aucun degat
fictif ni nombre affiche. Les feedbacks ennemis restent inchanges.

## Reactions ennemies invisibles B/Y

OpenEnemyReactionOpportunity ouvre B pendant 0.5 s et Y pendant 0.2 s reelles.
Le ralenti global 0.4 est configurable sur CombatHealthThresholdController
(1 = sans ralenti). Aucun panneau ni changement de map; les paliers gardent
leurs QTE visibles. B doit demarrer une roulade immediate sans buffer avant
d'accorder une protection ciblee au coup; Y utilise uniquement le CounterSkill.
ActionSequenceId distingue aussi deux coups consecutifs utilisant le meme skill.
Le handle de temps, les echeances et la protection sont independants des paliers.
Les clips Juggernaut et l'installateur utilisent le nouveau declenchement.
Assomoir utilise maintenant EnemyAttack a 1.70 s, timing confirme par l'auteur.
Compilation runtime/editeur reussie. Tests de fenetres, doublons, maintien et
identite ajoutes; execution Unity a revalider, aucun rapport recent obtenu.
Validation de combat Play Mode non realisee (capture de l'editeur indisponible).

## EnemyRush 3D

La ruee utilise le vecteur cible complet XYZ et un CapsuleCast dans cette
direction. Pendant la ruee, le moteur remplace la verticale balistique par
la verticale de ruee; a l'arret, la gravite reprend sans impulsion ascendante
residuelle. Un obstacle termine la ruee. La demande d'atterrissage reste
memorisee sans ecraser sa trajectoire; le timeout aerien reste prioritaire.
Distance d'arret mesuree en 3D. BeginEnemyRush applique maintenant une vitesse
immediate dans une direction capturee une fois, sans homing ni interpolation.
Fin automatique apres rushImpulseDuration (0.35 s locales par defaut), distance
maximale ou collision. EndEnemyRush retire du clip Assomoir et du contrat auteur;
l'API interne reste disponible pour annulation. Acceleration et homing historiques
retires. Compilation verifiee; validation en combat restant a effectuer.

## Selection adaptee aux skills equipes

EnemyCombatBrain applique la preference melee seulement si un pattern terrestre
complet est equipe. Un equipement uniquement aerien poursuit/recul selon sa portee
et choisit ses attaques sans tirage alternatif a 25 %. Un unique pattern peut se
repeter apres son cooldown et sa recuperation; les limites de repetition restent
actives avec plusieurs patterns equipes valides. Les combos incomplets sont exclus.
La validation editeur n'impose plus quatre skills equipes au Juggernaut.

## Juggernaut : evenements essentiels

Les quatre SkillSO utilisent un seul QTE Y (Strike/Followup 0.55 s, Sweep
0.625 s, Assomoir 0.837 s). Warning, telegraphie, reactions legacy et VFX
hors impact retires des clips/profils. Les deplacements gardent leurs timings.
Assomoir ouvre/ferme sa hitbox au meme instant (1.70 s). Les ripostes de palier
reutilisent ces evenements avec detection spatiale et impact unique.
Le menu Lit/Combat/Clean Juggernaut Animation Events effectue le nettoyage
cible sans reconstruire patterns, controller ou trajectoires. Les deux
installateurs deleguent au meme contrat pour ne pas recreer les warnings.
HitPlayerIf reste present pour GiantJuggernaut; les anciens dash et selections
par evenements sans references ont ete retires. Verification visuelle en combat
encore necessaire; ne pas confondre tests EditMode et validation Play Mode.
Verification du nettoyage : compilation runtime/editeur reussie, validation
prefab/bindings reussie, 22/22 tests EditMode passes (dont les quatre clips et
leur reinstallation idempotente), rapport Library/EnemyMotionTests.xml.

## Attaques engagees et contre QTE

Une attaque ennemie engagee ignore Hit/Stagger. Un evenement ennemi QTE(A/B/X/Y)
ouvre le panneau existant (0.5 s reelle, temps global 0.4 configurable). La reussite
lance uniquement le CounterSkill cinematique equipe; un echec laisse le coup continuer.
Mort et LightSkill restent prioritaires. Un palier Pending attend la fin du coup
et empeche le coup suivant du combo. Countered retire du controller Juggernaut,
mais son clip cinematique conserve. Compilation runtime reussie et 18 tests
EditMode passes, dont PendingThresholdDoesNotSuspendCommittedAttack.
Validation Play Mode encore necessaire; les boutons QTE des clips restent auteur.

## Objectif actuel

Utiliser `AIAgent` comme espace documentaire leger pour preparer des prompts
Codex efficaces et economes en tokens.

## Etat actuel

Atterrissage joueur : verrou horizontal et rotation dans le cycle UCC tant
que Jump_End/Landing/Landing_Hard restent visibles et que le joueur touche
le sol. Restitution des inputs a la sortie, liberation en cinematique ou
desactivation. Script, impulsion et trajectoire du saut inchanges.
Compilation Unity et test Play Mode reussis : landing normal/fort immobiles
malgre inertie et direction maintenue, rotation bloquee et inputs restitues.

Orientation retour : le controleur de locomotion regarde le point de retour
pendant la pause, puis le prochain virage NavMesh durant le trajet. Yaw NavMesh
desactive pour eviter deux rotations concurrentes. Arret/reengagement nettoient
cette consigne ; orientation de spawn conservee a l'arrivee. Compilation reussie,
test de nettoyage ajoute ; verification visuelle en jeu restante.

Reengagement au retour : vision normale reautorisee pendant retour/pause,
ou proximite 360 degres a 6 m (returnReengageDistance) avec obstruction testee.
Le joueur doit rentrer a 0.5 m a l'interieur de la zone de poursuite pendant le
retour. Arret du chemin de retour et decision neuve ; aucune attaque gratuite.
Compilation reussie, test de proximite arriere ajoute, validation en jeu restante.

Sortie de combat par fuite : decision de desengagement avant EnsureReady NavMesh.
Le retour physique peut attendre le NavMesh sans garder le combat actif.
SetInputActive(false) libere la garde et la suppression d'interactions de son
proprietaire ; EndCombat restaure Exploration et rescane les interactions.
Compilation C# reussie ; regression ajoutee pour retour avec ennemi vivant et
navigation indisponible. Validation en jeu echelles/items apres fuite a effectuer.

Les 92 ressources d'animation utilisees par Lucian (clips et modeles FBX)
sont regroupees dans `Assets/Characters/1_Squad/Lucian/Animation`. Les GUID,
identifiants de sous-clips et contenus sont conserves ; les references partagees
restent valides. Les chemins Editor et le manifeste suivent ce rangement.

2026-09-06 : migration gameplay Lucian InPlace appliquee dans Unity : 57 clips
remplaces (17 copies dediees, 40 equivalents fournisseur), 33 profils d'etat
mesures sur Lucian et appliques dans le cycle UCC avant collisions. Les clips
sources, le saut complet (hauteur cible 100), les reglages d'esquive et les
assets InPlace preexistants sont proteges par empreintes. Le Root gameplay
du bridge et l'ancien enum joueur sont retires ; le relay cinematique reste
borne par son jeton. Les compagnons partages conservent leurs reglages utiles.
Audit/prevalidation puis application explicite, migration idempotente,
installateur sans reecriture inutile et validateur avant build ajoutes.
Compilation Unity reussie ; 11 tests Editor passes. Le parcours physique UCC
sur sol libre et contre un mur passe, ainsi que le retour InPlace apres session
cinematique. Annulation, desactivation et interruption cinematique liberent
leur verrou dans les essais avec et sans Domain Reload. Rapports :
`Library/PlayerInPlaceTests.xml`, `Library/PlayerInPlaceRuntimeTests.xml` et
`Library/PlayerInPlaceRuntimeReloadTests.xml`.
L'outil Editor preexistant `MainMenuMissingPrefabRepair` tente d'ouvrir une
scene lors du Domain Reload en Play Mode ; cet effet est isole dans le montage
du test, sans changer cet outil. Les references deja manquantes du controller
(motion LucianAligned, destinations 812330941234567102/105 et masque de la
couche Flame) restent intactes ; aucune destination n'a ete inventee.
La recette visuelle complete (huit directions, contacts, toutes les actions,
sauts compares en scene, ralenti, cinematiques Timeline et host/client) reste
a effectuer ; les tests automatises ne valent pas validation de ces parcours.

2026-09-06 : rotation Juggernaut 720 deg/s dans le profil. Cadence locale a
CombatLocomotion, reference marche/course 1.8/3.6 m/s, plafond 1.35,
hysteresis activite .08/.03 m/s et retour Idle pendant un fondu entrant.
Commande Update Juggernaut Locomotion sans reinstallation des patterns.
Axes de diagnostic via Lit/Combat/Toggle Selected Actor Facing Axes.
Pas de correction d'offset sans preuve ; calibration visuelle des appuis et
dix cycles District 1 non encore valides. Compilation C# reussie.
14 tests Edit Mode passes (Library/EnemyMotionTests.xml), dont cadence et
hysteresis. Mise a jour controller appliquee dans Unity. Capture UI indisponible
(SetIsBorderRequired 0x80004002) : aucun resultat visuel Play Mode revendique.

Juggernaut InPlace : mode ScriptedOnly, clips dedies neutralises, avancees
scriptes Frappe/Followup et preference approche melee. Les composants
RealTimeCombatEnemyBehaviour et EnemyTacticalResponseController sont retires
du prefab par l'installateur unique. L'ancien installateur automatique et ses
branches de reactions sont supprimes ; les menus historiques deleguent au
nouvel installateur. Validation assets stricte, y compris legacy desactive.
Les classes partagees restent disponibles pour les autres ennemis.
Compilation C# avec references Unity reussie ; combat District 1 reste a valider.

Poursuite animee : portee evaluee avant observation, approche/recul dedies selon
SkillSO, suivi cible 0.15 s/0.5 m, course a 8 m et marche a 6 m. Chemins incomplets
refuses ; cooldown/orientation a portee ne declenchent plus une avance. Tests
Editor etendus pour les marges de portee et l'hysteresis des allures.

Correction aggro Juggernaut : attente au spawn distincte du retour, retour termine
rearmant la vision, agent deja rattache accepte avant le delai de retry, skills
resolus avant reservation et aucun ajout de reaction legacy par le manager pour
un acteur avec EnemyCombatBrain. Vision 30 m, poursuite Juggernaut 35 m ; sols
Default inclus de nouveau. Compilation C# Roslyn avec references Unity reussie.
Tests Editor EnemyAggroRegressionTests ajoutes, execution Unity et combat District 1
restent a valider.

MainMenu dispose maintenant d'un salon coopératif privé Relay/NGO, de réservations
serveur de personnages, d'une navigation directionnelle clavier/manette et de
réglages persistants. La manette sélectionne les boutons avec un highlight,
sans curseur ; le pointeur reste réservé à la souris. La reprise solo passe
uniquement par Charger, sans bouton Continuer. OK ferme le clavier virtuel
et sélectionne Confirmer avant un second appui de validation. Les échecs réseau reviennent au menu, les écritures de
métadonnées sont atomiques et les miniatures sont mises en cache. La compilation
C# et les tests indépendants des règles passent ; les parcours visuels et Relay
avec plusieurs joueurs restent à valider dans Unity. Voir `systems/runtime_session.md`.

`AIStudio_Legacy` a ete supprime. `AIAgent` contient un dossier `Codex/` avec
les regles de travail, l'etat courant et les fiches systeme utiles. Les anciennes
fiches systeme Markdown ont ete migrees depuis `AIStudio_Legacy/docs/systems/`.

Le nouveau flux ne depend plus d'une application Python, d'un environnement
virtuel ou d'un appel LLM local. Les prompts sont prepares manuellement depuis
`AIAgent/prompts/codex_task.md`.

La premiere boucle beta commence a la Maison puis emprunte `Portal_1` vers ses
quatre `DestinationPoints` dans la chambre de Maelle (index 0 : personnage
principal). Les `ZoneSpawnPoint` sont reserves au spawn initial de la Maison ;
les portails de changement de scene memorisent leurs points avant le dechargement
de la zone source et les appliquent apres le chargement. Les knowledges de Jon sont partages
par la session : le collier les revele a la recuperation, la petite tombe a la
lecture, puis Luc se resout apres les deux indices. `SceneMarker` est le marker
unique (Character, Item ou Ghost) ; son `Bake in Scene` fige les items et
fantomes directement dans la scene et peut placer un personnage déjà actif en
solo. En session reseau, la copie baked de personnage est masquee et le spawn
Netcode reste autoritaire. `ItemSceneMarker` reste seulement une
compatibilite migrable. En session reseau, la Maison reste chargee afin qu'un
joueur tardif y apparaisse toujours.
Les ennemis conservent leur vie et leur état actif entre les sessions, mais leur
pose est toujours réinitialisée à leur `SceneMarker` au lancement.

L'exploration UCC a maintenant un contrat d'autorité explicite : UCC possède le
moteur, saut, gravité, collisions et caméra ; les adaptations Lit se limitent à
l'input et à la présentation. Le saut de Lucian n'a plus de force ni de
freinage alternatif : sa gravité UCC est seulement réduite de façon bornée
autour de l'apex et pendant la descente. La caméra Adventure retourne directement
la pose et la collision calculées par UCC : aucun filtre Lit de position ne peut
la faire retarder ou rattraper le personnage. Le binder ne recentre pas une liaison
déjà valide. La descente visuelle de Lucian emploie le clip `Falling_Loop` du
prototype archivé uniquement comme ressource d'animation : la transition ne se
fait qu'après une vitesse verticale négative et n'active aucun système de chute
libre. Le menu
`Lit/UCC/Create Exploration Test Course` génère le parcours de validation.

Les contours d'interaction des fantômes suivent au minimum leur portée
d'interaction effective, collider et ancre compris. Un `RuntimeOutlineTarget`
correctement posé sur leur modèle est donc visible dès que le fantôme devient une
cible interactive valide.

Les confirmations de ramassage sont des toasts non bloquants. De même, une
stèle affiche son texte entre guillemets dans le DialoguePanel sans quitter le
mode Exploration : ces deux messages disparaissent automatiquement après deux
secondes et ne doivent jamais immobiliser le personnage.
Un objet interactif qui exige une influence de Flamme mais se trouve hors de
toute zone allumee (uniquement un `InteractableItem` de categorie `RecoverableItem` ou un document `Stab`) demande au `VFXManager` de
`GameplaySessionRoot` un signal visuel commun. Le manager maintient une seule
instance de VFX boucle par objet, sans cadence ni recreation. Quand une Flame
s'allume, toutes les emissions de ses `ParticleSystem` enfants fondent vers
zero; quand elle s'eteint, elles reviennent depuis zero a leur valeur initiale.
Le root du VFX est echelle depuis la plus grande dimension visuelle de l'objet,
sans jamais inclure le VFX lui-meme dans ce calcul.
Ils déclenchent aussi la relecture immédiate du stick ou des touches maintenus,
pour qu'aucun saut ou nouvel appui ne soit requis après une interaction.

Le Juggernaut utilise maintenant des patterns ordonnes de SkillSO depuis le
profil de son CharacterData : frappe, combo, balayage et Assomoir. Il engage
apres degat, conserve le maximum lumineux recu, verrouille la direction des
coups et attend la recuperation physique avant de reprendre. Juggernaut_Model
est une copie specialisee de Player_Model. Les QTE suspendent cette boucle via
EnemyCinematicState. La validation des assets et le premier cycle Assomoir
passent ; la boucle repetee a expose une destination NavMesh nulle apres la
reprise. Le controleur preserve maintenant une projection voisine distincte du
point courant ; le test repetitif doit etre rejoue apres la prochaine
compilation Unity. Les parcours district, QTE complet et host/client restent a valider.
Le diagnostic runtime a ensuite montre que le district a `Y=-94` etait exclu
des bounds NavMesh par des colliders persistants hors gameplay. `SquadAIManager`
ignore maintenant les scenes `DontDestroyOnLoad`, `Bootstrap` et `MainMenu`
lors du calcul des bounds, et l'agent du prefab Juggernaut est active par defaut.

Les choix de réaction des fantômes utilisent exclusivement le
`GhostReactionChoicePanel` authored dans `ApplicationRoot/UI_Overlay` : nom du
fantôme, question, conteneur `Options` et boutons `Choice_1` / `Choice_2` sont
réemployés. Le panneau ou son canvas ne sont jamais créés en runtime; seuls les
boutons supplémentaires indispensables à une liste plus longue le sont. Sa
visibilité passe uniquement par le `CanvasGroup` (alpha, raycasts et
interactivité), pas par l'activation de son GameObject.

Le bake des `CombatSkillTimelineAuthoringRig` valide les pistes, les bindings
et les caméras de prévisualisation, mais n'impose plus l'Animation Event
`ResolveCinematicSkillImpact`. Les paliers QTE sont exclus de ce flux : ils
utilisent un `ThresholdSequence` Animator-driven et ne créent aucun rig ni
Timeline runtime.
Une `ThresholdSequence` est strictement composee de `steps` complets : chaque
step possede son clip QTE (un unique Animation Event `QTE(Y/B/A/X)`), duree,
ralenti, watchdog, fondus, clip de succes, resultat et politique d'echec. Les
succes intermediaires enchainent automatiquement le step suivant; seul le
resultat du dernier step resout le palier. Il n'existe plus de montage legacy.
La pose prealable est authorisee par ennemi dans `CharacterData`; le
`CombatHealthThresholdController` ne contient que l'orchestration runtime.
La pose préalable des paliers QTE résout maintenant le volume réel du joueur
depuis un `CapsuleCollider`, puis un `CharacterController`; si UCC diffère la
création de ces composants, un volume de secours conservateur permet de tenter
la pose, dont le test d'obstacle reste obligatoire.

`TimeManager` est l'unique ecrivain de `Time.timeScale` et
`Time.fixedDeltaTime`; l'audit `Lit/Time/Audit Global Time Authority` et les
builds refusent toute ecriture ailleurs. Les QTE de palier demandent un handle
global de `0,4` uniquement durant la saisie, alors que leur compte a rebours et
l'UI restent en temps reel. Le pitch audio dependant du temps lit le scale
publie par `TimeManager`, jamais Unity directement.
Le meme `TimeManager` pilote aussi la presentation du ralenti : au premier
passage sous la vitesse normale (hors pause a zero), il declenche une onde
ecran et un SFX d'entree; un volume HDRP temporaire applique vignette et
aberration chromatique pendant le ralenti, puis un second SFX joue au retour a
la normale. Les clips et les intensites se reglent dans la section **Slow
Motion Presentation** du composant `TimeManager` sur `ApplicationRoot`.
Cette onde de ralenti est volontairement une unique deformation large et lente,
sans liseré lumineux; elle est distincte des ondes courtes, rapides et
localisees configurees dans chaque `SkillSO` d'impact. Elle est provisoirement
desactivee par `cameraEffectsEnabled` dans le prefab; les SFX de transition
restent actifs.

Netcode for GameObjects 2.11.2 est embarqué dans `Packages/` pour rendre son
teardown idempotent. Un `NetworkManager` arrêté, ou jamais démarré, ne relance
plus le nettoyage de ses services Spawn/Scene déjà libérés à la fermeture de
Play Mode ou de l'application. Le système de temps réseau mémorise aussi son
abonnement au tick : il ne relit plus un `ConnectionManager` déjà nettoyé à la
fermeture et sa référence est libérée après l'arrêt.

Le prototype de chute libre est archive dans `Assets/FallingPhase_Legacy/` avec
sa scene, son Animator, son grappin, ses scripts et son manifest. Il ne fait plus
partie des scenes de build; l'ActionMap partagee `Falling` est conservee afin que
l'archive reste testable manuellement.

`CharacterData` est maintenant une definition d'auteur immuable : son unique
prefab est `WorldPrefab`. Les donnees de session (inventaire, flamme, trois
objets combat, usure des boucliers et charges Munin) vivent dans
`CharacterRuntimeState`, possede par `SquadManager` et indexe par personnage.
`CharacterStateStore` conserve le format de sauvegarde existant et hydrate ce
cache avant le spawn; les compagnons non instancies gardent donc leurs donnees.
`CharacterInfo` est le point d'accès commun au `CharacterData` des personnages
et ennemis de scène; l'ancien `EnemyInfo` a été retiré. `CombatHealth`
initialise son maximum depuis ce `CharacterData` avant de remplir une vie
initialement vide.
Les ennemis issus d'un `SceneMarker` restent enfants du marker tout en étant
positionnés en coordonnées monde; hors session Netcode, aucun `NetworkTransform`
n'est ajouté ou activé sur ce clone. En host/client, le clone est parenté par
`NetworkObject.TrySetParent` et son `NetworkTransform` bascule en espace local.
Un état persistant ennemi dont la position diverge de plus de 20 m du marker est
ignoré pour éviter de restaurer une position invalide d'une ancienne session.

Le chantier performance repart de zero. Les systemes de culling manuel,
budget de lumieres, budget de portails, XRay et instrumentation de migration
ont ete retires des scenes et prefabs. Les LOD et l'Occlusion Culling natif
Unity de `Maison` sont conserves. La prochaine architecture sera fondee sur
`Maison` comme hub permanent et sur les environnements `Castle` et `Arena`
charges progressivement en additif.

Le combat temps reel est actif dans `GameplaySessionRoot` et sur
`Juggernaut_Combat`. Il porte les definitions d'attaques, le loadout de huit
slots, le ledger de lumiere des ennemis, les fenetres de reaction pilotees par
Animation Events, le lock camera, les inputs dedies et les vues HUD/loadout.
La locomotion continue de Lucian sous lock utilise maintenant les clips
`Twinblades_Strafe_*_Inplace`. UCC est l'unique proprietaire de la translation,
des collisions, des pentes et de l'inertie; l'Animator affiche seulement les
huit directions et le tier marche/course. Les actions engagees restent libres
d'utiliser leur root motion ou leur mouvement scripté configure.
Le strafe gauche/droite memorise son rayon autour de `EnemyLockPoint` et le
stabilise avec une faible intention radiale UCC. Il ne depend jamais du root
motion et les collisions gameplay conservent la priorite.
Le verrouillage est entierement manuel : `LeftShoulder` verrouille l'ennemi le
plus proche a portee, puis le deverrouille au prochain appui. Les portees de
lock et de deverrouillage augmentent avec la plus grande composante de scale de
l'ennemi. Une step finale `LaunchCombat` de `StorySequenceAsset` peut lancer
le combat a la fin reelle de la sequence : lock de l'ennemi le plus grand puis
le plus proche, musique combat et degats de lumiere `openingCombatDamage` (50
par defaut). Un orbe lumineux pulse et un signal sonore sont joues sur
`EnemyLockPoint`; la croix directionnelle gauche bascule entre les ennemis
visibles verrouillables.
Chaque lock reapplique le profil d'ActionMaps `Combat` au singleton
`LocalPlayerInput`, ce qui garantit l'activation de `RealTimeCombat` apres un
reset UI ou scene. `RealTimeCombatInput` journalise egalement les refus de
BasicAttack afin d'identifier sans ambiguite une map inactive, une roue ouverte
ou un skill indisponible.
`GameplayRuntimeReset` purge aussi le contexte gamepad, les suppressions
locales et la map combat : une session de test ne peut plus heriter d'un
verrou d'input invisible de la precedente.
La restitution d'une UI, d'une action combat ou d'une LightSkill relit
desormais l'etat reel du stick et de `RightShoulder` apres le retour de map :
un input maintenu pendant `Cinematic` ne laisse plus Lucian immobile jusqu'a
une nouvelle pression. Cette reinjection repasse par `SquadManager` et le
bridge UCC normaux. Le bridge ajoute aussi une faible impulsion UCC plafonnee
au premier pas de course, y compris apres une action, pour rendre la reprise
plus nette sans contourner les collisions.
Quand aucun ennemi n'est verrouillable, `LeftShoulder` ne declenche aucune UI
ni focus gameplay : il reste reserve au lock/unlock et ne peut donc plus ouvrir
le panneau d'escouade ou interrompre le mouvement.
Le verrouillage combat affiche aussi un contour rouge HDRP autonome sur les
renderers de l'ennemi. Il utilise la layer `CombatOutline` et la passe
`CombatLockOutlinePass` de `GameplaySessionRoot`, sans partager l'etat ou la
couleur des contours bleus d'interactables.
Sous lock, le mouvement de Lucian est calcule une seule fois dans
`LitOpsiveLocomotionBridge`, dans le repere de `EnemyLockPoint` (ou du root
ennemi). Avant et arriere approchent ou eloignent; gauche et droite memorisent
leur rayon initial et suivent une tangente corrigee par une faible intention
radiale UCC. Les diagonales ne verrouillent pas ce rayon et conservent donc leur
arc d'approche ou de recul. Lucian reste face a la cible; seules
roulade et saut peuvent prendre une orientation d'evasion temporaire.
Le blend tree combat de `Player_Model` contient un echantillon idle neutre afin
qu'un lock sans Move ne joue plus une animation de marche root.
Pendant ce lock, la camera conserve un evitement des obstacles visuels par
SphereCast, configurable sur `CombatLockOnCameraController`, sans laisser les
drivers UCC reprendre le cadrage de cible.
Hors lock, la vue UCC `Adventure` utilise un offset rapproche (`z = -1.9`);
le lock combat applique un offset distinct plus recule (`z = -6.5`) et un FOV
de 66, exposes sur `CombatLockOnCameraController` du `GameplaySessionRoot`.
La Main Camera et ses trois drivers UCC (`CameraController`, handler et binder)
sont references explicitement dans `GameplaySessionRoot`; le lock les maintient
desactives a chaque frame jusqu'au deverrouillage.
Le lock garantit aussi un cadrage complet de Lucian : il augmente son FOV jusqu'a
une limite reglable puis recule si necessaire. Son orbite suit la direction
joueur-ennemi avec un lissage distinct et une vitesse angulaire maximale afin
que les demi-tours et les deplacements root des BasicSkills restent fluides.
Les vitesses finales de position, rotation
et FOV sont aussi bornees pour absorber les changements de cadrage pres des
obstacles.
Exploration et combat restent dans le meme espace : aucune transition, vague,
BattleSphere, teleportation ou arene n'est activee.
Le combat tour par tour actif a ete retire : `RealTimeCombatSceneUiController`,
place sur `UI_Overlay`, pilote les panels existants de `Bootstrap` et `Arena` (`CombatEngagedPanel`,
`CombatScreenInfosPanel`, `VictoryPanel`, `DefeatPanel`) sans creation UI
runtime. `CombatDefensePanel`, l'ActionMap `Combat`, les transitions d'arene,
la camera de phase et les composants historiques ne font plus partie du
gameplay. `Assets/Legacy/BattleManager_SymphonieImport` reste une archive
intacte et hors compilation/gameplay actif.
Le premier scenario jouable est configure : Lucian dispose de `Lueur faible`
et `Lueur intense` dans les slots 1 et 2, tandis que le Juggernaut joue
`Skill_Juggernaut_Assomoir` via `EnemySkills` et ouvre sa fenetre d'esquive par
Animation Events. Son impact melee est configure entre 0 et 5 metres.
Le Juggernaut utilise desormais un `CombatEnemyRuntimeContract` sur le root de
son `WorldPrefab`. Ce contrat exige l'Animator de combat, `RealTimeCombatEnemy`,
`EnemySkills`, `CombatEnemyPhysicsMotor`, `Rigidbody` cinematique,
`CapsuleCollider` et `NavMeshAgent`. `SceneMarker` verifie le clone juste apres
son instanciation et coupe explicitement son IA/combat si le prefab est incomplet,
au lieu de laisser Assomoir demarrer sans moteur physique. Le menu
`Lit/Combat/Repair Juggernaut Runtime Contract` repare le prefab auteur et la
reference `Juggernaut.asset > WorldPrefab`; `Validate Juggernaut Combat AI`
permet de verifier le resultat avant un test.
Le prototype temps reel distingue maintenant garde, esquive et contre. `NorthButton`
maintenu active une garde qui reduit les degats recus de 60 % par defaut; une
nouvelle pression dans une fenetre Animation Event acceptant `Counter` fige le
temps, suspend l'attaque ennemie et lance le `defaultCounterSkill` configure.
Un contre joue une Timeline non scale liee dynamiquement a Lucian,
l'ennemi verrouille et une Virtual Camera, puis son Animation Event applique les
degats propres au `CounterSkillSO`. `EastButton` est l'unique esquive root avec
i-frames; le dash combat precedent a ete retire de ce mapping.
Le maintien de North joue `Guard_Block` depuis le pack Super Fast Fighting,
avec `Twinblades_Defense_Hit_Root` comme fallback tant que la state n'a pas ete
installee dans `Player_Model`. Relacher North rend l'Idle de combat; seule une
pression commencant dans une fenetre qui accepte `Counter` declenche la parade.
`CombatWarningOn` et `CombatWarningOff`, poses sur les clips ennemis, pilotent
une alerte HDRP locale et une surcharge de focus temporaire vers
`EnemyLockPoint`. `OpenReactionWindow(float)` ouvre la fenetre en temps reel
non scale sans jamais resoudre les degats: impact et fin restent les Animation
Events existants.
Les actions combat orientent Lucian vers `EnemyLockPoint`. L'esquive fait
exception : une direction explicite du stick a priorite, Lucian s'oriente alors
dans cette direction et y roule; sans direction, il reste face a l'ennemi et
effectue une roulade arriere.
`Saut de rupture` est equipe dans le quatrieme slot de Lucian. Son `SkillSO`
utilise `Skill_3_JumpKick`, refuse de demarrer hors de la plage 0-3.25 m et
declenche `ResolveSkillImpactAndRetreat` au contact : degats 15, stagger ennemi,
VFX/Audio d'impact, onde monde et ScreenWave locale cyan. L'impulsion UCC
projette ensuite Lucian a l'oppose de `EnemyLockPoint`; ses controles restent
verrouilles jusqu'au prochain atterrissage, avec une borne de secours de 3.5 s.
Son profil conserve un elan horizontal decroissant pendant `0.6 s`, puis 35 %
de sa vitesse initiale jusqu'a l'atterrissage, pour eviter un arret en plein air.
Les Skills 1 a 4 et les BasicSkills partagent maintenant un
`CombatImpactFeedbackProfile` directement configure dans chaque `SkillSO`.
Apres un hit effectivement applique, `CombatImpactFeedbackController` du
`BattleManager` joue le hit-stop non scale, la ScreenWave locale, les cues
optionnels et une impulsion amortie du cadrage UCC. `CombatLockOnCameraController`
attenue et borne globalement chaque impulsion, puis remplace progressivement la
precedente : un combo ne peut plus cumuler des deplacements de camera. Les BasicSkills sont regles
a 5, 10 et 15 degats; leur epee reste visible entre deux coups bufferises puis
se masque a la sortie effective du combo. Les clips utilisent
`ResolveSkillImpact` pour les impacts standards et
`ResolveSkillImpactAndRetreat` pour le Saut de rupture.
La mobilite temps reel est centralisee par `CombatMobilityController` sur
`BattleManager`. En combat, `NorthButton` est reserve a la garde et au contre;
`EastButton` declenche l'esquive root directionnelle avec 0.05 s de preparation
et 0.18 s d'invulnerabilite; `SouthButton` declenche le saut UCC reel. Esquive
et saut continuent de valider les fenetres de reaction Animation Event. Une
seule commande de mobilite peut
etre bufferisee 0.12 s pendant une action. Chaque `PlayerActionPresentationProfile`
expose maintenant `mobilityCancelNormalizedTime` et `allowMobilityCancel`;
les BasicSkills 1, 2 et 3 ouvrent cette annulation a 0.35, 0.42 et 0.55.
Le lock garde le cadrage cible, mais Lucian se tourne vers son mouvement hors
d'action engagee.
Chaque impact ajoute aussi un micro-tremblement lateral et vertical, tres court
et amorti en temps non scale; son amplitude, sa duree et sa frequence sont
reglables globalement dans `GameplaySessionRoot`.
Le `LightSkillPanel` de `Bootstrap` est visible uniquement pendant
un combat temps reel. Sa jauge est alimentee par les degats effectivement
appliques par Lucian selon `Light Charge On Hit`. Les LightSkills sont ecrites
dans `AnimationLab`, puis exportees avec `Bake LightSkill` dans l'Inspector du
`LightSkillTimelineAuthoringRig`. Le bake refuse une Timeline invalide, demande
confirmation avant ecrasement et genere un package runtime fige dans le dossier
du `LightSkillSO` : prefab de rig et copie `Runtime` de la Timeline. Les
preview actors, Main Camera de preview, Brain et AudioListener restent dans
`AnimationLab`. Seules les Cinemachines utilisees et les objets marques
`LightSkillRuntimeExport` sont copies. Le package lie dynamiquement les tracks
`Player.Animator`, `Enemy.Animator`, `Cinemachine`, `Signals` et les tracks
d'objets exportes aux vrais acteurs au lancement. Les cameras declarent
optionnellement leur cible Player, Enemy ou EnemyLockPoint et leurs modules
Cinemachine dans AnimationLab; une camera sans cible est valide lorsqu'elle est
entierement cadree par la Timeline. Le bake les conserve a l'identique.
La Main Camera passe sous Cinemachine avant la premiere evaluation de
Timeline et la piste reste liee a la Brain de `CombatLockOnCameraController`,
jamais a une camera de preview. Un nouveau bake est requis apres
toute modification de Timeline, camera ou objet exporte.
`LightSkill_Devastation` est la base active; Furie a ete supprime integralement.
Le bake valide une Timeline temporaire, puis remplace la seule Timeline runtime
stable `<LightSkill>_Runtime.playable` et le prefab au meme chemin. En cas
d'echec le package precedent reste intact; apres succes, les anciens assets
`Runtime_Baked*` sont supprimes. Le bouton `Log Framing Audit` du rig auteur
capture les poses AnimationLab a `0 s`, `1 s` et aux changements de plan; le
rig runtime compare automatiquement Player, Enemy, camera, rotation et FOV aux
memes instants afin d'isoler tout decalage sans compenser le montage.
`Attach Framing Audit To Active Rig` permet d'ajouter ces seuls releves au
package runtime deja actif, sans rebaker ni modifier son montage.
Les LightSkills a pistes acteurs `ApplySceneOffsets` suspendent le relais de
root motion pendant la Timeline : elle reste l'unique proprietaire du
deplacement et evite une double application des deltas.
Le `PlayableDirector` baked est inerte (`Play On Awake` desactive,
`UnscaledGameTime`, `DirectorWrapMode.None`). Le rig est l'unique proprietaire
du lancement : placement, bindings, reconstruction du graphe, `Evaluate(0)`,
mise a jour manuelle de la Brain et verification stricte de la premiere
`CinemachineShot` precedent `Play()`. Une camera d'ouverture non stabilisee
refuse le lancement et restitue proprement la session; aucun lancement
approximatif n'est accepte.
Le bake compare maintenant un snapshot de chaque camera d'auteur (cle Timeline,
transform, FOV, priorite, Output Channel, targets, Follow offset et pipeline)
au prefab exporte. Une divergence, un script manquant ou une dependance a
`AnimationLab` refuse integralement le package : aucun module camera n'est plus
ajoute en runtime pour masquer un bake incomplet. Au lancement, le rig instancie
journalise son prefab, sa Timeline, la Brain de la camera explicitement exposee
par `CombatLockOnCameraController`, la vcam active et ses vraies cibles; il ne
consulte jamais `Camera.main` ni `AnimationLab`. La restitution est centralisee
et idempotente a la fin, a l'interruption, a la desactivation et au retour pool.
Le bridge Timeline remet explicitement l'autorite a `LitCameraDirector` sur la
Main Camera resolue par `CombatLockOnCameraController`; cette restitution coupe
la Brain Cinemachine et restaure les drivers UCC, sans arbitrage vers une autre
vcam.
Les pistes d'animation qui enregistrent une Cinemachine (transform ou Lens) sont
des pistes camera bakees : leur binding est copie vers l'Animator de la camera
exportee. Devastation relie ainsi `Animation Track` a `Camera_1` dans son
package runtime, au lieu de laisser `None (Animator)` dans la Timeline. Ces
pistes conservent exactement leur mode d'offset Timeline d'auteur dans le
package runtime : leurs poses restent fideles au montage de `AnimationLab` tout
en etant portees par le rig aligne sur Lucian et l'ennemi. `LitCameraDirector`
est l'unique proprietaire du handoff
UCC pendant une LightSkill; le lock suspend seulement son cadrage et ne coupe
plus les drivers UCC une seconde fois.
`Lucian_Anchor` et `Enemy_Anchor` deviennent les reperes canoniques de roots
dans AnimationLab. Le bake les exporte comme `PlayerStageAnchor` et
`EnemyStageAnchor`, puis refuse tout package qui ne contient pas ces deux
locators. Au runtime, le rig est pose au midpoint de Lucian et de l'ennemi,
aligne sur leur axe horizontal et les vrais roots sont poses directement sur les
anchors avant la premiere evaluation de Timeline. Il n'y a ni recherche de
plateau, ni deplacement peripherique, ni test de collision, NavMesh ou flash de
transition. La LightSkill joue toujours immediatement sur place selon le
midpoint et les anchors bakes. Les positions finales sont conservees.
Les pistes `Player.Animator` et `Enemy.Animator` sont converties au bake en
`ApplySceneOffsets`. Une LightSkill posee sur un plateau laisse alors Timeline
etre l'unique pilote des transforms acteurs pendant la lecture : le rig ne lit
ni ne repose UCC ou l'ennemi chaque frame. Cela evite toute boucle de
retroaction entre Timeline et UCC; le root motion reste relatif au plateau
runtime. Un package plus ancien est refuse et doit etre rebake. Les
CounterSkills, qui n'utilisent pas ce placement de plateau, conservent leur
chemin root motion existant.
Le contrat de plateau version 3 enregistre aussi l'orientation monde du rig
d'auteur; tout prefab LightSkill plus ancien doit etre rebake avant lecture.
Le contrat `CombatActorAnimationRoot` centralise maintenant le root gameplay,
son `AnimationRoot`, l'Animator de gameplay et `EnemyLockPoint` lorsqu'il
existe. Le combat, les skills, UCC et les Timelines resolvent cet Animator
explicitement au lieu de choisir un enfant. `Lit/Combat/Normalize Actor
Animation Hierarchies` encapsule sans deformer les skeletons importes les
Animators des deux ennemis sous un `AnimationRoot` identite, retire l'Animator
vide du Giant et rebranche les references de combat. Lucian conserve
provisoirement son Animator racine: ses clips generiques dependants des paths
ne doivent pas etre reparentes automatiquement. Son comportement passe neanmoins
par le meme contrat et le meme relais de root motion cinematographique.
En sortie, les poses locales imposees par les Animation Tracks sur les Animators
Player et Enemy sont remises a zero avant la remise en pool du rig : chaque
declenchement repart donc d'un etat propre sans reemployer l'offset precedent.
Chaque `LightSkillSO` expose aussi deux profils optionnels de state post-Timeline
(Lucian, ennemi : state, fondu, temps normalise). Ils ne sont appliques qu'a la
fin naturelle de la Timeline et ne peuvent jamais ecraser une mort. Devastation
laisse le profil joueur vide et enchaine l'ennemi vers la state `GetUp` du
Juggernaut, basee sur le clip `EnterTheBattle_1` en attendant un clip de releve
dedie.
Le rig poolé efface aussi tous ses bindings Timeline et references Cinemachine,
arrete son director au temps zero puis revient a une transform identite avant sa
prochaine acquisition.
Pendant la Timeline, l'auto-desengagement de combat est suspendu : l'IA cible
est volontairement inactive et ne doit jamais interrompre la sequence avant sa
fin.
La pose de Lucian passe par `SetCinematicPositionAndRotation` du bridge UCC :
elle reste donc autorisee pendant le verrou d'input de la Timeline, sans jamais
le liberer.
La Brain passe en `Cut` pendant une Timeline LightSkill, puis retrouve son
blend precedent a la restitution : aucun mélange UCC/Cinemachine ne modifie le
premier plan cinematographique.
Le lancement d'une LightSkill resout explicitement le bridge UCC de Lucian;
tout refus (references combat, portee, locomotion UCC, camera Cinemachine ou sequence deja active)
est affiche dans la Console et par un feedback world-space.
`Skill_2_Fleche de lumiere` restitue maintenant la locomotion a 78 % de son
clip, apres le tir, au lieu d'attendre sa quasi-totalite.
Quand les PV de Lucian atteignent zero, `PlayerActionPresentationController`
verrouille la state `Death` avant la resolution de la defaite. Les actions,
combos, hurts et retours automatiques vers la locomotion ne peuvent plus la
remplacer; le verrou ne disparait qu'au rechargement de la partie via
`Revivre`.
L'arc et l'epee restent pilotables par leurs Animation Events, mais une fin ou
interruption d'action notifie aussi `RealTimeCombatAnimationEvents`, qui les
range automatiquement. Cette securite evenementielle couvre les clips dont le
`HideBow` ou `HideSword` final n'est pas atteint.
La sortie des BasicSkills est aussi securisee par le controleur de presentation :
si une state d'attaque est perdue avant son seuil de recuperation, il force le
retour a `Locomotion` plutot que de laisser Lucian fige dans sa pose finale.
`RealTimeCombatEnemyBehaviour` fournit la poursuite reutilisable : la vision
reste normale en patrouille, puis passe a son `alertedVisionDistance` apres une
detection et conserve cette alerte pendant `alertMemorySeconds` sans ligne de
vue. Le Juggernaut utilise `24 m` et `12 s` par defaut. Le lock combat utilise
ce rayon alerte plutot que le rayon initial de 7 m. Cette detection seule ne
declenche pas la musique : elle commence seulement lorsque Lucian inflige son
premier degat de lumiere, puis reste active pendant l'alerte provoquee.
Quand un combat se ferme automatiquement, le manager attend le relachement du
stick puis purge l'input et la vitesse UCC residuels afin que Lucian ne continue
pas a marcher dans une direction obsolete.
Un ennemi alerte regarde Lucian, mais ne le poursuit pour convertir son ledger
en riposte qu'apres avoir recu une attaque de lumiere. Apres une perte de vue,
il inspecte une fois sa derniere position connue pendant une courte duree, sans
utiliser la position reelle du joueur hors ligne de vue, puis retourne a son
point de patrouille. Son inspecteur expose une preference melee en pourcentage :
quand les deux types sont disponibles, il choisit d'abord melee ou distance
selon ce tirage, puis s'approche jusqu'a la portee correspondante. Le Juggernaut
porte actuellement le seul skill melee `Skill_Juggernaut_Assomoir`. Le
GiantJuggernaut utilise `Skill_GiantJuggernaut_Jump` via le meme flux : son
skill est utilisable de 0 a 30 m, sa state Animator est
`GiantJuggernaut_Jump` et son clip clot la riposte avec `EndEnemyAttack`. Son
clip n'est pas boucle. Les degats de lumiere recus pendant une attaque active
sont conserves dans un ledger suivant : a `EndEnemyAttack`, seul leur maximum
prepare une nouvelle riposte apres le delai normal, sans interrompre ni
relancer immediatement l'attaque en cours.
`RealTimeCombatEnemy` resout toujours l'Animator de gameplay portant un
Controller, avec priorite a celui configure dans `EnemySkills`; le
GiantJuggernaut utilise explicitement son Animator `MidPoly` pour ses hits et
son retour a `Idle`. Sa Timeline d'introduction ne deplace plus Lucian : il
reste a sa position d'entree jusqu'au lancement du combat.
Pendant son alerte, un ennemi provoque conserve Lucian comme cible de combat
meme si une animation root le tourne temporairement hors de son cone de vision :
il se retourne vers lui et peut consommer son ledger suivant tant que sa memoire
d'alerte reste active.
`AnimationGroundRecovery`, dans `Assets/Scripts/Animation/`, reste reserve a
Lucian. Les deux ennemis utilisent desormais `CombatEnemyPhysicsMotor` avec un
`Rigidbody` cinematique et une `CapsuleCollider` configures dans leurs prefabs :
il est l'unique autorite verticale pendant une attaque, ignore le root motion Y
des skills au sol et attend un contact sol avant de rendre l'IA et `Idle`.
Une sonde de sol ne peut corriger verticalement un `ActorRoot` que de `0.75 m`
au maximum : un collider d'un autre niveau du decor est journalise puis ignore.
Son audit de pose journalise temporairement spawn, hit, NavMesh, Rigidbody,
parent, Animator et synchronisation Netcode ainsi que tout saut superieur a
0.5 m, afin d'identifier une ecriture de transform concurrente.
Les skills ennemis portent un profil `Grounded` ou `Airborne`; `BeginEnemyAirborne`
et `RequestEnemyLanding` ouvrent et ferment la trajectoire balistique. Une mort
ou une interruption force d'abord une chute controlee, ce qui empeche un ennemi
de rester suspendu.
`RealTimeCombatEnemyBehaviour.Can Pursue Player` est actif par defaut. S'il est
desactive, l'ennemi reste immobile pendant son alerte, conserve son regard sur
Lucian et n'attaque que si la portee du skill choisi est deja respectee.
Tant qu'au moins un ennemi est dans ce mode, le manager applique l'override de
musique combat; il le relache apres le desengagement, la mort ou la fin de
l'affrontement.
Chaque degat du prototype temps reel produit aussi un nombre world-space local,
attache au combattant touche : cyan pour la lumiere recue par l'ennemi, rouge
pale pour Lucian. Il est billboard vers la camera, pulse, monte legerement et
s'efface en temps non scale sans prefab ni Canvas de scene.
Un impact ennemi non letal effectivement applique joue aussi la state joueur
`Base Layer.RealTimeCombat_RootMotion.TwinSword_Defense_Hit_Root`, configurable
sur `RealTimeCombatManager`, puis rend la locomotion a Lucian a la fin du clip.
Un coup letal conserve exclusivement la state `Death`.
Quand les PV de Lucian atteignent zero, UCC bloque normalement sa locomotion et
le manager ferme le combat; les skills de roue et attaques de loadout refusent
alors aussi toute nouvelle execution. La state `Base Layer.Death` est jouee
avant l'affichage du `DefeatPanel` de scene, adapte temporairement aux choix
`Revivre` et `Quitter le jeu`. Revivre recharge la scene du dernier checkpoint
de la sauvegarde active et supprime l'autosave de mort juste avant ce chargement.
`Revivre` recoit le focus manette initial; haut/bas navigue entre les deux choix,
avec une mise en avant visuelle interpolee du bouton selectionne et une
attenuation des autres choix. La croix directionnelle et le bouton Sud de la
manette pilotent directement cette navigation, meme avec un `EventSystem`.
Les `StatsSO` portent exclusivement les checks historiques. Les `SkillSO` de
combat portent nom, icone, clip, degats, une liste de VFX et une presentation
d'arme optionnelle, et sont listes dans
`CharacterData.combatSkills`. `SkillsManager`, place sur `SkillsPanel`, filtre les
competences de combat connues de Lucian et maintient jusqu'a huit competences equipees,
repercutees par evenement sur les `SkillWheelSlot` sans creation d'UI runtime
ni polling `Update`. Ce loadout n'est pas encore persiste dans les sauvegardes
personnage.
Tant que `LeftTrigger` est maintenu, l'alpha de `SkillsWheelSlots` passe de
`0` a `0.4` et le joystick droit survole le slot radial correspondant, avec une
echelle x1.5 et un alpha renforce; le relachement le ramene a `0`. `SouthButton`
valide la competence selectionnee et reste prioritaire sur le saut UCC tant que
le trigger est maintenu, sans bloquer la locomotion. A la fin du clip de skill,
le joueur revient a sa locomotion normale. Ce retour est borne par la duree du
clip du SkillSO, synchronise d'abord la pose finale root avec UCC, arrete les capacites UCC residuelles et le controleur personnage, puis declenche
`MoveStopTrigger` et reprend `Base Layer.Locomotion` avec ses parametres a zero,
sans ramener Lucian a sa pose initiale. La pose root finale est capturee apres
la derniere frame evaluee du clip puis reappliquee a UCC apres le `CrossFade`,
pour que les skills root comme `Skill_2_Fleche de lumiere` conservent leur
deplacement. Les attaques root du prototype appliquent
la meme synchronisation avant leur retour vers la locomotion. Les states taguees
`RealTimeCombatRootMotion` sont reconnues par UCC comme du root motion actif :
leur deplacement et leur rotation ne sont donc pas supprimes quand l'input est nul.
La validation oriente d'abord Lucian horizontalement vers `EnemyLockPoint` via
UCC, puis joue l'etat Animator explicitement configure sur le `SkillSO`
selectionne (avec fallback sur le nom du `AnimationClip`);
ses Animation Events restent responsables des VFX et degats. Les slots sans
SkillSO sont masques et exclus de la navigation de la roue. Les Skills et
BasicSkills de Lucian utilisent `UccBody` : la capsule conserve donc l'orientation
vers la cible apres le retour a locomotion, au lieu de ne tourner que le rig.
`SkillsManager` expose aussi une liste `BasicSkills` de `BasicSkillsSO`.
Cette preparation distingue maintenant les combos `GroundBasicSkills` et
`AirBasicSkills`. Chaque BasicSkill aerien peut, dans son Inspector, rester
en l'air ou demander une descente UCC a une seconde precise de l'animation,
sans teleporter Lucian au sol.
Pendant un lock, `WestButton` ajoute le prochain basic skill a un buffer de
combo : les clips sont joues dans l'ordre de la liste puis bouclent. Les Basic
Skills reutilisent les memes Animation Events de VFX et de hit que les skills
de la roue. Une pause de plus de `0.85 s` entre deux pressions reprend le combo
au premier basic skill. Une seule attaque peut etre bufferisee derriere celle
en cours; les pressions excedentaires sont ignorees. Chaque profil de
presentation expose une ouverture de chaine et une transition de chaine : une
BasicSkill bufferisee interrompt alors le clip courant a cette transition, sans
attendre sa recuperation. Les trois BasicSkills de Lucian ouvrent a `0.55`,
transitionnent entre `0.66` et `0.70` puis restituent la locomotion entre `0.68`
et `0.74`, avec un blend de sortie de `0.05 s`. `ToggleTorch` ignore
`WestButton` tant qu'une cible est verrouillee.
Chaque `SkillSO`, donc aussi chaque `BasicSkillsSO`, expose une distance
horizontale minimale et maximale de hit. `HitEnemy` ne blesse la cible qu'a la
frame de son Animation Event si Lucian est dans cette plage autour de
`EnemyLockPoint`. Les VFX `DirectOnTarget` et `Projectile` sont soumis a la
meme verification; les cues `PlayerHand` restent joues pour la presentation du

Les `SkillSO` peuvent maintenant choisir une Timeline optionnelle bakee depuis
`CombatSkillTimelineAuthoringRig`. Cette session s'execute sur place et utilise
le pool de `CombatCinematicRig` deja eprouve par les LightSkills. Elle suspend
le combat entier sans modifier le temps global; `ResolveCinematicSkillImpact`
reste l'unique impact valide. Les BasicSkills cinematographiques conservent leur
combo a la restitution, tandis que les EnemySkills finalisent leur ledger une
seule fois.
caster. Un hit refuse affiche aussi un feedback world-space `Raté (trop près)`
ou `Raté (trop loin)` sur l'ennemi.
La meme plage est appliquee a l'impact d'un skill ennemi, entre sa racine et
Lucian : hors plage, le joueur ne subit aucun degat.
`Skill_3_Entaille` utilise explicitement `Base Layer.Skill_3_Entaille` et porte
le tag `RealTimeCombatRootMotion`, afin que son retour a locomotion ne laisse
pas une intention UCC residuelle.
`Skill_2_Fleche de lumiere` cible explicitement sa state root plutot que le
fallback par nom de clip.
Les clips de skill peuvent aussi appeler `Dash`, qui propulse Lucian sur la
droite vers `EnemyLockPoint` avec un depassement reglable, puis `StopDash`, qui
freine cette impulsion sur une courte duree plutot que de l'annuler instantanement.
Les clips ennemis peuvent appeler `ShowInput(Sprite)` et `HideInput()` sur
`RealTimeCombatAnimationEvents` pour afficher puis retirer un prompt
world-space ancre au `EnemyLockPoint`. Le Sprite 2D est assigne directement
dans l'Animation Event, independamment de la fenetre logique de reaction.
Les clips d'attaque joueur peuvent maintenant declencher `InstantiateSkillVFX`
ou `InstantiateSkillVFXAtIndex` sur `RealTimeCombatAnimationEvents` : chaque
VFX du `SkillSO` peut apparaitre directement sur `EnemyLockPoint`, ou rester
sur la main tenant l'arc, ou rester sur le joueur puis devenir un projectile avec
ses delais configures. Chaque cue peut aussi jouer son `AudioClipSO` au point de
depart de sa presentation. `HitEnemy`
applique ensuite ses degats a l'ennemi locke
et son etat Animator `Hit` configurable est joue.
Le Juggernaut possede aussi `EnemySkills`, avec une liste de `SkillSO` a
assigner dans son Inspector, sans roue. Ses clips peuvent utiliser le meme
`RealTimeCombatAnimationEvents` avec `SetEnemySkill(int)`, `PlayEnemySkill(int)`,
`InstantiateEnemySkillVFX`, `InstantiateEnemySkillVFXAtIndex(int)` et
`HitPlayer` pour synchroniser animation, VFX et impact sur Lucian. La portee,
le multiplicateur et les reactions ennemies sont portes par le `SkillSO`; les
degats finaux restent calcules par le ledger de lumiere.
`HitPlayerIf("Grounded")` permet maintenant a un Animation Event ennemi de ne
toucher Lucian que s'il est au sol selon UCC. Les conditions sont des noms
extensibles; une condition inconnue est refusee avec un avertissement unique.
Une attaque ennemie active reste prioritaire sur son animation `Hit`, afin de
ne jamais interrompre le root motion d'un saut comme `Assomoir`.
Les ennemis temps reel ne dependent pas de transitions implicites de leur
Animator : un `Hit` suspend temporairement son root motion puis revient
doucement a la state `Idle` configuree, et `EndEnemyAttack` ramene aussi le
skill ennemi termine a cette state.
Le `Bow` deja attache a la main de Lucian est pilote strictement par les
Animation Events `ShowBow` et `HideBow` des clips; les VFX optionnels associes
sont configures sur le composant `PlayerBow` du modele Lucian.
L'epee `Sword` suit le meme flux via `PlayerSword` et les Animation Events
`ShowSword` et `HideSword`; elle est masquee par defaut et peut posseder ses
propres VFX optionnels d'apparition/disparition.
Une Ancient Flame proche d'un ennemi temps reel vivant devient bleue quand elle
est allumee, mais reste inerte : elle ne revele rien, n'active aucun objet et
ne compte pas pour la restauration temporelle avant la disparition de l'ennemi.

Le combat tour par tour existant pilote encore la camera locale par phase via
`CombatCameraPresentationController`; pendant le combat, Opsive est suspendu
comme driver camera spatial puis restaure a la sortie.
`CombatSessionManager` peut instancier un prefab public a mi-chemin entre le
joueur et l'ennemi au pic de la transition d'entree en combat; en reseau,
cette presentation est demandee a tous les clients et detruite a la sortie
manuelle du combat. L'entree combat est orchestree par `BattleTransition` sur le
`BattleManager` de `Maison`, mais la vague HDRP est maintenant un systeme dedie :
`ScreenWaveController` pilote le `CustomPassVolume` de scene `ScreenWavePass`
avec le material `MAT_ScreenWave`. Le bouton inspecteur `PlayScreenWave` permet
de tester une vague unique hors Play Mode depuis le `BattleManager`, sans
creation runtime. Le composant reste actif afin que son `Update` non scale
termine toujours le cycle et coupe le Custom Pass. La vague expose son origine,
sa direction, sa frequence, sa
vitesse de propagation, son amplitude, sa duree, son attenuation et un lisere
lumineux reglable pour rester lisible dans les scenes sombres, puis
`BattleTransition` la declenche au debut du combat pendant que BattleSphere, VFX
et shaders sont prechauffes au demarrage de scene. Quand le joueur rencontre un
ennemi, l'entree combat fige localement le temps pendant la premiere vague,
masque l'Outline monde actif pendant ce gel, effectue le placement combat a la
fin de cette vague, puis laisse le meme
Custom Pass enchaîner une deuxieme vague inversee pour revenir a un rendu normal
dans l'arene. Le bouton `PlayScreenWave` teste ce cycle complet hors Play Mode.
Le snapshot de retry pre-combat est aussi capture au pic de cette vague, avant tout
deplacement, pour eviter un gel visible avant l'effet. Ses validations
persistent en memoire sans etre loguees comme erreurs console.
Si le prefab contient un ou plusieurs `CharacterEffect`, ils sont joues a
l'apparition, stoppes a la sortie, puis detruits apres un delai par defaut de 2
secondes.
Chaque phase shot peut aussi ajouter une vitesse de deplacement locale a son
offset camera pour creer un drift cinematographique pendant la phase.
Le ralenti de combat est maintenant strictement pilote par les `AnimationEvent`
des clips via `CombatAnimationEvents`; la camera, le manager de combat et les
impacts ne declenchent plus de ralenti automatique.
Un composant `CombatAnimationEvents` permet aux clips d'attaque de declencher
un ralenti local, une ruee vers la victime et un retour a la pose initiale.
`Griffe` du Juggernaut est maintenant pilotee par son clip : le manager lance
seulement `Attack_Griffe`, sans saut/dash/audio/VFX specifiques codes.
Les clips peuvent notifier l'impact avec `NotifyCombatImpact`; le manager
applique alors l'impact pending une seule fois. Les impacts de combat attendent
ce `AnimationEvent` et ne sont plus resolus par timer fallback.
Le ralenti Animation Event descend maintenant a `0.1`, cible l'acteur et la
victime, et les anciens hooks legacy autonomes qui modifiaient `Time.timeScale`
ont ete retires.
Le joueur peut maintenant assigner hors combat jusqu'a 3 items defensifs comme
items combat. Les clips d'attaque ouvrent/ferment `CombatDefensePanel` via
`CombatAnimationEvents`; le panel ne propose que ces items a portee de main, et
la selection reste validee par `CombatSessionManager`, synchronisee par
`NetworkInventory` et sauvegardee avec l'etat personnage.
La fenetre defensive autorisee correspond maintenant a l'affichage reel de
`CombatDefensePanel` : tant que le panel est visible, le joueur peut remplacer
son choix, et seul le dernier item selectionne est resolu a l'impact avec un
surlignage, un tint de fond et un agrandissement du slot choisi. Les messages
positifs de choix defensif sont ajoutes au journal `CombatLog`; les erreurs
restent affichees dans `InfoBoxUI`.
Quand une attaque ennemie normale commence, le joueur local peut jouer une
animation de preparation defensive configurable et une voix `AudioClipSO`
optionnelle, avec fallback animation sur `Defense` puis `Block`.
Pendant toute la session de combat, le HUD garde l'ActionMap locale `Combat`
active, prend le focus exclusif et ferme l'inventaire s'il etait deja ouvert;
Au lancement de l'intro combat, il ferme aussi les UI monde deja ouvertes
(loot, pause, dialogue, InfoBox, confirmation, lecture, craft et construction)
afin de ne laisser visibles que les panels pilotes par le combat;
`UseItem1`, `UseItem2` et `UseItem3` activent les trois slots quand
`CombatDefensePanel` est visible. Le panel force aussi l'ActionMap `Combat` a
son affichage afin qu'un AnimationEvent ne laisse jamais NorthButton ouvrir
l'inventaire. Les enfants `Text` affichent le nom de l'item assigne, les slots
sans item restent masques, et l'affichage ne purge plus les items combat si
l'inventaire runtime n'est pas encore initialise;
l'initialisation des starter items conserve aussi ces assignations quand l'item
existe dans les objets de depart. Les anciennes sauvegardes `CharacterSaveData`
avant version 5 migrent les items combat manquants depuis les defaults du
personnage, puis les nouvelles sauvegardes persistent aussi les items avec
`CombatReactionProfile`. Les racines `EnableItem_1/2/3` sont aussi garanties
comme boutons UI pour la souris et la navigation manette/clavier.
Les items peuvent porter un `CombatReactionProfile` optionnel. `Item_Weapon_Sword`
est configure comme premier contre melee : il declenche `Counter_Sword`,
`Impaled`, un shot camera `CounterAction`, un court ralenti local, et peut
configurer ses attaches, SFX/VFX/voix depuis l'item. Les clips de contre peuvent
maintenant piloter le visuel via les AnimationEvents `Take` (apparition en main)
et `Release` (plantage sur l'ennemi), puis interrompre l'attaque ennemie via
`CounterHit` pour jouer `Impaled` et resoudre l'impact logique du contre au
meme frame, sans delai `impactDelaySeconds` cote manager. Si ce `CounterHit`
tue l'ennemi, `Impaled` est ignore et la resolution de victoire demarre sur
l'animation de mort.
`Item_Shield_WoodShield` utilise maintenant le type `MeleeDefense` : le joueur
sort son visuel en main, bloque les attaques melee, l'item perd des PV defensifs
persistes sur le personnage entre les combats, reste reutilisable s'il lui en
reste et casse sinon. L'inventaire separe les boucliers en piles par PV
restants identiques, par exemple une pile de boucliers pleins et des piles
distinctes pour les boucliers abimes.
Le profil peut aussi jouer un `enemyAnimationClip` direct via Playables, sans
state Animator par ennemi; `Impaled` reste le fallback par nom. Le Juggernaut
expose une state Animator `Impaled` vide pour y brancher le clip si besoin, et
le visuel plante oriente automatiquement son axe Z a l'inverse du Z local de
l'ennemi.
L'UI de combat joue maintenant `CombatEngagedPanel_Trigger` sur
`CombatEngagedPanel` des l'entree en session, sans attendre le premier snapshot,
affiche ensuite `CombatScreenInfosPanel`, puis masque ces infos quand un
AnimationEvent demande `CombatDefensePanel`; a la fermeture du panel, les infos
de combat reviennent.
Les demandes `CombatDefensePanel` issues des AnimationEvents ont priorite sur
l'intro de combat afin que la fenetre defensive s'ouvre meme si l'attaque
ennemie commence pendant `CombatEngagedPanel_Trigger`.
Les panels combat restaurent aussi leur hierarchie UI active et une echelle non
nulle afin de rester visibles si la racine `UI_Overlay` a ete sauvegardee a
`localScale` zero.
Les transitions d'entree et sortie du ralenti combat jouent des sons via
`ActionAudioCue.CombatTimeSlow` et `ActionAudioCue.CombatTimeResume`, relies a
des `AudioClipSO` de la banque de sons, et appliquent un leger ducking de la
musique pendant toute la duree du ralenti.
`RuntimePersistenceUtility` ignore les objets sous `NetworkObject`; les objets
reseau de scene restent donc dans leur hierarchie Netcode et ne sont plus
reparentes en `DontDestroyOnLoad` avant le demarrage host/server.
L'import legacy Symphonie est isole dans son assembly non auto-referencee,
Editor-only et compilee seulement avec le define manuel
`SYMPHONIE_IMPORT_COMPILE`; le combat actuel ne reference pas ses types ni ses
GUIDs.
La fin de combat n'est plus une sortie automatique immediatement apres la
resolution : le perdant joue sa mort, le gagnant tente un taunt (`Taunt`,
`Victory` ou `Celebrate`), et une victoire declenche le travelling camera
ralenti stylise des le coup fatal joueur, puis affiche le `VictoryPanel` apres
un delai sur : au moins 3 secondes apres ce coup, la fin de l'action fatale, la
mort ennemie et une petite marge. Les panels de scene `VictoryPanel` ou `DefeatPanel`
attendent ensuite une validation manuelle du joueur avant de restaurer les
positions, camera, mouvement et resultats monde.
En cas de defaite, la resolution remplace aussi la musique de combat par une
musique `Game Over` issue de `CombatAudioLibrary` jusqu'a la sortie manuelle.
Le `DefeatPanel` garde son texte de scene et expose maintenant trois boutons :
retour `MainMenu`, retry immediat du combat courant, ou retour au dernier
checkpoint/sauvegarde active. Les retours menu/checkpoint ignorent la prochaine
sauvegarde automatique pour ne pas persister l'etat de defaite.
Le retry capture maintenant un snapshot runtime pre-combat et le restaure avant
de relancer la session, afin de revenir aux inventaires, PV de bouclier, monde
persistant et PV joueur du debut de combat. Il remet aussi les Animator joueur
et ennemi a leur etat par defaut avant la nouvelle entree combat, y compris sur
le client proprietaire en reseau, pour eviter de conserver une mort ou un taunt
de la tentative precedente.

## Contraintes

- Le prototype de combat temps reel route les degats recus par Lucian via
  `SquadCharacterController.ApplyDamage`, afin de conserver la synchronisation
  de sante UCC, les retours de degats et la mort. Le degat ennemi est le plus
  grand degat de lumiere recu depuis sa precedente attaque : avec 10 PV,
  recevoir une riposte apres un skill a 10 ou 20 degats est volontairement
  letal et termine le combat.

- Garder `Codex/AGENTS.md` et `Codex/current_work.md` courts.
- Lire uniquement les fiches pertinentes de `Codex/systems/`.
- Ne pas recreer l'ancien pipeline AIStudio sans demande explicite.
- Ne pas stocker de secrets, caches ou environnements virtuels dans `AIAgent`.

La telegraphie de reaction temps reel est maintenant en trois temps et reste
exclusivement pilotee par les Animation Events ennemis
`BeginReactionTelegraph`, `OpenReactionWindow` et
`ResolveEnemyAttackImpact`. `CombatReactionTelegraphController`, sur
`BattleManager`, pilote le prompt world-space unique, les pulses
`AttackLightAlert`, les AudioClipSO et le micro-ralenti de fenetre parfaite.
Le menu Unity `Lit/Combat/Configure Reaction Telegraph` configure le prototype
Assomoir et cree le prompt de scene lorsqu'il est absent.
`LocalPlayerInput` detruit son `InputActionAsset` immediatement hors Play Mode,
afin qu'un dechargement de scene editeur ne laisse plus un GameObject
`LocalPlayerInput` residuel.
Le prompt de reaction reacquiert sa reference de scene avant chaque phase et
force son Canvas world-space sur la camera principale avec un ordre de rendu
eleve, afin de rester visible apres un changement de scene.

AnimationLab adopte maintenant le meme contrat que `Juggernaut_Combat` pour
son ennemi preview : `Enemy_Preview` porte l'Animator `Juggernaut.controller`,
le skeleton `MidPoly` reste visuel et les bakers enregistrent les poses des
`ActorRoot` plutot que celles d'un enfant Animator. Les rigs de Skills et de
LightSkills resolvent donc explicitement les roots preview avant de binder les
pistes `Player.Animator` et `Enemy.Animator`. Le menu
`Lit/Combat/Update AnimationLab Root Animators` resynchronise la scene et le
prefab AnimationLab avec ce contrat pour les futures migrations.

La locomotion combat est maintenant separee par autorite : le `NavMeshAgent`
deplace les ennemis hors action, `CombatEnemyLocomotionController` traduit sa
vitesse en strafe Root (sans lui ceder le deplacement monde), et
`CombatEnemyPhysicsMotor` possede les actions
engagees. `Skill_Juggernaut_Assomoir` est aerien : `BeginEnemyAirborne`,
`BeginEnemyRush`, `EndEnemyRush` et `RequestEnemyLanding` remplacent son ancien
deplacement ponctuel `DashToTarget`. La ruée suit la cible horizontalement,
s'arrete avant elle et ne peut pas ajouter de root motion concurrent.
Le lock ne remet plus a zero une intention de deplacement deja maintenue, et le
masque sol du Juggernaut couvre maintenant tout le decor afin que sa recuperation
physique ne puisse pas tomber sous le niveau.

Le Juggernaut poursuit maintenant un engagement apres le premier impact, meme
si la vision est brievement perdue. Sa decision alterne entre poursuite,
positionnement lateral, observation courte et attaque; les arrets NavMesh et
les sorties d'animation remontent vers `Idle` explicitement. Les diagnostics
optionnels du comportement et les validations `SceneMarker` signalent les
spawns hors NavMesh, sans appliquer de warp lointain.

Le spawn du Juggernaut relit maintenant `CharacterData.worldPrefab` a chaque
instanciation de `SceneMarker`: le registre Netcode ne peut plus reutiliser une
reference de prefab perimee entre deux essais editeur. Le contrat runtime
`CombatEnemyRuntimeContract` compare le prefab source et le clone (Animator,
combat, skills, physique et NavMesh), puis laisse l'IA en attente tant que le
NavMesh local n'est pas valide. Assomoir est refuse avant son lancement si son
moteur physique n'est pas operationnel; le watchdog finalise alors une attaque
incomplete une seule fois, sur le dernier sol confirme.

`SquadAIManager` reconstruit le NavMesh apres chaque chargement de scene et le
volume du `GameplaySessionRoot` calcule maintenant ses bounds depuis les
colliders charges. C'est indispensable pour les districts situes sous l'origine
du root persistant (notamment District_1 a `Y=-94`): sans cela, les ennemis
attendent legitimement un NavMesh qui ne peut pas exister sous leurs pieds.
Le Juggernaut demande egalement ce rebuild de maniere cadencee lorsqu'il ne
trouve aucune projection locale; `SquadAIManager` journalise alors le nombre de
sources et les bounds effectivement bakees afin de distinguer un chargement
retarde d'un decor absent des sources NavMesh.
Une demande explicite de rebuild est maintenant prioritaire sur
`autoUpdateNavMesh` : elle s'execute apres une frame complete de chargement,
publie colliders, layers retenus, sources et bounds, puis reveille les ennemis
en attente. Un bake a zero source retire la surface invalide et laisse l'IA
inactive avec un diagnostic exploitable. Les reactions tactiques du Juggernaut
exigent le meme etat runtime pret, afin qu'une garde isolee ne masque plus une
navigation absente.

L'orbe de Munin est maintenant visuellement independante de celle du chargement:
elle conserve ses materiaux HDRP, n'applique plus de transparence derivee du noir
et utilise des pulses de presentation relies aux evenements de charges. La
distorsion permanente est desactivee; elle ne joue qu'au court etat d'action.

Munin peut maintenant fusionner avec Lucian en exploration et en combat via
l'action `Melt`, sur un clic bref du stick droit. Le recentrage reste disponible
par maintien du stick droit (et `C` au clavier), tandis que le mode locomotion
est sur la croix droite. `Melt` declenche l'animation `Melt` hors fusion et
`Rupture` pendant une fusion; leurs AnimationEvents determinent le frame de
l'effet Holy et confirment l'etat visuel de Munin. Une LightSkill force cette
fusion, annule sans animation une
fusion precedente, puis defusionne automatiquement apres sa Timeline. Munin est
aussi masque tant que l'arc ou l'epee est manifeste, ou qu'une future arme porte
`SpiritWeaponManifestation`. Un AnimationEvent `InstantiateAtSpine` peut aussi
instancier un prefab configure sur l'os Spine de Lucian. Les triggers Animator
`Melt` et `Rupture` partent de `Any State` et reviennent a la locomotion en fin
de clip (sans passer par `Sit_Down`). `StopEffect_CharacterEffect` arrete proprement le graphe
VFX, sans masquer son GameObject. Holy est porte par `CC_Base_Body` pour rester cale sur son
`SkinnedMeshRenderer`; l'AnimationEvent historique `PlayEffect_CharacterEffect`
est relaye depuis la racine Animee de Lucian; pendant une Rupture legacy, il est
interprete comme un arret. `StopEffect_CharacterEffect` arrete explicitement Holy. Son instance VFX est egalement
enfant de `CC_Base_Body`; son binder Transform reste actif pour conserver une
AABB VFX valide. Les offsets du prefab Holy sont neutralises sur l'origine du
body de Lucian. `StopEffect_CharacterEffect` est uniquement pilote par l'AnimationEvent de
`Rupture`; sa sortie attend `1.1` temps normalise pour laisser son evenement de
fin de clip s'executer.
`MuninOrbVisualController` ne modifie plus les matériaux, les UV ni le mode de
rendu des enfants de `Munin_v4` en Play : le rendu de jeu conserve donc celui du
prefab en édition. Il applique seulement un plafond de taille écran de 0,06 aux
particules (ou une valeur auteur plus basse), afin d'éviter que les flares ne
deviennent de grands panneaux près de la caméra. Les couches `Flare4_Additive`,
`Flare_Ultrawide` et `FuzzAdd`, qui restent rectangulaires avec le shader du
pack sous HDRP, sont masquées uniquement au runtime.
La racine de `Munin_v4` ne doit pas être agrandie : certains systèmes du pack
deviennent invalides sous HDRP; un agrandissement futur doit viser les modules
de taille des particules. `MuninOrbVisualController` applique actuellement un
facteur de 2,5 à leur `startSize` et restaure les valeurs auteurs à sa désactivation.

## Prochaine utilisation

## Direction château : chaleur et givre (2026-10-04)

Le château doit rester en état matériel `Ice` et en ambiance froide. La
chaleur, notamment la torche du joueur, fond localement le givre vers l'état
`Normal`, révèle les interactions et active les objets dépendants de cette
influence. La torche possède une réserve rechargeable par combustible et
braseros. Le travail à venir doit séparer la ressource, l'influence gameplay et
la transition shader afin que chacune soit testable et synchronisable.

`Lit_Volume_Castle`, profil HDRP partagé du château, porte désormais les
réglages suivants : il
fixe l'exposition à `-0,35 EV`, ajoute une brume volumétrique bleue dense et
modérée, et la `Moon Light` du `Castle_Volume` éclaire à `1,1 lux` avec ombres
souples. Le profil est le point d'ajustement des valeurs d'ambiance; les
lumières pratiques restent indépendantes.

Le Shader Graph `LitIceFrostedEdges_v3` concentre les reflets bleu froid sur
les arêtes bakées et texturées, sans rendre le corps sombre uniforme miroir.
La transition de flamme conserve les textures de l'état normal, ajoute une
humidité chaude, puis produit dans le fondu une bordure de fonte animée avec
gouttelettes et faible émission orange optionnelle.

Le deblocage d'une `KnowledgeSO` n'interrompt plus le personnage par un
`CrossFade` force vers `Knowledge_Unlock`. Ce comportement legacy, partage par
les objets recuperables et les stabs, ecrasait des parametres Animator de
locomotion et pouvait immobiliser Lucian jusqu'au saut suivant. Le feedback
reste porte par l'UI, le son et le VFX; l'option legacy est desactivee par
defaut sur `KnowledgeManager`.

`GhostReactionChoicePanel` est le panneau auteur unique de `UI_Overlay`.
`GhostController` exige ses enfants `Ghost_Name`, `Ghost_Question` et `Options`;
il reutilise les choix enfants existants, leur ajoute au besoin le composant
`Button` et leur hauteur de layout sans instancier un second panneau. Les
listeners ajoutes pour une ouverture sont retires selectivement a la fermeture,
ce qui preserve les actions configurees par l'auteur sur les boutons.
`GhostChoiceCursor`, enfant de `Options`, est decoratif (sans raycast et hors
layout) et se cale au premier plan sur le choix survole ou selectionne par
l'EventSystem; il reste ainsi visible au-dessus des fonds opaques des boutons.
Il est aussi positionne explicitement sur le choix par defaut a l'ouverture :
son affichage ne depend donc pas de la presence d'un `EventSystem` runtime.
Les choix implementent `IInputModeHandler`: la map `UI/Navigate` transmet le
stick gauche et la croix directionnelle a `GhostController`, qui deplace le
curseur haut/bas entre reponse et `Reculer`. Valider sur `Reculer` ferme le
panneau sans declencher la reponse par defaut. La validation utilise le bouton
vise par le curseur comme source de verite, et non une selection EventSystem
eventuellement restee sur le choix precedent.

Pour une nouvelle tache, partir du modele `prompts/codex_task.md`, remplacer
`[TACHE]`, puis fournir le prompt a Codex depuis le contexte `AIAgent`.
# Juggernaut combat

- Added the real-time tactical reaction and attack-recovery safety contract for the Juggernaut.
- Le Juggernaut normal utilise maintenant `EnemyCombatBrain` comme seul decideur offensif. Son ancien comportement reste disponible pour les APIs de pose cinematographique, mais sa boucle IA est desactivee quand un profil moderne est assigne.
- `EnemyNavigationController` est le proprietaire de l'attachement NavMesh : il attend le bake de monde, redemande un rebuild de maniere debouncee, ne rattache l'agent que si la projection est tres proche et ne deplace jamais le transform en fallback. Le cerveau reste dans `WaitingForNavMesh` tant que ce contrat n'est pas valide.
- `Juggernaut_CombatProfile` contient provisoirement trois definitions Assomoir (proximite, pression, charge). Elles servent a valider le deck, les cooldowns, la selection de distance et l'enchainement avant l'ajout de clips visuellement distincts. Avec une seule definition, le cerveau autorise sa reutilisation apres cooldown : l'anti-repetition ne peut plus bloquer un combat.
- Les impacts autonomes utilisent une hitbox auteurisee non alloc (sphere, capsule ou arc), dedoublent chaque joueur par fenetre et respectent l'invulnerabilite d'esquive. `EndEnemyAttack` et le watchdog notifient tous deux le cerveau, qui libere alors l'action et restitue la recuperation.
# Combat realtime - paliers de vie

- Les ennemis peuvent activer `enableCombatHealthThresholds` dans leur `CharacterData`.
- Chaque palier plafonne le coup au pourcentage configure et reference un `ThresholdSequence`. Lucian est pose a 2 m devant l'ennemi (fallbacks angulaires de +/-15, +/-30 et +/-45 degres), puis les deux acteurs se font face. Aucun palier n'utilise de Timeline baked, de bindings ou de pool cinematographique.
- Une sequence lance un clip InPlace de Lucian, qui contient un ou plusieurs `QTE("Y"|"B"|"A"|"X")` ordonnes. Un override runtime lie les clips de la sequence aux deux etats generiques `ThresholdSequence_QTE` et `Threshold_Succes`, puis restaure le controller de Lucian a la fin. Sans clip QTE specifique, l'etat generique conserve son clip par defaut. Chaque reussite laisse le clip continuer vers le QTE suivant; seule la derniere declenche l'animation de succes. Le delai de mort tue l'ennemi sans couper le clip; a sa fin, Lucian fond vers `CombatIdle` avec `successExitBlendSeconds` avant la reprise ou la fermeture de combat. Un echec effectue un fondu vers `CombatIdle` avant la reprise ou la riposte. La fenetre dure 0,5 s non-scale, utilise uniquement la map `CombatQTE` et demande un ralentissement global a `0,4` pendant son ouverture.
- Une reussite lance immediatement son etat Animator de succes puis tue l'ennemi ou reprend le combat apres le delai configure. Un echec reprend immediatement ou lance le `SkillSO` ennemi configure; son impact est route vers `ResolveThresholdFailureImpact()` avant le recul UCC collision-aware.
- `KillEnemy` met les PV de l'ennemi a zero, joue sa mort puis appelle la fin de combat dediee au palier : le HUD de combat se ferme sans ouvrir un second ecran de victoire.
- `QTEPanelController` pilote l'overlay de scene : position aleatoire sure de `QTE_Circle_Slider`, sprite du bouton attendu, anneau radial et fermeture sur succes, echec ou interruption. Il est resolu explicitement ou parmi les objets de scene inactifs, avec diagnostic de CanvasGroup lors d'une ouverture.
- `SkillSO > Player Target Lunge` permet une approche UCC rapide vers l'ennemi suivie d'un rebond arriere/vertical. L'animation reste InPlace; UCC est l'unique proprietaire du mouvement, des collisions et de l'atterrissage.

# Temps global et local

- `ApplicationRoot` cree et conserve le `TimeManager` unique. Il est le seul ecrivain de `Time.timeScale` et `Time.fixedDeltaTime`; pauses, hit-stop, warnings, contres et QTE passent par des handles idempotents.
- `CombatTimeDomain` est automatiquement present sur chaque `ActorRoot` de combat. Il ralentit localement Animator, locomotion, IA, NavMesh, physique et actions sans ecrire le temps global.
- Les QTE gardent leur fenetre et leur overlay en temps reel non scale. Ils demandent temporairement une echelle globale au `TimeManager`; la fenetre reste donc exactement de la duree configuree. Les autres Timelines cinematographiques restent en lecture non scale.
# Navigation centralisee

# Outlines apres combat

- `RealTimeCombatManager` masque les interactions a l'entree d'un combat, puis planifie systematiquement, une image apres sa sortie, une restauration et un nouveau scan local. Cela couvre victoire, fuite, ennemi detruit et interruption forcee sans reveler un objet sous une UI ou une cinematique encore active.
- `RuntimeOutlineSelectionManager` elimine les proprietaires de suspension Unity detruits et refuse les suspensions anonymes. Une suspension UI/cinematique vivante reste prioritaire : les outlines ne reviennent que lorsque le joueur est effectivement hors combat et hors presentation bloquante.

Le cycle NavMesh est centralise dans `NavMeshWorldService` sur `GameplaySessionRoot`. Les zones peuvent fournir un `NavMeshData` pre-bake dans leur `ZoneManifest`; sinon un bake runtime controle est execute apres chargement complet. Les markers et agents attendent l'etat `Ready`.

La surface de build runtime est maintenant independante de l'offset monde de
`GameplaySessionRoot`: elle est creee a l'origine monde pour correspondre au
repere du `NavMeshData` pre-bake. Cela corrige le decalage observe entre le
marker a `Y=-94` et le polygone recharge a `Y=-97,56`.

Le flux normal est maintenant pilote exclusivement par `GameFlowService`: le service
ne lance plus de bake concurrent au reveil pendant un chargement additif. Avant le
bake, le nombre de scenes du manifest est compare au nombre de scenes effectivement
chargees. Un agent n'est rattache que si sa projection locale respecte 0,15 m en
horizontal et en vertical; un polygone sur un autre niveau est refuse et aucun Warp
n'est effectue. Les traces de validation indiquent la position attendue, le point
NavMesh trouve, le delta, l'etat de l'agent et la destination de locomotion.

# Presentation Melt de Munin

- `SpiritBondController.MeltTheIce()` est le point d'entree de la fusion manuelle : il lance la presentation locale puis le trigger Animator `Melt`; seul l'Animation Event existant confirme l'etat de fusion. Une Rupture garde son flux precedent et ne rejoue jamais cette presentation.
- `CameraProfilSO` decrit une courte pose locale autour d'un ancrage. `CameraProfilPlayer` remet temporairement l'autorite a une `CinemachineCamera` via `LitCameraDirector`, puis rend la main a UCC et son snap de retour. Il n'ecrit jamais le transform de la Main Camera.
- `CameraProfil_Melt` cadre Lucian en trois-quarts avec l'offset `(2, 1.8, -3.5)`, une entree/sortie de `0.5 s` et un maintien de `1 s`. Les durees UI et camera utilisent `Time.unscaledDeltaTime`.
- `CameraProfilPreviewTool`, attache a AnimationLab, permet de lire un `CameraProfilSO` hors Play Mode. Son Inspector fournit `Play`, `Rejouer` et `Stop`; il utilise une CinemachineCamera temporaire et restaure la pose de `Preview_MainCamera` apres chaque lecture. Le FOV et sa duree de transition sont maintenant des donnees du profil, partagees entre l'aperçu et le runtime.
- La presentation Melt capture puis restitue alpha, raycasts, HDR du givre et camera apres `endLerp`; aucun etat de presentation local ne persiste a la fin ou lors d'une interruption.

# Notifications d'objets

- Item genere maintenant les confirmations coherentes Objet recupere : [nom], Objet jete : [nom] et Objet detruit : [nom]. Elles sont utilisees par le ramassage, le jet et la destruction, localement comme via Netcode.

# SceneMarker et Outline

- Chaque Bake de SceneMarker inspecte la racine produite et ses enfants. Sans RuntimeOutlineTarget, l'Inspector demande au developpeur s'il souhaite en ajouter un, puis propose uniquement les GameObjects qui portent un Renderer. L'ajout est annulable et selectionne l'enfant choisi.

# Scientifique fou : acces et connaissance

- Le scientifique fou n'a aucun prerequis de connaissance pour son interaction Ghost. La defaite ncounter est enregistree au moment de sa mort et revele immediatement Existence des chimeres, independamment des flags legacy ou de la cinematique.

# Mort des ennemis de cycle

- `EnemyDeathPresentation` se lie directement au changement de sante et conserve
  son controle de secours par image. L'animation `Death`, la dissolution et la
  desactivation sont ainsi lancees meme si une victoire est resolue hors du flux
  habituel de degats. `DeadWeightBoss` force egalement ce flux a sa resolution;
  le `ShadowGuardian` de Belmont le partage par son `EnemyController`.
## Laboratoire Lucian / Brains AI (2026-10-08)

Iluvilirae_Test utilise le vrai Lucian/UCC et Juggernaut_v2 avec Brains AI,
sans Bootstrap ni EnemyController ajoute a v2. Le contrat ICombatTarget
permet de reutiliser RealTimeCombatManager, clarte, competences, palette,
garde, CounterSkill, LightSkill et leurs rigs sans changer les decisions
ennemies. LegacyCombatTarget preserve les contrats des ennemis existants.
La camera tactique UCC et les huit profils d'esquive viennent du prefab
GameplaySessionRoot. TimeManager est explicite sur la racine du laboratoire.
TemporalRiposte conserve ses clips et regles dans une copie avec un rig poolable
dedie (l'original dependait d'un Director de scene). Aucune carte input privee ;
X/B/Y/A/RB/L3/croix bas
suivent PlayerInputs. L3 ne declenche plus l'inspection gamepad en combat.
Start ouvre la pause, B/Start reprend, A dans la pause ou R recree le combat.
Les deux actions partageant Start sont dedupliquees sur la frame de transition.

Les jambes de Lucian sont pilotees en verrouillage par les parametres locaux
UCC ; les directions Walk/Run de v2 et Speed suivent la vitesse reelle NavMesh.
V2 contient 22 clips propres et trois attaques. Chacune ouvre une reaction
B/Y puis emet son contact anime. Hurt/Death et les cinematiques suspendent
l'ennemi. Les BasicSkills gardent leurs degats exacts et presentation de jeu.
Les anciens prefabs Juggernaut et les scenes de perception restent distincts.
La LightSkill Devastation demande toujours un depart entre 6 et 10 m.
Reseau, recompenses et persistance sont hors du laboratoire local.

Le menu Upgrade Lucian Combat Laboratory reconstruit explicitement les assets
et references depuis les sources de production. Le redemarrage annule les
actions/cinematiques, restitue inputs, temps et cible puis retire les acteurs
par identite de la simulation UCC avant destruction. La reconciliation des
inputs maintenus ne recree plus un input host pendant la fermeture de scene.

Validation : compilation runtime/editeur et parcours Play Mode batch dans un
projet de validation isole, avec les vrais scripts et assets Lit/UCC (aucun
contrat simule). Gamepad simule : mouvement des acteurs et des os de jambes,
attaque X, PV, garde Y, esquive B et invulnerabilite, palette RB/stick droit/A,
LightSkill L3 et retour de controle, saut A, attaque autonome ennemie, mort,
victoire/defaite et redemarrages, pause/reprise, CounterSkill depuis son
evenement de reaction et desactivation/reactivation du laboratoire. Ce parcours
final reussit sans MissingReferenceException, NullReferenceException ou objet
recree pendant la fermeture de scene. Le batch est sans affichage : rendu HDRP et
ressenti visuel restent a confirmer dans l'Editor avec manette.

GameFlowService continue d'ignorer les scenes lancees sans Bootstrap : aucune
redirection ni creation d'ApplicationRoot. Un composant present se desactive
sans publier son singleton. Bootstrap est reconnue meme chargee additivement
et non active.

Tests EditMode : 50/50 reussis (Iluvilirae, Juggernaut v2, references du
laboratoire, geometrie de contact, clarte, reprise exploration et stabilite
tactique). Rapport : Library/IluviliraeValidation/CombatRegression-complete.xml.
Parcours Play Mode : Library/IluviliraeValidation/combat-flow-final.log.

## Fluidite du laboratoire Lucian / Juggernaut_v2 (2026-10-09)

AnimationLab : atelier Editor distinct du combat. Le parent _PreviewActors
etait desactive et le Director/holder referencaient des assets supprimes.
Scene reconfiguree avec copies visuelles Lucian/Juggernaut_v2 sans gameplay,
ancres face a face, camera d'ensemble et bindings sur les Animators racines.
CombatAnimationLab expose clips et Timeline ; son Inspector permet sampling,
lecture en boucle et ouverture de la Timeline native avec bindings prepares.
L'arret du preview restaure les poses. Aucun service de combat ni lancement
de Play necessaire. Le menu historique Root Animators utilise le nouveau setup.
Validation : compilation runtime/Editor, reouverture de la scene et evaluation
des clips et de la Timeline hors Play passent, avec controle des os des deux
acteurs et restitution de pose. Rapport : Library/AnimationLabValidation/
editor-preview-final.log. Rendu HDRP et commandes de la fenetre Timeline a confirmer
dans l'Editor interactif ; aucun test de combat ajoute a cet atelier.

Contre : Timeline conservee, plateau relatif de 1,7 m centre et oriente
au declenchement. Le signal KnockedOut de CombatLab_CounterSkill_2 a 3,50 s programme
un stun de 2 secondes apres la cinematique : etat Knocked Out, mouvement/rotation/attaque bloques,
degats toujours recus et mort prioritaire. L'etat utilise Mixamo_KnockedOut, sans transition Hurt sur les impacts.
Le signal allume le VFX ; la liberation du graphe lance la fenetre de 2 s.
VFX : reference publique VFX_KnockedOut sur LitBrainsEnemy, effet attache
a la tete pendant le stun et detruit a la recuperation/mort/desactivation.
Le prefab Juggernaut_v2 reference l'effet existant par defaut ; pas de doublon.
Compilation Unity et parcours gamepad simule passes : duree du stun,
Knocked Out, immobilite/rotation, reprise, impact pendant stun, VFX unique,
suppression a la recuperation et mort prioritaire. Rapport :
Library/KnockoutSignalValidation/signal-play-final.log et legacy-play.log.
Signal observe avant liberation du graphe a 3,50 s (queue prolongee dans
le test seulement), appels repetes sans prolongation ni doublon. Rendu
visuel de l'effet et du nouveau clip a confirmer en jeu (tests sans affichage).
Regression finale : 61/61 tests EditMode passes, incluant le signal a 3,50 s,
l'etat Mixamo_KnockedOut, le VFX public et les bindings du controleur.
Rapport : Library/KnockoutSignalValidation/tests-final.xml.
La Timeline 2 re-editee pendant le travail conserve son signal valide :
verification ciblee 1/1 passee, latest-timeline.xml.
CameraShake : quatre signaux auteur raccordes au rig ; correction Cinemachine
Noise de 0,06 m / 0,14 s / 28 Hz reglable dans CombatCinematicCameraShake
sur les cameras du prefab. Horloge non ralentie, aucune translation des
acteurs ou derive du cadrage, effacement au reset du pool.
Un plan duplique avait une cle camera non raccordee : alias explicite vers
la camera du rig, clips et temps auteur conserves. Compilation Unity et
parcours Play Mode passes : quatre appels CameraShake observes, correction
Cinemachine non nulle pendant la pause du combat, Knocked Out/reprise/VFX
toujours valides. 63/63 tests EditMode passes (bindings, temps, correction
bornee et expiration inclus). Rapports : Library/CounterCameraShakeValidation/
play-final.log et tests.xml. Force de la secousse a valider visuellement.
La selection actuelle de CombatLab_CounterSkill reste la Timeline 1 ;
la Timeline 2 est configuree sans changer cette selection.
Plateau : ancres explicites et pistes Animator en Scene Offsets.
Pas de transfert concurrent de root motion. Camera calculee directement
depuis l'horloge commune dans un repere capture, reaction ennemie recalee
au contact de Counter_Sword (1,2567 s). Placement refuse hors NavMesh ou
a travers un obstacle. Le menu Upgrade conserve les ancres auteur valides.
Parcours Play Mode passe avec les vrais acteurs, depart a (5,0,5), rotation
123 degres, impact puis reprise du stick maintenu et recreation de l'arene.
Rapport : Library/CounterStageValidation/play-final-3.log. Le test cible
met l'IA en attente pendant ses controles initiaux de pause pour que sa
cadence acceleree ne perturbe pas leur precondition de pleine sante.
Regression EditMode : 59/59 tests passes, dont les nouveaux controles de
plateau relatif/contact et de trajectoire camera independante du mouvement
des acteurs. Rapport : Library/CounterStageValidation/tests.xml.
Le cadrage avec rendu HDRP et manette physique reste a confirmer visuellement.

Reglage du 2026-10-09 apres analyse video : Juggernaut engage depuis la
course a 3,6 m, avance pendant la preparation (1 m max), puis charge en
direction verrouillee (0,9 m max sur 0,2 s). Espacement 1,7 m, cadence
1,5–1,9 s, recuperation 0,2 s ; le delai ne bloque plus la poursuite.
Revalidation : compilation runtime/editeur, 57 tests EditMode et parcours
InputSystem gamepad complet passes. Un depart de frappe a 3,5 m ferme bien
la distance avec une avance bornee ; les fenetres de contre et les commandes
restent operationnelles. Rapports : Library/EnemyRushValidation/tests.xml,
play-2.log et gamepad.log. Le rendu du nouvel elan reste a valider en Play Mode.

Profil local LitBrainsCombatProfile raccorde au prefab v2 ; l'ancien Juggernaut
et les combats narratifs ne sont pas migres. Locomotion a proprietaire unique,
phases Preparation/Strike/Recovery, avance NavMesh bornee, cadence continue et
resistance progressive remplacent les blocages systematiques. Les copies des
BasicSkills sont effectivement selectionnees par SkillsManager dans l'arene.
La presentation conserve seule le prochain coup, ses degats et son de depart ;
l'esquive attend au maximum 150 ms et annule ce prochain coup. Les impacts
sont uniques et les VFX directs suivent un contact confirme. Le ralenti
automatique est retire du laboratoire ; la camera de contre garde les torses
cadres et une separation des acteurs. Profils et marqueurs restent reglables
sans etre ecrases par l'assistant de reconstruction.

Validation realisee : compilation runtime/editeur, 57 tests EditMode passes
(fluid-tests-complete.xml), parcours complet avec gamepad simule et vrais
acteurs/UCC, puis controle resistance/cadence/huit directions/expiration et
priorite des commandes. Une attaque annulee cesse immediatement d'etre eligible,
meme pendant son fondu d'animation. Le parcours de contre confirme aussi la
reprise du stick maintenu apres restitution des commandes et de la navigation.
Les poses des trois frappes ont ete rendues et inspectees : impacts recales,
recuperation a 0,3 s. Rapports dans Library/IluviliraeValidation :
fluid-gamepad-delivery.log, fluid-smoke-delivery.log et fluid-counter-delivery.log.

Limite de validation : ces parcours utilisent InputSystem et un Editor isole
sans affichage. Les captures de poses utilisent un rendu neutre. Le rendu HDRP
complet, les pieds aux departs/arrets, le cadrage dynamique et le ressenti sur
manette physique restent a confirmer visuellement dans Iluvilirae_Test.
Aucune generalisation aux combats historiques n'est lancee.
