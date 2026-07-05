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

Le combat tour par tour pilote maintenant la camera locale par phase via
`CombatCameraPresentationController`; pendant le combat, Opsive est suspendu
comme driver camera spatial puis restaure a la sortie.
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
surlignage/agrandissement du slot choisi.
Pendant toute la session de combat, le HUD garde l'ActionMap locale `Combat`
active, prend le focus exclusif et ferme l'inventaire s'il etait deja ouvert;
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
configurer ses attaches, SFX/VFX/voix depuis l'item.
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
`Victory` ou `Celebrate`), puis un ecran runtime `VICTOIRE`/`GAME OVER` attend
une validation manuelle du joueur avant de restaurer les positions, camera,
mouvement et resultats monde.

## Contraintes

- Garder `Codex/AGENTS.md` et `Codex/current_work.md` courts.
- Lire uniquement les fiches pertinentes de `Codex/systems/`.
- Ne pas recreer l'ancien pipeline AIStudio sans demande explicite.
- Ne pas stocker de secrets, caches ou environnements virtuels dans `AIAgent`.

## Prochaine utilisation

Pour une nouvelle tache, partir du modele `prompts/codex_task.md`, remplacer
`[TACHE]`, puis fournir le prompt a Codex depuis le contexte `AIAgent`.
