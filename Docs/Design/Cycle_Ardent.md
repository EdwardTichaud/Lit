# Ardent — La voix retenue

## Scènes et implantation

`Assets/Scenes/District_1/District_1_ConduitsNoyés_Environement.unity` contient les parcours secs, bassins décoratifs, maçonnerie, garde-corps, sept Flames communes et sortie persistante.
`Assets/Scenes/Cycles/Cycle_Ardent/District_1_Cycle_Ardent.unity` contient les acteurs, preuves, relais, boss et souvenir.
Définition : `Assets/Resources/Narrative/ArdentCycle.asset`, ID `district1.ardent`, catégorie Main, aucun prérequis d'un autre cycle.

Le raccord est calculé depuis le corridor existant et la limite du Puits, à la surface de marche : ancrage monde (-90.50, -116.24, 118.52), prolongement +Z. La limite `ConduitsNoyes_FutureLimit` est conservée inactive dans un groupe d'archives. La véritable porte `FloodedConduits_Blocker` et son activation Étienne sont conservées. Snapshots avant intervention : `Library/ArdentBeforeCreation` (locaux, non versionnés).

La base utilise les instances du pack Dungeon_Environment et des copies locales HDRP, sans conversion globale. L'eau noire est un visuel statique sans collider, nage ni dégâts. Les parcours principaux font quatre mètres de large ; l'arène centrale est un blockout carré de 24 mètres avec BattleWall circulaire de 24 mètres. La forme et l'habillage du bassin pourront être affinés.

## Parcours du joueur

1. Entrer dans le sas : `conduits_entered`, puis réplique de découverte de Lucian.
2. Rencontrer Nora, ou commencer directement les recherches.
3. Récupérer dans n'importe quel ordre le registre de transfert, la consigne des conduits et le ruban d'Iris. Interaction à 2 mètres ; lecture ou récupération accorde la connaissance correspondante.
4. Régler les trois relais, dans n'importe quel ordre. Ces actions ne consomment aucun objet.
5. Rejoindre l'arène avec tous les personnages vivants connectés : boss engagé à 8 mètres seulement si tous sont dans le futur BattleWall.
6. Observer le relais pulsant, l'actionner, puis attaquer le corps révélé. Répéter jusqu'à la mort du Faux Chœur.
7. Interagir au poste du souvenir. Solo : lancement direct après disponibilité des systèmes de présentation. Coop : rassemblement à 8 mètres, confirmation via Interagir, délai de 3 secondes ; Retour annule. La demande expire après 30 secondes. Aucun verrou de contrôle pendant la préparation.
8. Regarder « La voix qui reste », puis revenir parler volontairement à Nora.
9. Nora comprend qu'Iris a quitté la galerie. Dissolution, ouverture permanente de la galerie des processions, puis réplique finale de Lucian.

## Boss : Le Faux Chœur

300 PV, dégâts normaux filtrés hors exposition, HUD standard uniquement, IA et animations ordinaires locales dérivées du socle Juggernaut. Aucun changement aux assets Juggernaut ou BrokenAnchor.
La source active passe 1 → 2 → 3 après chaque fermeture. Une bonne interaction ouvre 8 secondes de vulnérabilité ; cooldown partagé de 12 secondes depuis l'ouverture. Un mauvais relais ne consomme rien et ne retire aucun PV. Les deux leurres sont strictement visuels.
`FalseChoirBoss` et `ArdentEchoRelay` portent toutes les règles spécifiques, sans branche Ardent dans EnemyController.

## Souvenir Timeline

Asset `Timeline/LaVoixQuiReste.playable`, 24 secondes, quatre plans Cinemachine : 0–5, 5–11, 11–17, 17–24 secondes. Pistes Animation, Activation et sous-titres éditables. La piste Audio indique l'emplacement de la voix d'Iris à fournir ; aucune voix originale n'est inventée. Les silhouettes sont des substituts existants, pas des modèles définitifs de Nora et Iris.
La mise en scène ne montre pas la mort d'Iris. Fin réussie seulement : `voice_departure_seen`. Une interruption ne remet pas le boss vivant et permet une nouvelle demande au poste. Les caméras utilisent la passerelle commune ; pas de caméra runtime indépendante.

## Sauvegarde et Dev

Preuves, réglages, victoire, souvenir et Nora sont des jalons persistants. Fenêtres, cooldown, source active et confirmations sont temporaires. Rechargement d'un combat inachevé : pleine santé. Sortie environnementale : `district1.ardent.lower_procession_access`, active après Nora même après déchargement du cycle.
Le bloc Dev de CycleController accepte notamment `transfer_register_read`, `echo_relay_one_tuned`, `false_choir_defeated`, `voice_departure_seen`, `nora_spoken`. État simulé uniquement, sans sauvegarde narrative. Le composant de prévisualisation de sortie respecte cet état sans écrire l'activation réelle.

## Validation

Menu : Lit > Validation > Valider le cycle Ardent. Tests éditeur : `ArdentCycleTests` (permutations, Dev, contrat et fenêtres du boss).
Les contrôles effectivement exécutés et les limitations de cette livraison sont consignés ci-dessous après l'audit final. Un audit de données ne remplace pas une session de jeu solo ou hôte/client, notamment pour les collisions du BattleWall et la restitution du contrôle après Timeline.

### Essais exécutés le 3 octobre 2026

- Audit Ardent : définition indépendante, références, trois preuves à 2 mètres, données Nora et connaissances, boss à 300 PV, deux leurres sans composants de combat/colliders/réseau, liaisons et quatre caméras de Timeline, Flames et sortie persistante.
- 14 cas Ardent exécutés dans l'éditeur : audit, Timeline éditable, six permutations des preuves et relais, quatre départs Dev sans écriture, filtre de dégâts/fenêtre/cooldown/double interaction, impossibilité de démarrer manuellement un boss dormant avant son engagement autorisé.
- 16 cas Étienne passent ; son audit de scène signale une interaction du fantôme hors de la couverture de sa Flame actuelle. La scène de quête Étienne est identique octet pour octet au snapshot avant intervention : ce signalement est préexistant et n'a pas été corrigé en déplaçant son décor.
- NavMesh District_1 rebâti avec ses scènes chargées : chemins complets vers le raccord du Puits, les deux postes, l'arène, les trois relais, le poste du souvenir et l'autre côté de la sortie, testée temporairement ouverte puis restaurée fermée. Asset actif : `Assets/Navigation/NavMeshData/District_1_Ardent_NavMeshData 1.asset`.

À tester en jeu : toutes les interactions et collisions avec un vrai personnage, le ressenti du combat, la prise/restitution de caméra, les sauvegardes à chaque jalon, ainsi que les scénarios hôte/client de vote, départ, arrivée tardive et interruption. Ces sessions n'ont pas été exécutées et ne sont pas garanties par les seuls tests éditeur. Les sons originaux restent à assigner ; le souvenir dispose de sous-titres pour ses informations essentielles.

La Nef des Tours Interrompus reste un accès futur, sans contenu jouable dans cette intervention. Aucune nouvelle Ancient Flame, aucun équipement ni compétence accordés.
