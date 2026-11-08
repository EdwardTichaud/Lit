# Moonrise — rencontre de reference et parcours court

## Objectif en cours

Finaliser le combat ameliore dans une vraie rencontre de Moonrise, puis peaufiner
un parcours de 15 a 20 minutes. Ne pas assimiler un test automatise avec approches
UCC a une validation du ressenti ou de cette duree.

## Rencontre retenue

Regle choisie explicitement : les ennemis soumis aux Flames restent dans la zone
eclairee. `ProductionBrainsFlamePolicy` expose une contrainte de mouvement,
distincte de sa dormance : tout segment est limite a l'union connexe des spheres
des Flames fixes allumees compatibles avec les colliders physiques de l'ennemi.
Marge de securite interieure .08m ; les triggers ne dilatent jamais ce territoire.
Une Flame eteinte ne fournit aucun territoire et un dash ne traverse pas un trou
sombre entre deux sources. Aucune augmentation du rayon auteur ni teleportation.
Les destinations NavMesh sont bornees suivant leur chemin puis recalculees et
revalidees ; l'evitement est borne en vitesse pres de la limite. Avances/rush,
repoussement et placements de contre passent par le meme contrat optionnel
`ILitBrainsMovementPolicy`. La limite ne gele pas l'animation ni le temps du
combat ; la perte effective de lumiere garde la dormance precedente. Les cibles
hors du territoire restent atteignables seulement depuis une position eclairee,
dans la portee effective et avec ligne de vue, sans assouplir les impacts.
Tests45/45 : `Logs/MoonriseLitTerritoryTestsVerified.xml`. Chemin clippe utilise
un arret de .05m pour approcher la limite plutot que soustraire de nouveau la
distance d'attaque. Verification native fraiche
`f1592fc8c7074ec4aa86786373d3a885` :76 controles,zero erreur runtime,restauree,
`Logs/MoonriseLitTerritoryFullRouteInputVerified.log`. Le Juggernaut atteint la
limite vers(-9.51,-94.18,43.13),continue Strike a55PV sans dormance,puis meurt
par les vrais coups de Lucian,joue InPlace_Death_v2 et disparait. Contre/KO avec
degats et retrait VFX, A victoire, X torche, montee echelle, save/menu/reprise
conservent pose/PV/reserve/Flame et ce marker vaincu. Assertion distincte272 en
echec : Luc termine/reward/collier conserves, mais sa scene reste chargee.
Descente/leviers/Stillhaven non executes dans ce nouveau full. La recette ne
constitue donc pas un full passe ni une preuve de duree humaine15-20min.
Le premier run4dd6... s'arretait sans degat pendant KO : le pilote X utilisait
une fenetre modulo120ms que les frames lentes pouvaient sauter. Appui puis
relachement sont maintenant garantis sur des mises a jour distinctes ; les
inputs de jeu, degats, duree du stun et criteres de validation restent inchanges.

Reutiliser `District_1_Enemies/Enemies/SceneMarker_Juggernaut`, personnage
`c54e94b14c7826c4482529b6d4d739ec`, position auteur (-9, -94, 43).
Conserver identite persistante, prefab, kit et politique d'activation par Flame.
La scene de laboratoire et ses overrides ne doivent pas etre charges en campagne.
La Flame de couverture doit etre allumee par l'interaction payante normale,
pas par SetLit dans une recette revendiquant le parcours complet.

## Parcours cible, a chronometrer

| Sequence | Budget indicatif | Contenu et critere |
| --- | --- | --- |
| Chambre de Maelle | 3-4 min | Brasero ancien (20), indices de Jon, lecture de la tombe et Luc avant la sortie ; fil narratif propose, non rendu obligatoire. |
| Rencontre Juggernaut | 3-4 min | Porte Maelle puis Flame commune (10), reperage frontal, poursuite, esquive/garde, contre et Knocked Out. |
| Exploration du balcon | 2-3 min | Echelle 1 aller/retour et lecture accessible ; verifier d'abord la sortie physique du palier et le retour. |
| Corridor ouest | 4-5 min | Deux leviers temporises avec fenetre locale de 20 s, puis ouverture du passage ; sortie physique vers les Cours en verification. |
| Sortie et reprise | 3-4 min | Acces aux Cours/Stillhaven, sauvegarde et reprise ; verifier le passage ouvert et le placement runtime du portail. |

Total cible : 15-20 minutes. Ces budgets sont un objectif de conception, pas une
mesure actuelle. Le trace physique exact, les indices et l'echelle seront retenus
apres inspection du montage reel ; ne pas deplacer les contenus pour masquer
une rupture de navigation ou rallonger artificiellement le parcours.

Budget torche a verifier : 1000 points = 16 min 40 s de torche sortie hors boost,
avant cout des Flames (10) / Ancient Flames (20). Le boost consomme 20 points/s.
Premier constat runtime : Lucian arrivait avec 300 points (5 minutes avant couts),
car `Lucian.asset/starterItemsWithQuantity` donne 300 `Item_Flame`, bien que
le prefab et le defaut du controller indiquent 1000. La seule quantite de ce
stack est corrigee a 1000, conformement au reglage utilisateur initial. Les
reserves des sauvegardes existantes ne sont pas remplies ni migrees. Le nouveau
depart a ete verifie par les recettes natives de combat et les couts reels 20/10 ;
ne pas se baser sur le seul defaut C#.
Le parcours doit permettre de ranger la torche dans les poches eclairees et de
comprendre son autonomie ; ne pas augmenter silencieusement la reserve pour
faire passer une recette de 20 minutes.

## Preuves attendues

Diagnostic ombres : runs `bb8ee958d6eb4221ba928838f8c152e9` puis
`56289825de1a4e4cb5f1af6a356a183e`,34/35 controles,zero erreur/restaures.
Comparaison GPU baseline / ombres locales .75 / sans ombres : les personnages
redeviennent lisibles sans ombres, mais le .75 local ne suffit pas a qualifier le
rendu. Cause logicielle trouvee : `FlameLightReceiver.ApplyShadowing` ecrasait
le profil `LitContrastLight` en forcant HD shadowDimmer1. Le receiver laisse
desormais tout le reglage d'ombres au profil present ; sans profil, son ancien
comportement est conserve. Tests lifecycle avec/sans profil :15/15,
`Logs/MoonriseShadowOwnershipTests.xml`. Recette native du fix
`e821d550cc5a4050b5470ad69eca71f0`,35 controles,zero erreur/restauree,
`Logs/MoonriseShadowOwnershipNative.log` : valeur du warm proche de Lucian
0.78 auteur conservee apres allumage/contre (avant1). Lune toujours1,
intensites/exposition/fill inchanges. Captures inspectees, corps encore sombres.
Le pilote ne change que temporairement les ombres, restaure aussi sur nettoyage
et ne revendique ni victoire, ni parcours complet, ni qualite artistique finale.

Correction de protection sans lock : `ResolveCombatVisibilityRoot` separe la
visibilite du verrouillage manuel. Masque/near clip preferent le verrou vivant,
sinon la cible du combat actif ; le choix de vue continue a utiliser le verrou
seul. Tests66/66 `Logs/MoonriseUnlockedVisibilityTests.xml` (camera, near clip,
contrats Moonrise). Run `29a0d1fa226c4562a17bb506123ee14b`,37 controles/zero
erreur/restaure, `Logs/MoonriseUnlockedVisibilityNative.log` : cible du masque
et corps physique de near clip verifies sans lock apres vrai contre, puis
passage dos et reprise. Captures inspectees : les deux personnages restent
trop sombres ; cette correction necessaire ne suffit pas a qualifier le rendu.
Aucune lumiere ni exposition modifiee. Diagnostic eclairage/ombres a poursuivre.

Verification native du passage dans le dos : run
`3f4bb70de86b42ffb2cdef1650706c0e`,
`Logs/MoonriseRearPursuitVerified.log`,36 controles,zero erreur,restauration terminee.
Apres vrai contre/Y, le stick deplace Lucian hors du cone d'acquisition pendant
le vrai KO. La cible connue reste memorisee, sans attaque/impact pendant le stun ;
au reveil le Juggernaut tourne puis se deplace de plus de25cm vers Lucian.
Le pilote ne force ni pose, ni degats, ni duree de stun. Son ancien ordre de
verification attendait un ecart2.85m avant de constater la poursuite : cet ecart
peut etre empeche par la poursuite elle-meme. Verification de reprise placee
avant cette demande, assertions par frame conservees sans multiplier les checks.
Captures `Moonrise_PlayerBehindEnemy.png` et `Moonrise_RearPursuitRecovered.png`
inspectees : decor masquant et personnages trop sombres, donc pas de validation
visuelle du ressenti. Ni mort/save/parcours complet ni duree15-20min prouves ici.

- Poursuite : detection naturelle, passage derriere la cible, obstacles et fuite.
- Attaques : locomotion, contact reel, degats uniques et recuperation.
- Contre : vrai input Y, Timeline configuree, cible capturee et controles rendus.
- Knocked Out : animation, VFX, deux secondes jouables, degats sans reaction Hurt
  ni nouvelle attaque ennemie ; reprise propre et VFX supprime.
- Mort : coup de Lucian via le gameplay, etat de mort puis dissolution/inactivation.
- Continuite : stick, interaction, echelle, torche, sauvegarde et rechargement
  conservant les PV, la torche, la Flame et la defaite de ce meme marker.
- Parcours : navigation continue et chronometrage reel 15-20 minutes, avec
  verification visuelle des gestes/impacts et du cadrage du contre.

### Prologue Luc/Jon depuis une nouvelle partie

`-moonriseLucRoute -moonriseLucRouteOnly` commence une nouvelle session isolee,
rejoint Moonrise, paie le brasero ancien puis marche vers collier, tombe et Luc.
Un defaut auteur a ete confirme : collier original (-7.5645,-93.0739,55.363)
eligible/eclaire/a1.09m mais le rayon joueur rencontrait Table avant son collider.
Le correcteur de pose sur table a refuse une hauteur excessive ; le collier est
finalement pose sur le vrai sol libre voisin (-6.74,-93.83584,54.54), dessous du
mesh a 3cm du sol. Source : seul Transform modifie dans la scene Luc, identite,
rotation, mesh et references conserves. Ni table ni regle de LOS modifiee.
L'outil MoonriseLucPlacementAuthoring et son diagnostic sous Library/MoonriseRoute
utilisent une surface physique et une capsule libre, sans pose runtime du joueur.
Les anciennes poses eventuellement deja enregistrees ne sont pas migrees ici.

Run b9b9f55490de400ea0844021514bd058 : 30 controles passes, zero erreur runtime,
restaure, Logs/MoonriseLucPrologueVerified.log. Une vraie recuperation/A donne
un collier et sa connaissance ; lecture/A de la stele, texte non bloquant/step,
Luc/A, ligne auteur, interaction/fin, recompense JonLocation et controle rendus.
Le recap reason du rapport mentionne encore UI/B (ancien libelle du fixture),
mais les checks et la branche executee prouvent la lecture non bloquante. Le
libelle source est corrige, rapport brut conserve. Ce test ne rejoue pas le
combat ni la suite et ne mesure pas 15-20 minutes.
Tests LucCycleTests et fabrique/prompt UI : 8/8 passes,
Logs/MoonriseLucContentTests.xml ; compilation Unity terminee.

Captures inspectees : dialogue de Luc lisible ; image de la tombe prise tout de
suite montre encore les notifications de recuperation/connaissance precedentes.
Verifier a .3/.8/1.3s avant de conclure sur la lisibilite reelle ou de modifier
les UI. Aucune attente artificielle ajoutee au jeu pour remplir le budget.

Cette verification est depuis faite : b0d68469dd9346b29e105359bed636c9 montre
l'epitaphe entierement cachee par InfoBox a tous les trois instants. Correction
commune InfoBoxUI : les toasts passifs s'effacent et suspendent leur compteur
pendant la lecture/dialogue, puis reprennent leur message et duree restants.
Modales et inputs preserves. edaf950a2dc54f1498ec80fd8c46e2c6 PASSE,34 controles,
zero erreur,restaure, MoonriseReadingPriorityNative.log. Images .8s/apres lecture
inspectees : epitaphe lisible, notification Collier de Jon reprise. Le petit temps
d'observation du probe -moonriseReadabilityProbe est uniquement du pilote de test.
18/18 tests UI prioritaires, inventaire/lecture, prompts et dialogues passent,
Logs/MoonriseReadingPriorityTests.xml. Aucun processus Unity encore actif.

### Retour du palier apres reprise de sauvegarde

Le test natif focalise `-moonriseLadderResume` reprend une copie de la sauvegarde
de validation apres victoire (41d77...), par le vrai chargement Bootstrap/menu.
Il ne touche ni la source de cette sauvegarde ni les slots utilisateur. Lucian
marche de 2,8 m hors du rayon haut, revient et presse A pour descendre.
L'ancien GetInteractionAnchor renvoyait Leafs_1 tant que detectedCharacter etait
nul : le joueur etait eligible en haut, mais le filtre de portee vers le bas
empechait la selection. Correction : extremite la plus proche du personnage local
avant selection, controles de passage inchanges.

Run e50c5c4391244c9f8ebfca5c45365ec1 : 10 controles passes, zero erreur runtime,
restauration confirmee, Logs/MoonriseLadderReturnFixed.log. Torche equipee,
consommation et controles a l'arrivee verifies. Capture du haut inspectee :
Descendre visible, decor encore sombre/encombre. Cette preuve concerne le retour
apres reprise, pas une montee/descente dans la meme session ni le rythme complet.
Regressions echelle/torche/detection/selection : 32/32 tests EditMode passes,
Logs/MoonriseLadderInteractionTests.xml, dont selection des deux extremites
sans personnage deja detecte. Compilation Unity et recette native terminees.

### Enigme des leviers et sortie ouest

La recette `-moonriseWestRoute` prolonge la reprise/echelle par une marche reelle
jusqu'aux deux leviers. Un seul acteur de squad actif, input A reel, aucun appel
SetActive ni injection de reussite. Baseline 9bfc8808e99d48949ce5e75236f03e33 :
premier levier eteint avant le second, trajet de 25,15 m en 10,42 s ; le mode
Timed/1 s ne permet pas ce trajet solo. Deux overrides locaux activeDuration=20
ajoutes dans District_1_Enigme_Levers, sans modifier le prefab Lever ni son mode.
Run b11c08e6cd9241739ce73d1c20a87c89 : deux activations reelles, chevauchement
et puzzle declenche en 10,29 s. Ensuite arret sur chemin NavMesh incomplet vers
le portail initial (-55,-94.227,33). Les essais physiques 9aa068... et 91d653...
rencontrent les cloisons ouest du corridor (x=-51), meme apres contournement
du premier pilier. Le pivot du puzzle ouvre un autre mur : il ne garantit pas
ce trajet vers le portail libre.

Le portail est replace cote couloir praticable a (-47,-94.227,36). Identite,
hierarchie, references et destination conservees ; ses quatre enfants d'arrivee
compensent le delta (8,0,3) pour garder les points monde (1199/1201,.1,0/2).
Le generateur auteur conserve aussi ce nouveau placement. Aucun mur ni collider
de l'environnement supprime. Les detours de diagnostic du pilote sont retires :
le test demande de nouveau un vrai chemin NavMesh et marche avec UCC/stick.
Run 28ce03d89e5e4236b36477a22dc5e3ae : 24 controles passes, zero erreur runtime,
restauration confirmee, Logs/MoonriseWestPortalAccessible.log. Descente, deux
leviers/A en solo (10,32 s), chemin complet puis 11,73 m marches vers portail/A,
quatre points monde et arrivee Stillhaven (1199,-.10,0), navigation/inputs prets.
Capture du portail inspectee : bloc de prototype encore peu explicite, sans
indication de destination (PortalController est actuellement sans UI). La
lisibilite du fil et le chronometrage restent a traiter ; le combat n'est pas
rejoue dans ce test qui reprend une sauvegarde de validation apres victoire.

Lisibilite suivante : deux leviers migres explicitement vers selection commune
(useSharedInteractionSelection) et composant WorldInteractionPrompt optionnel.
Textes Actionner / Rejoindre Stillhaven depuis UI_World_InteractionBox, cache a
l'eloignement ou suspension, detruit avec le proprietaire. Pas de double ecoute
de A sur ces leviers ; les autres scenes gardent leur ancien mode. Le bloc plein
du portail est recompose en arche ouverte, positions d'arrivee inchangees.
Run 5ef834706be3485bbf6bf1382e745539 : 31 controles passes, zero erreur runtime,
restaure. Logs/MoonriseReadableInteractionsNative.log. Deux textes de leviers
vus avant A, premier masque au second, portail affiche sa destination avant A,
Stillhaven charge et acteur controle. Captures du levier1 et du portail inspectees :
labels lisibles, arche ouverte ; rendu global toujours de prototype. Tests UI et
interactions : 36/36 passes, Logs/MoonriseReadableInteractionTests.xml. Cette
preuve ne remplace pas le parcours complet
avec combat actuel et la mesure de 15-20 minutes.
Le budget de 4-5 minutes inclut recherche/lecture : ce trajet moteur de dix secondes
ne prouve aucune duree de decouverte ni la duree totale de 15-20 minutes.

## Outil de verification en cours

`-moonriseFullRoute` assemble une nouvelle partie unique : prologue Luc/Jon,
combat actuel puis continuite/save/reprise et suite ouest. Aucun import de save
de validation. moonrise-route-timing.csv releve les checkpoints en temps reel
depuis l'arrivee Moonrise, y compris chargements ; cette conduite guidee rapide
n'est pas une qualification du rythme humain 15-20 minutes.

Premier full 6e3c1832b11a40d0a09f1a3d7489aea4 : 50 controles passes avant echec
fonctionnel, zero erreur runtime, restauration confirmee. Le Juggernaut quitte
sa Flame pendant le combat et se fige a61PV en Strike (-10.47,-94.22,43.50) ;
combat=false et joueur Exploration, sourceMoonriseFreshFullRoute.log/diagnostic.
Y/contre, vrai degat Timeline, KO/Mixamo/VFX, deux secondes jouables et degats
pendant KO sans Hurt/reprise d'attaque, retrait du VFX passes avant ce point.
La mort et les phases suivantes ne sont donc PAS validees dans cette execution.
La regle de sortie de lumiere est redemandee avec ce cas : maintien de rencontre
tant que Flame allumee ou bornage au territoire eclaire. Ne pas modifier ce choix
sans reponse. Prologue en12.056s ici : mesure rapide guidee, pas decouverte humaine.

`BootstrapDistrictPlayProbe.StartMoonriseCombatBatch` reutilise le cadre de
verification Bootstrap existant : copie possedee, sauvegarde isolee, restauration
des scenes et des options editeur, controles de hashes. Le mode `moonriseCombat`
est explicite ; il ne modifie pas les autres recettes.

Les approches par repositionnement UCC restent des fixtures declarees. Les
frappes ennemies, le Y de reaction et les attaques X utilisent les chemins de
production sans injection de degats, ForceDefeat ou stun force. La couverture
effective et les echecs sont dans le rapport de chaque execution sous
`Library/StartupDistrictValidation/PlayRuns/`. La recette reste en qualification ;
aucune validation du parcours complet n'est encore annoncee.

Premier essai `7a887774279b4f56936251270903da9a` : portail reel et Flame
payante (10 points) passes ; arret sur la fixture d'approche NavMesh de l'ennemi,
pas une victoire ni une preuve de defaut runtime. La recette attend maintenant
NavigationReady avant cette approche et conserve le detail des refus de projection.

Deuxieme essai `57d4cdd250684beb959fdd9bc038c606` : Flame payante et navigation
passes, poursuite naturelle de 5,86 m, deux impacts distincts et PV Lucian
1000 -> 960. Le joueur de test et l'ennemi ont ensuite quitte l'influence fixe
(rayon auteur 5 m) : dormance attendue et retour exploration, aucun contre possible
dans cette position. Execution arretee volontairement, scenes restaurees. Le
prochain essai garde la fixture de contre/victoire dans la zone eclairee, sans
agrandir la Flame ni supprimer la regle de dormance. La lisibilite de cette limite
et son integration au trace restent a peaufiner.

Essai suivant (Logs/MoonriseCombatContinuity.log) : poursuite 4,21 m, deux
impacts, vrai Y -> CounterSkill, degats de Timeline (300 -> 274) et Knocked Out
actif au retour passes. Arret sur l'absence du VFX sur le prefab production.
Deux erreurs auteur confirmees : `Juggernaut_Combat.VFX_KnockedOut` vide et
`ProductionProfile.knockoutState` pointant sur Hit au lieu de Knocked Out.
Corrections : raccorder VFX_KnockedOut et selectionner Base Layer.Knocked Out.
Cet etat et Mixamo_KnockedOut existent deja dans le controller de production ;
aucun clip ni controller de laboratoire n'est substitue. Un test d'editeur
verifie ce contrat. La recette native complete est relancee avec ces corrections
(Logs/MoonriseCombatKnockoutFix.log), resultat encore attendu.

Verification native suivante `9246fed014dc429bb924e04aac9481b4` : contre via Y,
VFX present, Animator sur Mixamo_KnockedOut (poids 1), pas de suspension cible
ni verrou joueur au retour. Laisser une evaluation ordinaire de l'Animator
apres destruction du graphe avant de verifier le CrossFade demande par le retour.
L'essai de victoire immobile avec X maintenu a ete arrete : le joueur doit
rejoindre l'ennemi qui reprend sa distance et refaire un appui apres blessure.
La recette utilise desormais stick camera-relatif/RT et appuis X frais, sans
repositionnement ni degats injectes pendant cette phase. La victoire et la
continuite ne sont pas encore prouvees par cet essai.

Contrat auteur valide en Editeur : `MoonriseEncounterContractTests`, 1/1 passe,
Logs/MoonriseEncounterContractTests.xml. La recette
`24ea3e3e8c5648e3a140cc87f14025bf` a ensuite obtenu une vraie victoire via stick/X,
observe Death puis dissolution/inactivation. Arret devant le panneau de victoire :
un vrai A est ajoute avant d'exiger le retour Exploration.
L'essai suivant sans verrou n'a pas reproduit cette victoire (PV ennemi 81 au
delai borne) ; le pilote de frappe est donc encore en qualification. Il demande
desormais R3 via le binding auteur apres le contre, puis approche au stick/RT
et frappes X. Cette demande ne modifie pas la preuve d'aggro initiale sans lock.
La capture camera RenderRequest fournie n'est pas une preuve du rendu complet
GameView/UI ; une capture GameView est ajoutee a la prochaine execution.

## Etat verifie au 29 octobre 2026

Recette native 077ad9a18a4e49f5ae326f1592f261ed : 50 controles passes, zero
erreur, restauration confirmee. Elle couvre le kit melee production, le HUD, le
contre/KO, la mort, A victoire, la torche, la selection reelle de VFX_Ladder_1
puis A pour monter, et sauvegarde/menu/reprise des etats. Les approches initiales
restent des fixtures UCC declarees ; aucune mesure continue de 15-20 minutes.
Tests Editeur courants : 14/14 (MoonriseTorchInteractionTestsVerified.xml).

Le rapport contient huit avertissements, dont trois attaques basiques recuperees
apres Animator entry failed. La recuperation evite le blocage mais ne valide pas
la fluidite de ces commandes. Les autres concernent des materiaux non compatibles
avec le masque tactique et un arbre importe. Une recette plus stricte observe
maintenant toute la fenetre KO : pose et VFX continus, zero frappe ennemie, degats
reels du joueur, puis retrait du VFX sur ennemi survivant. Resultat a verifier.

Recette fa523cc9423d48a7a174f3e0081b9a0f : fenetre KO observee image par
image de simulation (knockout-window.txt), coups reels acceptes en conservant
Knocked Out, aucune attaque ennemie et VFX retire apres expiration. Mort puis
retour exploration passes. Echec a la selection de l'echelle, pourtant eligible
et eclairee : selection vide, restauration effectuee. Le precedent succes de
traversee reste une preuve ponctuelle, pas de robustesse de cette selection.

La capture GameView du run 077 montre une forte occlusion architecturale apres
contre ; verifier le masque/cadrage avant de valider la lisibilite. Les transitions
Enter/ExitCombatIdle ont aussi un conflit possible avec une attaque entrante :
elles ne doivent agir ni pendant une action possedee ni vers une destination non
locomotrice. Gardes ajoutees et regression Animator lancee, resultat en attente.

Gardes de locomotion : PlayerActionContinuityRuntimeTests passe 5/5, y compris
le fondu vers une attaque depuis CombatIdle et CombatLocomotion, et le retour
ordinaire a l'attente. Recette native relancee avec traces passives des generations
/etats d'animation et de la selection d'echelle (orientation, portee, ligne de vue,
colliders interceptes, capture). Aucun filtre de selection contourne.

Recette native post-correction 989357b8fd0c46a8bc3b5bd666eff88b : 53 controles
passes, zero erreur, restauration confirmee, processus termine. Aucune recuperation
PlayerAction/Animator entry failed, cinq avertissements architecturaux/arbre
restants. KO complet avec degats et nettoyage, victoire/mort, A victoire, torche,
selection puis A echelle, sauvegarde et reprise tous passes. Diagnostic echelle :
range 0,952 m pour max 2,5, dot -0,117, ligne de vue vraie, VFX_Ladder_1 selectionnee.
L'echec du run precedent reste a expliquer. Ce succes n'etend pas la couverture
aux passages derriere un obstacle ni au parcours continu chronometre.

## Debut reel du parcours : chambre de Maelle

L'arrivee de la recette par le portail Maison est (-1,42,-93,97,55,15), dans la
chambre Maelle. Le trajet direct vers Flame_Base_2 est barre par Wall_2 a z~49 ;
le NavMesh partiel ne prouve pas un defaut de navigation. Inspection native :
Library/MoonriseRoute/navigation-inspection.txt. Le parcours retenu doit utiliser
le brasero ancien scene-flame:Maison:E6C2F108 (20 points), puis la porte Maelle
(-9,11,-93,88,49,19), sans cle, revelee par la chaleur. Ensuite seulement rejoindre
la Flame de rencontre (10 points) et Juggernaut. Ne pas deplacer le spawn ni ouvrir
la porte par SetOpen pour faire passer ce trajet. La nouvelle recette marche au
stick, interagit avec A et franchit physiquement le seuil ; resultat en attente.
Le franchissement utilise le moteur UCC dans l'ouverture : un chemin NavMesh
complet n'est pas requis a travers une porte encore fermee lors du bake.

Recette 20f1bdca194a4ba598fa82bcccc47ec5 : approche brasero, cout 20, ouverture
par A et franchissement physique reussis. Le Juggernaut engage ensuite naturellement
avant le feu commun. Le pilote est adapte pour respecter le combat actif (pas
d'interaction mondiale), puis allumer la Flame commune pour 10 apres victoire.
Retrait du repositionnement d'acquisition : l'approche utilise seulement le stick.
La source de Flame qui active vraiment l'ennemi est relevee, sans changer ses rayons.

Run bd0424a857224d81b502788aa41281ca : 62 controles passes, zero erreur,
restauration confirmee. Toutes les approches Moonrise sont marchees, incluant
brasero Maelle/porte/seuil, acquisition, victoire, feu commun, echelle et reprise.
Ce run revele une activation prematuree par le capteur de detection : la policy
comptait sa sphere trigger rayon 5 comme corps eclaire. Le corps physique devient
l'unique contact d'activation. Tests en cours ; cette preuve native precede le fix.
Les diagnostics montrent un pilier masque compatible/covered : reevaluer d'abord
la lisibilite avec le corps dans la vraie zone lumineuse avant d'ajuster la camera.
Les approches loguees durent quelques secondes ; elles ne prouvent aucun rythme
de 15-20 minutes ni les arrets narratifs a ajouter au parcours.

## Corps eclaire et placement auteur

Policy/lifecycle : 8/8 passes (MoonriseBodyFlamePolicyTests.xml). Le run
b5692221ec354fe7a458d7c7f49ac4a8 confirme ensuite que l'ancien marker n'a pas
de contact corporel avec une Flame. Son grand capteur masquait aussi ce defaut
auteur. Seul le marker Juggernaut passe de (-9,98,-94,42,53) a (-9,-94,43).
Inspection native : point NavMesh (-9,-94,13,43), capsule physique libre ; aucun
rayon change, identite de sauvegarde et prefab conserves. Les anciennes poses
sauvees ne sont pas migrees. Recette native du nouveau depart et reprise en cours,
Logs/MoonriseBodyLitPlacedEncounter.log (PID 33812). Aucun resultat encore affirme.

Run ff333d3c58b54cac82720e1fdc39195c : corps dans la Flame commune, navigation
pretes et couts 20/10 passes. Arret volontaire du pilote qui s'etait immobilise hors
cone de vision a portee melee. Le refus d'acquisition etait correct, sans perception
initiale 360. Le pilote doit marcher dans le cone frontal puis approcher la cible
connue. Recette relancee (MoonriseBodyLitFrontalEncounter.log), resultat en attente.

## Derniere validation du montage courant

Run 41d77ac8ab9a4c6caa0ec61900bf6161 : PASSE, 60 controles, zero erreur,
restauration confirmee, processus termine. Il inclut la policy corps/non-trigger,
le nouveau placement, le parcours marche chambre/brasero (20)/porte/seuil/Flame
commune (10), l'acquisition frontale reelle, combat/contre/KO complet/mort, marche
vers l'echelle puis A, sauvegarde/menu/reprise de tous les etats verifies. Seule
l'approche du portail Maison reste une fixture declaree, aucune dans Moonrise.
Aucun warning de recuperation PlayerAction ; cinq warnings de materiaux tactiques
Rooms/Nef et d'arbre importe. Huit tests policy/lifecycle passent egalement.

La capture GameView avec la Flame correcte montre une tache chaude mais encore
une forte presence du decor au premier plan. Les groupes de pilier sont couverts
par le masque : une comparaison de cadrage/ouverture est necessaire avant tout
reglage de rendu. Pas de validation artistique globale ni de duree 15-20 minutes.

## Lisibilite et nettoyage camera

La comparaison 8a828f7bdeab45418ca3503d2a67587c utilise un profil temporaire :
original .16, ouverture .26, puis .26 avec near clip adaptatif. Le dernier retire
le decor proche du premier plan et degage le terrain. Actors/camera continuent
a bouger ; ce n'est pas une comparaison a pose strictement identique.

Le profil partage GameplayCombatCamera retient .26 et adaptiveNearClip=true.
La camera protege desormais ICombatTarget.Root (masque, near clip, choix de vue),
le chemin LockChanged/EnemyController redondant est retire. Deux helpers de
teleportation inutilises et les anciens MaisonMaskActive/renderedAimSharpness
sans references sont supprimes. Aucun systeme historique encore utilise n'est
supprime. 65 tests camera/contrats passent apres nettoyage.

Smoke 8959b273591c4618ad2a3df85cb0f1ea : 38 controles, zero erreur, restaure ;
parcours initial, contre et protection explicite du corps Brains verifies. Arret
focalise apres captures : pas de preuve de mort/continuite avec ce dernier code.
Le run A/B precedent 8b891072f1ee4ed8847809582f767939 echouait ensuite : corps
sorti de l'influence, NPC dormant a 201 PV. Regle demandee a l'utilisateur :
conserver l'activite pendant la rencontre tant que la Flame est allumee, ou
borner la poursuite a la zone eclairee. Pas de changement de cette regle sans reponse.

## Approche d'une cible proche masquee

Correction Brains production et fluid : une cible a portee melee mais cachee par
un obstacle ne doit pas satisfaire le freinage de combat. La demande de navigation
utilise .05 tant que la ligne de vue manque, puis reprend la distance auteur des
que la cible redevient visible. Les protections LOS d'attaque/impact restent intactes.
27 tests de politique passent (MoonriseCoverApproachTestsVerified.xml), incluant
un vrai collider de mur entre source et cible. Ceci couvre le choix de distance,
pas encore un contournement NavMesh observe en Play Mode. Prochaine verification
independante de la regle Flame : obstacle proche apres acquisition, cible conservee,
contournement sans teleportation et aucun degat au travers, puis reprise normale.

Recette native ciblee -moonriseCoverProbe ajoutee : obstacle physique temporaire
avec carving, cree apres acquisition naturelle et impacts du vrai Juggernaut.
Le pilote obtient une separation avant creation, arrete ensuite ses inputs et
observe la conservation de cible, un chemin a plusieurs corners, la reprise de
ligne de vue et les evenements de sante (pas de degats pendant l'obstruction).
Obstacle invisible et non sauvegarde, retire au succes ou a l'echec. Ce fixture
n'est pas un mur auteur ni une preuve de parcours continu. Execution en cours
(MoonriseCloseCoverNative.log), resultat non affirme.

Preuve native du rapprochement : cdbbe749f94840e88093efcaecedd01e PASSE,
30 controles, zero erreur, restauration confirmee. Le Juggernaut production
reel, apres acquisition/impacts, conserve la cible masquee, suit un chemin NavMesh
a plusieurs corners, se deplace de 0,957 m et retrouve la ligne de vue en 3,87 s.
Aucun evenement de degat pendant l'obstruction. Obstacle et abonnement retires,
processus termine. Le mur est un fixture temporaire invisible avec carving,
pas un asset auteur. Ce resultat ne couvre ni le passage derriere l'ennemi,
ni contre/mort/reprise apres cet essai, ni le chronometrage continu de 15-20 min.

## Releve auteur natif et trace propose

`SurveyMoonriseRouteBatch` a ouvert les 26 scenes du manifest District1, sans
sauvegarde des scenes/assets, et releve 117 points. Rapport avant reglage des
leviers et deplacement de la sortie :
`Library/MoonriseRoute/authored-route-survey.json` (2026-10-29T16:14:56Z),
`Logs/MoonriseAuthoredRouteSurveyComplete.log`. Le setup de scenes precedent est
restaure a la fin du releve. Le NavMesh est celui des portes fermees ; un path
partiel n'est ni un bug certifie ni une preuve de trajet jouable. Les samples
a 2,5 m ne prouvent pas l'eligibilite d'interaction ni une approche physique.

| Point auteur | Position monde | Consequence pour le parcours |
| --- | --- | --- |
| Collier de Jon, avant correction | (-7.5645, -93.0739, 55.363) | Rayon bloque par Table ; deplace ensuite sur sol libre (-6.74,-93.83584,54.54), voir recette prologue. |
| Petite tombe de Jon | (-9, -93.865, 54.8) | Meme piece, lecture avant sortie possible. |
| Ghost_Luc | (-3.5, -93.865, 51.5) | Meme piece ; conclusion conditionnee par collier + tombe. |
| Levier 1 | (-30.1681, -93.8785, 38.4205) | Route NavMesh auteur complete depuis la rencontre : 24.08 m. |
| Levier 2 | (-48.8831, -93.8785, 22.5745) | Route auteur complete : 46.48 m ; separation directe des leviers 24.52 m. |
| Pivot du passage | (-42.0146, -93.92, 30.7065) | Les deux leviers sont lies au meme TwoLeverPuzzle et au meme pivot. |
| Acces libre aux Cours, avant correction | (-55, -94.227, 33) | Destination District_2_Courts ; cloison ouest entre couloir et portail. Replace a (-47,-94.227,36), voir recette ci-dessus. |

Le releve initial indiquait Timed/activeDuration=1, playOnce du puzzle=true.
Le parcours solo natif a depuis confirme l'expiration prematuree, puis la
reussite avec deux overrides locaux a 20 s (mode Timed conserve). Le timer reste
maintenu par un personnage proche. Lever reste un gestionnaire direct de l'input A, pas encore
un ICharacterDetectedInteractable : migration a evaluer si ce puzzle est retenu.

Point de vigilance echelle : les chemins auteur depuis le sample du haut de
l'echelle1 sont presque tous partiels apres moins d'un metre. Cela peut venir
du palier, d'un seuil ou du bake. Il faut observer un vrai depart du palier et
un retour par A, pas seulement constater que le joueur arrive en haut. Le run60
ne prouvait que la montee ; e50c5c... confirme depuis le depart marche et la
descente apres reprise, avec le correctif d'ancre decrit plus haut.

Le fil Luc tient dans la chambre : il ne remplit pas a lui seul 15-20 minutes.
Les budgets ci-dessus sont des cibles de conception, pas des mesures. Ne pas
ralentir les inputs, allonger artificiellement les dialogues ou ajouter des attentes
pour atteindre cette duree. La sortie via les leviers, le balcon et Stillhaven
doivent etre jouables avant une session continue chronometree.
