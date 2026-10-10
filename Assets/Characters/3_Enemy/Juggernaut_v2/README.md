# Juggernaut v2 — prototype Brains AI

`Juggernaut_v2.prefab` est une copie independante de `Juggernaut_Combat`.
Le prefab original, sa fiche, ses animations et les instances existantes ne
sont pas remplaces. Le modele, son avatar, ses materiaux, sa capsule et les
dimensions du NavMeshAgent sont conserves.

## Essai

Ouvrir `Juggernaut_v2_Test.unity` sans Bootstrap, puis lancer Play.
Fleches : deplacer la cible ; H : infliger 10 PV ; K : tuer ; R : recreer.
La scene reutilise l'arene d'Iluvilirae, avec un NavMesh propre adapte au rayon
de 0,7 m du Juggernaut.

Pour tester dans Lit, placer le nouveau prefab dans une scene avec NavMesh et
laisser `Detection Target Override` vide. La fiche `Juggernaut_v2.asset`
conserve 300 PV, la vision de 30 m et le cone de 100 degres de l'original.
Comme Iluvilirae, le masque de vision ignore la couche de l'ennemi lui-meme.

## Comportement et animations

`JuggernautV2Brain` et `IluviliraeBrain` reutilisent `LitBrainsEnemy`, derive du
Brain Engager du pack. La vision Lit fournit uniquement la cible visible.
Brains AI decide de la poursuite, de l'investigation et des attaques ; le
NavMeshAgent pilote seul le deplacement. Blessure et attaque suspendent la
locomotion et la rotation, la mort desactive l'ennemi apres quatre secondes.

Le controller propre `Juggernaut_v2.controller` contient Idle/Walk/Run en
BlendTree directionnel Walk/Run (huit directions), trois attaques (Strike,
Sweep, Followup), Hurt et Death. Les 22 clips sont des copies independantes
des animations du Juggernaut. Les courbes de transforms source et les anciens
evenements EnemyAttack/EndEnemyAttack sont retires. Chaque attaque porte
OpenBrainsReactionOpportunity puis ResolveBrainsAttackImpact. Iluvilirae_Test
raccorde ces evenements au combat partage de Lucian, sans ancien moteur ennemi.

Les composants CharacterInfo, EnemyController, NetworkObject, Rigidbody et
l'ancien trigger de detection sont retires de la copie. Le prototype reste
local dans la scene de perception : attaques visuelles, PV de test, aucun degat joueur, recompense,
sauvegarde ou enregistrement automatique dans le combat historique.

Brains AI utilisait Random.Range(1, maxAttacks), dont la borne haute entiere
est exclusive : la derniere attaque ne pouvait pas etre selectionnee. La
borne est maintenant maxAttacks + 1, avec trois IDs Animator distincts.

## Validation

Menu `Lit/Brains AI/Validate Juggernaut V2` ; tests `JuggernautV2Tests`.
Le parcours batch `JuggernautV2Setup.RunSmoke` reutilise le test comportemental
commun : navigation, perception, poursuite, hurt, perte de cible, attaque,
death, immobilite et desactivation. Le rendu et le raccordement au joueur reel
restent a verifier dans Lit ; le projet de validation isole simplifie les
contrats des services de session.

Validation du 2026-10-07 : compilation C# runtime/editeur avec les references
du projet reussie ; cinq tests EditMode passes ; parcours Play Mode isoles
passes pour Juggernaut v2 et Iluvilirae apres mutualisation. Empreintes du
prefab, de la fiche et du controller Juggernaut originaux inchangees.
