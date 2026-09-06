# Cycle Nina

La scène District_1_Enigme_Ghost_Nina est enregistrée dans le manifest du district.
Ses objets sont des emplacements à placer, pas des ressources artistiques temporaires.

## Configuration

- Sur le marker ScientifiqueFou, assigner le WorldPrefab du CharacterData et baker.
  Il doit utiliser le combat ennemi existant et CombatHealth. Ses attaques restent à auteuriser.
- Ajouter le modèle animal sous Ghost_Nina et assigner son Animator au contrôleur du cycle.
  Fournir les états Idle/Dead et leurs vrais clips. Le contrôleur ne crée aucune animation.
- Placer le prefab de sang sous `Nina's blood_A_ASSIGNER`, qui sert de root d'activation.
- Ajouter le modèle de Scar sous Ghost_Scar. Garder l'adapter NinaGhostInteraction.
- Assigner le WorldPrefab du parchemin Item_Edward puis baker son SceneMarker.
- Assigner la Timeline au PlayableDirector et son TimelineBindingProfile au cycle.
  Les pistes caméra doivent utiliser les participants Timeline/LitCameraDirector du projet.
  La Timeline doit avoir une durée finie et ne pas boucler.
- Assigner un SkillSO Cicatrice terminé dans Resources/Narrative/NinaCycle.
  Ses effets et animations ne sont pas inventés par le cycle. Ne pas renommer cycleId.

## Progression

Mort confirmée → délai réel 3 s → cinématique de groupe terminée → Existence des chimères.
Lire la lettre → Dilemme Édouard → Nina Dead et sang, même avant le combat.
Les deux savoirs et la cinématique sont nécessaires pour valider la visite Dead.
Le dialogue se ferme automatiquement après 4 s plus fondus. Une fermeture anticipée,
un remplacement de dialogue ou une désactivation annule la validation.
Visite Dead validée → Scar visible. Dialogue Scar terminé → Cicatrice connue du groupe.

L'état est stocké dans WorldRulesStateManager sous `narrative.district1.nina`, bits
1 (mort), 2 (cinématique), 4 (visite Nina), 8 (récompense). Le snapshot de monde le
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
