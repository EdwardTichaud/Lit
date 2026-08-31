# Combat

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
`UseRootMotionRotation=false` dans ces deux états. UCC applique donc seul
l'approche, le recul, l'orbite, les collisions et l'inertie. Les root motions
des actions engagées (roulade, saut, skills, impulsions et cinématiques) restent
pilotés par leurs profils explicites.
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

Les previews cinematographiques suivent le contrat de gameplay : un
`ActorRoot` porte l'Animator de gameplay et le skeleton n'est qu'un enfant
visuel. Pour l'ennemi, `Enemy_Preview` dans `AnimationLab` reproduit ainsi
`Juggernaut_Combat` et utilise `Juggernaut.controller`; `MidPoly` ne porte pas
d'Animator. Les bakers de `LightSkillTimelineAuthoringRig` et
`CombatSkillTimelineAuthoringRig` resolvent/bindent les `ActorRoot` previews
avant de copier une Timeline runtime, et les releves de cadrage sont pris sur
ces roots, jamais sur un mesh enfant. Une validation bloque un bake lorsque le
preview Animator n'est pas sur son root. Le menu
`Lit/Combat/Update AnimationLab Root Animators` remet la scene et le prefab
AnimationLab en conformite avec `Juggernaut_Combat` puis rebinde les pistes
acteurs des Timelines d'auteur.

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
