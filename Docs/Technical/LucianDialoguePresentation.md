# Dialogue de Lucian : présentation en corps entier

Le contrat de production est désormais celui de `SimpleLucianAnimationSetup`. Le controller de Lucian possède deux couches : corps entier et bras de torche. Les anciennes couches Dialogue Body, Dialogue Upper Body, Dialogue Free Arm et Moving Legs ont été retirées de ses deux controllers.

## Réglages

Le profil `Assets/Animations/Autres/Dialogue/Lucian/Lucian_DialoguePresentation.asset` conserve les identifiants, sources, intentions et expressions des répliques. Les gestes disponibles référencent des copies complètes du corps ; ils ne superposent plus un buste à des jambes indépendantes. Les marqueurs réseau et générations restent compatibles avec les sessions existantes.

- Conversation narrative : arrêt progressif UCC et orientation physique vers l'interlocuteur ; parole, écoute puis attente.
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
