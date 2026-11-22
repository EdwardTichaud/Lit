# Dialogue de Lucian : présentation en corps entier

Le contrat de production est désormais celui de `SimpleLucianAnimationSetup`. Le controller de Lucian possède deux couches : corps entier et bras de torche. Les anciennes couches Dialogue Body, Dialogue Upper Body, Dialogue Free Arm et Moving Legs ont été retirées de ses deux controllers.

## Réglages

Le profil `Assets/Animations/Autres/Dialogue/Lucian/Lucian_DialoguePresentation.asset` conserve les identifiants, sources, intentions et expressions des répliques. Les gestes disponibles référencent des copies complètes du corps ; ils ne superposent plus un buste à des jambes indépendantes. Les marqueurs réseau et générations restent compatibles avec les sessions existantes.

- Conversation narrative : arret progressif UCC, corps fixe et regard de tete limite vers le locuteur ; parole, ecoute puis attente.
- Réplique spontanée en mouvement : voix et expression, locomotion conservée. Le geste devient disponible une fois immobile.
- Reprise du déplacement : restitution de la locomotion, sans verrou de mouvement ajouté par une réplique spontanée.
- Assise : Sit Idle conservé avec voix et expression. Le geste assis complet n'est pas qualifié ; aucune pose debout ne le remplace.
- Torche : seul son bras est masqué. Les autres membres et le bassin appartiennent à la pose complète de la couche principale.
- Combat, mort, traversée et cinématique : priorité sur le dialogue et libération des commandes possédées par sa session.

Les demandes corporelles passent par `PlayerActionPresentationController`, qui possède l'état de dialogue et le libère seulement si personne ne l'a remplacé. Le dialogue ne modifie pas les os, les poids de couches, la cadence de locomotion ou Animator.speed. La bouche fermée et les expressions faciales restent indépendantes.

## Sources

Les gestes de conversation utilisent des copies complètes des sources non Twinblades déjà présentes. Les anciens sous-clips HD Talk_M et _Talk_M restent signalés indisponibles : ils n'offrent pas de véritable geste corporel. Les copies tronquées UpperBody ne sont plus référencées par le profil actif.

La fin de voix ou le délai texte déclenche l'attente attentive. Les choix sont effectués par le propriétaire réseau puis répliqués. Une ancienne fin de réplique ne peut pas annuler la nouvelle session.

## Vérification

Consulter `Docs/Technical/LucianSimpleAnimation.md` pour le contrat de locomotion, les outils de migration et les résultats de validation de ce lot. Les rapports antérieurs de novembre décrivent l'ancien système à masques ; ils ne constituent pas une preuve de validation du système simplifié.

## Placement et regard de sequence

Dans `StorySequenceRunner`, assigner un unique Empty au champ **Player Transform**. Sa position est le point d'arrivee et sa rotation Y l'orientation initiale. Sans reference, aucun trajet n'est ajoute aux sequences existantes.

L'approche utilise un chemin NavMesh complet (type d'agent 0 par defaut) et les commandes de marche UCC. Le NavMesh doit deja etre disponible dans la scene ; aucun NavMeshAgent n'est ajoute au joueur. Les collisions et la gravite restent gerees par UCC. La preparation attend une distance horizontale de 15 cm, une hauteur compatible et une vitesse inferieure a 0,05 m/s pendant trois ticks physiques, puis une orientation a 2 degres pres. Delai maximal : 30 secondes ; blocage : deux secondes sans progression. Le runner peut etre annule pendant cette phase.

Les replique, cadrages et evenements de debut attendent la fin du placement. Un echec ne consomme pas le declencheur : sortir puis rentrer permet de reessayer. IsPreparingPlayer expose cette phase ; IsPlaying couvre aussi la reservation et la preparation. PlaybackStarted et PreparationFailed permettent aux declencheurs de distinguer les resultats.

Pendant la conversation stationnaire, Lucian conserve son orientation physique. Le CharacterIK UCC existant dirige sa tete vers une direction memorisee par changement de locuteur, limitee horizontalement a +/-45 degres. Le buste ne participe pas a ce regard. Une nouvelle replique du meme locuteur, une pause et la propre parole de Lucian conservent la direction precedente. Un locuteur non resolu ne remplace pas le dernier regard valide. Les observateurs reseau recoivent le meme instantane ; ils ne recalculent pas la cible independamment.

Les actions prioritaires suspendent le regard ; une Timeline libere la session et le dialogue suivant en ouvre une nouvelle sur la pose physique courante. A la fin ou a l'interruption, les poids IK anterieurs, les verrous et les commandes sont restitues. Aucun masque, solveur de pieds, Root Motion ou ajustement de cadence n'est ajoute.

Verification manuelle : `Lit > Story > Validate Lucian Placement In Play Mode` utilise temporairement Iluvilirae_Test et restaure la scene de demarrage Play de l'Editor. Il ne sauvegarde pas les modifications de scene.
