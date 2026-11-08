# Premier labyrinthe — audit initial et suivi de réalisation

Date : 2026-11-01. Référence : [plan de production](Premier_Labyrinthe_Plan.md).
Statut : réalisation commencée, prototype complet non livré. Parcours technique
solo vérifié ; acceptation humaine et multijoueur non qualifiés par cet audit.

## Choix confirmé par le porteur du projet

Créer une scène de test indépendante, sans modifier la campagne, avec un
gardien provisoire fondé sur le Juggernaut. L'identité narrative définitive,
le rattachement à une strate et l'accès de campagne restent hors de cette
première scène. Ne pas modifier le Juggernaut de production pour régler ce test.

## Audit de réutilisation — sources inspectées

| Besoin | Source actuelle | Conclusion et travail restant |
|---|---|---|
| Entrée d'expédition | `Assets/Scripts/LabyrinthStartTrigger.cs` | Confirmation et téléportation de squad existantes, avec demande réseau. Ce n'est pas un service de tentative : pas de chrono, pièce collective, préparatifs ou reformation. Ne pas détourner le départ de campagne pour ce test. |
| Racine de session | `Assets/Scripts/SceneFlow/GameplaySessionRoot.cs` | Racine persistante entre zones ; ne fournit pas le cycle d'expédition. En scène autonome sans Bootstrap, le GameFlow ne doit pas être supposé disponible. |
| Savoirs partagés | `Assets/Scripts/KnowledgeReveal.cs`, `Assets/Persistence/Netcode/KnowledgeSynchronizationService.cs` | Réutiliser les IDs stables, la déduplication et la liste réseau, y compris pour les clients tardifs. Ne pas stocker une seconde progression de connaissances dans la tentative. |
| Annonce du révélateur | `KnowledgeRevealedClientRpc` dans le service ci-dessus ; `KnowledgeManager.ApplyValidatedKnowledge` | Le nom est transporté jusqu'au RPC, mais le corps applique seulement la connaissance. L'événement `KnowledgeUnlocked` ne transporte que le Savoir. Le HUD avec révélateur ne peut pas être déclaré opérationnel à partir de ce code : raccord et tests requis. |
| Interactions | `Assets/Scripts/Lever.Interaction.cs`, `Assets/UI/World/WorldInteractionPrompt.cs` | Sélection/outline partagés et prompt réutilisables. Les nouvelles stations doivent passer par la validation de portée/acteur et l'autorité, sans ajouter une seconde écoute brute du bouton Interagir. |
| Boss | `Assets/CombatRealTime/Bosses/BossEncounterBehaviour.cs`, `BossDefinitionSO.cs` | Shell Dormant/Engaged/Resolved et réplication existants. Il faut une implémentation concrète et un engagement explicitement contrôlé ; la proximité seule ne doit pas lancer ce boss. |
| Acteur Brains | `Assets/CombatRealTime/Migration/Encounters/LitBrainsEnemy.EncounterActor.cs` | Adaptateur générique existant, sous réserve de binding compatible du boss. Les intros/last words héritées ne doivent pas être contournées. Utiliser un acteur de test distinct. |
| Flame | `Assets/CombatRealTime/Migration/Encounters/ProductionBrainsFlamePolicy.cs` | La disponibilité, les dégâts et le mouvement dépendent de l'influence réelle des Flames ; un simple éclairage HDRP ne suffit pas. La croissance doit utiliser une horloge de tentative séparée du domaine de temps dormant du boss. |
| Montage solo de laboratoire | `Assets/Characters/3_Enemy/Iluvilirae/LucianBrainsCombatArena.cs` | Exemple de binding Lucian/UCC/skills existant. Il impose cependant une rencontre unique et son input appartient au laboratoire : ne pas le recopier comme s'il gérait déjà l'exploration et plusieurs rencontres. |
| Récompenses narratives | `Assets/Narrative/Cycles/CycleSteps.cs` | `CycleRewardKind` couvre Skill/Knowledge/Item/WorldVariable/Activation. Il n'y a pas de type XP dans ce contrat. Une récompense de cycle idempotente ne suffit pas à définir des gains répétés par tentative. |
| Persistance | `Assets/Persistence/Save/World/IPersistentStateProvider.cs` et pipeline monde/personnages | Le point d'extension existe. Un état de tentative complet, versionné et restauré doit être fourni avant toute promesse de reprise en cours de labyrinthe. |
| Perte de connexion | `Assets/Persistence/Netcode/PrivateSession/PrivateSessionService.cs` | La perte de l'hôte/transport termine la connexion avec un état d'erreur ; ce code ne prouve pas une migration d'hôte. Ne pas annoncer une continuité automatique. |

Ces constats sont une lecture de code, pas une recette host/client exécutée.
La présence d'une classe ou d'un prefab ne certifie pas son raccord à la future scène.

## Contrat de prototype proposé

À matérialiser dans les adaptateurs et à tester avant de le déclarer livré :

- Cycle : Ready → Exploring → Fighting → Won ; sorties Lost/Abandoned possibles
  depuis une tentative active. Reformation uniquement après un résultat terminal.
- Effectif capturé au lancement, de un à quatre ; une déconnexion ne change
  pas le barème, le chrono ni la propriété collective de la pièce.
- Deux horloges : temps total jusqu'à victoire et temps d'obscurité avant
  engagement. Aucun temps accumulé hors session ou pendant le chargement.
- Les demandes réseau ne transportent pas de preuve client de victoire, de
  connaissance ou d'ennemi tué : le serveur vérifie ces faits dans le monde.
- La pièce est un fait de tentative, non un objet abandonnable définitivement
  par un porteur déconnecté. Les actions restent locales et à portée contrôlée.
- L'engagement nécessite seuil ouvert, décision d'équipe et influence réelle
  de Flame. Les préparatifs sont figés dès l'engagement.
- La récompense provisoire est un résultat de test isolé, pas une attribution
  inventée d'objets ou d'XP de campagne. La livraison finale exige un vrai pipeline
  de récompense convenu, pas seulement un compteur de tests.
- Pour la première version : proposer l'abandon de tentative au déchargement
  et le retour à l'entrée, plutôt que promettre une sauvegarde partielle.
  Les connaissances déjà acquises restent persistantes. Le porteur du projet
  a confirmé cette politique de reprise et l'entrée entre les tentatives.
- Perte d'hôte : fin de tentative, pas de migration implicite. Distinguer cette
  limite de la reconnexion d'un client à un hôte encore actif.

## Socle de tentative

`Assets/Gameplay/FractureLabyrinth/FractureAttempt.cs` contient les transitions
et invariants de tentative : chronos, pièce collective, réparation, seuil,
raccourcis, préparatifs indépendants, puissance plafonnée à l'engagement,
victoire idempotente et reformation.

Ce modèle ne touche aucun Savoir, roster, slot de sauvegarde ou asset de campagne.
La graybox solo l'utilise pour ses faits d'exploration. Il n'est ni une autorité
NGO autonome ni un système de combat : seuls les futurs
adaptateurs serveur peuvent lui transmettre des faits déjà validés. Son
`RecordReward` enregistre uniquement un résultat de test et ne donne ni loot ni XP.
Les valeurs de croissance utilisées dans ses tests ne constituent pas un réglage
de difficulté validé pour le boss.

## Vérification réalisée

- Douze cas NUnit exécutés hors Editor contre la source de production : cycles
  avec 1/2/3/4 participants, chrono incluant le combat, obscurité arrêtée à
  l'engagement, données temporelles invalides, gates de pièce/Savoir/lumière,
  préparatifs indépendants et idempotents, refus de relance active, défaite,
  abandon, faux décès et récompense unique.
- Commande et exécutable de contrôle dans `Library/FractureLabyrinthValidation`.
  Ces tests ne simulent ni les joueurs, ni le réseau, ni les collisions.
- Compilation C# runtime et Editor réussie avec les références Unity, incluant
  le nouveau modèle et ses tests : `Library/FractureLabyrinthValidation/runtime.log`
  et `editor.log`. Un contrôle C# externe
  ne vaut pas import natif, compilation de build ni parcours Play.

## Non réalisé / preuves encore nécessaires

- Acceptation humaine et rendu final du montage solo décrit ci-dessous.
- Navigation et autorité des futurs ennemis dans la géométrie du prototype.
- Rencontres et gardien réels, deux préparatifs réellement appliqués au combat.
- Flame d'arène, paliers visuels et preuve de victoire au plafond sans préparatif.
- Autorité réseau, validation des commandes et synchronisation 2/3/4 joueurs.
- HUD réseau des Savoirs avec révélateur, chrono, confirmation d'équipe et bilan.
- Récompenses, relance physique des rencontres et contrat de sauvegarde validés.
- Deux parcours successifs, Domain Reload actif/inactif et recette humaine
  clavier/manette ; mesures de stratégies et équilibre selon l'effectif.

Contrat de cycle de vie confirmé : quitter ou
décharger abandonne la tentative (pas de reprise au milieu) ; arrivée de nouveaux
participants entre les tentatives seulement ; perte de l'hôte termine la session,
sans migration. La persistance des Savoirs n'est pas supprimée par cet abandon.
Leur validation ne constitue pas une preuve d'implémentation réseau.

## Avancement de la graybox

Scène créée et importée par Unity :
`Assets/Gameplay/FractureLabyrinth/Prototype/Fracture_01_Graybox.unity`.
Vrai prefab Lucian/UCC, interaction partagée, Savoir à trois symboles, pièce,
commandes et deux raccourcis. Pas de modification des scènes de campagne.
Gardes et effets de boss différés au jalon 2 ; aucune victoire de boss ou
récompense fictive à la sortie de ce parcours d'exploration.

Topologie de l'asset enregistré vérifiée : 184 cellules, 15 postes, branches
initialement accessibles, arène inaccessible avant le seuil. Les raccourcis
réduisent les chemins de 10 à 6 et de 19 à 7 pas de grille. Ce ne sont pas des
mesures de trajets physiques ni de temps humains. Treize tests natifs ont passé ;
les résultats actualisés sont sous `Library/FractureLabyrinthValidation`.

Le parcours UCC a révélé un défaut du montage : un collider solide automatique
de Flame de travail bloquait le rayon d'interaction. Correction limitée à ces
sources, sans assouplissement des règles globales. L'entrée est ensuite bien
sélectionnée et activée par le gamepad simulé. Les waypoints de test doivent
contourner les pupitres physiques, pas marcher à travers eux.

Le parcours technique complet a depuis passé dans Unity : vrai déplacement UCC,
gamepad simulé et interactions du détecteur partagé, révélation aux archives,
deux raccourcis, commandes des préparatifs, pièce, réparation, seuil et sortie.
`WalkTests.xml` : 1/1 passé ; ce n'est pas une mesure de temps d'un joueur humain.
`EditTests.xml` : 13/13 passés après les corrections. `PreviewTests.xml` : 1/1
passé ; capture `Preview-spawn.png` inspectée. L'exposition de la graybox a été
corrigée en enregistrant son composant Exposure comme sous-asset du profil,
sans modifier l'éclairage ou les profils du Château. L'ancienne capture
`Walk-arena.png` est antérieure à cette correction et ne prouve pas le rendu actuel.

Le jalon 1 attend encore le contrôle humain au clavier/manette.
Les jalons 2 à 5 ne sont pas livrés. Voir `AIAgent/Codex/systems/fracture_labyrinth.md` pour
les raccords et les rapports ; ne pas présenter les anciens échecs comme une réussite.
