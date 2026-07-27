# Travail en cours

## Objectif actuel

Utiliser `AIAgent` comme espace documentaire leger pour preparer des prompts
Codex efficaces et economes en tokens.

## Etat actuel

`AIStudio_Legacy` a ete supprime. `AIAgent` contient un dossier `Codex/` avec
les regles de travail, l'etat courant et les fiches systeme utiles. Les anciennes
fiches systeme Markdown ont ete migrees depuis `AIStudio_Legacy/docs/systems/`.

Le nouveau flux ne depend plus d'une application Python, d'un environnement
virtuel ou d'un appel LLM local. Les prompts sont prepares manuellement depuis
`AIAgent/prompts/codex_task.md`.

Le prototype de chute libre est archive dans `Assets/FallingPhase_Legacy/` avec
sa scene, son Animator, son grappin, ses scripts et son manifest. Il ne fait plus
partie des scenes de build; l'ActionMap partagee `Falling` est conservee afin que
l'archive reste testable manuellement.

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
Le verrouillage est entierement manuel : `LeftShoulder` verrouille l'ennemi le
plus proche a portee, puis le deverrouille au prochain appui. Le lock se ferme
automatiquement au-dela de 7 metres. Un orbe lumineux pulse et un signal sonore sont joues sur
`EnemyLockPoint`; la croix directionnelle gauche bascule entre les ennemis
visibles verrouillables.
Le verrouillage combat affiche aussi un contour rouge HDRP autonome sur les
renderers de l'ennemi. Il utilise la layer `CombatOutline` et la passe
`CombatLockOutlinePass` de `GameplaySessionRoot`, sans partager l'etat ou la
couleur des contours bleus d'interactables.
Pendant ce lock, la camera conserve un evitement des obstacles visuels par
SphereCast, configurable sur `CombatLockOnCameraController`, sans laisser les
drivers UCC reprendre le cadrage de cible.
Le lock garantit aussi un cadrage complet de Lucian : il augmente son FOV jusqu'a
une limite reglable puis recule si necessaire. Son orbite suit la direction
joueur-ennemi avec un lissage distinct et une vitesse angulaire maximale afin
que les demi-tours restent fluides. Les vitesses finales de position, rotation
et FOV sont aussi bornees pour absorber les changements de cadrage pres des
obstacles.
Exploration et combat restent dans le meme espace : aucune transition, vague,
BattleSphere, teleportation ou arene n'est activee.
Le premier scenario jouable est configure : Lucian dispose de `Lueur faible`
et `Lueur intense` dans les slots 1 et 2, tandis que le Juggernaut joue
`Skill_Juggernaut_Assomoir` via `EnemySkills` et ouvre sa fenetre d'esquive par
Animation Events.
Le prototype temps reel ne comporte pas encore de contre : les skills sont des
attaques et les fenetres ennemies acceptent actuellement esquive ou saut. Les
anciens modificateurs de contre sont conserves dans les donnees de savoir mais
ne sont pas appliques par le flux temps reel.
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
porte actuellement le seul skill melee `Skill_Juggernaut_Assomoir`.
Tant qu'au moins un ennemi est dans ce mode, le manager applique l'override de
musique combat; il le relache apres le desengagement, la mort ou la fin de
l'affrontement.
Chaque degat du prototype temps reel produit aussi un nombre world-space local,
attache au combattant touche : cyan pour la lumiere recue par l'ennemi, rouge
pale pour Lucian. Il est billboard vers la camera, pulse, monte legerement et
s'efface en temps non scale sans prefab ni Canvas de scene.
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
sans ramener Lucian a sa pose initiale. Les attaques root du prototype appliquent
la meme synchronisation avant leur retour vers la locomotion. Les states taguees
`RealTimeCombatRootMotion` sont reconnues par UCC comme du root motion actif :
leur deplacement et leur rotation ne sont donc pas supprimes quand l'input est nul.
La validation oriente d'abord Lucian horizontalement vers `EnemyLockPoint`, puis joue l'etat Animator explicitement configure sur le `SkillSO` selectionne (avec fallback sur le nom du `AnimationClip`);
ses Animation Events restent responsables des VFX et degats. Les slots sans
SkillSO sont masques et exclus de la navigation de la roue.
`SkillsManager` expose aussi une liste `BasicSkills` de `BasicSkillsSO`.
Pendant un lock, `WestButton` ajoute le prochain basic skill a un buffer de
combo : les clips sont joues dans l'ordre de la liste puis bouclent. Les Basic
Skills reutilisent les memes Animation Events de VFX et de hit que les skills
de la roue. Une pause de plus de `0.85 s` entre deux pressions reprend le combo
au premier basic skill. Une seule attaque peut etre bufferisee derriere celle
en cours; les pressions excedentaires sont ignorees. `ToggleTorch` ignore
`WestButton` tant qu'une cible est verrouillee.
Chaque `SkillSO`, donc aussi chaque `BasicSkillsSO`, expose une distance
horizontale minimale et maximale de hit. `HitEnemy` ne blesse la cible qu'a la
frame de son Animation Event si Lucian est dans cette plage autour de
`EnemyLockPoint`. Les VFX `DirectOnTarget` et `Projectile` sont soumis a la
meme verification; les cues `PlayerHand` restent joues pour la presentation du
caster.
`Skill_3_Entaille` utilise explicitement `Base Layer.Skill_3_Entaille` et porte
le tag `RealTimeCombatRootMotion`, afin que son retour a locomotion ne laisse
pas une intention UCC residuelle.
`Skill_2_Fleche de lumiere` cible explicitement sa state root plutot que le
fallback par nom de clip.
Les clips de skill peuvent aussi appeler `Dash`, qui propulse Lucian sur la
droite vers `EnemyLockPoint` avec un depassement reglable, puis `StopDash`, qui
freine cette impulsion sur une courte duree plutot que de l'annuler instantanement.
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

## Prochaine utilisation

Pour une nouvelle tache, partir du modele `prompts/codex_task.md`, remplacer
`[TACHE]`, puis fournir le prompt a Codex depuis le contexte `AIAgent`.
