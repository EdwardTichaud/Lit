# Iluvilirae — prototype Brains AI

Prefab : `Iluvilirae.prefab`. Scene de perception autonome : `Iluvilirae_Perception_Test.unity`.
`Iluvilirae_Test.unity` est maintenant le laboratoire de combat Lucian/Juggernaut v2.

## Combat jouable

Ouvrir `Iluvilirae_Test` seule, sans Bootstrap, puis Play. Le laboratoire cree
le vrai Lucian et Juggernaut v2. Brains AI conserve les decisions ennemies,
sans EnemyController. Les services de combat, la camera tactique UCC et les
inputs de Lit sont partages : aucune seconde carte de commandes.

- Stick gauche : deplacement ; X : BasicSkill/combo ; B : esquive.
- Y maintenu : garde ; Y dans la fenetre de reaction ennemie : CounterSkill.
- A : saut ; RB maintenu : palette ; stick droit : selection ; A : validation.
- L3 : LightSkill, si clarte et distance suffisantes ; croix bas : verrouillage.
- Start : pause ; B/Start : reprendre ; A dans la pause ou R : recommencer.

Les degats, la clarte, les Animation Events, l'invulnerabilite de roulade,
les competences et leurs cinematiques utilisent les regles de production.
La LightSkill Devastation conserve notamment sa distance de depart de 6 a 10 m.
Les huit profils d'esquive viennent de GameplaySessionRoot. Le CounterSkill
TemporalRiposte garde ses clips/regles dans une copie et un rig propres au
laboratoire, car la version historique utilisait un Director de scene.
TimeManager est present sur la racine pour pause, ralentissements et hit-stop.
La palette contient les competences de Lucian disponibles dans ce laboratoire.
Les PV Lucian, degats et geometrie de contact ennemis restent reglables dans
l'Inspector. Aucun multiplicateur artificiel de BasicSkill n'est applique.

`ICombatTarget` raccorde les services partages a Brains AI, avec un adaptateur
pour les EnemyController existants. Les contacts ennemis proviennent de deux
evenements propres aux clips v2 : ouverture de reaction, puis impact. Une
attaque interrompue par Hurt/Death/cinematique ne produit pas de contact tardif.
Les animations directionnelles Walk/Run utilisent la vitesse NavMesh reelle ;
Lucian alimente les parametres de locomotion UCC pendant le verrouillage.

Victoire et defaite terminent l'affrontement ; le redemarrage restitue les PV,
la cible, les commandes et les acteurs. Les entrees de simulation UCC sont
retirees avant destruction. Sauvegarde, recompenses et autorite multijoueur
restent hors de ce laboratoire local.

Menu d'auteur : `Lit/Brains AI/Upgrade Lucian Combat Laboratory` pour reconstituer
les references de scene, la camera, la palette et les profils depuis les assets
existants. Aucun montage automatique a chaque import.
`LucianBrainsCombatTests` verifie les references et la geometrie des contacts.
`LucianBrainsCombatSmoke.Run` teste le parcours avec les vrais composants
Lit/UCC et un gamepad simule dans un Editor batch distinct.

## Utilisation

- Ouvrir la scene de perception et lancer Play. Les fleches deplacent la cible verte,
  H inflige 10 PV de degats au prototype, K teste la mort et R recree l'ennemi.
- Dans une scene de Lit avec un NavMesh, placer le prefab. Laisser
  `Detection Target Override` vide pour detecter le personnage controle via
  `LocalPlayerContext`. La fiche `Iluvilirae.asset` configure portee, angle et
  obstacles avec la meme evaluation de vision que Lit.
- L'Inspector affiche les PV, la raison du dernier resultat de detection et
  l'etat de navigation. Les menus contextuels Test/Hurt et Test/Death sont
  disponibles sur IluviliraeBrain pendant Play.

## Responsabilites

`IluviliraeBrain` reutilise `LitBrainsEnemy`, derive du `Brain` Engager du pack
et partage avec Juggernaut v2. Le pack decide de la poursuite,
de l'investigation et des attaques. La liaison Lit ne fournit que la cible visible
et attend la disponibilite du NavMesh de zone. Le NavMeshAgent est l'unique moteur
de mouvement, sans root motion. Les animations d'action suspendent deplacement
et orientation ; la mort coupe perception et colliders puis desactive la racine
apres quatre secondes.

Dans les scenes de perception, il n'y a aucun EnemyController, CharacterInfo, ancien profil de patterns ou
enregistrement dans RealTimeCombatManager. Les attaques sont visuelles ; elles
ne retirent pas de PV au joueur. Les PV du prototype et les commandes H/K servent
uniquement a verifier Hurt/Death. Ce prototype local ne constitue pas encore une
integration multijoueur, de combat, de progression ou de sauvegarde.

## Assets propres

Le modele provisoire est l'humanoide Ultrabot livre avec Brains AI, avec un
materiau HDRP violet propre au prototype. Le controller `Iluvilirae.controller`
contient Locomotion (Idle/Walk/Run), Attack, Hurt et Death. Les clips sont des
copies independantes : locomotion, attaque et mort du pack ; blessure humanoide
du Juggernaut retargetee sur l'avatar Ultrabot. Les evenements et courbes de
transforms propres au modele source sont retires. Les actions ne bouclent pas.

## Adaptation du pack

`BrainsAI/Scripts/Brain Core/FOV.cs` expose `useExternalDetection` et
`SetExternalTarget`. Dans ce mode seulement, l'evaluation physique du pack et
son raccourci de demonstration W sont ignores. Les autres prefabs du pack
gardent leur comportement initial. Une mise a jour du pack peut ecraser cette
petite adaptation : verifier ces deux membres apres import.

## Validation

`Lit/Brains AI/Validate Iluvilirae` controle les bindings, l'absence de controleur
historique et les animations propres. `IluviliraeTests` couvre changement/perte
de cible, destruction de cible et configuration du prefab. La validation batch
`IluviliraePlayModeValidation.Run` parcourt poursuite, blessure, perte de cible,
attaque, mort, immobilite et desactivation dans un editeur de test distinct.

Ne pas regenerer les assets pour modifier les valeurs : les profils et clips
sont directement editables. L'assistant Create valide un prefab deja existant
au lieu d'ecraser les reglages auteurs.

Validation du 2026-10-07 : compilation C# runtime/editeur avec les references
Unity 6000.6.4f1 du projet reussie. Trois tests EditMode et le parcours Play Mode
batch reussis dans un projet d'auteur isole. Ce projet utilise la vraie vision
et les vrais scripts Brains AI/Iluvilirae, mais des contrats simplifies pour les
services Lit absents. L'integration en District 1, l'attente de son NavMesh et
le rendu HDRP doivent donc encore etre verifies dans Lit.

Validation du laboratoire (2026-10-07) : compilation Unity 6000.6.4f1 et
parcours Play Mode avec le vrai prefab Lucian, UCC et les scripts Lit reussis
sur une copie de projet isolee. Mouvement par gamepad simule, impact anime,
sante, garde, esquive, attaque automatique ennemie, mort et reprise apres les
deux issues testes. Les entrees de simulation UCC sont retirees par identite
avant de detruire Lucian : le parcours final ne leve aucune exception de
reference. Le rendu et le ressenti restent a verifier dans l'editeur avec GPU.

Les deux tests EditMode du laboratoire passent (references et geometrie de contact).
