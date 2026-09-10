# Modules du joueur

## Configurer un personnage

1. Ouvrir sa fiche CharacterData et la rubrique Modules du joueur.
2. Regler les categories Controle et locomotion, Saut et atterrissage, Esquive et mouvements d action, Combat et equipement, Animation et presentation, Interactions et suivi, Voix/visage/pas et Diagnostics.
3. Conserver sur le prefab uniquement les points, os, renderers et references Unity propres a l instance. Les adaptateurs retrouvent leurs dependances internes automatiquement.
4. Garder la fiche assignee dans CharacterInfo et SquadCharacterController avant l activation des modules. Les prefabs Link, Lucian, Luna et Mia sont migres.
5. Pour une variante avec un comportement different, utiliser une fiche distincte sans modifier les identifiants de sauvegarde d un personnage existant.

PlayerModuleConfiguration copie le bloc de chaque module, y compris ses listes, et conserve les references aux ressources partagees. Les modifications runtime ne changent pas l asset. Un changement de personnage invalide le cache de configuration. Les ressources de voix restent dans CharacterData.voiceLines; les profils de competences et les trajectoires restent des ressources partagees.

## Responsabilites

UCC reste le moteur physique. Les reservations planaires prennent un proprietaire explicite dans Begin/Drive/ApplyImpulse/EndScriptedPlanarMotion. Un autre module ne peut ni prendre, ni piloter, ni liberer la reservation. La desactivation du bridge et les changements de zone la reinitialisent.

PlayerCombatAnimationEvents conserve les noms des evenements des clips et ne fait que router. PlayerActionPresentationController execute armes, effets et impacts; CombatMobilityController execute l impulsion/freinage du dash. PlayerAnimationController ne contient plus de branche de physique ennemie. PlayerRootMotionRelay conserve son role sur l Animator pour les cinematiques.

Le saut, l esquive et les trajectoires restent des modules distincts; leur desactivation rend leurs points d entree inactifs. Le visage et les sons de pas ne dependent pas du combat. Le debugger facial runtime est remplace par les commandes de l inspecteur du visage.

La sante garde son autorite configuree dans LitUccDamageBridge : CharacterHealth en mode UCC, puis notification vers la squad; le chemin historique demeure pour le mode sans autorite UCC. Aucune migration des PV dans CharacterInfo.

## Validation

Compilation C# runtime et editeur avec le compilateur Unity 6000.4.9f1 : reussie. YAML et composition des quatre prefabs et de la scene AnimationLab controles. Les GUID des scripts sont resolus, y compris les dependances HDRP du cache de packages.

Tests ajoutes : isolation des copies, changement de personnage, refus d un proprietaire de mouvement etranger, composition des prefabs. Tests compiles, mais non executes dans Unity. Restent a executer : import/ILPP final, tests EditMode/PlayMode, parcours de locomotion/combat/interactions, reseau et Domain Reload dans les deux modes.

Le journal de l editeur ouvert rapporte un prefab Lucian avec script manquant lors de son auto-enregistrement. Les references sur disque sont presentes; recharger le prefab ouvert apres import avant de l enregistrer pour ne pas utiliser son ancien etat en memoire.

Les valeurs auteur ont ete conservees, notamment la hauteur de saut de Lucian (100), distincte des trois autres personnages (5). Ce changement ne retouche pas le ressenti de jeu demande par ces valeurs.
