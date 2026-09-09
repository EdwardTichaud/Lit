# Cycles narratifs

Le runtime commun remplace les scripts specifiques Nina. Nina reste un contenu
configure dans `Resources/Narrative/NinaCycle.asset` et dans sa scene de district.
Le controleur ne connait aucun nom de personnage, texte ou skill de ce cycle.

## Creer un cycle

1. Creer un asset via **Create > Lit > Narrative > Cycle** sous
   `Assets/Resources/Narrative/`. Choisir un `cycleId` unique et durable.
2. Ajouter `CycleController` sur une racine de scene ; son `NetworkObject` est
   ajoute automatiquement. Assigner la definition. En reseau, cette racine doit
   etre chargee/spawnee par le flux Netcode existant de la zone.
3. Ajouter des entrees `dialogues` a la definition, avec un ID unique par acteur
   interactif, un texte et les conditions requises. Un `unavailableLine` facultatif
   permet de parler avant ces conditions, sans effet de progression.
4. Sur chaque fantome, conserver un seul `GhostController` responsable du dialogue,
   de la detection, de la proximite et de l'outline. Ajouter `CycleInteraction`,
   renseigner `cycle` et `dialogueId`, puis referencer ces interactions dans le
   controleur. Le bouton **Relier les interactions enfants** inclut les acteurs
   initialement inactifs. Le composant d'interaction ne duplique pas leur revelation.
5. Configurer `activations` pour les objets dont la presence depend de conditions,
   et `poses` pour choisir entre deux etats Animator. Les objets et Animator sont
   des references de scene ; la definition ne contient pas de references de scene.
6. Facultativement, assigner `encounterMarker` : sa defaite inscrit
   `enemyDefeatedFlags` et revele `knowledgeOnEnemyDefeat`. Sans combat, laisser le
   marker vide et mettre ces flags a 0. Une cinematique est facultative : activer
   `playCinematicAfterDefeat` seulement si elle fait partie du cycle, puis assigner
   le director et son profil. Une ressource absente ne valide pas sa completion.

## Conditions, transitions et recompenses

Les flags sont des bits dans un entier sauvegarde sous `narrative.<cycleId>`.
Utiliser des puissances de deux distinctes (1, 2, 4, 8...) pour les jalons ; leur
somme exprime un ensemble. `allFlags` exige tout l'ensemble, `anyFlags` au moins
un bit. Zero ne demande aucun flag. Toutes les connaissances listees sont requises.

`openedFlags` s'applique a l'ouverture d'un dialogue eligible.
`completedFlags` s'applique uniquement a sa fermeture naturelle, apres le fondu.
Une recompense utilise `rewardSkill` et un `rewardFlag` unique, distinct des flags
de toutes les autres transitions. Son apprentissage intervient seulement a la
fermeture naturelle ; `SkillUnlockPanel` l'annonce une seule fois a la session.
`repeatLine` remplace ensuite le texte d'un dialogue qui a deja donne sa recompense.
Le skill devient connu, sans equipement automatique ni modification de CharacterData.

Un cycle peut avoir plusieurs dialogues et plusieurs recompenses, sans combat ou
cinematique. Les bindings d'animation et d'activation peuvent etre aussi nombreux
que necessaire. Le controleur prend actuellement en charge un encounter et une
cinematique apres cet encounter par cycle.

## Autorite et persistance

En solo, les transitions ecrivent dans WorldRulesStateManager. En reseau, seul le
serveur les valide, apres spawn : acteur lie au cycle, personnage vivant, portee,
prerequis et ouverture prealable du dialogue. La fermeture porte un token et ne
peut pas valider une autre conversation ni arriver avant la duree du dialogue.
Annulation, desactivation et despawn retirent les conversations en attente.

L'etat est replique aux clients, y compris lors d'une arrivee tardive. Les skills
des definitions de `Resources/Narrative` sont relus depuis la sauvegarde sans
rejouer leurs notifications. Ne jamais renommer un cycle publie ou renumeroter ses
bits. Les IDs de dialogue doivent rester stables entre serveur et clients.

Le cycle Nina conserve `narrative.district1.nina`, les bits 1/2/4/8/16 et les GUID
des scripts migres. L'ouverture du dialogue Nina inscrit 20 (4 + 16), et la
condition d'apparition sang/Scar accepte 4 ou 16 pour les anciennes sauvegardes.
Cicatrice conserve le bit 8 et s'apprend apres le dialogue de Scar.

Les ennemis qui doivent retarder la cinematique pendant leur presentation de mort
implementent `ICycleCinematicBlocker`. Le scientifique est un de ces participants,
sans dependance de CycleController envers son script specifique.

## Verification

Les tests `CycleTests` couvrent les conditions, plusieurs cycles independants,
plusieurs dialogues sans encounter, les configurations invalides et les messages
de fermeture annules, precoces ou perimes. `NinaCycleTests` conserve les regressions
du contenu Nina, dont sauvegarde, apprentissage unique et physique de Scar.
Valider aussi la scene et les flux solo/hote/client apres import Unity.
