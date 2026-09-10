# Cycle Nina

Ce contenu utilise le runtime generique [Cycles](../Cycles/README.md) :
CycleController, CycleDefinition et CycleInteraction.

La scène District_1_Enigme_Ghost_Nina est enregistrée dans le manifest du district.
Ses objets sont des emplacements à placer, pas des ressources artistiques temporaires.

## Configuration

- Utiliser `Lit/Narrative/Configure Mad Scientist Combat` après toute modification
  du prefab ou du marker. L'outil configure le prefab et la copie baked (physique,
  NavMesh, cerveau, attaque Slash), conserve la position auteur du marker,
  puis répare le collider racine du parchemin et l'outline de Flame_Base_5.
- Le Scientifique utilise GhostController avant l'interaction (apparition de
  proximite, contour et interaction communs aux fantomes). Lors de la rencontre,
  le corps reste visible et le comportement fantome est desactive : `Interact` joue sa réplique
  pour le groupe, puis active son combat serveur. La réplique et sa durée sont
  éditables dans `CharacterData > Détection et engagement > Enemy Encounter Options`.
  `EnemyController` porte cette interaction. Ses derniers mots, leur durée et la
  voix se règlent dans `CharacterData > À la mort de l'ennemi`.
- Ajouter le modèle animal sous Ghost_Nina et assigner son Animator au contrôleur du cycle.
  Fournir les états Idle/Dead et leurs vrais clips. Le contrôleur ne crée aucune animation.
- Placer le prefab de sang sous `Nina's blood_A_ASSIGNER`, qui sert de root d'activation.
- Ajouter le modèle de Scar sous Ghost_Scar. Garder CycleInteraction avec dialogueId = scar.
- Assigner le WorldPrefab du parchemin Item_Edward puis baker son SceneMarker.
- Assigner la Timeline au PlayableDirector et son TimelineBindingProfile au cycle.
  Les pistes caméra doivent utiliser les participants Timeline/LitCameraDirector du projet.
  La Timeline doit avoir une durée finie et ne pas boucler.
- Assigner un SkillSO Cicatrice terminé dans Resources/Narrative/NinaCycle.
  Ses effets et animations ne sont pas inventés par le cycle. Ne pas renommer cycleId.

## Progression

Victoire sur le scientifique : son animation Death joue avec la réplique
« Qu'est ce que... j'ai fait... » et l'AudioClipSO `Data/deathVoiceLine`
(clip « I'm sorry, forgive me », sans boucle). Le dialogue dure au moins 4 s ou
la durée de la voix, puis se ferme avant l'affichage du panneau de victoire.
La suite cinématique attend la fermeture de ce panneau. Une interruption de scène
annule la présentation et arrête la voix. La présentation se joue sur chaque UI
locale, sans modifier l'autorité sur la résolution du combat.

Mort confirmée → Existence des chimères immédiatement, via le service de connaissances
du groupe. La cinématique reste une présentation indépendante : son absence ou son
interruption ne bloque plus ce savoir. Une sauvegarde où le scientifique est déjà mort
accorde aussi la connaissance manquante au chargement du cycle.
Lire la lettre → Dilemme Édouard. Toutes les connaissances du cycle (Dilemme et
Existence des chimères) sont l'unique condition de Nina Dead. Sinon, Nina reste Idle.
Parler à Nina Dead active ensemble le sang et Scar dès l'ouverture du dialogue.
Scar conserve la révélation standard : invisible au loin, apparition progressive
à proximité. Aucun jalon de cinématique ou de mort du scientifique n'est exigé en plus.
Le contrôle de portée réutilise le collider et l'origine de l'interaction standard.
Le dialogue se ferme automatiquement après 4 s plus fondus. Fermer celui de Nina
ne retire pas le sang ni Scar. La fermeture naturelle du dialogue de Scar accorde
Cicatrice au groupe, apres le fondu; une annulation ne l'accorde pas.
Le premier apprentissage affiche
SkillUnlockPanel (titre et description, 4.5 s hors pause); reparler ou recharger
une recompense sauvegardee ne rejoue pas la notification. Cicatrice ne figure plus
dans les competences initiales ni dans l'equipement initial de Lucian.

L'état est stocké dans WorldRulesStateManager sous `narrative.district1.nina`, bits
1 (mort), 2 (cinématique), 4 (visite Nina), 8 (récompense), 16 (dialogue Dead commencé).
Les bits 4 et 16 sont inscrits ensemble au début du dialogue Dead. Chacun reste
compatible avec les anciennes sauvegardes pour activer sang et Scar.
Le snapshot de monde le
conserve et NGO réplique les changements actifs. Les savoirs conservent leur service
réseau existant. SkillsManager compose les skills auteur avec les récompenses sauvées,
sans écrire CharacterData. Aucun équipement automatique.

La cinématique capture les joueurs connectés au départ. Un nouvel arrivant reste sur
le parcours Maison normal et reçoit le snapshot, pas une lecture rétroactive.
Une interruption conserve la mort et retente la cinématique au rechargement de scène.
Un asset manquant bloque sa transition avec diagnostic, sans accorder de récompense.

## Vérifications Play Mode restantes

Solo et 2–4 joueurs : lettre avant/après combat, fermeture anticipée, dialogues simultanés,
death event répété, interruption Timeline, sauvegarde et rechargement de chaque étape,
arrivée tardive à Maison, scène déchargée pendant le dialogue/cinématique, skill connu
après changement de personnage et sortie du district. Tester le rendu après assignation.
