# Squad et personnages

## Contrat solo / combat migre

En solo Lucian est le seul membre physique de l'equipe ; Vesta, Soren et Aurell
restent des identites de progression dont les kits debloques sont executes par
Lucian. Aucun acteur de support cache. Le chargement de snapshots applique la
meme politique que le spawn initial, sans retirer les deblocages des sauvegardes.
Le filtre de snapshots reconnait seulement Vesta/Soren/Aurell via le catalogue ;
les PNJ non ennemis et identites inconnues gardent leur parcours normal.
En multi, chaque joueur conserve son personnage attribue et son propre kit.
La migration preserve GUID, characterId, statistiques et inventaires existants.

## Configuration des modules joueur

CharacterData.playerSettings contient les blocs de locomotion, saut, esquive/dash, equipement, interactions, visage, pas et trajectoires. Chaque module copie uniquement son bloc via PlayerModuleConfiguration; les references aux ressources restent partagees. Les quatre prefabs ont leur fiche assignee avant activation. CharacterHealth/LitUccDamageBridge gardent l autorite actuelle des PV; les abonnes ne changent pas de modele de sauvegarde.

## Rôle

Maintenir le roster, les instances de personnages, le personnage contrôlé, les
groupes, le follow et les données runtime de chaque membre.

## Classes principales

- `SquadManager` : roster, spawn, sélection, groupes, verrouillage global.
- `SquadCharacterController` : façade gameplay du personnage, inventaire, santé,
  mouvement, interactions, assise et crochetage.
- `SquadAIManager` / `SquadFollowerAgent` : formation et déplacement des followers.
- `CharacterData` : données d’auteur immuables, avec un unique `WorldPrefab`.
- `CharacterInfo` : référence de `CharacterData` commune aux personnages et ennemis
  placés en scène; il remplace l'ancien composant `EnemyInfo`.
- `CharacterRuntimeState` : inventaire, flamme, objets combat, boucliers et Munin
  de la partie; possédé par `SquadManager`, indexé par `characterId` stable.
- `LocalPlayerContext` : référence canonique du personnage local.

## Flux principaux

- Solo : `SquadManager` spawn le roster et choisit `currentCharacter`.
- Multijoueur : `NetcodePlayerSpawner` fournit les instances; `SquadManager`
  rafraîchit sa liste à partir du réseau.
- Les changements de contrôle mettent à jour `LocalPlayerContext`.
- Les consommateurs locaux (caméra, environnement, narration) écoutent
  `LocalCharacterChanged`.
- `SquadAIManager` pilote les membres groupés et suspend le follow pendant une
  `StorySequence`.
- Munin reste enfant logique du personnage pour les systèmes qui le résolvent
  via le character root, mais son suivi visuel est piloté en position monde par
  `MuninController`. L'avance de mouvement utilise le delta de position du
  Transform si la vitesse Rigidbody du personnage ne reflète pas le déplacement
  réel UCC.
- `Munin_Orbe` est une variante indépendante de l'orbe de chargement. Son rendu
  est piloté par `MuninOrbVisualController`, qui préserve strictement les
  matériaux, UV et modes de rendu auteurs; il désactive la distorsion permanente,
  plafonne la taille écran des particules à 0,06, masque en jeu les cartes
  `Flare4_Additive`, `Flare_Ultrawide` et `FuzzAdd` dont le shader HDRP reste rectangulaire, et
  joue des états transitoires repos, attention et action à partir des événements
  de charges de `MuninController`. Sa taille visible est multipliée par 2,5 via
  les modules `startSize` de particules; ne pas agrandir la racine du prefab.
  Le composant historique `MuninOrbAlphaGuard` reste désactivé afin de ne jamais
  déduire la transparence d'une couleur noire.
- Le lien esprit/incarnation est porté par `SpiritBondController` sur le
  compagnon. Il masque seulement sa racine visuelle pendant une fusion ou une
  manifestation-arme, sans désactiver sa logique de suivi et d'interaction.
  Lucian utilise `CharacterEffect` avec `Holy`; les LightSkills imposent une
  fusion temporaire et restaurent Munin à leur fin. `PlayerSword` et
  `PlayerBow` signalent leur visibilité au lien, tandis que les futures armes
  peuvent recevoir `SpiritWeaponManifestation`. Le clic bref du stick droit
  appelle l'action `Melt` : il declenche `Melt` hors fusion et `Rupture` pendant
  une fusion. Les AnimationEvents sont recus par `SpiritBondAnimationEvents`
  sur l'incarnation : `TriggerHolyEffect`, `ConfirmMeltFusion` et
  `ConfirmRuptureDefusion`. L'ancien evenement `PlayEffect_CharacterEffect`
  est relaie vers Holy, mais arrete Holy s'il est rencontre pendant une
  `Rupture` legacy; son pendant explicite
  `StopEffect_CharacterEffect` l'arrete. `InstantiateAtSpine`
  instancie le prefab configure
  sur l'os Spine et le fait suivre l'animation. Le maintien du stick droit
  conserve le recentrage de caméra (`C` au clavier). Les deux etats sont joints
  depuis `Any State` par leurs triggers et reviennent automatiquement a l'etat
  `Locomotion` a la fin du clip. `StopEffect_CharacterEffect` utilise l'arret propre du graphe
  VFX, sans desactiver son GameObject. Le `CharacterEffect` Holy est porte par
  `CC_Base_Body` (le `SkinnedMeshRenderer`) afin de partager son repere local
  et rester cale sur le personnage; son instance Holy doit aussi rester enfant
  de `CC_Base_Body` apres un `Load New` dans l'Inspector. Le binder `Transform`
  de Holy doit rester actif : le graphe en depend pour calculer une AABB valide.
  Les offsets `Transform` du prefab Holy sont neutres pour que son origine soit
  celle du body de Lucian, et non celle du modele de demonstration du package.
  La sortie de `Rupture` attend `1.1` temps normalise, afin que son evenement
  `StopEffect_CharacterEffect` de fin de clip ne soit pas coupe par la
  transition de sortie.

## Pièges observés

- Ne jamais modifier directement les assets `CharacterData`. Les compétences
  runtime peuvent encore employer `GetRuntimeCharacter`; tout état d'inventaire
  ou Munin doit passer par `SquadManager.GetCharacterRuntimeState`.
- `SquadManager.SetInputLocked` est compté : chaque verrou doit être libéré.
- Le personnage local n’est pas toujours `SquadManager.currentCharacter` en
  multijoueur; utiliser `LocalPlayerContext`.
- `SquadCharacterController` est réparti sur plusieurs fichiers `partial`.
## Soutiens solo et migration combat (2026-10-16)

Le catalogue CombatSkillKitCatalog.IsCharacter compare reference/GUID/characterId,
jamais le nom affiche. La regeneration solo sur Lucian fonctionne aussi avec un
clone runtime ou un nom localise ; un PNJ appele Lucian ne devient pas un joueur.
La selection d'allie inclut Lucian comme beneficiaire solo autorise. Intercession
conserve sa possibilite d'utilisation sur soi ; en reseau, les kits restent propres
au personnage attribue. Aucun compagnon physique n'est ajoute pour porter un effet.
Le migrateur prepare sur Lucian les modules necessaires aux kits empruntes, et les
modules de garde/protection sur les quatre prefabs, sans debloquer de competence.


## Assise et releve (2026-11-22)

Les etats Sit_Down et Stand_Up des quatre controllers joueurs actifs
(Player_Model, Vesta_Model, Soren_Model et Lucian_ImpactLab utilise par Lucian)
jouent deux fois plus vite ; le sens inverse de Stand_Up de Lucian est conserve.
Sitting_Idle et la vitesse globale de l'Animator restent inchanges.

SquadCharacterController.IsSittingTransitionActive couvre l'entree dans la
pose assise et toute la releve, jusqu'a la fin de la transition. TryToggleSitting
et CanToggleSitting refusent une nouvelle interaction pendant ces phases.
UpdateSittingState consomme les versions de mouvement sans programmer de
releve ; un stick maintenu pendant l'assise doit revenir au neutre avant qu'un
nouveau geste puisse faire lever le personnage. TrySetSitting(false) conserve
son contrat explicite pour les sequences narratives et le nettoyage de scene.

LocalInputRouter ignore les appuis gameplay pendant ces transitions, avant
leur diffusion et sans les stocker. Les dialogues et menus qui detiennent le
focus conservent leur propre navigation. Le verrou de mouvement existant
reste libere par la fin observee de Stand_Up ou son secours habituel.

Validation : compilation Unity et parcours isole PlayerSittingPlayProbe reussis.
Verification des quatre assets, Lucian/UCC avec stick maintenu puis relache,
nouvelle demande de releve, appuis de torche bloques et interactions repetees
ignorees pendant les transitions ; commandes rendues ensuite. Aucun changement
de scene source ni de sauvegarde utilisateur. Les erreurs deja presentes de
volumes, VFX menu et cape persistent. Pas de qualification visuelle ou multi.
