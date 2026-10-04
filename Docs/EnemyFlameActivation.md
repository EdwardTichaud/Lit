# Activation des ennemis par les Flames

Tous les `EnemyController` (boss inclus) exigent par défaut une influence de
`Flame` commune ou Ancienne, active et effectivement allumée. La torche du
joueur n'est pas une Flame et ne les réveille pas, même si sa zone utilise
la catégorie `Flame`. Les autres effets de la torche restent inchangés.

## État et autorité

- `RequiresFlameInfluence` : réglage sérialisé de l'EnemyController, activé
  par défaut. Ne le désactiver que pour une exception narrative explicitement
  autorisée. Aucune exemption n'est configurée par cette livraison.
- `HasActiveFlameInfluence` : état calculé, répliqué par le serveur.
- `IsFlameDormant` : absence d'influence sur un ennemi vivant non exempté.
- `CombatEnabled` tient compte de la dormance sans écraser son réglage initial
  ni les conditions propres aux Ghosts et aux boss.

Les contacts utilisent les volumes, couches et règles de triggers de
`LitInfluenceSource`. Les sources sont comptées par `EntityId` : quitter une
Flame ne suspend pas un ennemi encore couvert par une autre. Les notifications
existantes déclenchent les contrôles et un contrôle périodique de 0,25 s
couvre les spawns, déplacements, extinctions et déchargements. En réseau,
seul le serveur calcule l'état ; les clients appliquent l'état reçu.

## Pause et reprise

Les objets, colliders et renderers restent actifs. La dormance interdit les
nouveaux engagements/locks, attaques, réactions et dégâts. Elle ne termine
pas le combat et ne remet pas à zéro les PV, la cible, le trajet NavMesh,
l'action engagée, les segments de boss ou l'arène.

La pause possède sa propre raison dans `CombatTimeDomain`, composée avec une
demande locale de `TimeManager`. Elle ne passe pas par la suspension
cinématique qui annulerait l'action. Le retrait de cette raison ne retire
ni les autres pauses locales ni une suspension cinématique. Animations,
physique, navigation, récupération, fenêtres de réaction et horloges des
boss sont suspendues sans rattrapage du temps écoulé. Une mort acquise reste
définitive. Les tirs de l'Ancre en vol sont également suspendus.

Les séquences de palier arrêtent leurs délais et leur animation joueur
associée pendant la dormance ; les commandes de QTE ne progressent pas.
La suppression automatique d'une Flame Ancienne par proximité d'ennemis
est retirée. Les suppressions narratives explicites restent prises en compte.

## Cycle Étienne et sauvegardes

Les trois Flames `district1.puits.galerie`, `district1.puits.poste` et
`district1.puits.belvedere` démarrent éteintes dans
`District_1_PuitsDeLaReleve_Environment`. Le cycle n'ajoute pas d'autres
Flames dans sa scène propre. Aucun prefab commun ni état initial d'un autre
cycle n'est changé par cette correction. Aucun script n'éteint les Flames
au chargement : la restauration habituelle des sauvegardes est conservée.
Le validateur Étienne contrôle désormais la présence des trois Flames,
leur état initial éteint et la couverture potentielle des interactions.

## Vérification

Tests ciblés : `LitEnemyFlameInfluenceTests` et
`LitCombatTimeDomainPauseTests` (EditMode).

À vérifier en Play dans le projet complet : nouvelle partie et sauvegarde
existante, chaque boss, deux influences superposées, frontière d'influence,
reprise d'une attaque/récupération, Timeline, suppression narrative, spawn
et déchargement, puis serveur et deux clients. La compilation et les tests
unitaires ne remplacent pas ces validations de rendu et de réseau.
