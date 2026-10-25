# Combat

## Unification Player_Model — plan confirme, non applique

Reference auteur : Assets/Characters/4_Animations/Player_Model.controller,
90 etats, 11 Blend Trees et 88 transitions automatiques. Le controller actif
de Lucian est un override de Lucian_ImpactLab (96 etats), pas directement de
Player_Model. Les copies ennemies comptent actuellement 7 a 28 etats selon le
rig. La migration commune ne peut pas etre une copie aveugle des 90 etats.

Decisions utilisateur : structure et noms communs, lancement des animations
par nom d'etat, clips propres au rig ; une action sans clip reste explicitement
indisponible, sans remplacement. Werewolf et StoneGolem restent Generic.

Ordre de realisation :
1. Etablir dans Unity le manifeste des chemins complets (couche/sous-machine/
   etat) et des motions effectives, y compris les overrides et les feuilles de
   Blend Trees. Partir de Player_Model et y integrer les six etats actifs
   supplementaires de Lucian ainsi que les Hit_n communs. Conserver les
   identifiants encore consommes par les skills, Timelines et pilotes.
2. Definir un catalogue de disponibilite par acteur/etat/feuille de locomotion.
   Un etat vide ne suffit pas : Animator.HasState le considere toujours present.
   Interdire un appel indisponible avant cooldown, degats ou reservation de
   cible. Signaler precisement acteur, etat et clip absent. Ne pas confondre
   une entree existante avec une animation jouable.
3. Generer des controllers ayant le meme schema et leurs motions propres,
   avec prevalidation complete avant remplacement. Eviter le piege des
   AnimatorOverrideController : un override null conserve le clip de Lucian.
   Les motions indisponibles doivent etre reellement absentes et couvertes
   par le catalogue ; aucun clip vide ou etranger pour masquer le manque.
   Controler les masques/couches sur les vrais rigs Generic et Humanoid ;
   une couche optionnelle entierement indisponible ne doit pas modifier la pose.
4. Migrer les scripts/profils vers une entree commune par chemin d'etat et une
   autorite unique de presentation. Les Blend Trees peuvent conserver leurs
   parametres de melange ; entrees/sorties d'etats doivent etre pilotees par
   script. Remplacer progressivement les transitions automatiques apres
   migration de leurs pilotes : saut, reception, depart/arret, vol, torche,
   skills, reactions et mort. Adapter aussi les profils IA, les noms legacy,
   les garde-fous des marqueurs CounterHit et la presentation reseau.
5. Integrer sur les prefabs/definitions des acteurs actifs, puis leurs outils
   d'installation. Preserver les GUID, les overrides et evenements auteur,
   les physiques UCC/NavMesh, les autorites et sessions cinematographiques.
   Ne pas generaliser aux controllers UI/objets ni aux prototypes hors perimetre.
6. Valider l'egalite du schema (couches, chemins, parametres, Blend Trees), les
   correspondances de rig, le refus des actions manquantes, les evenements et
   le resultat d'une deuxieme migration. Recette sur les vrais acteurs :
   locomotion, saut/reception, torche, skills, Hit_n, mort, contre, interruption,
   changement de zone et host/client. Aucune action fonctionnelle de Lucian ne
   doit disparaitre lors du retour au gabarit Player_Model.

Les six etats supplementaires a conserver sont ChargedPreparation,
ChargedAscension, ChargedFloat, ImpactDeLumiere, AirBasicSkill_2 et AirBasicSkill_3.
Inventaire initial en lecture seule : Library/SharedAnimatorContract/Inventory.json.
Ce plan de migration ne decrit pas encore l'etat du runtime installe.

## Contres : impacts Timeline independants du rig ennemi

`CombatLab_CounterSkill_2` utilise des marqueurs `CounterHitSignal`, affiches
`CombatLab_CounterHit_Damage_Animation_VFX_SFX` sur la piste Signals.
Chaque marqueur expose Damage (float en points de vie, arrondi a l'entier),
Hit Index, Blend Seconds, Vfx et Sfx. Hit Index 2 cible Base Layer.Hit_2 sur
l'Animator de la victime capturee par la session. Ce meme nom d'etat doit
exister sur chaque ennemi compatible ; ses clips restent propres a son rig.
Un indice absent ou une valeur invalide refuse la lecture avant tout degat.

La piste Enemy.Animator fixe du contre 2 est conservee mais muette. Les
reactions sont evaluees par le rig sur l'horloge de la Timeline, meme pendant
la pause globale du contre. L'evaluation automatique de cet Animator est
suspendue pendant cette autorite, puis enabled/keep-state/root-motion sont
restitues sur fin, echec ou interruption. Aucun delta Root ennemi n'est
transfere pour ce mode. Les clips et horaires auteur du joueur et des cameras
restent inchanges. Les anciens evenements ResolveCounterSkillImpact sont
consommes sans degat supplementaire uniquement sur les Timelines migrees.

Les degats passent par le contrat combat existant avec la cible capturee ;
la clarte n'est accordee qu'au premier impact positif. Le reseau conserve
l'autorite serveur et la generation/reservation de cinematique. Le choix
explicite CounterSkillSO.Timeline est respecte pour validation, lecture et
delai de fin, plutot que d'imposer la Timeline bakee du rig. Les observers
jouent la presentation, sans appliquer de degats ni doubler le SFX via le RPC.
Chaque marqueur est consomme une fois par session/graphe ; un ancien graphe,
une victime morte et une session interrompue ne peuvent plus appliquer de coup.

Menu Lit/Combat Lab/Configure Unified Counter Hits : migration explicite et
idempotente, conservation des montants/indices/horaires/effets deja auteur.
Les quatre contacts existants gardent 25 degats de base au total (6/6/7/6),
librement editables par signal. Des alias natifs Hit_1 sont ajoutes aux
controllers Juggernaut v2, Iluvilirae, Juggernaut Production, Werewolf et
StoneGolem ; ce dernier a aussi Hit_2/Hit_3 a partir de gethit2/gethit3.
Aucun clip d'une autre famille de squelette n'est injecte dans ces etats.

Verification : compilation Unity runtime/Editor, 25 assertions de graphe
EditMode (notifications reelles, floats, indices, doublons, pause Animator,
interruption, reprise de pool, ancien graphe, mort, observer, arret reentrant),
seconde configuration sans changement des sept assets controles. Rapports
Library/CounterHitSignals*. Le rendu Play Mode et un parcours host/client
complet restent a qualifier dans le contexte de jeu.

## Recuperation Apres Support Non Navigable

Le moteur de mouvement production conserve la position NavMesh de depart du
coup. Apres atterrissage sur un collider hors NavMesh, il peut rechercher une
sortie locale bornee avec les reglages de clearance/sol existants. Destination
libre pour la capsule, hauteur compatible et chemin complet depuis le depart
obligatoires ; aucune extension de tolerance ou activation anticipee d'agent.
La sortie utilise Rigidbody.MovePosition, vitesse de marche et gravite, avec
balayages planaire et vertical separes pour ne pas traverser le support pendant
sa descente. Le rattachement garde la validation locale actuelle de 0,15 m.
Un mur bloque le trajet ; un delai borne nettoie l'action avec diagnostic sans
warp ni degat de secours. Cinematique, desactivation et changement de generation
annulent la sortie capturee ; la mort ne continue pas le trajet horizontal.
La descente balistique ou en ruee verifie aussi le volume entier de capsule, pas
seulement le rayon central qui pouvait enfoncer le corps dans un mesh decale.
Le support confirme par capsule empeche un snap ulterieur vers ce rayon plus bas.
Une face inclinee qui bloque la descente constitue un support physique pour
sortir de l'obstacle, pas un sol marchable : les filtres de sol de destination
et la validation NavMesh ne changent pas.
Tests physiques natifs : support solide sans penetration, support decale pendant
la descente, mur, autre etage, generation perimee et suspension cinematographique.
Regression ciblee 483/0/1.
La recette rendue depuis Bootstrap reste une preuve distincte, pas deduite de
ces tests de geometrie isolee.
Recette d14f503cc325488a82628d855da4fe58 passee : trois impacts avec fuite et
reengagement, puis retour home/NavMesh/orientation auteur et controles rendus,
sans impact tardif. Flame fixture explicite ; autres rencontres/reseau non valides.

## Integration Du Ressenti Valide (2026-10-19)

La preference melee de LitBrainsEnemy.Production s'applique seulement si le kit
equipe possede au moins un pattern terrestre. Un kit uniquement aerien reste
eligible sans tirage d'alternative, meme avec preferMeleeApproach actif. Cette
condition ne rajoute pas de skills : combatSkills reste la source auteur.
Un kit mixte conserve le choix aerien memorise et ses cooldowns. L'approche et
le retour remettent explicitement isStopped a false avant leur destination,
apres les gates existantes ; attaques, physique et suspensions gardent leur
autorite. Tests de filtrage dans LitBrainsProductionPolicyTests.

Sortie de poursuite : une cible vivante hors limites ne suffit pas a annuler une
attaque en Preparation/Strike/Recovery. Le controleur laisse sa terminaison
normale, puis refuse le prochain pas de combo hors zone et commence le retour.
Mort et interruptions prioritaires gardent leurs chemins existants. Pendant le
retour NavMesh, le root est oriente vers home par la vitesse de rotation du
profil ; NavMesh ne reprend pas une seconde autorite de yaw. L'orientation
auteur initiale est restituee a l'arrivee.

Apres une attaque, si la cible est sous la portee minimale de TOUS les patterns
equipes, l'approche production devient un repositionnement a pied vers l'exterieur
de cette zone. Destination locale validee et chemin NavMesh complet obligatoires,
sinon arret propre ; pas de warp ni mouvement Transform. La distance desiree est
bornee a l'interieur de la bande auteur (marge max 0,2 m), sans changer sa portee
de degats. Une melee encore possible interdit ce recul automatique. Attaque,
recuperation, cooldown et suspension restent prioritaires.

Import Unity 6000.6.4f1 : une ligne vide avant la section vision du Juggernaut
faisait charger les valeurs par defaut, malgre 30/100 dans le texte. Diagnostic
sur copies temporaires avec/sans ligne vide : 6/30, meme apres ForceImport du
source. Seule cette ligne est retiree ; meta/GUID/valeurs auteur preserves.
Le test de contrat charge maintenant le vrai asset et verifie 30 m/100 degres/
1,5 m, au lieu de conclure depuis la presence de valeurs dans son YAML.
Recette production bornee 52de58fb32b245f6b2487b975f212307 : vision, approche,
deux Assomoirs et repositionnement passes, Flame fixture explicite. Ce n'est pas
une validation de toutes les rencontres ni du ressenti manuel.

Diagnostic historique LucianJumpStateTraceBehaviour : suppression deja confirmee
par le commit 0b219dcdc4b9fd67224cacda8d57ddb5c0210726. Le nettoyage cible uniquement
ses 70 documents orphelins dans 10 controllers, apres verification GUID/type/schema
et SHA-256 ; aucun StateMachineBehaviour actif, motion, transition ou event ne
change. L'outil refuse une restauration du script, une modification concurrente
ou une forme inattendue. Les autres manques d'assets restent visibles a l'audit.

Les reglages jusque-la appliques par LucianBrainsCombatArena sont maintenant
auteur dans leurs proprietaires communs : rotations des PlayerSettings/profils
ennemis, assistance et curseur du CombatWorldRuntime, buffer de mobilite 0,15 s,
reactionTimeScale 1 et feedback externe de reussite du controleur de seuils.
Les anciennes surcharges de l'arene sont retirees apres migration des valeurs.
Ce deplacement ne change ni les fenetres de contre ni les QTE visibles.
L'outil de migration conserve les reglages auteur lors des applications suivantes.

La migration auteur commune transfere maintenant les reglages BasicTail/Impulse
de Lucian dans Ascension canonique sans changer son GUID. Les trois Basics
aeriens et les slots des controllers proviennent des memes assets valides.
Les quatre prefabs joueurs recoivent CombatContactAudio, avec leur configuration
propre si presente, sinon celle de Lucian ; les clips audio des skills restent
leurs clips auteur. Le Juggernaut reel recoit CombatContactPose seulement apres
verification du rig et absence de colliders sous les os modifies. Les autres
rencontres conservent leurs mecanismes et leurs presentations specialisees.

Le reseau execute BasicTail dans la presentation externe du Basic courant,
liee au ticket autoritaire ; maintien et horloge reelle sont decides au serveur.
Les observateurs rattachent la pose au bon ticket, la camera reste au proprietaire.
Le moteur de dash partage recoit Burst depuis le monde serveur ; Rupture conserve
sa deceleration de reception par generation. Aucun second moteur ou dommage
de secours. Les impacts confirmes transportent le point du dash aux VFX non
parentes et le skill/generation a l'audio, sans seconde lecture. Le siphon ne
declenche pas de reaction de torse par ses ticks.
Installation, regressions automatisees et qualification host/client restent
des preuves distinctes ; aucune migration auteur ne valide seule une rencontre.

## Presence Des Contacts Au Laboratoire

CombatContactPose est une presentation optionnelle sur ChargedEnemy_Lab et sur
le Juggernaut de production installe explicitement par la migration.
Apres un impact libre confirme et des PV retires, le resolveur demande une courte
reaction additive du Spine/Chest, sans changer l'etat Animator, la racine, l'IA,
les collisions ou les horloges. Intensite bornee selon la puissance du skill ;
reaction attenuee pendant une attaque engagee. Les reactions Hurt existantes,
la projection d'Ascension, la mort et les cinematiques restent prioritaires.
La pose additive est retiree sans ecraser une nouvelle evaluation de l'Animator ;
elle ne s'accumule pas lors d'une pause/culling et disparait a la desactivation.
Le prefab actuel ne porte aucun collider sur les os modifies. Ne pas installer
ce composant sur un rig dont ces os pilotent des volumes de gameplay sans audit.
Ce n'est ni une interruption supplementaire, ni un impact de secours. Les assets
non migres restent sans composant, donc sans changement de presentation.
L'installation explicite Lit/Combat Lab/Configure Contact Feel conserve les
reglages auteur existants et n'ajoute pas de nouveau moteur de camera/mouvement.

CombatContactAudio est installe sur Lucian_ImpactLab et les quatre joueurs migres. Il remplace
l'appel audio existant du VfxCue DirectOnTarget confirme, sans ajouter de voix ni
de seconde couche d'impact. La hauteur et la spatialisation restent celles du
contact. Le pitch de base varie legerement selon la generation et la puissance :
5 PV registre leger, 15 PV registre lourd, Ascension toujours registre lourd.
Le calcul deterministe ne consomme pas UnityEngine.Random et ne change donc pas
les decisions adverses. Les presentations externes/cinematiques conservent leur
son valide. AudioManager reste seul responsable du mixage et de la vie du son.

## Recuperations Du Juggernaut Au Laboratoire

CombatLab_ClashEnemyProfile utilise 1,05 / 0,985 / 1,017 s de follow-through,
mesurees via ActorPoseSampler sur ChargedEnemy_Lab, plutot qu'une sortie uniforme
a 0,2 s. Les poses de sortie sont plus proches de l'Idle auteur ; impacts,
reaction, mouvement et clips inchanges. VisualActionHandoff.validated reste faux :
pas d'IK/retiming experimental impose. CombatEnemyRecoveryAudit propose uniquement
des sorties au moins 25 % meilleures, apres le contact et avant la sortie
automatique du clip ; installation explicite et idempotente sous
Lit/Combat Lab/Configure Measured Enemy Follow Through.
CommitFluidAttack demarre l'etat Animator explicite du profil en fondu fixe de
0,08 s, sans trigger Attack concurrent : une nouvelle decision peut arriver
pendant le fondu de fin precedent sans annuler sa propre preparation.
La recette CombatExchangeValidation observe les inputs normaux et l'IA vivante
sur 30 s, apres stabilisation reelle. HP releves a 1000 dans la fixture seulement.
Les captures peuvent masquer les nombres sans les retirer du gameplay normal.
Cette recette ne remplace ni le ressenti manuel ni la validation multijoueur.

Le 2026-10-19, l'auteur valide le ressenti de l'echange demande : ouvertures
lisibles, reprises naturelles et distinction des frappes, sans les nombres.
Cette version devient la base acceptee d'Iluvilirae_Test : conserver la vue
large, ses rapprochements ponctuels et le ChargedSkill deja valide. Acceptance
limitee au laboratoire, sans qualification production ou reseau supplementaire.

## Charge Dans La Fin De BasicSkill (2026-10-19)

Ressenti valide par l'auteur. La charge est protegee des interruptions ordinaires :
les demandes directes de nouvelle animation et InterruptBasicSkillForDamage sont
refusees tant qu'elle est active. La Basic bufferisee qui porte la charge est
preservee lors d'une demande de nettoyage avec preserveChargedHold. Les PV restent
retires normalement ; relachement de X, mort, cinematique et nettoyage force restent
possibles. Les valeurs de vitesse, duree et camera validees ne sont pas modifiees.

ChargedSkillSO.preparationMode garde HeldPose par defaut ; BasicTail est active
uniquement sur Skill_AscensionLumineuse_Lab. Parametres : debut 0,7, vitesse 0,1,
fondu 0,08 s reel. Le debut est repousse apres les evenements auteur non cosmetiques.
Le meme etat Basic et la meme generation continuent, sans pose BasicSkill_1 imposee.
ChargedBasicPlaybackRate ne pilote que les trois etats Basic terrestres du lab ;
Animator.speed et Time.timeScale restent inchanges. La fin non bouclee peut etre
tenue a vitesse zero sans declencher le watchdog de progression. Le relachement
restaure progressivement la vitesse puis termine le geste. A deux secondes depuis
la pression, Ascension peut remplacer la fin du coup apres validation normale du
lancement ; un refus restitue la fin du Basic. Nettoyage du parametre, du verrou
de mouvement et du cadrage par generation. Reseau et autres configurations gardent
le chemin de preparation existant. Preuves de validation consignees dans current_work.
Validation ciblee : 26 tests Edit Mode passent (charge et continuite). Six cas
Play Mode couvrent relachement/lancement sur chacun des trois Basics ; une recette
distincte valide la pose finale tenue puis liberee, sans watchdog ni ressource
residuelle. Le ressenti manuel et les recettes reseau ne sont pas qualifies ici.

## Combo Aerien De Lucian (2026-10-19)

Lucian_ImpactLab equipe trois BasicSkills aeriennes, par pressions distinctes.
La premiere est conservee ; AirCombo/AirBasicSkill_2 et _3 utilisent des copies
dediees de Twinblades_Air_Attack_1_Inplace et Twinblades_Air_Attack_2_Inplace
(10 et 15 PV). Le corps entier provient des clips aeriens du pack, sans melange
avec des coups au sol ou remplacement des jambes. Les GUID des copies sont
conserves ; les imports FBX d'origine ne sont pas modifies. Les impacts et VFX
gardent leur phase normalisee lors du remplacement (durees des nouveaux clips).
Chaque clip possede un ResolveSkillImpact unique, avant sa fenetre de chaine,
et un seul evenement VFX. Deux etats distincts sont installes dans le controller
du laboratoire. LucianAirComboSetup.Configure est reexecutable et preserve les
reglages auteur deja crees. Aucun CharacterData de production n'est reconfigure.
Les nouveaux coups conservent LandOnApproach : pas de nouveau maintien de gravite
ni de prolongation du flottement d'Ascension. La disponibilite du coup suivant
reste revalidee selon l'etat physique au demarrage effectif.
La verification initiale LucianAirComboTests.xml (4/4) et LucianAirComboPlay.log
portait sur les anciens swings adaptes, pas sur les nouveaux clips Twinblades.
Les preuves Twinblades sont consignees separement ; aucun ancien resultat n'est
requalifie automatiquement. Le ressenti et le combo complet par pressions en
combat restent a essayer manuellement.
LucianTwinbladesAirTests.xml : 6/6 apres remplacement ; comparaison echantillonnee
des poses Humanoid avec les sources originales, impacts uniques et idempotence.
LucianTwinbladesAirPlay.log : deux nouveaux clips joues apres Ascension, un impact
chacun, atterrissage et restitution input/navigation ; sortie 0.

## Projection Chargee Impulse Au Laboratoire (2026-10-18)

Controle avec rendu : RunMotionVisualBatch produit six images de la camera
gameplay sans sauvegarde auteur. Deux premiers essais refuses car UCC montait
trop peu malgre la courbe theorique. Le raccord de maintien attend maintenant
la fin de l'ability au pas physique. En mode Impulse, la montee corrige l'ecart
entre position simulee et hauteur courante de la courbe, sans depasser la vitesse
de lancement ; rattrapage borne a 0,15 s, collisions prioritaires. Aucun warp.
ChargedImpulseMotionVisualHeight.log passe : Y Lucian 2,90 / ennemi 3,00 au
sommet, ecart 0,10 m, Basic aerienne et atterrissage avec controles restitues.
Captures dans Library/CombatFeel/ChargedVisual. Elles ne prouvent pas le ressenti
temporel du joueur ni les scenarios reseau.
Apres ce correctif, ChargedImpulseHeightSafety.log repasse les deux cas : plafond
bas (0,897 / 0,925 m), annulation en montee (1,520 / 1,644 m), un impact par cas,
deux acteurs au sol et commandes/NavMesh restitues. Aucun reglage de camera ajoute.

Le prototype de Lucian a son propre ChargedSkillSO dans Iluvilirae, cree depuis
la source par InstallImpulsePrototype. Seul Lucian_ImpactLab le reference.
Le mode Impulse partage une loi de montee quadratique decelerante entre l'ability
UCC et les adaptateurs ennemis. Prototype : hauteur 3 m, montee 0,36 s, sommet
atteint sans vitesse verticale constante. La fenetre de combos manuels reste
bornee a 1,2 s ; elle n'ajoute pas de second coup automatique.
La chute de l'ennemi integre sa vitesse initiale et son acceleration avec plafond
de vitesse ; Lucian conserve son solveur UCC et ses collisions, avec impulsion
descendante a expiration normale du maintien. Une interruption prioritaire ne
recoit pas cette impulsion tardive. La coroutine d'ascension ne peut plus arreter
l'ability d'une generation suivante. Mode ConstantVelocity conserve hors prototype.
Le hit de lancement reutilise burst et audio de frappe, sans shockwave/camera.
Qualification mathematique, execution reelle et ressenti doivent rester distincts.
Recette courte sans sauvegarde auteur : RunMotionBatch utilise les vrais acteurs,
un lancement puis une Basic aerienne manuelle. ChargedImpulseMotionPlayFinal.log
passe : Lucian +2,650 m, ennemi +2,999 m, impact charge unique, flotte interrompue
par la Basic, puis retour au sol et restitution de la navigation/commandes.
Les 0,349 m d'ecart ne constituent pas un alignement parfait. Les formules de
trajectoire passent aux pas 30/60/120 ; ce n'est pas une preuve visuelle ni reseau.
Ancienne suite ChargedImpulseArcFinalTests.xml : 7/8, echec du test de deconnexion sur
l'attente du callback canceled, sans changement runtime des inputs dans cette
passe. Tests de trajectoire et de separation prototype/production passes.
Correction de fixture : les updates joueur doivent etre explicitement autorises
hors Play Mode par le switch du package Input System, restaure en finally. La
phase Performed est desormais verifiee avant la deconnexion, sans enlever les
assertions canceled/lecture sure. ChargedInputLifecycleFinalTests.xml passe 8/8.
Cela ne remplace ni une suite globale, ni le ressenti manuel.
ChargedImpulseMotionSafety.log : une recette courte sans sauvegarde auteur couvre
plafond bas et annulation pendant la montee. Deux cas passes, un impact chacun,
deux acteurs revenus au sol, input et NavMesh restitues. Maxima sous plafond :
Lucian 0,896 m / ennemi 0,925 m ; lors de l'annulation : 1,232 / 1,678 m.
Ce ne sont ni une campagne d'interruptions repetees ni une validation reseau.

## Sortie et cibles detruites (2026-10-18)

FinishCombatExit verifie SkillTargetingContract.Exists avant de consulter les
menaces ou restituer leur suspension. Une interface C# conservee ne prouve pas
que le composant Unity existe encore. Exists ne teste ni sante ni activation :
un ennemi mort/inactif encore present doit recevoir son nettoyage. Alive reste
le filtre d'eligibilite aux attaques. La sortie doit atteindre la suppression
des menaces, la restitution des controles et sa notification meme apres despawn.
Preuve : 64 tests ContactContinuity/EngagementLedger/FreeAttack passent, dont
destruction de composant seul et d'objet entier, plus mort inactive. Relecture
reelle CombatSkillFeelExitReplayFinal.log : plus de MissingReferenceException
de fermeture. La recette reste globalement refusee pour un contact manque de
Bond ; ne pas confondre le resultat de l'attaque avec celui du nettoyage.

## Reactivite et poids des frappes (2026-10-18)

Le flux commun d'attaque libre soumet la premiere Basic dans la frame de l'input.
PlayerActionPresentationController conserve seul la prochaine Basic, y compris
avant l'ouverture de chaine ; le depart reste borne par chainTransitionNormalizedTime.
Une seconde pression en reserve est refusee. Le lancement revalide sol/air,
resout l'intention courante et notifie ses couts/sons au demarrage, pas au buffer.
Mort, interruption et annulation nettoient toujours la commande en attente.
La queue historique reste uniquement pour les anciennes configurations sans
runtime libre. Les demandes reseau passent toujours par l'autorite existante.

Les entrees d'action utilisent CrossFadeInFixedTime et un offset zero :
entryBlendSeconds ne varie plus avec la longueur du clip et une repetition
du meme etat recommence bien le geste. Les timings de contact et d'annulation
ne changent pas. Le validateur d'events couvre cette duree fixe.

Premier reglage de poids reserve au laboratoire : hit-stop 0,04 / 0,05 / 0,07 s
pour les trois Basics, sans effet camera additionnel ni changement de degats.
Profils CombatLab et copies Clash mis en coherence avec l'installateur.
Il s'agit d'un candidat de ressenti, pas d'une valeur mesuree dans God of War
ni d'une validation manuelle. Inspiration primaire :
https://www.gdcvault.com/play/1026423/Evolving-Combat-in-God-of
https://blog.playstation.com/2022/10/04/game-developers-explain-what-makes-god-of-war-2018s-combat-tick/

Preuve ciblee : CombatFeelResponsivenessFinal.xml, quatre UnityTests passes,
zero echec. Buffer sans cible de laboratoire, transition au timing auteur,
slot unique et annulation, fondu fixe/redemarrage, et recovery historique
avec pause/Animator desactive. Cette fixture ne mesure ni le rendu du vrai
avatar ni le ressenti des trois nouveaux hit-stops. Verification manette requise.

Retour auteur : BasicSkills relativement satisfaisantes, sans gain marque percu.
Reglage conserve ; priorite suivante aux deplacements et impacts des skills.
RunBasicFeelBatch du validateur FreeAttack ouvre uniquement le laboratoire dans
un Editor batch isole, sans sauver les assets. Le rapport mesure presses X,
departs, contacts, PV et echelles avec vrais acteurs. Essai courant NON passe :
trois departs a partir d'inputs, premier a +1 frame, deux contacts de 5/10 PV,
troisieme manque. Un clip peut sortir la victime de l'arc par son deplacement
lateral ; cette recette ne prouve pas trois impacts garantis ni le nettoyage
final, dont les etapes ne sont pas atteintes. Logs et rapport diagnostic sous
Library/CombatFeel ; aucune gate de migration n'est alimentee par cet essai.

## Impulsion et contact Eclair/Bond (2026-10-18)

Rupture conserve son impulsion et son clip protege. La recette baseline mesure
un recul de 7,411 m, une montee de 1,049 m, un seul impact de 15 PV, mais environ
1,3 m de glissement apres atterrissage. Le profil de session peut activer
retreatLandingDeceleration (60 m/s2 au laboratoire, 0 par defaut). La presentation
attend un depart en l'air reel avant de freiner l'elan au sol, uniquement pendant
la generation qui l'a produit. Le bridge applique une force opposee a la seule
composante de vitesse sur l'axe du recul ; pas de correction verticale ni de
Transform, ni de raccourcissement de la protection auteur. Aucun changement reseau.
Le freinage est cadence en FixedUpdate. Un faux Grounded de decollage ne suffit
pas : elevation montante d'au moins 0,1 m requise avant l'atterrissage. La recette
CombatRuptureFeelBrakingFinal.log passe : montee 1,083 m, recul 4,479 m, glissement
au sol 0,133 m, impact unique 15 PV et restitution finale. Essai precedent refuse
pour detection de decollage ambigue ; pas de qualification manette, murs ou reseau.

ContactDashMotion.Burst est un reglage explicite du profil de session,
active seulement dans Iluvilirae_Test. Approach reste la valeur par defaut.
Le meme mouvement planaire UCC suit une distance temporelle integree :
acceleration 30 ms maximum, freinage 60 ms maximum, croisiere entre les deux.
La duree respecte le plafond auteur et la distance sure calculee. Aucune
teleportation, aucun homing nouveau, aucun changement de portee ou de degats.
Les contacts restent reels, uniques et soumis aux generations existantes.
Pour ce mode, les VFX/audio d'impact utilisent le contact horizontal rencontre,
et non le centre de la cible ; Y reste joueur + 1,5 m. Le VFX est non parente
pour ne pas etre emporte par la victime apres le contact. Pas de nouveau
shake, son, ralenti ni changement des fenetres de contre.
Les 50 tests FreeAttack passent, incluant vitesse maximale, distance exacte,
rampe initiale/finale et hauteur de feedback. Ils ne prouvent pas le ressenti
ni les collisions en jeu ; RunSkillFeelBatch exerce les acteurs reels sans
sauver les assets auteur. Recette executee : Eclair +3,943 m derriere l'origine
cible, Bond devant la cible, un impact de 10 PV chacun, ressources de mouvement,
collisions et temps restituees. Logs/CombatSkillFeelPlay.log. Deux exceptions
de reference detruite JuggernautV2Brain apparaissent lors de la fermeture Editor
dans FinishCombatExit : ne pas qualifier le cycle global de destruction a partir
du code de sortie 0. Ressenti manuel et reseau restent non valides.
L'executeur reseau garde son comportement precedent.
Retour auteur : Eclair et Bond paraissent tous deux meilleurs, reglages conserves.
La relecture de sortie montre toutefois Bond arrete a 0,891 m devant la cible
sans contact ni degat. Ce cas reste ouvert ; pas de degat garanti ou de secours
ajoute pour satisfaire la recette. Le correctif de fermeture est decrit plus haut.
Relecture stationnaire corrigee : l'etourdissement peut remplacer hurtRemaining
et liberer le cerveau apres la mise en place initiale. La fixture maintient
explicitement son immobilisation et refuse une derive cible de plus de 3 cm.
CombatSkillFeelStationaryContact.log passe (derive 0,000 m, deux contacts uniques
de 10 PV, sortie 0 sans exception). Aucun changement du runtime ou de portee.
Le manque precedent ne prouve pas un defaut contre une cible stationnaire ; le
cas mobile reste distinct, sans suivi automatique ni degat garanti.

## Raccords communs de fin de migration (2026-10-17)

Les paliers reseau utilisent le moteur ThresholdSequence existant sur l'acteur
ayant porte le contact, avec reservation differee apres l'impact. Le serveur
decide du resultat ; seul le proprietaire recoit prompt et map CombatQTE.
Les autres joueurs conservent leur camera et leurs commandes. Presentation
d'observateurs et nettoyage sont identifies par generation. Aucun ralenti reseau.

Le clash commun parcourt les paires compatibles equipees de volumes auteur,
jamais les capsules corporelles ni une paire derivee du lock. Les impacts Basic
reseau passent dans l'arbitrage avant degats. Recul via les moteurs existants ;
replication cosmetique seulement chez les observateurs. Les clones de clips
production compatibles recoivent les fenetres, pas les originaux.

ProductionEncounterMigration conserve les executants specialises des rencontres.
Les deux GiantJuggernaut ont des profils distincts derives de leurs skills
reellement equipes (Jump et Assomoir), avec provenance dans le rapport auteur.
Ces raccords de code ne constituent pas une validation de leur comportement en jeu.

## Charge et mobilite partagees pendant migration (2026-10-17)

ICombatChargedTarget raccorde Brains et EnemyController a la meme projection/
flottement joueur. Le moteur physique existant de chaque ennemi reste seul
proprietaire de la verticale, des collisions et du retour au sol. La projection
invalide le coup engage et ses evenements ; aucun second coup automatique.
Les actions Brains de production au sol peuvent etre interrompues par cette
projection apres confirmation physique du sol, meme avec leur agent suspendu.
Le clip Rupture n'est plus bouclant ; son impact reste a 0,8333333 s.
Les sessions externes de presentation configurent le SkillSO et ses fenetres
d'annulation/recuperation, sans relire le lock aux impacts. Les VFX utilisent
l'acteur lanceur, y compris pour un acteur distant, pas le joueur local du manager.
Compilation et verifications auteur restent distinctes des tests en jeu differes.

## Continuation auteur et migration (2026-10-17)

La BasicSkill aerienne possede un seul HitEnemy a 0,533333 s, avec les deux
evenements VFX existants conserves. Les recepteurs solo/reseau gardent leur
exclusion mutuelle et l'unicite du contact par generation.
La conversion Brains du Juggernaut reel a ete appliquee/repetee dans la copie
isolee. Apres conversion, Analyze retrouve le controller source dans la requete
persistante uniquement si personnage, profil et controller de destination
correspondent exactement. Une nouvelle generation repart de cette source,
jamais du controller deja converti. Les autres rencontres restent non basculees.

## Finalisation de migration (2026-10-17)

Une fin d'engagement confirmee pendant une cinematique ou la fin d'un dash est
mise en attente, puis restituee meme sur un client qui n'execute pas la perception.
Une nouvelle menace ou un changement d'acteur invalide cette attente. Le lock
d'une cible morte est nettoye independamment de l'autorite de perception.
La restitution conserve une session d'action encore legitime.

Les impacts Brains de production collectent les victimes reelles du volume auteur,
sans compter deux fois un acteur ayant plusieurs colliders. Le montant est capture
au lancement par generation depuis le maximum de degats recus de l'engagement ;
un impact tardif ne recalcule pas ce montant. La riposte de palier conserve son
calcul auteur distinct. Tests Unity de continuite d'impact : 3 passes.

Les impacts historiques ne consultent plus le drapeau cinematique global du
manager pour bloquer tous les combats : ils verifient leur attaquant, leur victime
et les participants effectivement reserves. Les reservations refusent un
proprietaire Behaviour desactive ; leur nettoyage persistant existe seulement
en Play Mode, jamais lors d'une inspection Edit Mode.

Le rapport combat_migration_report.md distingue les chemins implementes des
recettes executees. Aucun resultat d'auteur ni test avec doubles reseau ne
declenche la bascule globale des prefabs de production.

## Continuation de migration (2026-10-16)

CombatWorldRuntime possede un CombatEngagementLedger par acteur. Le serveur
notifie les seules interfaces du participant local via un snapshot complet de
menaces ; changer de cible ennemie ne transfere ni verrou manuel ni victime
d'action. Les impacts transportent leur SquadCharacterController Source jusque
dans les adaptateurs historiques et les paliers. Les reactions visuelles aux
degats viennent de CharacterData.playerSettings.combatPresentation.

IEnemyEncounterActor separe les gates de rencontre du cerveau. Les boss speciaux
et l'introduction/mort du ScientifiqueFou restent sur leur implementation auteur ;
aucune detection globale ne les active par defaut. La conversion Brains conserve
les patterns/SkillSO source, les evenements physiques et le point de lock, sans
remplacer un ennemi de production par une identite de laboratoire.

Les paliers capturent l'auteur du coup franchissant le seuil et refusent une
substitution par le joueur local. Perte du participant avant lancement : nettoyage
et nouvelle eligibilite au prochain impact valide. Le QTE d'un participant distant
n'est pas encore executable : diagnostic, aucun clamp permanent ni faux succes.
Migration globale et recettes de production restent bloquees, voir rapport.

## Migration hors laboratoire (2026-10-16)

CombatWorldRuntime sur BattleManager partage configuration, liaison du joueur
local, impacts et reactions ennemies entre session et laboratoire. Son profil
est explicitement configure, jamais deduit du nom de scene. Aucun actor spawn
ni ecriture de Transform de poursuite dans ce composant. SquadManager et
NetworkCharacterInput restent les pilotes production ; seul un acteur autonome
sans ces pilotes recoit le forwarding de locomotion de la fixture commune.
Les threats par acteur local, le lock manuel et les participants cinematographiques
sont distincts. Mort/fuite d'une menace ne termine pas les autres engagements.
Une fin normale conserve l'action en cours et ses ressources ; changement
de personnage ou sortie technique nettoient l'ancien acteur.
La migration editeur explicite est auditee dans combat_migration_report.md :
ses ecritures auteur ne signifient pas que tous les ennemis/reseau sont valides.
Les reservations de cinematiques bloquent les paires concurrentes et conservent
la victime capturee aux impacts, independamment des changements de lock.
Le clash utilise un binding acteur/ennemi explicite ; le prototype reste limite
aux clips compatibles et a une paire par acteur, sans inventer d'autres fenetres.

## Preparation du ChargedSkill : protection d'interruption

La pose de preparation du laboratoire est la derniere pose de BasicSkill_1 :
ChargedSkillSetup genere ChargedWindup depuis la fin de ClashBasic_0, avec
courbes constantes et aucun event. Pas de gel global de l'Animator. La fin
de Basic sous maintien transfere directement la presentation a la charge,
sans demander Idle/locomotion entre les deux sessions.
ChargedFinalPose.log valide compilation, courbes finales constantes et recette
Play du maintien X avec degats, relachement, remplacement prioritaire et mort.

La preparation est identifiee par son token de session. La protection commence
des la Basic initiale acceptee si X est toujours tenu pour un ChargedSkill,
et couvre le passage a la preparation. Les BasicSkills ordinaires sont exclues.
Elle interdit Hurt et annulations ordinaires, sans invulnerabilite : les PV et
feedbacks de degats restent appliques. InterruptBasicComboOnDamage preserve le
maintien charge et n'annule pas la Basic initiale de ce chargement. Mobilite, garde, nouvelles
attaques et roues ne la remplacent pas. Relachement X, fin de combat, mort et
prises de controle prioritaires gardent leur nettoyage normal. La protection
n'est jamais conservee par une ancienne generation ni une autre action.
L'acteur conserve une liaison vers l'input proprietaire du maintien. La sante
ne relit pas directement le controle physique, dont le buffer depend de la phase
d'update ; callbacks et polling d'input restent responsables du relachement.
ChargedHoldArmorVerified.log valide des degats pendant Basic initiale et preparation,
la pose apres impact, puis les sorties volontaires/prioritaires et la mort.
Compilation et recette Play ciblee reussies dans Iluvilirae_Test isole :
ChargedPreparationArmor.log (degats reels, refus ordinaires, relachement,
presentation prioritaire et mort avec restitution UCC/camera).

## Vesta jouable au laboratoire

Vesta.asset conserve son GUID/identite et son portrait auteur. Il reference
Vesta.prefab et Vesta_Model.controller distincts : base UCC, modele, locomotion
et BasicSkills de Lucian temporaires. combatSkills contient exclusivement
Revitalisation, Soins d'urgence, Regeneration et Siphon vital, dans l'ordre
du catalogue. Aucune LightSkill de Lucian n'est equipee par copie implicite.
Le prefab comporte presentation d'actions, domaine de temps, mobilite,
PlayerRegenerationEffect et relais NetworkCharacterInput existants.

Iluvilirae_Test ajoute Vesta au selecteur (enum append, indices existants
preserves) et la selectionne par defaut. spawnProtectionAlly cree aussi
HealingAlly_Laboratory pour tester les soins cibles. Les valeurs des quatre
skills et le slot de concentration unique sont inchanges. La campagne solo
continue de materialiser seulement Lucian ; prefab disponible ne signifie
pas compagnon physique recrute automatiquement.
Installateur idempotent : Lit > Combat Lab > Configure Vesta Healing Skills.
Play valide soins sur Vesta et son allie, cooldowns, regeneration explicite,
remplacement par siphon reel et nettoyage. Host/client non execute ici.

## Impact de lumiere : quatrieme skill de Lucian

Prototype limite a Iluvilirae_Test. Skill_ImpactDeLumiere utilise un profil
GroundImpact unique : rayon 5 m (diametre 10), 20 degats au sol / 40 en l'air,
recul 1,5 m / 3 m, cooldown 12 s, descente UCC 25 m/s. Ces valeurs sont auteur.
Le bonus aerien est capture au lancement ; aucun impact avant contact sol.
Les paliers et la restitution restent independants de la copie d'animation.
Le contact physique, pas un ancien HitEnemy du clip, autorise un impact unique
par session et ennemi. Revelation, defenses, paliers et affichage des degats
passent par les recepteurs existants. L'onde est un anneau visuel temporaire,
sans shake, ralenti ni collider. Les ennemis sur un autre niveau sont exclus.

Seul Saut de rupture peut etre remplace pendant sa session. Les verrous
appartenant a son action et a son impulsion sont restitues, jamais ceux d'une
cinematique/traversee. Interruption ou absence de contact invalide l'impact,
libere le mouvement propre a la session et conserve la chute UCC.
Rupture soumet son decollage pendant 0,08 s via une ability UCC concurrente
qui ecrit LocalDesiredMovement avant les collisions, comme Jump. Les verrous
d'impulsion sont attribues a la presentation sans desactiver le handler UCC.
Le remplacement annule aussi l'inertie propre a cette impulsion ; aucune
position n'est restauree et aucun autre proprietaire de verrou n'est libere.
Le recul Brains utilise son NavMeshAgent sans annuler une attaque engagee.
Le recul EnemyController passe par NavMesh en navigation et par un delta
additif borne/collision-aware dans son moteur physique pendant une action,
sans annuler le coup ni prendre la verticale.

Lucian_ImpactLab.prefab, controller et CharacterData sont des copies dediees.
Le clip provisoire est une copie InPlace de Rupture sans Animation Events,
remplacable dans le SkillSO/controller du laboratoire. Le catalogue reference
le nouveau skill dans le slot droite de Lucian : seize skills au total.
Installateur : Lit > Combat Lab > Configure Impact de lumiere.
Lancement reseau refuse explicitement tant que son RPC serveur n'est pas livre.

## Saut de rupture : animation protégée

Depuis le 2026-10-16, Saut de rupture exige une cible ennemie compatible a
0-3 m horizontalement (LockedTarget + portee de lancement explicite). Le lock
manuel est obligatoire, meme au laboratoire : aucune acquisition temporaire.
La portee d'impact reste 3.25 m, les degats 15 PV et le stun confirme 1 s.
L'event conserve son timing : recul UCC oppose a la direction capturee au
lancement, une seule impulsion par generation. Dans le flux du laboratoire,
un ennemi qui evite le contact apres le depart ne supprime plus l'evasion ;
seuls les degats/feedback exigent un contact valide. Annulation et synergie
avec Impact de lumiere conservent leurs nettoyages existants.
Compilation et Play cible reussis dans Iluvilirae_Test isole : refus hors
melee/sans cible sans cout, impact/stun unique, recul confirme aussi sur coup
manque, restitution du mouvement et nettoyage mort. Log RuptureEscapeValidationFinal.log.
Apres exigence du lock, RuptureLockedValidation.log valide le refus sans lock
et hors portee sans cout, puis le lancement verrouille et les memes nettoyages.

Saut de rupture applique stunSeconds=1 sur son impact confirmé, par le même
contrat de stun qu'Éclair (legacy et ICombatStunReceiver). Un lancement ou
une attaque ratée n'assomme pas la cible ; la mort conserve sa priorité.
RuptureArmorPlayValidation vérifie aussi l'impact unique, l'absence de stun
avant le contact, l'immobilisation et la récupération à 1 seconde, IA active.
Compilation/Play réussis : Library/ActorAnimationSandbox/Logs/RuptureStun2.log.

Saut de rupture ignore le remplacement par Hurt pendant sa session d'animation,
via presentation.allowDamageInterruption=false. Les PV et feedbacks de dégâts
restent inchangés ; aucune invulnérabilité. Mobilité non annulable et récupération
à normalizedTime=1 empêchent esquive/garde/autre skill de couper le clip.
lockMovementDuringAnimation acquiert un verrou d'input UCC dès le lancement
jusqu'à la fin de session, avant comme après l'impulsion de recul. Le saut,
la gravité, le recul et les collisions restent physiques. Le nettoyage de
génération libère uniquement ce verrou, aussi en cas de mort ou désactivation.
Le handler UCC reste actif au lancement pour autoriser l'impulsion de recul ;
le verrou externe suffit à neutraliser le stick. Play ciblé validé avec stick
maintenu, recul conservé, reprise du mouvement et nettoyage à la mort.
Log : Library/ActorAnimationSandbox/Logs/RuptureMovementFinal.log.
La mort, les interruptions techniques et la désactivation gardent leurs nettoyages.
Le flag est true par défaut : les autres skills conservent leurs réactions.
Validation ciblée : RuptureArmorPlayValidation.RunBatch dans Iluvilirae_Test.
Compilation et Play passent : trois impacts de 7 PV pendant le clip, pas de
Hurt ni d'annulation mobilité, réaction normale restituée puis mort prioritaire.
Log : Library/ActorAnimationSandbox/Logs/RuptureArmor2.log.

## Progression de l’équipe solo (2026-10-13)

SoloTeamProgression, hébergé sur le SquadManager persistant (ou le manager du
laboratoire autonome), détient les recrutements et les slots déverrouillés.
Une campagne commence avec Lucian et ses trois skills existants. Vesta/Soren/
Aurell deviennent des supports UI après RecruitMember : leur premier slot (gauche) est
automatiquement disponible, les slots suivants nécessitent UnlockSkill.
Le catalogue reste une définition complète et ne porte aucun état de partie.
Les quatre positions gauche/haut/bas/droite restent fixes ; les slots verrouillés
sont affichés et refusés aussi par TryUseSkill et les palettes historiques.
GetOwnedSkill respecte la progression, notamment pour la réserve de Juste Retour.

SoloTeamStoryEvent expose les deux actions aux UnityEvents narratifs existants.
L’UI affiche nom/portrait/Support sans PV pour les recrues ; une file locale
d’annonces temporaires informe du recrutement sans capturer les contrôles.
Vesta.asset reference desormais son prefab jouable propre ; son modele reprend
temporairement celui de Lucian et son portrait auteur reste conserve.
Le roster physique solo contient uniquement Lucian, y compris Maison et
restauration. Les anciens swaps/followers sont inactifs dans ce mode.

CharacterSaveData v8 contient soloTeam, distinct du roster réseau. La migration
sans ce champ reprend uniquement les IDs du catalogue présents dans squadIds,
avec le premier slot ; Mia/Luna/Link ne sont jamais convertis. Les états historiques
restent conservés. Une restauration n’annonce pas les recrutements anciens.
SoloTeamTestProfile fournit un état explicite de laboratoire et refuse toute
application lorsqu’une sauvegarde de campagne est active. Il est activé dans
Iluvilirae_Test, dont le personnage par défaut est Lucian : les quatre kits sont
disponibles immédiatement (16 skills avec Impact de lumiere au laboratoire), sans compagnon
physique. RunBatchSceneProfile vérifie cette configuration réellement chargée ;
le test de recrutement retire le profil uniquement de sa fixture non sauvegardée.
Les points narratifs
de recrutement restent à configurer dans les séquences ; aucun lieu n’est inventé.
Validation : compilation et 21 tests Edit Mode passent (SoloTeamProgressionTests
et DirectionalSkillKitTests) ; Play Iluvilirae_Test passe avec et sans Domain
Reload, profil complet activé explicitement après les contrôles de verrouillage.
Logs SoloTeamFinalTests.xml, SoloTeamPlay.log et SoloTeamPlayNoReload.log dans
Library/ActorAnimationSandbox/Logs. Parcours campagne, HUD/manette physique et
host/client restent des validations distinctes non effectuées dans ce lot.

## Kits directionnels solo (2026-10-13)

Roue fermée : la croix choisit le kit. Roue ouverte : gauche/haut/bas/droite
lancent les slots 0/1/2/3 du kit, sans changer de personnage. B ferme toutes
les roues sans lancer de skill ni d'esquive. CombatSkillKitInput conserve un verrou de pression
physique entre les maps pour ignorer leur initialStateCheck sur un D-pad maintenu.
CombatWheel active uniquement Player/Move et Player/RightShoulder (sprint), en
plus de ses propres commandes. IsLocomotionAllowed et la réconciliation du
stick maintenu couvrent ce mode ; Iluvilirae_Test applique également le mouvement.
Les boutons de face n’activent jamais leurs actions habituelles roue ouverte.
Les modals et cinématiques gardent leurs règles de blocage.

CombatSkillKitCatalog (Resources/CombatSkillKits/Default) definit les slots
stables gauche/haut/bas/droite (anciens X/Y/A/B, masques de sauvegarde inchangés).
Haut = Lucian (Bond offensif, Eclair, Rupture, Impact de lumiere) ; droite = Vesta
(Revitalisation, Soins d'urgence, Regeneration, Siphon vital) ; bas = Soren
(Fleche, Revelation, Entrave, Pas de recul) ; gauche = Aurell (Intercession,
Rempart, Sommation, Juste Retour). Seize skills, quatrieme Lucian configure au laboratoire.
Une pression ouvre le kit puis la suivante lance un skill ; les diagonales
sont ignorées. B ferme la roue ; en combat libre LT ouvre le dernier kit.
La map CombatWheel + System conserve marche/sprint mais exclut les actions
de face habituelles pendant la sélection. Une tentative
refusee garde la roue et affiche une raison ; aucun fallback attaque/esquive.
Les boutons maintenus doivent etre relaches avant de retrouver leurs actions
de combat normales. Mort, changement d'acteur, modal, cinematic, disable et
reset d'asset nettoient l'owner ; aucune suppression UCC supplementaire.

Le personnage incarne lance les skills empruntes avec ses PV, cooldowns et
ressources. Aucun acteur cache n'est cree. La reserve d'Aurell est accessible
a Lucian pendant le combat ; l'emplacement passif unique est conserve.
Regeneration peut cibler Lucian lui-meme uniquement hors session reseau ;
IsValidAlly reste strict et le serveur refuse toujours l'auto-ciblage multi.
Player_Model ajoute seulement l'etat InPlace Soren_PasDeRecul, avec son clip
deja auteur ; Juste Retour conserve son override temporaire de BasicSkill_3.
Lucian.asset ne contient plus que Bond offensif, Eclair et Rupture.

Install : Lit/Input/Install Directional Skill Kits. Verification :
DirectionalSkillKitTests (Edit Mode), DirectionalSkillKitValidation (Play).
Onze tests Edit Mode passent. Les parcours Play avec et sans Domain Reload
passent : quatre kits, exclusivite de face, slot vide, ticks de regeneration,
reserve/impact auteur de Juste Retour, Pas de recul, Bond offensif, R3 tap/hold,
LT et restitution apres modal/fin de combat. Logs DirectionalSkillKit*.log/xml
dans Library/LucianCombatValidation/Logs. Ressenti manette et scenes de jeu
restent a verifier manuellement.
Les scripts d'import GLTF/Soren non lies sont isoles puis restaures dans le
sandbox. Repartition des kits a 2/3 joueurs et host/client non livres ici :
le reseau conserve ses loadouts et droits existants, sans privilege implicite.

## Soren : lucidite et controle (laboratoire)

Lit > Combat Lab > Configure Soren Lucidity Skills cree CharacterData/prefab/
controller distincts dans Characters/1_Squad/Soren et trois SkillSO dans
Resources/CombatControlSkills. La Fleche existante reste partagee (10 PV,
0-30 m sans distance minimale, stun immediat 2 s, cooldown 10 s). Aucune reference de production
n'est retiree. Iluvilirae_Test expose character = Lucian/Aurell/Soren ;
Soren est selectionne. Les modes speciaux equipent leur propre kit seulement.

Revelation : 25 m/6 s/+20%/20 s. Ennemi verrouille requis, aucun cumul,
rafraichissement et generation monotone. Application avant defenses/paliers.
CombatImpact conserve le flag DamageModifiersApplied pour eviter une seconde
application ; Passive exclut les ticks de siphon. Degats environnementaux et
morts explicites ne passent pas par le bonus d'attaque.

Entrave : 20 m/4 m/5 s/-60% navigation/25 s (40% de vitesse restante). Projection de sol locale avant
consommation ; cercle fixe sans collider ni rebuild. Les zones se composent
par le plus fort ralentissement, pas par multiplication. Les adaptateurs
Legacy et Brains AI ralentissent les intentions de navigation, conservent
l'allure demandee et adaptent la cadence a la vitesse reelle. Aucun changement
d'Animator.speed, de temps local, de sauts, ruees ou timings d'attaque.
Asset et SorenSkillSetup partagent le réglage 60%. SorenPlayValidation vérifie
x0,4 (3,6 → 1,44), la frontière, le non-cumul et l'expiration. Compilation/Play
réussis : Library/ActorAnimationSandbox/Logs/Entrave60Percent.log.

Pas de recul : 5 m/0,25 s/8 s, direction monde capturee dans le mode courant ;
sans input, loin de la cible ou vers l'arriere sans cible. Session externe
PlayerActionPresentationController et slot planaire UCC propres. Reutilise
les controles capsule/sol/BattleWall d'Eclair sans ignorer une collision.
Clip d'evasion InPlace dedie, sans Animation Events d'invulnerabilite. Nettoyage
identifie, restitution selon l'intention actuelle, aucun Transform deplace
directement. References rafraichies au lancement (presentation ajoutee apres
Awake possible). Le serveur valide cible, equipement, cooldown et trajet ;
les clients affichent les effets confirmes et la pose reseau, pas un second
moteur physique. Mort/desactivation/desengagement retirent seulement les effets
du lanceur concerne.

## Fusion automatique avec l'ange gardien (2026-10-13)

Le combat possede la fusion du seul personnage incarne via
RealTimeCombatManager.SpiritFusion. Entree classique, aggro et laboratoire
activent le SpiritBond avant les inputs ; fin, desactivation ou destruction
du manager le liberent. Aucun personnage d'equipier n'est cree en solo.
Un bond de presentation est ajoute au personnage si son prefab n'en contient
plus ; cela ne restaure ni Munin jouable ni son ancien gameplay.
La fusion automatique ne prend pas de verrou moteur/camera et ne depend pas
d'un AnimationEvent. LightSkill conserve un overlay cinematographique, puis
retrouve la fusion du combat. Les anciennes commandes de fusion manuelle
et les confirmations tardives ne peuvent plus inverser cet etat.
Le raccord local passe les tests Play Iluvilirae_Test avec et sans Domain
Reload (CombatSpiritFusionValidation). L'assise automatique est neutralisee
dans ce test seulement ; ses verrous restent independants. Les scripts
d'auteur Soren/GLTF sont exclus temporairement du sandbox, puis restaures.
La regression visuelle en scene de jeu et la replication host/client restent
a valider ; la repartition des kits n'est pas implementee ici.

Direction de design : en solo Lucian seul est visible et accede aux quatre
kits. A deux joueurs : deux kits chacun. A trois : deux/un/un. Chaque joueur
incarne un personnage physique independant ; posseder un kit ne fait pas
apparaitre son personnage en solo. Kits solo/R3 : voir le raccord directionnel
ci-dessus ; la repartition reseau reste distincte.

## Aurell : protection et justice (laboratoire)

Configuration : Lit > Combat Lab > Configure Aurell Justice Skills.
CharacterData/prefab distincts dans Characters/1_Squad/Aurell ; quatre SkillSO
dans Resources/CombatSupportSkills. Iluvilirae_Test expose le selecteur character,
aurellPrefab et spawnProtectionAlly. Le mode Aurell n'ajoute pas les skills de Lucian.
La selection d'allie existante dans la palette accepte Intercession et soi.

Intercession : transfert 75%, reduction personnelle 50%, 4 s, lancement 20 m,
rupture 30 m, cooldown 25 s. Rempart : 30%/6 m, sans cooldown, toggle, exclusif
avec regeneration/siphon. Les reductions distinctes se multiplient ; une seule
aura est retenue. Sommation : 15 m/6 s/30 s, appliquee a la prochaine decision.
Juste Retour : 0-3 m/10 s, reserve seule plafonnee a 50% PV max. Son clip InPlace
dedie remplace BasicSkill_3 pour une session seulement.

CombatProtectionDamage precompute les parts avant d'ecrire les PV, sans transfert
recursif ni seconde garde. Les montants evites sont credites une seule fois,
jamais les montants simplement transferes. PlayerGuardState appartient a la
victime. SetHealth, couts de PV et morts explicites bypassent les protections.
Les credits de reserve suivent la perte evitee apres l'arrondi final des parts,
pas une reduction fractionnaire qui n'aurait sauve aucun PV.
Le hook OnHealthBeforeDamage dans le package Opsive local permet les dangers
UCC avant perte de PV ; LitUccDamageBridge ignore les degats deja resolus par
Lit pour eviter une double reduction.

PlayerProtectionEffects nettoie liens/labels/concentration a mort/desactivation.
Fin de combat vide seulement la reserve, pas Rempart. Aucun root motion ni
prise de camera. Juste Retour appartient a une generation de presentation :
impact unique, aucun degat de secours. NetworkCharacterInput.Protection valide
skill equipe, cibles et cooldowns serveur ; replique protections/PV/reserve et
presentation de frappe. Host/client reste a valider avant migration production.

## Clash experimental du laboratoire

Uniquement Iluvilirae_Test : CombatLabClashController expose compatibilite,
centres/rayons des volumes d'attaque (distincts des capsules), recul, recuperation,
prefab VFX, audio et diagnostic. Les copies de clips sont dans Iluvilirae/Clash.
OpenAttackClashWindow precede l'impact de 0,08 s ; CloseAttackClashWindow le suit
de 0,06 s. BasicSkills Grounded contre Attack et Attack_Followup. Sweep et
attaques aeriennes/cinematiques exclus. Deux fenetres actives sont requises.

Le balayage relatif des spheres et l'arbitrage en fin de frame neutralisent les
deux generations avant de livrer les degats collectes. Les checks de contact,
garde et esquive restent captures a l'Animation Event. Aucun degat deja livre
n'est annule retroactivement. Aucun fallback production ne passe par ce controleur.
Le callback ExternalImpactArbitrator est installe au spawn laboratoire puis retire
au nettoyage. Les feedbacks ordinaires attendent la confirmation effective.

La presentation joueur possede le recul via une session externe avec nettoyage
enregistre et slot planaire UCC dedie. L'ennemi annule son action et deplace son
agent avec sondage obstacle/bord NavMesh ; reprise de decision apres recuperation.
Les verrous externes ne sont jamais liberes. Mort/cinematiques restent prioritaires.
Reinstallation : Lit > Combat Lab > Configure Attack Clash, sans doublon d'events.

## Audio et epee du CounterSkill laboratoire

CombatLab_CounterHitVfx utilise quatre signaux SpawnTimelineHitVfx(GameObject)
aux contacts auteur. CombatCinematicRig exige une session active, utilise
ResolvePlayerImpactPosition (Y joueur + 1,5 m) et gere le nettoyage au pool
et a la desactivation. Le prefab d'etincelles est fini, non scale, sans
shockwave. Aucun degat, hitstop ou feedback camera additionnel.

CombatLab_CounterSkill_2 utilise des SignalEmitter distincts pour les slashs,
la voix et ShowTimelineSword/HideTimelineSword. Les sons de contre existants
restent inchanges. CounterAudioSetup recree uniquement ses marqueurs, sans
dupliquer les reactions du receiver runtime. Les AudioClipSO sont externes
au rig, non loop/non scaled ; PlayVoice utilise le volume voix d'AudioManager.
L'epee appartient a la generation de presentation qui l'a affichee ; la
desactivation du receiver nettoie cette visibilite sans toucher une nouvelle
action. Configuration limitee au laboratoire, pas de migration production.

## Repere tactique preserve (2026-10-10)

Correction de regression : en camera tactique, le lock conserve le repere
camera-relative et le yaw simule par UCC. La conversion radiale ne doit pas
remplacer ce comportement ; elle reste reservee au lock third-person.
Le rendu des transitions course/strafe reste non valide en jeu.

## Curseur de lock (2026-10-10)

Iluvilirae_Test utilise desormais un prefab monde explicite configure sur
LucianBrainsCombatArena : CombatLab_LockCursor, offset (0, 3, 0) depuis le root.
Le visuel suit la cible, fait face a la camera, sans Canvas/projection/collider.
Il est masque au delock et detruit avec l'acteur. Production non migree ;
le fallback UI reste inchange pour les autres chemins.

Le chemin ICombatTarget (BeginExternalEncounter/ToggleExternalLock) pilote
egalement CombatLockIndicator sur son ActorRoot ; EndCombat le masque.

SetLockPresentation pilote le contour et CombatLockIndicator. Ce dernier cree
une fleche UI non interactive si aucun visualRoot auteur n'existe ; elle suit
le sommet du SkinnedMeshRenderer avec un offset configurable et garde une
taille constante a l'ecran. Seule la cible du lock local est affichee, sans
modifier le gameplay. Hors champ/delock/desactivation : masquage ; destruction
de l'acteur : liberation du Canvas, Sprite et Texture.

## Mobilite sous lock (2026-10-10)

Marche : orbite cible-relative et face-a-face. RT + mouvement : course libre
camera-relative, sans abandon du lock. Le bridge resout ces modes pour les
inputs, la presentation et le yaw UCC ; aucun mouvement direct du Transform
n'est ajoute. Vitesses, acceleration, sprint CharacterData et collisions restent
ceux du moteur existant. Les actions prioritaires gardent leurs verrous.

Toutes les roulades dirigees utilisent la presentation avant et leur direction
monde capturee ; seules les roulades sans direction restent arriere. La fin
physique de l'impulsion ne restitue pas prematurement le facing : la session
de presentation doit terminer ou etre annulee. Reprise sprint tenu sans retour
intermediaire vers la cible. Le laboratoire transmet maintenant RT avant Move.

Validation ciblee disponible : Lit > Combat > Validate Free Sprint in Iluvilirae.
Le rapport est ecrit dans Library/CombatFreeSprint.result. Le test utilise le
prefab Lucian, UCC et une manette virtuelle pour huit directions, conservation
du lock, relachement du trigger et roulade diagonale suivie du sprint.

## Regle d'integration des evolutions du combat

Toute amelioration, modification ou correction du systeme de combat doit
d'abord etre integree dans `Iluvilirae_Test`. Verifier son fonctionnement dans
ce laboratoire, ainsi que les regressions sur les commandes, les actions et
la restitution du controle. La validation inclut les tests pertinents et un
essai jouable dans Unity ; une compilation seule ne suffit pas.

Une fois le comportement valide dans `Iluvilirae_Test`, migrer les changements
dans le jeu, puis verifier leur integration dans les scenes de gameplay.
Les composants partages peuvent evoluer pendant cette premiere etape ;
le laboratoire reste le premier lieu d'integration et de validation.

## Prototype Iluvilirae avec Brains AI

Le socle `LitBrainsEnemy` raccorde Brains AI a la perception Lit et au contrat
`ICombatTarget`, sans ajouter EnemyController aux prototypes. Les ennemis
historiques conservent leur comportement. Iluvilirae reste un prototype de
perception ; `Iluvilirae_Test` est maintenant le laboratoire de combat avec
le vrai Lucian/UCC, Juggernaut_v2, les inputs PlayerInputs, clarte, garde,
esquive, palette, LightSkill et contre cinematographique. Reseau, sauvegarde
et recompenses restent hors de ce laboratoire.

### Acquisition et poursuite Brains AI

`LitBrainsEnemy` distingue le premier reperage de la poursuite. Une cible
inconnue doit satisfaire le cone, la distance et la ligne de vue de
`CharacterData.vision`. Une cible connue reste suivie a 360 degres, y compris
derriere un obstacle : le NavMeshAgent contourne cet obstacle sans placement
force. La distance reste celle entre les points eyeHeight/targetHeight, avec
maximumDistance existant (Juggernaut_v2 : 30 m ; Iluvilirae : 14 m).

Mort, inactivite, disparition ou sortie de portee liberent la cible. Changer
de joueur exige un nouveau reperage normal. La desactivation efface aussi
la derniere cible et son investigation ; pause, Knocked Out et cinematographie
conservent le suivi. DetectionReason distingue reperage, poursuite et perte.

Avec ou sans profil, le depart d'une attaque et son impact exigent une ligne
de vue utilisant les couches d'obstruction de la vision. Un contact bloque
est consomme, sans degats differes si l'obstacle disparait. L'identifiant
d'attaque empeche les doubles impacts, y compris pendant les transitions
Animator. Vitesses, cadence, marqueurs et verrouillage de Strike sont conserves.
Les ennemis historiques et les sauvegardes ne changent pas.

### Profil fluide de Juggernaut_v2

`LitBrainsCombatProfile` porte portee, orientation, cadence, resistance et
marqueurs des trois clips. Lit pilote seul Speed/MoveX/MoveY depuis la vitesse
reelle, dans le point d'extension UpdateAnimator de Brains AI ; aucun second
pilote LateUpdate. Le NavMeshAgent possede locomotion et avance de frappe.
Preparation suit la cible a 540 deg/s et avance jusqu'a 1 m pendant le geste.
Strike verrouille la direction puis charge jusqu'a 0,9 m sur 0,2 s, avec un
freinage progressif et obstacle physique/NavMesh. L'avance est bornee par
la distance utile au joueur : aucune poursuite laterale pendant Strike.
Recovery dure 0,2 s apres son marqueur puis rend la locomotion en 80 ms.
La detection et le delai d'attaque continuent pendant les actions ; pause
et cinematographie les suspendent. Delai entre debuts : 1,5–1,9 s ; depart
possible a 3,6 m et 55 deg, sans passage prealable par Idle. L'attaque
initiale peut demarrer apres 0,2 s. Pendant le delai, la poursuite continue
jusqu'a 1,7 m : la zone d'attente 2,2–2,6 m est supprimee. Un chemin
invalide ou bloque une seconde est recalcule sans teleportation.

Chaque action a une identite et un impact unique. Les marqueurs (secondes,
issus des poses inspectees, clips a 30 images/s) sont Preparation/Strike/Impact/Recovery :
Strike 0,233/0,467/0,567/0,733 ; Sweep 0,333/0,567/0,700/0,900 ;
Followup 0,267/0,500/0,600/0,767. La zone de contact est un secteur auteur,
avec hauteur et obstacle, visualisable par gizmos ; elle ne simule pas une
collision physique continue de la lame.

Le contrat optionnel ICombatImpactReceiver ajoute force d'interruption et
origine aux degats ; ReceiveDamage(int) reste compatible avec le systeme
historique. Resistance 100 ; petits coups 20/30/50, LightSkill/contre 100.
Une seconde sans impact lance la recuperation a 50/s. Une rupture joue
Hurt 0,35 s, puis protege des interruptions 0,75 s ; le contre est prioritaire.
La reaction deja jouee par une Timeline n'est pas remplacee par Hurt.
Mort annule la frappe, coupe navigation/colliders puis desactive l'acteur.

### Commandes et presentation du laboratoire

Les demandes repetees de LockDeathAnimation sur le meme etat sont idempotentes :
elles conservent le temps du clip au lieu de recommencer la chute de Lucian
a chaque frame. La pose terminale reste verrouillee jusqu'au redemarrage.
Juggernaut arrete navigation et attaques a zero PV, joue Death, puis se
desactive apres le delai auteur et la fin effective du clip. Une mort pendant
une cinematique demarre sa presentation lors de la restitution de l'Animator,
avec un nouveau delai complet ; elle ne disparait pas pendant la Timeline.
LucianDeathPlayValidation verifie les vrais acteurs dans Iluvilirae_Test,
la progression des clips et des os, les interruptions, la restitution apres
cinematique et le redemarrage. Ses captures utilisent des materiaux neutres.

Les copies CombatLab_BasicSkill_1/2/3 sont raccordees explicitement par
SkillsManager.SetGroundBasicSkillOverride ; les assets de production restent
inchanges. La presentation possede la seule attaque suivante en attente :
son animation, skill d'impact et son de depart changent au debut reel, pas
lors de la mise en attente. Esquive, blessure, mort, cinematographie et changement
sol/air eliminent cette attente. Une esquive non annulable reste memorisee
150 ms maximum et a priorite sur le prochain coup. Le maintien de X est conserve.

Aucun ralenti automatique a l'anticipation dans le laboratoire. Une esquive
parfaite acceptee utilise 0,85 pendant 150 ms ; les contres gardent leur
Timeline. Pauses d'impact des trois attaques : 25/35/50 ms, sans accumulation.
Degats, VFX de contact, son et pause d'impact partent du contact confirme.
La camera du contre vise les torses et garde 1,8 m de separation des acteurs ;
les anciens rigs sans cette configuration gardent leur cadrage.

L'assistant Upgrade Lucian Combat Laboratory conserve les profils et copies
existants, puis reconcilie les evenements depuis les marqueurs auteurs.
showCombatDiagnostics est desactive par defaut sur l'arene ; il affiche phase,
vitesse, delai, resistance, dernier contact et facteur temporel. Les outils
LucianFluidCombatValidation et LucianFluidCombatTests verifient ce socle dans
un Editor isole avec les vrais acteurs et assets.

Validation du 2026-10-08 : 57 tests EditMode passes, compilation runtime/editeur,
parcours InputSystem/gamepad simule complet et controles de resistance,
cadence, impact unique, obstacles, huit directions et buffer de 150 ms.
Le contre restitue temps/inputs/navigation et reprend un stick maintenu.
Les poses de Juggernaut ont ete inspectees en rendu neutre pour les marqueurs.
Le rendu HDRP, les pieds en transition et le ressenti sur manette physique
restent a confirmer en Play Mode dans l'editeur ; ces tests ne les remplacent pas.
Revalidation du 2026-10-09 apres elargissement de l'engagement : 57 tests passes,
parcours gamepad complet et charge depuis 3,5 m avec fermeture de distance
et budget de deplacement respecte. Les marqueurs de contact restent inchanges.
Le ressenti et le rendu du nouvel elan ne sont pas valides par le batch.

## Sessions joueur et continuite

PlayerActionPresentationController est le proprietaire de la generation d'action. RegisterActionCleanup et RegisterActionTermination enregistrent uniquement des ressources de la generation courante. TerminateAction invalide avant nettoyage ; Completed, Interrupted, Failed, OwnerDisabled et Death convergent vers ce chemin. Le remplacement valide l'etat avant de terminer l'action precedente, sans restitution intermediaire de locomotion.

La surveillance lit l'etat courant et suivant : sortie externe, Animator desactive, entree impossible et absence de progression pendant une seconde locale effective declenchent un nettoyage cible. Les pauses globales/locales ne consomment pas ce delai. Une autre animation deja proprietaire n'est pas remplacee par Idle ; en l'air, la gravite et UCC restent prioritaires. Aucun impact de secours n'est ajoute par cette recuperation.

Les sessions de garde et cinematiques s'enregistrent avec leur proprietaire. Les services de paliers/Timeline gardent leurs fins et watchdogs specifiques ; une interruption de session appelle leur annulation. La mort ne reprend pas une attaque bufferisee. Une anomalie annule les BasicSkills en attente et exige relachement puis nouvelle pression ; les demandes de mobilite existantes expirent toujours selon leur echeance initiale.

PlayerCombatAnimationEvents conserve les noms et parametres auteur mais recoit AnimationEvent pour verifier clip et etat emetteurs. Les evenements d'un etat sortant, d'une session terminee ou d'un fondu vers le meme etat sont refuses. Les cinematiques verifient aussi la piste Player et le clip actif ; le QTE de palier verifie le clip de l'etape. Les timings places dans un fondu ambigu doivent etre deplaces par l'auteur ou utiliser des etats distincts.

Validation ciblee : CombatExplorationRecoveryTests ; PlayerActionContinuityRuntimeTests. La validation d'un combat complet District 1, des transitions de palier et des cinematiques reste indispensable en plus des tests unitaires.

## Presentation du joueur modulaire

PlayerCombatAnimationEvents est un routeur sur l Animator. Les handlers sont dans PlayerActionPresentationController.SkillEvents, avec des noms distincts pour ne pas recevoir deux fois les memes evenements. Le dash et son freinage sont dans CombatMobilityController.AnimationDash, avec capture du personnage et annulation propre. PlayerAnimationController ne contient plus de branche physique ennemie; PlayerRootMotionRelay ne consomme que les deltas cinematographiques autorises.

## Skills De Soin

SkillSO.healingTarget (None/Self/PlayersInRadius), healingPercent (0-100) et
healingRadius pilotent les soins instantanes via RealTimeCombatManager.TryUseSkill.
Ils ne passent pas par les degats, impacts, Clarte ou un Animation Event d'attaque.
Aucun clip n'est requis dans cette premiere version, aucune animation concurrente
n'est interrompue. Le lanceur doit etre vivant, hors action/verrou/cinematique.
Un lancement sans soin effectif est refuse sans cooldown.

Revitalisation : 20% des PV maximum propres a chaque joueur dans une sphere
de 30 m inclus, lanceur inclus, cooldown 120 s. Soins d'urgence : 50% du maximum
du lanceur seul, cooldown 30 s. Montants arrondis au PV superieur, limites aux
PV manquants; morts, ennemis et objets inactifs exclus. SetCurrentHp conserve
l'autorite UCC et les notifications UI habituelles. Les cooldowns reutilisent
le contrat par acteur en secondes globales de jeu, sans reset a EndCombat.

Assets dans Resources/CombatSupportSkills pour resolution identique sur serveur
et clients. NetworkCharacterInput transporte uniquement le nom d'asset, refuse
les skills absents de CharacterData.combatSkills, applique le soin/cooldown au
serveur et diffuse les PV absolus. La confirmation/cooldown de presentation ne
concerne que le proprietaire du lanceur. Le protocole reste limite a ces soins;
aucune refonte de l'autorite reseau des degats existants n'est incluse.
Iluvilirae_Test expose les deux skills via additionalCombatSkills. Leur
attribution au futur healer et la validation host/client restent necessaires.

### Regeneration Ciblee

SkillHealingTarget.AllyRegeneration utilise regenerationHealthPerTick=1,
regenerationTickSeconds=1, cooldown=0. Asset Skill_Regeneration dans
Resources/CombatSupportSkills, disponible dans Iluvilirae_Test sans migration
CharacterData. Portee d'application 30 m par defaut (healingRadius); pas de
restriction de distance apres application. Effet indefini jusqu'au transfert,
deces ou retrait d'un des acteurs. Pas de reanimation; conserve a PV pleins.

Le manager conserve une SupportTarget distincte de la cible ennemie : API
SetSupportTarget et TryCycleSupportTarget. Roue ouverte avec ce skill selectionne,
l'action SwitchEnemyLock selectionne explicitement l'allie suivant, et la
confirmation habituelle lance le skill. Aucun allie choisi par defaut.
Selection sans changement de yaw/lock/camera. Le lanceur lui-meme est exclu.

Un PlayerRegenerationEffect sur le lanceur gere un seul destinataire. Recast
identique sans effet; transfert valide retire l'ancien lien et repart avec
un premier tick apres une seconde. Le timer suit les secondes globales de jeu,
avec compensation d'une frame longue, sans cooldown, ni timers UI concurrents.
EndCombat ne retire pas le passif. Desactivation/mort/despawn liberent le lien.

CombatDamageWorldFeedback fournit aussi un texte persistant, reutilisant son
Canvas world-space et billboard : indicateur de regeneration et +1 PV montant
a chaque soin effectif, rien a PV pleins. Nettoyage immediate par desactivation
avant Destroy; plusieurs lanceurs gardent leurs ressources independantes.
NetworkCharacterInput transmet la cible explicite et fait autoriser/transférer
le passif par le serveur, puis diffuse indicateur et ticks aux clients. Le
skill doit etre dans CharacterData.combatSkills pour les casts reseau.

### Siphon Vital

SkillHealingTarget.EnemyLifeSteal reutilise le slot passif de
PlayerRegenerationEffect, avec EnemyRecipient distinct de l'allie soigne.
Skill_SiphonVital : 1 PV par tick de 1 s, cooldown 0, healingRadius 10 m.
Le lancement capture LockedCombatTarget; transfert vers une autre cible ou
Regeneration retire l'ancien effet sans doublon. Pas de choix automatique,
pas de besoin de maintenir le lock apres application.

Le rayon 3D est valide a l'application puis a chaque frame. Hors de 10 m ou
pendant l'autorite cinematique des acteurs, timer suspendu/recale, sans backlog.
Le retour a portee reprend apres une seconde. Mort et disparition liberent le
slot; plafond de PV du lanceur, aucune reanimation. Cible a PV pleins du lanceur
reste drainee, sans afficher de soin inexistant.

EnemyController.ReceivePassiveDamage conserve la resolution de degats,
la garde, le ledger, l'auteur reel et les paliers, sans rejouer Hit a chaque
seconde. Brains utilise ReceiveImpact avec interruption nulle. Le soin ne
depasse jamais le montant effectivement retire : refus/invulnerabilite/palier
sans degat effectif ne rendent aucun PV. Aucun gain de Clarte.
Indicateur persistant sur la cible, degats et soins flottants effectifs.
Reseau : reference ennemie au serveur, validation de CharacterData/portee,
tick autoritaire, PV joueur et feedbacks diffuses; pas de nouvelle autorite
de PV ennemis en parallele de leur pipeline existant. Host/client reste a tester.

## Skills De Controle : Fleche De Lumiere

SkillSO.cooldownSeconds et stunSeconds sont optionnels (0 = comportement
anterieur). La fleche existante utilise 10 s de cooldown et 2 s de stun.
Sa distance minimale est supprimée (0 m), portée maximale conservée à 30 m.
Compilation/Play de LucianBorrowedArrowStunValidation réussis à 3 m : dégâts
et stun 2 s ; contrôles de bornes 0/30/30,01 m validés. Log ArrowNoMinimumRange.log.
LucianBorrowedArrowStunValidation revalide ce contrat depuis le kit Soren
emprunté par Lucian : pas de stun au lancement, impact unique, immobilisation
pendant 2 s et reprise. Compilation/Play réussis, log ArrowStunLucian2.log
dans Library/ActorAnimationSandbox/Logs. Réglage existant préservé.
RealTimeCombatManager compte le cooldown par couple ActorRoot/SkillSO au
lancement accepte, jamais au refus, et ne l'efface pas au delock/EndCombat.
GetSkillCooldownRemaining expose le temps restant en secondes globales de jeu.

Apres un impact valide infligeant des degats, ICombatStunReceiver applique le
stun immediat, y compris pendant une attaque engagee. EnemyController annule
son action et son mouvement via CancelAction, bloque navigation/decision pendant
le delai local, puis reprend. LitBrainsEnemy utilise sa reaction Knocked Out
(Hurt si absente), invalide l'action et son avancee et reprend apres 2 s de jeu.
Pas de prolongation par impacts repetes; morts, cinematiques et QTE prioritaires.
Le projectile conserve le contrat auteur existant : effet lance a 1,1667 s,
impact ResolveSkillImpact a 1,3167 s apres les 0,15 s de voyage visuel. Cette
trajectoire visuelle est désormais parabolique : hauteur de 0,2 m par mètre
horizontal, plafonnée à 4 m, orientation suivant la tangente ascendante puis
descendante. L'arrivée conserve cette orientation plutôt que le yaw ennemi.
La cible est réévaluée pendant le vol. Timing d'impact et tests de portée /
obstruction restent inchangés : pas de nouveau moteur de collision balistique.
Les autres cues restent droites (projectileArcHeightPerMeter=0 par défaut).
Validation : 29 tests Edit Mode passent, dont arcs à 3/15/30 m et trajet
droit par défaut. Play confirme les tangentes montante/descendante du vrai
projectile et le maintien des dégâts/stun. Logs ArrowParabolaTests.xml et
ArrowParabolaPlay.log dans Library/ActorAnimationSandbox/Logs. Rendu interactif
et ressenti manette à confirmer.
La fleche est disponible dans Iluvilirae_Test; son attribution au futur
perturbateur reste a faire lorsque son CharacterData sera identifie.

## References internes EnemyController

Les references aux scripts (sante, contrats, controleur, temps local, contour et indicateur) sont privees et non serialisees. Elles sont resolues automatiquement. Les references Unity configurables restent visibles; les categories sans champs sont masquees. Compilation C# runtime/editeur verifiee; affichage Unity et parcours combat restent a valider.

## Vision et perception

VisionField est supprime. CharacterData.vision centralise portee, angle, hauteurs et masque d obstacles. EnemyController porte l origine du regard et les methodes de detection; la surcharge temporaire de portee reste locale a l instance. Le verrouillage joueur reutilise CharacterData.TryEvaluateVision. Les quatre prefabs et leurs fiches conservent leurs valeurs distinctes. Compilation C# runtime/editeur et ressources verifiees; tests de portee, angle, obstacles, triggers et copies runtime prepares. Execution Unity et parcours de detection/verrouillage restent a valider.

## Options de mort dans CharacterData

enemyDeathOptions configure l activation, les derniers mots, leur duree reelle et la voix. EnemyController observe la sante et execute une seule presentation locale par mort, attendue par la victoire et EnemyDeathPresentation. La desactivation annule dialogue, audio et coroutine. Les autres ennemis ont cette option desactivee par defaut. La mort animee et la dissolution gardent leurs services existants.

## Controleur ennemi unifie

EnemyController est l’unique composant de comportement ennemi, reparti en fichiers
partial. CharacterInfo contient les donnees runtime et remplace CombatHealth.
Les reglages viennent de CharacterData/EnemySettings et du profil de patterns;
les copies runtime ne modifient pas les assets sources. La sante de squad reste
independante. CharacterAnimationController est le contrat abstrait des animations;
le joueur a PlayerAnimationController/PlayerRootMotionRelay/PlayerCombatAnimationEvents.

L’horloge locale est commune aux decisions, mouvements et securites d’attaque.
CompleteAction protege les fins d’action contre les doublons et callbacks anciens.
Le mode Ghost du scientifique bascule CombatEnabled sans desactiver le composant.
Les anciens noms cites dans les sections historiques ci-dessous designent les
responsabilites desormais internes a EnemyController. Guide et bilan de validation :
Assets/CombatRealTime/Enemies/Controller/README.md.

## Mort et disparition des ennemis

CombatEnemyLocomotionController refuse mouvement, orientation et presentation Idle
quand CombatHealth est mort. EnemyDeathPresentation est assure automatiquement
par RealTimeCombatEnemy : il attend la mort animee (minimum 3 s), ainsi que les
derniers mots du scientifique, puis dissout le corps sur 2.8 s reelles via
GhostDissolveController et desactive la racine. Les objets et identites persistantes
ne sont pas detruits. Les materiaux sans dissolution utilisent temporairement
Resources/EnemyDeathDissolve, derive du shader Ghost; les originaux sont restaures
a la desactivation. La disparition ne depend pas de la fermeture de la victoire.
La presentation s'abonne directement a CharacterInfo.HealthChanged, avec un
controle de secours lorsque la sante est assignee plus tard : une defaite de cycle
ou de boss ne peut donc plus manquer l'animation et la dissolution. DeadWeight
route aussi toute resolution de boss vers ForceDefeatFromThreshold; le Veilleur
d'Ombre de Belmont utilise le meme contrat commun.

## Victoire du scientifique

RealTimeCombatSceneUiController differe uniquement le panneau de victoire d'un
ennemi portant ScientistEncounterController : animation Death, dialogue et voix,
puis fermeture du dialogue avant le resultat. Le combat se termine normalement;
le mode UI/Dialogue garde la main pendant cette presentation locale. Une nouvelle
entree en combat ou la desactivation de l'UI annule la sequence et nettoie sa voix.

## EnemyAttack : contrat auteur instantane

Dans un clip ennemi, ajouter EnemyAttack et assigner le SkillSO dans le parametre
Object de l'Animation Event. Le skill doit etre equipe et correspondre a l'action
active. Un parametre vide reprend le skill actif pour les clips partages.
Un seul impact est accepte par action, sans fenetre persistante ni degat retarde.
enemyImpact definit sphere locale, rayon, angle, layers et condition sol;
les bornes de portee du SkillSO restent appliquees. Plusieurs colliders d'une
victime ne produisent qu'un degat. Les decisions de degats restent serveur.

Les VfxCues non DirectOnTarget sont emis a l'evenement; DirectOnTarget seulement
au contact valide. enemyAttackFeedback configure VFX/audio de degat, garde et
contre. Le contre est presente au demarrage effectif du CounterSkill, puisque
l'attaque ennemie annulee ne doit plus infliger son impact. Les degats gardes
passent par ModifyGuardDamage; une esquive ne produit pas de feedback de contact.
Une riposte de palier utilise ce meme contact et ne demande le recul de Lucian
qu'apres degats reels sur celui-ci. Aucun effet camera n'est ajoute.

Open/CloseEnemyAttackHitbox, HitPlayer/HitPlayerIf, ResolveThresholdFailureImpact
et InstantiateEnemySkillVFX/AtIndex ne sont plus des recepteurs d'AnimationEvent.
L'installateur migre les anciens clips et preserve les VfxCues auteur. Assomoir
possede EnemyAttack(Skill_Juggernaut_Assomoir) a 1.70 s. Les evenements physiques,
de reaction et EndEnemyAttack restent necessaires. Les cinematiques de skills,
LightSkills et CounterSkills conservent leurs evenements d'impact specifiques.

## Degats recus : presentation unique

RecordDamageApplied dans SquadCharacterController affiche un nombre rouge pres
du personnage uniquement pour une perte de PV positive. Les coups Lit/UCC et
les dangers passant par ces points d'entree partagent ce feedback.
LitUccDamageBridge rapporte le resultat mesure apres CharacterHealth.Damage pour
les appels Lit; son callback reste le chemin des degats UCC natifs. Le gestionnaire
de combat n'affiche pas une seconde fois les degats de squad et ne conserve que
le fallback CombatHealth. Montant nul, invulnerabilite ou esquive : aucun nombre.

## Attaques libres du laboratoire (2026-10-15)

Recette automatisee : FreeAttackPlayValidation.RunAcceptanceBatch ouvre une copie
isolee d'Iluvilirae_Test sans sauvegarder les assets. RunRemainingAcceptanceBatch
permet une reprise des seuls cas clash/projection/obstacles. La fixture controle
les poses via UCC et espace les capsules ennemies ; les regles de contact et les
clips restent ceux du jeu. Les resultats ne sont pas une acceptation manuelle.
78 tests cibles passent dans FreeAttackAcceptanceTestsFinal.xml. Le test existant
de deconnexion manette/charge echoue en Edit Mode sur son callback et reste hors
du filtre final, sans etre declare valide. Les logs Play consignent separement
les scenarios executes et les parcours interrompus par leur limite.
La reprise physique valide projection sans lock, Basic aerienne apres restitution,
atterrissage et arret des deux dashs devant mur/vide sur plateforme de test.
RunClashAcceptanceBatch isole le contact a pas fixe 1/60 : deux fenetres valides,
neutralisation sans PV et nettoyage reussis. Un essai non cadence a touche Lucian
avant sa fenetre : il reste consigne et n'est pas transforme en clash retroactif.
Logs FreeAttackAcceptancePhysical.log et FreeAttackAcceptanceClash.log dans la
copie isolee. Aucun reglage gameplay change pour obtenir ces resultats.
Pas de validation manuelle du ressenti, de campagne multi-FPS ni de test reseau.

Le laboratoire prepare maintenant l'adaptateur externe sans engagement initial
ni lock. Une perception reelle conserve le droit de demarrer l'aggro. Depuis
une attaque, seule une resolution de contact acceptee passe par
TryEngageFromPlayerContact : session/generation/acteur valides, sans cible
substituee. Le resultat Legacy distingue contact accepte et PV retires : une
garde/absorption a zero engage, contrairement a une esquive ou une protection
cinematique. Le clash est arbitre avant l'execution du contact.

L'entree commune notifie IA, musique, interactions et BattleWall existants,
sans EndCombat, changement de lock, reset de cooldown ou remplacement d'action.
EncounterAggroStarted appartient au passage non engage -> engage ; les autres
contacts ne rejouent pas EN GARDE. Production : anciennes entrees conservees.
Impacts simples, zone et Juste Retour utilisent cette entree dans le laboratoire.
Le contexte capture reste seul proprietaire des destinations ciblees ; les
projectiles visuels sont bornes par la generation et nettoyes a sa terminaison.

Validation de continuite : compilation et 56 tests Edit Mode reussis ; court
Play isole reussi (entree au premier contact, session stable, introduction unique,
lock/unlock stable, attaques/dashs libres, obstacle et callback perime refuse).
Garde/absorption Legacy, paliers, maintien physique de X a l'entree, ressenti
course/roulade et host/client restent a verifier manuellement. Aucune fenetre
de contre ou de QTE de palier modifiee.

Iluvilirae_Test active un FreeAttackProfile de scene pour ses personnages.
Le manager distingue engagement, lock manuel et PlayerAttackContext de session.
Le profil est refuse hors de cette scene. SkillSO.targetingMode porte le contrat
unique ; les metadonnees sont migrees sans activer le nouveau flux en production.
Le profil ne contient plus de liste d'associations ni de deduction par classe.
Non configure provoque un refus lisible. Les installateurs preservent les modes
deja renseignes ; Migrate/Validate Skill Targeting couvre les kits du laboratoire.

Au demarrage effectif du coup, input camera-relative (facing sans input),
acquisition dans 60 degres avec correction <=25, priorite angle puis distance,
ligne de vue decor. L'intention est capturee avant les verrous d'input, jamais
au buffering. UCC oriente progressivement, sans homing ni modification du lock.
Les portees de lancement servent aux dashs ; les portees de hit restent distinctes.

Chaque contact ponctuel interroge le volume offensif du profil (sphere locale,
pas la capsule corporelle). Preference a la cible capturee si reellement touchee,
puis meilleur contact pour une attaque libre. Les actions ciblees ne changent
pas de victime. Les skills cibles a distance utilisent un corridor offensif
etroit de leur portee ; les attaques de zone gardent leur resolution existante.
Defenses, Revelation, paliers, feedback, autorite serveur et clash restent dans
leurs chemins communs. Generation/consommation rejettent doublons et callbacks
perimes ; un contact manque ne produit aucun degat de rattrapage.

Eclair et Bond offensif acquierent une cible facultative dans leur portee de
lancement ; a defaut, dash directionnel fixe de 10 m, sans acquisition tardive.
Le trajet est borne par sol, obstacles et BattleWall. Aucun acteur n'est ignore
en dash libre. Le bypass d'Eclair ne concerne que sa cible capturee. Cooldown
consomme au demarrage valide, meme dans le vide ; trajet nul refuse sans cout.
Ascension reste lancable dans le vide ; sa projection exige un contact revalide.
Rupture exige desormais une cible au corps-a-corps (0-3 m au lancement).

TryResolveSkillTargeting couvre attaques, soins, protections, controle et skills
cinematiques ordinaires avant tout cout/effet. Direction, cible, beneficiaire
allie et origine de zone sont captures dans PlayerAttackContext ; un rebind de
generation ne recalcule pas la pose cible. Les supports gardent leurs executants,
selection explicite, conditions d'auto-soin, concentration et regles de cumul.
Ennemi obligatoire/facultatif : lock compatible prioritaire, sinon acquisition
temporaire. Un lock invalide refuse le lancement cible, sans changer de victime.
Les portees/rayons restent dans leurs definitions specialisees existantes.

CombatSkillCinematicController accepte explicitement ce contexte et sa victime,
valide rig/tracks/camera auteur avant annulation de l'action et avant cooldown.
Placement, bindings, suspension et impact utilisent cette victime, pas le lock.
Sans plan camera auteur, aucune prise de controle camera. Fin/interruption
restitue uniquement les suspensions acquises. Mort/disparition de la victime
obligatoire nettoie la session sans substitution. Les entrees specialisees des
CounterSkills, LightSkills et paliers ne sont pas migrees.

Validation du contrat : 27 assets auteur valides, compilation et 50 tests Edit
Mode reussis. Court Play automatise dans Iluvilirae_Test isole : acquisition
obligatoire sans lock, refus non configure, Basic au contact, Eclair cible et
dashs libres/obstacle/interruption valides. Les cinematics ordinaires avec cible
temporaire, supports en situation complete et host/client restent a valider en
jeu ; ne pas confondre ces controles avec une acceptation manuelle du ressenti.

## Reactions invisibles aux attaques

Exception laboratoire (2026-10-15) : LucianBrainsCombatArena transmet un
EnemyReactionTiming avec le clip/etat, l'instant d'impact et ActionSequenceId.
L'evenement existant arme B ; Y utilise [impact-.2, impact[ en temps de clip.
La lecture tient compte de l'etat suivant pendant un fondu et de la vitesse
Animator reelle, sans chronometre d'animation parallele. Le ralenti .4 etire
donc ces .2 s en environ .5 s reelle. B reste a .5 s reelle, independant de Y.
Les surcharges sans contexte gardent les regles de production ci-dessous.

L'input Y capture son horodatage Input System et sa phase eligible ; la
demande est traitee avant le contact et avant l'arbitrage de clash. Aucun
impact deja applique n'est retire. Une fermeture, generation differente,
sortie d'etat, destruction, pause menu ou cinematique prioritaire invalide
la demande. Antispam .4 s reelle inchange, aucun auto-contre au maintien.
Le validateur Lit > Combat Lab > Validate Fair Counter Timings verifie les
evenements reels sans reecrire les clips. Un timing runtime invalide retire
seulement l'option contre de l'action avec un avertissement unique.

OpenEnemyReactionOpportunity() ouvre simultanement les fenetres B (0.5 s) et Y
(0.2 s) en temps reel. Les trois reglages dodgeWindowSeconds,
counterWindowSeconds et reactionTimeScale (0.4, 1 sans ralenti) sont sur le
CombatHealthThresholdController du BattleManager. Les anciens champs de temps
sont migres par FormerlySerializedAs. Les QTE des paliers restent independants.

B demarre la roulade par TryDodgeImmediate sans buffer; un refus ne donne aucune
protection. Une roulade acceptee protege seulement la victime du skill et de
l'ActionSequenceId ennemis captures, pour toutes les fenetres de ce coup.
L'impact ferme l'opportunite mais conserve cette protection jusqu'a la fin du
coup. Le coup suivant, meme avec le meme SkillSO, n'est pas protege.
Y demarre uniquement TryStartFromSuccessfulQte. Hors fenetre, garde et roulade
habituelles restent disponibles. Premier choix accepte gagnant, doublons ignores,
aucune validation automatique d'une touche maintenue avant l'ouverture.

Le ralenti utilise un handle propre : fin, expiration, impact, cinematique,
desengagement ou desactivation le liberent sans imposer Time.timeScale = 1.
Pas de panneau QTE, de map CombatQTE ni d'effet camera ajoute pour ces reactions.
QTE(input) sur un ancien clip ennemi delegue avec un avertissement unique par clip.
Le chemin reseau reste autoritaire serveur; aucune validation de timing declaree
par un client distant n'est ajoutee dans cette passe.

## Ruee ennemie tridimensionnelle

BeginEnemyRush conserve la condition de phase aerienne et la cible configuree,
mais ResolveRushDelta utilise maintenant X/Y/Z. Le moteur est l'unique autorite
de translation et suspend la gravite durant la ruee. Le cast capsule suit le
vecteur 3D et arrete la ruee contre le decor. La fin automatique rend la verticale
a la gravite; RequestEnemyLanding pendant la ruee ne la detourne pas.
Les durees utilisent toujours le temps local; le timeout et les securites de
sol restent actifs. rushStoppingDistance est une distance 3D, pas horizontale.
L'impulsion atteint immediatement rushMaximumSpeed dans la direction initiale,
sans suivre ensuite la cible. rushImpulseDuration limite sa duree (0.35 s locales
par defaut); la distance initiale moins rushStoppingDistance limite son trajet.
EndEnemyRush n'est plus requis dans les clips; l'API reste une annulation interne
et une compatibilite pour les autres anciens clips. Fin d'action, interruption,
cinematique et desactivation annulent toujours l'impulsion.

## Patterns et equipement effectif

La selection et la poursuite utilisent les patterns dont tous les skills sont
equipes, avec poids positif et portee initiale coherente. La preference melee
est inactive sans pattern terrestre equipe : Assomoir seul peut poursuivre,
se mettre a portee et attaquer. Le seul pattern valide est repetable sans
ignorer cooldown ou recuperation; une alternative equipee reactive la limite
de repetition. Aucun skill absent du CharacterData n'est rajoute automatiquement.

## Contrat minimal Juggernaut

- Un QTE par coup, Y par defaut, 0.5 s reelle et temps global 0.4 via le
  controleur existant. Reussite : CounterSkill cinematique; echec : coup continue.
- Plus de warning, telegraphie, reactions parfaites Dodge/Jump, ni VFX Light/Slash
  autonomes pour les quatre skills. Garde, invulnerabilite physique et feedback
  des degats conservent leurs chemins existants. EN GARDE reste lie a l'aggro.
- OpenEnemyAttackHitbox/CloseEnemyAttackHitbox sont le seul contrat d'impact
  de ces clips; paire au meme temps 1.70 s pour Assomoir, sans seconde fenetre.
- Les ripostes de palier hors pattern echantillonnent aussi le volume balaye
  durant la fenetre, respectent portee/arc et n'appliquent qu'un impact/recul.
- JuggernautEssentialEvents valide QTE, paire de hitbox, mouvements et fin;
  nettoie seulement les skills/clips et preserve les boutons QTE deja auteurs.
  Les installateurs de patterns et CounterSkill ne reintroduisent plus l'ancien flux.

## Contre par QTE d'attaque

RealTimeCombatAnimationEvents.QTE route les evenements ennemis vers OpenAttackQte,
les evenements joueur vers les sequences de paliers existantes. Le QTE ennemi
capture skill et ActionId; fin de coup, impact, mort ou cinematique invalident
la fenetre. Seule une nouvelle pression correcte lance le CounterSkill cinematique.
La garde et RegisterReaction(Counter) ne produisent plus de contre simple.
L'echec ne suspend pas l'attaque. La cinematique suspend les cerveaux et moteurs,
annule leurs hitboxes puis restitue les suspensions qu'elle a acquises.
Un palier atteint pendant un coup reste Pending sans suspendre celui-ci; le combo
ne lance pas de coup supplementaire. Mort et LightSkills restent prioritaires.
Les Skills cinematiques ordinaires sont refuses contre un ennemi en attaque.

## Interactions apres desengagement

SetReturnFacing(home) oriente le retour : point d'origine pendant la pause,
steeringTarget NavMesh pendant le trajet pour suivre les virages. Le controleur
de locomotion possede le yaw ; StopNavigation et SetCombatTarget annulent cette
consigne. La rotation d'origine est retablie a l'arrivee par la phase de retour.

Pendant retour et pause, la vision peut reengager le combat. Une detection de
proximite a 360 degres complete le cone, par defaut 6 m et avec le meme masque
d'obstruction. returnReengageDistance est configurable par profil (0 desactive
la proximite seule). Une marge de 0.5 m dans la zone de poursuite evite les
reengagements immediatement annules a la frontiere. La reprise annule le chemin
de retour, rend la cible au cerveau et passe par BeginEnemyAggro pour UI/inputs.

Une fuite hors zone ferme le combat sans attendre le retour au spawn ni la mort
de l'ennemi. La decision precede la disponibilite NavMesh ; seule la navigation
de retour attend EnsureReady. Une attaque deja engagee conserve sa fin normale.
EndCombat retire l'agression suivie, rend les inputs Exploration et rescane les
interactions. L'input combat libere sa propre suppression et sa garde, sans
supprimer les verrous des UI, cinematographiques ou UCC encore legitimes.

## Rotation et cadence Juggernaut

Le profil impose 720 deg/s avant le verrou d'attaque. La valeur de secours
du composant n'est plus ecrasee : EffectiveFacingSpeed expose la valeur active.
CombatLocomotionPlaybackRate ajuste uniquement l'etat de locomotion au rapport
vitesse NavMesh / (echelle locale * vitesse de cycle), plafond 1.35, fondu .08 s.
Les vitesses de cycle 1.8/3.6 sont independantes des vitesses de navigation.
Activite avec hysteresis .08/.03 ; l'arret interrompt aussi un fondu entrant.
Les diagnostics optionnels distinguent navigation et action/suspension.

## Juggernaut sans IA concurrente

JuggernautPatternSetup est l'unique installateur du Juggernaut. Il retire
RealTimeCombatEnemyBehaviour et EnemyTacticalResponseController du prefab,
au lieu de les desactiver. Les anciens menus deleguent a cet installateur :
aucune auto-installation de reactions legacy au rechargement de scripts.
La validation refuse ces composants meme desactives. Les classes partagees
ne sont pas supprimees tant que les autres ennemis en dependent.

ScriptedOnly ignore les deltas Animator hors cinematographique. NavMesh
deplace la navigation ; CombatEnemyPhysicsMotor gere saut, ruee et avancees.
BeginEnemyAdvance/EndEnemyAdvance encadrent une progression collisionnee
(Frappe 0.4 m/0.18 s, Followup 0.5 m/0.2 s). Toute interruption les annule.
Approche melee privilegiee ; alternative aerienne tiree une fois par approche.

## Poursuite animee

EnemyCombatBrain evalue les portees des premiers skills des patterns avant
l'observation. Hors portee : Chase (approche) ou Position (recul si trop pres),
avec destination radiale via ApproachTarget. La marge d'entree est 0.2 m,
plafonnee a la moitie de l'intervalle minimum/maximum. Les patterns disponibles
sont prioritaires pour l'approche ; a portee, cooldown ou orientation ne
provoquent pas une avance supplementaire. Sortir de portee annule l'observation.

ApproachTarget actualise le chemin toutes les 0.15 s locales ou apres 0.5 m de
deplacement cible. Projection locale et chemin complet du type d'agent requis ;
echec = arret avec motif, sans teleportation. Marche 1.8 m/s, course 3.6 m/s,
entree course a 8 m, retour marche a 6 m. Animator suit la velocite NavMesh.

## Correctif aggro et retour (2026-09-05)

L'attente initiale au spawn ne declenche plus le mode retour. L'arrivee au spawn
libere `returning`, qui bloquait jusque-la toute acquisition visuelle suivante.
EnemyNavigationController accepte un agent deja rattache apres validation de
disponibilite du monde, avant le delai de retry. EnemySkills relit CharacterData
avant enumeration et reservation. Le manager ne rajoute plus de reactions legacy
aux acteurs possedant EnemyCombatBrain et recoit le Transform du personnage,
jamais son ancetre de scene via transform.root.

Juggernaut : vision 30 m, poursuite 35 m autour du spawn pour eviter une sortie
immediate a l'acquisition. Le masque physique inclut Default, Ground, Stairs,
CameraObstruction, Obstacle, XRayEnvironment et MovingPlatform ; le filtre des
colliders et la tolerance de hauteur restent appliques. Les tests Editor couvrent
l'attente initiale, la fin du retour et la reservation avant initialisation.

## Juggernaut : patterns de skills

Le Juggernaut utilise `EnemyCombatBrain` et le profil reference par son
`CharacterData`. La liste `combatSkills` est la source des skills equipes pour
`EnemySkills`. Les patterns ordonnent ces skills avec observation, cooldown et
recuperation : frappe, combo de deux coups, balayage et Assomoir. La vue seule
ne provoque pas d'attaque ; un degat avec son acteur source engage la cible.
Le maximum lumineux recu persiste jusqu'au desengagement et chaque coup
verrouille ce maximum multiplie par le coefficient du SkillSO.

Les nouveaux impacts utilisent la forme spatiale du SkillSO, une fenetre
auteur et un balayage entre deux echantillons. Une cible n'est touchee qu'une
fois par fenetre. La direction se verrouille au depart du coup ; Assomoir
memorise la destination de sa ruee. La navigation ne reprend qu'apres contact
sol et fin du clip. Le rayon de poursuite vaut 20 m autour du spawn ; la fuite
termine le coup courant puis impose 1 s de pause avant le retour.

`EnemyCinematicState` suspend le cerveau et invalide l'attaque avant les QTE
et cinematiques. L'ancien comportement reste un adaptateur de placement,
desactive sur le prefab ; les reactions tactiques autonomes sont retirees.
Les seuils et leur ralentissement global conservent leur fonctionnement.
`Lit/Combat/Install Juggernaut Patterns` migre les assets ;
`Lit/Combat/Validate Juggernaut Patterns` verifie skills, etats et evenements.

## Rôle

Gérer le combat temps réel continu dans le monde, sa présentation, ses réactions
et sa résolution. Le combat tour par tour actif a été retiré; seul l'import
`Assets/Legacy/BattleManager_SymphonieImport` reste archivé et hors gameplay.

## Combat temps réel (prototype isolé)

`Assets/CombatRealTime/` est le seul système combat actif dans
`GameplaySessionRoot` et sur les ennemis temps réel. `CombatHealth`, la
bibliothèque audio et les feedbacks de dégâts restent partagés car ils sont
utilisés par ce système.
`RealTimeCombatSceneUiController`, sur `UI_Overlay` de `Bootstrap` et `Arena`,
pilote exclusivement les panels déjà écrits dans la scène et se lie au
`RealTimeCombatManager` existant de `GameplaySessionRoot` :
`CombatEngagedPanel` joue son intro, puis `CombatScreenInfosPanel` affiche la
cible, les PV, la Clarté, le rang et le journal. `VictoryPanel` et `DefeatPanel`
restent les écrans de résultat. `CombatDefensePanel` n'existe plus : garde,
télégraphie et contre cinématique immédiat couvrent cette interaction.
`RealTimeCombatManager` possède le verrouillage, les dégâts de lumière, la
Clarté et les fenêtres de réaction. `RealTimeCombatEnemy` stocke le plus grand
dégât de lumière reçu jusqu'à sa prochaine attaque et le verrouille au démarrage
de l'animation ennemie. Les dégâts reçus pendant cette animation alimentent un
ledger suivant, promu uniquement par son événement de fin.
Le lock ne possede pas le mouvement du joueur: il fournit uniquement la cible.
`LitOpsiveLocomotionBridge` convertit l'axe brut en repere ennemi et est la
seule autorite pour l'intention UCC, le facing et l'orbite. Les actions ne
doivent pas ecrire le Transform de Lucian directement pendant le lock.
La locomotion continue sous lock est InPlace : `CombatIdle` et
`CombatLocomotion` utilisent les clips Twinblades InPlace et le tag
`RealTimeCombatInPlace`. Le bridge impose `UseRootMotionPosition=false` et
`UseRootMotionRotation=false` dans tout le gameplay. UCC applique seul
l'approche, le recul, l'orbite, les collisions et l'inertie.
Les actions anciennement Root utilisent `PlayerStateMotionLibrary` et
`PlayerStateMotionController` : les trajectoires sont mesurees dans l'Editor
sur l'avatar de Lucian, puis appliquees par `LitUccStateMotionAbility` avant
la resolution des collisions UCC. Aucun delta Animator n'est lu par ce pilote.
`PlayerActionMovementPolicy` distingue presentation seule, trajectoire d'etat
et mouvement deja scripte. Esquives, approches ciblees et franchissements
gardent leur propre commande ; une trajectoire d'etat ne prend pas leur verrou.
Le saut existant est integralement protege, sans recalibration.

Le Root de Lucian reste autorise uniquement pendant une session cinematique
explicite de `CombatActorAnimationRoot`. Le relay applique alors les deltas,
avec le consommateur Root UCC toujours desactive. La fin du bon jeton et la
desactivation referment cette autorite. Les clips cinematiques et ceux des
autres acteurs restent dans leurs ressources dediees.

Les menus `Lit/Animation` proposent audit, migration explicite et validation.
L'audit effectue la prevalidation des copies en memoire et ecrit
`Library/PlayerInPlacePreflight.json`. L'application exige le meme manifeste
et les memes empreintes d'entree ; une divergence demande un nouvel audit.
Les clips et modeles d'animation consommes par Lucian sont regroupes dans
`Assets/Characters/1_Squad/Lucian/Animation`, avec leurs GUID conserves.
Les copies gameplay et le manifeste sont dans
`Assets/Characters/1_Squad/Lucian/Animation/PlayerInPlace` ; les originaux fournisseur
restent intacts. L'installateur combat n'est plus execute automatiquement
a chaque compilation et conserve les clips InPlace deja assignes. Le
validateur avant build controle references, evenements, profils et donnees
protegees. Les anciens noms d'etats contenant Root restent des identifiants.
Gauche/droite sous lock memorise le rayon initial autour de `EnemyLockPoint`.
Le bridge ajoute ensuite une faible correction radiale sous forme d'intention
UCC, jamais par ecriture directe du Transform : le strafe reste circulaire sur
sol libre tandis que les collisions gardent la priorite. Les diagonales liberent
ce rayon et conservent leur arc d'approche ou de recul.
Pendant la mémoire d'alerte, un ennemi provoqué peut consommer ce ledger même
si une animation root l'a temporairement tourné hors de son champ de vision ; il
continue alors de se tourner vers Lucian. La perte de vision prolongée conserve
les règles existantes de désengagement.
Les Animation Events `BeginReactionTelegraph`/`OpenReactionWindow`,
`ResolveEnemyAttackImpact` et `EndEnemyAttack` de
`RealTimeCombatAnimationEvents` restent la source auteur du timing de menace,
fenêtre, impact et clôture. `EnemyAttackRecoverySafety` ne s'active qu'après la
durée du clip plus une marge, afin de récupérer un ennemi dont l'événement de
fin n'aurait pas été évalué sans doubler l'impact.
Le Juggernaut peut aussi utiliser `EnemyTacticalResponseController` : garde et
esquive sont des réponses rares, configurables et distinctes de ses attaques.
Chaque ennemi auteur peut exposer un `CombatEnemyRuntimeContract`. Pour le
Juggernaut, il est obligatoire : le clone genere par `SceneMarker` doit porter
sur son root `RealTimeCombatEnemy`, `EnemySkills`, `CombatEnemyPhysicsMotor`,
`CombatActorAnimationRoot`, Animator valide, Rigidbody cinematique,
CapsuleCollider et NavMeshAgent. Si cette enveloppe gameplay manque, le marker
desactive IA, navigation et combat avec un rapport unique : aucune attaque
aerienne ne doit pouvoir commencer sans moteur physique operationnel.
Assomoir reste pilote par ses Animation Events auteur. Le moteur physique est
le seul proprietaire de sa verticale et de sa ruee; le filet de securite peut
desormais finaliser idempotemment une attaque sans evenement de fin afin de
liberer le ledger et permettre la riposte suivante. `CombatEnemyLocomotionController`
revient explicitement a `CombatIdle` lorsque sa velocite NavMesh retombe a zero.
`EN GARDE !` n'est plus une annonce d'entree en combat : il est reserve a la
telegraphie/aux fenetres de reaction d'une attaque menace effective.
`CombatWarningOn`/`CombatWarningOff` ajoutent seulement la presentation locale
de danger et le focus camera; `OpenReactionWindow(float)` ferme son eligibility
en temps non scale, sans jamais declencher les degats a la place de
`ResolveEnemyAttackImpact`.
Le `CombatWarningProfile` du `SkillSO` ennemi porte aussi son ralentissement de
debut d'alerte (`Use Slow Motion`, echelle et duree); il passe par le meme
controleur temporel que le hit-stop et ne peut pas liberer une pause de contre.
Le combat temps reel gere aussi `Counter`. `NorthButton` maintenu active une
garde, qui reduit les degats au moment de l'impact; une nouvelle pression dans la
fenetre ouverte par l'Animation Event et acceptee par le `SkillSO` ennemi suspend
l'attaque avant son impact. `CounterSkillCombatController` fige alors le temps,
l'IA et les actions, puis lance immediatement son `defaultCounterSkill`. Chaque
`CounterSkillSO` porte une Timeline, les noms de pistes dynamiques joueur/ennemi/
Cinemachine, ses degats, sa Clarite et ses AudioClipSO. La Timeline tourne en
temps non scale; seul `ResolveCounterSkillImpact` applique le hit. Sa fin termine
definitivement l'attaque suspendue et restaure le temps, le lock et les inputs.
Les attaques qui ne listent pas `Counter` dans `acceptedEnemyReactions` restent
gardables mais ne peuvent pas declencher cette riposte.
La garde visuelle est une state `Guard_Block` de `Player_Model`, alimentee par
`Anim_SF_Block_v2` du pack Super Fast Fighting. `CounterSkillCombatController`
la joue au maintien de South et revient a l'Idle de combat au relachement. Tant
que cette state n'est pas encore installee, il utilise
`Twinblades_Defense_Hit_Root` comme fallback visuel.
Le prototype Juggernaut est installe par `Lit/Combat/Build CounterSkill Prototype` :
ce menu editeur cree la Timeline, le `CounterSkillSO` et la Virtual Camera sous
`BattleManager`; aucun objet de selection de contre n'est cree pendant le jeu.
Les `LightSkillSO` sont des capacites cinematographiques distinctes de la roue :
ils portent le cout de jauge; chaque `SkillSO` ou `BasicSkillsSO` porte son gain
explicite `Light Charge On Hit`, applique seulement apres un impact valide; la
Timeline, les degats finaux et le gain de Clarte. `LightSkillCombatController`
sur `BattleManager` suit cette charge pendant une session; les hits rates et
l'impact du LightSkill ne la rechargent pas. `LightSkillPanel` de scene affiche
la jauge uniquement en combat et devient cyan lorsqu'elle est prete. L'action
`RealTimeCombat/LightSkill` (`Left Stick Press` / `R`) vide la jauge, verrouille
temporairement Lucian et joue la Timeline du SO via le `PlayableDirector` du
`BattleManager`. Chaque LightSkill est ecrite dans `AnimationLab`, puis exportee
par l'Inspector du `LightSkillTimelineAuthoringRig`: `Validate Runtime Contract`
valide les pistes requises, Signals, cameras et objets exportes; `Bake
LightSkill` demande une confirmation puis cree un package runtime fige dans le
dossier du SO. Ce package contient un prefab de rig et une copie `Runtime` de
la Timeline. Les preview actors, camera, Brain et AudioListener restent dans
`AnimationLab`; les tracks Player/Enemy sont lies aux vrais Animators lors du
lancement. Les seules copies de GameObjects autorisees sont les Cinemachines
utilisees et les racines marquees `LightSkillRuntimeExport`; leurs tracks sont
enregistres dans `CombatCinematicRig`. Chaque bake valide d'abord une copie
temporaire, puis remplace atomiquement la seule Timeline runtime stable
`<LightSkill>_Runtime.playable` et le prefab stable. En cas d'echec, le package
precedent reste intact; apres succes, les anciennes Timeline `Runtime_Baked*`
non utilisees sont supprimees.
Pour auditer un package deja fige sans le rebaker, `Attach Framing Audit To
Active Rig` copie uniquement les releves d'AnimationLab dans son prefab actif;
aucun clip, track, camera ni montage n'est modifie.
Lorsqu'une LightSkill utilise des pistes acteurs `ApplySceneOffsets`, la Timeline
est l'unique proprietaire des transforms pendant la sequence : le relais de
root motion des acteurs est suspendu pour eviter un second deplacement de
Lucian ou de l'ennemi.
Le `PlayableDirector` bake est toujours inerte (`Play On Awake` desactive,
`UnscaledGameTime`, `DirectorWrapMode.None`). A chaque lancement, le rig pose
les acteurs, lie toutes les pistes, reconstruit explicitement son graphe,
evalue `t=0`, met a jour manuellement la Brain et verifie la camera du premier
`CinemachineShot` avant le moindre `Play()`. Un pre-roll dont la camera active
ne correspond pas exactement au plan attendu est refuse et restitue
immediatement charge, IA, inputs et camera.
Chaque camera exportee peut declarer une
cible de suivi/regard Player, Enemy ou EnemyLockPoint et les modules
`CinemachineFollow`/visee correspondants; une camera sans cible est valide si
son cadrage est entierement pilote par Timeline. Le bake capture et compare
cle Timeline, transform, FOV, priorite, Output Channel, targets, Follow offset
et pipeline : toute divergence, script manquant ou dependance a AnimationLab
refuse le package avant son assignment au SO. Aucun correctif camera n'est
ajoute en runtime. La piste Cinemachine est liee exclusivement a la Brain de
la camera explicitement exposee par `CombatLockOnCameraController`, sans
`Camera.main`. Le rig runtime journalise cette Brain, sa vcam active et ses
cibles, puis restitue une seule fois la camera UCC a la fin, a l'interruption,
a la desactivation ou a la remise en pool.
Pour une LightSkill, cette restitution passe par `LitCameraDirector` attache a
la Main Camera resolue depuis `CombatLockOnCameraController`. Il coupe la Brain
et restaure le binder, handler et controller UCC : aucune autre vcam ne peut
prendre la releve entre la Timeline et UCC.
Le lock combat ne suspend que son propre cadrage pendant ce handoff; il ne
desactive pas les drivers UCC. Cette separation garantit que `LitCameraDirector`
restitue exactement leur etat initial a la fin de Timeline.
`Lucian_Anchor` et `Enemy_Anchor` d'AnimationLab sont les reperes canoniques
des roots Player et Enemy. Le bake les copie comme `PlayerStageAnchor` et
`EnemyStageAnchor` dans le prefab runtime; il refuse un package auquel il
manquerait l'un de ces reperes. Une LightSkill place le root du rig au midpoint
exact des roots de Lucian et de l'ennemi verrouille, aligne l'axe bake sur leur
axe horizontal, puis pose directement les vrais roots sur ces anchors avant la
premiere evaluation de Timeline. Le midpoint ne bouge jamais : aucune recherche
d'orientation ou de place, aucun test de collision, sol, NavMesh ou
depenetration ne peut refuser ou deplacer une LightSkill. Les positions
atteintes sont conservees a la sortie. Toute modification de formation dans
`AnimationLab` necessite un nouveau `Bake LightSkill`.
Les Animators ne sont jamais convertis en poses de root : cette ancienne
conversion pouvait cumuler un offset de hierarchie et une position monde
d'auteur. Au bake, les pistes `Player.Animator` et `Enemy.Animator` sont forcees
en `ApplySceneOffsets`, donc relatives au plateau runtime. Pendant une
LightSkill midpoint, Timeline est l'unique pilote des transforms acteurs : le
rig ne les echantillonne ni ne reappelle UCC chaque frame. Cela evite toute
boucle de retroaction entre Timeline et UCC tout en conservant le root motion et
les positions finales. Un package bake avec les anciens offsets est refuse et
doit etre regenere. Les CounterSkills sans plateau gardent le transfert direct
de `Animator.deltaPosition`. Les poses locales imposees aux enfants Animator
sont restaurees avant la remise en pool du rig.
Le layout runtime version 3 conserve egalement l'orientation monde du rig
d'auteur; un prefab bake avec une version plus ancienne est refuse et doit etre
regenere depuis `AnimationLab`.
`CombatActorAnimationRoot` est la source explicite de l'Animator de combat:
`ActorRoot` reste le seul transform monde, tandis que l'Animator est declare
avec son `AnimationRoot`. Ce dernier est maintenu a sa pose locale identite,
y compris pendant `Hit`, afin qu'un clip importe ne puisse pas deplacer le mesh
hors de son root gameplay. `CombatActorRootMotionRelay` transfere uniquement le
root motion cinematographique au root gameplay. Le menu `Lit/Combat/Normalize
Actor Animation Hierarchies` normalise Juggernaut et GiantJuggernaut sans
modifier leurs skeletons importes; `Validate Actor Animation Contract` permet
de verifier Lucian et les deux ennemis avant un test. Lucian garde actuellement
son Animator sur le root par securite pour ses clips generiques, mais tous les
systemes le resolvent via le meme contrat explicite.
La remise en pool efface les bindings Timeline et les references Cinemachine,
arrete le Director au temps zero et remet le transform du rig a l'identite.
L'auto-desengagement est suspendu tant qu'une LightSkill est cinematographique :
l'IA cible est alors arretee intentionnellement et ne peut pas mettre fin au
combat avant la restitution de la sequence.
Lucian est pose via `SetCinematicPositionAndRotation` du bridge UCC, une API
autorisee pendant le verrou d'input cinematographique et qui ne le libere pas.
La Brain Cinemachine utilise temporairement un `Cut` pendant la Timeline, puis
restaure son blend de gameplay a la sortie. Le premier plan ne peut donc pas
etre interpole avec la pose UCC precedente.
Le rig d'auteur ne choisit jamais de camera de remplacement : une cle de
`CinemachineShot` non resolue est bloquante, afin que le plan previsualise et
le plan bake soient necessairement le meme.
Le bouton `Log Framing Audit` du rig d'auteur evalue les poses de reference a
`0 s`, `1 s` et a chaque changement de plan Cinemachine. Ces releves sont
emballes dans le rig baked; en jeu, `CombatCinematicRig` journalise aux memes
instants les ecarts Player, Enemy, camera, rotation et FOV, ainsi que la
camera attendue/active et les verrous UCC. Cette instrumentation sert a isoler
un decalage de cadrage sans corriger le montage par compensation runtime.
Une `AnimationTrack` liee a l'Animator d'une Cinemachine d'auteur est reconnue
comme une piste camera et est liee a l'Animator de sa copie baked. Cela preserve
les enregistrements de transform et de Lens de la Timeline sans laisser une
piste `None (Animator)` au runtime.
Les pistes d'animation Cinemachine bakees conservent egalement leur mode
d'offset Timeline d'auteur : le bake ne les convertit jamais vers un autre
repere, ce qui garantit que le mouvement valide dans `AnimationLab` reste
identique une fois le rig aligne sur Lucian reel.
`LightSkillCinematicSequenceController` suspend l'IA cible et interprete les
Signals projectile, impact VFX et degats. Un `LightSkillSO` peut aussi definir
un state Animator optionnel pour Lucian et/ou l'ennemi apres une fin naturelle
de Timeline (state, fondu, temps normalise). Ces states ne s'appliquent jamais
apres interruption ou mort; une state `Death` conserve toujours sa priorite.
`LightSkill_Devastation` utilise actuellement `GetUp` pour le Juggernaut; cette
state joue `EnterTheBattle_1` en attente d'un clip de releve dedie.
`ResolveLightSkillImpact` reste la seule resolution lorsque le fallback est
desactive. `LightSkill_Devastation` est la base active; Furie a ete supprime.
La fin d'une LightSkill restitue le verrou UCC, le mode `Combat` et
`RealTimeCombatInput` avant de demander une relecture des controles maintenus.
Le stick et la course tenus pendant la Timeline sont donc reappliques par le
flux UCC normal au premier frame jouable; aucune roulade n'est necessaire pour
repartir.
`CombatAttackDefinition`, `SkillSO`, `EnemySkills` et
`RealTimeCombatLoadout` portent les données auteur, avec exactement huit slots
d'attaque. `CombatLockOnCameraController` est le seul pilote de caméra lorsque
le verrouillage est actif et doit recevoir explicitement les drivers gameplay à
suspendre dans la scène.
Le premier test utilise `Lueur faible` (2 dégâts) et `Lueur intense` (8 dégâts)
dans les deux premiers slots de Lucian. Le Juggernaut choisit
`Skill_Juggernaut_Assomoir` dans `EnemySkills`; il joue la state `Assomoir`,
accepte uniquement `Dodge` et ses events pilotent prompt, VFX, impact et fin.
Le contour rouge de verrouillage utilise `CombatLockOutline`, la layer
`CombatOutline` et la passe HDRP `CombatLockOutlinePass` de
`GameplaySessionRoot`. Il est independant de `RuntimeOutlineSelectionManager`,
qui reste reserve aux contours bleus d'interactables.
Le Juggernaut de test utilise 30 PV et un delai de riposte de 1 seconde afin de
permettre la verification du maximum de son ledger.
`CombatDamageWorldFeedback` est une presentation locale creee a l'impact : le
nombre est attache au combattant touche, reste lisible face a la camera, pulse,
monte puis disparait. Les ennemis recoivent un retour cyan pour les degats de
lumiere et Lucian un retour rouge pale pour les degats subis. Aucun prefab ni
Canvas de scene n'est requis.
Les degats recus par Lucian passent par `SquadCharacterController.ApplyDamage`,
et non par une ecriture directe de ses PV, afin que UCC conserve son etat de
sante, ses retours et sa mort. Une riposte lethale termine volontairement la
session et bloque le controle du personnage jusqu'a sa resolution; les deux
voies d'attaque joueur refusent aussi toute nouvelle action a `0 PV`.
Une mort temps reel joue la state `Base Layer.Death` de `Player_Model` avant
d'afficher le `DefeatPanel` existant. Ce panneau est configure temporairement
avec `Revivre`, qui recharge la scene du dernier checkpoint sauvegarde, et
`Quitter le jeu`; son ancien bouton checkpoint est masque pour cette issue.
`PlayerActionPresentationController.LockDeathAnimation` traite cette state
comme terminale pour l'instance de Lucian : aucune action, animation de hurt
ou reprise de locomotion ne peut l'ecraser. Le verrou est volontairement
recree seulement lors du rechargement declenche par `Revivre`.
`Revivre` est selectionne par defaut dans l'`EventSystem`; la navigation UI
haut/bas relie explicitement les deux choix. Le bouton selectionne est agrandi
et teinte en or, tandis que l'autre est attenue. La croix directionnelle
haut/bas et le bouton Sud de la manette assurent directement cette navigation
et validation, avec ou sans `EventSystem` dans la scene gameplay.

## Skills UI

`StatsSO` sert exclusivement aux checks historiques. `SkillSO` porte les donnees
de combat : nom, icone, clip, chemin de state Animator optionnel, degats et une liste de `SkillVfxCue`. Chaque cue
choisit un prefab, un spawn sur la main tenant l'arc, direct sur `EnemyLockPoint` ou un projectile, son
delai attache au joueur et sa duree de trajet vers la cible. Chaque cue peut
aussi referencer un `AudioClipSO`, joue au point de depart de son VFX. Un skill peut aussi
demander le `Bow` attache a Lucian, avec prefabs VFX optionnels d'apparition et
de disparition. `SkillsManager`, sur
`SkillsPanel`, ne propose que les competences connues de `CharacterData.combatSkills`
et conserve au maximum huit `SkillSO` equipes
sans doublon. Il les synchronise avec les slots persistants de `SkillWheel`; le
panneau sert a la preparation, tandis que la roue expose les memes slots pendant
le combat. La roue s'abonne a l'evenement `EquippedSkillsChanged` de
`SkillsManager` et se met a jour uniquement lors d'un changement reel du
loadout, sans polling dans `Update`. Cette etape ne declenche pas encore une
competence : `SouthButton` oriente d'abord le root du joueur horizontalement
vers `EnemyLockPoint` via le bridge UCC, puis lance le chemin de state Animator configure sur le
`SkillSO` selectionne (fallback sur son `AnimationClip`); ses VFX et degats restent appeles par
Animation Events. Les slots sans `SkillSO` sont masques et ignores par la
navigation. Le loadout de `SkillsManager` n'est pas encore inclus dans
`CharacterSaveData`.
`SkillsManager.BasicSkills` contient une liste distincte de `BasicSkillsSO`.
Elle est maintenant scindee en `GroundBasicSkills` et `AirBasicSkills` :
`WestButton` choisit la famille selon l'etat UCC reel de Lucian et le combo est
reinitialise lors d'un changement sol/air. Chaque `BasicSkillsSO` declare son
contexte. Un BasicSkill `Airborne` peut conserver Lucian dans les airs ou
demander une descente physique a une seconde precise de son clip; UCC reste
proprietaire de l'atterrissage et aucun repositionnement n'est applique.
Quand le lock est actif, `WestButton` (ou `Q`) ajoute le prochain basic skill au
buffer de combo. Les clips sont joues dans l'ordre de la liste puis bouclent sur
le premier; cinq skills produisent donc la sequence 1-2-3-4-5-1-2-3 apres huit
pressions. Une pause superieure a `basicComboResetDelaySeconds` (0.85 s par
defaut) reprend la sequence au premier skill. Seul un skill peut etre garde en
attente derriere l'animation en cours (`maximumBufferedBasicSkills`), afin que
les pressions tres rapides ne produisent pas une longue file d'animations. Les
Basic Skills reutilisent les Animation Events de `SkillSO`. Le profil de
presentation distingue `chainNormalizedTime`, qui ouvre le buffer, de
`chainTransitionNormalizedTime`, qui lance reellement la BasicSkill bufferisee
et interrompt le clip en cours. Cette transition est automatiquement bornee
entre l'ouverture et `recoveryNormalizedTime`; elle conserve le mode de root
motion UCC entre les deux clips. Le buffer interne et la file d'input sont
coordonnees pour qu'une seule attaque future soit acceptee a la fois.
Les states Basic n'ont pas de transition Animator de sortie :
`PlayerActionPresentationController` force donc `Locomotion` si une state est
interrompue ou perdue avant son seuil de recuperation, au lieu de seulement
vider le buffer interne.
`SkillSO` porte aussi `minimumHitDistance` et `maximumHitDistance`. Ces champs
sont herites par `BasicSkillsSO`; lors de `HitEnemy`, le manager compare la
distance horizontale actuelle entre Lucian et `EnemyLockPoint` a cette plage.
Hors plage, aucun degat ni animation `Hit` ennemi n'est applique.
La meme plage est verifiee a l'impact d'un skill ennemi, entre sa racine et
Lucian : hors plage, aucun degat ne lui est applique.
Les cues `DirectOnTarget` et `Projectile` sont aussi ignores hors de cette
plage; un cue `PlayerHand` reste autorise car il presente le caster, pas la
cible. Le `HitEnemy` hors plage affiche un message world-space jaune : `Raté
(trop près)` sous la distance minimale, ou `Raté (trop loin)` au-dela de la
distance maximale.
`SkillSO.requireValidRangeToStart` reserve cette meme validation au lancement
d'un skill auteurise : hors plage, le clip ne demarre pas et le feedback de
distance existant est affiche. `Saut de rupture` utilise cette option : sa state
`Base Layer.Skill_3_JumpKick` est taguee `RealTimeCombatRootMotion` et son event
`ResolveSkillImpactAndRetreat` reevalue la portee au contact. Si le hit est
valide, il applique les degats et le stagger existants, joue tous les cues VFX/
Audio, une `ScreenWave` locale optionnelle projetee depuis `EnemyLockPoint`, puis
une impulsion UCC opposee a la cible. Cette impulsion suspend les controles
jusqu'au retour au sol (borne de secours configurable), sans gerer le sol via le
manager de combat. `SkillRetreatImpulse` peut maintenir une inertie aerienne :
apres sa deceleration configuree, une fraction de la vitesse horizontale est
conservee jusqu'a l'atterrissage. Le Saut de rupture utilise `0.6 s` puis 35 %
de sa vitesse initiale. `CombatJumpKickShockwave` est le prefab visuel monde associe.
Les clips peuvent appeler `Dash` sur `RealTimeCombatAnimationEvents` : la force
est calculee depuis le caster vers `EnemyLockPoint` plus
`dashOvershootDistance`, afin de traverser la cible. `StopDash` applique ensuite
une contre-impulsion pendant `stopDashDuration`, avec une deceleration reglable,
pour une arret court mais non instantane.
`LeftTrigger` maintenu passe l'alpha de `SkillsWheelSlots` de `0` a `0.4`, et
son relachement le repasse a `0`; le joystick droit selectionne le slot le plus
proche de sa direction et `SouthButton` confirme. Chaque slot selectionne
interpole vers une echelle x1.5 et un alpha de `CanvasGroup` plus eleve en temps
non scale; les autres slots sont attenues. Chaque slot resolve son enfant `Icon`
si la reference n'a pas ete assignee dans le prefab, ce qui garantit l'icone du
`SkillSO` equipe plutot que le sprite de maquette du slot.
Tant que la roue est maintenue, elle pousse son focus d'input : `SouthButton`
est reserve a la confirmation et ne peut pas declencher le fallback
interaction/saut UCC; la locomotion reste disponible. Cette suppression ciblee
est relachee uniquement avec `LeftTrigger`.
La confirmation lit directement le slot selectionne dans SkillWheel, y compris
apres un survol souris, sans reutiliser l'ancien index d'input. Les survols sont
ignores roue fermee ; un slot vide ou une roue desactivee ne lance rien. Le
declenchement passe toujours par TryUseSkill et ses controles de combat.
Une competence validee revient a `Base Layer.Locomotion` a son
`recoveryNormalizedTime` configure sur le `SkillSO`; sa duree est aussi un garde-fou pour forcer ce retour si une state sans
transition ne remonte jamais sa fin. La pose root finale est alors communiquee
explicitement a UCC avant et apres le `CrossFade`, afin que l'idle ne restaure
jamais le point de depart du clip. Ce retour arrete aussi UCC et remet les
parametres de mouvement a zero avant `MoveStopTrigger`, apres avoir synchronise
la position et rotation finales avec le bridge UCC, arrete les capacites UCC et le
controleur personnage afin que `Locomotion` reprenne sur son idle sans vitesse
residuelle ni annuler le deplacement root du clip. Les states de skill root
doivent porter le tag `RealTimeCombatRootMotion` : UCC conserve alors leur
deplacement et leur rotation meme sans input. Les attaques root du
prototype appliquent la meme synchronisation. Un nouveau lancement de competence
ou d'attaque annule ce retour precedent.
Les Skills et BasicSkills diriges utilisent `UccBody` : UCC tourne donc le
corps de Lucian vers `EnemyLockPoint` et conserve cette orientation a la sortie
du clip. `VisualOnly` reste reserve aux poses qui ne doivent tourner que le rig.

## Timelines optionnelles de SkillSO

Chaque `SkillSO`, donc aussi chaque `BasicSkillsSO` et `EnemySkills`, expose une
definition `Combat Cinematic` optionnelle. Quand elle contient une Timeline et
un `CombatCinematicRig` baked, `CombatSkillCinematicController` suspend le
combat, l'IA, UCC et les inputs, puis lit le rig poolé **sur place**. Aucun
midpoint ni replacement d'acteurs n'est applique. L'unique Animation Event
`ResolveCinematicSkillImpact` applique les degats; la fin de Timeline ne sert
jamais de fallback. Les BasicSkills cinematographiques conservent leur index de
combo et reprennent donc au coup suivant apres restitution. L'outil
`CombatSkillTimelineAuthoringRig` permet de valider puis baker un package runtime
depuis AnimationLab; il exige les pistes Player, Enemy, Cinemachine, Signals et
exactement un evenement d'impact.
`Skill_3_Entaille` utilise explicitement `Base Layer.Skill_3_Entaille` et cette
state porte le tag `RealTimeCombatRootMotion`, comme toute competence qui peut
deplacer Lucian pendant son clip.
`Skill_2_Fleche de lumiere` utilise aussi son chemin de state explicite, afin
que le retour a locomotion suive toujours le clip root reellement joue.
Les clips joueur peuvent utiliser `InstantiateSkillVFX` pour jouer tous les
cues, ou `InstantiateSkillVFXAtIndex(int)` pour les cadencer individuellement.
`ResolveSkillImpact` est l'event standard : il applique d'abord les degats au
verrou actif, puis declenche une fois le `CombatImpactFeedbackProfile` du
`SkillSO`. Ce profil configure par competence le hit-stop non scale, l'onde
ecran locale, les cues VFX/Audio supplementaires et l'impulsion amortie du
cadrage UCC; il n'ecrit jamais la Main Camera. Le controleur de lock applique
une attenuation et des plafonds globaux, puis remplace progressivement
l'impulsion precedente : les impacts rapproches ne cumulent donc pas leurs
decalages de camera. `HitEnemy` reste un alias de
compatibilite. `ResolveSkillImpactAndRetreat` reutilise le meme feedback avant
l'impulsion specifique du Saut de rupture. Les cues peuvent viser
`PlayerHand`, `PlayerSword`, `EnemyLockPoint`, ou partir de la main tenant
l'arc avec `ProjectileFromPlayerHand`.
Les BasicSkills 1, 2 et 3 sont regles a 5, 10 et 15 degats. Leurs clips
appellent `HideSwordWhenComboEnds` : l'epee reste donc visible pendant une
transition bufferisee et ne disparait qu'apres la recuperation du dernier coup.
Ils ouvrent leur chaine a `0.55`, transitionnent entre `0.66` et `0.70` et
rendent la locomotion entre `0.68` et `0.74`, avec un blend de sortie de `0.05 s`.
`CombatMobilityController`, sur `BattleManager`, gere les actions de mobilite
du combat temps reel. `Dodge` joue une des quatre states root directionnelles,
avec un startup de 0.05 s, 0.18 s d'invulnerabilite et 0.25 s de cooldown.
`EastButton` joue l'esquive root directionnelle avec ses i-frames; `NorthButton`
est reserve a la garde et au contre, et le dash dedie a ete retire du prototype.
`Jump` appelle la capacite UCC existante. Les actions restent soumises a la priorite mort,
cinematique et hurt; un unique input de mobilite peut etre conserve 0.12 s
jusqu'a l'ouverture de l'annulation d'un skill. `PlayerActionPresentationProfile`
expose `mobilityCancelNormalizedTime` et `allowMobilityCancel`; les trois
BasicSkills ouvrent respectivement a 0.35, 0.42 et 0.55.
`EnemySkills`, place sur la racine d'un ennemi temps reel, expose une liste de
`SkillSO` sans roue de selection. Le meme receveur
`RealTimeCombatAnimationEvents` permet aux clips ennemis d'appeler
`SetEnemySkill(int)`, `PlayEnemySkill(int)`, `InstantiateEnemySkillVFX`,
`InstantiateEnemySkillVFXAtIndex(int)` et `HitPlayer`. Les VFX ciblent Lucian;
`HitPlayer` resout l'impact du `SkillSO` actif via `RealTimeCombatManager`.
`HitPlayerIf(string)` conditionne cet impact au moment exact de l'Animation
Event. `Grounded` ne touche Lucian que lorsque UCC le rapporte au sol; `Always`
(ou un argument vide) conserve un impact inconditionnel. Une condition inconnue
annule l'impact et produit un avertissement unique.
La portee, le multiplicateur et les reactions ennemies sont configures sur ce
`SkillSO`; le montant final reste celui du ledger de lumiere.
Lorsqu'un impact ennemi applique effectivement des degats non letaux a Lucian,
`RealTimeCombatManager` joue sa state de hurt configuree, par defaut
`Base Layer.RealTimeCombat_RootMotion.TwinSword_Defense_Hit_Root`. Cette
reaction interrompt un skill joueur en cours et utilise le retour existant vers
la locomotion. Aucun hurt ne remplace l'animation `Death` lors d'un coup letal.
Les states ennemies sont terminees explicitement : une attaque active reste
prioritaire sur l'animation `Hit`, pour qu'un clip root comme `Assomoir` ne soit
jamais coupe et ne laisse pas l'ennemi en l'air. L'Animation Event
`EndEnemyAttack` finalise le ledger puis ramene l'ennemi a sa state `Idle`
configuree. `RealTimeCombatEnemy` coupe le root motion seulement pendant `Hit`,
puis relache cette state a la duree du clip pour eviter un ennemi bloque ou
decale du sol si son Animator ne contient pas de transition de sortie.
`CombatEnemyPhysicsMotor` est l'unique autorite verticale des ennemis temps
reel. Chaque prefab ennemi porte un `Rigidbody` cinematique et une
`CapsuleCollider`; pendant une attaque, le `NavMeshAgent` est suspendu, le root
motion horizontal est applique au `ActorRoot` et son composant vertical est
ignore pour les skills `Grounded`. Le moteur expose un audit temporaire de pose : spawn, hit, attaques, NavMesh,
Rigidbody, Animator, parent et Netcode sont traces, avec alerte a chaque saut
de plus de 0.5 m. Il permet d'isoler un ecrivain de transform concurrent sans
modifier les clips d'animation.
Une sonde de sol ne peut jamais corriger verticalement l'ActorRoot de plus de
`maximumGroundSnapDistance` (`0.75 m` par defaut) : tout collider d'un autre
niveau est journalise puis ignore.
Un `SkillSO` ennemi peut declarer une trajectoire `Airborne`; ses Animation Events `BeginEnemyAirborne` et
`RequestEnemyLanding` pilotent une chute controlee. `EndEnemyAttack` ne clot
la riposte et ne retourne a `Idle` qu'apres le contact sol. Toute mort ou
interruption force cette meme recuperation, sans laisser l'ennemi en hauteur.
Le clip `GiantJuggernaut_Jump` utilise `HitPlayerIf("Grounded")` a son
impact : une esquive par saut n'evite les degats que si Lucian n'est plus
Grounded a cette frame.
L'Animator utilise par `RealTimeCombatEnemy` doit posseder un Controller : la
reference explicite est prioritaire, puis celui de `EnemySkills`, puis un
Animator enfant valide. Ainsi les Animation Events et le retour a `Idle`
pilotent le meme Animator que le skill root.
Le `Bow` existant sous le modele de Lucian est masque par defaut. Les Animation
Events `ShowBow` et `HideBow` de `RealTimeCombatAnimationEvents` pilotent son
affichage et son masquage. Une notification de fin/interruption de
`PlayerActionPresentationController` range aussi l'arc si le `HideBow` final
n'est jamais atteint. Les VFX d'apparition/disparition sont configures sur
`PlayerBow` directement sur le GameObject `Bow`.
L'epee `Sword` fonctionne de facon identique via `PlayerSword` et les Animation
Events `ShowSword`/`HideSword`, avec ses propres VFX optionnels sur le composant
attache a l'epee. La meme securite la range a la fin/interruption d'une action;
elle reste neanmoins visible entre les BasicSkills bufferises. Elle est masquee
au demarrage.

## Poursuite et musique

`RealTimeCombatEnemyBehaviour` est le comportement reutilisable place sur les
ennemis temps reel. Il se tourne vers Lucian des que le `VisionField` de son
`RealTimeCombatEnemy` le voit. Sa vision utilise la portee normale en patrouille
et passe a `alertedVisionDistance` apres cette premiere detection; elle reste
alertee pendant `alertMemorySeconds` apres la perte de ligne de vue, puis revient
a la portee normale. Le `RealTimeCombatManager` garde le lock pendant ce rayon
d'action alerte au lieu de le fermer au rayon global initial. L'override de
musique reste inactif a la seule detection et commence uniquement au premier
degat de lumiere recu par l'ennemi.
L'option `Can Pursue Player` est activee par defaut. Desactivee, l'ennemi ne se
deplace ni pour poursuivre Lucian, ni pour rechercher sa derniere position ou
retourner a sa patrouille pendant son alerte : il reste a sa place, regarde la
cible et ne lance que les attaques deja a portee.
Quand la session temps reel se ferme automatiquement, `RealTimeCombatManager`
attend le relachement du mouvement local, puis synchronise la pose et remet a
zero l'input UCC residuel pour eviter une marche involontaire apres le lock.
La detection seule rend l'ennemi alerte, mais une attaque de lumiere recue est
necessaire avant qu'il poursuive Lucian via `NavMeshAgent` (ou deplacement direct
de secours) pour convertir le ledger en riposte. Apres perte de ligne de vue, il
inspecte une fois sa derniere position connue pendant
`searchLastKnownPositionSeconds`, a `searchArrivalDistance`, sans suivre la
position reelle de Lucian hors de sa vision, puis retourne au `patrolPoint`
optionnel ou a sa pose initiale. Il choisit alors une famille d'attaque selon
`meleeAttackPreferencePercent`, avec fallback sur l'autre famille si elle est
indisponible, puis rejoint sa portee melee ou distance configuree.
Le Juggernaut de prototype possede actuellement le seul skill melee
`Skill_Juggernaut_Assomoir`. La portee de lancement d'un skill ennemi est sa
`maximumHitDistance` lorsque celle-ci depasse la distance de famille
melee/distance du comportement. Une attaque peut donc etre categorisee melee
pour la selection tout en restant declenchable a longue distance, si son
`SkillSO` l'autorise. Le `Skill_GiantJuggernaut_Jump` est configure de 0 a
30 m, utilise la state `GiantJuggernaut_Jump` et doit terminer par l'Animation
Event `EndEnemyAttack`. Les clips de riposte ne doivent pas boucler. Les degats
recus pendant une attaque active sont stockes dans un ledger suivant, qui garde
uniquement leur maximum. `EndEnemyAttack` clot l'attaque, promeut ce montant et
repart le delai de riposte : le clip courant n'est donc ni interrompu ni relance
immediatement. `RealTimeCombatManager` centralise un unique
override de musique combat tant qu'au moins un de ces comportements est en mode
attaque, et le relache a leur desengagement ou mort.
Les clips ennemis de combat temps reel peuvent aussi afficher un prompt
world-space independant avec `ShowInput(Sprite)` sur
`RealTimeCombatAnimationEvents`, puis le retirer avec `HideInput()`. Le Sprite
2D est assigne directement dans l'Animation Event; le prompt est ancre au
`EnemyLockPoint` par defaut et ne modifie pas la fenetre logique de reaction.

## Vision Et Lock

`VisionField` valide la portee, le cone de vision et une ligne de vue physique.
La vision ne verrouille jamais Lucian automatiquement. `Player/LeftShoulder`
verrouille ou deverrouille manuellement l'ennemi le plus proche dans
`lockRange` (6 m); cette portee et la distance de deverrouillage sont
multipliees par la plus grande composante de scale de l'ennemi. Une step
`LaunchCombat` d'une `StorySequenceAsset` peut aussi lancer l'affrontement a
la toute fin de sa sequence : elle verrouille l'ennemi prioritaire, active la
musique combat et lui applique `openingCombatDamage` (50 par defaut) comme un
coup de lumiere. Parmi les cibles a portee, le plus grand ennemi est prioritaire,
puis le plus proche. `SwitchEnemyLock`, lie a
`Gamepad/dpad/left`, fait tourner la
cible parmi les ennemis visibles a portee. La camera vise `EnemyLockPoint`, ou
la racine si ce point est absent. Son enfant `LockGlow` affiche un orbe lumineux
et joue `AudioClip_CursorFlames` a chaque nouveau lock. Exploration et combat
sont continus : aucune transition ecran, BattleSphere, teleportation ou arene
ne fait partie du flux.

## Classes principales

- `CombatAggroEnemy` : détection et création des définitions ennemies.
- `CombatSessionManager` : autorité, sessions, tours, actions et RPC.
- `CombatSessionState`, `CombatTurn`, `CombatRuntimeEnemy` : état runtime.
- `CombatHudController` : commandes et affichage local.
  Il orchestre aussi `CombatEngagedPanel`, `CombatScreenInfosPanel` et
  l'exclusivite de `CombatDefensePanel`, ainsi que l'ActionMap `Combat` pendant
  toute la session.
- `CombatDefensePanelController` : affiche les 3 items defensifs assignes
  quand un `AnimationEvent` de combat le demande, puis route `UseItem1/2/3`
  vers ces slots.
- `BattleTransition` : composant place sur le `BattleManager` de `Maison` qui
  orchestre l'entree combat, declenche `ScreenWaveController` et prechauffe
  BattleSphere/VFX.
- `ScreenWaveController` : systeme HDRP Custom Pass dedie a la vague d'ecran,
  testable hors Play Mode via le bouton inspecteur `PlayScreenWave`, avec un
  lisere lumineux reglable pour rester lisible dans les scenes sombres.
- `CombatCameraPresentationController` : pilote camera cinematographique locale
  par phase de combat et expose le shot temporaire `CounterAction`.
- `CombatCounterItemPresentation` : presentation locale des items de contre
  configures par `Item.CombatReactionProfile`.
- `CombatAnimationEvents` : hooks Animation Event pour ralentir la presentation,
  ouvrir/fermer `CombatDefensePanel`, deplacer l'attaquant, revenir a sa pose
  initiale et notifier l'impact.
- `TimeManager` : multiplicateur local de presentation combat declenche par les
  `AnimationEvent`, sans `Time.timeScale`.
- `CombatTransitionController` : audio/musique de transition, transition de
  sortie et musique de proximite.
- `CombatHealth` : santé persistante des ennemis de scène; initialise son
  maximum depuis le `CharacterData` résolu via `CharacterInfo`.

## Flux principaux

1. Un trigger d’aggro demande une session au manager.
2. L’autorité capture les positions de retour et construit les ennemis runtime.
3. `BattleTransition` demande a `ScreenWaveController` de jouer une vague HDRP
   locale chez le joueur engage et fige localement `Time.timeScale` pendant cette
   premiere vague. Pendant ce gel, `RuntimeOutlineSelectionManager` suspend
   l'Outline monde actif puis le restaure a la fin de la transition. A la fin
   de la vague, le manager capture le snapshot de retry pre-combat, instancie
   `combatEntryMidpointPrefab` a mi-chemin entre le joueur et l'ennemi, oriente
   vers l'ennemi, puis teleporte le joueur et l'ennemi vers l'arene. Le meme
   Custom Pass reste actif et enchaine alors une deuxieme vague
   inversee pour revenir a un rendu normal dans l'arene. En reseau, la vague
   n'est jouee que chez le joueur
   engage, tandis qu'un RPC dedie demande la BattleSphere a tous les clients sans
   declencher le HUD/camera des joueurs non engages. L'instance est suivie par
   session et detruite quand la sortie
   manuelle du combat est validee apres l'ecran victoire/defaite; un retry la
   remplace par une nouvelle instance. Les `CharacterEffect` presents sur cette
   instance sont joues a l'apparition, stoppes a la sortie, puis detruits apres
   un delai par defaut de 2 secondes. Le joueur engage est verrouille au moment
   de cette teleportation.
4. Le HUD ferme les UI monde ouvertes (inventaire, loot, pause, dialogue,
   InfoBox, confirmations, lecture, craft et construction), prend le focus
   exclusif, active l'ActionMap locale `Combat` et joue une fois par session l'intro
   `CombatEngagedPanel_Trigger` sur `CombatEngagedPanel`, des l'entree en
   combat et sans attendre le premier snapshot, puis affiche
   `CombatScreenInfosPanel`.
5. Chaque tour commence par une courte phase de décision locale : HUD/focus et
   caméra se suspendent visuellement sans utiliser `Time.timeScale` global.
6. Pendant la décision ennemie, le joueur engagé dispose d'une réaction
   defensive locale : l'inventaire ne s'ouvre pas en combat, et un item choisi
   via `CombatDefensePanel` est valide puis resolu cote autorite. Le ralenti
   n'est pas declenche par cette phase; il doit venir des `AnimationEvent`
   places dans les clips d'attaque.
   Quand un `AnimationEvent` le demande, `CombatDefensePanel` devient le seul
   panel combat visible et ne propose que les 3 items defensifs assignes au
   personnage comme items combat. L'ActionMap locale `Combat` reste active
   pendant toute la session et le panel la force aussi quand il devient visible;
   `UseItem1`, `UseItem2` et `UseItem3` selectionnent les slots 1 a 3 quand le
   panel est visible. La fenetre defensive autorisee correspond a cet affichage
   reel : le joueur peut remplacer son choix jusqu'a ce que le panel se masque,
   et seul le dernier item choisi est resolu a l'impact. Le slot choisi est
   agrandi, outline et tinte; les slots sans item assigne restent masques.
   Les retours positifs de choix defensif sont ajoutes a `CombatLog`, tandis
   que les erreurs restent dans `InfoBoxUI`.
   Les racines `EnableItem_1/2/3` sont resolues comme boutons UI pour permettre
   aussi la selection souris et la navigation manette/clavier.
   Cette demande issue d'un AnimationEvent est prioritaire sur l'intro
   `CombatEngagedPanel_Trigger` : si l'attaque ennemie commence pendant
   l'intro, celle-ci est masquee et `CombatDefensePanel` s'affiche
   immediatement.
   Quand le clip ferme ce panel,
   `CombatScreenInfosPanel` redevient visible.
7. Le manager alterne joueur puis ennemi, applique les intentions validées côté
   autorité et synchronise les clients.
   Au debut d'une attaque ennemie normale, le joueur engage peut jouer une
   animation de preparation defensive configurable, avec fallback sur `Defense`
   puis `Block`, ainsi qu'une voix `AudioClipSO` optionnelle. Cette
   presentation est jouee localement chez le client proprietaire.
8. La resolution joue la mort du perdant puis un taunt du gagnant
   (`Taunt`, puis `Victory`/`Celebrate` en fallback si disponibles). En cas de
   victoire, le coup fatal joueur est memorise et un shot camera ralenti de fin
   de combat est declenche aussitot, puis le `VictoryPanel` attend le plus long
   delai entre 3 secondes apres cet impact, la fin de l'action fatale joueur et
   la duree de mort ennemie, avec une petite marge de securite. Le HUD affiche
   le panel de scene `VictoryPanel` ou
   `DefeatPanel`; aucun panel de resultat n'est cree en runtime. La victoire
   garde une validation manuelle simple. En defaite,
   `CombatHudController` ne remplace pas le texte de `DefeatPanel` et route ses
   boutons de scene vers trois choix : retour `MainMenu`, retry immediat du
   combat courant, ou rechargement du dernier checkpoint/sauvegarde active. Le
   retry restaure le snapshot en memoire pris juste avant l'entree en combat :
   etat personnage/inventaire, snapshot monde persistant, PV joueur pre-combat
   et ennemis reconstruits depuis l'etat monde. Il rebind aussi les Animator
   joueur/ennemi cote autorite et client proprietaire avant de relancer la
   session, afin d'effacer la mort/taunt de la tentative perdue. Les sorties
   menu et checkpoint terminent la session avant chargement
   de scene. En cas de defaite, la musique de combat est remplacee par la
   musique `Game Over` configuree dans `CombatAudioLibrary` jusqu'a cette
   sortie. Cette validation restaure alors les positions, la camera et le
   mouvement, puis applique le resultat a l'ennemi monde.

Pendant une session, la camera locale de combat est la seule source de pilotage
spatial de la `Main Camera`. `CombatCameraPresentationController`, cree par
`CombatSessionManager`, lit uniquement le contexte local du manager, suspend les
pilotes camera Opsive (`CameraController`, handler et binder), applique un plan
de camera par phase, puis restaure Opsive a la sortie. La phase `EnemyAction`
utilise un cadrage cinematographique proche du joueur, un FOV plus large, un
focus biaise vers l'ennemi et une respiration lente pour suivre l'attaque.
Chaque phase shot expose aussi une vitesse de deplacement locale de son offset :
une valeur X positive fait glisser la camera lateralement depuis son offset de
depart, Y la fait monter, et Z la pousse sur l'axe joueur-vers-ennemi pendant
la duree du shot. Le shot temporaire de victoire utilise ce meme systeme pour
creer un travelling final ralenti des que la resolution de victoire demarre.

Pendant une attaque de mêlée, la présentation peut déplacer temporairement
l'attaquant vers sa cible puis le ramener à sa position de combat. Ce mouvement
reste cosmétique : l'impact est toujours appliqué par `CombatSessionManager`
quand le clip emet son AnimationEvent d'impact, et les attaques
distance/support restent sur place. Les attaques dont l'animation embarque déjà
le déplacement ne reçoivent pas d'approche scriptée supplémentaire.

Les ralentis de combat sont exclusivement declenches par les `AnimationEvent`
exposes par `CombatAnimationEvents`. La camera de combat, `CombatSessionManager`
et les impacts ne demarrent plus de ralenti ou de hit-stop automatiques. Les
animations, UCC et mouvements scriptes de presentation lisent le meme
multiplicateur quand un clip a lance `SlowCombatTime`.
Les entrees/sorties de ce ralenti jouent aussi les cues
`CombatTimeSlow`/`CombatTimeResume` de l'`ActionAudioLibrary` par defaut, via
des assets `AudioClipSO`.
Pendant ce meme ralenti, `TimeManager` demande aussi un leger ducking de la
musique via `AudioManager.BeginMusicDucking`, puis le relache au retour a la
vitesse normale.
L'affichage de `CombatDefensePanel` ne depend pas de l'etat du ralenti dans
`TimeManager` : il est demande explicitement par `CombatAnimationEvents`.

Un item peut porter un `CombatReactionProfile` optionnel. Les items de reaction
peuvent etre gardes dans les 3 items combat meme s'ils n'absorbent pas de
degats. Le premier type supporte est `MeleeCounterImpale` : si le joueur choisit
l'item avant l'impact d'une attaque ennemie melee, l'attaque est interrompue,
les animations joueur/ennemi configurees se jouent, puis les AnimationEvents
`Take` et `Release` du clip joueur font passer le visuel de l'item de l'attache
joueur a l'attache ennemie. L'AnimationEvent `CounterHit` du clip joueur
interrompt alors l'animation d'attaque ennemie et declenche l'animation/clip
ennemi configure, `Impaled` par defaut. Ce meme `CounterHit` notifie aussi la
resolution logique du contre au manager : les degats/morts ne dependent plus
d'un delai profil ou d'un timer manager. Si ce `CounterHit` tue l'ennemi,
l'animation/clip `Impaled` est ignoree pour laisser la resolution jouer
directement la mort. Le shot camera `CounterAction`, le ralenti local, les SFX,
VFX et voix optionnelles du profil soulignent l'impact.
Contre une attaque non melee, ce profil ne remplace pas une defense.
Le type `MeleeDefense` sert aux objets comme `Item_Shield_WoodShield` : le
joueur sort le visuel de l'item en main, bloque une attaque melee sans subir de
degats, puis l'item perd des PV defensifs persistants portes par le personnage.
Si ses PV tombent a zero, une unite est retiree de l'inventaire; sinon elle peut
etre reutilisee lors des combats suivants avec ses PV restants. Quand plusieurs
boucliers identiques existent, le combat consomme d'abord une unite de la pile
la plus abimee.
Si le profil renseigne `enemyAnimationClip`, `CombatReactionClipPlayer` lit ce
clip directement via Playables sur l'Animator ennemi; sinon le manager retombe
sur la state ou le trigger `enemyAnimationName`, puis sur la duree fallback.
Au moment ou le visuel reste plante sur l'ennemi, son axe Z monde est force a
l'inverse du Z local de l'ennemi; seul le roll Z du profil sert encore
d'ajustement fin. Le Juggernaut fournit une state Animator `Impaled` vide pour
recevoir le clip d'empalement.

Les clips peuvent aussi declencher des evenements via `CombatAnimationEvents`
pour controler finement le ralenti, l'ouverture/fermeture de
`CombatDefensePanel`, une ruee cosmetique et la presentation d'item avec
`Take`/`Release`/`CounterHit`. Le composant resout la victime depuis le contexte
local de combat, capture la pose de depart au moment de la ruee, restaure
uniquement cette presentation et peut notifier `NotifyCombatImpact` au frame
d'impact. Les degats restent resolus une seule fois par `CombatSessionManager`;
il n'y a plus de timer fallback, donc un clip d'attaque doit emettre
`NotifyCombatImpact` pour appliquer l'impact, tandis qu'un clip de contre
`MeleeCounterImpale` doit emettre `CounterHit` au frame ou la reaction doit
etre resolue.
Les panels UI de combat reactivenent aussi leur hierarchie et corrigent une
echelle locale nulle sur les parents au moment de l'affichage, afin que
`CombatEngagedPanel`, `CombatScreenInfosPanel` et `CombatDefensePanel` restent
visibles meme si la racine `UI_Overlay` a ete sauvegardee a `localScale` zero.
`SlowCombatTime` descend par defaut a `0.1` en entree rapide, suit les
`Animator`/UCC sous l'acteur et inclut aussi la victime de combat pour rendre
le ralenti visible sur les deux corps. Les appels `SlowCombatTime` ouvrent
aussi `CombatDefensePanel`; `RestoreCombatTime` le ferme.
L'attaque Juggernaut `Griffe` est animation-driven : le manager force seulement
le lancement de `Attack_Griffe` et ne joue plus de saut, dash, audio ou VFX
specifiques pour cette attaque. Ces elements doivent etre places dans le clip.
Les anciens hooks autonomes `AnimationEventsManager` et `FirstStrikeEffect` du
systeme legacy ont ete retires pour eviter les appels accidentels a
`Time.timeScale`.

Le systeme de combat actuel ne doit pas dependre du legacy Symphonie importe
dans `Assets/Legacy/BattleManager_SymphonieImport`. Cet import reste isole dans
son assembly `BattleManager.SymphonieImport`, non auto-referencee, compilee
uniquement dans l'Editor et seulement si le define `SYMPHONIE_IMPORT_COMPILE`
est ajoute manuellement. Aucun code, scene ou asset de combat actuel ne doit
referencer ses types ou ses GUIDs afin que le dossier puisse etre supprime plus
tard.

La musique de combat peut aussi être demandée localement par proximité d'un
`CombatAggroEnemy`, avant qu'une session tour par tour ne démarre réellement.
Cette demande reste cosmétique et utilise l'override musical de
`CombatAudioLibrary` exposée par `AudioManager`; elle est relâchée avec
hystérésis quand le joueur local sort assez loin du trigger d'aggro.
La resolution de defaite empile un override musical `GameOverMusic` au-dessus
de cette musique de combat, puis le relache pendant la transition de sortie.
Les boutons de `DefeatPanel` sont resolus depuis la scene par nom ou texte
(`menu`, `retry`/`reessayer`, `checkpoint`), avec fallback sur l'ordre des
boutons enfants si necessaire; eviter de renommer ces boutons sans mettre a jour
les mots-cles ou les references serializees.
Le snapshot de retry est strictement runtime et n'ecrit aucun fichier : le
manager capture `CharacterStateStore.CaptureRuntimeState` et
`WorldStateManager.CaptureSnapshot` avant tout deplacement vers l'arene, puis
restaure ces donnees au bouton retry. Avant la nouvelle entree combat,
`CombatSessionManager` appelle aussi `Animator.Rebind()`/`Update(0)` sur le
joueur et l'ennemi concernes, avec un RPC cible pour le client engage en
multijoueur.
`BattleTransition` prechauffe au demarrage une `ShaderVariantCollection`
optionnelle, les materiaux/prefabs optionnels et une instance cachee de
BattleSphere dont les colliders sont desactives; il joue puis stoppe ses
`CharacterEffect`/VFX sur une frame pour limiter le hitch du premier combat.
Le timing d'entree par defaut est 0.9s avec pic a 0.38.
Le pass d'ecran n'est pas cree en runtime : `BattleManager` possede en scene un
enfant `ScreenWavePass` avec un `CustomPassVolume` global et un
`FullScreenCustomPass` desactive par defaut. `ScreenWaveController` reference ce
volume et le material `MAT_ScreenWave`, qui utilise le shader
`Hidden/Lit/ScreenWave`. Le bouton inspecteur `PlayScreenWave` sur
`ScreenWaveController` joue hors Play Mode le cycle complet vague normale puis
vague inversee. Ses parametres
exposent l'origine viewport, la direction de pousse, la frequence, la vitesse de
propagation, l'amplitude, la duree, l'attenuation, le fondu de sortie,
`highlightIntensity`, `highlightColor` et `edgeContrast`.
Le composant `ScreenWaveController` reste active sur le `BattleManager` : son
`Update` en temps non scale fait avancer le cycle et desactive le pass apres le
fondu final.
La sortie wave vers normal est un release progressif : `StopScreenWave` lance ce
fondu, puis le pass est coupe seulement apres une frame neutre. Le flux combat
utilise `PlayScreenWaveCycle(origin)` pour garder le Custom Pass actif entre la
phase `reverse = false` et la phase `reverse = true`; le placement en arene se
fait a la fin de la premiere phase. Le gel d'entree et le timing de placement
restent calcules sur la duree principale de la vague, pas sur le fade-out.
Le snapshot monde du retry combat utilise une capture qui conserve les issues
de validation mais ne les log pas en erreurs console, afin de ne pas polluer
l'entree combat avec des providers de scene incomplets deja presents.

### Telegraphie des reactions temps reel

Les attaques ennemies temps reel exposent un `CombatReactionTelegraphProfile`
dans leur `SkillSO`. Il est optionnel et contient le prefab de pulse, les
couleurs de menace/fenetre parfaite, les AudioClipSO, le fade et le micro-ralenti.
Un clip ennemi conserve seul les timings : `BeginReactionTelegraph` affiche la
menace sans ouvrir de logique, `OpenReactionWindow` ouvre la fenetre et
`ResolveEnemyAttackImpact` la ferme et resout les degats. Ne pas remettre
`ShowInput` ou `HideInput` sur ces clips : ils restent reserves aux sequences
non-combat.

`CombatReactionTelegraphController` est le pilote unique de presentation sur
`BattleManager`. Il gere un seul prompt world-space et le nettoie sur impact,
desengagement, mort, changement de lock et contre. Le micro-ralenti est gere
par `CombatImpactFeedbackController` en temps non scale et ne peut pas
restaurer `Time.timeScale` pendant une pause de CounterSkill ou un hit-stop.
Avant chaque phase, le controleur resout a nouveau le prompt actif afin de ne
pas conserver une reference detruite lors d'un changement de scene. Son Canvas
world-space se rattache a `Camera.main` et utilise un ordre de rendu eleve.
Le menu `Lit/Combat/Configure Reaction Telegraph` configure Assomoir et le
prompt `RealTimeCombatReactionPrompt` de `Bootstrap`.

### Locomotion et Assommoir

Le Juggernaut conserve maintenant son engagement apres le premier degat de
lumiere, meme si Lucian quitte ponctuellement son champ de vision. Sa boucle
temps reel separe `Chase`, `Position`, `Observe`, `Attack` et `Recovery` :
avant chaque nouvelle attaque, il se replace brievement avec le
`CombatEnemyLocomotionController` plutot que de figer a portee. Les durees
d'observation et de recuperation sont exposees sur
`RealTimeCombatEnemyBehaviour`. L'option `logCombatDiagnostics` permet de
tracer l'etat IA, le NavMesh et le motif exact d'un arret.

La locomotion retourne explicitement a `Idle` lorsque la vitesse NavMesh est
nulle; elle ne peut donc plus conserver une pose de marche apres un arret. La
sonde du `CombatEnemyPhysicsMotor` ignore les personnages, VFX, UI et triggers,
et sa reprise NavMesh est refusee si aucune surface locale coherente ne peut
etre prouvee. `SceneMarker` journalise egalement un spawn ennemi hors NavMesh
sans jamais le teleporter vers une autre hauteur.

### AnimationLab et bake cinematographique

`Assets/Scenes/Workshop/AnimationLab.unity` est un atelier hors Play, distinct
du laboratoire de combat Iluvilirae_Test. Le composant CombatAnimationLab
reference Lucian et Juggernaut_v2 face a face (1,7 m), deux Animators racines,
un Director, un Brain et une camera d'ensemble. Les copies visuelles n'ont
ni scripts gameplay, ni IA, ni navigation. Le parent _PreviewActors doit etre
actif : sa desactivation rendait les acteurs invisibles et inanimables.
Dans l'Inspector du root AnimationLab, choisir Player/Enemy Clip et utiliser
le curseur ou Lire les clips en boucle. AnimationMode restitue les poses a
l'arret, a la fermeture de l'Inspector et avant Play. Pour une Timeline,
choisir l'asset puis Preparer et ouvrir la Timeline ; utiliser la lecture et
le curseur natifs. Les pistes Player.Animator/Enemy.Animator sont rebindees
sur les previews, Cinemachine sur le Brain, les Signals sur un receiver vide
pour ne pas simuler des effets de combat. Les pistes supplementaires peuvent
etre liees manuellement dans la fenetre Timeline. Le contenu des assets n'est
pas modifie par le preview. Le menu Lit/Combat/Reconfigure AnimationLab
(Editor Preview) reconstruit les copies depuis les prefabs courants ; l'ancien
menu Update AnimationLab Root Animators pointe desormais sur cette commande.

Les previews cinematographiques suivent le contrat de gameplay : un
`ActorRoot` porte l'Animator de gameplay et le skeleton n'est qu'un enfant
visuel. Pour les rigs de bake historiques, `Enemy_Preview` du prefab
`AnimationLab` reproduit `Juggernaut_Combat` et utilise `Juggernaut.controller`; `MidPoly` ne porte pas
d'Animator. Les bakers de `LightSkillTimelineAuthoringRig` et
`CombatSkillTimelineAuthoringRig` resolvent/bindent les `ActorRoot` previews
avant de copier une Timeline runtime, et les releves de cadrage sont pris sur
ces roots, jamais sur un mesh enfant. Une validation bloque un bake lorsque le
preview Animator n'est pas sur son root. Le menu
Le prefab historique AnimationLab conserve ses adaptateurs de bake ; le
nouvel atelier de scene permet la previsualisation sans exiger une SkillSO
ou lancer les services de combat.

`CombatEnemyLocomotionController` est le pont reutilisable entre la navigation
et l'Animator ennemi. Il ne deplace jamais un Transform : `NavMeshAgent` garde
l'autorite hors action et le controleur traduit sa vitesse locale relative a la
cible vers `CombatMoveX`, `CombatMoveZ` et `CombatMoveSpeed`. Le menu
`Lit/Combat/Configure Combat Locomotion` ajoute les blend trees Root et le
composant aux prefabs Juggernaut et GiantJuggernaut. Le NavMesh conserve le
deplacement monde pendant cette locomotion, tandis que le controleur reapplique
l'orientation vers la cible en fin d'image; `Validate Combat
Locomotion` en controle les etats et parametres.

Pendant une action ennemie, `CombatEnemyPhysicsMotor` est la seule autorite de
deplacement. Un `EnemyActionMotionProfile` peut demander une ruée homing : la
verticale reste balistique, la ruée possede seulement le plan horizontal,
suit la cible jusqu'a sa distance d'arret et applique un CapsuleCast contre le
decor. `Assomoir` utilise dans cet ordre `BeginEnemyAirborne`, telegraphie et
fenetre de reaction, `BeginEnemyRush`, impact, `EndEnemyRush`,
`RequestEnemyLanding`, puis `EndEnemyAttack`; aucun clip ne doit lui ajouter
`DashToTarget`.

Le moteur conserve aussi une hauteur de sol de secours au debut de chaque
action. Une demande d'atterrissage ou un timeout sans sonde de sol valide pose
l'ennemi a cette hauteur, en conservant son plan horizontal : une couche de
decor mal configuree ne peut donc plus provoquer une chute infinie.

### Contrat de spawn ennemi

Les ennemis places par `SceneMarker` relisent leur `CharacterData.worldPrefab`
au moment du spawn; le cache Netcode ne conserve que l'identite du marker et
est invalide a chaque nouveau domaine runtime ainsi qu'apres modification d'un
prefab ou d'un asset personnage dans l'editeur. `CombatEnemyRuntimeContract`
audite le prefab source puis le clone : Animator avec controller,
`RealTimeCombatEnemy`, `EnemySkills`, `CombatEnemyPhysicsMotor`,
`CombatActorAnimationRoot`, Rigidbody cinematique, CapsuleCollider et
NavMeshAgent doivent etre presents sur le root. Un clone invalide reste visible
mais son IA, sa navigation et son combat sont desactives, avec un rapport
source/clone exploitable. L'IA attend egalement une projection NavMesh locale
coherente avant d'entrer dans sa boucle. Une riposte, en particulier Assomoir,
ne peut jamais se lancer si le moteur physique est absent ou non operationnel.

Le `SquadAIManager` reconstruit aussi son NavMesh dynamique lors de tout
chargement de scene. Son volume calcule ses bounds sur les colliders actuellement
charges, afin de couvrir les districts places loin du root persistant. Un
NavMeshAgent qui s'initialise avant ce bake peut etre temporairement desactive :
c'est un etat attendu, il ne constitue pas une divergence de contrat et l'IA
reprend seulement apres une projection locale valide.
Les demandes explicites de rebuild (chargement de district ou ennemi en attente)
sont prioritaires sur `autoUpdateNavMesh`, qui ne pilote que les rebuilds
periodiques. Elles attendent une frame complete de registration des colliders,
puis publient colliders trouves, layers retenus, sources et bounds. Un bake sans
source retire la surface runtime invalide et maintient l'ennemi en attente ; les
reactions tactiques sont elles aussi suspendues jusqu'a validation du NavMesh.

Lorsqu'un ennemi ne trouve aucune projection locale, il demande un rebuild
cadence au `SquadAIManager`. Le manager journalise les bounds et le nombre de
sources du bake: ce rapport doit etre consulte avant de modifier une IA, car une
absence de surface sous le spawn empeche volontairement toute navigation et
toute attaque physique.

## Pièges observés

- `CombatSessionManager` coordonne plusieurs systèmes : limiter les changements.
- Ne pas confondre `CombatHealth` avec la santé du `SquadCharacterController`.
- Une action couverte de transition doit être exécutée même si la transition est
  interrompue.
- Le serveur est l’autorité en multijoueur; les clients gardent une présentation locale.
- Le ralenti de combat est cosmétique, local au client engagé et
  animation-driven; ne pas utiliser `Time.timeScale` ni le declencher depuis le
  manager ou la camera.
- Les items défensifs de réaction ennemie sont choisis depuis l'inventaire local,
  mais l'absorption, la casse et la synchronisation d'inventaire restent côté
  autorité.
- Les PV restants des items defensifs sont un etat de personnage, pas un etat de
  session de combat; ne pas les stocker dans `CombatSession`.
- Un item defensif doit etre assigne aux 3 items combat du personnage pour etre
  utilisable dans `CombatDefensePanel` et dans l'inventaire de reaction.
- Le joueur local doit être résolu via `LocalPlayerContext`; éviter les fallbacks
  arbitraires qui peuvent viser le mauvais personnage en Netcode.
- Tester victoire, défaite, déconnexion et destruction pendant une transition.
- Les approches d'attaque doivent toujours restaurer la position de combat en
  fin d'action ou lors d'un abort/despawn.
# Paliers de vie cinematographiques

Les paliers optionnels sont definis par ennemi dans `CharacterData`. Un coup qui franchit un palier est plafonne au seuil. Chaque palier reference un `ThresholdSequence` autonome : avant son lancement, `CombatHealthThresholdController` cherche a exactement 2 m devant l'ennemi, puis a +/-15, +/-30 et +/-45 degres, une pose UCC au sol sans decor bloquant. Lucian est pose sur cette position et l'ennemi pivote vers lui; si les sept poses sont bloquees, le palier attend sans rearmer l'ennemi. Les paliers n'utilisent ni `CombatCinematicRig`, ni Timeline, ni pool cinematographique.

Une `ThresholdSequence` lance un clip InPlace de Lucian contenant un ou plusieurs Animation Events `QTE(string input)`, dans leur ordre auteur. Au runtime, un `AnimatorOverrideController` temporaire injecte le clip QTE choisi dans l'etat generique `ThresholdSequence_QTE` et le clip de reussite choisi dans `Threshold_Succes`; le controller original est restaure a la fin de la sequence. Sans clip QTE specifique, `ThresholdSequence_QTE` conserve son clip par defaut. Chaque reussite ferme son overlay, laisse le clip continuer vers l'evenement suivant et n'active l'animation de succes qu'apres le dernier QTE valide. Une mort de palier est appliquee au delai configure, mais le combat reste suspendu jusqu'a la fin du clip de succes; le joueur fond ensuite vers `CombatIdle` avec `successExitBlendSeconds` avant la restitution. Un echec coupe la chaine par un fondu vers `CombatIdle`, puis applique le resultat d'echec de la sequence. Les valeurs sont `Y`, `B`, `A` et `X`; l'evenement ouvre la map isolee `CombatQTE` pendant la duree reelle configuree dans `CombatHealthThresholdController` (0,5 s par defaut; `Y/B/A/X` manette, fleches haut/droite/bas/gauche clavier). Pendant cette fenetre, le controller demande au `TimeManager` un ralentissement global configurable (0,4 par defaut). L'overlay et son timer restent non-scales, donc la fenetre ne change pas de duree. Une touche tenue avant l'ouverture est ignoree.

Une reussite lance immediatement l'etat Animator de succes, puis applique `KillEnemy` ou `ResumeCombat` apres le delai configure dans la sequence. `KillEnemy` force les PV a zero et la mort de l'ennemi, puis utilise `CompleteThresholdKill`: combat, lock et HUD se ferment sans declencher le panneau de victoire standard. Un echec reprend le combat ou lance le Skill ennemi configure; son impact normal est route vers `ResolveThresholdFailureImpact()` et demande un recul UCC de trois metres, interrompu par les collisions. Une sequence ou une riposte invalide ne bloque jamais le combat.

`QTEPanelController` est la presentation unique de scene : il trouve `QTE_Zone`, affiche `QTE_Circle_Slider` a une position aleatoire sure, associe le sprite `A/B/X/Y` et vide son anneau radial. `CombatHealthThresholdController` conserve seul le timer, les inputs et le resultat gameplay; il resout le panneau actif y compris lorsqu'il demarre desactive et journalise les CanvasGroups parents si l'overlay ne peut pas etre vu.

# Skill target lunge

## Eclair et Bond offensif

Eclair : useLaunchRange=true, minimumLaunchDistance=0, maximumLaunchDistance=10.
Son impact confirmé applique 1 seconde d’étourdissement via le pipeline existant.
holdControlsUntilAnimationEnd conserve le slot planaire UCC après la traversée,
avec vitesse demandée nulle : Lucian garde l’orientation de sa charge, sans
se retourner vers la cible passée derrière lui, et les entrées de déplacement/
mobilité restent bloquées jusqu’à la fin du clip (recoveryNormalizedTime=1,
allowMobilityCancel=false). Les collisions avec la cible sont rétablies dès
la fin de la traversée ; seule la commande de locomotion reste verrouillée.
TerminateAction libère le slot aussi sur interruption, mort et désactivation.
Bond offensif conserve sa récupération et ses réglages indépendants.
Validation : EclairInertiaPlayValidation passe sur l’impact réel, le stun 1 s,
la direction de traversée, le maintien du stick bloqué pendant le clip, sa
restitution et l’interruption. TargetMobilityPlayValidation passe ses 14 cas,
dont murs/sol/BattleWall, grandes cibles et leases de collisions imbriquées.
Logs EclairInertia.log et EclairMobilityRegression.log dans
Library/ActorAnimationSandbox/Logs. Une référence détruite dans EndCombat à la
fermeture du laboratoire survient après les validations ; elle reste hors de ce lot.
Bond offensif : 2-30 m. Ces bornes inclusives comparent horizontalement les
ActorRoots avant animation/VFX/audio ; elles ne changent pas la portee des degats.
Sans override, RequireValidRangeToStart conserve le comportement historique.

Les deux profils targetLunge utilisent resolveImpactAtContact : Eclair traverse
la cible et vise sa position initiale + 4 m sur l'axe d'approche, Bond s'arrete
devant elle sans impulsion. La duree tient compte de la distance et du plafond de
vitesse ; l'intention physique passe exclusivement par le slot planaire UCC.
Le temps local/global est respecte, et un verrou externe incompatible refuse
le lancement plutot que de jouer une attaque fictive.

contactAnimationSlot reference le clip original du state Skill_1_Eclair.
Le clip Eclair_Contact est substitue uniquement durant la session. Son unique
OpenSkillContactWindow est place a 0,08 s, apres le fondu d'entree de 0,06 s.
HitEnemy/ResolveSkillImpact ponctuels sont absents de cette copie ; VFX de contact
et degats sont emis une seule fois apres confirmation spatiale, via la resolution
existante. Les VFX d'attaque et les sons d'origine sont conserves.

LitOpsiveLocomotionBridge.TryAcquireTargetCollisionBypass renvoie un handle
idempotent : il combine colliders ignores UCC et paires Physics.IgnoreCollision.
Les scopes imbriques partagent leurs compteurs et preservent l'etat anterieur.
Aucun collider ennemi n'est desactive. Fin, interruption, desactivation et
transition de scene rendent les ressources possedees, sans modifier un autre
verrou. Un trajet qui ne permet pas de ressortir de la capsule cible s'arrete
devant elle. Les murs, autres acteurs, BattleWall et bords de sol restent prioritaires.

Iluvilirae_Test expose additionalCombatSkills, ajoute aux skills connus/equipes
du laboratoire uniquement ; CharacterData n'est pas modifie. Le menu
Lit > Combat Lab > Configure Eclair And Bond Offensif reinstalle sans doublons.

Un `SkillSO` peut activer `Player Target Lunge`. Au lancement du skill, son animation est forcee en `InPlace`; `PlayerActionPresentationController` pilote une approche UCC tres rapide vers `EnemyLockPoint`, sans ecriture directe de Transform ni root motion. A la distance d'arret configuree, il relache le mouvement plan et applique une impulsion UCC arriere et verticale jusqu'a l'atterrissage. Une collision ou une cible non atteinte annule le rebond au lieu de traverser le decor.

# Temps de combat

`TimeManager`, porte par `ApplicationRoot`, est l'unique autorite de `Time.timeScale` et `Time.fixedDeltaTime`. Les systemes demandent un handle global ou local par owner; la pause gagne, sinon l'echelle la plus lente gagne. Toute duree de handle est calculee en temps reel non scale afin qu'un ralentissement ne se prolonge pas artificiellement.

`CombatTimeDomain` complete ce contrat pour Lucian et chaque ennemi : il compose son echelle locale avec le temps global et alimente Animator, locomotion, navigation, IA, physique et rusees. Les deltas root motion ne sont pas remultiplies dans le relay car `Animator.speed` a deja applique l'echelle locale.

Les Timelines LightSkill, CounterSkill et Skill restent `Unscaled` pour preserver leur montage. Les paliers n'utilisent pas de Timeline; leur overlay et leur fenetre de 0,5 s restent non scales.
# Navigation et combat

`Iluvilirae_Test` est le laboratoire autonome Lucian/Juggernaut v2, sans
Bootstrap ni EnemyController sur v2. Brains AI garde les decisions ennemies.
`ICombatTarget` expose cible, sante, engagement d'attaque, sequence d'action,
suspension et placement cinematographique ; `LegacyCombatTarget` conserve
l'integration des ennemis historiques. RealTimeCombatManager, ses inputs,
les competences, clarte, camera, presentation, palette, esquive, garde,
CounterSkill et LightSkill sont reutilises par le laboratoire.
Le CounterSkill du laboratoire conserve une Timeline commune pour Lucian,
Juggernaut et la camera. Son rig definit un plateau de 1,7 m : les deux
ancres sont placees a +/- 0,85 m, face a face. Au declenchement, le plateau
est centre et oriente depuis la position des acteurs ; les pistes Animator
utilisent les Scene Offsets et ne transferent pas de root motion au moteur.
Le placement est refuse si les positions quittent le NavMesh ou traversent
un obstacle. Les anciens rigs sans plateau auteur conservent leur contrat.
La camera du laboratoire capture ce repere une fois et evalue sa trajectoire
depuis le temps de la Timeline, sans retard de lissage par frame. Elle se met
a jour avant le pont camera du rig. La reaction ennemie commence au contact
de Counter_Sword (1,2567 s), au lieu de commencer au debut du contre ; le plan
camera dure jusqu'a la fin de cette reaction (2,4233 s).
CombatLab_CounterSkill_2 porte quatre signaux CameraShake aux temps auteur
0,3667 / 0,7667 / 1,2833 / 1,85 s. Le SignalReceiver du rig appelle
AnimationEvents.CameraShake, qui delegue au plateau de contre et a ses cameras. CombatCinematicCameraShake
applique une correction Cinemachine au stade Noise, sans deplacer les roots
ni changer la trajectoire auteur. Amplitude (0,06 m), duree (0,14 s) et
frequence (28 Hz) sont reglables sur les cameras du prefab du rig. Le temps
non ralenti permet de fonctionner pendant la pause du combat ; la correction
expire et est effacee au reset du pool. Les quatre marqueurs partagent le
signal CombatLab_CameraShake et emettent une fois par lecture.
Les plans dupliques de la Timeline ont des aliases camera explicites dans
le rig a camera unique. Validation : compilation, quatre appels observes
en Play Mode avec correction Cinemachine active pendant la pause, 63 tests
EditMode passes. Rapports : Library/CounterCameraShakeValidation. Le rendu
et la force de la secousse restent a ajuster visuellement dans l'Inspector.
CombatLab_CounterSkill_2 porte un SignalEmitter KnockedOut a 3,50 s.
Le SignalReceiver du CombatCinematicRig appelle KnockedOut sur la cible
reelle via ICombatKnockoutReceiver. Le VFX apparait au signal ; le stun
conserve 2 secondes de temps de jeu apres la liberation de la Timeline.
La selection du CounterSkill reste conservee : l'asset du laboratoire
reference encore CombatLab_CounterSkill_1 ; la seconde Timeline est prete
a etre choisie dans son champ Timeline.
Les anciennes Timelines sans signal gardent leur stun a la fin d'un contre
ayant touche. Un impact de type Counter seul ne programme plus de stun. LitBrainsEnemy joue l'etat
`Knocked Out`, bloque navigation/rotation/attaques puis reprend la locomotion.
Les autres impacts infligent leurs degats sans ecourter ce stun ; une mort
est prioritaire. Une pause ou une nouvelle cinematique suspend le decompte.
L'etat Knocked Out utilise Mixamo_KnockedOut sur 2 secondes, sans
transition de sortie automatique. Les appels repetes ne relancent ni le
decompte ni l'effet. Les impacts ne declenchent pas Hurt pendant cet etat.
La reference publique `LitBrainsEnemy.VFX_KnockedOut` definit l'effet du stun.
Il est instancie une seule fois sur l'os Head (racine si non humanoide), puis
desactive et detruit a la fin du stun, a la mort ou a la desactivation.
Juggernaut_v2 reference Assets/Prefabs/VFX_KnockedOut.prefab par defaut ;
l'assistant conserve une reference remplacee par le developpeur.
Validation : 61 tests EditMode passes et parcours Play Mode du signal
puis de l'ancien contre passes dans le projet isole. Le test du signal
prolonge uniquement sa queue a 4 s pour observer le VFX avant liberation
du graphe ; la Timeline auteur termine apres 3,50 s. Animation/VFX,
degats sans Hurt, immobilite, expiration sans prolongation et mort testes
sans affichage. Rendu visuel a confirmer en jeu.
Reglages auteur : CombatLab_CounterRig (ancres, offsets camera, degagement)
et CombatLab_CounterSkill.playable (clips et contact). L'assistant conserve
un plateau deja configure et une reaction dont le depart a ete edite.
Les contacts v2 viennent des Animation Events propres (reaction puis impact),
avec portee/angle/obstacles et protection d'esquive. Walk/Run directionnels
suivent la vitesse NavMesh effective. Victoire, defaite, pause et recreation
nettoient les verrous et les entrees de simulation UCC. Les scenes de perception
conservent leurs commandes de test. Reseau, sauvegarde et recompenses ne sont
pas simules. Le menu Upgrade Lucian Combat Laboratory reconstruit explicitement
la camera, palette et profils depuis GameplaySessionRoot ; pas de carte input
privee ni de creation automatique lors d'un import.

Les ennemis de combat utilisent `NavMeshWorldService` pour valider leur monde avant toute poursuite ou attaque. Un `EnemyNavigationController` hors monde valide reste inactif et ne compense jamais une projection lointaine.

Le Juggernaut distingue maintenant les phases `Walk` et `Run`: il marche pour
les repositionnements proches et court au-dela de 8 m. La vitesse NavMesh et
`LocomotionTier` sont synchronises avec les seuils reels du controller commun,
afin que la course franchisse bien le seuil Animator de 2,5.
## Reception commune des animations et signaux (2026-10-09)

`AnimationEvents` remplace `PlayerCombatAnimationEvents` et conserve son GUID,
ainsi que les noms des callbacks des clips existants. Les personnages du jeu,
leurs objets Animator et les objets portant un PlayableDirector recoivent ce
composant. Les packs tiers, demos et `_Recovery` sont exclus de la migration.
Les callbacks joueur, ennemi historique, Brains AI, pas, SpiritBond, levier,
projectile, impact, CameraShake et KnockedOut deleguent aux services existants.
Les services ne conservent plus les anciens noms de reception sur le meme
objet : un evenement ne peut donc pas appeler deux implementations.

Dans l'Inspector : Actor Owner designe le personnage proprietaire ; Signal
Target peut imposer la cible d'un signal ; Camera Root designe les cameras a
secouer. Un Director utilise ses bindings actuels, sans conserver la cible de
la lecture precedente ; le plateau de contre conserve son contexte de combat.
Une destination absente ou ambigue est signalee une fois, sans recherche globale.
Les methodes `Signal...` permettent de choisir dans les UnityEvents les actions
joueur dont le callback de clip recoit un AnimationEvent. Les controles metier
(droit d'attaquer, degats uniques, mort, stun) restent dans les services.

L'aperçu hors Play n'exécute aucun gameplay. Preview Only prolonge cette securite
si AnimationLab est lance accidentellement en Play ; Last Signal fournit le
retour auteur. SpiritBondAnimationActions conserve les references et reglages
historiques de SpiritBond, LocomotionAnimationEvent conserve ceux des pas.
Les assistants de personnages et plateaux, RequireComponent et le branchement
ObjectFactory de l'editeur installent le recepteur pour les nouvelles creations.
`Lit/Animation Events/Validate Game Content` controle les prefabs et scenes ;
ce controle est execute avant build. `Repair Game Content Receivers` complete
les montages importes, preserve les instances et retire les slots de signaux
sans asset, qui ne peuvent jamais etre declenches.

Validation de la migration : 70 tests EditMode cibles passes, compilation Unity
runtime/editeur et parcours Play Mode du contre passes avec vrais acteurs/UCC
et gamepad simule. Les quatre secousses sont observees aux temps auteur ; stun,
VFX unique, degats sans Hurt, expiration et restitution des commandes verifies.
Audit statique de 1 378 assets source et validation native du contenu disponible
dans le projet de test : aucun recepteur manquant ni reaction invalide.
Rapports : Library/AnimationEventsMigrationValidation. Cette validation ne
constitue pas une inspection visuelle HDRP ni un essai sur manette physique.
Une passe historique plus large garde des echecs hors migration (champs prives
de reactions et anciens contrats de presentation/Animator/masque de torche).
## Presentation et reconstruction ciblees (2026-10-10)

Assets/Characters/AnimationPresentation contient l'audit non destructif sur
les avatars des prefabs, la reconstruction source et les diagnostics Play.
Les rapports vont uniquement dans Library/ActorAnimationValidation. Les
semelles sont mesurees sur le mesh skinne ; l'audit Editor utilise le skinning
CPU car BakeMesh peut rester en bind pose pendant une evaluation manuelle.
Les mesures anciennes de semelle Editor ne constituent pas une validation
d'appui ; les positions des os et la fidelite source restent exploitables.

Lit/Animation Presentation/Prevalidate Juggernaut Reconstruction construit
les candidats sans ecriture, puis Apply Validated Juggernaut Manifest controle
les empreintes prefab/source/copie avant toute application. Sous-asset ambigu,
source absente ou perte de pose bloquent l'application. Les clips deja aplatis
ne sont jamais une source. Evenements et courbes supplementaires des copies
restent conserves, CopySerialized preserve les GUID. Une seconde application
est sans changement. Les clips non attribues, notamment KnockedOut, ne sont
pas reconstruits par approximation ; aucune migration generale des ennemis.

Le Juggernaut conserve NavMeshAgent comme moteur. La reconciliation de hauteur
est opt-in et agit sur baseOffset avec sol physique, couche Ground/Default,
pente et distance bornees ; aucune teletransportation ni correction de bassin
devinee. La conversion preserve la variation corporelle verticale du source.

VisualActionHandoff ajoute une fenetre de recuperation visuelle et une phase
de destination aux profils existants, sans changer les marqueurs gameplay.
Seul un raccord explicitement valide peut retimer la recuperation apres son
marqueur. Annulation remet le multiplicateur a 1. Les trois attaques du
Juggernaut ne passent pas encore la comparaison de pose des semelles avec
Idle (environ 5,3 a 6,1 cm au raccord) ; une recuperation dediee/retouche est
requise. Ce critere de pose est distinct du glissement cumule en Play.
Les profils de raccord ne sont pas actives dans le jeu ni dans le parcours
laboratoire par defaut.

Essai explicite : ouvrir Iluvilirae_Test puis Lit/Animation Presentation/Try
Enemy Recovery Preview in Iluvilirae. Un controller distinct sous le dossier
Iluvilirae/PresentationPreview et un profil runtime isolent l'experience.
IK Humanoid natif sur Locomotion et les deux frappes candidates, recuperation
retimee apres son marqueur et suppression des sorties automatiques concurrentes
sur ces seuls etats copies. Strike/Followup atteignent environ 2,985 cm d'ecart
de pose avec Idle ; Sweep reste a 4,14 cm et garde son comportement initial.
Ce seuil de pose ne valide ni les appuis pendant les cycles ni le fondu entier.
Preparation/impact non retimes, Root desactive et controller original intact
verifies en Play isole. Sortir du Play restaure les references, sans transferer
ces candidats au jeu ; IK et marche directionnelle restent a accepter.

ActorAnimationContractValidator controle les deux acteurs avant build et
refuse les profils sans supportValidated. ValidateTechnical controle les
references, Root physique des clips reconstruits et fidelite source, sans
pretendre valider les appuis. Les audits ne reparent rien automatiquement.
Les copies et profils candidats sont prepares uniquement dans le projet
isole Library/ActorAnimationSandbox. Acceptation artistique et parcours
complet de regression obligatoires avant transfert des profils au jeu.

## Locomotion des jambes pendant une action (2026-10-13)

La couche Moving Legs des controllers joueur couvre seulement les jambes.
Elle reutilise les arbres marche/course et strafe, avec parametres dedies et
clips sans events : aucun impact, bruit de pas ou callback n'est double.
Le torse conserve la garde/attaque en cours pendant un deplacement autorise.
Les actions qui verrouillent le mouvement (Eclair, Rupture, etc.) gardent leur
animation complete ; la couche est alors a zero, comme en vol ou a la mort.
MovingLegsAnimatorSetup est idempotent et couvre aussi Lucian_ImpactLab quand
cette copie existe. Les nouvelles copies de controllers doivent garder cette
couche ou etre equipees par Lit/Animation/Install Moving Legs Overlay.
Controle de poses isole disponible via MovingLegsPoseValidation.RunBatch ;
le ressenti des appuis et les transitions doivent etre controles en Play.
# ChargedSkill personnel (laboratoire, 2026-10-14)

CharacterData.chargedSkill est une reference unique optionnelle, hors roue.
X lance une BasicSkill immediate ; son maintien ne repete plus les BasicSkills.
Au sol, Lucian attend la fin complete de cette BasicSkill, puis entre dans une
preparation InPlace si X reste tenu. Le seuil de 2 s utilise le temps reel depuis
la pression ; relacher ou perdre la session annule la charge. Une nouvelle
pression est obligatoire apres lancement/refus. Les autres personnages restent
sans ChargedSkill.

Ascension lumineuse : 0-3 m, un contact auteur ChargedSkillContact(0), 10 PV,
montee de 3 m en .55 s, maintien 1.2 s apres la montee, cooldown 8 s.
Le premier contact confirme projette la cible et interrompt son attaque :
exception explicite, sans priorite sur mort/cinematique. La fin de montee rend
les commandes pour les BasicSkills aeriennes manuelles. Aucun coup aerien
automatique, aucun degat de secours. ChargedFloat (Lucian) et ChargedFloatHit
(victime) sont des boucles InPlace sans Animation Events. La pose passive
n'est pas une action : un Basic aerien la remplace, puis elle reprend si le
maintien est encore actif. Les fondus durent .08 s.
UCC possede le deplacement joueur ; Rigidbody cinematique et capsule possedent
la reaction aerienne du LitBrainsEnemy, NavMesh suspendu jusqu'au sol valide.
Le maintien est borne et les callbacks sont filtres par generation.
Configuration : Lit > Combat Lab > Configure Ascension lumineuse. Assets et
controller Lucian_ImpactLab uniquement, aucune migration de production.
ChargedWindup remplace l'Idle de preparation ; geste d'anticipation InPlace
sans events. La camera accompagne uniquement cette preparation : -10 degres
de FOV et .6 m derriere Lucian, entree/sortie .25 s reel, via LitCameraDirector.
Ces valeurs sont configurables sur ChargedSkillSO. Relachement, lancement et
interruption liberent le cadrage. Les holds aeriens UCC sont identifies et
bornes en temps local ; un nettoyage ancien ne peut retirer celui du Basic
suivant. Une cinematique memorise la gravite de base, pas la suspension aerienne.
Apres Ascension, le hold des Basics aeriens est plafonne au temps de flottement
restant. A expiration, la gravite reprend meme si leur animation continue ;
aucune nouvelle Basic ne peut remettre la descente en suspension avant le sol.
L'entree ChargedSkill utilise un fondu fixe avec temps de clip explicite zero,
y compris lorsqu'il remplace une ancienne instance du meme etat Animator.
Projection reseau refusee explicitement tant que sa commande serveur dediee
n'est pas livree ; aucun client ne declare ses montants ou sa destination.
