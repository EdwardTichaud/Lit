# Configurer un ennemi

Le prefab porte un CharacterInfo et un EnemyController sur la racine de son Animator de gameplay. Rigidbody, capsule et NavMeshAgent restent des composants Unity. VisionField, GhostController, narration, temps local et présentation de mort restent des services distincts.

1. Dupliquer la fiche CharacterData et le prefab de l'ennemi le plus proche du résultat voulu. Assigner le nouveau prefab à worldPrefab et sa fiche à CharacterInfo.
2. Modifier les statistiques, compétences et catégories de réglages dans CharacterData. Le profil de combat référencé reste le lieu des patterns, poids, distances de poursuite, garde et délais.
3. Vérifier que chaque compétence équipée possède son animation et ses événements. EnemyAttack(SkillSO) produit l'impact; EndEnemyAttack termine l'action. Conserver les événements de mouvement pour les attaques aériennes.
4. Sur EnemyController, activer le mode combat initial pour un ennemi hostile. Pour un personnage initialement Ghost, le handler narratif passe CombatEnabled à true au moment prévu. Le scientifique conserve son introduction existante.
5. Vérifier le placement du marker, le NavMesh et le contrat Animator/capsule/corps avant de tester le combat.

Au lancement, CharacterInfo copie CharacterData et son profil. Les statistiques, listes et réglages structurés appartiennent à l'instance; les références aux compétences, portraits et autres ressources restent partagées. SourceData désigne toujours la fiche auteur. La santé restaurée, même à zéro, ne repasse pas à pleine vie à la réactivation. La santé spécifique de l'escouade n'est pas remplacée.

EnemyController est une classe partial : Actor, Brain, Navigation, Locomotion, Physics, Skills, Animation, AnimationEvents, Recovery, Cinematic et Contract. Il n'y a qu'un composant dans Unity. CharacterAnimationController est le contrat abstrait commun; PlayerAnimationController, PlayerRootMotionRelay et PlayerCombatAnimationEvents servent au joueur.

## Migration et compatibilité

Les scripts sources supprimés ne sont pas conservés sous forme de composants de compatibilité. Les références sérialisées sont transférées à l'unique contrôleur. Les GUID des ressources conservées et les identifiants des markers ne changent pas. GiantJuggernaut reçoit une fiche dédiée car il partageait la fiche du Juggernaut avec des réglages de prefab différents.

Le menu Tools > Lit > Enemies > Migrate legacy enemy resources relance la migration des ressources anciennes. Il utilise Python 3 et Git, déjà disponibles sur ce poste. Les tables et scripts sont dans Tools/EnemyUnification; le rapport va dans Library/EnemyUnification. Une ressource déjà migrée est ignorée. Ne pas utiliser cette commande en Play Mode.

Les RPC du scientifique conservent leurs règles d'autorité. Les décisions et la physique de combat sont locales en solo, et serveur après spawn en réseau. La composition des NetworkBehaviour évolue avec le contrat commun d'animation : toutes les machines d'une session doivent utiliser la même version des prefabs. L'import Netcode/ILPP et les parcours réseau restent à valider dans Unity.

## Validation de cette intervention

Effectué : compilation C# runtime et éditeur avec le compilateur fourni par Unity 6000.4.9f1, tests inclus à la compilation; analyse YAML et contrôle des références locales des ressources modifiées.

Tests ajoutés/préparés : restauration de santé à zéro, santé sans fiche, indépendance de deux copies runtime, absence des anciens composants sur les trois prefabs, rejet d'une fin d'action périmée. Tests existants adaptés aux noms du contrôleur et aux méthodes privées déplacées.

Non exécuté : import/compilation Unity avec ILPP, exécution EditMode/PlayMode, affichage des inspecteurs, parcours complet Juggernaut/scientifique, hôte/client distant et arrivée tardive, sauvegarde existante, Domain Reload activé/désactivé. L'éditeur ouvert n'a pas été fermé; son contrôle natif n'est pas disponible dans cette session. Une compilation C# réussie ne clôture pas ces validations.
